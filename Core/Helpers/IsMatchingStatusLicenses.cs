using Core.Enums;

namespace Core.Helpers;

public static class LicenseStatusHelper
{
    private static readonly Dictionary<string, LicenseStatus> _statusMap =
       new(StringComparer.OrdinalIgnoreCase)
       {
         { "active", LicenseStatus.Active },
         { "expired", LicenseStatus.Expired },
         { "revoked", LicenseStatus.Revoked }
       };

    /// <summary>
    /// Try to parse a string into a LicenseStatus enum.
    /// Returns null if the value is invalid.
    /// </summary>
    public static LicenseStatus? Parse(string? status)
    {
        if (string.IsNullOrEmpty(status))
            return null;

        return _statusMap.TryGetValue(status, out var target) ? target : null;
    }

    /// <summary>
    /// Returns all valid status keys, e.g. ["active", "expired", "revoked"].
    /// </summary>
    public static IReadOnlyCollection<string> ValidStatuses => _statusMap.Keys;
}
