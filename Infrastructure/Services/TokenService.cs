using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Core.Entities;
using Core.Interfaces;
using Core.DTOs;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using Infrastructure.Services.Security;
using Infrastructure.Services.Models;

namespace Infrastructure.Services;

public class TokenService : ITokenService
{
	private readonly IConfiguration _config;
	private readonly ICacheRepository _cache;
	private readonly IUnitOfWork _unitOfWork;

	public TokenService(IConfiguration config, ICacheRepository cache, IUnitOfWork unitOfWork)
	{
		_unitOfWork = unitOfWork;
		_config = config;
		_cache = cache;
	}

	/// <summary>
	/// Generates a JWT access token for the given user
	/// </summary>
	public string GenerateAccessToken(User user)
	{
		var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
		var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

		// Base claims
		var claims = new List<Claim>
		{
			new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
			new(JwtRegisteredClaimNames.Email, user.Email!),
			new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
		};

		// Add roles dynamically if the user has them
		if (!string.IsNullOrEmpty(user.Role))
		{
			claims.Add(new Claim("role", user.Role));
		}

		var token = new JwtSecurityToken(
			issuer: _config["Jwt:Issuer"],
			audience: _config["Jwt:Audience"],
			claims: claims,
			expires: DateTime.UtcNow.AddMinutes(int.Parse(_config["Jwt:AccessTokenExpiryMinutes"]!)),
			signingCredentials: creds
		);

		return new JwtSecurityTokenHandler().WriteToken(token);
	}


	public async Task<string> GenerateRefreshTokenAsync(User user)
	{
		var randomBytes = new byte[32];
		using var rng = RandomNumberGenerator.Create();
		rng.GetBytes(randomBytes);
		var refreshToken = Convert.ToBase64String(randomBytes);

		var hashedToken = TokenHasher.HashToken(refreshToken);

		var tokenId = Guid.NewGuid().ToString();
		var tokenModel = new RefreshToken
		{
			TokenId = tokenId,
			UserId = user.Id.ToString(),
			TokenHash = hashedToken,
			Created = DateTime.UtcNow,
			Expires = DateTime.UtcNow.AddDays(int.Parse(_config["Jwt:RefreshTokenExpiryDays"]!)),
			Revoked = false
		};

		var key = $"refresh:{user.Id}:{tokenId}";

		await _cache.SetAsync(key, tokenModel, tokenModel.Expires - DateTime.UtcNow);

		return refreshToken;
	}

	public async Task<TokenResponseDto> RefreshTokenAsync(string refreshToken)
	{
		var hashedToken = TokenHasher.HashToken(refreshToken);

		var keys = await _cache.SearchKeysAsync("refresh:*");

		RefreshToken? matchedToken = null;

		foreach (var key in keys)
		{
			var tokenModel = await _cache.GetAsync<RefreshToken>(key);
			if (tokenModel != null && tokenModel.TokenHash == hashedToken)
			{
				matchedToken = tokenModel;
				break;
			}
		}

		if (matchedToken == null)
			throw new Exception("Refresh token not found.");

		if (matchedToken.Revoked || matchedToken.Expires < DateTime.UtcNow)
			throw new Exception("Invalid or expired refresh token.");

		var userId = int.Parse(matchedToken.UserId);
		var user = await _unitOfWork.Repository<User>().GetByIdAsync(userId)
				   ?? throw new Exception("User not found.");

		var newAccessToken = GenerateAccessToken(user);

		var newRefreshToken = await GenerateRefreshTokenAsync(user);

		matchedToken.Revoked = true;
		matchedToken.RevokedAt = DateTime.UtcNow;
		matchedToken.ReplacedByTokenId = Guid.NewGuid().ToString();
		var oldKey = $"refresh:{user.Id}:{matchedToken.TokenId}";
		await _cache.SetAsync(oldKey, matchedToken, matchedToken.Expires - DateTime.UtcNow);

		return new TokenResponseDto
		{
			AccessToken = newAccessToken,
			RefreshToken = newRefreshToken,
			AccessTokenExpires = DateTime.UtcNow.AddMinutes(int.Parse(_config["Jwt:AccessTokenExpiryMinutes"]!)),
			RefreshTokenExpires = DateTime.UtcNow.AddDays(int.Parse(_config["Jwt:RefreshTokenExpiryDays"]!))
		};
	}

	public async Task RevokeRefreshTokenAsync(string tokenId)
	{
		var key = $"refresh:{tokenId}";

		var tokenModel = await _cache.GetAsync<RefreshToken>(key) ?? throw new Exception("Refresh token not found.");
		tokenModel.Revoked = true;
		tokenModel.RevokedAt = DateTime.UtcNow;

		var expiry = tokenModel.Expires - DateTime.UtcNow;
		await _cache.SetAsync(key, tokenModel, expiry > TimeSpan.Zero ? expiry : TimeSpan.Zero);
	}

	public async Task<bool> IsRefreshTokenValidAsync(string refreshToken, string tokenId)
	{
		var key = $"refresh:{tokenId}";

		var tokenModel = await _cache.GetAsync<RefreshToken>(key);
		if (tokenModel == null)
			return false;

		if (tokenModel.Revoked || tokenModel.Expires < DateTime.UtcNow)
			return false;

		var hashedToken = TokenHasher.HashToken(refreshToken);
		if (tokenModel.TokenHash != hashedToken)
			return false;

		return true;
	}
}
