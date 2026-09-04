using Retalon.Models.Enums;

namespace Retalon.Services.Interfaces;

public interface ISecurityEventService
{
    Task LogAsync(
        Guid? userId,
        SecurityEventType securityEventType,
        string description,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);
}