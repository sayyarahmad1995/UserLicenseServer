using Infrastructure.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Helpers;

public class AuthHelper : IAuthHelper
{
    public void SetAuthCookies(HttpResponse response, string accessToken, string refreshToken, IConfiguration config)
    {
        var accessExpiryMinutes = int.Parse(config["Jwt:AccessTokenExpiryMinutes"]!);
        var refreshExpiryDays = int.Parse(config["Jwt:RefreshTokenExpiryDays"]!);

        var accessOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTime.UtcNow.AddMinutes(accessExpiryMinutes),
            Path = "/api/v1"
        };

        var refreshOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTime.UtcNow.AddDays(refreshExpiryDays),
            Path = "/api/v1/auth"
        };

        response.Cookies.Append("accessToken", accessToken, accessOptions);
        response.Cookies.Append("refreshToken", refreshToken, refreshOptions);
    }

    public void ClearAuthCookies(HttpResponse response)
    {
        var accessDeleteOptions = new CookieOptions
        {
            Path = "/api/v1"
        };

        var refreshDeleteOptions = new CookieOptions
        {
            Path = "/api/v1/auth"
        };

        response.Cookies.Delete("accessToken", accessDeleteOptions);
        response.Cookies.Delete("refreshToken", refreshDeleteOptions);
    }

    public bool TryGetCookie(HttpRequest request, string key, out string? value)
    {
        return request.Cookies.TryGetValue(key, out value);
    }
}
