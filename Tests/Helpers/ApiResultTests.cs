using Api.Errors;
using Api.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Xunit;

namespace Tests.Helpers;

public class ApiResultTests
{
    #region Success

    [Fact]
    public void Success_WithDefaults_ShouldReturn200()
    {
        var result = ApiResult.Success() as ObjectResult;

        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(200);
        var response = result.Value as ApiResponse;
        response!.Success.Should().BeTrue();
        response.Message.Should().Be("Request completed successfully");
    }

    [Fact]
    public void Success_WithCustomStatusCodeAndMessage_ShouldUseProvided()
    {
        var result = ApiResult.Success(202, "Accepted") as ObjectResult;

        result!.StatusCode.Should().Be(202);
        var response = result.Value as ApiResponse;
        response!.Message.Should().Be("Accepted");
    }

    [Fact]
    public void Success_WithData_ShouldIncludeData()
    {
        var data = new { Name = "test" };
        var result = ApiResult.Success(data: data) as ObjectResult;

        var response = result!.Value as ApiResponse;
        response!.Data.Should().Be(data);
    }

    #endregion

    #region Created

    [Fact]
    public void Created_WithDefaults_ShouldReturn201()
    {
        var result = ApiResult.Created() as ObjectResult;

        result!.StatusCode.Should().Be(201);
        var response = result.Value as ApiResponse;
        response!.Success.Should().BeTrue();
        response.Message.Should().Be("Resource created successfully");
    }

    [Fact]
    public void Created_WithCustomMessage_ShouldUseProvided()
    {
        var result = ApiResult.Created("User created") as ObjectResult;

        var response = (result as ObjectResult)!.Value as ApiResponse;
        response!.Message.Should().Be("User created");
    }

    #endregion

    #region NoContent

    [Fact]
    public void NoContent_ShouldReturn204()
    {
        var result = ApiResult.NoContent() as ObjectResult;

        result!.StatusCode.Should().Be(204);
        var response = result.Value as ApiResponse;
        response!.Message.Should().Be("No content");
    }

    #endregion

    #region Fail

    [Fact]
    public void Fail_WithDefaults_ShouldReturn400()
    {
        var result = ApiResult.Fail() as ObjectResult;

        result!.StatusCode.Should().Be(400);
        var response = result.Value as ApiResponse;
        response!.Success.Should().BeFalse();
    }

    [Fact]
    public void Fail_WithCustomStatusCode_ShouldUseProvided()
    {
        var result = ApiResult.Fail(404, "Not found") as ObjectResult;

        result!.StatusCode.Should().Be(404);
        var response = result.Value as ApiResponse;
        response!.Message.Should().Be("Not found");
    }

    #endregion

    #region Validation

    [Fact]
    public void Validation_WithDictionary_ShouldReturnBadRequest()
    {
        var errors = new Dictionary<string, string[]>
        {
            { "Email", new[] { "Email is required" } }
        };

        var result = ApiResult.Validation(errors) as BadRequestObjectResult;

        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(400);
        var response = result.Value as ApiValidationErrorResponse;
        response!.Errors.Should().ContainKey("Email");
    }

    [Fact]
    public void Validation_WithModelState_ShouldReturnBadRequest()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Username", "Username is too short");

        var result = ApiResult.Validation(modelState) as BadRequestObjectResult;

        result.Should().NotBeNull();
        var response = result!.Value as ApiValidationErrorResponse;
        response!.Errors.Should().ContainKey("Username");
        response.Errors!["Username"].Should().Contain("Username is too short");
    }

    #endregion

    #region ServerError

    [Fact]
    public void ServerError_WithDefaults_ShouldReturn500()
    {
        var result = ApiResult.ServerError() as ObjectResult;

        result!.StatusCode.Should().Be(500);
        var response = result.Value as ApiException;
        response!.Message.Should().Be("An internal server error occurred");
    }

    [Fact]
    public void ServerError_WithDetails_ShouldIncludeDetails()
    {
        var result = ApiResult.ServerError("Custom error", "stack trace here") as ObjectResult;

        var response = result!.Value as ApiException;
        response!.Message.Should().Be("Custom error");
        response.Details.Should().Be("stack trace here");
    }

    #endregion
}
