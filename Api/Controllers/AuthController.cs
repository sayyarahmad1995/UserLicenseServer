using Core.DTOs;
using Core.Interfaces;
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

	public AuthController(ITokenService tokenService,
	IConfiguration config,
	IUnitOfWork unitOfWork,
	ICacheRepository cacheRepo)
	{
		_tokenService = tokenService;
		_config = config;
		_unitOfWork = unitOfWork;
		_cacheRepo = cacheRepo;
	}

	[HttpPost("login")]
	public async Task<IActionResult> Login([FromBody] LoginDto dto)
	{
		var user = await _unitOfWork.UserRepository.GetByUsernameAsync(dto.Username);
		if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
			return Unauthorized("Invalid credentials");

		var accessToken = _tokenService.GenerateAccessToken(user);
		var AccessTokenExpiryMinutes = int.Parse(_config["jwt:AccessTokenExpiryMinutes"]!);

		var refreshToken = await _tokenService.GenerateRefreshTokenAsync(user);
		var refreshTokenExpiryDays = int.Parse(_config["Jwt:RefreshTokenExpiryDays"]!);

		var refresCookieOptions = new CookieOptions
		{
			HttpOnly = true,
			Secure = false,
			SameSite = SameSiteMode.None,
			Expires = DateTime.UtcNow.AddDays(refreshTokenExpiryDays)
		};
		Response.Cookies.Append("refreshToken", refreshToken, refresCookieOptions);

		var accessCookiesOptions = new CookieOptions
		{
			HttpOnly = true,
			Secure = false,
			SameSite = SameSiteMode.None,
			Expires = DateTime.UtcNow.AddMinutes(AccessTokenExpiryMinutes)
		};
		Response.Cookies.Append("accessToken", accessToken, accessCookiesOptions);

		return Ok(new
		{
			Message = "Login successful",
			AccessTokenExpires = DateTime.UtcNow.AddMinutes(AccessTokenExpiryMinutes)
		});
	}

	[HttpPost("refresh")]
	public async Task<IActionResult> RefreshToken()
	{
		if (!Request.Cookies.TryGetValue("refreshToken", out var refreshToken) || string.IsNullOrEmpty(refreshToken))
			return Unauthorized(new { message = "Refresh token missing." });

		var result = await _tokenService.RefreshTokenAsync(refreshToken);
		if (result == null)
			return Unauthorized(new { message = "Invalid or expired refresh token." });

		var accessExpiryMinutes = int.Parse(_config["Jwt:AccessTokenExpiryMinutes"]!);
		var refreshExpiryDays = int.Parse(_config["Jwt:RefreshTokenExpiryDays"]!);

		Response.Cookies.Append("accessToken", result.AccessToken, new CookieOptions
		{
			HttpOnly = true,
			Secure = false,
			SameSite = SameSiteMode.None,
			Expires = DateTime.UtcNow.AddMinutes(accessExpiryMinutes)
		});

		Response.Cookies.Append("refreshToken", result.RefreshToken, new CookieOptions
		{
			HttpOnly = true,
			Secure = false,
			SameSite = SameSiteMode.None,
			Expires = DateTime.UtcNow.AddDays(refreshExpiryDays)
		});

		return Ok(new
		{
			Message = "Token refreshed successfully",
			result.AccessTokenExpires
		});
	}

	[HttpPost("revoke")]
	public async Task<IActionResult> Revoke()
	{
		if (!Request.Cookies.TryGetValue("refreshToken", out var refreshToken))
			return BadRequest("Refresh token missing");

		try
		{
			await _tokenService.RevokeRefreshTokenAsync(refreshToken);

			// Delete the cookie
			Response.Cookies.Delete("refreshToken");

			return NoContent();
		}
		catch (Exception ex)
		{
			return BadRequest(new { message = ex.Message });
		}
	}

	[HttpPost("logout")]
	public async Task<IActionResult> Logout()
	{
		// Check if refresh token cookie exists
		if (!Request.Cookies.TryGetValue("refreshToken", out var refreshToken))
			return Ok(new { message = "Already logged out." });

		try
		{
			// Hash the token to match Redis storage format
			var hashedToken = TokenHasher.HashToken(refreshToken);

			// Find and revoke it in Redis
			var keys = await _cacheRepo.SearchKeysAsync("refresh:*");
			foreach (var key in keys)
			{
				var tokenModel = await _cacheRepo.GetAsync<RefreshToken>(key);
				if (tokenModel != null && tokenModel.TokenHash == hashedToken)
				{
					tokenModel.Revoked = true;
					tokenModel.RevokedAt = DateTime.UtcNow;
					await _cacheRepo.SetAsync(key, tokenModel, tokenModel.Expires - DateTime.UtcNow);
					break;
				}
			}

			// Remove both tokens from cookies
			Response.Cookies.Delete("refreshToken", new CookieOptions
			{
				HttpOnly = true,
				Secure = false, // true in production (HTTPS)
				SameSite = SameSiteMode.None
			});
			Response.Cookies.Delete("accessToken", new CookieOptions
			{
				HttpOnly = true,
				Secure = false,
				SameSite = SameSiteMode.None
			});

			return Ok(new { message = "Logged out successfully." });
		}
		catch (Exception ex)
		{
			return BadRequest(new { message = $"Logout failed: {ex.Message}" });
		}
	}

}
