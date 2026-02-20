using Api.Errors;
using Api.Helpers;
using AutoMapper;
using Core.DTOs;
using Core.Entities;
using Core.Enums;
using Core.Interfaces;
using Infrastructure.Interfaces;
using Infrastructure.Services.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    private readonly IConfiguration _config;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuthHelper _authHelper;
    private readonly IUserCacheService _userCache;
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;
    private readonly IMapper _mapper;
    private readonly ICacheRepository _cacheRepository;

    public AuthController(ITokenService tokenService,
    ILogger<AuthController> logger,
    IConfiguration config,
    IUnitOfWork unitOfWork,
    IAuthHelper authHelper,
    IUserCacheService userCache,
    ICacheRepository cacheRepository,
    IAuthService authService,
    IMapper mapper)
    {
        _mapper = mapper;
        _logger = logger;
        _tokenService = tokenService;
        _config = config;
        _unitOfWork = unitOfWork;
        _authHelper = authHelper;
        _userCache = userCache;
        _authService = authService;
        _cacheRepository = cacheRepository;
    }

    /// <summary>
    /// Authenticates a user and returns JWT access token and refresh token.
    /// </summary>
    /// <param name="dto">Login credentials (username and password)</param>
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
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        try
        {
            _logger.LogInformation("Login endpoint called for user {Username}", dto.Username);
            var result = await _authService.LoginAsync(dto, Response);
            _logger.LogInformation("Login successful for {Username}", dto.Username);
            return ApiResult.Success(200, result.Message, result.AccessTokenExpires != null ? new { result.AccessTokenExpires } : null);
        }
        catch (InvalidCredentialsException ex)
        {
            _logger.LogWarning(ex, "Login failed - invalid credentials");
            return ApiResult.Fail(401, "Invalid username or password");
        }
        catch (TokenException ex)
        {
            _logger.LogError(ex, "Token error during login");
            return ApiResult.Fail(500, "Authentication service error");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during login");
            return ApiResult.Fail(500, "Internal server error");
        }
    }

    /// <summary>
    /// Registers a new user account.
    /// Password must contain: uppercase, lowercase, numbers, and special characters.
    /// </summary>
    /// <param name="dto">Registration details (username, email, password)</param>
    /// <returns>Success message on account creation</returns>
    /// <response code="200">User registered successfully</response>
    /// <response code="400">Validation error (duplicate email/username or weak password)</response>
    /// <response code="429">Too many registration attempts - rate limit exceeded</response>
    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        _logger.LogInformation("Registration attempt for username {Username} and email {Email}", dto.Username, dto.Email);

        var errors = new Dictionary<string, string[]>();
        if (await _unitOfWork.UserRepository.GetByEmailAsync(dto.Email!) is not null)
        {
            _logger.LogWarning("Registration failed - email {Email} already in use", dto.Email);
            errors.Add("Email", new[] { "Email already in use." });
        }

        if (await _unitOfWork.UserRepository.GetByUsernameAsync(dto.Username!) is not null)
        {
            _logger.LogWarning("Registration failed - username {Username} already taken", dto.Username);
            errors.Add("Username", new[] { "Username already taken." });
        }

        if (errors.Count > 0)
            return ApiResult.Validation(errors);

        var user = _mapper.Map<User>(dto);

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password!);
        user.CreatedAt = DateTime.UtcNow;
        user.Status = UserStatus.Unverified;

        _unitOfWork.UserRepository.Add(user);
        await _unitOfWork.CompleteAsync();

        _logger.LogInformation("User registered successfully - username {Username}, ID {UserId}", dto.Username, user.Id);

        return ApiResult.Success(200, "Registered successfully.");
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
    public async Task<IActionResult> GetCurrentUser()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null)
        {
            _logger.LogWarning("GetCurrentUser called without valid user ID in claims");
            return ApiResult.Fail(401, "Unauthorized.");
        }

        _logger.LogDebug("Fetching current user profile for user {UserId}", userId);

        var cached = await _userCache.GetUserAsync(int.Parse(userId!));

        UserDto? userDto;
        if (cached != null)
        {
            _logger.LogDebug("User {UserId} profile retrieved from cache", userId);
            userDto = _mapper.Map<UserDto>(cached);
            return ApiResult.Success(200, "User retrieved successfully.", userDto);
        }

        var user = await _unitOfWork.UserRepository.GetByIdAsync(int.Parse(userId));
        if (user == null)
        {
            _logger.LogWarning("User {UserId} not found in database", userId);
            return ApiResult.Fail(404, "User not found.");
        }

        userDto = _mapper.Map<UserDto>(cached != null ? cached : user);

        await _userCache.CacheUserAsync(int.Parse(userId), userDto);
        _logger.LogDebug("User {UserId} profile cached", userId);

        return ApiResult.Success(200, "User retrieved successfully.", userDto);
    }

    /// <summary>
    /// Refreshes the access token using the refresh token from cookies.
    /// </summary>
    /// <returns>New access token expiry time</returns>
    /// <response code="200">Token refreshed successfully</response>
    /// <response code="401">Refresh token missing, invalid, or expired</response>
    /// <response code="500">Internal server error</response>
    [HttpPost("refresh")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RefreshToken()
    {
        _logger.LogInformation("Token refresh endpoint called");

        if (!_authHelper.TryGetCookie(Request, "refreshToken", out var refreshToken))
        {
            _logger.LogWarning("Token refresh failed - no refresh token in cookie");
            return ApiResult.Fail(401, "Refresh token missing.");
        }

        TokenResponseDto result;
        try
        {
            result = await _tokenService.RefreshTokenAsync(refreshToken!);
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

        await _authHelper.SetAuthCookiesAsync(Response, result.AccessToken, result.RefreshToken, _config);

        var accessExpiryMinutes = int.Parse(_config["Jwt:AccessTokenExpiryMinutes"]!);
        _logger.LogInformation("Token refresh successful");

        return ApiResult.Success(200, "Token refreshed successfully.", new
        {
            AccessTokenExpires = DateTime.UtcNow.AddMinutes(accessExpiryMinutes)
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
    public async Task<IActionResult> Logout()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var jti = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

        if (userId == null || jti == null)
            return ApiResult.Fail(400, "Invalid session.");

        await _tokenService.RevokeSessionAsync(int.Parse(userId), jti);
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
    public async Task<IActionResult> LogoutAll()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null)
            return Unauthorized(new ApiResponse(401, "Invalid session."));

        await _tokenService.RevokeAllSessionsAsync(int.Parse(userId));
        _authHelper.ClearAuthCookies(Response);

        await _cacheRepository.PublishInvalidationAsync($"session:{userId}*");

        return ApiResult.Success(200, "All sessions logged out successfully.");
    }
}
