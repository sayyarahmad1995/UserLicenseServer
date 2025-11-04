using System.Security.Cryptography;
using System.Text;

namespace Infrastructure.Services.Security;

public static class TokenHasher
{
    public static string HashToken(string token)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = sha?.ComputeHash(bytes);
        return Convert.ToBase64String(hash!);
    }
}
