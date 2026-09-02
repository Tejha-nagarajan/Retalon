using Retalon.DTOs.Cart;

namespace Retalon.Services.Interfaces;

public interface ICartService
{
    Task<CartResponseDto> GetCartAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<CartResponseDto?> AddItemAsync(
        Guid userId,
        AddCartItemRequestDto request,
        CancellationToken cancellationToken = default);

    Task<bool> RemoveItemAsync(
        Guid userId,
        long cartItemId,
        CancellationToken cancellationToken = default);
}