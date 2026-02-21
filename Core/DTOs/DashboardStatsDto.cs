namespace Core.DTOs;

public class DashboardStatsDto
{
    // ── Users ──
    public int TotalUsers { get; set; }
    public Dictionary<string, int> UsersByStatus { get; set; } = new();
    public int NewUsersLast7Days { get; set; }
    public int NewUsersLast30Days { get; set; }

    // ── Licenses ──
    public int TotalLicenses { get; set; }
    public Dictionary<string, int> LicensesByStatus { get; set; } = new();
    public int LicensesExpiringSoon { get; set; }
    public int NewLicensesLast7Days { get; set; }
    public int NewLicensesLast30Days { get; set; }

    // ── Activity ──
    public int ActiveUsersLast24Hours { get; set; }
    public int ActiveUsersLast7Days { get; set; }

    // ── System ──
    public string DatabaseSize { get; set; } = string.Empty;
    public int ActiveDbConnections { get; set; }
    public double CacheHitRatio { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
