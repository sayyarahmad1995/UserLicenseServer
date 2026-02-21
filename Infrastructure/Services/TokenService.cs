using Core.DTOs;
using Core.Entities;
using Core.Helpers;
using Core.Interfaces;
using Infrastructure.Services.Models;
using Infrastructure.Services.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Infrastructure.Services.Exceptions;

namespace Infrastructure.Services;

public class TokenService : ITokenService
{
    private readonly ICacheRepository _cache;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TokenService> _logger;

    // Cached config values - avoid repeated config lookups
    private readonly SymmetricSecurityKey _signingKey;
    private readonly SigningCredentials _signingCredentials;
    private readonly string _jwtIssuer;
    private readonly string _jwtAudience;
    private readonly int _accessTokenExpiryMinutes;
    private readonly int _refreshTokenExpiryDays;
    private readonly JwtSecurityTokenHandler _tokenHandler = new();

    public TokenService(IOptions<JwtSettings> jwtOptions, ICacheRepository cache, IUnitOfWork unitOfWork, ILogger<TokenService> logger)
    {
        _logger = logger;
        _cache = cache;
        _unitOfWork = unitOfWork;

        var jwt = jwtOptions.Value;
        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key));
        _signingCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha512);
        _jwtIssuer = jwt.Issuer;
        _jwtAudience = jwt.Audience;
        _accessTokenExpiryMinutes = jwt.AccessTokenExpiryMinutes;
        _refreshTokenExpiryDays = jwt.RefreshTokenExpiryDays;
    }

    public string GenerateAccessToken(User user)
    {
        _logger.LogDebug("Generating access token for user {UserId} with role {Role}", user.Id, user.Role);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _jwtIssuer,
            audience: _jwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_accessTokenExpiryMinutes),
            signingCredentials: _signingCredentials
        );

        _logger.LogDebug("Access token generated successfully for user {UserId}", user.Id);
        return _tokenHandler.WriteToken(token);
    }

    public async Task<string> GenerateRefreshTokenAsync(User user, string jti, CancellationToken ct = default)
    {
        _logger.LogDebug("Generating refresh token for user {UserId}", user.Id);

        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var hashedToken = TokenHasher.HashToken(refreshToken);

        var tokenModel = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = hashedToken,
            CreatedAt = DateTime.UtcNow,
            Expires = DateTime.UtcNow.AddDays(_refreshTokenExpiryDays),
            Jti = jti
        };

        var key = CacheKeys.Session(user.Id, jti);
        var ttl = tokenModel.Expires - DateTime.UtcNow;
        await _cache.SetAsync(key, tokenModel, ttl, ct);

        // Store reverse index: tokenHash → session key for O(1) lookup
        var indexKey = CacheKeys.TokenIndex(hashedToken);
        await _cache.SetAsync(indexKey, key, ttl, ct);

        _logger.LogDebug("Refresh token stored in cache for user {UserId}", user.Id);
        return refreshToken;
    }

    public async Task<TokenResponseDto> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        _logger.LogInformation("Token refresh requested");

        var hashedToken = TokenHasher.HashToken(refreshToken);

        // O(1) lookup via reverse index instead of scanning all sessions
        var indexKey = CacheKeys.TokenIndex(hashedToken);
        var matchedKey = await _cache.GetAsync<string>(indexKey, ct);

        if (matchedKey == null)
        {
            _logger.LogWarning("Refresh token not found in cache");
            throw new TokenException("Refresh token not found.");
        }

        var matchedToken = await _cache.GetAsync<RefreshToken>(matchedKey, ct);

        if (matchedToken == null)
        {
            _logger.LogWarning("Session key found in index but session data missing");
            await _cache.RemoveAsync(indexKey, ct);
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

        var user = await _unitOfWork.UserRepository.GetByIdAsync(matchedToken.UserId, ct);
        if (user == null)
        {
            _logger.LogError("User {UserId} not found when refreshing token", matchedToken.UserId);
            throw new TokenException("User not found.");
        }

        var newAccessToken = GenerateAccessToken(user);
        var newRefreshToken = await GenerateRefreshTokenAsync(user, matchedToken.Jti, ct);

        // Revoke old token and clean up its reverse index
        matchedToken.Revoked = true;
        await _cache.SetAsync(matchedKey!, matchedToken, matchedToken.Expires - DateTime.UtcNow, ct);
        await _cache.RemoveAsync(indexKey, ct);

        _logger.LogInformation("Token refresh successful for user {UserId}", user.Id);

        return new TokenResponseDto { AccessToken = newAccessToken, RefreshToken = newRefreshToken };
    }

    public async Task RevokeSessionAsync(int userId, string jti, CancellationToken ct = default)
    {
        _logger.LogInformation("Revoking session for user {UserId} with JTI {Jti}", userId, jti);

        var key = CacheKeys.Session(userId, jti);
        var tokenModel = await _cache.GetAsync<RefreshToken>(key, ct);

        if (tokenModel == null)
        {
            _logger.LogWarning("Session not found for user {UserId} with JTI {Jti}", userId, jti);
            return;
        }

        tokenModel.Revoked = true;
        await _cache.SetAsync(key, tokenModel, tokenModel.Expires - DateTime.UtcNow, ct);

        // Clean up reverse index
        if (!string.IsNullOrEmpty(tokenModel.TokenHash))
            await _cache.RemoveAsync(CacheKeys.TokenIndex(tokenModel.TokenHash), ct);

        _logger.LogInformation("Session revoked successfully for user {UserId} with JTI {Jti}", userId, jti);
    }

    public async Task RevokeAllSessionsAsync(int userId, CancellationToken ct = default)
    {
        _logger.LogInformation("Revoking all sessions for user {UserId}", userId);

        var keys = await _cache.SearchKeysAsync(CacheKeys.SessionPattern(userId));
        int revokedCount = 0;

        foreach (var key in keys)
        {
            ct.ThrowIfCancellationRequested();
            var tokenModel = await _cache.GetAsync<RefreshToken>(key, ct);
            if (tokenModel != null && !tokenModel.Revoked)
            {
                tokenModel.Revoked = true;
                await _cache.SetAsync(key, tokenModel, tokenModel.Expires - DateTime.UtcNow, ct);

                // Clean up reverse index
                if (!string.IsNullOrEmpty(tokenModel.TokenHash))
                    await _cache.RemoveAsync(CacheKeys.TokenIndex(tokenModel.TokenHash), ct);

                revokedCount++;
            }
        }

        _logger.LogInformation("Revoked {RevokedCount} sessions for user {UserId}", revokedCount, userId);
    }

    public async Task<bool> ValidateRefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            _logger.LogDebug("Token validation failed - token is null or empty");
            return false;
        }

        var hashedToken = TokenHasher.HashToken(refreshToken);

        // O(1) lookup via reverse index
        var indexKey = CacheKeys.TokenIndex(hashedToken);
        var sessionKey = await _cache.GetAsync<string>(indexKey, ct);

        if (sessionKey == null)
        {
            _logger.LogDebug("Token validation failed - no reverse index found");
            return false;
        }

        var tokenModel = await _cache.GetAsync<RefreshToken>(sessionKey, ct);
        if (tokenModel != null && tokenModel.TokenHash == hashedToken && !tokenModel.Revoked && tokenModel.Expires >= DateTime.UtcNow)
        {
            _logger.LogDebug("Token validation successful for user {UserId}", tokenModel.UserId);
            return true;
        }

        _logger.LogDebug("Token validation failed - token revoked or expired");
        return false;
    }

    public async Task RevokeByRefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(refreshToken)) return;

        var hashedToken = TokenHasher.HashToken(refreshToken);

        // O(1) lookup via reverse index
        var indexKey = CacheKeys.TokenIndex(hashedToken);
        var sessionKey = await _cache.GetAsync<string>(indexKey, ct);

        if (sessionKey == null) return;

        var tokenModel = await _cache.GetAsync<RefreshToken>(sessionKey, ct);
        if (tokenModel != null && !tokenModel.Revoked)
        {
            tokenModel.Revoked = true;
            await _cache.SetAsync(sessionKey, tokenModel, tokenModel.Expires - DateTime.UtcNow, ct);
            await _cache.RemoveAsync(indexKey, ct);
            _logger.LogInformation("Revoked session {Key} by refresh token", sessionKey);
        }
    }
}
