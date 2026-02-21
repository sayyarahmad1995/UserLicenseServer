using Core.Helpers;
using Infrastructure.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Infrastructure.Helpers;

public class AuthHelper : IAuthHelper
{
    private readonly int _accessExpiryMinutes;
    private readonly int _refreshExpiryDays;

    public AuthHelper(IOptions<JwtSettings> jwtOptions)
    {
        var jwt = jwtOptions.Value;
        _accessExpiryMinutes = jwt.AccessTokenExpiryMinutes;
        _refreshExpiryDays = jwt.RefreshTokenExpiryDays;
    }

    public void SetAuthCookies(HttpResponse response, string accessToken, string refreshToken)
    {
        var accessOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTime.UtcNow.AddMinutes(_accessExpiryMinutes),
            Path = CookieConstants.ApiBasePath
        };

        var refreshOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTime.UtcNow.AddDays(_refreshExpiryDays),
            Path = CookieConstants.AuthPath
        };

        response.Cookies.Append(CookieConstants.AccessToken, accessToken, accessOptions);
        response.Cookies.Append(CookieConstants.RefreshToken, refreshToken, refreshOptions);
    }

    public void ClearAuthCookies(HttpResponse response)
    {
        var accessDeleteOptions = new CookieOptions
        {
            Path = CookieConstants.ApiBasePath
        };

        var refreshDeleteOptions = new CookieOptions
        {
            Path = CookieConstants.AuthPath
        };

        response.Cookies.Delete(CookieConstants.AccessToken, accessDeleteOptions);
        response.Cookies.Delete(CookieConstants.RefreshToken, refreshDeleteOptions);
    }

    public bool TryGetCookie(HttpRequest request, string key, out string? value)
    {
        return request.Cookies.TryGetValue(key, out value);
    }
}
