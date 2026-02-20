namespace Core.Interfaces;

public interface ICacheRepository
{
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default);
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
    Task<bool> PingAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<string>> SearchKeysAsync(string pattern);
    Task PublishInvalidationAsync(string key);
    void SubscribeToInvalidations(Func<string, Task> onInvalidation);
    Task RefreshAsync(string key, TimeSpan? expiry = null, CancellationToken cancellationToken = default);
    /// <summary>
    /// Atomically increments a counter. Sets expiry only when the key is first created.
    /// </summary>
    Task<long> IncrementAsync(string key, TimeSpan? expiryOnCreate = null, CancellationToken cancellationToken = default);
}
