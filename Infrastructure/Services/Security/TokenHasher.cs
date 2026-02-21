using System.Security.Cryptography;
using System.Text;

namespace Infrastructure.Services.Security;

public static class TokenHasher
{
    public static string HashToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }
}
