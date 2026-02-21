using Core.Entities;
using Infrastructure.Data;
using Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly AppDbContext _db;
    private readonly ILogger<AuditService> _logger;

    public AuditService(AppDbContext db, ILogger<AuditService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task LogAsync(string action, string entityType, int? entityId = null,
        int? userId = null, string? details = null, string? ipAddress = null,
        CancellationToken ct = default)
    {
        var entry = new AuditLog
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            UserId = userId,
            Details = details,
            IpAddress = ipAddress,
            Timestamp = DateTime.UtcNow
        };

        _db.AuditLogs.Add(entry);
        await _db.SaveChangesAsync(ct);

        _logger.LogDebug("Audit: {Action} on {EntityType} {EntityId} by user {UserId}",
            action, entityType, entityId, userId);
    }

    public async Task<IReadOnlyList<AuditLog>> GetLogsAsync(int page = 1, int pageSize = 50,
        string? action = null, string? entityType = null, int? userId = null,
        CancellationToken ct = default)
    {
        var query = BuildQuery(action, entityType, userId);

        return await query
            .OrderByDescending(a => a.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<int> CountLogsAsync(string? action = null, string? entityType = null,
        int? userId = null, CancellationToken ct = default)
    {
        return await BuildQuery(action, entityType, userId).CountAsync(ct);
    }

    private IQueryable<AuditLog> BuildQuery(string? action, string? entityType, int? userId)
    {
        var query = _db.AuditLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(a => a.Action == action);
        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(a => a.EntityType == entityType);
        if (userId.HasValue)
            query = query.Where(a => a.UserId == userId.Value);

        return query;
    }
}
