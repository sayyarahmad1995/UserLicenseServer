using Core.DTOs;
using Core.Interfaces;
using Infrastructure.Interfaces;
using Infrastructure.Services.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.IdentityModel.Tokens.Jwt;

namespace Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;
    private readonly IAuthHelper _authHelper;
    private readonly IConfiguration _config;
    private readonly ILogger<AuthService> _logger;

    public AuthService(IUnitOfWork unitOfWork,
    ITokenService tokenService,
    IAuthHelper authHelper,
    IConfiguration config,
    ILogger<AuthService> logger)
    {
        _config = config;
        _authHelper = authHelper;
        _tokenService = tokenService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<LoginResultDto> LoginAsync(LoginDto dto, HttpResponse response)
    {
        _logger.LogInformation("Login attempt initiated");

        try
        {
            // If this browser already has a session, revoke it to prevent duplicate sessions
            if (_authHelper.TryGetCookie(response.HttpContext.Request, "refreshToken", out var existingRefreshToken)
                && !string.IsNullOrEmpty(existingRefreshToken))
            {
                await _tokenService.RevokeByRefreshTokenAsync(existingRefreshToken);
                _logger.LogInformation("Revoked existing browser session before new login");
            }

            var user = await _unitOfWork.UserRepository.GetByUsernameAsync(dto.Username);
            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            {
                _logger.LogWarning("Login failed - invalid credentials");
                throw new InvalidCredentialsException();
            }

            _logger.LogInformation("User {UserId} credentials verified", user.Id);

            var accessToken = _tokenService.GenerateAccessToken(user);
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
            var jti = jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;

            var refreshToken = await _tokenService.GenerateRefreshTokenAsync(user, jti);

            _authHelper.SetAuthCookies(response, accessToken, refreshToken, _config);
            user.LastLogin = DateTime.UtcNow;
            await _unitOfWork.CompleteAsync();

            var accessExpiryMinutes = int.Parse(_config["Jwt:AccessTokenExpiryMinutes"]!);

            _logger.LogInformation("Login successful for user {UserId}", user.Id);

            return new LoginResultDto
            {
                AccessTokenExpires = DateTime.UtcNow.AddMinutes(accessExpiryMinutes),
                Message = "Login successful"
            };
        }
        catch (InvalidCredentialsException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during login");
            throw;
        }
    }
}
