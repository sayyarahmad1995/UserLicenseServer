using Api.Errors;
using Api.Helpers;
using AutoMapper;
using Core.DTOs;
using Core.Interfaces;
using Core.Helpers;
using Core.Spec;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[Authorize(Roles = "Admin")]
public class UsersController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IUserCacheService _userCache;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IUserCacheService userCache,
        ILogger<UsersController> logger
    )
    {
        _mapper = mapper;
        _userCache = userCache;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<Pagination<UserDto>>> GetUsers([FromQuery] UserSpecParams p)
    {
        _logger.LogInformation("GetUsers called - PageIndex: {PageIndex}, PageSize: {PageSize}, Search: {Search}", 
            p.PageIndex, p.PageSize, p.Search);

        var cached = await _userCache.GetUsersAsync(p);
        if (cached != null)
        {
            _logger.LogDebug("Users list retrieved from cache");
            return Ok(cached);
        }

        var spec = new UserSpecification(p);
        var countSpec = new UserCountSpecification(p);

        var total = await _unitOfWork.UserRepository.CountAsync(countSpec);
        var users = await _unitOfWork.UserRepository.ListAsync(spec);

        var dto = _mapper.Map<IReadOnlyList<UserDto>>(users);

        var result = new Pagination<UserDto>
        {
            PageIndex = p.PageIndex,
            PageSize = p.PageSize,
            TotalCount = total,
            Data = dto
        };

        await _userCache.CacheUsersAsync(p, result);
        _logger.LogDebug("Users list cached - Total count: {TotalCount}", total);

        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<UserDto>> GetUserById(int id)
    {
        _logger.LogInformation("GetUserById called for user {UserId}", id);

        var cached = await _userCache.GetUserAsync(id);
        if (cached != null)
        {
            _logger.LogDebug("User {UserId} retrieved from cache", id);
            return Ok(cached);
        }

        var user = await _unitOfWork.UserRepository.GetByIdAsync(id);
        if (user == null)
        {
            _logger.LogWarning("User {UserId} not found", id);
            return NotFound();
        }

        var dto = _mapper.Map<UserDto>(user);
        await _userCache.CacheUserAsync(id, dto);

        return ApiResult.Success(200, "User retrieved successfully.", dto);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        _logger.LogInformation("DeleteUser called for user {UserId}", id);

        var user = await _unitOfWork.UserRepository.GetByIdAsync(id);
        if (user == null)
        {
            _logger.LogWarning("DeleteUser failed - User {UserId} not found", id);
            return ApiResult.Fail(404, "User not found");
        }

        _unitOfWork.UserRepository.Delete(user);
        await _unitOfWork.CompleteAsync();

        await _userCache.InvalidateUsersAsync();
        await _userCache.InvalidateUserAsync(id);

        _logger.LogInformation("User {UserId} deleted successfully", id);

        return ApiResult.NoContent();
    }

    [HttpPatch("{id:int}")]
    public async Task<IActionResult> UpdateUserPartial(int id, [FromBody] StatusUpdateDto dto)
    {
        _logger.LogInformation("UpdateUserPartial called for user {UserId} with status {Status}", id, dto.Status);

        if (!ModelState.IsValid)
        {
            _logger.LogWarning("UpdateUserPartial failed - Invalid model state for user {UserId}", id);
            return ApiResult.Validation(ModelState);
        }

        var user = await _unitOfWork.UserRepository.GetByIdAsync(id);
        if (user == null)
        {
            _logger.LogWarning("UpdateUserPartial failed - User {UserId} not found", id);
            return NotFound(new ApiResponse(404, "User not found"));
        }

        var status = dto.Status.Trim().ToLower();

        try
        {
            switch (status)
            {
                case "verify":
                case "verified":
                    user.Verify();
                    _logger.LogInformation("User {UserId} verified", id);
                    break;
                case "active":
                    user.Activate();
                    _logger.LogInformation("User {UserId} activated", id);
                    break;
                case "block":
                case "blocked":
                    user.Block();
                    _logger.LogInformation("User {UserId} blocked", id);
                    break;
                case "unblock":
                    user.Unblock();
                    _logger.LogInformation("User {UserId} unblocked", id);
                    break;
                default:
                    return BadRequest(new ApiResponse(400, "Invalid status value"));
            }

            _unitOfWork.UserRepository.Update(user);
            await _unitOfWork.CompleteAsync();

            await _userCache.InvalidateUsersAsync();
            await _userCache.InvalidateUserAsync(id);

            var data = _mapper.Map<UserDto>(user);
            return ApiResult.Success(200, "User updated successfully.", data);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "UpdateUserPartial failed for user {UserId} - {Message}", id, ex.Message);
            return ApiResult.Fail(400, ex.Message);
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateUserProfile(int id, [FromBody] UpdateUserProfileDto dto)
    {
        _logger.LogInformation("UpdateUserProfile called for user {UserId}", id);

        if (!ModelState.IsValid)
        {
            _logger.LogWarning("UpdateUserProfile failed - Invalid model state for user {UserId}", id);
            return ApiResult.Validation(ModelState);
        }

        var user = await _unitOfWork.UserRepository.GetByIdAsync(id);
        if (user == null)
        {
            _logger.LogWarning("UpdateUserProfile failed - User {UserId} not found", id);
            return ApiResult.Fail(404, "User not found");
        }

        // Check for duplicate username if being updated
        if (!string.IsNullOrWhiteSpace(dto.Username) && dto.Username != user.Username)
        {
            var existingUser = await _unitOfWork.UserRepository.GetByUsernameAsync(dto.Username);
            if (existingUser != null)
            {
                _logger.LogWarning("UpdateUserProfile failed - Username {Username} already taken", dto.Username);
                return ApiResult.Fail(400, "Username already taken");
            }
            user.Username = dto.Username.Trim();
        }

        // Check for duplicate email if being updated
        if (!string.IsNullOrWhiteSpace(dto.Email) && dto.Email != user.Email)
        {
            var existingUser = await _unitOfWork.UserRepository.GetByEmailAsync(dto.Email);
            if (existingUser != null)
            {
                _logger.LogWarning("UpdateUserProfile failed - Email {Email} already in use", dto.Email);
                return ApiResult.Fail(400, "Email already in use");
            }
            user.Email = dto.Email.Trim();
        }

        user.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.UserRepository.Update(user);
        await _unitOfWork.CompleteAsync();

        await _userCache.InvalidateUsersAsync();
        await _userCache.InvalidateUserAsync(id);

        var data = _mapper.Map<UserDto>(user);
        _logger.LogInformation("User {UserId} profile updated successfully", id);

        return ApiResult.Success(200, "User profile updated successfully.", data);
    }

    [HttpGet("{id}/licenses")]
    public async Task<ActionResult<Pagination<LicenseDto>>> GetUserLicenses(
       int id,
       [FromQuery] LicenseSpecParams specParams)
    {
        _logger.LogInformation("GetUserLicenses called for user {UserId} - PageIndex: {PageIndex}, PageSize: {PageSize}",
            id, specParams.PageIndex, specParams.PageSize);

        specParams.UserId = id;

        var spec = new LicenseSpecification(specParams);
        var countSpec = new LicenseCountWithFiltersSpecification(specParams);

        var totalItems = await _unitOfWork.LicenseRepository.CountAsync(countSpec);
        var licenses = await _unitOfWork.LicenseRepository.ListAsync(spec);

        var data = _mapper.Map<IReadOnlyList<LicenseDto>>(licenses);

        _logger.LogDebug("Retrieved {LicenseCount} licenses for user {UserId}", licenses.Count, id);

        return Ok(new Pagination<LicenseDto>
        {
            PageIndex = specParams.PageIndex,
            PageSize = specParams.PageSize,
            TotalCount = totalItems,
            Data = data
        });
    }

    [Authorize]
    [HttpGet("license/{key}")]
    public async Task<ActionResult<LicenseDto>> GetLicenseByKey(string key)
    {
        _logger.LogInformation("GetLicenseByKey called for license key: {LicenseKey}", key);

        var license = await _unitOfWork.LicenseRepository.GetByIdAsync(key);

        if (license == null)
        {
            _logger.LogWarning("License with key {LicenseKey} not found", key);
            return ApiResult.Fail(404, "License not found.");
        }

        var data = _mapper.Map<LicenseDto>(license);
        return ApiResult.Success(200, "License retrieved successfully.", data);
    }
}
