namespace Core.Helpers;

/// <summary>
/// Centralized cache key templates to prevent typo drift across services.
/// </summary>
public static class CacheKeys
{
    public static string Session(int userId, string jti) => $"session:{userId}:{jti}";
    public static string SessionPattern(int userId) => $"session:{userId}:*";
    public static string TokenIndex(string hashedToken) => $"tokenindex:{hashedToken}";
    public static string User(int id) => $"user:{id}";
}
