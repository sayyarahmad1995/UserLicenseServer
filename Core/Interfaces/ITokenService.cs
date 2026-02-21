using Core.DTOs;
using Core.Entities;

namespace Core.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(User user);
    Task<string> GenerateRefreshTokenAsync(User user, string jti, CancellationToken ct = default);
    Task<TokenResponseDto> RefreshTokenAsync(string refreshToken, CancellationToken ct = default);
    Task RevokeSessionAsync(int userId, string jti, CancellationToken ct = default);
    Task RevokeAllSessionsAsync(int userId, CancellationToken ct = default);
    Task RevokeByRefreshTokenAsync(string refreshToken, CancellationToken ct = default);
    Task<bool> ValidateRefreshTokenAsync(string refreshToken, CancellationToken ct = default);
}
