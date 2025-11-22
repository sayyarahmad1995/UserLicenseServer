using Core.Interfaces;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace Infrastructure.Services.Cache;

public class CacheInvalidationListener : BackgroundService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IUserCacheVersionService _versionService;

    public CacheInvalidationListener(IConnectionMultiplexer redis,
        IUserCacheVersionService versionService)
    {
        _redis = redis;
        _versionService = versionService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var sub = _redis.GetSubscriber();
        await sub.SubscribeAsync(RedisChannel.Literal("users:invalidate"), async (channel, value) => await _versionService.IncrementVersionAsync());
    }
}