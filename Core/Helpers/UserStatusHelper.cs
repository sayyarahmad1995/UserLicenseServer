using Core.Enums;

namespace Core.Helpers;

public static class UserStatusHelper
{
    private static readonly Dictionary<string, UserStatus> _statusMap =
       new(StringComparer.OrdinalIgnoreCase)
       {
         { "unverified", UserStatus.Unverified },
         { "verified", UserStatus.Verified },
         { "blocked", UserStatus.Blocked },
         { "active", UserStatus.Active },
       };

    /// <summary>
    /// Try to parse a string into a UserStatus enum.
    /// Returns null if the value is invalid.
    /// </summary>
    public static UserStatus? Parse(string? status)
    {
        if (string.IsNullOrEmpty(status))
            return null;

        return _statusMap.TryGetValue(status, out var target) ? target : null;
    }

    /// <summary>
    /// Returns all valid status keys, e.g. ["verified", "unverified", "active"].
    /// </summary>
    public static IReadOnlyCollection<string> ValidStatuses => _statusMap.Keys;
}
