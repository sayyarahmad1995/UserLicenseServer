using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Api.Errors;
using Api.Helpers;
using Core.DTOs;
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
   private readonly ILogger<AuthController> _logger;

   public AuthController(ITokenService tokenService, ILogger<AuthController> logger, IConfiguration config, IUnitOfWork unitOfWork, IAuthHelper authHelper)
   {
      _logger = logger;
      _tokenService = tokenService;
      _config = config;
      _unitOfWork = unitOfWork;
      _authHelper = authHelper;
   }

   [HttpPost("login")]
   public async Task<IActionResult> Login([FromBody] LoginDto dto)
   {
      if (_authHelper.TryGetCookie(Request, "refreshToken", out var existingRefreshToken))
      {
         var isValid = await _tokenService.ValidateRefreshTokenAsync(existingRefreshToken!);
         if (isValid)
         {
            return Ok(new
            {
               Message = "You are already signed in",
            });
         }
      }

      var user = await _unitOfWork.UserRepository.GetByUsernameAsync(dto.Username);
      if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
         return Unauthorized(new ApiResponse(401, "Invalid credentials"));

      var accessToken = _tokenService.GenerateAccessToken(user);
      var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
      var jti = jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;

      var refreshToken = await _tokenService.GenerateRefreshTokenAsync(user, jti);

      await _authHelper.SetAuthCookiesAsync(Response, accessToken, refreshToken, _config);

      var accessExpiryMinutes = int.Parse(_config["Jwt:AccessTokenExpiryMinutes"]!);
      return Ok(new
      {
         Message = "Login successful",
         AccessTokenExpires = DateTime.UtcNow.AddMinutes(accessExpiryMinutes)
      });
   }

   [HttpPost("refresh")]
   public async Task<IActionResult> RefreshToken()
   {
      if (!_authHelper.TryGetCookie(Request, "refreshToken", out var refreshToken))
         return Unauthorized(new ApiResponse(401, "Refresh token missing."));

      TokenResponseDto? result;
      try
      {
         result = await _tokenService.RefreshTokenAsync(refreshToken!);
      }
      catch (Exception ex)
      {
         _logger.LogError(ex, $"Error refreshing token.");
         return ApiResult.Fail("Internal server error while refreshing token.", 500);
      }
      if (result == null)
         return Unauthorized(new ApiResponse(401, "Invalid or expired refresh token."));

      await _authHelper.SetAuthCookiesAsync(Response, result.AccessToken, result.RefreshToken, _config);

      return Ok(new
      {
         Message = "Token refreshed successfully",
         result.AccessTokenExpires
      });
   }

   [HttpPost("logout")]
   public async Task<IActionResult> Logout()
   {
      var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
      var jti = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

      if (userId == null || jti == null)
         return BadRequest(new ApiResponse(400, "Invalid session."));

      await _tokenService.RevokeSessionAsync(int.Parse(userId), jti);
      _authHelper.ClearAuthCookies(Response);

      return Ok(new { message = "Logged out successfully." });
   }

   [HttpPost("logout-all")]
   public async Task<IActionResult> LogoutAll()
   {
      var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
      if (userId == null)
         return Unauthorized(new ApiResponse(401, "Unauthorized"));

      await _tokenService.RevokeAllSessionsAsync(int.Parse(userId));
      _authHelper.ClearAuthCookies(Response);

      return Ok(new { message = "All sessions logged out successfully." });
   }
}
