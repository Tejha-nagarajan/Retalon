namespace Retalon.Models.Entities;

public class User
{
    public Guid UserId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string PostalCode { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public int FailedLoginAttempts { get; set; }

    public DateTime? LockedUntil { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime? LastLoginDate { get; set; }

    public ICollection<UserRole> UserRoles { get; set; }
        = new List<UserRole>();

    public ICollection<RefreshToken> RefreshTokens { get; set; }
        = new List<RefreshToken>();

    public ICollection<SecurityEvent> SecurityEvents { get; set; }
        = new List<SecurityEvent>();

    public ICollection<Notification> Notifications { get; set; }
        = new List<Notification>();

    public ICollection<AuditLog> AuditLogs { get; set; }
        = new List<AuditLog>();

    public ICollection<SearchHistory> SearchHistories { get; set; }
        = new List<SearchHistory>();

    public ICollection<Cart> Carts { get; set; }
        = new List<Cart>();

    public ICollection<Order> Orders { get; set; }
        = new List<Order>();
}