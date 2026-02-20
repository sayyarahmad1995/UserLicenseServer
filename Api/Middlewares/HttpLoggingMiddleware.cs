using System.Diagnostics;

namespace Api.Middlewares;

/// <summary>
/// Middleware for comprehensive HTTP request/response logging.
/// Logs request details, response status, and execution time for performance monitoring.
/// </summary>
public class HttpLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<HttpLoggingMiddleware> _logger;

    public HttpLoggingMiddleware(RequestDelegate next, ILogger<HttpLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestId = context.TraceIdentifier;
        var method = context.Request.Method;
        var path = context.Request.Path;
        var queryString = context.Request.QueryString;
        var clientIp = GetClientIpAddress(context);

        // Log incoming request
        LogRequest(requestId, method, path, queryString, clientIp);

        // Capture original body stream
        var originalBodyStream = context.Response.Body;

        try
        {
            using (var memoryStream = new MemoryStream())
            {
                context.Response.Body = memoryStream;

                await _next(context);

                stopwatch.Stop();

                // Log response
                LogResponse(requestId, context.Response.StatusCode, stopwatch.ElapsedMilliseconds, method, path);

                // Reset stream position to beginning before copying
                memoryStream.Seek(0, SeekOrigin.Begin);
                
                // Copy memory stream back to original body stream
                await memoryStream.CopyToAsync(originalBodyStream);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "[{RequestId}] Unhandled exception in request {Method} {Path} - Duration: {ElapsedMs}ms",
                requestId, method, path, stopwatch.ElapsedMilliseconds);
            throw;
        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }

    private void LogRequest(string requestId, string method, PathString path, QueryString queryString, string clientIp)
    {
        var fullPath = string.IsNullOrEmpty(queryString.Value)
            ? path.Value ?? "/"
            : $"{path.Value}{queryString.Value}";

        _logger.LogInformation(
            "[{RequestId}] Incoming request: {Method} {Path} from {ClientIp}",
            requestId, method, fullPath, clientIp);
    }

    private void LogResponse(string requestId, int statusCode, long elapsedMs, string method, PathString path)
    {
        var level = statusCode >= 500 ? LogLevel.Error
                  : statusCode >= 400 ? LogLevel.Warning
                  : LogLevel.Information;

        _logger.Log(level,
            "[{RequestId}] Response: {StatusCode} {Method} {Path} - Duration: {ElapsedMs}ms",
            requestId, statusCode, method, path, elapsedMs);

        // Log slow requests (threshold: 1000ms)
        if (elapsedMs > 1000)
        {
            _logger.LogWarning(
                "[{RequestId}] Slow request detected: {Method} {Path} took {ElapsedMs}ms",
                requestId, method, path, elapsedMs);
        }
    }

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
}
