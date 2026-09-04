using FluentAssertions;
using Retalon.DTOs.Cart;
using Retalon.Models.Entities;
using Retalon.Models.Enums;
using Retalon.Services;
using Retalon.Tests.Infrastructure;
using Xunit;

namespace Retalon.Tests.Unit;

/// <summary>
/// Uses SqliteTestDatabase (a real relational provider), not the EF Core InMemory provider:
/// Cart.UserId and CartItem.ProductId are required FKs that the InMemory provider does not
/// enforce the same way SQLite/SQL Server do, which previously masked an unseeded-User bug
/// in these tests (confirmed empirically - see SeedUser below).
/// </summary>
public class CartServiceTests
{
    private static User SeedUser(Data.ApplicationDbContext db, Guid userId)
    {
        var user = new User
        {
            UserId = userId,
            Email = $"seeded_{userId:N}@test.local",
            PasswordHash = "hash",
            FirstName = "Seeded",
            LastName = "User",
            Address = "1 Test St",
            City = "Testville",
            PostalCode = "00000",
            Country = "USA",
            IsActive = true,
            CreatedDate = DateTime.UtcNow
        };
        db.Users.Add(user);
        db.SaveChanges();
        db.ChangeTracker.Clear();
        return user;
    }

    private static (Product product, Inventory inventory) SeedProductWithInventory(
        Data.ApplicationDbContext db,
        int quantityAvailable,
        int quantityReserved = 0,
        bool isDeleted = false,
        ProductStatus status = ProductStatus.Active)
    {
        var category = new Category { Name = "Cat" };
        db.Categories.Add(category);
        db.SaveChanges();

        var product = new Product
        {
            CategoryId = category.CategoryId,
            Name = "Widget",
            Price = 10m,
            IsDeleted = isDeleted,
            ProductStatus = status
        };
        db.Products.Add(product);
        db.SaveChanges();

        var inventory = new Inventory
        {
            ProductId = product.ProductId,
            QuantityAvailable = quantityAvailable,
            QuantityReserved = quantityReserved
        };
        db.Inventories.Add(inventory);
        db.SaveChanges();

        // Detach seeded entities so CartService's own AsNoTracking() re-query of the
        // same Product doesn't collide with this context's leftover tracked instance.
        db.ChangeTracker.Clear();

        return (product, inventory);
    }

    /// <summary>
    /// Seeds a Cart + CartItem directly via EF, bypassing CartService.AddItemAsync's own
    /// "create Cart then Add(new CartItem { Product = untrackedProduct })" path - see the
    /// confirmed bug documented on AddItemAsync_ThrowsDbUpdateException_WhenCartIsNewAndProductAlreadyExists
    /// below. Used by tests whose real target is RemoveItemAsync or the accumulate-quantity
    /// branch, so they aren't blocked by that unrelated, already-existing-cart-independent bug.
    /// </summary>
    private static CartItem SeedCartItem(Data.ApplicationDbContext db, Guid userId, Product product, int quantity)
    {
        var cart = new Cart { CartId = Guid.NewGuid(), UserId = userId };
        db.Carts.Add(cart);
        db.SaveChanges();

        var cartItem = new CartItem
        {
            CartId = cart.CartId,
            ProductId = product.ProductId,
            Quantity = quantity,
            AddedDate = DateTime.UtcNow
        };
        db.CartItems.Add(cartItem);
        db.SaveChanges();
        db.ChangeTracker.Clear();

        return cartItem;
    }

    [Fact]
    public async Task GetCartAsync_CreatesCart_WhenMissing()
    {
        using var sqlite = new SqliteTestDatabase();
        var db = sqlite.Context;
        var userId = Guid.NewGuid();
        SeedUser(db, userId);
        var sut = new CartService(db);

        var result = await sut.GetCartAsync(userId);

        result.Should().NotBeNull();
        db.Carts.Should().ContainSingle(c => c.UserId == userId);
    }

    [Fact]
    public async Task GetCartAsync_ReturnsExistingCart_WhenPresent()
    {
        using var sqlite = new SqliteTestDatabase();
        var db = sqlite.Context;
        var userId = Guid.NewGuid();
        SeedUser(db, userId);
        var cart = new Cart { CartId = Guid.NewGuid(), UserId = userId };
        db.Carts.Add(cart);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var sut = new CartService(db);
        await sut.GetCartAsync(userId);

        db.Carts.Count(c => c.UserId == userId).Should().Be(1);
    }

    [Fact]
    public async Task AddItemAsync_Throws_WhenQuantityNotPositive()
    {
        using var sqlite = new SqliteTestDatabase();
        var db = sqlite.Context;
        var sut = new CartService(db);

        var act = () => sut.AddItemAsync(Guid.NewGuid(), new AddCartItemRequestDto { ProductId = 1, Quantity = 0 });

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AddItemAsync_ReturnsNull_WhenProductMissing()
    {
        using var sqlite = new SqliteTestDatabase();
        var db = sqlite.Context;
        var sut = new CartService(db);

        var result = await sut.AddItemAsync(Guid.NewGuid(), new AddCartItemRequestDto { ProductId = 999, Quantity = 1 });

        result.Should().BeNull();
    }

    [Fact]
    public async Task AddItemAsync_ReturnsNull_WhenProductInactive()
    {
        using var sqlite = new SqliteTestDatabase();
        var db = sqlite.Context;
        var (product, _) = SeedProductWithInventory(db, 10, status: ProductStatus.Inactive);
        var sut = new CartService(db);

        var result = await sut.AddItemAsync(Guid.NewGuid(), new AddCartItemRequestDto { ProductId = product.ProductId, Quantity = 1 });

        result.Should().BeNull();
    }

    [Fact]
    public async Task AddItemAsync_Throws_WhenInventoryMissing()
    {
        using var sqlite = new SqliteTestDatabase();
        var db = sqlite.Context;
        var category = new Category { Name = "Cat" };
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        var product = new Product { CategoryId = category.CategoryId, Name = "NoInventory", Price = 1m };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var sut = new CartService(db);

        var act = () => sut.AddItemAsync(Guid.NewGuid(), new AddCartItemRequestDto { ProductId = product.ProductId, Quantity = 1 });

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task AddItemAsync_Throws_WhenInsufficientInventory()
    {
        using var sqlite = new SqliteTestDatabase();
        var db = sqlite.Context;
        var (product, _) = SeedProductWithInventory(db, quantityAvailable: 5, quantityReserved: 3);
        var sut = new CartService(db);

        var act = () => sut.AddItemAsync(Guid.NewGuid(), new AddCartItemRequestDto { ProductId = product.ProductId, Quantity = 3 });

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Insufficient inventory*");
    }

    /// <summary>
    /// CONFIRMED PRODUCTION BUG (not fixed - see task constraints): when AddItemAsync creates
    /// a brand-new Cart for a user (Carts.Add(cart)) and then appends a new CartItem whose
    /// Product navigation is set to an already-existing, AsNoTracking()-loaded Product
    /// (CartService.cs lines 127-134), EF Core's automatic navigation-fixup graph-attacher
    /// marks that already-existing Product as Added instead of Unchanged (the key-based
    /// "already exists" heuristic only applies to explicit context.Add() graph walks, not to
    /// fixup triggered by mutating an already-tracked/Added entity's collection navigation
    /// after the fact). SaveChangesAsync then attempts to re-INSERT a Product row that
    /// already exists. Confirmed empirically against SQLite (a real relational provider, not
    /// just the lenient EF Core InMemory provider): DbUpdateException wrapping
    /// "SQLite Error 19: 'UNIQUE constraint failed: Products.ProductId'". This means any
    /// real user's very first AddItemAsync call (no existing Cart yet) for a Product that
    /// already exists in the catalog would fail the same way against SQL Server.
    /// </summary>
    [Fact]
    public async Task AddItemAsync_ThrowsDbUpdateException_WhenCartIsNewAndProductAlreadyExists()
    {
        using var sqlite = new SqliteTestDatabase();
        var db = sqlite.Context;
        var (product, _) = SeedProductWithInventory(db, quantityAvailable: 10);
        var userId = Guid.NewGuid();
        SeedUser(db, userId);
        var sut = new CartService(db);

        var act = () => sut.AddItemAsync(userId, new AddCartItemRequestDto { ProductId = product.ProductId, Quantity = 4 });

        await act.Should().ThrowAsync<Microsoft.EntityFrameworkCore.DbUpdateException>();
    }

    [Fact]
    public async Task AddItemAsync_AccumulatesQuantity_WhenItemAlreadyInCart()
    {
        using var sqlite = new SqliteTestDatabase();
        var db = sqlite.Context;
        var (product, _) = SeedProductWithInventory(db, quantityAvailable: 10);
        var userId = Guid.NewGuid();
        SeedUser(db, userId);
        // Seed the cart/item directly rather than via a first AddItemAsync call, to exercise
        // the accumulate-quantity branch without hitting the unrelated new-cart bug documented
        // on AddItemAsync_ThrowsDbUpdateException_WhenCartIsNewAndProductAlreadyExists above.
        SeedCartItem(db, userId, product, quantity: 3);
        var sut = new CartService(db);

        var result = await sut.AddItemAsync(userId, new AddCartItemRequestDto { ProductId = product.ProductId, Quantity = 4 });

        result!.Items.Should().ContainSingle(i => i.ProductId == product.ProductId && i.Quantity == 7);
    }

    [Fact]
    public async Task RemoveItemAsync_ReturnsFalse_WhenNotFound()
    {
        using var sqlite = new SqliteTestDatabase();
        var db = sqlite.Context;
        var sut = new CartService(db);

        var result = await sut.RemoveItemAsync(Guid.NewGuid(), 999);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveItemAsync_ReturnsFalse_WhenBelongsToAnotherUser()
    {
        using var sqlite = new SqliteTestDatabase();
        var db = sqlite.Context;
        var (product, _) = SeedProductWithInventory(db, 10);
        var owner = Guid.NewGuid();
        SeedUser(db, owner);
        // Seed directly rather than via AddItemAsync - see the new-cart bug documented on
        // AddItemAsync_ThrowsDbUpdateException_WhenCartIsNewAndProductAlreadyExists above;
        // this test's target is RemoveItemAsync, not AddItemAsync.
        var cartItem = SeedCartItem(db, owner, product, quantity: 1);
        var sut = new CartService(db);

        var result = await sut.RemoveItemAsync(Guid.NewGuid(), cartItem.CartItemId);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveItemAsync_ReturnsTrue_WhenOwnedAndRemoved()
    {
        using var sqlite = new SqliteTestDatabase();
        var db = sqlite.Context;
        var (product, _) = SeedProductWithInventory(db, 10);
        var userId = Guid.NewGuid();
        SeedUser(db, userId);
        // Seed directly rather than via AddItemAsync - see the new-cart bug documented on
        // AddItemAsync_ThrowsDbUpdateException_WhenCartIsNewAndProductAlreadyExists above;
        // this test's target is RemoveItemAsync, not AddItemAsync.
        var cartItem = SeedCartItem(db, userId, product, quantity: 1);
        var sut = new CartService(db);

        var result = await sut.RemoveItemAsync(userId, cartItem.CartItemId);

        result.Should().BeTrue();
        db.CartItems.Should().BeEmpty();
    }
}
