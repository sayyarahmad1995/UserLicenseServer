using Core.Helpers;
using Infrastructure.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Infrastructure.Helpers;

public class AuthHelper : IAuthHelper
{
    private readonly int _accessExpiryMinutes;
    private readonly int _refreshExpiryDays;
    private readonly IHostEnvironment _environment;

    public AuthHelper(IOptions<JwtSettings> jwtOptions, IHostEnvironment environment)
    {
        var jwt = jwtOptions.Value;
        _accessExpiryMinutes = jwt.AccessTokenExpiryMinutes;
        _refreshExpiryDays = jwt.RefreshTokenExpiryDays;
        _environment = environment;
    }

    public void SetAuthCookies(HttpResponse response, string accessToken, string refreshToken)
    {
        // In development, allow HTTP cookies (Secure=false) and relax SameSite for local testing
        // In production, enforce HTTPS-only cookies (Secure=true) with strict SameSite
        var isProduction = !_environment.IsDevelopment();
        
        var accessOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = isProduction,
            SameSite = isProduction ? SameSiteMode.Strict : SameSiteMode.Lax,
            Expires = DateTime.UtcNow.AddMinutes(_accessExpiryMinutes),
            Path = "/"  // Available to all paths
        };

        var refreshOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = isProduction,
            SameSite = isProduction ? SameSiteMode.Strict : SameSiteMode.Lax,
            Expires = DateTime.UtcNow.AddDays(_refreshExpiryDays),
            Path = "/"  // Available to all paths
        };

        response.Cookies.Append(CookieConstants.AccessToken, accessToken, accessOptions);
        response.Cookies.Append(CookieConstants.RefreshToken, refreshToken, refreshOptions);
    }

    public void ClearAuthCookies(HttpResponse response)
    {
        var accessDeleteOptions = new CookieOptions
        {
            Path = "/"
        };

        var refreshDeleteOptions = new CookieOptions
        {
            Path = "/"
        };

        response.Cookies.Delete(CookieConstants.AccessToken, accessDeleteOptions);
        response.Cookies.Delete(CookieConstants.RefreshToken, refreshDeleteOptions);
    }

    public bool TryGetCookie(HttpRequest request, string key, out string? value)
    {
        return request.Cookies.TryGetValue(key, out value);
    }
}
