namespace Retalon.Models.Entities;

public class Cart
{
    public Guid CartId { get; set; }

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public ICollection<CartItem> CartItems { get; set; }
        = new List<CartItem>();
}