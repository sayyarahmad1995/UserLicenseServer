using Api.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Api.Helpers;

/// <summary>
/// Standardized API response factory ensuring consistent envelope format across all endpoints.
/// </summary>
public static class ApiResult
{
    /// <summary>
    /// Returns a success response with optional data payload.
    /// </summary>
    public static ActionResult Success(int statusCode = 200, string? message = null, object? data = null)
    {
        var response = new ApiResponse(statusCode, message ?? "Request completed successfully", data);
        return new ObjectResult(response)
        {
            StatusCode = statusCode
        };
    }

    /// <summary>
    /// Returns a 201 Created response with optional data payload.
    /// </summary>
    public static ActionResult Created(string? message = null, object? data = null)
    {
        var response = new ApiResponse(201, message ?? "Resource created successfully", data);
        return new ObjectResult(response)
        {
            StatusCode = 201
        };
    }

    /// <summary>
    /// Returns a 204 No Content response.
    /// </summary>
    public static ActionResult NoContent(string? message = null)
    {
        var response = new ApiResponse(204, message ?? "No content");
        return new ObjectResult(response)
        {
            StatusCode = 204
        };
    }

    /// <summary>
    /// Returns an error response with the given status code and message.
    /// </summary>
    public static ActionResult Fail(int statusCode = 400, string? message = null, object? data = null)
    {
        var response = new ApiResponse(statusCode, message, data);
        return new ObjectResult(response)
        {
            StatusCode = statusCode
        };
    }

    /// <summary>
    /// Returns a 400 Bad Request with structured validation errors.
    /// </summary>
    public static ActionResult Validation(IDictionary<string, string[]> errors, object? data = null)
    {
        var response = new ApiValidationErrorResponse
        {
            Errors = errors,
            Data = data
        };
        return new BadRequestObjectResult(response);
    }

    /// <summary>
    /// Returns a 400 Bad Request by converting ModelStateDictionary to structured validation errors.
    /// </summary>
    public static ActionResult Validation(ModelStateDictionary modelState)
    {
        var errors = modelState
            .Where(e => e.Value?.Errors.Count > 0)
            .ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
            );

        var response = new ApiValidationErrorResponse
        {
            Errors = errors
        };
        return new BadRequestObjectResult(response);
    }

    /// <summary>
    /// Returns a 500 Internal Server Error response.
    /// </summary>
    public static ActionResult ServerError(string message = "An internal server error occurred", string? details = null)
    {
        var response = new ApiException(500, message, details);
        return new ObjectResult(response)
        {
            StatusCode = 500
        };
    }
}
