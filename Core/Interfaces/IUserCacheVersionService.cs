namespace Core.Interfaces;

public interface IUserCacheVersionService
{
    Task<long> GetVersionAsync();
    Task<long> IncrementVersionAsync();
}