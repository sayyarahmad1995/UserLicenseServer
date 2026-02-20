namespace Core.Helpers;

public class ThrottlingSettings
{
    public ThrottleTier Global { get; set; } = new();
    public ThrottleTier Auth { get; set; } = new();
    public ThrottleTier User { get; set; } = new();
}

public class ThrottleTier
{
    public int ThrottleThreshold { get; set; }
    public int MaxRequestsPerMinute { get; set; }
    public int WindowSeconds { get; set; }
    public int MaxDelayMs { get; set; }
}