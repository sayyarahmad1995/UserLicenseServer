using Core.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;

namespace Infrastructure.Data;

public class RedisCacheRepository : ICacheRepository
{
   private readonly IDatabase _database;
   private readonly IConnectionMultiplexer _redis;
   private readonly ILogger<RedisCacheRepository> _logger;
   private readonly TimeSpan _defaultTimeout = TimeSpan.FromMilliseconds(200);

   private static readonly JsonSerializerOptions _serializerOptions = new()
   {
      PropertyNameCaseInsensitive = true,
      WriteIndented = false
   };

   public RedisCacheRepository(IConnectionMultiplexer redis, ILogger<RedisCacheRepository> logger)
   {
      _redis = redis;
      _logger = logger;
      _database = _redis.GetDatabase();
   }

   public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
   {
      try
      {
         var json = JsonSerializer.Serialize(value, _serializerOptions);
         var redisTask = _database.StringSetAsync(key, json, expiry);
         var completed = await Task.WhenAny(redisTask, Task.Delay(_defaultTimeout));

         if (completed != redisTask)
            throw new TimeoutException($"Redis SET timeout for key '{key}'.");

         await redisTask;
      }
      catch (Exception ex)
      {
         _logger.LogWarning(ex, "Failed to SET key '{Key}' in Redis.", key);
      }
   }

   public async Task<T?> GetAsync<T>(string key)
   {
      try
      {
         var redisTask = _database.StringGetAsync(key);
         var completed = await Task.WhenAny(redisTask, Task.Delay(_defaultTimeout));

         if (completed != redisTask)
         {
            _logger.LogWarning("Redis GET timeout for key '{Key}'.", key);
            return default;
         }

         var value = await redisTask;
         return value.IsNullOrEmpty
            ? default
            : JsonSerializer.Deserialize<T>(value!, _serializerOptions);
      }
      catch (Exception ex)
      {
         _logger.LogWarning(ex, "Redis GET failed for key '{Key}'.", key);
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
      catch (Exception ex)
      {
         _logger.LogWarning(ex, "Redis REMOVE failed for key '{Key}'.", key);
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
      catch (Exception ex)
      {
         _logger.LogWarning(ex, "Redis EXISTS failed for key '{Key}'.", key);
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
         {
            _logger.LogWarning("Redis PING timeout.");
            return false;
         }

         var latency = await redisTask;
         _logger.LogDebug("Redis PING latency: {Latency}ms", latency.TotalMilliseconds);
         return latency.TotalMilliseconds < 1000;
      }
      catch (Exception ex)
      {
         _logger.LogWarning(ex, "Redis PING failed.");
         return false;
      }
   }

   public async Task PublishInvalidationAsync(string key)
   {
      try
      {
         var sub = _redis.GetSubscriber();
         await sub.PublishAsync(RedisChannel.Literal("cache-invalidation"), key);
         _logger.LogDebug("Published cache invalidation for key '{Key}'.", key);
      }
      catch (Exception ex)
      {
         _logger.LogError(ex, "Redis publish error for key '{Key}'.", key);
      }
   }

   public void SubscribeToInvalidations(Func<string, Task> onInvalidation)
   {
      try
      {
         var sub = _redis.GetSubscriber();
         sub.Subscribe(RedisChannel.Literal("cache-invalidation"), async (channel, message) =>
         {
            _logger.LogDebug("Cache invalidation received for key '{Key}'.", message);
            await onInvalidation(message!);
         });
      }
      catch (Exception ex)
      {
         _logger.LogError(ex, "Redis subscription error.");
      }
   }

   public async Task<IEnumerable<string>> SearchKeysAsync(string pattern)
   {
      var server = GetServer();
      if (server == null)
      {
         _logger.LogWarning("No Redis server available for key search.");
         return Enumerable.Empty<string>();
      }

      try
      {
         var results = new List<string>();

         await foreach (var key in server.KeysAsync(pattern: pattern))
         {
            results.Add(key.ToString());
         }

         return results;
      }
      catch (Exception ex)
      {
         _logger.LogError(ex, "Redis key search failed for pattern '{Pattern}'.", pattern);
         return Enumerable.Empty<string>();
      }
   }

   private IServer? GetServer()
   {
      try
      {
         var endpoints = _redis.GetEndPoints();
         if (endpoints.Length == 0)
            return null;

         var endpoint = endpoints.Length == 1
             ? endpoints[0]
             : endpoints[new Random().Next(endpoints.Length)];

         return _redis.GetServer(endpoint);
      }
      catch (Exception ex)
      {
         _logger.LogError(ex, "Failed to get Redis server instance.");
         return null;
      }
   }
}
