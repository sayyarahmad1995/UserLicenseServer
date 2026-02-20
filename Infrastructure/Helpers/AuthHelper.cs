using Infrastructure.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Helpers;

public class AuthHelper : IAuthHelper
{
    public async Task SetAuthCookiesAsync(HttpResponse response, string accessToken, string refreshToken, IConfiguration config)
    {
        var accessExpiryMinutes = int.Parse(config["Jwt:AccessTokenExpiryMinutes"]!);
        var refreshExpiryDays = int.Parse(config["Jwt:RefreshTokenExpiryDays"]!);

        var accessOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTime.UtcNow.AddMinutes(accessExpiryMinutes)
        };

        var refreshOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTime.UtcNow.AddDays(refreshExpiryDays)
        };

        response.Cookies.Append("accessToken", accessToken, accessOptions);
        response.Cookies.Append("refreshToken", refreshToken, refreshOptions);

        await Task.CompletedTask;
    }

    public void ClearAuthCookies(HttpResponse response)
    {
        response.Cookies.Delete("accessToken");
        response.Cookies.Delete("refreshToken");
    }

    public bool TryGetCookie(HttpRequest request, string key, out string? value)
    {
        return request.Cookies.TryGetValue(key, out value);
    }
}
