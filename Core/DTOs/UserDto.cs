namespace Core.DTOs;

public class UserDto
{
    public int Id { get; set; }
    public string? Username { get; set; }
    public string? Email { get; set; }
    public string? Role { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? LastLogin { get; set; }
    public string? Status { get; set; }

    // Notification preferences
    public bool NotifyLicenseExpiry { get; set; }
    public bool NotifyAccountActivity { get; set; }
    public bool NotifySystemAnnouncements { get; set; }
}
