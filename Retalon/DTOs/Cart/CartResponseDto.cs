namespace Retalon.DTOs.Cart;

public class CartResponseDto
{
    public Guid CartId { get; set; }

    public List<CartItemResponseDto> Items { get; set; }
        = new();

    public decimal Total =>
        Items.Sum(item => item.Subtotal);
}