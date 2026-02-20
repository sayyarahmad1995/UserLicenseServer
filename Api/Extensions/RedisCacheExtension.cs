using Core.Interfaces;
using Infrastructure.Data;

namespace Api.Extensions;

public static class RedisCacheExtensions
{
    /// <summary>
    /// Subscribes to Redis cache invalidation events and automatically removes the corresponding key.
    /// </summary>
    public static void UseRedisCacheInvalidation(this IApplicationBuilder app)
    {
        var scope = app.ApplicationServices.CreateScope();
        var cacheRepo = scope.ServiceProvider.GetRequiredService<ICacheRepository>();

        var logger = scope.ServiceProvider.GetRequiredService<ILogger<RedisCacheRepository>>();

        if (cacheRepo is RedisCacheRepository redisCache)
        {
            redisCache.SubscribeToInvalidations(async key =>
            {
                logger.LogInformation("[Redis 🔁] Invalidation received for key: {Key}", key);
                try
                {
                    await redisCache.RemoveAsync(key);
                    logger.LogInformation("[Redis 🔁] Cache removed for key: {Key}", key);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "[Redis ⚠️] Failed to remove cache for key {Key}", key);
                }
            });
        }
    }
}
