using Core.Enums;

namespace Core.Entities;

public class License : BaseEntity
{
    public string? LicenseKey { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public LicenseStatus Status { get; set; } = LicenseStatus.Active;

    public int UserId { get; set; }
    public User? User { get; set; }
}
