using Api.Errors;
using Microsoft.AspNetCore.Mvc;

namespace Api.Helpers;

public static class ApiResult
{
    public static IActionResult Success(object? data = null, string? message = null, int statusCode = 200)
    {
        var response = new ApiResponse(statusCode, message ?? "Request completed successfully", data);
        return new ObjectResult(response)
        {
            StatusCode = statusCode
        };
    }

    public static IActionResult Created(object? data = null, string? message = null)
    {
        var response = new ApiResponse(201, message ?? "Resource created successfully", data);
        return new ObjectResult(response)
        {
            StatusCode = 201
        };
    }

    public static IActionResult NoContent(string? message = null)
    {
        var response = new ApiResponse(204, message ?? "No content");
        return new ObjectResult(response)
        {
            StatusCode = 204
        };
    }

    public static IActionResult Fail(string message, int statusCode = 400, object? data = null)
    {
        var response = new ApiResponse(statusCode, message, data);
        return new ObjectResult(response)
        {
            StatusCode = statusCode
        };
    }

    public static IActionResult Validation(IDictionary<string, string[]> errors)
    {
        var response = new ApiValidationErrorResponse
        {
            Errors = errors
        };
        return new BadRequestObjectResult(response);
    }

    public static IActionResult ServerError(string message = "An internal server error occurred", string? details = null)
    {
        var response = new ApiException(500, message, details);
        return new ObjectResult(response)
        {
            StatusCode = 500
        };
    }
}
