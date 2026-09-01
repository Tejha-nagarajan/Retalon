namespace Retalon.Models.Entities;

public class AuditLog
{
    public long AuditLogId { get; set; }

    public Guid? UserId { get; set; }

    public Guid? PerformedByUserId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string EntityName { get; set; } = string.Empty;

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public string? IpAddress { get; set; }

    public string? CorrelationId { get; set; }

    public string? RequestId { get; set; }

    public DateTime Timestamp { get; set; }

    public User? User { get; set; }
}