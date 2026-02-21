namespace Core.DTOs;

public class NotificationPreferencesDto
{
    public bool NotifyLicenseExpiry { get; set; }
    public bool NotifyAccountActivity { get; set; }
    public bool NotifySystemAnnouncements { get; set; }
}
