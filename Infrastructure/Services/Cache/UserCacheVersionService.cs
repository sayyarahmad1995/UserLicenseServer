using Core.Interfaces;
using StackExchange.Redis;

namespace Infrastructure.Services.Cache;

public class UserCacheVersionService : IUserCacheVersionService
{
    private readonly IDatabase _db;
    private const string VersionKey = "users:version";

    public UserCacheVersionService(IConnectionMultiplexer redis)
    {
        _db = redis.GetDatabase();
    }

    public async Task<long> GetVersionAsync()
    {
        var v = await _db.StringGetAsync(VersionKey);
        return v.HasValue ? (long)v : 0;
    }

    public Task<long> IncrementVersionAsync()
    {
        return _db.StringIncrementAsync(VersionKey);
    }
}