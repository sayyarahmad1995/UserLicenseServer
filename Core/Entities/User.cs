using Core.Enums;

namespace Core.Entities;

public class User : BaseEntity
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "User";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? VerifiedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? LastLogin { get; set; }
    public DateTime? BlockedAt { get; set; }
    public UserStatus Status { get; set; } = UserStatus.Unverified;

    // ── Notification preferences ──
    public bool NotifyLicenseExpiry { get; set; } = true;
    public bool NotifyAccountActivity { get; set; } = true;
    public bool NotifySystemAnnouncements { get; set; } = true;

    public ICollection<License> Licenses { get; set; } = new List<License>();

    public void Verify()
    {
        if (Status == UserStatus.Verified || Status == UserStatus.Active)
            return;

        VerifiedAt = DateTime.UtcNow;
        Status = UserStatus.Verified;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        if (Status == UserStatus.Blocked)
            throw new InvalidOperationException("Cannot activate a blocked user until unblocked.");

        Status = UserStatus.Active;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Block()
    {
        if (Status == UserStatus.Blocked)
            return;

        BlockedAt = DateTime.UtcNow;
        Status = UserStatus.Blocked;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Unblock()
    {
        if (Status != UserStatus.Blocked)
            return;

        BlockedAt = null;
        Status = UserStatus.Active;
        UpdatedAt = DateTime.UtcNow;
    }
}