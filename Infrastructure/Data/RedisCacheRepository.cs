using Core.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;

namespace Infrastructure.Data;

public class RedisCacheRepository : ICacheRepository
{
   private readonly IDatabase _database;
   private readonly IConnectionMultiplexer _redis;
   private readonly TimeSpan _defaultTimeout = TimeSpan.FromMilliseconds(200);
   private readonly ILogger<RedisCacheRepository> _logger;

   public RedisCacheRepository(IConnectionMultiplexer redis, ILogger<RedisCacheRepository> logger)
   {
      _logger = logger;
      _redis = redis;
      _database = _redis.GetDatabase();
   }

   public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
   {
      try
      {
         var json = JsonSerializer.Serialize(value);

         var redisTask = _database.StringSetAsync(key, json, expiry);
         var completed = await Task.WhenAny(redisTask, Task.Delay(_defaultTimeout));

         if (completed != redisTask)
            throw new TimeoutException("Redis SET timed out.");
      }
      catch
      {
         // Swallow exception silently or log if needed
      }
   }

   public async Task<T?> GetAsync<T>(string key)
   {
      try
      {
         var redisTask = _database.StringGetAsync(key);
         var completed = await Task.WhenAny(redisTask, Task.Delay(_defaultTimeout));

         if (completed != redisTask)
            return default;

         var value = await redisTask;
         return value.IsNullOrEmpty ? default : JsonSerializer.Deserialize<T>(value!);
      }
      catch
      {
         return default;
      }
   }

   public async Task RemoveAsync(string key)
   {
      try
      {
         var redisTask = _database.KeyDeleteAsync(key);
         await Task.WhenAny(redisTask, Task.Delay(_defaultTimeout));
      }
      catch
      {
         // Ignore if Redis is unavailable
      }
   }

   public async Task<bool> ExistsAsync(string key)
   {
      try
      {
         var redisTask = _database.KeyExistsAsync(key);
         var completed = await Task.WhenAny(redisTask, Task.Delay(_defaultTimeout));

         return completed == redisTask && await redisTask;
      }
      catch
      {
         return false;
      }
   }

   public async Task<bool> PingAsync()
   {
      try
      {
         var redisTask = _database.PingAsync();
         var completed = await Task.WhenAny(redisTask, Task.Delay(_defaultTimeout));

         if (completed != redisTask)
            return false;

         var latency = await redisTask;
         return latency.TotalMilliseconds < 1000;
      }
      catch
      {
         return false;
      }
   }

   public async Task PublishInvalidationAsync(string key)
   {
      try
      {
         var sub = _redis.GetSubscriber();
         await sub.PublishAsync(RedisChannel.Literal("cache-invalidation"), key);
      }
      catch (Exception ex)
      {
         _logger.LogError($"Redis connection error: {ex.Message}");
      }
   }

   public void SubscribeToInvalidations(Func<string, Task> onInvalidation)
   {
      try
      {
         var sub = _redis.GetSubscriber();
         sub.Subscribe(RedisChannel.Literal("cache-invalidation"), async (channel, message) =>
         {
            await onInvalidation(message!);
         });
      }
      catch (Exception ex)
      {
         _logger.LogError($"Redis connection error: {ex.Message}");
      }
   }

   public async Task<IEnumerable<string>> SearchKeysAsync(string pattern)
   {
      var server = GetServer();
      if (server == null)
         return Enumerable.Empty<string>();

      // StackExchange.Redis does not have async key search, so we wrap in Task.Run
      return await Task.Run(() =>
      {
         return server.Keys(pattern: pattern)
                   .Select(k => k.ToString())
                   .ToList();
      });
   }

   private IServer? GetServer()
   {
      try
      {
         // Get endpoints and take the first one
         var endpoints = _redis.GetEndPoints();
         if (endpoints.Length == 0)
            return null;

         return _redis.GetServer(endpoints[0]);
      }
      catch
      {
         return null;
      }
   }
}
