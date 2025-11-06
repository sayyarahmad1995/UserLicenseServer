using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

public class TestController : BaseApiController
{
    [HttpGet("ok")]
    public IActionResult GetOk()
    {
        var data = new { Id = 1, Message = "Everything is working fine" };
        return Success(data, "Success response example");
    }

    [HttpPost("created")]
    public IActionResult CreateSomething()
    {
        var newItem = new { Id = 42, Name = "New Resource" };
        return CreatedResponse(newItem, "Resource created successfully");
    }

    [HttpDelete("delete/{id}")]
    public IActionResult DeleteSomething(int id)
    {
        return NoContentResponse($"Item {id} deleted successfully");
    }

    [HttpGet("badrequest")]
    public IActionResult BadRequestExample()
    {
        return Fail("This is a bad request example");
    }

    [HttpGet("unauthorized")]
    public IActionResult UnauthorizedExample()
    {
        return Fail("You are not authorized", 401);
    }

    [HttpGet("notfound")]
    public IActionResult NotFoundExample()
    {
        return Fail("Resource not found", 404);
    }

    [HttpGet("servererror")]
    public IActionResult ServerErrorExample()
    {
        return ServerError("Manual server error for testing", "Stack trace or extra info could go here");
    }

    [HttpGet("throw")]
    public IActionResult ThrowErrorExample()
    {
        throw new Exception("This is a simulated unhandled exception");
    }

    [HttpPost("validate")]
    public IActionResult ValidateExample([FromBody] TestValidationDto dto)
    {
        return Success(dto, "Validation passed successfully");
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
