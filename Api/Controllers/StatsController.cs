using Api.Helpers;
using Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Admin dashboard statistics.
/// </summary>
[Authorize(Roles = "Admin")]
public class StatsController : BaseApiController
{
    private readonly IDashboardService _dashboardService;

    public StatsController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    /// <summary>
    /// Returns an overview of users, licenses, activity, and system health.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var stats = await _dashboardService.GetStatsAsync(ct);
        return ApiResult.Success(200, "Dashboard statistics retrieved.", stats);
    }
}
