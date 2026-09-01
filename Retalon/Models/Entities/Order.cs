using Retalon.Models.Enums;

namespace Retalon.Models.Entities;

public class Order
{
    public long OrderId { get; set; }

    public Guid UserId { get; set; }

    public OrderStatus OrderStatus { get; set; }

    public decimal TotalAmount { get; set; }

    public DateTime ExpectedDeliveryDate { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public User User { get; set; } = null!;

    public ICollection<OrderItem> OrderItems { get; set; }
        = new List<OrderItem>();

    public ICollection<Payment> Payments { get; set; }
        = new List<Payment>();

    public ICollection<Procurement> Procurements { get; set; }
        = new List<Procurement>();
}