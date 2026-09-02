namespace Retalon.DTOs.Products;

public class ProductResponseDto
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

    public string ProductStatus { get; set; } = string.Empty;
}