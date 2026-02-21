#if DEBUG
using Api.Helpers;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Api.Controllers;

/// <summary>
/// Test endpoints only available in Debug builds. Not compiled in Release/Production.
/// </summary>
public class TestController : BaseApiController
{
    [HttpGet("ok")]
    public IActionResult GetOk()
    {
        var data = new { Id = 1, Message = "Everything is working fine" };
        return ApiResult.Success(200, "Success response example", data);
    }

    [HttpPost("created")]
    public IActionResult CreateSomething()
    {
        var newItem = new { Id = 42, Name = "New Resource" };
        return ApiResult.Created("Resource created successfully", newItem);
    }

    [HttpDelete("delete/{id}")]
    public IActionResult DeleteSomething(int id)
    {
        return ApiResult.NoContent($"Item {id} deleted successfully");
    }

    [HttpGet("badrequest")]
    public IActionResult BadRequestExample()
    {
        return ApiResult.Fail(400, "This is a bad request example");
    }

    [HttpGet("unauthorized")]
    public IActionResult UnauthorizedExample()
    {
        return ApiResult.Fail(401, "You are not authorized");
    }

    [HttpGet("notfound")]
    public IActionResult NotFoundExample()
    {
        return ApiResult.Fail(404, "Resource not found");
    }

    [HttpGet("servererror")]
    public IActionResult ServerErrorExample()
    {
        return ApiResult.ServerError("Manual server error for testing", "Stack trace or extra info could go here");
    }

    [HttpGet("throw")]
    public IActionResult ThrowErrorExample()
    {
        throw new Exception("This is a simulated unhandled exception");
    }

    [HttpPost("validate")]
    public IActionResult ValidateExample([FromBody] TestValidationDto dto)
    {
        return ApiResult.Success(200, "Validation passed successfully", dto);
    }
}

public class TestValidationDto
{
    [Required(ErrorMessage = "Name is required")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Name must be between 3 and 50 characters")]
    public string? Name { get; set; }

    [Range(18, 60, ErrorMessage = "Age must be between 18 and 60")]
    public int Age { get; set; }

    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string? Email { get; set; }
}
#endif
