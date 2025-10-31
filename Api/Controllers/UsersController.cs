using Api.DTOs;
using Api.Errors;
using Api.Helpers;
using AutoMapper;
using Core.Entities;
using Core.Interfaces;
using Core.Spec;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

public class UsersController : BaseApiController
{
   private readonly IUnitOfWork _unitOfWork;
   private readonly IMapper _mapper;
   private readonly ICacheRepository _cacheRepo;
   private readonly ILogger<UsersController> _logger;
   public UsersController(IUnitOfWork unitOfWork, IMapper mapper, ICacheRepository cacheRepo, ILogger<UsersController> logger)
   {
      _logger = logger;
      _cacheRepo = cacheRepo;
      _mapper = mapper;
      _unitOfWork = unitOfWork;
   }

   [HttpGet]
   public async Task<ActionResult<Pagination<UserDto>>> GetUsers([FromQuery] UserSpecParams specParams)
   {
      var spec = new UserSpecification(specParams);
      var countSpec = new UserCountSpecification(specParams);

      var totalItems = await _unitOfWork.Repository<User>().CountAsync(countSpec);
      var users = await _unitOfWork.Repository<User>().ListAsync(spec);

      var mappedData = _mapper.Map<IReadOnlyList<UserDto>>(users);
      var data = new Pagination<UserDto>
      {
         PageIndex = specParams.PageIndex,
         PageSize = specParams.PageSize,
         TotalCount = totalItems,
         Data = mappedData
      };
      return Ok(data);
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

      var user = await _unitOfWork.Repository<User>().GetByIdAsync(id);

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

   [HttpPatch("{id}/{status}")]
   public async Task<ActionResult<UserDto>> UserStatus(int id, string status)
   {
      var user = await _unitOfWork.Repository<User>().GetByIdAsync(id);
      if (user == null)
         return NotFound(new ApiResponse(404, "User not found"));
      status = status.Trim().ToLower();

      try
      {
         switch (status)
         {
            case "verify":
            case "verified":
               user.Verify();
               break;
            case "active":
               user.Activate();
               break;
            case "block":
            case "blocked":
               user.Block();
               break;
            case "unblock":
               user.Unblock();
               break;
            default:
               return BadRequest(new ApiResponse(400, "Invalid status value"));
         }
         _unitOfWork.Repository<User>().Update(user);
         await _unitOfWork.SaveChangesAsync();

         var cacheKey = $"user:{id}";
         await _cacheRepo.PublishInvalidationAsync(cacheKey);

         var mapData = _mapper.Map<UserDto>(user);
         return Ok(new ApiResponse(200, message: $"User status updated to {mapData.Status}", data: mapData));
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

      var totalItems = await _unitOfWork.Repository<License>().CountAsync(countSpec);
      var licenses = await _unitOfWork.Repository<License>().ListAsync(spec);

      var data = _mapper.Map<IReadOnlyList<LicenseDto>>(licenses);

      return Ok(new Pagination<LicenseDto>
      {
         PageIndex = specParams.PageIndex,
         PageSize = specParams.PageSize,
         TotalCount = totalItems,
         Data = data
      });
   }
}
