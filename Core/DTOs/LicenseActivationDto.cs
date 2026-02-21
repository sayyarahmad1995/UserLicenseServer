namespace Core.DTOs;

public class LicenseActivationDto
{
    public int Id { get; set; }
    public string MachineFingerprint { get; set; } = string.Empty;
    public string? Hostname { get; set; }
    public string? IpAddress { get; set; }
    public DateTime ActivatedAt { get; set; }
    public DateTime? DeactivatedAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public bool IsActive { get; set; }
}
