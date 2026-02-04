namespace Core.Helpers;

public class CacheSettings
{
    public int UserSlidingExpirationMinutes { get; set; } = 10;
    public int UsersListExpirationMinutes { get; set; } = 5;
}
