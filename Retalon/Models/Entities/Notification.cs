using Retalon.Models.Enums;

namespace Retalon.Models.Entities;

public class Notification
{
    public long NotificationId { get; set; }

    public Guid UserId { get; set; }

    public NotificationType NotificationType { get; set; }

    public string Message { get; set; } = string.Empty;

    public NotificationStatus NotificationStatus { get; set; }

    public DateTime? SentDate { get; set; }

    public DateTime CreatedDate { get; set; }

    public User User { get; set; } = null!;
}