namespace Retalon.DTOs.Orders;

public class OrderItemResponseDto
{
    public long OrderItemId { get; set; }
    public long ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal => UnitPrice * Quantity;
    public int DeliveryDays { get; set; }
}