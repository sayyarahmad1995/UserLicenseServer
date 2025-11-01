using Core.Interfaces;
using Infrastructure.Data;

namespace Api.Extensions;

public static class RedisCacheExtensions
{
	/// <summary>
	/// Subscribes to Redis cache invalidation events and automatically removes the corresponding key.
	/// </summary>
	public static void UseRedisCacheInvalidation(this WebApplication app)
	{
		using var scope = app.Services.CreateScope();
		var cacheRepo = scope.ServiceProvider.GetRequiredService<ICacheRepository>();

		if (cacheRepo is RedisCacheRepository redisCache)
		{
			redisCache.SubscribeToInvalidations(async key =>
			{
				Console.ForegroundColor = ConsoleColor.Yellow;
				Console.WriteLine($"[Redis 🔁] Invalidation received for key: {key}");
				Console.ResetColor();

				// Automatically remove the cached key
				try
				{
					await redisCache.RemoveAsync(key);
					Console.ForegroundColor = ConsoleColor.Green;
					Console.WriteLine($"[Redis 🔁] Cache removed for key: {key}");
					Console.ResetColor();
				}
				catch (Exception ex)
				{
					Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine($"[Redis ⚠️] Failed to remove cache for key {key}: {ex.Message}");
					Console.ResetColor();
				}
			});
		}
	}
}
