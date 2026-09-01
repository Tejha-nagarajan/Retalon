namespace Retalon.Models.Entities;

public class CartItem
{
    public long CartItemId { get; set; }

    public long CartId { get; set; }

    public long ProductId { get; set; }

    public int Quantity { get; set; }

    public DateTime AddedDate { get; set; }

    public Cart Cart { get; set; } = null!;

    public Product Product { get; set; } = null!;
}