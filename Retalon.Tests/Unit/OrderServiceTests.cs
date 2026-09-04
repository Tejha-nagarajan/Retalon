using FluentAssertions;
using Moq;
using Retalon.Models.Entities;
using Retalon.Models.Enums;
using Retalon.Services;
using Retalon.Services.Interfaces;
using Retalon.Tests.Infrastructure;
using Xunit;

namespace Retalon.Tests.Unit;

public class OrderServiceTests
{
    private readonly Mock<IAuditService> _auditService = new();

    private OrderService CreateSut(Data.ApplicationDbContext db) => new(db, _auditService.Object);

    private static (Cart cart, Product product, Inventory inventory) SeedCartWithItem(
        Data.ApplicationDbContext db,
        Guid userId,
        int quantity,
        int quantityAvailable,
        int quantityReserved = 0,
        decimal price = 10m,
        int safetyStockLevel = 0,
        int procurementLeadTimeDays = 3,
        bool productDeleted = false,
        ProductStatus productStatus = ProductStatus.Active)
    {
        var category = new Category { Name = "Cat" };
        db.Categories.Add(category);
        db.SaveChanges();

        var product = new Product
        {
            CategoryId = category.CategoryId,
            Name = "Widget",
            Price = price,
            IsDeleted = productDeleted,
            ProductStatus = productStatus
        };
        db.Products.Add(product);
        db.SaveChanges();

        var inventory = new Inventory
        {
            ProductId = product.ProductId,
            QuantityAvailable = quantityAvailable,
            QuantityReserved = quantityReserved,
            SafetyStockLevel = safetyStockLevel,
            ProcurementLeadTimeDays = procurementLeadTimeDays
        };
        db.Inventories.Add(inventory);
        db.SaveChanges();

        var cart = new Cart { CartId = Guid.NewGuid(), UserId = userId };
        db.Carts.Add(cart);
        db.SaveChanges();

        db.CartItems.Add(new CartItem
        {
            CartId = cart.CartId,
            ProductId = product.ProductId,
            Quantity = quantity,
            AddedDate = DateTime.UtcNow
        });
        db.SaveChanges();

        return (cart, product, inventory);
    }

    [Fact]
    public async Task CreateOrderAsync_ReturnsNull_WhenCartMissing()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var sut = CreateSut(db);

        var result = await sut.CreateOrderAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateOrderAsync_ReturnsNull_WhenCartEmpty()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var userId = Guid.NewGuid();
        db.Carts.Add(new Cart { CartId = Guid.NewGuid(), UserId = userId });
        await db.SaveChangesAsync();

        var sut = CreateSut(db);
        var result = await sut.CreateOrderAsync(userId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateOrderAsync_Throws_WhenProductInactive()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var userId = Guid.NewGuid();
        SeedCartWithItem(db, userId, quantity: 1, quantityAvailable: 10, productStatus: ProductStatus.Inactive);

        var sut = CreateSut(db);
        var act = () => sut.CreateOrderAsync(userId);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*unavailable*");
    }

    [Fact]
    public async Task CreateOrderAsync_Throws_WhenInsufficientInventory()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var userId = Guid.NewGuid();
        SeedCartWithItem(db, userId, quantity: 5, quantityAvailable: 3);

        var sut = CreateSut(db);
        var act = () => sut.CreateOrderAsync(userId);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Insufficient inventory*");
    }

    [Fact]
    public async Task CreateOrderAsync_ComputesTotalAmount_AndClearsCart()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var userId = Guid.NewGuid();
        SeedCartWithItem(db, userId, quantity: 3, quantityAvailable: 10, price: 5m);

        var sut = CreateSut(db);
        var result = await sut.CreateOrderAsync(userId);

        result.Should().NotBeNull();
        result!.TotalAmount.Should().Be(15m);
        result.OrderStatus.Should().Be(OrderStatus.Pending.ToString());
        db.CartItems.Should().BeEmpty();
        db.Orders.Should().ContainSingle(o => o.OrderId == result.OrderId);

        _auditService.Verify(a => a.LogAsync(
            userId, "OrderCreated", "Order", result.OrderId.ToString(), It.IsAny<string>(), default), Times.Once);
    }

    [Fact]
    public async Task CreateOrderAsync_UsesStandardTwoDayDelivery_WhenStockComfortablyAboveSafetyLevel()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var userId = Guid.NewGuid();
        SeedCartWithItem(db, userId, quantity: 2, quantityAvailable: 100, safetyStockLevel: 5, procurementLeadTimeDays: 10);

        var sut = CreateSut(db);
        var result = await sut.CreateOrderAsync(userId);

        var order = await sut.GetOrderByIdAsync(userId, result!.OrderId);
        order!.Items.Single().DeliveryDays.Should().Be(2);
    }

    [Fact]
    public async Task CreateOrderAsync_UsesLeadTimeDelivery_WhenAtOrBelowSafetyStock()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var userId = Guid.NewGuid();
        // available after reservation (10) is not > safetyStockLevel (10) -> triggers lead-time branch
        SeedCartWithItem(db, userId, quantity: 5, quantityAvailable: 10, safetyStockLevel: 10, procurementLeadTimeDays: 6);

        var sut = CreateSut(db);
        var result = await sut.CreateOrderAsync(userId);

        var order = await sut.GetOrderByIdAsync(userId, result!.OrderId);
        order!.Items.Single().DeliveryDays.Should().Be(8);
    }

    [Fact]
    public async Task GetOrdersAsync_ReturnsOnlyOwnedOrders()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        SeedCartWithItem(db, userId, quantity: 1, quantityAvailable: 10);
        SeedCartWithItem(db, otherUserId, quantity: 1, quantityAvailable: 10);

        var sut = CreateSut(db);
        await sut.CreateOrderAsync(userId);
        await sut.CreateOrderAsync(otherUserId);

        var orders = await sut.GetOrdersAsync(userId);

        orders.Should().ContainSingle();
        orders.Single().UserId.Should().Be(userId);
    }

    [Fact]
    public async Task GetOrderByIdAsync_ReturnsNull_WhenNotOwnedByUser()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var userId = Guid.NewGuid();
        SeedCartWithItem(db, userId, quantity: 1, quantityAvailable: 10);

        var sut = CreateSut(db);
        var created = await sut.CreateOrderAsync(userId);

        var result = await sut.GetOrderByIdAsync(Guid.NewGuid(), created!.OrderId);

        result.Should().BeNull();
    }
}
