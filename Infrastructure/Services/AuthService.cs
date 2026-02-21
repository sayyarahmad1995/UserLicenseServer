using Core.DTOs;
using Core.Enums;
using Core.Helpers;
using Core.Interfaces;
using Infrastructure.Interfaces;
using Infrastructure.Services.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;

namespace Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITokenService _tokenService;
    private readonly IAuthHelper _authHelper;
    private readonly ICacheRepository _cache;
    private readonly IEmailService _emailService;
    private readonly JwtSettings _jwt;
    private readonly ILogger<AuthService> _logger;

    private const string VerifyPrefix = "email_verify:";
    private const string ResetPrefix = "password_reset:";
    private static readonly TimeSpan VerifyTokenExpiry = TimeSpan.FromHours(24);
    private static readonly TimeSpan ResetTokenExpiry = TimeSpan.FromHours(1);

    public AuthService(IUnitOfWork unitOfWork,
    ITokenService tokenService,
    IAuthHelper authHelper,
    ICacheRepository cache,
    IEmailService emailService,
    IOptions<JwtSettings> jwtOptions,
    ILogger<AuthService> logger)
    {
        _jwt = jwtOptions.Value;
        _authHelper = authHelper;
        _tokenService = tokenService;
        _cache = cache;
        _emailService = emailService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<LoginResultDto> LoginAsync(LoginDto dto, HttpResponse response, CancellationToken ct = default)
    {
        try
        {
            // If this browser already has a session, revoke it to prevent duplicate sessions
            if (_authHelper.TryGetCookie(response.HttpContext.Request, CookieConstants.RefreshToken, out var existingRefreshToken)
                && !string.IsNullOrEmpty(existingRefreshToken))
            {
                await _tokenService.RevokeByRefreshTokenAsync(existingRefreshToken, ct);
                _logger.LogInformation("Revoked existing browser session before new login");
            }

            var user = await _unitOfWork.UserRepository.GetByUsernameAsync(dto.Username, ct);
            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            {
                _logger.LogWarning("Login failed - invalid credentials");
                throw new InvalidCredentialsException();
            }

            if (user.Status == Core.Enums.UserStatus.Blocked)
            {
                _logger.LogWarning("Login denied - account blocked for user {UserId}", user.Id);
                throw new AccountBlockedException();
            }

            _logger.LogInformation("User {UserId} credentials verified", user.Id);

            var accessToken = _tokenService.GenerateAccessToken(user);
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
            var jti = jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;

            var refreshToken = await _tokenService.GenerateRefreshTokenAsync(user, jti, ct);

            _authHelper.SetAuthCookies(response, accessToken, refreshToken);
            user.LastLogin = DateTime.UtcNow;
            await _unitOfWork.CompleteAsync(ct);

            _logger.LogInformation("Login successful for user {UserId}", user.Id);

            return new LoginResultDto
            {
                AccessTokenExpires = DateTime.UtcNow.AddMinutes(_jwt.AccessTokenExpiryMinutes),
                Message = "Login successful"
            };
        }
        catch (InvalidCredentialsException)
        {
            throw;
        }
        catch (AccountBlockedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during login");
            throw;
        }
    }

    public async Task ChangePasswordAsync(int userId, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        var user = await _unitOfWork.UserRepository.GetByIdAsync(userId, ct)
            ?? throw new InvalidOperationException("User not found.");

        if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
        {
            _logger.LogWarning("Password change failed - incorrect current password for user {UserId}", userId);
            throw new InvalidCredentialsException("Current password is incorrect.");
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.UserRepository.Update(user);
        await _unitOfWork.CompleteAsync(ct);

        // Revoke all sessions so user must re-authenticate with new password
        await _tokenService.RevokeAllSessionsAsync(userId, ct);

        _logger.LogInformation("Password changed successfully for user {UserId}", userId);
    }

    public async Task<string> GenerateVerificationTokenAsync(int userId, CancellationToken ct = default)
    {
        var user = await _unitOfWork.UserRepository.GetByIdAsync(userId, ct)
            ?? throw new InvalidOperationException("User not found.");

        var token = GenerateSecureToken();
        await _cache.SetAsync($"{VerifyPrefix}{token}", userId, VerifyTokenExpiry, ct);

        // Fire-and-forget: don't block the HTTP request on SMTP latency
        _ = Task.Run(async () =>
        {
            try
            {
                await _emailService.SendVerificationEmailAsync(user.Email, token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background: failed to send verification email to {Email}", user.Email);
            }
        });

        _logger.LogInformation("Verification token generated for user {UserId}", userId);
        return token;
    }

    public async Task VerifyEmailAsync(string token, CancellationToken ct = default)
    {
        var userId = await _cache.GetAsync<int?>($"{VerifyPrefix}{token}", ct)
            ?? throw new InvalidOperationException("Invalid or expired verification token.");

        var user = await _unitOfWork.UserRepository.GetByIdAsync(userId, ct)
            ?? throw new InvalidOperationException("User not found.");

        if (user.Status != UserStatus.Unverified)
            throw new InvalidOperationException("Account is already verified.");

        user.Verify();
        _unitOfWork.UserRepository.Update(user);
        await _unitOfWork.CompleteAsync(ct);

        await _cache.RemoveAsync($"{VerifyPrefix}{token}", ct);

        _logger.LogInformation("Email verified for user {UserId}", userId);
    }

    public async Task ResendVerificationAsync(string email, CancellationToken ct = default)
    {
        var user = await _unitOfWork.UserRepository.GetByEmailAsync(email, ct)
            ?? throw new InvalidOperationException("User not found.");

        if (user.Status != UserStatus.Unverified)
            throw new InvalidOperationException("Account is already verified.");

        await GenerateVerificationTokenAsync(user.Id, ct);

        _logger.LogInformation("Verification email resent for user {UserId}", user.Id);
    }

    public async Task<string> GeneratePasswordResetTokenAsync(string email, CancellationToken ct = default)
    {
        var user = await _unitOfWork.UserRepository.GetByEmailAsync(email, ct)
            ?? throw new InvalidOperationException("User not found.");

        var token = GenerateSecureToken();
        await _cache.SetAsync($"{ResetPrefix}{token}", user.Id, ResetTokenExpiry, ct);

        // Fire-and-forget: don't block the HTTP request on SMTP latency
        _ = Task.Run(async () =>
        {
            try
            {
                await _emailService.SendPasswordResetEmailAsync(user.Email, token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background: failed to send password reset email to {Email}", user.Email);
            }
        });

        _logger.LogInformation("Password reset token generated for user {UserId}", user.Id);
        return token;
    }

    public async Task ResetPasswordAsync(string token, string newPassword, CancellationToken ct = default)
    {
        var userId = await _cache.GetAsync<int?>($"{ResetPrefix}{token}", ct)
            ?? throw new InvalidOperationException("Invalid or expired reset token.");

        var user = await _unitOfWork.UserRepository.GetByIdAsync(userId, ct)
            ?? throw new InvalidOperationException("User not found.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.UserRepository.Update(user);
        await _unitOfWork.CompleteAsync(ct);

        await _cache.RemoveAsync($"{ResetPrefix}{token}", ct);
        await _tokenService.RevokeAllSessionsAsync(userId, ct);

        _logger.LogInformation("Password reset successfully for user {UserId}", userId);
    }

    private static string GenerateSecureToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }
}
