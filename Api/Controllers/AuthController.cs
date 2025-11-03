using Api.DTOs;
using Api.Errors;
using AutoMapper;
using Core.Entities;
using Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

public class AuthController : BaseApiController
{
	private readonly IUnitOfWork _unitOfWork;
	private readonly ICacheRepository _cacheRepo;
	private readonly ILogger<AuthController> _logger;
	private readonly IMapper _mapper;
	public AuthController(IUnitOfWork unitOfWork, ICacheRepository cacheRepo, IMapper mapper, ILogger<AuthController> logger)
	{
		_mapper = mapper;
		_logger = logger;
		_cacheRepo = cacheRepo;
		_unitOfWork = unitOfWork;
	}

	[HttpPost("login")]
	public async Task<IActionResult> UserLogin([FromBody] LoginDto loginDto)
	{
		var user = await _unitOfWork.UserRepository.GetByUsernameAsync(loginDto.Username!);
		if (user == null)
			return NotFound(new ApiResponse(404, "User not found"));

		var mapUser = _mapper.Map<UserDto>(user);

		return Ok(mapUser);
	}
}
