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
        var cached = await _userCache.GetUsersAsync(p);
        if (cached != null)
            return Ok(cached);

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

        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<UserDto>> GetUserById(int id)
    {
        var cached = await _userCache.GetUserAsync(id);
        if (cached != null)
            return Ok(cached);

        var user = await _unitOfWork.UserRepository.GetByIdAsync(id);
        if (user == null)
            return NotFound();

        var dto = _mapper.Map<UserDto>(user);
        await _userCache.CacheUserAsync(id, dto);

        return Ok(dto);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var user = await _unitOfWork.UserRepository.GetByIdAsync(id);
        if (user == null)
            return ApiResult.Fail(404, "User not found");

        _unitOfWork.UserRepository.Delete(user);
        await _unitOfWork.CompleteAsync();

        await _userCache.InvalidateUsersAsync();
        await _userCache.InvalidateUserAsync(id);

        return NoContent();
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

            await _userCache.InvalidateUsersAsync();
            await _userCache.InvalidateUserAsync(id);

            var data = _mapper.Map<UserDto>(user);
            return NoContent();
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
