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
    /// Public shallow health check.
    /// </summary>
    [HttpGet]
    public IActionResult GetHealth()
    {
        return ApiResult.Success(200, "Healthy");
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
