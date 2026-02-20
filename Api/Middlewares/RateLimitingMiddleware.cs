using Api.Errors;
using Core.Interfaces;
using System.Net;
using System.Text.Json;

namespace Api.Middlewares;

/// <summary>
/// Middleware for rate limiting authentication endpoints to prevent brute force attacks.
/// Uses Redis to track request counts per IP address/endpoint combination.
/// </summary>
public class RateLimitingMiddleware
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;

    // Rate limit configuration
    private const int MaxLoginAttempts = 5;
    private const int MaxRegisterAttempts = 3;
    private const int WindowDurationMinutes = 15;

    public RateLimitingMiddleware(RequestDelegate next, ILogger<RateLimitingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ICacheRepository cache)
    {
        // Only rate limit auth endpoints
        var path = context.Request.Path.Value;
        if (!path!.Contains("/auth/login") && !path.Contains("/auth/register"))
        {
            await _next(context);
            return;
        }

        var clientIp = GetClientIpAddress(context);
        var endpoint = ExtractEndpoint(path);
        var key = $"ratelimit:{clientIp}:{endpoint}";

        // Get current attempt count
        var attemptCountStr = await cache.GetAsync<string>(key);
        int attemptCount = 0;

        if (attemptCountStr != null && int.TryParse(attemptCountStr, out var count))
        {
            attemptCount = count;
        }

        // Determine max attempts based on endpoint
        int maxAttempts = endpoint == "login" ? MaxLoginAttempts : MaxRegisterAttempts;

        if (attemptCount >= maxAttempts)
        {
            _logger.LogWarning("Rate limit exceeded for {Endpoint} from IP {ClientIp}", endpoint, clientIp);
            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            context.Response.ContentType = "application/json";

            var response = new ApiResponse(429, $"Too many {endpoint} attempts. Please try again after 15 minutes.");
            await context.Response.WriteAsync(JsonSerializer.Serialize(response, _jsonOptions));
            return;
        }

        // Increment attempt counter
        await cache.SetAsync(key, (attemptCount + 1).ToString(), TimeSpan.FromMinutes(WindowDurationMinutes));

        _logger.LogInformation("Rate limit check passed for {Endpoint} from IP {ClientIp} - Attempt: {AttemptCount}/{MaxAttempts}",
            endpoint, clientIp, attemptCount + 1, maxAttempts);

        await _next(context);
    }

    /// <summary>
    /// Extracts the client's real IP address, handling proxy scenarios.
    /// </summary>
    private static string GetClientIpAddress(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
        {
            var ips = forwardedFor.ToString().Split(',');
            return ips[0].Trim();
        }

        if (context.Request.Headers.TryGetValue("X-Real-IP", out var realIp))
        {
            return realIp.ToString();
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    /// <summary>
    /// Extracts endpoint name from the request path.
    /// </summary>
    private static string ExtractEndpoint(string? path)
    {
        if (path == null) return "unknown";

        if (path.Contains("login")) return "login";
        if (path.Contains("register")) return "register";

        return "unknown";
    }
}
