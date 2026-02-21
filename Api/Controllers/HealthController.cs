using Api.Helpers;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

public class HealthController : BaseApiController
{
    private readonly HealthService _healthService;
    public HealthController(HealthService healthService)
    {
        _healthService = healthService;
    }

    /// <summary>
    /// Public liveness check — verifies API, database, and Redis are reachable.
    /// Used by Docker HEALTHCHECK and container orchestrators.
    /// </summary>
    /// <response code="200">All services healthy</response>
    /// <response code="503">One or more services degraded</response>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetHealth(CancellationToken ct)
    {
        var result = await _healthService.GetLiveHealthAsync(ct);
        var statusCode = result.Status == "Healthy" ? 200 : 503;
        return ApiResult.Success(statusCode, result.Status, result);
    }

    /// <summary>
    /// Detailed database health (admin only).
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpGet("details")]
    public async Task<IActionResult> GetHealthDetails(CancellationToken ct)
    {
        var health = await _healthService.GetDatabaseHealthAsync(ct);
        return ApiResult.Success(200, "Database health retrieved.", new
        {
            database = health,
            timestamp = DateTime.UtcNow
        });
    }
}
