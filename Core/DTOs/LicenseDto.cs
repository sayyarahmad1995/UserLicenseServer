using Core.Enums;

namespace Core.DTOs;

public class LicenseDto
{
    public int Id { get; set; }
    public string? LicenseKey { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public LicenseStatus Status { get; set; }
    public int MaxActivations { get; set; }
    public int ActiveActivations { get; set; }
}
