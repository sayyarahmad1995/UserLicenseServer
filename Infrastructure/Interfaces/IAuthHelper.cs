using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Interfaces;

public interface IAuthHelper
{
    void SetAuthCookies(HttpResponse response, string accessToken, string refreshToken, IConfiguration config);
    void ClearAuthCookies(HttpResponse response);
    bool TryGetCookie(HttpRequest request, string key, out string? value);
}