using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Infrastructure.Services;

public class HealthService
{
    private readonly IConfiguration _config;
    public HealthService(IConfiguration config) => _config = config;

    public async Task<DatabaseHealthInfo> GetDatabaseHealthAsync(CancellationToken ct = default)
    {
        var info = new DatabaseHealthInfo();
        var connString = _config.GetConnectionString("DefaultConnection");

        try
        {
            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync(ct);

            info.Status = "Healthy";

            var connectionStats = await conn.QueryFirstAsync(@"
            SELECT
                COALESCE(sum(CASE WHEN state = 'active' THEN 1 ELSE 0 END), 0) AS active,
                COALESCE(sum(CASE WHEN state = 'idle' THEN 1 ELSE 0 END), 0) AS idle,
                count(*) AS total
            FROM pg_stat_activity
            WHERE datname = current_database();
			");
            info.Connections.Active = (int)connectionStats.active;
            info.Connections.Idle = (int)connectionStats.idle;
            info.Connections.Total = (int)connectionStats.total;

            // Cache + transactions
            var dbStats = await conn.QueryFirstOrDefaultAsync(@"
            SELECT
                xact_commit, xact_rollback,
                blks_hit, blks_read,
                EXTRACT(EPOCH FROM (now() - COALESCE(stats_reset, now()))) AS uptime
            FROM pg_stat_database
            WHERE datname = current_database();
			");

            if (dbStats != null)
            {
                double hit = (double)dbStats.blks_hit;
                double read = (double)dbStats.blks_read;
                info.CacheHitRatio = (hit + read) > 0 ? hit / (hit + read) : 0;
                info.Transactions.Committed = (long)dbStats.xact_commit;
                info.Transactions.RolledBack = (long)dbStats.xact_rollback;
                info.UptimeSeconds = (long)dbStats.uptime;
            }

            // Database size
            var size = await conn.ExecuteScalarAsync<string>(@"
            SELECT pg_size_pretty(pg_database_size(current_database()));
			");
            info.Size = size ?? "Unknown";
        }
        catch (Exception ex)
        {
            info.Status = "Unreachable";
            info.Size = ex.Message; // optional: include minimal context during debug
        }

        return info;
    }
}
