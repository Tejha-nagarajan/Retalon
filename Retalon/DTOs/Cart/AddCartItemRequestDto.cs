namespace Retalon.DTOs.Cart;

public class AddCartItemRequestDto
{
    public long ProductId { get; set; }
    public int Quantity { get; set; }
}