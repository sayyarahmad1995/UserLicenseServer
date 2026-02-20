using System.Text.Json.Serialization;

namespace Api.Errors;

public class ApiValidationErrorResponse : ApiResponse
{
    public ApiValidationErrorResponse() : base(400) { }

    [JsonPropertyOrder(100)]
    public IDictionary<string, string[]>? Errors { get; set; }
}
