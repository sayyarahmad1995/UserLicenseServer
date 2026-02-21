using Api.Middlewares;
using Core.Helpers;
using FluentAssertions;
using Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Tests.Middlewares;

public class ThrottlingMiddlewareTests
{
    private readonly Mock<ILogger<ThrottlingMiddleware>> _loggerMock = new();
    private readonly Mock<IHostEnvironment> _envMock = new();

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Helper: creates the middleware with given settings and a non-Testing environment.
    /// </summary>
    private ThrottlingMiddleware CreateMiddleware(
        RequestDelegate next, ThrottlingSettings? settings = null)
    {
        var s = settings ?? DefaultSettings();
        _envMock.Setup(e => e.EnvironmentName).Returns("Development");

        return new ThrottlingMiddleware(
            next,
            _loggerMock.Object,
            Options.Create(s),
            _envMock.Object);
    }

    /// <summary>
    /// Default Auth tier: 3 threshold, 5 max, 60s window, 300s penalty.
    /// </summary>
    private static ThrottlingSettings DefaultSettings() => new()
    {
        Global = new ThrottleTier
        {
            ThrottleThreshold = 30,
            MaxRequestsPerMinute = 60,
            WindowSeconds = 60,
            MaxDelayMs = 0, // no delay in tests
            PenaltySeconds = 300
        },
        Auth = new ThrottleTier
        {
            ThrottleThreshold = 3,
            MaxRequestsPerMinute = 5,
            WindowSeconds = 60,
            MaxDelayMs = 0, // no delay in tests
            PenaltySeconds = 300
        },
        User = new ThrottleTier
        {
            ThrottleThreshold = 60,
            MaxRequestsPerMinute = 120,
            WindowSeconds = 60,
            MaxDelayMs = 0,
            PenaltySeconds = 900
        }
    };

    /// <summary>
    /// Creates an HttpContext for a given path with an IP address.
    /// </summary>
    private static DefaultHttpContext CreateHttpContext(string path = "/api/v1/test", string ip = "127.0.0.1")
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = "POST";
        context.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        context.Response.Body = new MemoryStream();
        return context;
    }

    /// <summary>
    /// Reads the JSON response body from the context.
    /// </summary>
    private static async Task<JsonElement> ReadResponseBody(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();
        return JsonSerializer.Deserialize<JsonElement>(body, _jsonOptions);
    }

    /// <summary>
    /// Creates an in-memory cache that behaves like Redis for throttle testing.
    /// Supports Get, Set, Remove, Exists, Increment with TTL tracking.
    /// </summary>
    private static InMemoryTestCache CreateCache() => new();

    #region Normal Mode Tests

    [Fact]
    public async Task RequestUnderThreshold_ShouldPassThrough()
    {
        // Arrange
        var nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var cache = CreateCache();
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context, cache);

        // Assert
        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task AuthEndpoint_UnderThreshold_ShouldPassThrough()
    {
        // Arrange
        var nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var cache = CreateCache();
        var context = CreateHttpContext("/api/v1/auth/login");

        // Act — first request
        await middleware.InvokeAsync(context, cache);

        // Assert
        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task AuthEndpoint_ExceedingMax_ShouldReturn429()
    {
        // Arrange: Auth tier max = 5
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var cache = CreateCache();

        // Act — send 6 requests (exceeding max of 5)
        HttpContext? lastContext = null;
        for (var i = 0; i < 6; i++)
        {
            lastContext = CreateHttpContext("/api/v1/auth/login");
            await middleware.InvokeAsync(lastContext, cache);
        }

        // Assert — 6th request should be blocked
        lastContext!.Response.StatusCode.Should().Be(429);
        var body = await ReadResponseBody(lastContext);
        body.GetProperty("inPenalty").GetBoolean().Should().BeTrue();
        body.GetProperty("remainingAttempts").GetInt32().Should().Be(0);
        body.GetProperty("nextAttemptInSeconds").GetInt32().Should().Be(60);
    }

    [Fact]
    public async Task AuthEndpoint_AtExactMax_ShouldStillPass()
    {
        // Arrange: Auth tier max = 5
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var cache = CreateCache();

        // Act — send exactly 5 requests
        HttpContext? lastContext = null;
        for (var i = 0; i < 5; i++)
        {
            lastContext = CreateHttpContext("/api/v1/auth/login");
            await middleware.InvokeAsync(lastContext, cache);
        }

        // Assert — 5th request is at the max but not over
        lastContext!.Response.StatusCode.Should().NotBe(429);
    }

    [Fact]
    public async Task GlobalThrottle_ExceedingMax_ShouldReturn429()
    {
        // Arrange: Global max = 60, but we use a smaller setting for practicality
        var settings = DefaultSettings();
        settings.Global.MaxRequestsPerMinute = 3;
        settings.Global.ThrottleThreshold = 2;
        var middleware = CreateMiddleware(_ => Task.CompletedTask, settings);
        var cache = CreateCache();

        // Act — 4 requests on a non-auth path
        HttpContext? lastContext = null;
        for (var i = 0; i < 4; i++)
        {
            lastContext = CreateHttpContext("/api/v1/users");
            await middleware.InvokeAsync(lastContext, cache);
        }

        // Assert
        lastContext!.Response.StatusCode.Should().Be(429);
        var body = await ReadResponseBody(lastContext);
        body.GetProperty("message").GetString().Should().Contain("Too many requests");
    }

    #endregion

    #region Penalty Mode Tests

    [Fact]
    public async Task Penalty_FirstMinute_ShouldBlockAllRequests()
    {
        // Arrange: trigger penalty, then try again immediately
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var cache = CreateCache();

        // Exceed auth limit (5 max) → triggers penalty
        for (var i = 0; i < 6; i++)
            await middleware.InvokeAsync(CreateHttpContext("/api/v1/auth/login"), cache);

        // Act — try one more immediately (within first minute of penalty)
        var context = CreateHttpContext("/api/v1/auth/login");
        await middleware.InvokeAsync(context, cache);

        // Assert — should be blocked (0 released attempts in first minute)
        context.Response.StatusCode.Should().Be(429);
        var body = await ReadResponseBody(context);
        body.GetProperty("inPenalty").GetBoolean().Should().BeTrue();
        body.GetProperty("remainingAttempts").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Penalty_AfterOneMinute_ShouldReleaseOneAttempt()
    {
        // Arrange
        var cache = CreateCache();
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        // Trigger penalty
        for (var i = 0; i < 6; i++)
            await middleware.InvokeAsync(CreateHttpContext("/api/v1/auth/login"), cache);

        // Simulate 65 seconds passing by adjusting the penalty timestamp
        var penaltyKey = "throttle:auth:127.0.0.1:/api/v1/auth/login:penalty";
        var pastTimestamp = DateTimeOffset.UtcNow.AddSeconds(-65).ToUnixTimeSeconds();
        cache.ForceSet(penaltyKey, pastTimestamp, TimeSpan.FromSeconds(300));

        // Act — first request after 1 min should pass
        var context1 = CreateHttpContext("/api/v1/auth/login");
        await middleware.InvokeAsync(context1, cache);

        // Assert — should be allowed (1 released attempt available)
        context1.Response.StatusCode.Should().NotBe(429);
    }

    [Fact]
    public async Task Penalty_AfterOneMinute_SecondRequestShouldBeBlocked()
    {
        // Arrange
        var cache = CreateCache();
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        // Trigger penalty
        for (var i = 0; i < 6; i++)
            await middleware.InvokeAsync(CreateHttpContext("/api/v1/auth/login"), cache);

        // Simulate 65 seconds passing
        var penaltyKey = "throttle:auth:127.0.0.1:/api/v1/auth/login:penalty";
        var pastTimestamp = DateTimeOffset.UtcNow.AddSeconds(-65).ToUnixTimeSeconds();
        cache.ForceSet(penaltyKey, pastTimestamp, TimeSpan.FromSeconds(300));

        // First request uses the 1 released attempt
        await middleware.InvokeAsync(CreateHttpContext("/api/v1/auth/login"), cache);

        // Act — second request should be blocked (only 1 released)
        var context2 = CreateHttpContext("/api/v1/auth/login");
        await middleware.InvokeAsync(context2, cache);

        // Assert — blocked, and penalty should be reset
        context2.Response.StatusCode.Should().Be(429);
    }

    [Fact]
    public async Task Penalty_AfterTwoMinutes_ShouldReleaseTwoAttempts()
    {
        // Arrange
        var cache = CreateCache();
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        // Trigger penalty
        for (var i = 0; i < 6; i++)
            await middleware.InvokeAsync(CreateHttpContext("/api/v1/auth/login"), cache);

        // Simulate 125 seconds (2+ minutes) elapsed
        var penaltyKey = "throttle:auth:127.0.0.1:/api/v1/auth/login:penalty";
        var pastTimestamp = DateTimeOffset.UtcNow.AddSeconds(-125).ToUnixTimeSeconds();
        cache.ForceSet(penaltyKey, pastTimestamp, TimeSpan.FromSeconds(300));

        // Act — first and second requests should both pass
        var ctx1 = CreateHttpContext("/api/v1/auth/login");
        await middleware.InvokeAsync(ctx1, cache);
        ctx1.Response.StatusCode.Should().NotBe(429);

        var ctx2 = CreateHttpContext("/api/v1/auth/login");
        await middleware.InvokeAsync(ctx2, cache);
        ctx2.Response.StatusCode.Should().NotBe(429);

        // Third should be blocked (only 2 released)
        var ctx3 = CreateHttpContext("/api/v1/auth/login");
        await middleware.InvokeAsync(ctx3, cache);
        ctx3.Response.StatusCode.Should().Be(429);
    }

    [Fact]
    public async Task Penalty_ExhaustReleasedAttempts_ShouldResetPenalty()
    {
        // Arrange
        var cache = CreateCache();
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        // Trigger penalty
        for (var i = 0; i < 6; i++)
            await middleware.InvokeAsync(CreateHttpContext("/api/v1/auth/login"), cache);

        // Simulate 65 seconds (1 min passed, 1 attempt released)
        var penaltyKey = "throttle:auth:127.0.0.1:/api/v1/auth/login:penalty";
        var originalTimestamp = DateTimeOffset.UtcNow.AddSeconds(-65).ToUnixTimeSeconds();
        cache.ForceSet(penaltyKey, originalTimestamp, TimeSpan.FromSeconds(300));

        // Use the one released attempt
        await middleware.InvokeAsync(CreateHttpContext("/api/v1/auth/login"), cache);

        // Trigger reset by trying again (exhausted released, allowedAttempts > 0)
        await middleware.InvokeAsync(CreateHttpContext("/api/v1/auth/login"), cache);

        // Act — verify penalty was reset: the penalty timestamp should be fresh
        var newTimestamp = cache.GetRaw<long>(penaltyKey);
        newTimestamp.Should().BeGreaterThan(originalTimestamp);

        // And penalty_used should be cleared
        var penaltyUsedKey = "throttle:auth:127.0.0.1:/api/v1/auth/login:penalty_used";
        var usedExists = cache.RawExists(penaltyUsedKey);
        usedExists.Should().BeFalse();
    }

    [Fact]
    public async Task Penalty_FirstMinuteBlock_ShouldNotResetPenalty()
    {
        // Arrange
        var cache = CreateCache();
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        // Trigger penalty
        for (var i = 0; i < 6; i++)
            await middleware.InvokeAsync(CreateHttpContext("/api/v1/auth/login"), cache);

        // Get the initial penalty timestamp
        var penaltyKey = "throttle:auth:127.0.0.1:/api/v1/auth/login:penalty";
        var initialTimestamp = cache.GetRaw<long>(penaltyKey);

        // Act — hit again in first minute (0 released)
        var context = CreateHttpContext("/api/v1/auth/login");
        await middleware.InvokeAsync(context, cache);

        // Assert — penalty timestamp should NOT change
        var afterTimestamp = cache.GetRaw<long>(penaltyKey);
        afterTimestamp.Should().Be(initialTimestamp);
    }

    #endregion

    #region Response Format Tests

    [Fact]
    public async Task BlockedResponse_ShouldIncludeAllPenaltyFields()
    {
        // Arrange
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var cache = CreateCache();

        // Exceed auth limit
        HttpContext? blockedContext = null;
        for (var i = 0; i < 6; i++)
        {
            blockedContext = CreateHttpContext("/api/v1/auth/login");
            await middleware.InvokeAsync(blockedContext, cache);
        }

        // Assert
        blockedContext!.Response.StatusCode.Should().Be(429);
        blockedContext.Response.ContentType.Should().Contain("application/json");
        blockedContext.Response.Headers.Should().ContainKey("Retry-After");

        var body = await ReadResponseBody(blockedContext);
        body.GetProperty("statusCode").GetInt32().Should().Be(429);
        body.GetProperty("message").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("remainingAttempts").GetInt32().Should().Be(0);
        body.GetProperty("inPenalty").GetBoolean().Should().BeTrue();
        body.GetProperty("penaltyRemainingSeconds").GetInt32().Should().BeGreaterThan(0);
        body.GetProperty("nextAttemptInSeconds").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task NonPenaltyBlock_ShouldNotIncludePenaltyRemainingAsNull()
    {
        // This tests that the first 429 (entering penalty) still shows InPenalty=true
        var settings = DefaultSettings();
        settings.Global.MaxRequestsPerMinute = 2;
        settings.Global.ThrottleThreshold = 1;
        var middleware = CreateMiddleware(_ => Task.CompletedTask, settings);
        var cache = CreateCache();

        // Exceed global limit
        HttpContext? lastCtx = null;
        for (var i = 0; i < 3; i++)
        {
            lastCtx = CreateHttpContext("/api/v1/users");
            await middleware.InvokeAsync(lastCtx, cache);
        }

        var body = await ReadResponseBody(lastCtx!);
        body.GetProperty("inPenalty").GetBoolean().Should().BeTrue();
        body.GetProperty("penaltyRemainingSeconds").GetInt32().Should().Be(settings.Global.PenaltySeconds);
    }

    #endregion

    #region Testing Environment Tests

    [Fact]
    public async Task TestingEnvironment_ShouldSkipAllThrottling()
    {
        // Arrange
        var nextCalled = false;
        _envMock.Setup(e => e.EnvironmentName).Returns("Testing");
        var middleware = new ThrottlingMiddleware(
            _ => { nextCalled = true; return Task.CompletedTask; },
            _loggerMock.Object,
            Options.Create(DefaultSettings()),
            _envMock.Object);

        var cache = CreateCache();

        // Act — even 100 requests should all pass
        for (var i = 0; i < 100; i++)
        {
            nextCalled = false;
            var ctx = CreateHttpContext("/api/v1/auth/login");
            await middleware.InvokeAsync(ctx, cache);
            nextCalled.Should().BeTrue();
        }
    }

    #endregion

    #region Tier Isolation Tests

    [Fact]
    public async Task AuthAndGlobal_ShouldHaveSeparateCounters()
    {
        // Arrange: Auth max=5, Global max=60
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var cache = CreateCache();

        // Send 5 requests to auth endpoint (reaches auth max)
        for (var i = 0; i < 5; i++)
            await middleware.InvokeAsync(CreateHttpContext("/api/v1/auth/login"), cache);

        // 6th should be blocked by auth tier
        var authBlocked = CreateHttpContext("/api/v1/auth/login");
        await middleware.InvokeAsync(authBlocked, cache);
        authBlocked.Response.StatusCode.Should().Be(429);

        // But a non-auth request should still pass (global counter is only at 6)
        var normalCtx = CreateHttpContext("/api/v1/users");
        var nextCalled = false;
        var middleware2 = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        await middleware2.InvokeAsync(normalCtx, cache);
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task DifferentIPs_ShouldHaveSeparateCounters()
    {
        // Arrange
        var settings = DefaultSettings();
        settings.Global.MaxRequestsPerMinute = 3;
        var middleware = CreateMiddleware(_ => Task.CompletedTask, settings);
        var cache = CreateCache();

        // IP1 sends 3 requests (at max)
        for (var i = 0; i < 3; i++)
            await middleware.InvokeAsync(CreateHttpContext("/api/v1/test", "10.0.0.1"), cache);

        // IP1's 4th should be blocked
        var ip1Blocked = CreateHttpContext("/api/v1/test", "10.0.0.1");
        await middleware.InvokeAsync(ip1Blocked, cache);
        ip1Blocked.Response.StatusCode.Should().Be(429);

        // IP2's first request should be allowed
        var ip2Ctx = CreateHttpContext("/api/v1/test", "10.0.0.2");
        var nextCalled = false;
        var middleware2 = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, settings);
        await middleware2.InvokeAsync(ip2Ctx, cache);
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task LoginAndRegister_ShouldHaveSeparateAuthCounters()
    {
        // Arrange: Auth max=5
        var middleware = CreateMiddleware(_ => Task.CompletedTask);
        var cache = CreateCache();

        // Send 5 login requests (at max)
        for (var i = 0; i < 5; i++)
            await middleware.InvokeAsync(CreateHttpContext("/api/v1/auth/login"), cache);

        // 6th login should be blocked
        var loginBlocked = CreateHttpContext("/api/v1/auth/login");
        await middleware.InvokeAsync(loginBlocked, cache);
        loginBlocked.Response.StatusCode.Should().Be(429);

        // But a register request should still pass (separate counter)
        var registerCtx = CreateHttpContext("/api/v1/auth/register");
        var nextCalled = false;
        var middleware2 = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        await middleware2.InvokeAsync(registerCtx, cache);
        nextCalled.Should().BeTrue();
    }

    #endregion
}
