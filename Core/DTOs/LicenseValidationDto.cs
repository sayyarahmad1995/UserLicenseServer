using System.ComponentModel.DataAnnotations;

namespace Core.DTOs;

public class LicenseValidationDto
{
    [Required]
    public string LicenseKey { get; set; } = string.Empty;

    [Required]
    public string MachineFingerprint { get; set; } = string.Empty;
}
