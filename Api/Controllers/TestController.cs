using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

public class TestController : BaseApiController
{
    [HttpGet]
    public ActionResult Test()
    {
        return Ok(new { message = "Test successful!" });
    }

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
}
