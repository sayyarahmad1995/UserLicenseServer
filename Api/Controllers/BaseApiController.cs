using Api.Errors;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class BaseApiController : ControllerBase
{
	protected IActionResult Success(object? data = null, string? message = null, int statusCode = 200)
	{
		var response = new ApiResponse(statusCode, message ?? "Request completed successfully", data);
		return new ObjectResult(response)
		{
			StatusCode = statusCode
		};
	}

	protected IActionResult CreatedResponse(object? data = null, string? message = null)
	{
		var response = new ApiResponse(201, message ?? "Resource created successfully", data);
		return new ObjectResult(response)
		{
			StatusCode = 201
		};
	}

	protected IActionResult NoContentResponse(string? message = null)
	{
		var response = new ApiResponse(204, message ?? "No content");
		return new ObjectResult(response)
		{
			StatusCode = 204
		};
	}

	protected IActionResult Fail(string message, int statusCode = 400, object? data = null)
	{
		var response = new ApiResponse(statusCode, message, data);
		return new ObjectResult(response)
		{
			StatusCode = statusCode
		};
	}

	protected IActionResult Validation(IDictionary<string, string[]> errors)
	{
		var response = new ApiValidationErrorResponse
		{
			Errors = errors
		};
		return new BadRequestObjectResult(response);
	}

	protected IActionResult ServerError(string message = "An internal server error occurred", string? details = null)
	{
		var response = new ApiException(500, message, details);
		return new ObjectResult(response)
		{
			StatusCode = 500
		};
	}
}
