namespace Retalon.DTOs.Products;

public class VendorProductImportRowDto
{
    public string? ExternalProductId { get; set; }

    public string? Name { get; set; }

    public string? Barcode { get; set; }

    public string? Description { get; set; }

    public string? ImageUrl { get; set; }

    public decimal? Price { get; set; }

    public string? Currency { get; set; }

    public string? Category { get; set; }

    public int? QuantityAvailable { get; set; }

    public int? SafetyStockLevel { get; set; }

    public int? ProcurementLeadTimeDays { get; set; }
}