using System.Text.Json.Serialization;

namespace Api.Errors;

/// <summary>
/// Standard API response envelope wrapping status code, message, and optional data.
/// </summary>
public class ApiResponse
{
    public bool Success { get; set; }
    public int StatusCode { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Data { get; set; }

    public ApiResponse(int statusCode, string? message = null, object? data = null)
    {
        StatusCode = statusCode;
        Message = message ?? GetDefaultMessageForStatusCode(statusCode);
        Data = data;
        Success = statusCode is >= 200 and < 300;
    }

    private static string? GetDefaultMessageForStatusCode(int statusCode)
    {
        return statusCode switch
        {
            400 => "You have made a bad request",
            401 => "You are not authorized",
            403 => "Access is forbidden",
            404 => "Resource not found",
            405 => "Method not allowed",
            406 => "Not acceptable",
            415 => "Unsupported media type. Please use 'application/json'.",
            500 => "An internal server error occurred",
            _ => null
        };
    }
}
