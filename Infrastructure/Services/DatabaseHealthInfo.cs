namespace Infrastructure.Services;

public class DatabaseHealthInfo
{
    public string Status { get; set; } = "Unknown";
    public ConnectionStats Connections { get; set; } = new();
    public double CacheHitRatio { get; set; }
    public TransactionStats Transactions { get; set; } = new();
    public long UptimeSeconds { get; set; }
    public string Size { get; set; } = string.Empty;

    public class ConnectionStats
    {
        public int Active { get; set; }
        public int Idle { get; set; }
        public int Total { get; set; }
    }

    public class TransactionStats
    {
        public long Committed { get; set; }
        public long RolledBack { get; set; }
    }
}
