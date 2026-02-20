using Api.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Api.Helpers;

public static class ApiResult
{
    public static ActionResult Success(int statusCode = 200, string? message = null, object? data = null)
    {
        var response = new ApiResponse(statusCode, message ?? "Request completed successfully", data);
        return new ObjectResult(response)
        {
            StatusCode = statusCode
        };
    }

    public static ActionResult Created(string? message = null, object? data = null)
    {
        var response = new ApiResponse(201, message ?? "Resource created successfully", data);
        return new ObjectResult(response)
        {
            StatusCode = 201
        };
    }

    public static ActionResult NoContent(string? message = null)
    {
        var response = new ApiResponse(204, message ?? "No content");
        return new ObjectResult(response)
        {
            StatusCode = 204
        };
    }

    public static ActionResult Fail(int statusCode = 400, string? message = null, object? data = null)
    {
        var response = new ApiResponse(statusCode, message, data);
        return new ObjectResult(response)
        {
            StatusCode = statusCode
        };
    }

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
    /// Overload for ModelStateDictionary that converts it to the proper format.
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

    public static ActionResult ServerError(string message = "An internal server error occurred", string? details = null)
    {
        var response = new ApiException(500, message, details);
        return new ObjectResult(response)
        {
            StatusCode = 500
        };
    }
}
