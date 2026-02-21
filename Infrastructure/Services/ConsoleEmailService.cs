using Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

/// <summary>
/// Development-only email service that logs emails to the console/logger.
/// Replace with SMTP / SendGrid / SES implementation for production.
/// </summary>
public class ConsoleEmailService : IEmailService
{
    private readonly ILogger<ConsoleEmailService> _logger;

    public ConsoleEmailService(ILogger<ConsoleEmailService> logger)
    {
        _logger = logger;
    }

    public Task SendVerificationEmailAsync(string email, string verificationToken, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[EMAIL] Verification email to {Email} — Token: {Token} — Link: /api/v1/auth/verify-email?token={Token}",
            email, verificationToken, verificationToken);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetEmailAsync(string email, string resetToken, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[EMAIL] Password reset email to {Email} — Token: {Token}",
            email, resetToken);
        return Task.CompletedTask;
    }
}
