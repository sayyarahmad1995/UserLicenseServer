using Core.DTOs;
using Core.Entities;
using Core.Enums;
using Core.Helpers;
using Core.Interfaces;
using FluentAssertions;
using Infrastructure.Interfaces;
using Infrastructure.Services;
using Infrastructure.Services.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.IdentityModel.Tokens.Jwt;
using Xunit;

namespace Tests.Services;

public class AuthServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ITokenService> _tokenServiceMock;
    private readonly Mock<IAuthHelper> _authHelperMock;
    private readonly Mock<ICacheRepository> _cacheRepositoryMock;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly Mock<ILogger<AuthService>> _loggerMock;
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _tokenServiceMock = new Mock<ITokenService>();
        _authHelperMock = new Mock<IAuthHelper>();
        _cacheRepositoryMock = new Mock<ICacheRepository>();
        _emailServiceMock = new Mock<IEmailService>();
        _loggerMock = new Mock<ILogger<AuthService>>();

        var jwtSettings = Options.Create(new JwtSettings
        {
            Key = "dummy_key_not_used_in_auth_service_tests_pad_to_64_chars_or_more_for_safety",
            Issuer = "localhost",
            Audience = "localhostClient",
            AccessTokenExpiryMinutes = 15,
            RefreshTokenExpiryDays = 30
        });

        _authService = new AuthService(
            _unitOfWorkMock.Object,
            _tokenServiceMock.Object,
            _authHelperMock.Object,
            _cacheRepositoryMock.Object,
            _emailServiceMock.Object,
            jwtSettings,
            _loggerMock.Object
        );
    }

    #region LoginAsync Tests

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ShouldReturnSuccessResult()
    {
        // Arrange
        var loginDto = new LoginDto { Username = "testuser", Password = "TestPassword@123" };
        var user = new User
        {
            Id = 1,
            Username = "testuser",
            Email = "test@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("TestPassword@123"),
            Role = "User",
            Status = UserStatus.Active
        };

        var httpContextMock = new Mock<HttpContext>();
        var requestMock = new Mock<HttpRequest>();
        var responseMock = new Mock<HttpResponse>();
        responseMock.Setup(r => r.HttpContext).Returns(httpContextMock.Object);
        httpContextMock.Setup(c => c.Request).Returns(requestMock.Object);

        string? outToken = null;
        _authHelperMock
            .Setup(x => x.TryGetCookie(It.IsAny<HttpRequest>(), "refreshToken", out outToken))
            .Returns(false);

        _tokenServiceMock
            .Setup(x => x.ValidateRefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _unitOfWorkMock.Setup(x => x.UserRepository.GetByUsernameAsync("testuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Create a valid JWT token for testing
        var validToken = new JwtSecurityToken(
            issuer: "test",
            audience: "test",
            expires: DateTime.UtcNow.AddMinutes(15),
            claims: new[] { new System.Security.Claims.Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()) }
        );
        var validTokenString = new JwtSecurityTokenHandler().WriteToken(validToken);

        _tokenServiceMock.Setup(x => x.GenerateAccessToken(user))
            .Returns(validTokenString);

        _tokenServiceMock.Setup(x => x.GenerateRefreshTokenAsync(user, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("refresh_token");

        _authHelperMock.Setup(x => x.SetAuthCookies(responseMock.Object, validTokenString, "refresh_token"));

        // Act
        var result = await _authService.LoginAsync(loginDto, responseMock.Object);

        // Assert
        result.Should().NotBeNull();
        result.Message.Should().Be("Login successful");
        result.AccessTokenExpires.Should().NotBeNull();

        _unitOfWorkMock.Verify(x => x.UserRepository.GetByUsernameAsync("testuser", It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.CompleteAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_WithInvalidUsername_ShouldThrowInvalidCredentialsException()
    {
        // Arrange
        var loginDto = new LoginDto { Username = "nonexistent", Password = "TestPassword@123" };

        var httpContextMock = new Mock<HttpContext>();
        var requestMock = new Mock<HttpRequest>();
        var responseMock = new Mock<HttpResponse>();
        responseMock.Setup(r => r.HttpContext).Returns(httpContextMock.Object);
        httpContextMock.Setup(c => c.Request).Returns(requestMock.Object);

        string? outToken = null;
        _authHelperMock
            .Setup(x => x.TryGetCookie(It.IsAny<HttpRequest>(), "refreshToken", out outToken))
            .Returns(false);

        _tokenServiceMock
            .Setup(x => x.ValidateRefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _unitOfWorkMock.Setup(x => x.UserRepository.GetByUsernameAsync("nonexistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidCredentialsException>(async () =>
            await _authService.LoginAsync(loginDto, responseMock.Object)
        );

        _unitOfWorkMock.Verify(x => x.UserRepository.GetByUsernameAsync("nonexistent", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_WithInvalidPassword_ShouldThrowInvalidCredentialsException()
    {
        // Arrange
        var loginDto = new LoginDto { Username = "testuser", Password = "WrongPassword@123" };
        var user = new User
        {
            Id = 1,
            Username = "testuser",
            Email = "test@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("TestPassword@123"),
            Role = "User",
            Status = UserStatus.Active
        };

        var httpContextMock = new Mock<HttpContext>();
        var requestMock = new Mock<HttpRequest>();
        var responseMock = new Mock<HttpResponse>();
        responseMock.Setup(r => r.HttpContext).Returns(httpContextMock.Object);
        httpContextMock.Setup(c => c.Request).Returns(requestMock.Object);

        string? outToken = null;
        _authHelperMock
            .Setup(x => x.TryGetCookie(It.IsAny<HttpRequest>(), "refreshToken", out outToken))
            .Returns(false);

        _tokenServiceMock
            .Setup(x => x.ValidateRefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _unitOfWorkMock.Setup(x => x.UserRepository.GetByUsernameAsync("testuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidCredentialsException>(async () =>
            await _authService.LoginAsync(loginDto, responseMock.Object)
        );
    }

    [Fact]
    public async Task LoginAsync_WithExistingValidSession_ShouldStillLoginSuccessfully()
    {
        // Arrange - even with an existing session, login should proceed
        var loginDto = new LoginDto { Username = "testuser", Password = "TestPassword@123" };
        var user = new User
        {
            Id = 1,
            Username = "testuser",
            Email = "test@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("TestPassword@123"),
            Role = "User",
            Status = UserStatus.Active
        };

        var httpContextMock = new Mock<HttpContext>();
        var requestMock = new Mock<HttpRequest>();
        var responseMock = new Mock<HttpResponse>();
        responseMock.Setup(r => r.HttpContext).Returns(httpContextMock.Object);
        httpContextMock.Setup(c => c.Request).Returns(requestMock.Object);

        _unitOfWorkMock.Setup(x => x.UserRepository.GetByUsernameAsync("testuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var validToken = new JwtSecurityToken(
            issuer: "test",
            audience: "test",
            expires: DateTime.UtcNow.AddMinutes(15),
            claims: new[] { new System.Security.Claims.Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()) }
        );
        var validTokenString = new JwtSecurityTokenHandler().WriteToken(validToken);

        _tokenServiceMock.Setup(x => x.GenerateAccessToken(user))
            .Returns(validTokenString);

        _tokenServiceMock.Setup(x => x.GenerateRefreshTokenAsync(user, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("refresh_token");

        _authHelperMock.Setup(x => x.SetAuthCookies(responseMock.Object, validTokenString, "refresh_token"));

        // Act
        var result = await _authService.LoginAsync(loginDto, responseMock.Object);

        // Assert
        result.Should().NotBeNull();
        result.Message.Should().Be("Login successful");
        result.AccessTokenExpires.Should().NotBeNull();
        _unitOfWorkMock.Verify(x => x.UserRepository.GetByUsernameAsync("testuser", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_WithBlockedUser_ShouldThrowAccountBlockedException()
    {
        // Arrange
        var loginDto = new LoginDto { Username = "blockeduser", Password = "TestPassword@123" };
        var user = new User
        {
            Id = 2,
            Username = "blockeduser",
            Email = "blocked@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("TestPassword@123"),
            Role = "User",
            Status = UserStatus.Blocked
        };

        _unitOfWorkMock.Setup(x => x.UserRepository.GetByUsernameAsync("blockeduser", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var responseMock = new Mock<HttpResponse>();
        var requestMock = new Mock<HttpRequest>();
        var httpContextMock = new Mock<HttpContext>();
        httpContextMock.Setup(x => x.Request).Returns(requestMock.Object);
        responseMock.Setup(x => x.HttpContext).Returns(httpContextMock.Object);

        _authHelperMock.Setup(x => x.TryGetCookie(requestMock.Object, It.IsAny<string>(), out It.Ref<string?>.IsAny))
            .Returns(false);

        // Act & Assert
        await FluentActions.Invoking(() =>
            _authService.LoginAsync(loginDto, responseMock.Object)
        ).Should().ThrowAsync<AccountBlockedException>()
            .WithMessage("*blocked*");
    }

    #endregion

    #region ChangePasswordAsync Tests

    [Fact]
    public async Task ChangePasswordAsync_WithCorrectCurrentPassword_ShouldSucceed()
    {
        // Arrange
        var currentPassword = "OldPassword@123";
        var newPassword = "NewPassword@456";
        var user = new User
        {
            Id = 1,
            Username = "testuser",
            Email = "test@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(currentPassword),
            Status = UserStatus.Active
        };

        _unitOfWorkMock.Setup(x => x.UserRepository.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        await _authService.ChangePasswordAsync(1, currentPassword, newPassword);

        // Assert
        BCrypt.Net.BCrypt.Verify(newPassword, user.PasswordHash).Should().BeTrue();
        user.UpdatedAt.Should().NotBeNull();
        _unitOfWorkMock.Verify(x => x.UserRepository.Update(user), Times.Once);
        _unitOfWorkMock.Verify(x => x.CompleteAsync(It.IsAny<CancellationToken>()), Times.Once);
        _tokenServiceMock.Verify(x => x.RevokeAllSessionsAsync(1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChangePasswordAsync_WithWrongCurrentPassword_ShouldThrowInvalidCredentialsException()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Username = "testuser",
            Email = "test@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("CorrectPassword@123"),
            Status = UserStatus.Active
        };

        _unitOfWorkMock.Setup(x => x.UserRepository.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act & Assert
        await FluentActions.Invoking(() =>
            _authService.ChangePasswordAsync(1, "WrongPassword@123", "NewPassword@456")
        ).Should().ThrowAsync<InvalidCredentialsException>();

        _unitOfWorkMock.Verify(x => x.UserRepository.Update(It.IsAny<User>()), Times.Never);
        _tokenServiceMock.Verify(x => x.RevokeAllSessionsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ChangePasswordAsync_WithNonExistentUser_ShouldThrowInvalidOperationException()
    {
        // Arrange
        _unitOfWorkMock.Setup(x => x.UserRepository.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act & Assert
        await FluentActions.Invoking(() =>
            _authService.ChangePasswordAsync(999, "AnyPassword@123", "NewPassword@456")
        ).Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task ChangePasswordAsync_ShouldRevokeAllSessions()
    {
        // Arrange
        var currentPassword = "OldPassword@123";
        var user = new User
        {
            Id = 5,
            Username = "sessionuser",
            Email = "session@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(currentPassword),
            Status = UserStatus.Active
        };

        _unitOfWorkMock.Setup(x => x.UserRepository.GetByIdAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        await _authService.ChangePasswordAsync(5, currentPassword, "NewPassword@789");

        // Assert - all sessions for this user must be revoked
        _tokenServiceMock.Verify(x => x.RevokeAllSessionsAsync(5, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region VerifyEmail Tests

    [Fact]
    public async Task VerifyEmailAsync_WithValidToken_ShouldVerifyUser()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Username = "testuser",
            Email = "test@example.com",
            Status = UserStatus.Unverified
        };

        _cacheRepositoryMock.Setup(x => x.GetAsync<int?>("email_verify:valid_token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _unitOfWorkMock.Setup(x => x.UserRepository.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        await _authService.VerifyEmailAsync("valid_token");

        // Assert
        user.Status.Should().Be(UserStatus.Verified);
        user.VerifiedAt.Should().NotBeNull();
        _unitOfWorkMock.Verify(x => x.UserRepository.Update(user), Times.Once);
        _unitOfWorkMock.Verify(x => x.CompleteAsync(It.IsAny<CancellationToken>()), Times.Once);
        _cacheRepositoryMock.Verify(x => x.RemoveAsync("email_verify:valid_token", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task VerifyEmailAsync_WithInvalidToken_ShouldThrowInvalidOperationException()
    {
        // Arrange
        _cacheRepositoryMock.Setup(x => x.GetAsync<int?>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);

        // Act & Assert
        await FluentActions.Invoking(() => _authService.VerifyEmailAsync("invalid_token"))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*expired*");
    }

    [Fact]
    public async Task VerifyEmailAsync_WithAlreadyVerifiedUser_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var user = new User { Id = 1, Status = UserStatus.Active };

        _cacheRepositoryMock.Setup(x => x.GetAsync<int?>("email_verify:token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _unitOfWorkMock.Setup(x => x.UserRepository.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act & Assert
        await FluentActions.Invoking(() => _authService.VerifyEmailAsync("token"))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already verified*");
    }

    [Fact]
    public async Task GenerateVerificationTokenAsync_ShouldStoreTokenAndSendEmail()
    {
        // Arrange
        var user = new User { Id = 1, Username = "testuser", Email = "test@example.com" };
        _unitOfWorkMock.Setup(x => x.UserRepository.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var token = await _authService.GenerateVerificationTokenAsync(1);

        // Assert
        token.Should().NotBeNullOrEmpty();
        _cacheRepositoryMock.Verify(x => x.SetAsync(
            It.Is<string>(k => k.StartsWith("email_verify:")),
            1,
            It.IsAny<TimeSpan?>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _emailServiceMock.Verify(x => x.SendVerificationEmailAsync("test@example.com", token, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResendVerificationAsync_WithUnverifiedUser_ShouldSendEmail()
    {
        // Arrange
        var user = new User { Id = 1, Username = "testuser", Email = "test@example.com", Status = UserStatus.Unverified };
        _unitOfWorkMock.Setup(x => x.UserRepository.GetByEmailAsync("test@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _unitOfWorkMock.Setup(x => x.UserRepository.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        await _authService.ResendVerificationAsync("test@example.com");

        // Assert
        _emailServiceMock.Verify(x => x.SendVerificationEmailAsync("test@example.com", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResendVerificationAsync_WithVerifiedUser_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var user = new User { Id = 1, Email = "test@example.com", Status = UserStatus.Active };
        _unitOfWorkMock.Setup(x => x.UserRepository.GetByEmailAsync("test@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act & Assert
        await FluentActions.Invoking(() => _authService.ResendVerificationAsync("test@example.com"))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already verified*");
    }

    #endregion

    #region PasswordReset Tests

    [Fact]
    public async Task GeneratePasswordResetTokenAsync_ShouldStoreTokenAndSendEmail()
    {
        // Arrange
        var user = new User { Id = 1, Email = "test@example.com" };
        _unitOfWorkMock.Setup(x => x.UserRepository.GetByEmailAsync("test@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var token = await _authService.GeneratePasswordResetTokenAsync("test@example.com");

        // Assert
        token.Should().NotBeNullOrEmpty();
        _cacheRepositoryMock.Verify(x => x.SetAsync(
            It.Is<string>(k => k.StartsWith("password_reset:")),
            1,
            It.IsAny<TimeSpan?>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _emailServiceMock.Verify(x => x.SendPasswordResetEmailAsync("test@example.com", token, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GeneratePasswordResetTokenAsync_WithNonExistentEmail_ShouldThrow()
    {
        // Arrange
        _unitOfWorkMock.Setup(x => x.UserRepository.GetByEmailAsync("nope@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act & Assert
        await FluentActions.Invoking(() => _authService.GeneratePasswordResetTokenAsync("nope@example.com"))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task ResetPasswordAsync_WithValidToken_ShouldResetPassword()
    {
        // Arrange
        var user = new User
        {
            Id = 1,
            Username = "testuser",
            Email = "test@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("OldPassword@123"),
            Status = UserStatus.Active
        };

        _cacheRepositoryMock.Setup(x => x.GetAsync<int?>("password_reset:reset_token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _unitOfWorkMock.Setup(x => x.UserRepository.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        await _authService.ResetPasswordAsync("reset_token", "NewPassword@456");

        // Assert
        BCrypt.Net.BCrypt.Verify("NewPassword@456", user.PasswordHash).Should().BeTrue();
        _unitOfWorkMock.Verify(x => x.CompleteAsync(It.IsAny<CancellationToken>()), Times.Once);
        _cacheRepositoryMock.Verify(x => x.RemoveAsync("password_reset:reset_token", It.IsAny<CancellationToken>()), Times.Once);
        _tokenServiceMock.Verify(x => x.RevokeAllSessionsAsync(1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResetPasswordAsync_WithInvalidToken_ShouldThrowInvalidOperationException()
    {
        // Arrange
        _cacheRepositoryMock.Setup(x => x.GetAsync<int?>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);

        // Act & Assert
        await FluentActions.Invoking(() => _authService.ResetPasswordAsync("bad_token", "NewPassword@456"))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*expired*");
    }

    #endregion
}
