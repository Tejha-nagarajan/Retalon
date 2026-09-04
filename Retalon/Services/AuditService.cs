using Retalon.Data;
using Retalon.Models.Entities;
using Retalon.Services.Interfaces;

namespace Retalon.Services;

public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _context;

    public AuditService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task LogAsync(
        Guid? userId,
        string action,
        string? entityName = null,
        string? entityId = null,
        string? details = null,
        CancellationToken cancellationToken = default)
    {
        var auditLog = new AuditLog
        {
            UserId = userId,
            PerformedByUserId = userId,
            Action = action,
            EntityName = entityName ?? string.Empty,
            NewValue = details,
            CorrelationId = null,
            RequestId = null,
            IpAddress = null,
            Timestamp = DateTime.UtcNow
        };

        _context.auditLogs.Add(auditLog);

        await _context.SaveChangesAsync(cancellationToken);
    }
}