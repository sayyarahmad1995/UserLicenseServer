using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Api.Errors;
using Api.Helpers;
using AutoMapper;
using Core.DTOs;
using Core.Entities;
using Core.Enums;
using Core.Interfaces;
using Infrastructure.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

public class AuthController : BaseApiController
{
   private readonly ITokenService _tokenService;
   private readonly IConfiguration _config;
   private readonly IUnitOfWork _unitOfWork;
   private readonly IAuthHelper _authHelper;
   private readonly IAuthService _authService;
   private readonly ILogger<AuthController> _logger;
   private readonly IMapper _mapper;

   public AuthController(ITokenService tokenService,
   ILogger<AuthController> logger,
   IConfiguration config,
   IUnitOfWork unitOfWork,
   IAuthHelper authHelper,
   IAuthService authService,
   IMapper mapper)
   {
      _mapper = mapper;
      _logger = logger;
      _tokenService = tokenService;
      _config = config;
      _unitOfWork = unitOfWork;
      _authHelper = authHelper;
      _authService = authService;
   }

   [HttpPost("login")]
   public async Task<IActionResult> Login([FromBody] LoginDto dto)
   {
      try
      {
         var result = await _authService.LoginAsync(dto, Response);
         return ApiResult.Success(200, result.Message, new { result.AccessTokenExpires });
      }
      catch (UnauthorizedAccessException ex)
      {
         return ApiResult.Fail(401, ex.Message);
      }
      catch (Exception ex)
      {
         _logger.LogError(ex, "Unexpected error during login");
         return ApiResult.Fail(500, "Internal server error");
      }
   }

   [HttpPost("register")]
   public async Task<IActionResult> Register([FromBody] RegisterDto dto)
   {
      if (!ModelState.IsValid)
         return ApiResult.Fail(400, "Invalid input data.", ModelState);

      if (await _unitOfWork.UserRepository.GetByEmailAsync(dto.Email!) is not null)
         return ApiResult.Fail(400, "Email already registered.");

      if (await _unitOfWork.UserRepository.GetByUsernameAsync(dto.Username!) is not null)
         return ApiResult.Fail(400, "Username already taken.");

      var user = _mapper.Map<User>(dto);

      user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password!);
      user.CreatedAt = DateTime.UtcNow;
      user.Status = UserStatus.Unverified;

      _unitOfWork.UserRepository.Add(user);
      await _unitOfWork.CompleteAsync();

      return ApiResult.Success(message: "Registered successfully.", statusCode: 200);
   }

   [HttpPost("refresh")]
   public async Task<IActionResult> RefreshToken()
   {
      if (!_authHelper.TryGetCookie(Request, "refreshToken", out var refreshToken))
         return Unauthorized(ApiResult.Fail(401, "Refresh token missing."));

      TokenResponseDto result;
      try
      {
         result = await _tokenService.RefreshTokenAsync(refreshToken!);
      }
      catch (Exception ex)
      {
         _logger.LogError(ex, "Error refreshing token.");
         return ApiResult.Fail(500, "Internal server error while refreshing token.");
      }

      await _authHelper.SetAuthCookiesAsync(Response, result.AccessToken, result.RefreshToken, _config);

      var accessExpiryMinutes = int.Parse(_config["Jwt:AccessTokenExpiryMinutes"]!);
      return ApiResult.Success(200, "Token refreshed successfully.", new
      {
         AccessTokenExpires = DateTime.UtcNow.AddMinutes(accessExpiryMinutes)
      });
   }

   [HttpPost("logout")]
   public async Task<IActionResult> Logout()
   {
      var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
      var jti = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

      if (userId == null || jti == null)
         return ApiResult.Fail(400, "Invalid session.");

      await _tokenService.RevokeSessionAsync(int.Parse(userId), jti);
      _authHelper.ClearAuthCookies(Response);

      return ApiResult.Success(200, "Logged out successfully.");
   }

   [HttpPost("logout-all")]
   public async Task<IActionResult> LogoutAll()
   {
      var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
      if (userId == null)
         return Unauthorized(new ApiResponse(401, "Unauthorized"));

      await _tokenService.RevokeAllSessionsAsync(int.Parse(userId));
      _authHelper.ClearAuthCookies(Response);

      return ApiResult.Success(200, "All sessions logged out successfully.");
   }
}
