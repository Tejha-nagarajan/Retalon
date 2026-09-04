using FluentAssertions;
using Retalon.DTOs.Procurement;
using Retalon.Models.Entities;
using Retalon.Models.Enums;
using Retalon.Services;
using Retalon.Tests.Infrastructure;
using Xunit;

namespace Retalon.Tests.Unit;

public class ProcurementServiceTests
{
    private static (Order order, Product product) SeedOrderWithItem(
        Data.ApplicationDbContext db,
        Guid userId,
        int orderedQuantity,
        int quantityAvailable,
        int quantityReserved = 0)
    {
        var category = new Category { Name = "Cat" };
        db.Categories.Add(category);
        db.SaveChanges();

        var product = new Product { CategoryId = category.CategoryId, Name = "Widget", Price = 10m };
        db.Products.Add(product);
        db.SaveChanges();

        db.Inventories.Add(new Inventory
        {
            ProductId = product.ProductId,
            QuantityAvailable = quantityAvailable,
            QuantityReserved = quantityReserved
        });
        db.SaveChanges();

        var order = new Order
        {
            UserId = userId,
            OrderStatus = OrderStatus.Pending,
            TotalAmount = orderedQuantity * product.Price,
            CreatedDate = DateTime.UtcNow
        };
        db.Orders.Add(order);
        db.SaveChanges();

        db.OrderItems.Add(new OrderItem
        {
            OrderId = order.OrderId,
            ProductId = product.ProductId,
            Quantity = orderedQuantity,
            UnitPrice = product.Price,
            DeliveryDays = 2
        });
        db.SaveChanges();

        return (order, product);
    }

    [Fact]
    public async Task CreateProcurementAsync_Throws_WhenOrderNotFound()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var sut = new ProcurementService(db);

        var act = () => sut.CreateProcurementAsync(Guid.NewGuid(), new CreateProcurementRequestDto { OrderId = 999 });

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Order not found*");
    }

    [Fact]
    public async Task CreateProcurementAsync_Throws_WhenOrderBelongsToAnotherUser()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var (order, _) = SeedOrderWithItem(db, Guid.NewGuid(), orderedQuantity: 10, quantityAvailable: 5);
        var sut = new ProcurementService(db);

        var act = () => sut.CreateProcurementAsync(Guid.NewGuid(), new CreateProcurementRequestDto { OrderId = order.OrderId });

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateProcurementAsync_SkipsItem_WhenNoShortage()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var userId = Guid.NewGuid();
        var (order, _) = SeedOrderWithItem(db, userId, orderedQuantity: 5, quantityAvailable: 10);
        var sut = new ProcurementService(db);

        var result = await sut.CreateProcurementAsync(userId, new CreateProcurementRequestDto { OrderId = order.OrderId });

        result.Should().BeEmpty();
        db.Procurements.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateProcurementAsync_CreatesProcurement_WhenShortageExists()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var userId = Guid.NewGuid();
        var (order, product) = SeedOrderWithItem(db, userId, orderedQuantity: 10, quantityAvailable: 4);
        var sut = new ProcurementService(db);

        var result = await sut.CreateProcurementAsync(userId, new CreateProcurementRequestDto { OrderId = order.OrderId });

        result.Should().ContainSingle();
        result.Single().RequiredQuantity.Should().Be(6);
        result.Single().ProcurementStatus.Should().Be(ProcurementStatus.Requested.ToString());
        db.Procurements.Should().ContainSingle(p => p.ProductId == product.ProductId);
    }

    [Fact]
    public async Task CreateProcurementAsync_ReusesExistingNonTerminalProcurement()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var userId = Guid.NewGuid();
        var (order, product) = SeedOrderWithItem(db, userId, orderedQuantity: 10, quantityAvailable: 4);

        var sut = new ProcurementService(db);
        await sut.CreateProcurementAsync(userId, new CreateProcurementRequestDto { OrderId = order.OrderId });
        await sut.CreateProcurementAsync(userId, new CreateProcurementRequestDto { OrderId = order.OrderId });

        db.Procurements.Where(p => p.ProductId == product.ProductId).Should().ContainSingle();
    }

    [Fact]
    public async Task GetProcurementsAsync_ReturnsOnlyOwnedByUser()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var (order1, _) = SeedOrderWithItem(db, userId, orderedQuantity: 10, quantityAvailable: 4);
        var (order2, _) = SeedOrderWithItem(db, otherUserId, orderedQuantity: 10, quantityAvailable: 4);

        var sut = new ProcurementService(db);
        await sut.CreateProcurementAsync(userId, new CreateProcurementRequestDto { OrderId = order1.OrderId });
        await sut.CreateProcurementAsync(otherUserId, new CreateProcurementRequestDto { OrderId = order2.OrderId });

        var result = await sut.GetProcurementsAsync(userId);

        result.Should().ContainSingle();
    }

    [Fact]
    public async Task UpdateProcurementStatusAsync_ReturnsNull_WhenNotFound()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var sut = new ProcurementService(db);

        var result = await sut.UpdateProcurementStatusAsync(999, ProcurementStatus.Ordered);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateProcurementStatusAsync_UpdatesStatus_WhenFound()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var userId = Guid.NewGuid();
        var (order, _) = SeedOrderWithItem(db, userId, orderedQuantity: 10, quantityAvailable: 4);
        var sut = new ProcurementService(db);
        var created = await sut.CreateProcurementAsync(userId, new CreateProcurementRequestDto { OrderId = order.OrderId });

        var result = await sut.UpdateProcurementStatusAsync(created.Single().ProcurementId, ProcurementStatus.Ordered);

        result!.ProcurementStatus.Should().Be(ProcurementStatus.Ordered.ToString());
    }
}
