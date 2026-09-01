namespace Retalon.Models.Entities;

public class Cart
{
    public long SearchHistoryId { get; set; }

    public Guid UserId { get; set; }

    public string SearchTerm { get; set; } = string.Empty;

    public bool FoundLocally { get; set; }

    public DateTime SearchDate { get; set; }

    public User User { get; set; } = null!;
}