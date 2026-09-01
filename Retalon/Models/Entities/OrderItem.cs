namespace Retalon.Models.Entities;

public class OrderItem
{
    public long OrderItemId { get; set; }

    public long OrderId { get; set; }

    public long ProductId { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public int DeliveryDays { get; set; }

    public Order Order { get; set; } = null!;

    public Product Product { get; set; } = null!;
}