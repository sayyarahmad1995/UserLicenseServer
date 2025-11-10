using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Core.DTOs;
using Core.Entities;
using Core.Interfaces;
using Infrastructure.Services.Models;
using Infrastructure.Services.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

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

      return new JwtSecurityTokenHandler().WriteToken(token);
   }

   public async Task<string> GenerateRefreshTokenAsync(User user, string jti)
   {
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

      return refreshToken;
   }

   public async Task<TokenResponseDto> RefreshTokenAsync(string refreshToken)
   {
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
         throw new Exception("Refresh token not found.");

      if (matchedToken.Revoked)
         throw new Exception("This refresh token has already been used or revoked.");

      if (matchedToken.Expires < DateTime.UtcNow)
         throw new Exception("Refresh token has expired.");

      var userId = int.Parse(matchedToken.UserId);
      var user = await _unitOfWork.Repository<User>().GetByIdAsync(userId)
         ?? throw new Exception("User not found.");

      var newAccessToken = GenerateAccessToken(user);
      var jwt = new JwtSecurityTokenHandler().ReadJwtToken(newAccessToken);
      var newJti = jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;

      var newRefreshToken = await GenerateRefreshTokenAsync(user, newJti);

      matchedToken.Revoked = true;
      matchedToken.RevokedAt = DateTime.UtcNow;
      matchedToken.ReplacedByTokenId = newJti;

      if (matchedKey != null)
         await _cache.SetAsync(matchedKey, matchedToken, matchedToken.Expires - DateTime.UtcNow);

      return new TokenResponseDto
      {
         AccessToken = newAccessToken,
         RefreshToken = newRefreshToken,
         AccessTokenExpires = DateTime.UtcNow.AddMinutes(int.Parse(_config["Jwt:AccessTokenExpiryMinutes"]!)),
         RefreshTokenExpires = DateTime.UtcNow.AddDays(int.Parse(_config["Jwt:RefreshTokenExpiryDays"]!))
      };
   }

   public async Task RevokeSessionAsync(int userId, string jti)
   {
      var key = $"session:{userId}:{jti}";
      var token = await _cache.GetAsync<RefreshToken>(key)
         ?? throw new Exception("Session not found.");

      if (token.Revoked) return;

      token.Revoked = true;
      token.RevokedAt = DateTime.UtcNow;
      await _cache.SetAsync(key, token, token.Expires - DateTime.UtcNow);
   }

   public async Task RevokeAllSessionsAsync(int userId)
   {
      var keys = await _cache.SearchKeysAsync($"session:{userId}:*");

      foreach (var key in keys)
      {
         var token = await _cache.GetAsync<RefreshToken>(key);
         if (token != null && !token.Revoked)
         {
            token.Revoked = true;
            token.RevokedAt = DateTime.UtcNow;
            await _cache.SetAsync(key, token, token.Expires - DateTime.UtcNow);
         }
      }
   }

   public async Task<bool> ValidateRefreshTokenAsync(string refreshToken)
   {
      if (string.IsNullOrWhiteSpace(refreshToken))
         return false;

      var hashedToken = TokenHasher.HashToken(refreshToken);
      var keys = await _cache.SearchKeysAsync("session:*");

      foreach (var key in keys)
      {
         var token = await _cache.GetAsync<RefreshToken>(key);
         if (token == null || token.TokenHash != hashedToken)
            continue;

         if (token.Revoked)
         {
            _logger.LogWarning("Attempted use of revoked refresh token: {Key}", key);
            return false;
         }

         if (token.Expires < DateTime.UtcNow)
         {
            _logger.LogInformation("Expired refresh token found: {Key}", key);
            return false;
         }

         return true;
      }

      _logger.LogInformation("No matching refresh token found in Redis.");
      return false;
   }
}
