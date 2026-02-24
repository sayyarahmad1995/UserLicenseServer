using Api.Errors;
using Api.Helpers;
using AutoMapper;
using Core.DTOs;
using Core.Entities;
using Core.Helpers;
using Core.Interfaces;
using Infrastructure.Interfaces;
using Infrastructure.Services.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Api.Controllers;

/// <summary>
/// Authentication controller for user login, registration, token refresh, and logout operations.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : BaseApiController
{
    private readonly ITokenService _tokenService;
    private readonly JwtSettings _jwt;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuthHelper _authHelper;
    private readonly IUserCacheService _userCache;
    private readonly IAuthService _authService;
    private readonly IAuditService _auditService;
    private readonly ILogger<AuthController> _logger;
    private readonly IMapper _mapper;
    private readonly ICacheRepository _cacheRepository;

    public AuthController(ITokenService tokenService,
    ILogger<AuthController> logger,
    IOptions<JwtSettings> jwtOptions,
    IUnitOfWork unitOfWork,
    IAuthHelper authHelper,
    IUserCacheService userCache,
    ICacheRepository cacheRepository,
    IAuthService authService,
    IAuditService auditService,
    IMapper mapper)
    {
        _mapper = mapper;
        _logger = logger;
        _tokenService = tokenService;
        _jwt = jwtOptions.Value;
        _unitOfWork = unitOfWork;
        _authHelper = authHelper;
        _userCache = userCache;
        _authService = authService;
        _auditService = auditService;
        _cacheRepository = cacheRepository;
    }

    /// <summary>
    /// Safely extracts and parses the user ID from JWT claims.
    /// </summary>
    private bool TryGetUserId(out int userId)
    {
        userId = 0;
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return claim != null && int.TryParse(claim, out userId);
    }

    private string GetIpAddress() =>
        HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    /// <summary>
    /// Authenticates a user and returns JWT access token and refresh token.
    /// </summary>
    /// <param name="dto">Login credentials (username and password)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Access token expiry time on successful login</returns>
    /// <response code="200">Login successful - tokens set in HTTP-only cookies</response>
    /// <response code="401">Invalid credentials</response>
    /// <response code="429">Too many login attempts - rate limit exceeded</response>
    /// <response code="500">Internal server error</response>
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Login([FromBody] LoginDto dto, CancellationToken ct)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var authKey = $"throttle:auth:{ipAddress}:/api/v1/auth/login";

        try
        {
            _logger.LogInformation("Login attempt initiated");
            var result = await _authService.LoginAsync(dto, Response, ct);
            _logger.LogInformation("Login successful");

            await _auditService.LogAsync("Login", "User", details: $"User '{dto.Username}' logged in",
                ipAddress: GetIpAddress(), ct: ct);

            return ApiResult.Success(200, result.Message, result.AccessTokenExpires != null ? new { result.AccessTokenExpires } : null);
        }
        catch (InvalidCredentialsException)
        {
            _logger.LogWarning("Login failed - invalid credentials");
            _authHelper.ClearAuthCookies(Response);

            var attemptInfo = await GetRemainingAttempts(authKey, ct);
            return ApiResult.Fail(400, "Invalid username or password", attemptInfo);
        }
        catch (AccountBlockedException ex)
        {
            _logger.LogWarning("Login denied - account is blocked");
            _authHelper.ClearAuthCookies(Response);
            return ApiResult.Fail(403, ex.Message);
        }
        catch (TokenException ex)
        {
            _logger.LogError(ex, "Token error during login");
            return ApiResult.Fail(500, "Authentication service error");
        }
    }

    /// <summary>
    /// Returns remaining attempts and penalty info for the auth throttle key.
    /// </summary>
    private async Task<object> GetRemainingAttempts(string authKey, CancellationToken ct)
    {
        var settings = HttpContext.RequestServices
            .GetRequiredService<IOptions<ThrottlingSettings>>().Value.Auth;

        var penaltyKey = $"{authKey}:penalty";
        var penaltyUsedKey = $"{authKey}:penalty_used";

        var penaltyStartRaw = await _cacheRepository.GetAsync<long?>(penaltyKey, ct);

        if (penaltyStartRaw.HasValue)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var elapsedSeconds = (int)(now - penaltyStartRaw.Value);
            var elapsedMinutes = elapsedSeconds / 60;
            var usedAttempts = await _cacheRepository.GetAsync<int?>(penaltyUsedKey, ct) ?? 0;
            var remaining = Math.Max(0, elapsedMinutes - usedAttempts);
            var penaltyRemaining = Math.Max(0, settings.PenaltySeconds - elapsedSeconds);
            var nextAttemptIn = remaining > 0 ? 0 : 60 - (elapsedSeconds % 60);

            return new
            {
                RemainingAttempts = remaining,
                NextAttemptInSeconds = nextAttemptIn,
                PenaltyRemainingSeconds = penaltyRemaining,
                InPenalty = true
            };
        }

        var currentCount = await _cacheRepository.GetAsync<int?>(authKey, ct) ?? 0;
        var normalRemaining = Math.Max(0, settings.MaxRequestsPerMinute - currentCount);

        return new
        {
            RemainingAttempts = normalRemaining,
            NextAttemptInSeconds = (int?)null,
            PenaltyRemainingSeconds = (int?)null,
            InPenalty = false
        };
    }

    /// <summary>
    /// Registers a new user account.
    /// Password must contain: uppercase, lowercase, numbers, and special characters.
    /// </summary>
    /// <param name="dto">Registration details (username, email, password)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Success message on account creation</returns>
    /// <response code="200">User registered successfully</response>
    /// <response code="400">Validation error (duplicate email/username or weak password)</response>
    /// <response code="429">Too many registration attempts - rate limit exceeded</response>
    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto, CancellationToken ct)
    {
        _logger.LogInformation("Registration attempt initiated");

        var errors = new Dictionary<string, string[]>();
        if (await _unitOfWork.UserRepository.GetByEmailAsync(dto.Email!, ct) is not null)
        {
            _logger.LogWarning("Registration failed - email already in use");
            errors.Add("Email", new[] { "Email already in use." });
        }

        if (await _unitOfWork.UserRepository.GetByUsernameAsync(dto.Username!, ct) is not null)
        {
            _logger.LogWarning("Registration failed - username already taken");
            errors.Add("Username", new[] { "Username already taken." });
        }

        if (errors.Count > 0)
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var authKey = $"throttle:auth:{ipAddress}:/api/v1/auth/register";
            var attemptInfo = await GetRemainingAttempts(authKey, ct);
            return ApiResult.Validation(errors, attemptInfo);
        }

        var user = _mapper.Map<User>(dto);
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password!);

        _unitOfWork.UserRepository.Add(user);
        await _unitOfWork.CompleteAsync(ct);

        // Send verification email
        await _authService.GenerateVerificationTokenAsync(user.Id, ct);

        _logger.LogInformation("User registered successfully with ID {UserId}", user.Id);

        await _auditService.LogAsync("Register", "User", user.Id,
            details: $"User '{user.Username}' registered", ipAddress: GetIpAddress(), ct: ct);

        return ApiResult.Created("Registered successfully.");
    }

    /// <summary>
    /// Retrieves the current authenticated user's profile.
    /// </summary>
    /// <returns>User profile data with cached data when available</returns>
    /// <response code="200">User profile retrieved successfully</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="404">User not found</response>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrentUser(CancellationToken ct)
    {
        if (!TryGetUserId(out var parsedUserId))
        {
            _logger.LogWarning("GetCurrentUser called without valid user ID in claims");
            return ApiResult.Fail(401, "Unauthorized.");
        }

        _logger.LogDebug("Fetching current user profile for user {UserId}", parsedUserId);

        var cached = await _userCache.GetUserAsync(parsedUserId, ct);
        if (cached != null)
        {
            _logger.LogDebug("User {UserId} profile retrieved from cache", parsedUserId);
            return ApiResult.Success(200, "User retrieved successfully.", cached);
        }

        var user = await _unitOfWork.UserRepository.GetByIdAsync(parsedUserId, ct);
        if (user == null)
        {
            _logger.LogWarning("User {UserId} not found in database", parsedUserId);
            return ApiResult.Fail(404, "User not found.");
        }

        var userDto = _mapper.Map<UserDto>(user);
        await _userCache.CacheUserAsync(parsedUserId, userDto, ct);
        _logger.LogDebug("User {UserId} profile cached", parsedUserId);

        return ApiResult.Success(200, "User retrieved successfully.", userDto);
    }

    /// <summary>
    /// Refreshes the access token using the refresh token from cookies.
    /// </summary>
    /// <returns>New access token expiry time</returns>
    /// <response code="200">Token refreshed successfully</response>
    /// <response code="401">Refresh token missing, invalid, or expired</response>
    /// <response code="500">Internal server error</response>
    [AllowAnonymous]
    [HttpPost("refresh")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RefreshToken(CancellationToken ct)
    {
        _logger.LogInformation("Token refresh endpoint called");

        if (!_authHelper.TryGetCookie(Request, CookieConstants.RefreshToken, out var refreshToken))
        {
            _logger.LogWarning("Token refresh failed - no refresh token in cookie");
            return ApiResult.Fail(401, "Refresh token missing.");
        }

        TokenResponseDto result;
        try
        {
            result = await _tokenService.RefreshTokenAsync(refreshToken!, ct);
        }
        catch (TokenException ex)
        {
            _logger.LogWarning(ex, "Token refresh failed - {Message}", ex.Message);
            return ApiResult.Fail(401, "Invalid or expired refresh token.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during token refresh");
            return ApiResult.Fail(500, "Internal server error.");
        }

        _authHelper.SetAuthCookies(Response, result.AccessToken, result.RefreshToken);

        _logger.LogInformation("Token refresh successful");

        return ApiResult.Success(200, "Token refreshed successfully.", new
        {
            AccessTokenExpires = DateTime.UtcNow.AddMinutes(_jwt.AccessTokenExpiryMinutes)
        });
    }

    /// <summary>
    /// Logs out the current user by revoking the current session.
    /// </summary>
    /// <returns>Success message</returns>
    /// <response code="200">Logged out successfully</response>
    /// <response code="400">Invalid session</response>
    /// <response code="401">User not authenticated</response>
    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var jti = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

        if (!TryGetUserId(out var parsedUserId) || jti == null)
            return ApiResult.Fail(400, "Invalid session.");

        await _tokenService.RevokeSessionAsync(parsedUserId, jti, ct);
        _authHelper.ClearAuthCookies(Response);

        return ApiResult.Success(200, "Logged out successfully.");
    }

    /// <summary>
    /// Logs out the user from all devices by revoking all active sessions.
    /// </summary>
    /// <returns>Success message</returns>
    /// <response code="200">All sessions logged out successfully</response>
    /// <response code="401">User not authenticated</response>
    [Authorize]
    [HttpPost("logout-all")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LogoutAll(CancellationToken ct)
    {
        if (!TryGetUserId(out var parsedUserId))
            return ApiResult.Fail(401, "Invalid session.");

        await _tokenService.RevokeAllSessionsAsync(parsedUserId, ct);
        _authHelper.ClearAuthCookies(Response);

        await _cacheRepository.PublishInvalidationAsync(CacheKeys.SessionPattern(parsedUserId));

        return ApiResult.Success(200, "All sessions logged out successfully.");
    }

    /// <summary>
    /// Changes the authenticated user's password. Revokes all sessions on success.
    /// </summary>
    /// <param name="dto">Current and new password</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Success message</returns>
    /// <response code="200">Password changed successfully</response>
    /// <response code="401">Current password is incorrect or user not authenticated</response>
    [Authorize]
    [HttpPost("change-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto, CancellationToken ct)
    {
        if (!TryGetUserId(out var parsedUserId))
            return ApiResult.Fail(401, "Unauthorized.");

        try
        {
            await _authService.ChangePasswordAsync(parsedUserId, dto.CurrentPassword, dto.NewPassword, ct);
            _authHelper.ClearAuthCookies(Response);

            await _auditService.LogAsync("ChangePassword", "User", parsedUserId, parsedUserId,
                ipAddress: GetIpAddress(), ct: ct);

            return ApiResult.Success(200, "Password changed successfully. Please log in again.");
        }
        catch (InvalidCredentialsException)
        {
            return ApiResult.Fail(401, "Current password is incorrect.");
        }
    }

    /// <summary>
    /// Updates the authenticated user's profile (username and/or email).
    /// </summary>
    /// <param name="dto">Profile update payload</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Updated user profile</returns>
    /// <response code="200">Profile updated successfully</response>
    /// <response code="400">Validation error (duplicate username/email)</response>
    /// <response code="401">User not authenticated</response>
    [Authorize]
    [HttpPut("profile")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateUserProfileDto dto, CancellationToken ct)
    {
        if (!TryGetUserId(out var parsedUserId))
            return ApiResult.Fail(401, "Unauthorized.");

        if (!ModelState.IsValid)
            return ApiResult.Validation(ModelState);

        var user = await _unitOfWork.UserRepository.GetByIdAsync(parsedUserId, ct);
        if (user == null)
            return ApiResult.Fail(404, "User not found.");

        if (!string.IsNullOrWhiteSpace(dto.Username) && dto.Username != user.Username)
        {
            var existing = await _unitOfWork.UserRepository.GetByUsernameAsync(dto.Username, ct);
            if (existing != null)
                return ApiResult.Fail(400, "Username already taken.");
            user.Username = dto.Username.Trim();
        }

        if (!string.IsNullOrWhiteSpace(dto.Email) && dto.Email != user.Email)
        {
            var existing = await _unitOfWork.UserRepository.GetByEmailAsync(dto.Email, ct);
            if (existing != null)
                return ApiResult.Fail(400, "Email already in use.");
            user.Email = dto.Email.Trim();
        }

        user.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.UserRepository.Update(user);
        await _unitOfWork.CompleteAsync(ct);

        await _userCache.InvalidateUserAsync(parsedUserId);

        var data = _mapper.Map<UserDto>(user);
        return ApiResult.Success(200, "Profile updated successfully.", data);
    }

    /// <summary>
    /// Gets the authenticated user's notification preferences.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Current notification preferences</returns>
    /// <response code="200">Preferences retrieved</response>
    /// <response code="401">User not authenticated</response>
    [Authorize]
    [HttpGet("notifications")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetNotificationPreferences(CancellationToken ct)
    {
        if (!TryGetUserId(out var parsedUserId))
            return ApiResult.Fail(401, "Unauthorized.");

        var user = await _unitOfWork.UserRepository.GetByIdAsync(parsedUserId, ct);
        if (user == null)
            return ApiResult.Fail(404, "User not found.");

        var prefs = new NotificationPreferencesDto
        {
            NotifyLicenseExpiry = user.NotifyLicenseExpiry,
            NotifyAccountActivity = user.NotifyAccountActivity,
            NotifySystemAnnouncements = user.NotifySystemAnnouncements
        };

        return ApiResult.Success(200, "Notification preferences retrieved.", prefs);
    }

    /// <summary>
    /// Updates the authenticated user's notification preferences.
    /// </summary>
    /// <param name="dto">Notification preference settings</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Updated notification preferences</returns>
    /// <response code="200">Preferences updated</response>
    /// <response code="401">User not authenticated</response>
    [Authorize]
    [HttpPut("notifications")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateNotificationPreferences(
        [FromBody] NotificationPreferencesDto dto, CancellationToken ct)
    {
        if (!TryGetUserId(out var parsedUserId))
            return ApiResult.Fail(401, "Unauthorized.");

        var user = await _unitOfWork.UserRepository.GetByIdAsync(parsedUserId, ct);
        if (user == null)
            return ApiResult.Fail(404, "User not found.");

        user.NotifyLicenseExpiry = dto.NotifyLicenseExpiry;
        user.NotifyAccountActivity = dto.NotifyAccountActivity;
        user.NotifySystemAnnouncements = dto.NotifySystemAnnouncements;
        user.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.UserRepository.Update(user);
        await _unitOfWork.CompleteAsync(ct);

        await _userCache.InvalidateUserAsync(parsedUserId);

        return ApiResult.Success(200, "Notification preferences updated.", dto);
    }

    /// <summary>
    /// Verifies a user's email using the token sent to their email address.
    /// </summary>
    /// <param name="dto">Verification token</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Success message</returns>
    /// <response code="200">Email verified successfully</response>
    /// <response code="400">Invalid or expired token, or already verified</response>
    [AllowAnonymous]
    [HttpPost("verify-email")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailDto dto, CancellationToken ct)
    {
        try
        {
            await _authService.VerifyEmailAsync(dto.Token, ct);
            return ApiResult.Success(200, "Email verified successfully.");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(400, ex.Message);
        }
    }

    /// <summary>
    /// Resends a verification email to the specified address.
    /// </summary>
    /// <param name="dto">Email address to resend verification to</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Success message (always returns 200 to prevent email enumeration)</returns>
    /// <response code="200">If the email exists and is unverified, a verification email will be sent</response>
    [AllowAnonymous]
    [HttpPost("resend-verification")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationDto dto, CancellationToken ct)
    {
        try
        {
            await _authService.ResendVerificationAsync(dto.Email, ct);
        }
        catch (InvalidOperationException)
        {
            // Silently ignore to prevent email enumeration
        }

        return ApiResult.Success(200, "If your email is registered and unverified, a verification email has been sent.");
    }

    /// <summary>
    /// Requests a password reset email for the specified email address.
    /// </summary>
    /// <param name="dto">Email address</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Success message (always returns 200 to prevent email enumeration)</returns>
    /// <response code="200">If the email exists, a password reset email will be sent</response>
    [AllowAnonymous]
    [HttpPost("forgot-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ForgotPassword([FromBody] ResendVerificationDto dto, CancellationToken ct)
    {
        try
        {
            await _authService.GeneratePasswordResetTokenAsync(dto.Email, ct);
        }
        catch (InvalidOperationException)
        {
            // Silently ignore to prevent email enumeration
        }

        return ApiResult.Success(200, "If your email is registered, a password reset email has been sent.");
    }

    /// <summary>
    /// Resets the user's password using a password reset token.
    /// </summary>
    /// <param name="dto">Reset token and new password</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Success message</returns>
    /// <response code="200">Password reset successfully</response>
    /// <response code="400">Invalid or expired token</response>
    [AllowAnonymous]
    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto, CancellationToken ct)
    {
        try
        {
            await _authService.ResetPasswordAsync(dto.Token, dto.NewPassword, ct);
            return ApiResult.Success(200, "Password reset successfully. Please log in with your new password.");
        }
        catch (InvalidOperationException ex)
        {
            return ApiResult.Fail(400, ex.Message);
        }
    }
}
