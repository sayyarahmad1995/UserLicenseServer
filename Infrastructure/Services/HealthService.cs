using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;
using StackExchange.Redis;

namespace Infrastructure.Services;

public class HealthService
{
    private readonly IConfiguration _config;
    private readonly IConnectionMultiplexer _redis;
    public HealthService(IConfiguration config, IConnectionMultiplexer redis)
    {
        _config = config;
        _redis = redis;
    }

    /// <summary>
    /// Quick liveness check — verifies DB and Redis are reachable.
    /// Used by Docker HEALTHCHECK and container orchestrators.
    /// </summary>
    public async Task<LiveHealthResult> GetLiveHealthAsync(CancellationToken ct = default)
    {
        var result = new LiveHealthResult { Timestamp = DateTime.UtcNow };

        // Check PostgreSQL
        try
        {
            var connString = _config.GetConnectionString("DefaultConnection");
            if (!string.IsNullOrEmpty(connString))
            {
                await using var conn = new NpgsqlConnection(connString);
                await conn.OpenAsync(ct);
                await conn.ExecuteScalarAsync<int>("SELECT 1;");
                result.Database = "Healthy";
            }
            else
            {
                result.Database = "Not configured";
            }
        }
        catch
        {
            result.Database = "Unreachable";
        }

        // Check Redis
        try
        {
            var db = _redis.GetDatabase();
            var pong = await db.PingAsync();
            result.Redis = "Healthy";
            result.RedisLatencyMs = (int)pong.TotalMilliseconds;
        }
        catch
        {
            result.Redis = "Unreachable";
        }

        result.Status = (result.Database == "Healthy" && result.Redis == "Healthy")
            ? "Healthy"
            : "Degraded";

        return result;
    }

    public async Task<DatabaseHealthInfo> GetDatabaseHealthAsync(CancellationToken ct = default)
    {
        var info = new DatabaseHealthInfo();
        var connString = _config.GetConnectionString("DefaultConnection");

        if (string.IsNullOrEmpty(connString))
        {
            info.Status = "Unreachable";
            info.Size = "Connection string 'DefaultConnection' is not configured.";
            return info;
        }

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
            info.Size = "Error retrieving database size";
            _ = ex; // logged elsewhere if needed
        }

        return info;
    }
}
