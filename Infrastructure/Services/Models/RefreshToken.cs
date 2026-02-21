namespace Infrastructure.Services.Models;

public class RefreshToken
{
    public string TokenId { get; set; } = Guid.NewGuid().ToString();
    public string Jti { get; set; } = default!;
    public string TokenHash { get; set; } = default!;
    public int UserId { get; set; }
    public DateTime Expires { get; set; }
    public bool Revoked { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? ReplacedByTokenId { get; set; }
    public bool IsActive => !Revoked && DateTime.UtcNow < Expires;
}
