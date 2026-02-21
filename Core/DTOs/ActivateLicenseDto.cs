using System.ComponentModel.DataAnnotations;

namespace Core.DTOs;

public class ActivateLicenseDto
{
    [Required]
    public string LicenseKey { get; set; } = string.Empty;

    [Required]
    [StringLength(256, MinimumLength = 8)]
    public string MachineFingerprint { get; set; } = string.Empty;

    [StringLength(256)]
    public string? Hostname { get; set; }
}
