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

    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private static readonly Random _random = new();
    private readonly string _keyPrefix;
    private readonly TimeSpan _defaultTimeout;

    public RedisCacheRepository(
       IConnectionMultiplexer redis,
       ILogger<RedisCacheRepository> logger,
       TimeSpan? defaultTimeout = null,
       string keyPrefix = "")
    {
        _redis = redis;
        _logger = logger;
        _database = _redis.GetDatabase();
        _keyPrefix = keyPrefix;
        _defaultTimeout = defaultTimeout ?? TimeSpan.FromMilliseconds(200);
    }

    private string BuildKey(string key) => string.IsNullOrEmpty(_keyPrefix) ? key : $"{_keyPrefix}:{key}";

    private CancellationToken GetCancellationToken(CancellationToken token)
       => token != default ? token : new CancellationTokenSource(_defaultTimeout).Token;

    #region Core Redis Operations

    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        var token = GetCancellationToken(cancellationToken);
        return ExecuteSafelyAsync(
           () => _database.StringSetAsync(BuildKey(key), JsonSerializer.Serialize(value, _serializerOptions), expiry)
           .WaitAsync(token),
           "SET",
           key
        );
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var token = GetCancellationToken(cancellationToken);
        try
        {
            var value = await _database.StringGetAsync(BuildKey(key)).WaitAsync(token).ConfigureAwait(false);
            if (value.IsNullOrEmpty) return default;
            return JsonSerializer.Deserialize<T>(value!, _serializerOptions);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Redis GET operation timed out for key '{Key}'.", key);
            return default;
        }
        catch (JsonException jex)
        {
            _logger.LogError(jex, "Deserialization failed for key '{Key}'. Type: {Type}", key, typeof(T).Name);
            return default;
        }
        catch (RedisConnectionException rex)
        {
            _logger.LogError(rex, "Redis connection issue while GETting key '{Key}'.", key);
            return default;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during Redis GET for key '{Key}'.", key);
            return default;
        }
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
       => ExecuteSafelyAsync(() => _database.KeyDeleteAsync(BuildKey(key)).WaitAsync(GetCancellationToken(cancellationToken)), "REMOVE", key);

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        var token = GetCancellationToken(cancellationToken);
        try
        {
            return await _database.KeyExistsAsync(BuildKey(key)).WaitAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Redis EXISTS operation timed out for key '{Key}'.", key);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis EXISTS failed for key '{Key}'.", key);
            return false;
        }
    }

    public async Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        var token = GetCancellationToken(cancellationToken);
        try
        {
            var latency = await _database.PingAsync().WaitAsync(token).ConfigureAwait(false);
            _logger.LogDebug("Redis PING latency: {Latency}ms", latency.TotalMilliseconds);
            return latency.TotalMilliseconds < 1000;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Redis PING operation timed out.");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during Redis PING.");
            return false;
        }
    }

    #endregion

    #region Pub/Sub

    public async Task PublishInvalidationAsync(string key)
    {
        try
        {
            var sub = _redis.GetSubscriber();
            await sub.PublishAsync(RedisChannel.Literal("cache-invalidation"), BuildKey(key)).ConfigureAwait(false);
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
                var key = message!;
                _logger.LogDebug("Cache invalidation received for key '{Key}'.", key);
                await onInvalidation(key!);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis subscription error.");
        }
    }

    #endregion

    #region Key Search

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
            if (endpoints.Length == 0) return null;

            var endpoint = endpoints.Length == 1
               ? endpoints[0]
               : endpoints[_random.Next(endpoints.Length)];

            return _redis.GetServer(endpoint);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Redis server instance.");
            return null;
        }
    }

    #endregion

    #region Helpers

    private async Task ExecuteSafelyAsync(Func<Task> action, string operation, string key)
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Redis {Operation} operation timed out for key '{Key}'.", operation, key);
        }
        catch (RedisConnectionException rex)
        {
            _logger.LogError(rex, "Redis connection issue during {Operation} for key '{Key}'.", operation, key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during Redis {Operation} for key '{Key}'.", operation, key);
        }
    }

    #endregion

    public Task RefreshAsync(string key, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        var token = GetCancellationToken(cancellationToken);

        return ExecuteSafelyAsync(
            () => _database.KeyExpireAsync(BuildKey(key), expiry).WaitAsync(token),
            "EXPIRE",
            key
        );
    }
}
