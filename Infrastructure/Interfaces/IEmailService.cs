namespace Infrastructure.Interfaces;

/// <summary>
/// Abstraction for sending emails. Swap the implementation to use SMTP, SendGrid, etc.
/// </summary>
public interface IEmailService
{
    Task SendVerificationEmailAsync(string email, string verificationToken, CancellationToken ct = default);
    Task SendPasswordResetEmailAsync(string email, string resetToken, CancellationToken ct = default);
}
