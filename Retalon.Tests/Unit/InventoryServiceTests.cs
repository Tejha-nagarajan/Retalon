using FluentAssertions;
using Retalon.Models.Entities;
using Retalon.Services;
using Retalon.Tests.Infrastructure;
using Xunit;

namespace Retalon.Tests.Unit;

public class InventoryServiceTests
{
    private static Product SeedProduct(Data.ApplicationDbContext db, string name = "Widget")
    {
        var category = new Category { Name = "Cat" };
        db.Categories.Add(category);
        db.SaveChanges();

        var product = new Product { CategoryId = category.CategoryId, Name = name, Price = 1m };
        db.Products.Add(product);
        db.SaveChanges();
        return product;
    }

    [Theory]
    [InlineData(-1, 0, 0, 0)]
    [InlineData(0, -1, 0, 0)]
    [InlineData(0, 0, -1, 0)]
    [InlineData(0, 0, 0, -1)]
    public async Task UpdateAsync_Throws_WhenAnyValueNegative(
        int available, int reserved, int safety, int leadTime)
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var sut = new InventoryService(db);

        var act = () => sut.UpdateAsync(1, available, reserved, safety, leadTime);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UpdateAsync_Throws_WhenReservedExceedsAvailable()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var sut = new InventoryService(db);

        var act = () => sut.UpdateAsync(1, quantityAvailable: 5, quantityReserved: 10, safetyStockLevel: 0, procurementLeadTimeDays: 0);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*cannot exceed*");
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNull_WhenProductDoesNotExist()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var sut = new InventoryService(db);

        var result = await sut.UpdateAsync(999, 10, 0, 0, 0);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_CreatesInventory_WhenMissingButProductExists()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var product = SeedProduct(db);
        var sut = new InventoryService(db);

        var result = await sut.UpdateAsync(product.ProductId, 50, 5, 10, 3);

        result.Should().NotBeNull();
        result!.QuantityAvailable.Should().Be(50);
        db.Inventories.Should().ContainSingle(i => i.ProductId == product.ProductId);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesExistingInventory_WhenPresent()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var product = SeedProduct(db);
        db.Inventories.Add(new Inventory
        {
            ProductId = product.ProductId,
            QuantityAvailable = 10,
            QuantityReserved = 2,
            SafetyStockLevel = 1,
            ProcurementLeadTimeDays = 1
        });
        await db.SaveChangesAsync();

        var sut = new InventoryService(db);
        var result = await sut.UpdateAsync(product.ProductId, 100, 20, 5, 7);

        result!.QuantityAvailable.Should().Be(100);
        result.QuantityReserved.Should().Be(20);
        db.Inventories.Should().ContainSingle(i => i.ProductId == product.ProductId && i.QuantityAvailable == 100);
    }

    [Fact]
    public async Task RestockAsync_Throws_WhenQuantityNotPositive()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var sut = new InventoryService(db);

        var act = () => sut.RestockAsync(1, 0);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RestockAsync_ReturnsNull_WhenInventoryMissing()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var sut = new InventoryService(db);

        var result = await sut.RestockAsync(999, 10);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RestockAsync_IncrementsQuantityAvailable()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var product = SeedProduct(db);
        db.Inventories.Add(new Inventory { ProductId = product.ProductId, QuantityAvailable = 10 });
        await db.SaveChangesAsync();

        var sut = new InventoryService(db);
        var result = await sut.RestockAsync(product.ProductId, 15);

        result!.QuantityAvailable.Should().Be(25);
    }
}
