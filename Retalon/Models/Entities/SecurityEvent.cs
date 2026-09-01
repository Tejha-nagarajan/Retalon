using Retalon.Models.Enums;

namespace Retalon.Models.Entities;

public class SecurityEvent
{
    public long SecurityEventId { get; set; }

    public Guid? UserId { get; set; }

    public SecurityEventType SecurityEventType { get; set; }

    public string Description { get; set; } = string.Empty;

    public string? IpAddress { get; set; }

    public DateTime CreatedDate { get; set; }

    public User? User { get; set; }
}