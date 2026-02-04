using Core.DTOs;
using Core.Helpers;
using Core.Interfaces;
using Core.Spec;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services.Cache;

public class UserCacheService : IUserCacheService
{
    private readonly ICacheRepository _cacheRepo;
    private readonly IUserCacheVersionService _versionService;
    private readonly TimeSpan _slidingExpiration;
    private readonly TimeSpan _usersListExpiration;

    public UserCacheService(
        ICacheRepository cacheRepo,
        IOptions<CacheSettings> cacheSettings,
        IUserCacheVersionService versionService)
    {
        _cacheRepo = cacheRepo;
        _versionService = versionService;
        _slidingExpiration = TimeSpan.FromMinutes(
            cacheSettings.Value.UserSlidingExpirationMinutes
        );
        _usersListExpiration = TimeSpan.FromMinutes(
            cacheSettings.Value.UsersListExpirationMinutes
        );
    }

    private async Task<string> BuildListKey(UserSpecParams p)
        => $"users:v{await _versionService.GetVersionAsync()}:{p.PageIndex}-{p.PageSize}-{p.Sort}-{p.Search}-{p.Status}";
    private static string BuildUserKey(int id)
        => $"user:{id}";
    public async Task<Pagination<UserDto>?> GetUsersAsync(UserSpecParams specParams)
        => await _cacheRepo.GetAsync<Pagination<UserDto>>(await BuildListKey(specParams));
    public async Task CacheUsersAsync(UserSpecParams specParams, Pagination<UserDto> data)
        => await _cacheRepo.SetAsync(await BuildListKey(specParams), data, _usersListExpiration);
    public Task InvalidateUserAsync(int id)
    => _cacheRepo.PublishInvalidationAsync(BuildUserKey(id));
    public Task InvalidateUsersAsync()
        => _versionService.IncrementVersionAsync();
    public async Task<UserDto?> GetUserAsync(int id)
    {
        var key = BuildUserKey(id);
        var cached = await _cacheRepo.GetAsync<UserDto>(key);
        if (cached != null)
        {
            await _cacheRepo.RefreshAsync(key, _slidingExpiration);
            return cached;
        }
        return null;
    }
    public Task CacheUserAsync(int id, UserDto user)
        => _cacheRepo.SetAsync(BuildUserKey(id), user, _slidingExpiration);
}