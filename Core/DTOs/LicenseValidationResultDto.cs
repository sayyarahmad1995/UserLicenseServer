namespace Core.DTOs;

public class LicenseValidationResultDto
{
    public bool Valid { get; set; }
    public string? LicenseKey { get; set; }
    public string? Status { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? Reason { get; set; }
}
