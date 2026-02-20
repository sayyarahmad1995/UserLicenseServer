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
        _logger.LogInformation("Login attempt for username: {Username}", dto.Username);

        try
        {
            _authHelper.TryGetCookie(response.HttpContext.Request, "refreshToken", out var existingRefreshToken);

            var isValid = await _tokenService.ValidateRefreshTokenAsync(existingRefreshToken!);
            if (isValid)
            {
                _logger.LogInformation("User {Username} already has valid session", dto.Username);
                return new LoginResultDto { Message = "You are already signed in" };
            }

            var user = await _unitOfWork.UserRepository.GetByUsernameAsync(dto.Username);
            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            {
                _logger.LogWarning("Login failed for username {Username} - invalid credentials", dto.Username);
                throw new InvalidCredentialsException();
            }

            _logger.LogInformation("User {Username} credentials verified", dto.Username);

            var accessToken = _tokenService.GenerateAccessToken(user);
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
            var jti = jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;

            var refreshToken = await _tokenService.GenerateRefreshTokenAsync(user, jti);
            await _authHelper.SetAuthCookiesAsync(response, accessToken, refreshToken, _config);

            var accessExpiryMinutes = int.Parse(_config["Jwt:AccessTokenExpiryMinutes"]!);

            user.LastLogin = DateTime.UtcNow;
            _unitOfWork.UserRepository.Update(user);
            await _unitOfWork.CompleteAsync();

            _logger.LogInformation("Login successful for user {Username} with ID {UserId}", dto.Username, user.Id);

            return new LoginResultDto
            {
                AccessTokenExpires = DateTime.UtcNow.AddMinutes(accessExpiryMinutes),
                Message = "Login successful"
            };
        }
        catch (InvalidCredentialsException ex)
        {
            _logger.LogWarning(ex, "Invalid credentials exception during login");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during login for username {Username}", dto.Username);
            throw;
        }
    }
}
