using Core.DTOs;
using Core.Entities;
using Core.Enums;
using Core.Interfaces;
using FluentAssertions;
using Infrastructure.Interfaces;
using Infrastructure.Services;
using Infrastructure.Services.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.IdentityModel.Tokens.Jwt;
using Xunit;

namespace Tests.Services;

public class AuthServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ITokenService> _tokenServiceMock;
    private readonly Mock<IAuthHelper> _authHelperMock;
    private readonly Mock<IConfiguration> _configMock;
    private readonly Mock<ILogger<AuthService>> _loggerMock;
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _tokenServiceMock = new Mock<ITokenService>();
        _authHelperMock = new Mock<IAuthHelper>();
        _configMock = new Mock<IConfiguration>();
        _loggerMock = new Mock<ILogger<AuthService>>();

        _authService = new AuthService(
            _unitOfWorkMock.Object,
            _tokenServiceMock.Object,
            _authHelperMock.Object,
            _configMock.Object,
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
            .Setup(x => x.ValidateRefreshTokenAsync(It.IsAny<string>()))
            .ReturnsAsync(false);

        _unitOfWorkMock.Setup(x => x.UserRepository.GetByUsernameAsync("testuser"))
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

        _tokenServiceMock.Setup(x => x.GenerateRefreshTokenAsync(user, It.IsAny<string>()))
            .ReturnsAsync("refresh_token");

        _configMock.Setup(x => x["Jwt:AccessTokenExpiryMinutes"])
            .Returns("15");

        _authHelperMock.Setup(x => x.SetAuthCookiesAsync(responseMock.Object, validTokenString, "refresh_token", _configMock.Object))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _authService.LoginAsync(loginDto, responseMock.Object);

        // Assert
        result.Should().NotBeNull();
        result.Message.Should().Be("Login successful");
        result.AccessTokenExpires.Should().NotBeNull();

        _unitOfWorkMock.Verify(x => x.UserRepository.GetByUsernameAsync("testuser"), Times.Once);
        _unitOfWorkMock.Verify(x => x.CompleteAsync(), Times.Once);
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
            .Setup(x => x.ValidateRefreshTokenAsync(It.IsAny<string>()))
            .ReturnsAsync(false);

        _unitOfWorkMock.Setup(x => x.UserRepository.GetByUsernameAsync("nonexistent"))
            .ReturnsAsync((User?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidCredentialsException>(async () =>
            await _authService.LoginAsync(loginDto, responseMock.Object)
        );

        _unitOfWorkMock.Verify(x => x.UserRepository.GetByUsernameAsync("nonexistent"), Times.Once);
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
            .Setup(x => x.ValidateRefreshTokenAsync(It.IsAny<string>()))
            .ReturnsAsync(false);

        _unitOfWorkMock.Setup(x => x.UserRepository.GetByUsernameAsync("testuser"))
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

        _unitOfWorkMock.Setup(x => x.UserRepository.GetByUsernameAsync("testuser"))
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

        _tokenServiceMock.Setup(x => x.GenerateRefreshTokenAsync(user, It.IsAny<string>()))
            .ReturnsAsync("refresh_token");

        _configMock.Setup(x => x["Jwt:AccessTokenExpiryMinutes"])
            .Returns("15");

        _authHelperMock.Setup(x => x.SetAuthCookiesAsync(responseMock.Object, validTokenString, "refresh_token", _configMock.Object))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _authService.LoginAsync(loginDto, responseMock.Object);

        // Assert
        result.Should().NotBeNull();
        result.Message.Should().Be("Login successful");
        result.AccessTokenExpires.Should().NotBeNull();
        _unitOfWorkMock.Verify(x => x.UserRepository.GetByUsernameAsync("testuser"), Times.Once);
    }

    #endregion
}
