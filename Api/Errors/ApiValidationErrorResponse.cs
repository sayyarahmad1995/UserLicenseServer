namespace Api.Errors;

public class ApiValidationErrorResponse : ApiResponse
{
	public ApiValidationErrorResponse() : base(400) { }

	public IDictionary<string, string[]>? Errors { get; set; }
}
