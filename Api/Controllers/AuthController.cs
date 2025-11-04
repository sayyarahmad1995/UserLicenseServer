using Core.DTOs;
using Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

public class AuthController : BaseApiController
{
	private readonly ITokenService _tokenService;
	private readonly IConfiguration _config;
	private readonly IUnitOfWork _unitOfWork;

	public AuthController(
		ITokenService tokenService,
		IConfiguration config,
		IUnitOfWork unitOfWork)
	{
		_tokenService = tokenService;
		_config = config;
		_unitOfWork = unitOfWork;
	}

	[HttpPost("login")]
	public async Task<IActionResult> Login([FromBody] LoginDto dto)
	{
		var user = await _unitOfWork.UserRepository.GetByUsernameAsync(dto.Username);
		if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
			return Unauthorized("Invalid credentials");

		var accessToken = _tokenService.GenerateAccessToken(user);

		var refreshToken = await _tokenService.GenerateRefreshTokenAsync(user);
		var refreshTokenExpiryDays = int.Parse(_config["Jwt:RefreshTokenExpiryDays"]!);

		var cookieOptions = new CookieOptions
		{
			HttpOnly = true,
			Secure = false,
			SameSite = SameSiteMode.None,
			Expires = DateTime.UtcNow.AddDays(refreshTokenExpiryDays)
		};
		Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);

		return Ok(new
		{
			AccessToken = accessToken,
			AccessTokenExpires = DateTime.UtcNow.AddMinutes(int.Parse(_config["Jwt:AccessTokenExpiryMinutes"]!))
		});
	}

	[HttpPost("refresh")]
	public async Task<IActionResult> Refresh()
	{
		if (!Request.Cookies.TryGetValue("refreshToken", out var existingRefreshToken))
			return Unauthorized("Refresh token missing.");

		var tokenResponse = await _tokenService.RefreshTokenAsync(existingRefreshToken);

		var cookieOptions = new CookieOptions
		{
			HttpOnly = true,
			Secure = false,
			SameSite = SameSiteMode.Strict,
			Expires = tokenResponse.RefreshTokenExpires
		};
		Response.Cookies.Append("refreshToken", tokenResponse.RefreshToken, cookieOptions);

		return Ok(tokenResponse);
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
}
