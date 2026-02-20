using Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

public class HealthController : BaseApiController
{
    private readonly HealthService _healthService;
    public HealthController(HealthService healthService)
    {
        _healthService = healthService;
    }

    [HttpGet]
    public async Task<IActionResult> GetHealth()
    {
        var health = await _healthService.GetDatabaseHealthAsync();
        return Ok(new
        {
            database = health,
            timestamp = DateTime.UtcNow
        });
    }
}
