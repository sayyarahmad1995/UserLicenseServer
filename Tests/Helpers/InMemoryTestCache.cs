using Core.Interfaces;
using System.Text.Json;

namespace Tests.Helpers;

/// <summary>
/// In-memory ICacheRepository for unit/integration tests.
/// Mimics Redis behavior: supports Get/Set/Remove/Exists/Increment with TTL.
/// Provides ForceSet, GetRaw, and RawExists for test setup/assertion.
/// </summary>
public class InMemoryTestCache : ICacheRepository
{
    private readonly Dictionary<string, (string json, DateTime expiry)> _store = new();

    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(value);
        var exp = expiry.HasValue ? DateTime.UtcNow.Add(expiry.Value) : DateTime.MaxValue;
        _store[key] = (json, exp);
        return Task.CompletedTask;
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (_store.TryGetValue(key, out var item) && DateTime.UtcNow < item.expiry)
            return Task.FromResult(JsonSerializer.Deserialize<T>(item.json));
        return Task.FromResult(default(T));
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _store.Remove(key);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.ContainsKey(key) && DateTime.UtcNow < _store[key].expiry);

    public Task<long> IncrementAsync(string key, TimeSpan? expiryOnCreate = null, CancellationToken cancellationToken = default)
    {
        long newValue = 1;
        if (_store.TryGetValue(key, out var item) && DateTime.UtcNow < item.expiry)
        {
            var current = JsonSerializer.Deserialize<long>(item.json);
            newValue = current + 1;
            _store[key] = (JsonSerializer.Serialize(newValue), item.expiry);
        }
        else
        {
            var exp = expiryOnCreate.HasValue ? DateTime.UtcNow.Add(expiryOnCreate.Value) : DateTime.MaxValue;
            _store[key] = (JsonSerializer.Serialize(newValue), exp);
        }
        return Task.FromResult(newValue);
    }

    // --- Test helpers ---

    /// <summary>Force-set a value (for simulating time passage in penalty tests).</summary>
    public void ForceSet<T>(string key, T value, TimeSpan expiry)
    {
        _store[key] = (JsonSerializer.Serialize(value), DateTime.UtcNow.Add(expiry));
    }

    /// <summary>Read raw value for assertions.</summary>
    public T GetRaw<T>(string key)
    {
        if (_store.TryGetValue(key, out var item))
            return JsonSerializer.Deserialize<T>(item.json)!;
        return default!;
    }

    /// <summary>Check existence without TTL filtering (for verifying removal).</summary>
    public bool RawExists(string key) => _store.ContainsKey(key);

    // --- Interface members not commonly needed in tests ---
    public Task<bool> PingAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    public Task<IEnumerable<string>> SearchKeysAsync(string pattern) => Task.FromResult(_store.Keys.AsEnumerable());
    public Task PublishInvalidationAsync(string key) => Task.CompletedTask;
    public void SubscribeToInvalidations(Func<string, Task> onInvalidation) { }
    public Task RefreshAsync(string key, TimeSpan? expiry = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
