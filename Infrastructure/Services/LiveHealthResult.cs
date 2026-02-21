namespace Infrastructure.Services;

/// <summary>
/// Lightweight health result for liveness probes (Docker, Kubernetes).
/// </summary>
public class LiveHealthResult
{
    public string Status { get; set; } = "Unknown";
    public string Database { get; set; } = "Unknown";
    public string Redis { get; set; } = "Unknown";
    public int RedisLatencyMs { get; set; }
    public DateTime Timestamp { get; set; }
}
