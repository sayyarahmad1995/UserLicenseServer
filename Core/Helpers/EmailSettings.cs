namespace Core.Helpers;

/// <summary>
/// SMTP configuration settings bound from appsettings.json "Email" section.
/// </summary>
public class EmailSettings
{
    /// <summary>
    /// SMTP server hostname (e.g. smtp.gmail.com, smtp.office365.com).
    /// </summary>
    public string SmtpHost { get; set; } = string.Empty;

    /// <summary>
    /// SMTP server port. Common values: 587 (STARTTLS), 465 (SSL), 25 (unencrypted).
    /// </summary>
    public int SmtpPort { get; set; } = 587;

    /// <summary>
    /// SMTP username / email for authentication.
    /// </summary>
    public string SmtpUser { get; set; } = string.Empty;

    /// <summary>
    /// SMTP password or app-specific password.
    /// </summary>
    public string SmtpPass { get; set; } = string.Empty;

    /// <summary>
    /// The "From" address shown to recipients.
    /// </summary>
    public string FromEmail { get; set; } = string.Empty;

    /// <summary>
    /// The display name shown in the "From" field.
    /// </summary>
    public string FromName { get; set; } = "EazeCad";

    /// <summary>
    /// Enable SSL/TLS for SMTP connection. Default true for port 587.
    /// </summary>
    public bool EnableSsl { get; set; } = true;

    /// <summary>
    /// Base URL of the front-end application for building verification/reset links.
    /// Example: "https://app.eazecad.com" or "http://localhost:3000"
    /// </summary>
    public string FrontendBaseUrl { get; set; } = "http://localhost:3000";
}
