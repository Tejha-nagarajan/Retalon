namespace Retalon.DTOs.Orders;

public class CreateOrderResponseDto
{
    public long OrderId { get; set; }
    public string OrderStatus { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTime ExpectedDeliveryDate { get; set; }
}