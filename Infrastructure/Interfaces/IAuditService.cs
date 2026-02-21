using Core.Entities;

namespace Infrastructure.Interfaces;

/// <summary>
/// Service for recording and querying audit log entries.
/// </summary>
public interface IAuditService
{
    Task LogAsync(string action, string entityType, int? entityId = null, int? userId = null,
        string? details = null, string? ipAddress = null, CancellationToken ct = default);

    Task<IReadOnlyList<AuditLog>> GetLogsAsync(int page = 1, int pageSize = 50,
        string? action = null, string? entityType = null, int? userId = null,
        CancellationToken ct = default);

    Task<int> CountLogsAsync(string? action = null, string? entityType = null,
        int? userId = null, CancellationToken ct = default);
}
