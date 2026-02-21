using Api.Helpers;
using AutoMapper;
using Core.DTOs;
using Core.Helpers;
using Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Admin-only controller for viewing audit logs.
/// </summary>
[Authorize(Roles = "Admin")]
public class AuditController : BaseApiController
{
    private readonly IAuditService _auditService;
    private readonly IMapper _mapper;
    private readonly ILogger<AuditController> _logger;

    public AuditController(IAuditService auditService, IMapper mapper, ILogger<AuditController> logger)
    {
        _auditService = auditService;
        _mapper = mapper;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves a paginated list of audit log entries with optional filtering.
    /// </summary>
    /// <param name="page">Page number (default 1)</param>
    /// <param name="pageSize">Page size (default 50, max 100)</param>
    /// <param name="action">Filter by action type (e.g. "Login", "CreateLicense")</param>
    /// <param name="entityType">Filter by entity type (e.g. "User", "License")</param>
    /// <param name="userId">Filter by user ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Paginated audit log entries</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? action = null,
        [FromQuery] string? entityType = null,
        [FromQuery] int? userId = null,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 100) pageSize = 100;

        var total = await _auditService.CountLogsAsync(action, entityType, userId, ct);
        var logs = await _auditService.GetLogsAsync(page, pageSize, action, entityType, userId, ct);
        var data = _mapper.Map<IReadOnlyList<AuditLogDto>>(logs);

        _logger.LogDebug("Retrieved {Count} audit logs (page {Page})", data.Count, page);

        return ApiResult.Success(200, "Audit logs retrieved successfully.", new Pagination<AuditLogDto>
        {
            PageIndex = page,
            PageSize = pageSize,
            TotalCount = total,
            Data = data
        });
    }
}
