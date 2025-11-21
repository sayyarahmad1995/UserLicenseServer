using Api.Errors;
using Api.Helpers;
using AutoMapper;
using Core.DTOs;
using Core.Entities;
using Core.Interfaces;
using Core.Spec;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[Authorize(Roles = "Admin")]
public class UsersController : BaseApiController
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ICacheRepository _cacheRepo;
    private readonly ILogger<UsersController> _logger;
    public UsersController(IUnitOfWork unitOfWork,
    IMapper mapper,
    ICacheRepository cacheRepo,
    ILogger<UsersController> logger)
    {
        _logger = logger;
        _cacheRepo = cacheRepo;
        _mapper = mapper;
        _unitOfWork = unitOfWork;
    }

    private string BuildUsersCacheKey(UserSpecParams p)
    {
        return $"users:p:{p.PageIndex}:s:{p.PageSize}:sort:{p.Sort ?? ""}:q:{p.Search ?? ""}:st:{p.Status ?? ""}";
    }

    [HttpGet]
    public async Task<ActionResult<Pagination<UserDto>>> GetUsers([FromQuery] UserSpecParams specParams)
    {
        var cacheKey = BuildUsersCacheKey(specParams);

        try
        {
            var cached = await _cacheRepo.GetAsync<Pagination<UserDto>>(cacheKey);
            if (cached != null)
                return Ok(cached);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Redis not available: {ex.Message}");
        }

        var spec = new UserSpecification(specParams);
        var countSpec = new UserCountSpecification(specParams);

        var totalItems = await _unitOfWork.UserRepository.CountAsync(countSpec);
        var users = await _unitOfWork.UserRepository.ListAsync(spec);

        var result = new Pagination<UserDto>
        {
            PageIndex = specParams.PageIndex,
            PageSize = specParams.PageSize,
            TotalCount = totalItems,
            Data = _mapper.Map<IReadOnlyList<UserDto>>(users)
        };

        try
        {
            await _cacheRepo.SetAsync(cacheKey, result, TimeSpan.FromMinutes(5));
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to cache users in Redis: {ex.Message}");
        }

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> GetUserById(int id)
    {
        var cacheKey = $"user:{id}";
        UserDto? userDto;
        try
        {
            userDto = await _cacheRepo.GetAsync<UserDto>(cacheKey);
            if (userDto != null)
                return Ok(userDto);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Redis not available: {ex.Message}");
        }

        var user = await _unitOfWork.UserRepository.GetByIdAsync(id);

        if (user == null)
            return NotFound(new { message = "User not found" });

        userDto = _mapper.Map<UserDto>(user);

        try
        {
            await _cacheRepo.SetAsync(cacheKey, userDto, TimeSpan.FromMinutes(10));
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to cache user in Redis: {ex.Message}");
        }

        return Ok(userDto);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var user = await _unitOfWork.UserRepository.GetByIdAsync(id);
        if (user == null)
            return ApiResult.Fail(404, "User not found");

        _unitOfWork.UserRepository.Delete(user);
        await _unitOfWork.CompleteAsync();

        var cacheKey = $"user:{id}";
        await _cacheRepo.PublishInvalidationAsync(cacheKey);

        return ApiResult.Success(200, "User deleted successfully");
    }

    [HttpPatch("{id:int}")]
    public async Task<IActionResult> UpdateUserPartial(int id, [FromBody] StatusUpdateDto dto)
    {
        var user = await _unitOfWork.UserRepository.GetByIdAsync(id);
        if (user == null)
            return NotFound(new ApiResponse(404, "User not found"));

        if (string.IsNullOrWhiteSpace(dto.Status))
            return BadRequest(new ApiResponse(400, "Status is required"));

        var status = dto.Status.Trim().ToLower();

        try
        {
            switch (status)
            {
                case "verify":
                case "verified": user.Verify(); break;
                case "active": user.Activate(); break;
                case "block":
                case "blocked": user.Block(); break;
                case "unblock": user.Unblock(); break;
                default:
                    return BadRequest(new ApiResponse(400, "Invalid status value"));
            }

            _unitOfWork.UserRepository.Update(user);
            await _unitOfWork.CompleteAsync();

            await _cacheRepo.PublishInvalidationAsync($"user:{id}");

            var data = _mapper.Map<UserDto>(user);
            return Ok(new ApiResponse(200, "Status updated", data));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse(400, ex.Message));
        }
    }

    [HttpGet("{id}/licenses")]
    public async Task<ActionResult<Pagination<LicenseDto>>> GetUserLicenses(
       int id,
       [FromQuery] LicenseSpecParams specParams)
    {
        specParams.UserId = id;

        var spec = new LicenseSpecification(specParams);
        var countSpec = new LicenseCountWithFiltersSpecification(specParams);

        var totalItems = await _unitOfWork.LicenseRepository.CountAsync(countSpec);
        var licenses = await _unitOfWork.LicenseRepository.ListAsync(spec);

        var data = _mapper.Map<IReadOnlyList<LicenseDto>>(licenses);

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
        var license = await _unitOfWork.LicenseRepository.GetByIdAsync(key);

        if (license == null)
            return NotFound(new ApiResponse(404, "License not found"));

        var data = _mapper.Map<LicenseDto>(license);
        return Ok(data);
    }
}
