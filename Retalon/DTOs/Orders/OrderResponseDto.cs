namespace Retalon.DTOs.Orders;

public class OrderResponseDto
{
    public long OrderId { get; set; }
    public Guid UserId { get; set; }
    public string OrderStatus { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTime ExpectedDeliveryDate { get; set; }
    public DateTime CreatedDate { get; set; }

    public List<OrderItemResponseDto> Items { get; set; }
        = new();
}