using Microsoft.EntityFrameworkCore;
using Retalon.Data;
using Retalon.DTOs.Procurement;
using Retalon.Models.Entities;
using Retalon.Models.Enums;
using Retalon.Services.Interfaces;

namespace Retalon.Services;

public class ProcurementService : IProcurementService
{
    private readonly ApplicationDbContext _context;

    public ProcurementService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ProcurementResponseDto>> CreateProcurementAsync(
        Guid userId,
        CreateProcurementRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(
                o => o.OrderId == request.OrderId &&
                     o.UserId == userId,
                cancellationToken);

        if (order == null)
            throw new ArgumentException("Order not found.");

        var procurements = new List<Procurement>();

        foreach (var item in order.OrderItems)
        {
            var inventory = await _context.Inventories
                .FirstOrDefaultAsync(
                    i => i.ProductId == item.ProductId,
                    cancellationToken);

            if (inventory == null)
                continue;

            var availableQuantity =
                inventory.QuantityAvailable - inventory.QuantityReserved;

            var shortage = item.Quantity - availableQuantity;

            if (shortage <= 0)
                continue;

            var existing = await _context.Procurements
                .FirstOrDefaultAsync(
                    p => p.OrderId == order.OrderId &&
                         p.ProductId == item.ProductId &&
                         p.ProcurementStatus != ProcurementStatus.Completed &&
                         p.ProcurementStatus != ProcurementStatus.Cancelled,
                    cancellationToken);

            if (existing != null)
            {
                procurements.Add(existing);
                continue;
            }

            var procurement = new Procurement
            {
                OrderId = order.OrderId,
                ProductId = item.ProductId,
                RequiredQuantity = shortage,
                ProcurementStatus = ProcurementStatus.Requested,
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            };

            _context.Procurements.Add(procurement);
            procurements.Add(procurement);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return procurements.Select(MapToDto).ToList();
    }

    public async Task<List<ProcurementResponseDto>> GetProcurementsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Procurements
            .Include(p => p.Order)
            .Where(p => p.Order.UserId == userId)
            .OrderByDescending(p => p.CreatedDate)
            .Select(p => new ProcurementResponseDto
            {
                ProcurementId = p.ProcurementId,
                OrderId = p.OrderId,
                ProductId = p.ProductId,
                RequiredQuantity = p.RequiredQuantity,
                ProcurementStatus = p.ProcurementStatus.ToString(),
                ExpectedArrivalDate = p.ExpectedArrivalDate,
                CreatedDate = p.CreatedDate,
                UpdatedDate = p.UpdatedDate
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<ProcurementResponseDto?> GetProcurementByIdAsync(
        Guid userId,
        long procurementId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Procurements
            .Include(p => p.Order)
            .Where(p => p.ProcurementId == procurementId &&
                        p.Order.UserId == userId)
            .Select(p => new ProcurementResponseDto
            {
                ProcurementId = p.ProcurementId,
                OrderId = p.OrderId,
                ProductId = p.ProductId,
                RequiredQuantity = p.RequiredQuantity,
                ProcurementStatus = p.ProcurementStatus.ToString(),
                ExpectedArrivalDate = p.ExpectedArrivalDate,
                CreatedDate = p.CreatedDate,
                UpdatedDate = p.UpdatedDate
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static ProcurementResponseDto MapToDto(Models.Entities.Procurement p)
    {
        return new ProcurementResponseDto
        {
            ProcurementId = p.ProcurementId,
            OrderId = p.OrderId,
            ProductId = p.ProductId,
            RequiredQuantity = p.RequiredQuantity,
            ProcurementStatus = p.ProcurementStatus.ToString(),
            ExpectedArrivalDate = p.ExpectedArrivalDate,
            CreatedDate = p.CreatedDate,
            UpdatedDate = p.UpdatedDate
        };
    }
    public async Task<ProcurementResponseDto?> UpdateProcurementStatusAsync(
    long procurementId,
    ProcurementStatus status,
    CancellationToken cancellationToken = default)
    {
        var procurement = await _context.Procurements
            .FirstOrDefaultAsync(
                p => p.ProcurementId == procurementId,
                cancellationToken);

        if (procurement == null)
            return null;

        procurement.ProcurementStatus = status;
        procurement.UpdatedDate = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return MapToDto(procurement);
    }
}