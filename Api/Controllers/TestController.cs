using Api.Errors;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

public class TestController : BaseApiController
{
    [HttpGet("slow")]
    public async Task<IActionResult> Slow(CancellationToken cancellationToken)
    {
        Console.WriteLine("Started slow request");

        try
        {
            // Simulate long-running task
            await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
            return Ok("Finished slow request");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Request was cancelled due to shutdown");
            return StatusCode(499, "Request cancelled due to shutdown");
        }
    }

    [HttpGet("notfound")]
    public IActionResult GetNotFound()
    {
        return NotFound(new { message = "Resource not found" });
    }

    [HttpGet("validationerror")]
    public IActionResult GetValidationError()
    {
        var errors = new List<string>
        {
            "Name is required",
            "Email must be a valid email address"
        };

        var validationErrorResponse = new ApiValidationErrorResponse
        {
            Errors = errors
        };

        return BadRequest(validationErrorResponse);
    }

    [HttpGet("servererror")]
    public IActionResult GetServerError()
    {
        throw new Exception("This is a server error for testing purposes");
    }

    [HttpGet("badrequest")]
    public IActionResult GetBadRequest()
    {
        return BadRequest(new { message = "This is a bad request" });
    }
}
