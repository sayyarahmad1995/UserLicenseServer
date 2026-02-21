using System.Text.Json.Serialization;

namespace Api.Errors;

/// <summary>
/// API response carrying additional exception details (used in development error responses).
/// </summary>
public class ApiException : ApiResponse
{
    public ApiException(int statusCode, string? message, string? details = null)
       : base(statusCode, message)
    {
        Details = details;
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyOrder(100)]
    public string? Details { get; set; }
}
