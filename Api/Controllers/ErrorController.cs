using Api.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiExplorerSettings(IgnoreApi = true)]
[ApiController]
[Route("error/{code}")]
[AllowAnonymous]
[IgnoreAntiforgeryToken]
public class ErrorController : ControllerBase
{
    [HttpGet, HttpPost, HttpPut, HttpDelete, HttpPatch, HttpOptions, HttpHead]
    public IActionResult HandleError(int code)
    {
        return new ObjectResult(new ApiResponse(code));
    }
}
