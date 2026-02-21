using Api.Helpers;
using AutoMapper;
using Core.DTOs;
using Core.Helpers;
using Core.Interfaces;
using Core.Spec;
using Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.Controllers;

/// <summary>
/// Admin-only controller for managing licenses (create, revoke, renew, delete, list).
/// </summary>
[Authorize(Roles = "Admin")]
public class LicensesController : BaseApiController
{
    private readonly ILicenseService _licenseService;
    private readonly IAuditService _auditService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<LicensesController> _logger;

    public LicensesController(
        ILicenseService licenseService,
        IAuditService auditService,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<LicensesController> logger)
    {
        _licenseService = licenseService;
        _auditService = auditService;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }

    private int? GetAdminUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return claim != null && int.TryParse(claim, out var id) ? id : null;
    }

    private string GetIpAddress() =>
        HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    /// <summary>
    /// Retrieves a paginated list of all licenses with optional filtering and sorting.
    /// </summary>
    /// <param name="specParams">Query parameters for pagination, filtering, and sorting</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Paginated list of licenses</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<Pagination<LicenseDto>>> GetLicenses(
        [FromQuery] LicenseSpecParams specParams,
        CancellationToken ct)
    {
        _logger.LogInformation("GetLicenses called - PageIndex: {PageIndex}, PageSize: {PageSize}",
            specParams.PageIndex, specParams.PageSize);

        var spec = new LicenseSpecification(specParams);
        var countSpec = new LicenseCountWithFiltersSpecification(specParams);

        var totalItems = await _unitOfWork.LicenseRepository.CountAsync(countSpec, ct);
        var licenses = await _unitOfWork.LicenseRepository.ListAsync(spec, ct);
        var data = _mapper.Map<IReadOnlyList<LicenseDto>>(licenses);

        return ApiResult.Success(200, "Licenses retrieved successfully.", new Pagination<LicenseDto>
        {
            PageIndex = specParams.PageIndex,
            PageSize = specParams.PageSize,
            TotalCount = totalItems,
            Data = data
        });
    }

    /// <summary>
    /// Retrieves a single license by its ID.
    /// </summary>
    /// <param name="id">License ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>License details</returns>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LicenseDto>> GetLicenseById(int id, CancellationToken ct)
    {
        var license = await _unitOfWork.LicenseRepository.GetByIdAsync(id, ct);
        if (license == null)
        {
            _logger.LogWarning("License {LicenseId} not found", id);
            return ApiResult.Fail(404, "License not found.");
        }

        var data = _mapper.Map<LicenseDto>(license);
        return ApiResult.Success(200, "License retrieved successfully.", data);
    }

    /// <summary>
    /// Creates a new license for a user with a cryptographically-generated key.
    /// </summary>
    /// <param name="dto">License creation payload</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Created license details</returns>
    /// <response code="201">License created successfully</response>
    /// <response code="400">Invalid input (user not found, date in past)</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateLicense([FromBody] CreateLicenseDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return ApiResult.Validation(ModelState);

        try
        {
            var license = await _licenseService.CreateLicenseAsync(dto, ct);
            var data = _mapper.Map<LicenseDto>(license);

            await _auditService.LogAsync("CreateLicense", "License", license.Id, GetAdminUserId(),
                $"License {license.LicenseKey} created for user {dto.UserId}", GetIpAddress(), ct);

            return ApiResult.Created("License created successfully.", data);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(400, ex.Message);
        }
        catch (ArgumentException ex)
        {
            return ApiResult.Fail(400, ex.Message);
        }
    }

    /// <summary>
    /// Revokes an active license. Sets status to Revoked and records the revocation timestamp.
    /// </summary>
    /// <param name="id">License ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Success message</returns>
    /// <response code="200">License revoked successfully</response>
    /// <response code="400">License already revoked</response>
    /// <response code="404">License not found</response>
    [HttpPatch("{id:int}/revoke")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeLicense(int id, CancellationToken ct)
    {
        try
        {
            await _licenseService.RevokeLicenseAsync(id, ct);

            await _auditService.LogAsync("RevokeLicense", "License", id, GetAdminUserId(),
                ipAddress: GetIpAddress(), ct: ct);

            return ApiResult.Success(200, "License revoked successfully.");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return ApiResult.Fail(404, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(400, ex.Message);
        }
    }

    /// <summary>
    /// Renews a license by extending its expiration date. Cannot renew a revoked license.
    /// </summary>
    /// <param name="id">License ID</param>
    /// <param name="dto">Renewal payload with new expiration date</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Updated license details</returns>
    /// <response code="200">License renewed successfully</response>
    /// <response code="400">Invalid operation (revoked license, past date)</response>
    /// <response code="404">License not found</response>
    [HttpPatch("{id:int}/renew")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RenewLicense(int id, [FromBody] RenewLicenseDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return ApiResult.Validation(ModelState);

        try
        {
            var license = await _licenseService.RenewLicenseAsync(id, dto.NewExpiresAt, ct);
            var data = _mapper.Map<LicenseDto>(license);

            await _auditService.LogAsync("RenewLicense", "License", id, GetAdminUserId(),
                $"Extended to {dto.NewExpiresAt:u}", GetIpAddress(), ct);

            return ApiResult.Success(200, "License renewed successfully.", data);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return ApiResult.Fail(404, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(400, ex.Message);
        }
        catch (ArgumentException ex)
        {
            return ApiResult.Fail(400, ex.Message);
        }
    }

    /// <summary>
    /// Permanently deletes a license record.
    /// </summary>
    /// <param name="id">License ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>204 No Content on success</returns>
    /// <response code="204">License deleted</response>
    /// <response code="404">License not found</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteLicense(int id, CancellationToken ct)
    {
        try
        {
            await _licenseService.DeleteLicenseAsync(id, ct);

            await _auditService.LogAsync("DeleteLicense", "License", id, GetAdminUserId(),
                ipAddress: GetIpAddress(), ct: ct);

            return ApiResult.NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(404, ex.Message);
        }
    }

    /// <summary>
    /// Revokes multiple licenses in a single operation.
    /// </summary>
    /// <param name="dto">License IDs to revoke</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Summary of successful and failed revocations</returns>
    /// <response code="200">Bulk revoke completed</response>
    /// <response code="400">Invalid input</response>
    [HttpPost("bulk-revoke")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> BulkRevoke([FromBody] BulkLicenseRevokeDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return ApiResult.Validation(ModelState);

        var succeeded = new List<int>();
        var failed = new Dictionary<int, string>();

        foreach (var licenseId in dto.LicenseIds.Distinct())
        {
            try
            {
                await _licenseService.RevokeLicenseAsync(licenseId, ct);
                succeeded.Add(licenseId);
            }
            catch (InvalidOperationException ex)
            {
                failed[licenseId] = ex.Message;
            }
        }

        if (succeeded.Count > 0)
        {
            await _auditService.LogAsync("BulkRevokeLicenses", "License", userId: GetAdminUserId(),
                details: $"Revoked {succeeded.Count} licenses: [{string.Join(", ", succeeded)}]",
                ipAddress: GetIpAddress(), ct: ct);
        }

        _logger.LogInformation("Bulk license revoke: {Succeeded} succeeded, {Failed} failed",
            succeeded.Count, failed.Count);

        return ApiResult.Success(200, "Bulk revoke completed.", new
        {
            Succeeded = succeeded,
            Failed = failed,
            SucceededCount = succeeded.Count,
            FailedCount = failed.Count
        });
    }
}
