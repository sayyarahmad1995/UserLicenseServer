using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Prometheus;

namespace Infrastructure.Services;

/// <summary>
/// Background service that periodically collects business-level metrics
/// and exposes them as Prometheus gauges.
/// </summary>
public class AppMetricsService : BackgroundService
{
    private readonly IConfiguration _config;
    private readonly ILogger<AppMetricsService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(30);

    // ── User gauges ──
    private static readonly Gauge UsersTotal = Metrics.CreateGauge(
        "eazecad_users_total", "Total number of registered users");

    private static readonly Gauge UsersByStatus = Metrics.CreateGauge(
        "eazecad_users_by_status", "Number of users by status",
        new GaugeConfiguration { LabelNames = new[] { "status" } });

    // ── License gauges ──
    private static readonly Gauge LicensesTotal = Metrics.CreateGauge(
        "eazecad_licenses_total", "Total number of licenses");

    private static readonly Gauge LicensesByStatus = Metrics.CreateGauge(
        "eazecad_licenses_by_status", "Number of licenses by status",
        new GaugeConfiguration { LabelNames = new[] { "status" } });

    // ── License expiration gauges ──
    private static readonly Gauge LicensesExpiringSoon = Metrics.CreateGauge(
        "eazecad_licenses_expiring_within_7d", "Licenses expiring within 7 days");

    // ── Database gauges ──
    private static readonly Gauge DbConnectionsActive = Metrics.CreateGauge(
        "eazecad_db_connections_active", "Active database connections");

    private static readonly Gauge DbConnectionsTotal = Metrics.CreateGauge(
        "eazecad_db_connections_total", "Total database connections");

    private static readonly Gauge DbSizeBytes = Metrics.CreateGauge(
        "eazecad_db_size_bytes", "Database size in bytes");

    // ── Request counters (incremented by middleware) ──
    public static readonly Counter HttpRequestsTotal = Metrics.CreateCounter(
        "eazecad_http_requests_total", "Total HTTP requests processed",
        new CounterConfiguration { LabelNames = new[] { "method", "endpoint", "status_code" } });

    public static readonly Histogram HttpRequestDuration = Metrics.CreateHistogram(
        "eazecad_http_request_duration_seconds", "HTTP request duration in seconds",
        new HistogramConfiguration
        {
            LabelNames = new[] { "method", "endpoint" },
            Buckets = new[] { 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1.0, 2.5, 5.0, 10.0 }
        });

    // ── Auth counters ──
    public static readonly Counter AuthLoginsTotal = Metrics.CreateCounter(
        "eazecad_auth_logins_total", "Total login attempts",
        new CounterConfiguration { LabelNames = new[] { "result" } });

    public static readonly Counter AuthTokenRefreshTotal = Metrics.CreateCounter(
        "eazecad_auth_token_refresh_total", "Total token refresh attempts",
        new CounterConfiguration { LabelNames = new[] { "result" } });

    public AppMetricsService(IConfiguration config, ILogger<AppMetricsService> logger)
    {
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AppMetricsService started — collecting every {Interval}s", _interval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CollectMetricsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to collect application metrics");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task CollectMetricsAsync(CancellationToken ct)
    {
        var connString = _config.GetConnectionString("DefaultConnection");
        if (string.IsNullOrEmpty(connString)) return;

        await using var conn = new NpgsqlConnection(connString);
        await conn.OpenAsync(ct);

        // ── User metrics ──
        var userStats = await conn.QueryAsync(
            """
            SELECT "Status", COUNT(*) AS count
            FROM "Users"
            GROUP BY "Status"
            """);

        long totalUsers = 0;
        foreach (var row in userStats)
        {
            int status = (int)row.Status;
            long count = (long)row.count;
            string statusName = status switch
            {
                1 => "Verified",
                2 => "Unverified",
                3 => "Blocked",
                4 => "Active",
                _ => "Unknown"
            };
            UsersByStatus.WithLabels(statusName).Set(count);
            totalUsers += count;
        }
        UsersTotal.Set(totalUsers);

        // ── License metrics ──
        var licenseStats = await conn.QueryAsync(
            """
            SELECT "Status", COUNT(*) AS count
            FROM "Licenses"
            GROUP BY "Status"
            """);

        long totalLicenses = 0;
        foreach (var row in licenseStats)
        {
            int status = (int)row.Status;
            long count = (long)row.count;
            string statusName = status switch
            {
                1 => "Active",
                2 => "Expired",
                3 => "Revoked",
                _ => "Unknown"
            };
            LicensesByStatus.WithLabels(statusName).Set(count);
            totalLicenses += count;
        }
        LicensesTotal.Set(totalLicenses);

        // ── Licenses expiring soon ──
        var expiringSoon = await conn.ExecuteScalarAsync<long>(
            """
            SELECT COUNT(*)
            FROM "Licenses"
            WHERE "Status" = 1
              AND "ExpiresAt" <= @threshold
            """,
            new { threshold = DateTime.UtcNow.AddDays(7) });
        LicensesExpiringSoon.Set(expiringSoon);

        // ── Database connection stats ──
        var connStats = await conn.QueryFirstOrDefaultAsync(
            """
            SELECT
                COALESCE(SUM(CASE WHEN state = 'active' THEN 1 ELSE 0 END), 0) AS active,
                COUNT(*) AS total
            FROM pg_stat_activity
            WHERE datname = current_database()
            """);

        if (connStats != null)
        {
            DbConnectionsActive.Set((double)(int)connStats.active);
            DbConnectionsTotal.Set((double)(int)connStats.total);
        }

        // ── Database size ──
        var sizeBytes = await conn.ExecuteScalarAsync<long>(
            "SELECT pg_database_size(current_database())");
        DbSizeBytes.Set(sizeBytes);
    }
}
