using Core.DTOs;
using Core.Interfaces;
using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;
using StackExchange.Redis;

namespace Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private readonly IConfiguration _config;
    private readonly IConnectionMultiplexer _redis;

    public DashboardService(IConfiguration config, IConnectionMultiplexer redis)
    {
        _config = config;
        _redis = redis;
    }

    public async Task<DashboardStatsDto> GetStatsAsync(CancellationToken ct = default)
    {
        var stats = new DashboardStatsDto();
        var connString = _config.GetConnectionString("DefaultConnection");

        if (string.IsNullOrEmpty(connString))
            return stats;

        await using var conn = new NpgsqlConnection(connString);
        await conn.OpenAsync(ct);

        // ── User stats ──
        var userRows = await conn.QueryAsync(
            """
            SELECT "Status", COUNT(*) AS count
            FROM "Users"
            GROUP BY "Status"
            """);

        foreach (var row in userRows)
        {
            int status = (int)row.Status;
            int count = (int)(long)row.count;
            string statusName = status switch
            {
                1 => "Verified",
                2 => "Unverified",
                3 => "Blocked",
                4 => "Active",
                _ => "Unknown"
            };
            stats.UsersByStatus[statusName] = count;
            stats.TotalUsers += count;
        }

        // ── New users (7d, 30d) ──
        var now = DateTime.UtcNow;
        stats.NewUsersLast7Days = (int)await conn.ExecuteScalarAsync<long>(
            """SELECT COUNT(*) FROM "Users" WHERE "CreatedAt" >= @since""",
            new { since = now.AddDays(-7) });

        stats.NewUsersLast30Days = (int)await conn.ExecuteScalarAsync<long>(
            """SELECT COUNT(*) FROM "Users" WHERE "CreatedAt" >= @since""",
            new { since = now.AddDays(-30) });

        // ── Active users (24h, 7d) — based on LastLogin ──
        stats.ActiveUsersLast24Hours = (int)await conn.ExecuteScalarAsync<long>(
            """SELECT COUNT(*) FROM "Users" WHERE "LastLogin" >= @since""",
            new { since = now.AddHours(-24) });

        stats.ActiveUsersLast7Days = (int)await conn.ExecuteScalarAsync<long>(
            """SELECT COUNT(*) FROM "Users" WHERE "LastLogin" >= @since""",
            new { since = now.AddDays(-7) });

        // ── License stats ──
        var licenseRows = await conn.QueryAsync(
            """
            SELECT "Status", COUNT(*) AS count
            FROM "Licenses"
            GROUP BY "Status"
            """);

        foreach (var row in licenseRows)
        {
            int status = (int)row.Status;
            int count = (int)(long)row.count;
            string statusName = status switch
            {
                1 => "Active",
                2 => "Expired",
                3 => "Revoked",
                _ => "Unknown"
            };
            stats.LicensesByStatus[statusName] = count;
            stats.TotalLicenses += count;
        }

        // ── Licenses expiring within 7 days ──
        stats.LicensesExpiringSoon = (int)await conn.ExecuteScalarAsync<long>(
            """
            SELECT COUNT(*)
            FROM "Licenses"
            WHERE "Status" = 1 AND "ExpiresAt" <= @threshold
            """,
            new { threshold = now.AddDays(7) });

        // ── New licenses (7d, 30d) ──
        stats.NewLicensesLast7Days = (int)await conn.ExecuteScalarAsync<long>(
            """SELECT COUNT(*) FROM "Licenses" WHERE "CreatedAt" >= @since""",
            new { since = now.AddDays(-7) });

        stats.NewLicensesLast30Days = (int)await conn.ExecuteScalarAsync<long>(
            """SELECT COUNT(*) FROM "Licenses" WHERE "CreatedAt" >= @since""",
            new { since = now.AddDays(-30) });

        // ── Database size ──
        stats.DatabaseSize = await conn.ExecuteScalarAsync<string>(
            "SELECT pg_size_pretty(pg_database_size(current_database()))") ?? "Unknown";

        // ── Active DB connections ──
        var connStats = await conn.QueryFirstOrDefaultAsync(
            """
            SELECT
                COALESCE(SUM(CASE WHEN state = 'active' THEN 1 ELSE 0 END), 0) AS active
            FROM pg_stat_activity
            WHERE datname = current_database()
            """);
        stats.ActiveDbConnections = connStats != null ? (int)connStats.active : 0;

        // ── Cache hit ratio ──
        var dbStats = await conn.QueryFirstOrDefaultAsync(
            """
            SELECT blks_hit, blks_read
            FROM pg_stat_database
            WHERE datname = current_database()
            """);
        if (dbStats != null)
        {
            double hit = (double)dbStats.blks_hit;
            double read = (double)dbStats.blks_read;
            stats.CacheHitRatio = (hit + read) > 0 ? Math.Round(hit / (hit + read), 4) : 0;
        }

        stats.Timestamp = DateTime.UtcNow;
        return stats;
    }
}
