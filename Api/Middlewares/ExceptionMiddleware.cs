using Api.Errors;
using Infrastructure.Services.Exceptions;
using System.Net;
using System.Text.Json;

namespace Api.Middlewares;

public class ExceptionMiddleware
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger, IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (InvalidCredentialsException)
        {
            // Don't log stack traces for expected auth failures
            _logger.LogWarning("Authentication failed for {Path}", context.Request.Path);
            await WriteResponse(context, 401, "Invalid credentials.");
        }
        catch (TokenException ex)
        {
            _logger.LogWarning("Token error: {Message}", ex.Message);
            await WriteResponse(context, 401, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);

            var response = _env.IsDevelopment()
               ? new ApiException(500, ex.Message, ex.StackTrace?.ToString())
               : new ApiException(500, "An internal server error occurred");

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            await context.Response.WriteAsync(JsonSerializer.Serialize(response, _jsonOptions));
        }
    }

    private static async Task WriteResponse(HttpContext context, int statusCode, string message)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;
        var response = new ApiResponse(statusCode, message);
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, _jsonOptions));
    }
}
