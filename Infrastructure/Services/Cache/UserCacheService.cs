using System.Text.Json;
using Core.DTOs;
using Core.Helpers;
using Core.Interfaces;
using Core.Spec;
using StackExchange.Redis;

namespace Infrastructure.Services.Cache;

public class UserCacheService : IUserCacheService
{
    private readonly IDatabase _db;
    private readonly IUserCacheVersionService _versionService;

    public UserCacheService(IConnectionMultiplexer redis,
        IUserCacheVersionService versionService)
    {
        _db = redis.GetDatabase();
        _versionService = versionService;
    }

    private async Task<string> BuildKey(UserSpecParams p)
    {
        long version = await _versionService.GetVersionAsync();

        return $"users:v{version}:{p.PageIndex}-{p.PageSize}-{p.Sort}-{p.Search}-{p.Status}";
    }

    public async Task<Pagination<UserDto>?> GetUsersAsync(UserSpecParams specParams)
    {
        var key = await BuildKey(specParams);

        var value = await _db.StringGetAsync(key);
        if (value.IsNullOrEmpty)
            return null;

        return JsonSerializer.Deserialize<Pagination<UserDto>>(value!);
    }

    public async Task CacheUsersAsync(UserSpecParams specParams, Pagination<UserDto> data)
    {
        var key = await BuildKey(specParams);

        var json = JsonSerializer.Serialize(data);
        await _db.StringSetAsync(key, json, TimeSpan.FromMinutes(5));
    }

    public async Task InvalidateUsersAsync()
    {
        await _versionService.IncrementVersionAsync();
    }
}