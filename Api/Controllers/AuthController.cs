using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Api.Errors;
using Core.DTOs;
using Core.Interfaces;
using Infrastructure.Interfaces;
using Infrastructure.Services.Models;
using Infrastructure.Services.Security;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

public class AuthController : BaseApiController
{
	private readonly ITokenService _tokenService;
	private readonly IConfiguration _config;
	private readonly IUnitOfWork _unitOfWork;
	private readonly ICacheRepository _cacheRepo;
	private readonly IAuthHelper _authHelper;

	public AuthController(
		ITokenService tokenService,
		IConfiguration config,
		IUnitOfWork unitOfWork,
		ICacheRepository cacheRepo,
		IAuthHelper authHelper)
	{
		_tokenService = tokenService;
		_config = config;
		_unitOfWork = unitOfWork;
		_cacheRepo = cacheRepo;
		_authHelper = authHelper;
	}

	[HttpPost("login")]
	public async Task<IActionResult> Login([FromBody] LoginDto dto)
	{
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

		var result = await _tokenService.RefreshTokenAsync(refreshToken!);
		if (result == null)
			return Unauthorized(new ApiResponse(401, "Invalid or expired refresh token."));

		await _authHelper.SetAuthCookiesAsync(Response, result.AccessToken, result.RefreshToken, _config);

		return Ok(new
		{
			Message = "Token refreshed successfully",
			result.AccessTokenExpires
		});
	}

	[HttpPost("revoke")]
	public async Task<IActionResult> Revoke()
	{
		if (!_authHelper.TryGetCookie(Request, "refreshToken", out var refreshToken))
			return BadRequest(new ApiResponse(400, "Refresh token missing"));

		try
		{
			var hashed = TokenHasher.HashToken(refreshToken!);
			var keys = await _cacheRepo.SearchKeysAsync("user:*:session:*");

			foreach (var key in keys)
			{
				var token = await _cacheRepo.GetAsync<RefreshToken>(key);
				if (token != null && token.TokenHash == hashed)
				{
					token.Revoked = true;
					token.RevokedAt = DateTime.UtcNow;
					await _cacheRepo.SetAsync(key, token, token.Expires - DateTime.UtcNow);
					break;
				}
			}

			_authHelper.ClearAuthCookies(Response);

			return Ok(new { message = "Session revoked successfully." });
		}
		catch (Exception ex)
		{
			return BadRequest(new ApiResponse(ex.HResult, ex.Message));
		}
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
