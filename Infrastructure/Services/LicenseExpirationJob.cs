using Core.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

/// <summary>
/// Background service that periodically checks for expired licenses and updates their status.
/// Runs every hour by default.
/// </summary>
public class LicenseExpirationJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LicenseExpirationJob> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromHours(1);

    public LicenseExpirationJob(IServiceScopeFactory scopeFactory, ILogger<LicenseExpirationJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("LicenseExpirationJob started. Running every {Interval}", _interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExpiredLicensesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing expired licenses");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task ProcessExpiredLicensesAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTime.UtcNow;
        var expiredLicenses = await db.Licenses
            .Where(l => l.Status == LicenseStatus.Active && l.ExpiresAt <= now)
            .ToListAsync(ct);

        if (expiredLicenses.Count == 0)
        {
            _logger.LogDebug("No expired licenses found");
            return;
        }

        foreach (var license in expiredLicenses)
        {
            license.Status = LicenseStatus.Expired;
        }

        var count = await db.SaveChangesAsync(ct);
        _logger.LogInformation("Marked {Count} licenses as expired", count);
    }
}
