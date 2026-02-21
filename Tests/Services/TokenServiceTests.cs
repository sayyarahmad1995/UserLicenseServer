using Core.DTOs;
using Core.Entities;
using Core.Enums;
using Core.Helpers;
using Core.Interfaces;
using FluentAssertions;
using Infrastructure.Services;
using Infrastructure.Services.Exceptions;
using Infrastructure.Services.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Text;
using Xunit;

namespace Tests.Services;

public class TokenServiceTests
{
    private readonly Mock<ICacheRepository> _cacheMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<TokenService>> _loggerMock;
    private readonly TokenService _tokenService;

    // JWT key must be at least 512 bits (64 bytes) for HS512
    private const string ValidJwtKey = "this_is_a_valid_jwt_key_for_hmacsha512_it_must_be_at_least_64_bytes_long_padded_to_128_bytes_for_safety_to_pass_key_size_checks!!!!";

    public TokenServiceTests()
    {
        _cacheMock = new Mock<ICacheRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<TokenService>>();

        var jwtSettings = Options.Create(new JwtSettings
        {
            Key = ValidJwtKey,
            Issuer = "localhost",
            Audience = "localhostClient",
            AccessTokenExpiryMinutes = 15,
            RefreshTokenExpiryDays = 30
        });

        _tokenService = new TokenService(
            jwtSettings,
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
        // Session storage + reverse index = 2 SetAsync calls
        _cacheMock.Verify(
            x => x.SetAsync(It.Is<string>(k => k.StartsWith("session:")), It.IsAny<object>(), It.IsAny<TimeSpan>(), CancellationToken.None),
            Times.Once);
        _cacheMock.Verify(
            x => x.SetAsync(It.Is<string>(k => k.StartsWith("tokenindex:")), It.IsAny<object>(), It.IsAny<TimeSpan>(), CancellationToken.None),
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

        _cacheMock
            .Setup(x => x.RemoveAsync(It.IsAny<string>(), CancellationToken.None))
            .Returns(Task.CompletedTask);

        // Act
        await _tokenService.RevokeSessionAsync(userId, jti);

        // Assert
        _cacheMock.Verify(x => x.GetAsync<RefreshToken>(key, CancellationToken.None), Times.Once);
        _cacheMock.Verify(
            x => x.SetAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan>(), CancellationToken.None),
            Times.Once);
        // Verify reverse index cleanup
        _cacheMock.Verify(
            x => x.RemoveAsync(It.Is<string>(k => k.StartsWith("tokenindex:")), CancellationToken.None),
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

    [Fact]
    public async Task ValidateRefreshTokenAsync_WithNoReverseIndex_ShouldReturnFalse()
    {
        // Arrange
        _cacheMock.Setup(x => x.GetAsync<string>(It.IsAny<string>(), CancellationToken.None))
            .ReturnsAsync((string?)null);

        // Act
        var result = await _tokenService.ValidateRefreshTokenAsync("some-token");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateRefreshTokenAsync_WithRevokedToken_ShouldReturnFalse()
    {
        // Arrange
        var token = "valid-refresh-token";
        var hashedToken = Infrastructure.Services.Security.TokenHasher.HashToken(token);
        var indexKey = $"tokenindex:{hashedToken}";
        var sessionKey = "session:1:jti1";

        _cacheMock.Setup(x => x.GetAsync<string>(indexKey, CancellationToken.None))
            .ReturnsAsync(sessionKey);

        _cacheMock.Setup(x => x.GetAsync<RefreshToken>(sessionKey, CancellationToken.None))
            .ReturnsAsync(new RefreshToken
            {
                UserId = "1",
                TokenHash = hashedToken,
                Revoked = true,
                Expires = DateTime.UtcNow.AddDays(30),
                Jti = "jti1"
            });

        // Act
        var result = await _tokenService.ValidateRefreshTokenAsync(token);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateRefreshTokenAsync_WithExpiredToken_ShouldReturnFalse()
    {
        // Arrange
        var token = "valid-refresh-token";
        var hashedToken = Infrastructure.Services.Security.TokenHasher.HashToken(token);
        var indexKey = $"tokenindex:{hashedToken}";
        var sessionKey = "session:1:jti1";

        _cacheMock.Setup(x => x.GetAsync<string>(indexKey, CancellationToken.None))
            .ReturnsAsync(sessionKey);

        _cacheMock.Setup(x => x.GetAsync<RefreshToken>(sessionKey, CancellationToken.None))
            .ReturnsAsync(new RefreshToken
            {
                UserId = "1",
                TokenHash = hashedToken,
                Revoked = false,
                Expires = DateTime.UtcNow.AddDays(-1),
                Jti = "jti1"
            });

        // Act
        var result = await _tokenService.ValidateRefreshTokenAsync(token);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateRefreshTokenAsync_WithValidToken_ShouldReturnTrue()
    {
        // Arrange
        var token = "valid-refresh-token";
        var hashedToken = Infrastructure.Services.Security.TokenHasher.HashToken(token);
        var indexKey = $"tokenindex:{hashedToken}";
        var sessionKey = "session:1:jti1";

        _cacheMock.Setup(x => x.GetAsync<string>(indexKey, CancellationToken.None))
            .ReturnsAsync(sessionKey);

        _cacheMock.Setup(x => x.GetAsync<RefreshToken>(sessionKey, CancellationToken.None))
            .ReturnsAsync(new RefreshToken
            {
                UserId = "1",
                TokenHash = hashedToken,
                Revoked = false,
                Expires = DateTime.UtcNow.AddDays(30),
                Jti = "jti1"
            });

        // Act
        var result = await _tokenService.ValidateRefreshTokenAsync(token);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateRefreshTokenAsync_WithMismatchedHash_ShouldReturnFalse()
    {
        // Arrange
        var token = "valid-refresh-token";
        var hashedToken = Infrastructure.Services.Security.TokenHasher.HashToken(token);
        var indexKey = $"tokenindex:{hashedToken}";
        var sessionKey = "session:1:jti1";

        _cacheMock.Setup(x => x.GetAsync<string>(indexKey, CancellationToken.None))
            .ReturnsAsync(sessionKey);

        _cacheMock.Setup(x => x.GetAsync<RefreshToken>(sessionKey, CancellationToken.None))
            .ReturnsAsync(new RefreshToken
            {
                UserId = "1",
                TokenHash = "different-hash",
                Revoked = false,
                Expires = DateTime.UtcNow.AddDays(30),
                Jti = "jti1"
            });

        // Act
        var result = await _tokenService.ValidateRefreshTokenAsync(token);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region RefreshTokenAsync Tests

    [Fact]
    public async Task RefreshTokenAsync_WithNonExistentToken_ShouldThrowTokenException()
    {
        // Arrange
        _cacheMock.Setup(x => x.GetAsync<string>(It.IsAny<string>(), CancellationToken.None))
            .ReturnsAsync((string?)null);

        // Act & Assert
        var act = () => _tokenService.RefreshTokenAsync("nonexistent-token");
        await act.Should().ThrowAsync<TokenException>()
            .WithMessage("Refresh token not found.");
    }

    [Fact]
    public async Task RefreshTokenAsync_WithRevokedToken_ShouldThrowTokenException()
    {
        // Arrange
        var token = "revoked-token";
        var hashedToken = Infrastructure.Services.Security.TokenHasher.HashToken(token);
        var indexKey = $"tokenindex:{hashedToken}";
        var sessionKey = "session:1:jti1";

        _cacheMock.Setup(x => x.GetAsync<string>(indexKey, CancellationToken.None))
            .ReturnsAsync(sessionKey);

        _cacheMock.Setup(x => x.GetAsync<RefreshToken>(sessionKey, CancellationToken.None))
            .ReturnsAsync(new RefreshToken
            {
                UserId = "1",
                TokenHash = hashedToken,
                Revoked = true,
                Expires = DateTime.UtcNow.AddDays(30),
                Jti = "jti1"
            });

        // Act & Assert
        var act = () => _tokenService.RefreshTokenAsync(token);
        await act.Should().ThrowAsync<TokenException>()
            .WithMessage("*revoked*");
    }

    [Fact]
    public async Task RefreshTokenAsync_WithExpiredToken_ShouldThrowTokenException()
    {
        // Arrange
        var token = "expired-token";
        var hashedToken = Infrastructure.Services.Security.TokenHasher.HashToken(token);
        var indexKey = $"tokenindex:{hashedToken}";
        var sessionKey = "session:1:jti1";

        _cacheMock.Setup(x => x.GetAsync<string>(indexKey, CancellationToken.None))
            .ReturnsAsync(sessionKey);

        _cacheMock.Setup(x => x.GetAsync<RefreshToken>(sessionKey, CancellationToken.None))
            .ReturnsAsync(new RefreshToken
            {
                UserId = "1",
                TokenHash = hashedToken,
                Revoked = false,
                Expires = DateTime.UtcNow.AddDays(-1),
                Jti = "jti1"
            });

        // Act & Assert
        var act = () => _tokenService.RefreshTokenAsync(token);
        await act.Should().ThrowAsync<TokenException>()
            .WithMessage("*expired*");
    }

    [Fact]
    public async Task RefreshTokenAsync_WithUserNotFound_ShouldThrowTokenException()
    {
        // Arrange
        var token = "valid-token";
        var hashedToken = Infrastructure.Services.Security.TokenHasher.HashToken(token);
        var indexKey = $"tokenindex:{hashedToken}";
        var sessionKey = "session:1:jti1";

        _cacheMock.Setup(x => x.GetAsync<string>(indexKey, CancellationToken.None))
            .ReturnsAsync(sessionKey);

        _cacheMock.Setup(x => x.GetAsync<RefreshToken>(sessionKey, CancellationToken.None))
            .ReturnsAsync(new RefreshToken
            {
                UserId = "1",
                TokenHash = hashedToken,
                Revoked = false,
                Expires = DateTime.UtcNow.AddDays(30),
                Jti = "jti1"
            });

        var userRepoMock = new Mock<IUserRepository>();
        userRepoMock.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        _unitOfWorkMock.Setup(x => x.UserRepository).Returns(userRepoMock.Object);

        // Act & Assert
        var act = () => _tokenService.RefreshTokenAsync(token);
        await act.Should().ThrowAsync<TokenException>()
            .WithMessage("User not found.");
    }

    [Fact]
    public async Task RefreshTokenAsync_WithValidToken_ShouldReturnNewTokens()
    {
        // Arrange
        var token = "valid-token";
        var hashedToken = Infrastructure.Services.Security.TokenHasher.HashToken(token);
        var indexKey = $"tokenindex:{hashedToken}";
        var sessionKey = "session:1:jti1";

        _cacheMock.Setup(x => x.GetAsync<string>(indexKey, CancellationToken.None))
            .ReturnsAsync(sessionKey);

        _cacheMock.Setup(x => x.GetAsync<RefreshToken>(sessionKey, CancellationToken.None))
            .ReturnsAsync(new RefreshToken
            {
                UserId = "1",
                TokenHash = hashedToken,
                Revoked = false,
                Expires = DateTime.UtcNow.AddDays(30),
                Jti = "jti1"
            });

        var user = new User
        {
            Id = 1,
            Username = "testuser",
            Email = "test@example.com",
            Role = "User",
            Status = UserStatus.Active
        };

        var userRepoMock = new Mock<IUserRepository>();
        userRepoMock.Setup(x => x.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _unitOfWorkMock.Setup(x => x.UserRepository).Returns(userRepoMock.Object);

        _cacheMock
            .Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan>(), CancellationToken.None))
            .Returns(Task.CompletedTask);
        _cacheMock
            .Setup(x => x.RemoveAsync(It.IsAny<string>(), CancellationToken.None))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _tokenService.RefreshTokenAsync(token);

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();

        // Verify old token was revoked
        _cacheMock.Verify(
            x => x.SetAsync(sessionKey, It.Is<RefreshToken>(t => t.Revoked == true), It.IsAny<TimeSpan>(), CancellationToken.None),
            Times.Once);
        // Verify old reverse index was cleaned up
        _cacheMock.Verify(
            x => x.RemoveAsync(indexKey, CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task RefreshTokenAsync_WithMissingSessionData_ShouldThrowAndCleanupIndex()
    {
        // Arrange - index exists but session data is missing
        var token = "orphaned-token";
        var hashedToken = Infrastructure.Services.Security.TokenHasher.HashToken(token);
        var indexKey = $"tokenindex:{hashedToken}";
        var sessionKey = "session:1:jti1";

        _cacheMock.Setup(x => x.GetAsync<string>(indexKey, CancellationToken.None))
            .ReturnsAsync(sessionKey);

        _cacheMock.Setup(x => x.GetAsync<RefreshToken>(sessionKey, CancellationToken.None))
            .ReturnsAsync((RefreshToken?)null);

        _cacheMock.Setup(x => x.RemoveAsync(indexKey, CancellationToken.None))
            .Returns(Task.CompletedTask);

        // Act & Assert
        var act = () => _tokenService.RefreshTokenAsync(token);
        await act.Should().ThrowAsync<TokenException>()
            .WithMessage("Refresh token not found.");

        // Verify orphaned index was cleaned up
        _cacheMock.Verify(x => x.RemoveAsync(indexKey, CancellationToken.None), Times.Once);
    }

    #endregion

    #region RevokeByRefreshTokenAsync Tests

    [Fact]
    public async Task RevokeByRefreshTokenAsync_WithNullToken_ShouldNotThrow()
    {
        // Act & Assert
        await _tokenService.RevokeByRefreshTokenAsync(null!);
        _cacheMock.Verify(x => x.GetAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RevokeByRefreshTokenAsync_WithEmptyToken_ShouldNotThrow()
    {
        // Act & Assert
        await _tokenService.RevokeByRefreshTokenAsync("");
        _cacheMock.Verify(x => x.GetAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RevokeByRefreshTokenAsync_WithNonExistentToken_ShouldNotThrow()
    {
        // Arrange
        _cacheMock.Setup(x => x.GetAsync<string>(It.IsAny<string>(), CancellationToken.None))
            .ReturnsAsync((string?)null);

        // Act & Assert
        await _tokenService.RevokeByRefreshTokenAsync("nonexistent-token");
    }

    [Fact]
    public async Task RevokeByRefreshTokenAsync_WithValidToken_ShouldRevokeAndCleanupIndex()
    {
        // Arrange
        var token = "valid-token";
        var hashedToken = Infrastructure.Services.Security.TokenHasher.HashToken(token);
        var indexKey = $"tokenindex:{hashedToken}";
        var sessionKey = "session:1:jti1";

        _cacheMock.Setup(x => x.GetAsync<string>(indexKey, CancellationToken.None))
            .ReturnsAsync(sessionKey);

        _cacheMock.Setup(x => x.GetAsync<RefreshToken>(sessionKey, CancellationToken.None))
            .ReturnsAsync(new RefreshToken
            {
                UserId = "1",
                TokenHash = hashedToken,
                Revoked = false,
                Expires = DateTime.UtcNow.AddDays(30),
                Jti = "jti1"
            });

        _cacheMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan>(), CancellationToken.None))
            .Returns(Task.CompletedTask);
        _cacheMock.Setup(x => x.RemoveAsync(It.IsAny<string>(), CancellationToken.None))
            .Returns(Task.CompletedTask);

        // Act
        await _tokenService.RevokeByRefreshTokenAsync(token);

        // Assert
        _cacheMock.Verify(
            x => x.SetAsync(sessionKey, It.Is<RefreshToken>(t => t.Revoked == true), It.IsAny<TimeSpan>(), CancellationToken.None),
            Times.Once);
        _cacheMock.Verify(x => x.RemoveAsync(indexKey, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task RevokeByRefreshTokenAsync_WithAlreadyRevokedToken_ShouldNotRevoke()
    {
        // Arrange
        var token = "already-revoked";
        var hashedToken = Infrastructure.Services.Security.TokenHasher.HashToken(token);
        var indexKey = $"tokenindex:{hashedToken}";
        var sessionKey = "session:1:jti1";

        _cacheMock.Setup(x => x.GetAsync<string>(indexKey, CancellationToken.None))
            .ReturnsAsync(sessionKey);

        _cacheMock.Setup(x => x.GetAsync<RefreshToken>(sessionKey, CancellationToken.None))
            .ReturnsAsync(new RefreshToken
            {
                UserId = "1",
                TokenHash = hashedToken,
                Revoked = true,
                Expires = DateTime.UtcNow.AddDays(30),
                Jti = "jti1"
            });

        // Act
        await _tokenService.RevokeByRefreshTokenAsync(token);

        // Assert - SetAsync should NOT be called since token was already revoked
        _cacheMock.Verify(
            x => x.SetAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region RevokeAllSessionsAsync Additional Tests

    [Fact]
    public async Task RevokeAllSessionsAsync_WithNoSessions_ShouldNotThrow()
    {
        // Arrange
        _cacheMock.Setup(x => x.SearchKeysAsync("session:1:*"))
            .ReturnsAsync(new List<string>());

        // Act & Assert
        await _tokenService.RevokeAllSessionsAsync(1);

        _cacheMock.Verify(
            x => x.SetAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RevokeAllSessionsAsync_ShouldSkipAlreadyRevokedSessions()
    {
        // Arrange
        var userId = 1;
        var keys = new List<string> { "session:1:jti1", "session:1:jti2" };

        _cacheMock.Setup(x => x.SearchKeysAsync($"session:{userId}:*"))
            .ReturnsAsync(keys);

        // jti1 already revoked, jti2 still active
        _cacheMock.Setup(x => x.GetAsync<RefreshToken>("session:1:jti1", CancellationToken.None))
            .ReturnsAsync(new RefreshToken { UserId = "1", TokenHash = "h1", Revoked = true, Expires = DateTime.UtcNow.AddDays(30), Jti = "jti1" });
        _cacheMock.Setup(x => x.GetAsync<RefreshToken>("session:1:jti2", CancellationToken.None))
            .ReturnsAsync(new RefreshToken { UserId = "1", TokenHash = "h2", Revoked = false, Expires = DateTime.UtcNow.AddDays(30), Jti = "jti2" });

        _cacheMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan>(), CancellationToken.None))
            .Returns(Task.CompletedTask);
        _cacheMock.Setup(x => x.RemoveAsync(It.IsAny<string>(), CancellationToken.None))
            .Returns(Task.CompletedTask);

        // Act
        await _tokenService.RevokeAllSessionsAsync(userId);

        // Assert - only the active session should be revoked
        _cacheMock.Verify(
            x => x.SetAsync("session:1:jti2", It.Is<RefreshToken>(t => t.Revoked == true), It.IsAny<TimeSpan>(), CancellationToken.None),
            Times.Once);
        // jti1 was already revoked, should not be set again
        _cacheMock.Verify(
            x => x.SetAsync("session:1:jti1", It.IsAny<object>(), It.IsAny<TimeSpan>(), CancellationToken.None),
            Times.Never);
    }

    #endregion

    #region GenerateAccessToken Additional Tests

    [Fact]
    public void GenerateAccessToken_ShouldContainExpectedClaims()
    {
        // Arrange
        var user = new User
        {
            Id = 42,
            Username = "claimuser",
            Email = "claims@test.com",
            Role = "Admin",
            Status = UserStatus.Active
        };

        // Act
        var token = _tokenService.GenerateAccessToken(user);
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        // Assert
        jwt.Claims.Should().Contain(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier && c.Value == "42");
        jwt.Claims.Should().Contain(c => c.Type == System.Security.Claims.ClaimTypes.Email && c.Value == "claims@test.com");
        jwt.Claims.Should().Contain(c => c.Type == System.Security.Claims.ClaimTypes.Role && c.Value == "Admin");
        jwt.Claims.Should().Contain(c => c.Type == "jti");
    }

    [Fact]
    public void GenerateAccessToken_ShouldHaveCorrectIssuerAndAudience()
    {
        // Arrange
        var user = new User { Id = 1, Username = "u", Email = "e@e.com", Role = "User", Status = UserStatus.Active };

        // Act
        var token = _tokenService.GenerateAccessToken(user);
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        // Assert
        jwt.Issuer.Should().Be("localhost");
        jwt.Audiences.Should().Contain("localhostClient");
    }

    [Fact]
    public void GenerateAccessToken_ShouldExpireInConfiguredMinutes()
    {
        // Arrange
        var user = new User { Id = 1, Username = "u", Email = "e@e.com", Role = "User", Status = UserStatus.Active };
        var before = DateTime.UtcNow;

        // Act
        var token = _tokenService.GenerateAccessToken(user);
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        // Assert - configured to 15 minutes
        jwt.ValidTo.Should().BeAfter(before.AddMinutes(14));
        jwt.ValidTo.Should().BeBefore(before.AddMinutes(16));
    }

    #endregion

    #region GenerateRefreshTokenAsync Additional Tests

    [Fact]
    public async Task GenerateRefreshTokenAsync_ShouldReturnBase64String()
    {
        // Arrange
        var user = new User { Id = 1, Username = "u", Email = "e@e.com", Role = "User" };
        var jti = Guid.NewGuid().ToString();

        _cacheMock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<TimeSpan>(), CancellationToken.None))
            .Returns(Task.CompletedTask);

        // Act
        var token = await _tokenService.GenerateRefreshTokenAsync(user, jti);

        // Assert - should be valid base64
        var act = () => Convert.FromBase64String(token);
        act.Should().NotThrow();
        Convert.FromBase64String(token).Length.Should().Be(32);
    }

    #endregion
}
