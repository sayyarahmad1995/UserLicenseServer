namespace Core.Entities;

/// <summary>
/// Records each activation of a license on a specific machine.
/// </summary>
public class LicenseActivation : BaseEntity
{
    public int LicenseId { get; set; }
    public License? License { get; set; }

    public string MachineFingerprint { get; set; } = string.Empty;
    public string? Hostname { get; set; }
    public string? IpAddress { get; set; }

    public DateTime ActivatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeactivatedAt { get; set; }
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

    public bool IsActive => DeactivatedAt == null;
}
