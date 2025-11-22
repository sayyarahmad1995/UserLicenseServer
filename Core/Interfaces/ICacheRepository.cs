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
}
