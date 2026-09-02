using Retalon.DTOs.Inventory;

namespace Retalon.Services.Interfaces;

public interface IInventoryService
{
    Task<InventoryResponseDto?> GetByProductIdAsync(
        long productId,
        CancellationToken cancellationToken = default);

    Task<InventoryResponseDto?> UpdateAsync(
        long productId,
        int quantityAvailable,
        int quantityReserved,
        int safetyStockLevel,
        int procurementLeadTimeDays,
        CancellationToken cancellationToken = default);

    Task<InventoryResponseDto?> RestockAsync(
        long productId,
        int quantity,
        CancellationToken cancellationToken = default);
}