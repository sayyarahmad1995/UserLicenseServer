namespace Core.Helpers;

public class CacheSettings
{
    public int UserExpirationMinutes { get; set; } = 10;
    public int UsersListExpirationMinutes { get; set; } = 5;
}
