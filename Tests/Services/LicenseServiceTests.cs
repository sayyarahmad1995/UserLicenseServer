using Core.DTOs;
using Core.Entities;
using Core.Enums;
using Core.Interfaces;
using FluentAssertions;
using Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Tests.Services;

public class LicenseServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<ILicenseRepository> _licenseRepositoryMock;
    private readonly Mock<ILogger<LicenseService>> _loggerMock;
    private readonly LicenseService _licenseService;

    public LicenseServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _licenseRepositoryMock = new Mock<ILicenseRepository>();
        _loggerMock = new Mock<ILogger<LicenseService>>();

        _unitOfWorkMock.Setup(x => x.UserRepository).Returns(_userRepositoryMock.Object);
        _unitOfWorkMock.Setup(x => x.LicenseRepository).Returns(_licenseRepositoryMock.Object);

        _licenseService = new LicenseService(_unitOfWorkMock.Object, _loggerMock.Object);
    }

    #region CreateLicenseAsync Tests

    [Fact]
    public async Task CreateLicenseAsync_WithValidData_ShouldCreateLicense()
    {
        // Arrange
        var dto = new CreateLicenseDto
        {
            UserId = 1,
            ExpiresAt = DateTime.UtcNow.AddDays(365)
        };
        var user = new User { Id = 1, Username = "testuser", Email = "test@example.com" };

        _unitOfWorkMock.Setup(x => x.UserRepository.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _licenseService.CreateLicenseAsync(dto);

        // Assert
        result.Should().NotBeNull();
        result.LicenseKey.Should().NotBeNullOrEmpty();
        result.LicenseKey.Should().MatchRegex(@"^[A-Z0-9]{5}-[A-Z0-9]{5}-[A-Z0-9]{5}-[A-Z0-9]{5}-[A-Z0-9]{5}$");
        result.Status.Should().Be(LicenseStatus.Active);
        result.UserId.Should().Be(1);
        result.ExpiresAt.Should().BeAfter(DateTime.UtcNow);

        _unitOfWorkMock.Verify(x => x.LicenseRepository.Add(It.IsAny<License>()), Times.Once);
        _unitOfWorkMock.Verify(x => x.CompleteAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateLicenseAsync_WithNonExistentUser_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var dto = new CreateLicenseDto { UserId = 999, ExpiresAt = DateTime.UtcNow.AddDays(30) };

        _unitOfWorkMock.Setup(x => x.UserRepository.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act & Assert
        await FluentActions.Invoking(() => _licenseService.CreateLicenseAsync(dto))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task CreateLicenseAsync_WithPastExpiryDate_ShouldThrowArgumentException()
    {
        // Arrange
        var dto = new CreateLicenseDto
        {
            UserId = 1,
            ExpiresAt = DateTime.UtcNow.AddDays(-1)
        };
        var user = new User { Id = 1, Username = "testuser", Email = "test@example.com" };

        _unitOfWorkMock.Setup(x => x.UserRepository.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act & Assert
        await FluentActions.Invoking(() => _licenseService.CreateLicenseAsync(dto))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*future*");
    }

    [Fact]
    public async Task CreateLicenseAsync_ShouldGenerateUniqueLicenseKeys()
    {
        // Arrange
        var user = new User { Id = 1, Username = "testuser", Email = "test@example.com" };
        _unitOfWorkMock.Setup(x => x.UserRepository.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var dto = new CreateLicenseDto { UserId = 1, ExpiresAt = DateTime.UtcNow.AddDays(30) };

        // Act
        var license1 = await _licenseService.CreateLicenseAsync(dto);
        var license2 = await _licenseService.CreateLicenseAsync(dto);

        // Assert
        license1.LicenseKey.Should().NotBe(license2.LicenseKey);
    }

    #endregion

    #region RevokeLicenseAsync Tests

    [Fact]
    public async Task RevokeLicenseAsync_WithActiveLicense_ShouldRevoke()
    {
        // Arrange
        var license = new License
        {
            Id = 1,
            LicenseKey = "AAAAA-BBBBB-CCCCC-DDDDD-EEEEE",
            Status = LicenseStatus.Active,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };

        _unitOfWorkMock.Setup(x => x.LicenseRepository.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(license);

        // Act
        await _licenseService.RevokeLicenseAsync(1);

        // Assert
        license.Status.Should().Be(LicenseStatus.Revoked);
        license.RevokedAt.Should().NotBeNull();
        license.RevokedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        _unitOfWorkMock.Verify(x => x.LicenseRepository.Update(license), Times.Once);
        _unitOfWorkMock.Verify(x => x.CompleteAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RevokeLicenseAsync_WithAlreadyRevokedLicense_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var license = new License
        {
            Id = 1,
            LicenseKey = "AAAAA-BBBBB-CCCCC-DDDDD-EEEEE",
            Status = LicenseStatus.Revoked,
            RevokedAt = DateTime.UtcNow.AddDays(-1)
        };

        _unitOfWorkMock.Setup(x => x.LicenseRepository.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(license);

        // Act & Assert
        await FluentActions.Invoking(() => _licenseService.RevokeLicenseAsync(1))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already revoked*");
    }

    [Fact]
    public async Task RevokeLicenseAsync_WithNonExistentLicense_ShouldThrowInvalidOperationException()
    {
        // Arrange
        _unitOfWorkMock.Setup(x => x.LicenseRepository.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((License?)null);

        // Act & Assert
        await FluentActions.Invoking(() => _licenseService.RevokeLicenseAsync(999))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    #endregion

    #region RenewLicenseAsync Tests

    [Fact]
    public async Task RenewLicenseAsync_WithActiveLicense_ShouldExtendExpiry()
    {
        // Arrange
        var newExpiry = DateTime.UtcNow.AddDays(365);
        var license = new License
        {
            Id = 1,
            LicenseKey = "AAAAA-BBBBB-CCCCC-DDDDD-EEEEE",
            Status = LicenseStatus.Active,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };

        _unitOfWorkMock.Setup(x => x.LicenseRepository.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(license);

        // Act
        var result = await _licenseService.RenewLicenseAsync(1, newExpiry);

        // Assert
        result.ExpiresAt.Should().BeCloseTo(newExpiry.ToUniversalTime(), TimeSpan.FromSeconds(1));
        result.Status.Should().Be(LicenseStatus.Active);
        result.RevokedAt.Should().BeNull();

        _unitOfWorkMock.Verify(x => x.LicenseRepository.Update(license), Times.Once);
        _unitOfWorkMock.Verify(x => x.CompleteAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RenewLicenseAsync_WithExpiredLicense_ShouldReactivate()
    {
        // Arrange
        var newExpiry = DateTime.UtcNow.AddDays(365);
        var license = new License
        {
            Id = 1,
            LicenseKey = "AAAAA-BBBBB-CCCCC-DDDDD-EEEEE",
            Status = LicenseStatus.Expired,
            ExpiresAt = DateTime.UtcNow.AddDays(-10)
        };

        _unitOfWorkMock.Setup(x => x.LicenseRepository.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(license);

        // Act
        var result = await _licenseService.RenewLicenseAsync(1, newExpiry);

        // Assert
        result.Status.Should().Be(LicenseStatus.Active);
        result.ExpiresAt.Should().BeCloseTo(newExpiry.ToUniversalTime(), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task RenewLicenseAsync_WithRevokedLicense_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var license = new License
        {
            Id = 1,
            LicenseKey = "AAAAA-BBBBB-CCCCC-DDDDD-EEEEE",
            Status = LicenseStatus.Revoked,
            RevokedAt = DateTime.UtcNow.AddDays(-1)
        };

        _unitOfWorkMock.Setup(x => x.LicenseRepository.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(license);

        // Act & Assert
        await FluentActions.Invoking(() => _licenseService.RenewLicenseAsync(1, DateTime.UtcNow.AddDays(30)))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*revoked*");
    }

    [Fact]
    public async Task RenewLicenseAsync_WithPastDate_ShouldThrowArgumentException()
    {
        // Arrange
        var license = new License
        {
            Id = 1,
            LicenseKey = "AAAAA-BBBBB-CCCCC-DDDDD-EEEEE",
            Status = LicenseStatus.Active,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };

        _unitOfWorkMock.Setup(x => x.LicenseRepository.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(license);

        // Act & Assert
        await FluentActions.Invoking(() => _licenseService.RenewLicenseAsync(1, DateTime.UtcNow.AddDays(-5)))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*future*");
    }

    [Fact]
    public async Task RenewLicenseAsync_WithNonExistentLicense_ShouldThrowInvalidOperationException()
    {
        // Arrange
        _unitOfWorkMock.Setup(x => x.LicenseRepository.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((License?)null);

        // Act & Assert
        await FluentActions.Invoking(() => _licenseService.RenewLicenseAsync(999, DateTime.UtcNow.AddDays(30)))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    #endregion

    #region DeleteLicenseAsync Tests

    [Fact]
    public async Task DeleteLicenseAsync_WithExistingLicense_ShouldDelete()
    {
        // Arrange
        var license = new License
        {
            Id = 1,
            LicenseKey = "AAAAA-BBBBB-CCCCC-DDDDD-EEEEE",
            Status = LicenseStatus.Active
        };

        _unitOfWorkMock.Setup(x => x.LicenseRepository.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(license);

        // Act
        await _licenseService.DeleteLicenseAsync(1);

        // Assert
        _unitOfWorkMock.Verify(x => x.LicenseRepository.Delete(license), Times.Once);
        _unitOfWorkMock.Verify(x => x.CompleteAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteLicenseAsync_WithNonExistentLicense_ShouldThrowInvalidOperationException()
    {
        // Arrange
        _unitOfWorkMock.Setup(x => x.LicenseRepository.GetByIdAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((License?)null);

        // Act & Assert
        await FluentActions.Invoking(() => _licenseService.DeleteLicenseAsync(999))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    #endregion
}
