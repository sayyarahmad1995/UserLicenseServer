using Core.DTOs;
using Core.Entities;
using Core.Interfaces;
using Infrastructure.Services.Models;
using Infrastructure.Services.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Infrastructure.Services.Exceptions;

namespace Infrastructure.Services;

public class TokenService : ITokenService
{
    private readonly IConfiguration _config;
    private readonly ICacheRepository _cache;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TokenService> _logger;

    public TokenService(IConfiguration config, ICacheRepository cache, IUnitOfWork unitOfWork, ILogger<TokenService> logger)
    {
        _logger = logger;
        _config = config;
        _cache = cache;
        _unitOfWork = unitOfWork;
    }

    public string GenerateAccessToken(User user)
    {
        _logger.LogDebug("Generating access token for user {UserId} with role {Role}", user.Id, user.Role);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email!),
            new(ClaimTypes.Role, user.Role!),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(int.Parse(_config["Jwt:AccessTokenExpiryMinutes"]!)),
            signingCredentials: creds
        );

        _logger.LogDebug("Access token generated successfully for user {UserId}", user.Id);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<string> GenerateRefreshTokenAsync(User user, string jti)
    {
        _logger.LogDebug("Generating refresh token for user {UserId}", user.Id);

        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var hashedToken = TokenHasher.HashToken(refreshToken);

        var tokenModel = new RefreshToken
        {
            UserId = user.Id.ToString(),
            TokenHash = hashedToken,
            CreatedAt = DateTime.UtcNow,
            Expires = DateTime.UtcNow.AddDays(int.Parse(_config["Jwt:RefreshTokenExpiryDays"]!)),
            Jti = jti
        };

        var key = $"session:{user.Id}:{jti}";
        await _cache.SetAsync(key, tokenModel, tokenModel.Expires - DateTime.UtcNow);

        _logger.LogDebug("Refresh token stored in cache for user {UserId}", user.Id);
        return refreshToken;
    }

    public async Task<TokenResponseDto> RefreshTokenAsync(string refreshToken)
    {
        _logger.LogInformation("Token refresh requested");

        var hashedToken = TokenHasher.HashToken(refreshToken);
        var keys = await _cache.SearchKeysAsync("session:*");
        RefreshToken? matchedToken = null;
        string? matchedKey = null;

        foreach (var key in keys)
        {
            var tokenModel = await _cache.GetAsync<RefreshToken>(key);
            if (tokenModel != null && tokenModel.TokenHash == hashedToken)
            {
                matchedToken = tokenModel;
                matchedKey = key;
                break;
            }
        }

        if (matchedToken == null)
        {
            _logger.LogWarning("Refresh token not found in cache");
            throw new TokenException("Refresh token not found.");
        }

        if (matchedToken.Revoked)
        {
            _logger.LogWarning("Refresh token already revoked for user {UserId}", matchedToken.UserId);
            throw new TokenException("This refresh token has already been used or revoked.");
        }

        if (matchedToken.Expires < DateTime.UtcNow)
        {
            _logger.LogWarning("Refresh token expired for user {UserId}", matchedToken.UserId);
            throw new TokenException("Refresh token has expired.");
        }

        var user = await _unitOfWork.UserRepository.GetByIdAsync(int.Parse(matchedToken.UserId));
        if (user == null)
        {
            _logger.LogError("User {UserId} not found when refreshing token", matchedToken.UserId);
            throw new TokenException("User not found.");
        }

        var newAccessToken = GenerateAccessToken(user);
        var newRefreshToken = await GenerateRefreshTokenAsync(user, matchedToken.Jti);

        matchedToken.Revoked = true;
        await _cache.SetAsync(matchedKey!, matchedToken, matchedToken.Expires - DateTime.UtcNow);

        _logger.LogInformation("Token refresh successful for user {UserId}", user.Id);

        return new TokenResponseDto { AccessToken = newAccessToken, RefreshToken = newRefreshToken };
    }

    public async Task RevokeSessionAsync(int userId, string jti)
    {
        _logger.LogInformation("Revoking session for user {UserId} with JTI {Jti}", userId, jti);

        var key = $"session:{userId}:{jti}";
        var tokenModel = await _cache.GetAsync<RefreshToken>(key);

        if (tokenModel == null)
        {
            _logger.LogWarning("Session not found for user {UserId} with JTI {Jti}", userId, jti);
            return;
        }

        tokenModel.Revoked = true;
        await _cache.SetAsync(key, tokenModel, tokenModel.Expires - DateTime.UtcNow);

        _logger.LogInformation("Session revoked successfully for user {UserId} with JTI {Jti}", userId, jti);
    }

    public async Task RevokeAllSessionsAsync(int userId)
    {
        _logger.LogInformation("Revoking all sessions for user {UserId}", userId);

        var keys = await _cache.SearchKeysAsync($"session:{userId}:*");
        int revokedCount = 0;

        foreach (var key in keys)
        {
            var tokenModel = await _cache.GetAsync<RefreshToken>(key);
            if (tokenModel != null && !tokenModel.Revoked)
            {
                tokenModel.Revoked = true;
                await _cache.SetAsync(key, tokenModel, tokenModel.Expires - DateTime.UtcNow);
                revokedCount++;
            }
        }

        _logger.LogInformation("Revoked {RevokedCount} sessions for user {UserId}", revokedCount, userId);
    }

    public async Task<bool> ValidateRefreshTokenAsync(string refreshToken)
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            _logger.LogDebug("Token validation failed - token is null or empty");
            return false;
        }

        var hashedToken = TokenHasher.HashToken(refreshToken);
        var keys = await _cache.SearchKeysAsync("session:*");

        foreach (var key in keys)
        {
            var tokenModel = await _cache.GetAsync<RefreshToken>(key);
            if (tokenModel != null && tokenModel.TokenHash == hashedToken && !tokenModel.Revoked && tokenModel.Expires >= DateTime.UtcNow)
            {
                _logger.LogDebug("Token validation successful for user {UserId}", tokenModel.UserId);
                return true;
            }
        }

        _logger.LogDebug("Token validation failed - no valid token found");
        return false;
    }
}
