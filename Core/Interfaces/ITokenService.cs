using Core.DTOs;
using Core.Entities; 

namespace Core.Interfaces;

public interface ITokenService
{
    /// <summary>
    /// Generates a new access token for the given user.
    /// </summary>
    string GenerateAccessToken(User user);

    /// <summary>
    /// Generates a new refresh token and stores it in Redis.
    /// </summary>
    Task<string> GenerateRefreshTokenAsync(User user);

    /// <summary>
    /// Refreshes the access token using a valid refresh token.
    /// Also handles rotation (issues a new refresh token) and revokes the old one.
    /// </summary>
    Task<TokenResponseDto> RefreshTokenAsync(string refreshToken);

    /// <summary>
    /// Revokes a specific refresh token.
    /// </summary>
    Task RevokeRefreshTokenAsync(string refreshToken);

    /// <summary>
    /// Checks if a refresh token is valid and not revoked.
    /// </summary>
    Task<bool> IsRefreshTokenValidAsync(string refreshToken, string tokenId);
}
