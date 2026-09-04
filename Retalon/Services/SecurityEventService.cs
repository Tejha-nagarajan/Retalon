using Retalon.Data;
using Retalon.Models.Entities;
using Retalon.Models.Enums;
using Retalon.Services.Interfaces;

namespace Retalon.Services;

public class SecurityEventService : ISecurityEventService
{
    private readonly ApplicationDbContext _context;

    public SecurityEventService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task LogAsync(
        Guid? userId,
        SecurityEventType securityEventType,
        string description,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        var securityEvent = new SecurityEvent
        {
            UserId = userId,
            SecurityEventType = securityEventType,
            Description = description,
            IpAddress = ipAddress,
            CreatedDate = DateTime.UtcNow
        };

        _context.SecurityEvents.Add(securityEvent);

        await _context.SaveChangesAsync(cancellationToken);
    }
}