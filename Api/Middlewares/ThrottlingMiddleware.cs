using Core.Helpers;
using Core.Interfaces;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Api.Middlewares;

public class ThrottlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ThrottlingMiddleware> _logger;
    private readonly ThrottlingSettings _settings;
    private readonly IHostEnvironment _env;

    public ThrottlingMiddleware(
        RequestDelegate next,
        ILogger<ThrottlingMiddleware> logger,
        IOptions<ThrottlingSettings> settings,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _settings = settings.Value;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context, ICacheRepository cache)
    {
        // Skip all throttling in Testing environment
        if (_env.IsEnvironment("Testing"))
        {
            await _next(context);
            return;
        }

        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var path = context.Request.Path.Value?.ToLower() ?? "";
        var method = context.Request.Method;

        // 1. Check global IP throttling first
        var globalResult = await CheckThrottling(
            cache, $"throttle:global:{ipAddress}", _settings.Global);

        if (globalResult.Status == ThrottleStatus.Blocked)
        {
            _logger.LogWarning(
                "Global rate limit reached for IP: {IpAddress} on {Method} {Path}",
                ipAddress, method, path);
            await WriteRateLimitResponse(context,
                "Too many requests. Please wait before trying again.",
                globalResult);
            return;
        }

        if (globalResult.Status == ThrottleStatus.Throttled)
        {
            var delay = await GetCurrentDelay(cache, $"throttle:global:{ipAddress}", _settings.Global);
            _logger.LogInformation(
                "Throttling IP: {IpAddress} - {Delay}ms delay on {Method} {Path}",
                ipAddress, delay, method, path);

            AddThrottleHeaders(context, _settings.Global, delay);
            await Task.Delay(delay);
        }

        // 2. Check authenticated user throttling
        var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId != null)
        {
            var userResult = await CheckThrottling(
                cache, $"throttle:user:{userId}", _settings.User);

            if (userResult.Status == ThrottleStatus.Blocked)
            {
                _logger.LogWarning(
                    "User rate limit reached for User: {UserId} on {Method} {Path}",
                    userId, method, path);
                await WriteRateLimitResponse(context,
                    "Too many requests. Please slow down.",
                    userResult);
                return;
            }

            if (userResult.Status == ThrottleStatus.Throttled)
            {
                var delay = await GetCurrentDelay(cache, $"throttle:user:{userId}", _settings.User);
                _logger.LogInformation(
                    "Throttling User: {UserId} - {Delay}ms delay on {Method} {Path}",
                    userId, delay, method, path);

                AddThrottleHeaders(context, _settings.User, delay);
                await Task.Delay(delay);
            }
        }

        // 3. Check auth endpoint throttling (strictest)
        if (IsAuthEndpoint(path))
        {
            var authResult = await CheckThrottling(
                cache, $"throttle:auth:{ipAddress}:{path}", _settings.Auth);

            if (authResult.Status == ThrottleStatus.Blocked)
            {
                _logger.LogWarning(
                    "Auth rate limit reached for IP: {IpAddress} on {Method} {Path}",
                    ipAddress, method, path);
                await WriteRateLimitResponse(context,
                    "Too many authentication attempts. Please try again later.",
                    authResult);
                return;
            }

            if (authResult.Status == ThrottleStatus.Throttled)
            {
                var delay = await GetCurrentDelay(cache, $"throttle:auth:{ipAddress}:{path}", _settings.Auth);
                _logger.LogWarning(
                    "Throttling auth for IP: {IpAddress} - {Delay}ms delay on {Method} {Path}",
                    ipAddress, delay, method, path);

                AddThrottleHeaders(context, _settings.Auth, delay);
                await Task.Delay(delay);
            }
        }

        await _next(context);
    }

    #region Throttle Logic

    private static async Task<ThrottleInfo> CheckThrottling(
        ICacheRepository cache, string key, ThrottleTier tier)
    {
        var penaltyKey = $"{key}:penalty";       // stores Unix timestamp of penalty start
        var penaltyUsedKey = $"{key}:penalty_used"; // tracks total attempts used during penalty

        var penaltyStartRaw = await cache.GetAsync<long?>(penaltyKey);

        if (penaltyStartRaw.HasValue)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var elapsedSeconds = (int)(now - penaltyStartRaw.Value);
            var elapsedMinutes = elapsedSeconds / 60;
            var allowedAttempts = elapsedMinutes; // 0 in first min, 1 after 1 min, 2 after 2 min...
            var penaltyRemaining = Math.Max(0, tier.PenaltySeconds - elapsedSeconds);
            var secondsUntilNextRelease = 60 - (elapsedSeconds % 60);

            var usedAttempts = await cache.GetAsync<int?>(penaltyUsedKey) ?? 0;
            var remaining = Math.Max(0, allowedAttempts - usedAttempts);

            if (usedAttempts >= allowedAttempts)
            {
                if (allowedAttempts > 0)
                {
                    // Reset penalty — recalculate from new start
                    var resetNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    await cache.SetAsync(penaltyKey, resetNow, TimeSpan.FromSeconds(tier.PenaltySeconds));
                    await cache.RemoveAsync(penaltyUsedKey);
                    penaltyRemaining = tier.PenaltySeconds;
                    secondsUntilNextRelease = 60;
                }

                return new ThrottleInfo
                {
                    Status = ThrottleStatus.Blocked,
                    InPenalty = true,
                    RemainingAttempts = 0,
                    PenaltyRemainingSeconds = penaltyRemaining,
                    NextAttemptInSeconds = secondsUntilNextRelease
                };
            }

            // Consume one released attempt
            await cache.IncrementAsync(penaltyUsedKey, TimeSpan.FromSeconds(tier.PenaltySeconds));
            return new ThrottleInfo
            {
                Status = ThrottleStatus.Allowed,
                InPenalty = true,
                RemainingAttempts = remaining - 1, // just consumed one
                PenaltyRemainingSeconds = penaltyRemaining,
                NextAttemptInSeconds = remaining - 1 > 0 ? 0 : secondsUntilNextRelease
            };
        }

        // Normal mode
        var requestCount = await IncrementCounter(cache, key, tier.WindowSeconds);

        if (requestCount > tier.MaxRequestsPerMinute)
        {
            var nowTs = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await cache.SetAsync(penaltyKey, nowTs, TimeSpan.FromSeconds(tier.PenaltySeconds));
            return new ThrottleInfo
            {
                Status = ThrottleStatus.Blocked,
                InPenalty = true,
                RemainingAttempts = 0,
                PenaltyRemainingSeconds = tier.PenaltySeconds,
                NextAttemptInSeconds = 60
            };
        }

        var normalRemaining = tier.MaxRequestsPerMinute - requestCount;

        if (requestCount > tier.ThrottleThreshold)
            return new ThrottleInfo
            {
                Status = ThrottleStatus.Throttled,
                InPenalty = false,
                RemainingAttempts = normalRemaining
            };

        return new ThrottleInfo
        {
            Status = ThrottleStatus.Allowed,
            InPenalty = false,
            RemainingAttempts = normalRemaining
        };
    }

    private static async Task<int> GetCurrentDelay(
        ICacheRepository cache, string key, ThrottleTier tier)
    {
        var currentCount = await cache.GetAsync<int?>(key) ?? 0;
        return CalculateDelay(currentCount, tier.ThrottleThreshold, tier.MaxRequestsPerMinute, tier.MaxDelayMs);
    }

    /// <summary>
    /// Calculate progressive delay using exponential backoff
    /// </summary>
    private static int CalculateDelay(int currentCount, int threshold, int maxRequests, int maxDelayMs)
    {
        if (maxDelayMs <= 0) return 0;

        var overageRatio = (double)(currentCount - threshold) / (maxRequests - threshold);
        overageRatio = Math.Clamp(overageRatio, 0.0, 1.0);

        var delay = (int)(maxDelayMs * Math.Pow(overageRatio, 2));
        return Math.Clamp(delay, 0, maxDelayMs);
    }

    /// <summary>
    /// Increment request counter in Redis and return current count
    /// </summary>
    private static async Task<int> IncrementCounter(ICacheRepository cache, string key, int windowSeconds)
    {
        // Atomic increment — TTL is only set when the key is first created
        var count = await cache.IncrementAsync(key, TimeSpan.FromSeconds(windowSeconds));
        return (int)count;
    }

    #endregion

    #region Helpers

    private static bool IsAuthEndpoint(string path)
    {
        return path.Contains("/auth/login") || path.Contains("/auth/register");
    }

    private static void AddThrottleHeaders(HttpContext context, ThrottleTier tier, int delay)
    {
        context.Response.Headers.Append("X-RateLimit-Limit", tier.MaxRequestsPerMinute.ToString());
        context.Response.Headers.Append("X-RateLimit-Remaining",
            Math.Max(0, tier.MaxRequestsPerMinute - delay).ToString());
        context.Response.Headers.Append("X-Throttle-Delay", delay.ToString());
    }

    private static async Task WriteRateLimitResponse(
        HttpContext context, string message, ThrottleInfo info)
    {
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.ContentType = "application/json";
        context.Response.Headers.Append("Retry-After", info.NextAttemptInSeconds.ToString());

        var response = new
        {
            StatusCode = 429,
            Message = message,
            info.RemainingAttempts,
            info.NextAttemptInSeconds,
            PenaltyRemainingSeconds = info.InPenalty ? info.PenaltyRemainingSeconds : (int?)null,
            info.InPenalty
        };

        await context.Response.WriteAsJsonAsync(response);
    }

    #endregion
}

internal enum ThrottleStatus
{
    Allowed,
    Throttled,
    Blocked
}

internal class ThrottleInfo
{
    public ThrottleStatus Status { get; init; }
    public bool InPenalty { get; init; }
    public int RemainingAttempts { get; init; }
    public int PenaltyRemainingSeconds { get; init; }
    public int NextAttemptInSeconds { get; init; }
}
