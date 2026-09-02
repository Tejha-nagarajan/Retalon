using Microsoft.EntityFrameworkCore;
using Retalon.Data;
using Retalon.DTOs.Inventory;
using Retalon.Models.Entities;
using Retalon.Services.Interfaces;

namespace Retalon.Services;

public class InventoryService : IInventoryService
{
    private readonly ApplicationDbContext _context;

    public InventoryService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<InventoryResponseDto?> GetByProductIdAsync(
        long productId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Inventories
            .AsNoTracking()
            .Where(i => i.ProductId == productId)
            .Select(i => new InventoryResponseDto
            {
                InventoryId = i.InventoryId,
                ProductId = i.ProductId,
                QuantityAvailable = i.QuantityAvailable,
                QuantityReserved = i.QuantityReserved,
                SafetyStockLevel = i.SafetyStockLevel,
                ProcurementLeadTimeDays = i.ProcurementLeadTimeDays,
                LastUpdated = i.LastUpdated
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<InventoryResponseDto?> UpdateAsync(
        long productId,
        int quantityAvailable,
        int quantityReserved,
        int safetyStockLevel,
        int procurementLeadTimeDays,
        CancellationToken cancellationToken = default)
    {
        if (quantityAvailable < 0 ||
            quantityReserved < 0 ||
            safetyStockLevel < 0 ||
            procurementLeadTimeDays < 0)
        {
            throw new ArgumentException(
                "Inventory values cannot be negative.");
        }

        if (quantityReserved > quantityAvailable)
        {
            throw new ArgumentException(
                "Quantity reserved cannot exceed quantity available.");
        }

        var inventory = await _context.Inventories
            .FirstOrDefaultAsync(
                i => i.ProductId == productId,
                cancellationToken);

        if (inventory == null)
        {
            var productExists = await _context.Products
                .AnyAsync(
                    p => p.ProductId == productId && !p.IsDeleted,
                    cancellationToken);

            if (!productExists)
            {
                return null;
            }

            inventory = new Inventory
            {
                ProductId = productId,
                QuantityAvailable = quantityAvailable,
                QuantityReserved = quantityReserved,
                SafetyStockLevel = safetyStockLevel,
                ProcurementLeadTimeDays = procurementLeadTimeDays,
                LastUpdated = DateTime.UtcNow
            };

            _context.Inventories.Add(inventory);
        }
        else
        {
            inventory.QuantityAvailable = quantityAvailable;
            inventory.QuantityReserved = quantityReserved;
            inventory.SafetyStockLevel = safetyStockLevel;
            inventory.ProcurementLeadTimeDays = procurementLeadTimeDays;
            inventory.LastUpdated = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return MapToDto(inventory);
    }

    public async Task<InventoryResponseDto?> RestockAsync(
        long productId,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        if (quantity <= 0)
        {
            throw new ArgumentException(
                "Restock quantity must be greater than zero.");
        }

        var inventory = await _context.Inventories
            .FirstOrDefaultAsync(
                i => i.ProductId == productId,
                cancellationToken);

        if (inventory == null)
        {
            return null;
        }

        inventory.QuantityAvailable += quantity;
        inventory.LastUpdated = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return MapToDto(inventory);
    }

    private static InventoryResponseDto MapToDto(
        Inventory inventory)
    {
        return new InventoryResponseDto
        {
            InventoryId = inventory.InventoryId,
            ProductId = inventory.ProductId,
            QuantityAvailable = inventory.QuantityAvailable,
            QuantityReserved = inventory.QuantityReserved,
            SafetyStockLevel = inventory.SafetyStockLevel,
            ProcurementLeadTimeDays =
                inventory.ProcurementLeadTimeDays,
            LastUpdated = inventory.LastUpdated
        };
    }
}