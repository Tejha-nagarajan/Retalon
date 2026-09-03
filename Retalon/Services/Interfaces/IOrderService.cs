using Retalon.DTOs.Orders;

namespace Retalon.Services.Interfaces;

public interface IOrderService
{
    Task<CreateOrderResponseDto?> CreateOrderAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<List<OrderResponseDto>> GetOrdersAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<OrderResponseDto?> GetOrderByIdAsync(
        Guid userId,
        long orderId,
        CancellationToken cancellationToken = default);
}