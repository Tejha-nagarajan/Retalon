using Microsoft.EntityFrameworkCore;
using Retalon.Data;
using Retalon.DTOs.Orders;
using Retalon.Models.Entities;
using Retalon.Models.Enums;
using Retalon.Services.Interfaces;

namespace Retalon.Services;

public class OrderService : IOrderService
{
    private readonly ApplicationDbContext _context;

    public OrderService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CreateOrderResponseDto?> CreateOrderAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var cart = await _context.Carts
            .Include(c => c.CartItems)
            .ThenInclude(ci => ci.Product)
            .FirstOrDefaultAsync(
                c => c.UserId == userId,
                cancellationToken);

        if (cart == null || !cart.CartItems.Any())
        {
            return null;
        }

        var productIds = cart.CartItems
            .Select(ci => ci.ProductId)
            .ToList();

        var inventories = await _context.Inventories
            .Where(i => productIds.Contains(i.ProductId))
            .ToDictionaryAsync(
                i => i.ProductId,
                cancellationToken);

        decimal totalAmount = 0m;
        var orderItems = new List<OrderItem>();
        var maxDeliveryDays = 0;

        foreach (var cartItem in cart.CartItems)
        {
            var product = cartItem.Product;

            if (product == null ||
                product.IsDeleted ||
                product.ProductStatus != ProductStatus.Active)
            {
                throw new InvalidOperationException(
                    $"Product {cartItem.ProductId} is unavailable.");
            }

            if (!inventories.TryGetValue(
                    cartItem.ProductId,
                    out var inventory))
            {
                throw new InvalidOperationException(
                    $"Inventory is not available for product " +
                    $"{cartItem.ProductId}.");
            }

            var availableQuantity =
                inventory.QuantityAvailable -
                inventory.QuantityReserved;

            if (cartItem.Quantity > availableQuantity)
            {
                throw new InvalidOperationException(
                    $"Insufficient inventory for product " +
                    $"{cartItem.ProductId}. " +
                    $"Only {availableQuantity} units are available.");
            }

            var unitPrice = product.Price;
            var subtotal = unitPrice * cartItem.Quantity;

            var deliveryDays =
                CalculateDeliveryDays(
                    inventory,
                    cartItem.Quantity);

            if (deliveryDays > maxDeliveryDays)
            {
                maxDeliveryDays = deliveryDays;
            }

            totalAmount += subtotal;

            orderItems.Add(new OrderItem
            {
                ProductId = product.ProductId,
                Quantity = cartItem.Quantity,
                UnitPrice = unitPrice,
                DeliveryDays = deliveryDays
            });
        }

        var order = new Order
        {
            UserId = userId,
            OrderStatus = OrderStatus.Pending,
            TotalAmount = totalAmount,
            ExpectedDeliveryDate =
                DateTime.UtcNow.Date.AddDays(maxDeliveryDays),
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow,
            OrderItems = orderItems
        };

        _context.Orders.Add(order);

        _context.CartItems.RemoveRange(cart.CartItems);

        await _context.SaveChangesAsync(cancellationToken);

        return new CreateOrderResponseDto
        {
            OrderId = order.OrderId,
            OrderStatus = order.OrderStatus.ToString(),
            TotalAmount = order.TotalAmount,
            ExpectedDeliveryDate = order.ExpectedDeliveryDate
        };
    }

    public async Task<List<OrderResponseDto>> GetOrdersAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .AsNoTracking()
            .Where(o => o.UserId == userId)
            .Include(o => o.OrderItems)
            .ThenInclude(oi => oi.Product)
            .OrderByDescending(o => o.CreatedDate)
            .Select(o => MapToDto(o))
            .ToListAsync(cancellationToken);
    }

    public async Task<OrderResponseDto?> GetOrderByIdAsync(
        Guid userId,
        long orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await _context.Orders
            .AsNoTracking()
            .Include(o => o.OrderItems)
            .ThenInclude(oi => oi.Product)
            .FirstOrDefaultAsync(
                o => o.OrderId == orderId &&
                     o.UserId == userId,
                cancellationToken);

        return order == null
            ? null
            : MapToDto(order);
    }

    private static int CalculateDeliveryDays(
        Inventory inventory,
        int quantity)
    {
        var availableAfterReservation =
            inventory.QuantityAvailable -
            inventory.QuantityReserved;

        // Normal stock: standard delivery.
        if (availableAfterReservation >= quantity &&
            availableAfterReservation > inventory.SafetyStockLevel)
        {
            return 2;
        }

        // Stock is at or below safety stock:
        // use procurement lead time.
        return Math.Max(
            2,
            inventory.ProcurementLeadTimeDays + 2);
    }

    private static OrderResponseDto MapToDto(Order order)
    {
        return new OrderResponseDto
        {
            OrderId = order.OrderId,
            UserId = order.UserId,
            OrderStatus = order.OrderStatus.ToString(),
            TotalAmount = order.TotalAmount,
            ExpectedDeliveryDate =
                order.ExpectedDeliveryDate,
            CreatedDate = order.CreatedDate,

            Items = order.OrderItems
                .Select(item => new OrderItemResponseDto
                {
                    OrderItemId = item.OrderItemId,
                    ProductId = item.ProductId,
                    ProductName = item.Product.Name,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    DeliveryDays = item.DeliveryDays
                })
                .ToList()
        };
    }
}