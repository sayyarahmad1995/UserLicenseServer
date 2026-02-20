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

        if (globalResult == ThrottleResult.Blocked)
        {
            _logger.LogWarning(
                "Global rate limit reached for IP: {IpAddress} on {Method} {Path}",
                ipAddress, method, path);
            await WriteRateLimitResponse(context,
                "Too many requests. Please wait before trying again.",
                _settings.Global);
            return;
        }

        if (globalResult == ThrottleResult.Throttled)
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

            if (userResult == ThrottleResult.Blocked)
            {
                _logger.LogWarning(
                    "User rate limit reached for User: {UserId} on {Method} {Path}",
                    userId, method, path);
                await WriteRateLimitResponse(context,
                    "Too many requests. Please slow down.",
                    _settings.User);
                return;
            }

            if (userResult == ThrottleResult.Throttled)
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

            if (authResult == ThrottleResult.Blocked)
            {
                _logger.LogWarning(
                    "Auth rate limit reached for IP: {IpAddress} on {Method} {Path}",
                    ipAddress, method, path);
                await WriteRateLimitResponse(context,
                    "Too many authentication attempts. Please try again later.",
                    _settings.Auth);
                return;
            }

            if (authResult == ThrottleResult.Throttled)
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

    private static async Task<ThrottleResult> CheckThrottling(
        ICacheRepository cache, string key, ThrottleTier tier)
    {
        var requestCount = await IncrementCounter(cache, key, tier.WindowSeconds);

        if (requestCount > tier.MaxRequestsPerMinute)
            return ThrottleResult.Blocked;

        if (requestCount > tier.ThrottleThreshold)
            return ThrottleResult.Throttled;

        return ThrottleResult.Allowed;
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
        var currentCount = await cache.GetAsync<int?>(key);

        if (currentCount == null)
        {
            await cache.SetAsync(key, 1, TimeSpan.FromSeconds(windowSeconds));
            return 1;
        }

        var newCount = currentCount.Value + 1;
        await cache.SetAsync(key, newCount);
        return newCount;
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
        HttpContext context, string message, ThrottleTier tier)
    {
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.ContentType = "application/json";
        context.Response.Headers.Append("Retry-After", tier.WindowSeconds.ToString());

        var response = new
        {
            StatusCode = 429,
            Message = message,
            RetryAfterSeconds = tier.WindowSeconds
        };

        await context.Response.WriteAsJsonAsync(response);
    }

    #endregion
}

internal enum ThrottleResult
{
    Allowed,
    Throttled,
    Blocked
}
