using System.Diagnostics;
using Infrastructure.Services;
using Microsoft.AspNetCore.Http;

namespace Api.Middlewares;

/// <summary>
/// Records Prometheus counters and histograms for every HTTP request.
/// </summary>
public class PrometheusRequestMiddleware
{
    private readonly RequestDelegate _next;

    public PrometheusRequestMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip the /metrics endpoint itself to avoid recursion noise
        if (context.Request.Path.StartsWithSegments("/metrics"))
        {
            await _next(context);
            return;
        }

        var sw = Stopwatch.StartNew();
        await _next(context);
        sw.Stop();

        var method = context.Request.Method;
        var endpoint = GetNormalizedEndpoint(context);
        var statusCode = context.Response.StatusCode.ToString();

        AppMetricsService.HttpRequestsTotal
            .WithLabels(method, endpoint, statusCode)
            .Inc();

        AppMetricsService.HttpRequestDuration
            .WithLabels(method, endpoint)
            .Observe(sw.Elapsed.TotalSeconds);
    }

    /// <summary>
    /// Normalizes endpoint paths to avoid high-cardinality labels.
    /// e.g., /api/v1/users/42 → /api/v1/users/{id}
    /// </summary>
    private static string GetNormalizedEndpoint(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint is RouteEndpoint routeEndpoint)
        {
            return "/" + routeEndpoint.RoutePattern.RawText?.TrimStart('/');
        }

        return context.Request.Path.Value ?? "unknown";
    }
}
