namespace Retalon.DTOs.Inventory;

public class InventoryResponseDto
{
    public long InventoryId { get; set; }
    public long ProductId { get; set; }

    public int QuantityAvailable { get; set; }
    public int QuantityReserved { get; set; }
    public int SafetyStockLevel { get; set; }

    public int ProcurementLeadTimeDays { get; set; }

    public int QuantityAfterReservation =>
        QuantityAvailable - QuantityReserved;

    public bool IsLowStock =>
        QuantityAfterReservation <= SafetyStockLevel;

    public DateTime LastUpdated { get; set; }
}