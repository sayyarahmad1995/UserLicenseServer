using Core.Interfaces;
using StackExchange.Redis;
using System.Text.Json;

namespace Infrastructure.Data;

public class RedisCacheRepository : ICacheRepository
{
   private readonly IDatabase _database;
   private readonly IConnectionMultiplexer _redis;

   public RedisCacheRepository(IConnectionMultiplexer redis)
   {
      _redis = redis;
      _database = _redis.GetDatabase();
   }

   public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
   {
      var json = JsonSerializer.Serialize(value);
      await _database.StringSetAsync(key, json, expiry);
   }

   public async Task<T?> GetAsync<T>(string key)
   {
      var value = await _database.StringGetAsync(key);
      return value.IsNullOrEmpty ? default : JsonSerializer.Deserialize<T>(value!);
   }

   public async Task RemoveAsync(string key)
   {
      await _database.KeyDeleteAsync(key);
   }

   public async Task<bool> ExistsAsync(string key)
   {
      return await _database.KeyExistsAsync(key);
   }

   public async Task<bool> PingAsync()
   {
      try
      {
         var latency = await _database.PingAsync();
         return latency.TotalMilliseconds < 1000;
      }
      catch
      {
         return false;
      }
   }
}
