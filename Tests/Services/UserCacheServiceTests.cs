using Core.DTOs;
using Core.Helpers;
using Core.Interfaces;
using Core.Spec;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Infrastructure.Services.Cache;
using Xunit;

namespace Tests.Services;

public class UserCacheServiceTests
{
    private readonly Mock<ICacheRepository> _cacheRepoMock;
    private readonly Mock<IUserCacheVersionService> _versionServiceMock;
    private readonly UserCacheService _service;

    public UserCacheServiceTests()
    {
        _cacheRepoMock = new Mock<ICacheRepository>();
        _versionServiceMock = new Mock<IUserCacheVersionService>();

        var settings = Options.Create(new CacheSettings
        {
            UserExpirationMinutes = 30,
            UsersListExpirationMinutes = 10
        });

        _versionServiceMock.Setup(x => x.GetVersionAsync()).ReturnsAsync(1L);

        _service = new UserCacheService(
            _cacheRepoMock.Object,
            settings,
            _versionServiceMock.Object
        );
    }

    #region GetUserAsync / CacheUserAsync

    [Fact]
    public async Task GetUserAsync_WithCachedUser_ShouldReturnAndRefresh()
    {
        // Arrange
        var userDto = new UserDto { Id = 1, Username = "test" };
        _cacheRepoMock.Setup(x => x.GetAsync<UserDto>("user:1", CancellationToken.None))
            .ReturnsAsync(userDto);
        _cacheRepoMock.Setup(x => x.RefreshAsync("user:1", It.IsAny<TimeSpan>(), CancellationToken.None))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.GetUserAsync(1);

        // Assert
        result.Should().NotBeNull();
        result!.Username.Should().Be("test");
        _cacheRepoMock.Verify(x => x.RefreshAsync("user:1", It.IsAny<TimeSpan>(), CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task GetUserAsync_WithNoCache_ShouldReturnNull()
    {
        // Arrange
        _cacheRepoMock.Setup(x => x.GetAsync<UserDto>("user:1", CancellationToken.None))
            .ReturnsAsync((UserDto?)null);

        // Act
        var result = await _service.GetUserAsync(1);

        // Assert
        result.Should().BeNull();
        _cacheRepoMock.Verify(x => x.RefreshAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CacheUserAsync_ShouldStoreInCache()
    {
        // Arrange
        var userDto = new UserDto { Id = 1, Username = "test" };
        _cacheRepoMock.Setup(x => x.SetAsync("user:1", userDto, It.IsAny<TimeSpan>(), CancellationToken.None))
            .Returns(Task.CompletedTask);

        // Act
        await _service.CacheUserAsync(1, userDto);

        // Assert
        _cacheRepoMock.Verify(
            x => x.SetAsync("user:1", userDto, It.IsAny<TimeSpan>(), CancellationToken.None),
            Times.Once);
    }

    #endregion

    #region GetUsersAsync / CacheUsersAsync

    [Fact]
    public async Task GetUsersAsync_WithCachedList_ShouldReturnData()
    {
        // Arrange
        var specParams = new UserSpecParams { PageIndex = 1, PageSize = 10 };
        var pagination = new Pagination<UserDto>
        {
            PageIndex = 1,
            PageSize = 10,
            TotalCount = 1,
            Data = new List<UserDto> { new() { Id = 1 } }
        };

        _cacheRepoMock.Setup(x => x.GetAsync<Pagination<UserDto>>(It.IsAny<string>(), CancellationToken.None))
            .ReturnsAsync(pagination);

        // Act
        var result = await _service.GetUsersAsync(specParams);

        // Assert
        result.Should().NotBeNull();
        result!.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetUsersAsync_WithNoCache_ShouldReturnNull()
    {
        // Arrange
        var specParams = new UserSpecParams { PageIndex = 1, PageSize = 10 };
        _cacheRepoMock.Setup(x => x.GetAsync<Pagination<UserDto>>(It.IsAny<string>(), CancellationToken.None))
            .ReturnsAsync((Pagination<UserDto>?)null);

        // Act
        var result = await _service.GetUsersAsync(specParams);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CacheUsersAsync_ShouldStoreWithVersionedKey()
    {
        // Arrange
        var specParams = new UserSpecParams { PageIndex = 1, PageSize = 10 };
        var pagination = new Pagination<UserDto>();

        _cacheRepoMock.Setup(x => x.SetAsync(It.IsAny<string>(), pagination, It.IsAny<TimeSpan>(), CancellationToken.None))
            .Returns(Task.CompletedTask);

        // Act
        await _service.CacheUsersAsync(specParams, pagination);

        // Assert
        _cacheRepoMock.Verify(
            x => x.SetAsync(It.Is<string>(k => k.StartsWith("users:v1:")), pagination, It.IsAny<TimeSpan>(), CancellationToken.None),
            Times.Once);
    }

    #endregion

    #region Invalidation

    [Fact]
    public async Task InvalidateUserAsync_ShouldPublishInvalidation()
    {
        // Arrange
        _cacheRepoMock.Setup(x => x.PublishInvalidationAsync("user:42"))
            .Returns(Task.CompletedTask);

        // Act
        await _service.InvalidateUserAsync(42);

        // Assert
        _cacheRepoMock.Verify(x => x.PublishInvalidationAsync("user:42"), Times.Once);
    }

    [Fact]
    public async Task InvalidateUsersAsync_ShouldIncrementVersion()
    {
        // Arrange
        _versionServiceMock.Setup(x => x.IncrementVersionAsync()).ReturnsAsync(2L);

        // Act
        await _service.InvalidateUsersAsync();

        // Assert
        _versionServiceMock.Verify(x => x.IncrementVersionAsync(), Times.Once);
    }

    #endregion
}
