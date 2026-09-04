namespace Retalon.Services.Interfaces;

public interface IAuditService
{
    Task LogAsync(
        Guid? userId,
        string action,
        string? entityName = null,
        string? entityId = null,
        string? details = null,
        CancellationToken cancellationToken = default);
}