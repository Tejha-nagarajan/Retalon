using Retalon.Models.Enums;

namespace Retalon.Models.Entities;

public class Product
{
    public long ProductId { get; set; }

    public long CategoryId { get; set; }

    public string? ExternalProductId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Barcode { get; set; }

    public string? Description { get; set; }

    public string? ImageUrl { get; set; }

    public decimal Price { get; set; }

    public string Currency { get; set; } = "USD";

    public string? ImportSource { get; set; }

    public ProductStatus ProductStatus { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime? LastUpdated { get; set; }

    public DateTime? DeletedDate { get; set; }

    public Category Category { get; set; } = null!;

    public Inventory? Inventory { get; set; }

    public ICollection<CartItem> CartItems { get; set; }
        = new List<CartItem>();

    public ICollection<OrderItem> OrderItems { get; set; }
        = new List<OrderItem>();

    public ICollection<Procurement> Procurements { get; set; }
        = new List<Procurement>();
}