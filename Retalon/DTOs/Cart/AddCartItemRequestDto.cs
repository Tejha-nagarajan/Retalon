using System.ComponentModel.DataAnnotations;

namespace Retalon.DTOs.Cart;

public class AddCartItemRequestDto
{
    [Range(1, long.MaxValue)]
    public long ProductId { get; set; }

    [Range(1, 100)]
    public int Quantity { get; set; }
}