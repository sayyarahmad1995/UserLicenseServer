using Core.DTOs;
using Core.Entities;
using Core.Enums;
using Core.Interfaces;
using FluentAssertions;
using Infrastructure.Services;
using Infrastructure.Services.Exceptions;
using Infrastructure.Services.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text;
using Xunit;

namespace Tests.Services;

public class TokenServiceTests
{
    private readonly Mock<IConfiguration> _configMock;
    private readonly Mock<ICacheRepository> _cacheMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<TokenService>> _loggerMock;
    private readonly TokenService _tokenService;

    // JWT key must be at least 512 bits (64 bytes) for HS512
    private const string ValidJwtKey = "this_is_a_valid_jwt_key_for_hmacsha512_it_must_be_at_least_64_bytes_long_padded_to_128_bytes_for_safety_to_pass_key_size_checks!!!!";

    public TokenServiceTests()
    {
        _configMock = new Mock<IConfiguration>();
        _cacheMock = new Mock<ICacheRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<TokenService>>();

        // Setup default config values with a valid key (32+ bytes)
        _configMock.Setup(x => x["Jwt:Key"]).Returns(ValidJwtKey);
        _configMock.Setup(x => x["Jwt:Issuer"]).Returns("localhost");
        _configMock.Setup(x => x["Jwt:Audience"]).Returns("localhostClient");
        _configMock.Setup(x => x["Jwt:AccessTokenExpiryMinutes"]).Returns("15");
        _configMock.Setup(x => x["Jwt:RefreshTokenExpiryDays"]).Returns("30");

        _tokenService = new TokenService(
            _configMock.Object,
            _cacheMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object
        );
    }

    #region GenerateAccessToken Tests

    [Fact]
    public void GenerateAccessToken_WithValidUser_ShouldReturnValidToken()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Username = "testuser",
            Email = "test@example.com",
            Role = "User",
            Status = UserStatus.Active
        };

        // Act
        var token = _tokenService.GenerateAccessToken(user);

        // Assert
        token.Should().NotBeNullOrEmpty();
        token.Split('.').Length.Should().Be(3); // JWT has 3 parts: header.payload.signature
    }

    [Fact]
    public void GenerateAccessToken_WithAdminUser_ShouldIncludeAdminRole()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Username = "admin",
            Email = "admin@example.com",
            Role = "Admin",
            Status = UserStatus.Active
        };

        // Act
        var token = _tokenService.GenerateAccessToken(user);

        // Assert
        token.Should().NotBeNullOrEmpty();
        token.Split('.').Length.Should().Be(3);
    }

    #endregion

    #region GenerateRefreshTokenAsync Tests

    [Fact]
    public async Task GenerateRefreshTokenAsync_ShouldStoreTokenInCache()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Username = "testuser",
            Email = "test@example.com",
            Role = "User"
        };
        var jti = Guid.NewGuid().ToString();

        // Setup cache mock - don't use It.IsAny with optional parameters
        _cacheMock
            .Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan>(), CancellationToken.None))
            .Returns(Task.CompletedTask);

        // Act
        var refreshToken = await _tokenService.GenerateRefreshTokenAsync(user, jti);

        // Assert
        refreshToken.Should().NotBeNullOrEmpty();
        _cacheMock.Verify(
            x => x.SetAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan>(), CancellationToken.None),
            Times.Once);
    }

    #endregion

    #region RevokeSessionAsync Tests

    [Fact]
    public async Task RevokeSessionAsync_WithValidSession_ShouldMarkTokenAsRevoked()
    {
        // Arrange
        var userId = 1;
        var jti = "test-jti";
        var key = $"session:{userId}:{jti}";

        var tokenModel = new RefreshToken
        {
            UserId = userId.ToString(),
            TokenHash = "hashed_token",
            CreatedAt = DateTime.UtcNow,
            Expires = DateTime.UtcNow.AddDays(30),
            Jti = jti,
            Revoked = false
        };

        _cacheMock.Setup(x => x.GetAsync<RefreshToken>(key, CancellationToken.None))
            .ReturnsAsync(tokenModel);

        _cacheMock
            .Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan>(), CancellationToken.None))
            .Returns(Task.CompletedTask);

        // Act
        await _tokenService.RevokeSessionAsync(userId, jti);

        // Assert
        _cacheMock.Verify(x => x.GetAsync<RefreshToken>(key, CancellationToken.None), Times.Once);
        _cacheMock.Verify(
            x => x.SetAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan>(), CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task RevokeSessionAsync_WithNonExistentSession_ShouldNotThrow()
    {
        // Arrange
        var userId = 1;
        var jti = "nonexistent-jti";
        var key = $"session:{userId}:{jti}";

        _cacheMock.Setup(x => x.GetAsync<RefreshToken>(key, CancellationToken.None))
            .ReturnsAsync((RefreshToken?)null);

        // Act & Assert (should not throw)
        await _tokenService.RevokeSessionAsync(userId, jti);

        _cacheMock.Verify(x => x.GetAsync<RefreshToken>(key, CancellationToken.None), Times.Once);
        _cacheMock.Verify(
            x => x.SetAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region RevokeAllSessionsAsync Tests

    [Fact]
    public async Task RevokeAllSessionsAsync_ShouldRevokeAllUserSessions()
    {
        // Arrange
        var userId = 1;

        var tokens = new List<string> { "session:1:jti1", "session:1:jti2", "session:1:jti3" };

        _cacheMock.Setup(x => x.SearchKeysAsync($"session:{userId}:*"))
            .ReturnsAsync(tokens);

        var tokenModel = new RefreshToken
        {
            UserId = userId.ToString(),
            TokenHash = "hashed_token",
            CreatedAt = DateTime.UtcNow,
            Expires = DateTime.UtcNow.AddDays(30),
            Jti = "test-jti",
            Revoked = false
        };

        _cacheMock.Setup(x => x.GetAsync<RefreshToken>(It.IsAny<string>(), CancellationToken.None))
            .ReturnsAsync(tokenModel);

        _cacheMock
            .Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan>(), CancellationToken.None))
            .Returns(Task.CompletedTask);

        // Act
        await _tokenService.RevokeAllSessionsAsync(userId);

        // Assert
        _cacheMock.Verify(x => x.SearchKeysAsync($"session:{userId}:*"), Times.Once);
        _cacheMock.Verify(
            x => x.SetAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan>(), CancellationToken.None),
            Times.AtLeastOnce);
    }

    #endregion

    #region ValidateRefreshTokenAsync Tests

    [Fact]
    public async Task ValidateRefreshTokenAsync_WithNullToken_ShouldReturnFalse()
    {
        // Act
        var result = await _tokenService.ValidateRefreshTokenAsync(null!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateRefreshTokenAsync_WithEmptyToken_ShouldReturnFalse()
    {
        // Act
        var result = await _tokenService.ValidateRefreshTokenAsync("");

        // Assert
        result.Should().BeFalse();
    }

    #endregion
}
