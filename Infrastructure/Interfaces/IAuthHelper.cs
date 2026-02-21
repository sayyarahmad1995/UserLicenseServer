using Microsoft.AspNetCore.Http;

namespace Infrastructure.Interfaces;

public interface IAuthHelper
{
    void SetAuthCookies(HttpResponse response, string accessToken, string refreshToken);
    void ClearAuthCookies(HttpResponse response);
    bool TryGetCookie(HttpRequest request, string key, out string? value);
}