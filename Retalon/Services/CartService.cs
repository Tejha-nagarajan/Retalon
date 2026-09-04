using Microsoft.EntityFrameworkCore;
using Retalon.Data;
using Retalon.DTOs.Cart;
using Retalon.Models.Entities;
using Retalon.Models.Enums;
using Retalon.Services.Interfaces;

namespace Retalon.Services;

public class CartService : ICartService
{
    private readonly ApplicationDbContext _context;

    public CartService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CartResponseDto> GetCartAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var cart = await _context.Carts
            .AsNoTracking()
            .Include(c => c.CartItems)
            .ThenInclude(ci => ci.Product)
            .FirstOrDefaultAsync(
                c => c.UserId == userId,
                cancellationToken);

        if (cart == null)
        {
            cart = new Cart
            {
                CartId = Guid.NewGuid(),
                UserId = userId
            };

            _context.Carts.Add(cart);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return MapToDto(cart);
    }

    public async Task<CartResponseDto?> AddItemAsync(
        Guid userId,
        AddCartItemRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request.Quantity <= 0)
        {
            throw new ArgumentException(
                "Quantity must be greater than zero.");
        }

        var product = await _context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.ProductId == request.ProductId &&
                     !p.IsDeleted &&
                     p.ProductStatus == ProductStatus.Active,
                cancellationToken);

        if (product == null)
        {
            return null;
        }

        var inventory = await _context.Inventories
            .FirstOrDefaultAsync(
                i => i.ProductId == request.ProductId,
                cancellationToken);

        if (inventory == null)
        {
            throw new InvalidOperationException(
                "Inventory is not available for this product.");
        }

        var availableQuantity =
            inventory.QuantityAvailable -
            inventory.QuantityReserved;

        var cart = await _context.Carts
            .Include(c => c.CartItems)
            .ThenInclude(ci => ci.Product)
            .FirstOrDefaultAsync(
                c => c.UserId == userId,
                cancellationToken);

        if (cart == null)
        {
            cart = new Cart
            {
                CartId = Guid.NewGuid(),
                UserId = userId
            };

            _context.Carts.Add(cart);
        }

        var existingItem = cart.CartItems
            .FirstOrDefault(
                ci => ci.ProductId == request.ProductId);

        var newQuantity = request.Quantity;

        if (existingItem != null)
        {
            newQuantity += existingItem.Quantity;
        }

        if (newQuantity > availableQuantity)
        {
            throw new InvalidOperationException(
                $"Insufficient inventory. " +
                $"Only {availableQuantity} units are available.");
        }

        if (existingItem != null)
        {
            existingItem.Quantity = newQuantity;
        }
        else
        {
            cart.CartItems.Add(new CartItem
            {
                CartId = cart.CartId,
                ProductId = request.ProductId,
                Quantity = request.Quantity,
                AddedDate = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _context.Entry(cart)
            .Collection(c => c.CartItems)
            .Query()
            .Include(ci => ci.Product)
            .LoadAsync(cancellationToken);

        return MapToDto(cart);
    }

    public async Task<bool> RemoveItemAsync(
        Guid userId,
        long cartItemId,
        CancellationToken cancellationToken = default)
    {
        var cartItem = await _context.CartItems
            .Include(ci => ci.Cart)
            .FirstOrDefaultAsync(
                ci => ci.CartItemId == cartItemId &&
                      ci.Cart.UserId == userId,
                cancellationToken);

        if (cartItem == null)
        {
            return false;
        }

        _context.CartItems.Remove(cartItem);

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static CartResponseDto MapToDto(Cart cart)
    {
        return new CartResponseDto
        {
            CartId = cart.CartId,
            Items = cart.CartItems
                .Select(item => new CartItemResponseDto
                {
                    CartItemId = item.CartItemId,
                    ProductId = item.ProductId,
                    ProductName = item.Product.Name,
                    ImageUrl = item.Product.ImageUrl,
                    Price = item.Product.Price,
                    Quantity = item.Quantity
                })
                .ToList()
        };
    }
}