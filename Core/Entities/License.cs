using Core.Enums;

namespace Core.Entities;

public class License : BaseEntity
{
    public string LicenseKey { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public LicenseStatus Status { get; set; } = LicenseStatus.Active;

    /// <summary>Maximum concurrent machine activations allowed (0 = unlimited).</summary>
    public int MaxActivations { get; set; } = 1;

    public int UserId { get; set; }
    public User? User { get; set; }

    public ICollection<LicenseActivation> Activations { get; set; } = new List<LicenseActivation>();
}
