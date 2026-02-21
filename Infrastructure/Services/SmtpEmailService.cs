using System.Net;
using System.Net.Mail;
using Core.Helpers;
using Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services;

/// <summary>
/// Production SMTP email service that sends real emails via System.Net.Mail.
/// Configure via the "Email" section in appsettings.json.
/// </summary>
public class SmtpEmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IOptions<EmailSettings> options, ILogger<SmtpEmailService> logger)
    {
        _settings = options.Value;
        _logger = logger;
    }

    public async Task SendVerificationEmailAsync(string email, string verificationToken, CancellationToken ct = default)
    {
        var verifyUrl = $"{_settings.FrontendBaseUrl.TrimEnd('/')}/verify-email?token={Uri.EscapeDataString(verificationToken)}";

        var subject = "Verify your email address";
        var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background: #f4f4f7; margin: 0; padding: 40px 0; }}
        .container {{ max-width: 560px; margin: 0 auto; background: #fff; border-radius: 8px; box-shadow: 0 2px 8px rgba(0,0,0,0.08); overflow: hidden; }}
        .header {{ background: #2563eb; padding: 32px; text-align: center; }}
        .header h1 {{ color: #fff; margin: 0; font-size: 22px; }}
        .body {{ padding: 32px; color: #333; line-height: 1.6; }}
        .btn {{ display: inline-block; background: #2563eb; color: #fff; padding: 14px 32px; border-radius: 6px; text-decoration: none; font-color: #000; font-weight: 600; margin: 20px 0; }}
        .footer {{ padding: 20px 32px; background: #f9fafb; color: #888; font-size: 13px; text-align: center; }}
        .token {{ background: #f1f5f9; padding: 10px 16px; border-radius: 4px; font-family: monospace; font-size: 14px; word-break: break-all; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>{WebUtility.HtmlEncode(_settings.FromName)}</h1>
        </div>
        <div class=""body"">
            <h2>Verify your email</h2>
            <p>Thanks for signing up! Please verify your email address by clicking the button below:</p>
            <p style=""text-align: center;"">
                <a href=""{WebUtility.HtmlEncode(verifyUrl)}"" class=""btn"">Verify Email</a>
            </p>
            <p>Or copy and paste this link into your browser:</p>
            <p class=""token"">{WebUtility.HtmlEncode(verifyUrl)}</p>
            <p>This link expires in <strong>24 hours</strong>.</p>
            <p>If you didn't create an account, you can safely ignore this email.</p>
        </div>
        <div class=""footer"">
            &copy; {DateTime.UtcNow.Year} {WebUtility.HtmlEncode(_settings.FromName)}. All rights reserved.
        </div>
    </div>
</body>
</html>";

        await SendEmailAsync(email, subject, htmlBody, ct);
    }

    public async Task SendPasswordResetEmailAsync(string email, string resetToken, CancellationToken ct = default)
    {
        var resetUrl = $"{_settings.FrontendBaseUrl.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(resetToken)}";

        var subject = "Reset your password";
        var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background: #f4f4f7; margin: 0; padding: 40px 0; }}
        .container {{ max-width: 560px; margin: 0 auto; background: #fff; border-radius: 8px; box-shadow: 0 2px 8px rgba(0,0,0,0.08); overflow: hidden; }}
        .header {{ background: #2563eb; padding: 32px; text-align: center; }}
        .header h1 {{ color: #fff; margin: 0; font-size: 22px; }}
        .body {{ padding: 32px; color: #333; line-height: 1.6; }}
        .btn {{ display: inline-block; background: #dc2626; color: #fff; padding: 14px 32px; border-radius: 6px; text-decoration: none; font-weight: 600; margin: 20px 0; }}
        .footer {{ padding: 20px 32px; background: #f9fafb; color: #888; font-size: 13px; text-align: center; }}
        .token {{ background: #f1f5f9; padding: 10px 16px; border-radius: 4px; font-family: monospace; font-size: 14px; word-break: break-all; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>{WebUtility.HtmlEncode(_settings.FromName)}</h1>
        </div>
        <div class=""body"">
            <h2>Reset your password</h2>
            <p>We received a request to reset your password. Click the button below to choose a new password:</p>
            <p style=""text-align: center;"">
                <a href=""{WebUtility.HtmlEncode(resetUrl)}"" class=""btn"">Reset Password</a>
            </p>
            <p>Or copy and paste this link into your browser:</p>
            <p class=""token"">{WebUtility.HtmlEncode(resetUrl)}</p>
            <p>This link expires in <strong>1 hour</strong>.</p>
            <p>If you didn't request a password reset, you can safely ignore this email. Your password will remain unchanged.</p>
        </div>
        <div class=""footer"">
            &copy; {DateTime.UtcNow.Year} {WebUtility.HtmlEncode(_settings.FromName)}. All rights reserved.
        </div>
    </div>
</body>
</html>";

        await SendEmailAsync(email, subject, htmlBody, ct);
    }

    private async Task SendEmailAsync(string to, string subject, string htmlBody, CancellationToken ct)
    {
        try
        {
            using var message = new MailMessage();
            message.From = new MailAddress(_settings.FromEmail, _settings.FromName);
            message.To.Add(new MailAddress(to));
            message.Subject = subject;
            message.Body = htmlBody;
            message.IsBodyHtml = true;

            using var client = new SmtpClient(_settings.SmtpHost, _settings.SmtpPort);
            client.Credentials = new NetworkCredential(_settings.SmtpUser, _settings.SmtpPass);
            client.EnableSsl = _settings.EnableSsl;

            await client.SendMailAsync(message, ct);

            _logger.LogInformation("Email sent to {To} — Subject: {Subject}", to, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To} — Subject: {Subject}", to, subject);
            throw;
        }
    }
}
