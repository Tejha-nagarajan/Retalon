namespace Retalon.Models.Entities;

public class RefreshToken
{
    public Guid RefreshTokenId { get; set; }

    public Guid UserId { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTime CreatedDate { get; set; }

    public DateTime ExpiryDate { get; set; }

    public DateTime? RevokedDate { get; set; }

    public string? CreatedByIp { get; set; }

    public User User { get; set; } = null!;
}