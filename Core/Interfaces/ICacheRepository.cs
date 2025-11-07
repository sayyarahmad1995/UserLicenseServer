namespace Core.Interfaces;

public interface ICacheRepository
{
   Task SetAsync<T>(string key, T value, TimeSpan? expiry = null);
   Task<T?> GetAsync<T>(string key);
   Task<IEnumerable<string>> SearchKeysAsync(string pattern);
   Task RemoveAsync(string key);
   Task<bool> ExistsAsync(string key);
   Task<bool> PingAsync();
   Task PublishInvalidationAsync(string key);
   void SubscribeToInvalidations(Func<string, Task> onInvalidation);

}
