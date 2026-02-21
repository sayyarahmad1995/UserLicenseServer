using Api.Middlewares;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Tests.Middlewares;

public class SecurityHeadersMiddlewareTests
{
    private readonly SecurityHeadersMiddleware _middleware;
    private bool _nextCalled;

    public SecurityHeadersMiddlewareTests()
    {
        _nextCalled = false;
        _middleware = new SecurityHeadersMiddleware(_ =>
        {
            _nextCalled = true;
            return Task.CompletedTask;
        });
    }

    private static DefaultHttpContext CreateHttpContext() => new();

    [Fact]
    public async Task InvokeAsync_ShouldCallNextMiddleware()
    {
        // Arrange
        var context = CreateHttpContext();

        // Act
        await _middleware.InvokeAsync(context);

        // Assert
        _nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_ShouldSetXContentTypeOptions()
    {
        var context = CreateHttpContext();
        await _middleware.InvokeAsync(context);
        context.Response.Headers["X-Content-Type-Options"].ToString().Should().Be("nosniff");
    }

    [Fact]
    public async Task InvokeAsync_ShouldSetXFrameOptions()
    {
        var context = CreateHttpContext();
        await _middleware.InvokeAsync(context);
        context.Response.Headers["X-Frame-Options"].ToString().Should().Be("DENY");
    }

    [Fact]
    public async Task InvokeAsync_ShouldSetXXSSProtection()
    {
        var context = CreateHttpContext();
        await _middleware.InvokeAsync(context);
        context.Response.Headers["X-XSS-Protection"].ToString().Should().Be("1; mode=block");
    }

    [Fact]
    public async Task InvokeAsync_ShouldSetReferrerPolicy()
    {
        var context = CreateHttpContext();
        await _middleware.InvokeAsync(context);
        context.Response.Headers["Referrer-Policy"].ToString().Should().Be("strict-origin-when-cross-origin");
    }

    [Fact]
    public async Task InvokeAsync_ShouldSetContentSecurityPolicy()
    {
        var context = CreateHttpContext();
        await _middleware.InvokeAsync(context);
        context.Response.Headers["Content-Security-Policy"].ToString().Should().Be("default-src 'self'; frame-ancestors 'none'");
    }

    [Fact]
    public async Task InvokeAsync_ShouldSetCacheControl()
    {
        var context = CreateHttpContext();
        await _middleware.InvokeAsync(context);
        context.Response.Headers["Cache-Control"].ToString().Should().Be("no-store, no-cache, must-revalidate");
        context.Response.Headers["Pragma"].ToString().Should().Be("no-cache");
    }

    [Fact]
    public async Task InvokeAsync_ShouldSetStrictTransportSecurity()
    {
        var context = CreateHttpContext();
        await _middleware.InvokeAsync(context);
        context.Response.Headers["Strict-Transport-Security"].ToString().Should().Be("max-age=31536000; includeSubDomains");
    }

    [Fact]
    public async Task InvokeAsync_ShouldSetPermissionsPolicy()
    {
        var context = CreateHttpContext();
        await _middleware.InvokeAsync(context);
        context.Response.Headers["Permissions-Policy"].ToString().Should().Be("camera=(), microphone=(), geolocation=()");
    }
}
