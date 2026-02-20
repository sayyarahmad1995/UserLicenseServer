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

        // Log request
        LogRequest(requestId, method, path);

        var originalBodyStream = context.Response.Body;
        var memoryStream = new MemoryStream();

        try
        {
            context.Response.Body = memoryStream;

            await _next(context);

            stopwatch.Stop();

            // Log response
            LogResponse(requestId, context.Response.StatusCode, stopwatch.ElapsedMilliseconds, method, path);

            // Only log response body in Development
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                memoryStream.Seek(0, SeekOrigin.Begin);
                var responseBody = await new StreamReader(memoryStream).ReadToEndAsync();
                _logger.LogDebug("[{RequestId}] Response Body: {Body}", requestId, responseBody);
            }

            memoryStream.Seek(0, SeekOrigin.Begin);
            await memoryStream.CopyToAsync(originalBodyStream);
        }
        finally
        {
            // Always restore the original stream, even if an exception occurs
            context.Response.Body = originalBodyStream;
            await memoryStream.DisposeAsync();
        }
    }

    private void LogRequest(string requestId, string method, PathString path)
    {
        var fullPath = path.Value ?? "/";

        _logger.LogInformation(
            "[{RequestId}] Incoming request: {Method} {Path}",
            requestId, method, fullPath);
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

}
