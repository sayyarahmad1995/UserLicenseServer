namespace Core.Helpers;

/// <summary>
/// Centralized cookie name and path constants to prevent magic-string drift.
/// </summary>
public static class CookieConstants
{
    public const string AccessToken = "accessToken";
    public const string RefreshToken = "refreshToken";
    public const string ApiBasePath = "/api/v1";
    public const string AuthPath = "/api/v1/auth";
}
