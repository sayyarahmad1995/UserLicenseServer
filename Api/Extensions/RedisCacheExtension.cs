using StackExchange.Redis;

namespace Api.Extensions;

public static class RedisCacheExtensions
{
    /// <summary>
    /// Subscribes to Redis cache invalidation events and automatically removes the corresponding key.
    /// Uses IConnectionMultiplexer directly (Singleton) to avoid scoped service lifetime issues.
    /// </summary>
    public static void UseRedisCacheInvalidation(this IApplicationBuilder app)
    {
        var redis = app.ApplicationServices.GetRequiredService<IConnectionMultiplexer>();
        var logger = app.ApplicationServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("RedisCacheInvalidation");

        var subscriber = redis.GetSubscriber();
        var database = redis.GetDatabase();

        subscriber.Subscribe(RedisChannel.Literal("cache-invalidation"), async (channel, message) =>
        {
            var key = message.ToString();
            logger.LogInformation("[Redis] Invalidation received for key: {Key}", key);
            try
            {
                await database.KeyDeleteAsync(key);
                logger.LogInformation("[Redis] Cache removed for key: {Key}", key);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[Redis] Failed to remove cache for key {Key}", key);
            }
        });
    }
}
