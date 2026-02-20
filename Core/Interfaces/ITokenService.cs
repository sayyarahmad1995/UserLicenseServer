using Core.DTOs;
using Core.Entities;

namespace Core.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(User user);
    Task<string> GenerateRefreshTokenAsync(User user, string jti);
    Task<TokenResponseDto> RefreshTokenAsync(string refreshToken);
    Task RevokeSessionAsync(int userId, string jti);
    Task RevokeAllSessionsAsync(int userId);
    Task RevokeByRefreshTokenAsync(string refreshToken);
    public Task<bool> ValidateRefreshTokenAsync(string refreshToken);
}
