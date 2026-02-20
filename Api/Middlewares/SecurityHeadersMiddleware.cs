namespace Api.Middlewares;

/// <summary>
/// Middleware to add security headers to all HTTP responses.
/// Protects against common web vulnerabilities like XSS, clickjacking, and MIME sniffing.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        // Prevent MIME type sniffing
        headers["X-Content-Type-Options"] = "nosniff";

        // Prevent clickjacking
        headers["X-Frame-Options"] = "DENY";

        // Enable XSS protection in older browsers
        headers["X-XSS-Protection"] = "1; mode=block";

        // Referrer policy - only send origin for cross-origin requests
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // Content Security Policy - restrict resource loading
        headers["Content-Security-Policy"] = "default-src 'self'; frame-ancestors 'none'";

        // Prevent browsers from caching sensitive responses
        headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
        headers["Pragma"] = "no-cache";

        // Strict Transport Security (only effective over HTTPS)
        headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";

        // Permissions policy - disable unnecessary browser features
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

        await _next(context);
    }
}
