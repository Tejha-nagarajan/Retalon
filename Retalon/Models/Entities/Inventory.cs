namespace Retalon.Models.Entities;

public class Inventory
{
    public long InventoryId { get; set; }

    public long ProductId { get; set; }

    public int QuantityAvailable { get; set; }

    public int QuantityReserved { get; set; }

    public int SafetyStockLevel { get; set; }

    public int ProcurementLeadTimeDays { get; set; }

    public DateTime LastUpdated { get; set; }

    public Product Product { get; set; } = null!;
}