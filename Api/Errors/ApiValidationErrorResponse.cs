using System.Text.Json.Serialization;

namespace Api.Errors;

/// <summary>
/// API response containing model validation errors keyed by field name.
/// </summary>
public class ApiValidationErrorResponse : ApiResponse
{
    public ApiValidationErrorResponse() : base(400) { }

    [JsonPropertyOrder(100)]
    public IDictionary<string, string[]>? Errors { get; set; }
}
