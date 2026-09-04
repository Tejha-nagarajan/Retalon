using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Retalon.DTOs.Products;
using Retalon.Models.Entities;
using Retalon.Models.Enums;
using Retalon.Services;
using Retalon.Services.Interfaces;
using Retalon.Tests.Infrastructure;
using Xunit;

namespace Retalon.Tests.Unit;

public class ProductServiceTests
{
    private readonly Mock<IOpenFoodFactsService> _openFoodFacts = new();

    private ProductService CreateSut(Data.ApplicationDbContext db) =>
        new(db, _openFoodFacts.Object, new MemoryCache(new MemoryCacheOptions()));

    private static Category SeedCategory(Data.ApplicationDbContext db, string name = "Snacks")
    {
        var category = new Category { Name = name };
        db.Categories.Add(category);
        db.SaveChanges();
        return category;
    }

    private static Product SeedProduct(
        Data.ApplicationDbContext db,
        Category category,
        string name,
        decimal price,
        string? barcode = null,
        bool isDeleted = false,
        ProductStatus status = ProductStatus.Active,
        DateTime? createdDate = null)
    {
        var product = new Product
        {
            CategoryId = category.CategoryId,
            Name = name,
            Barcode = barcode,
            Price = price,
            Currency = "USD",
            ProductStatus = status,
            IsDeleted = isDeleted,
            CreatedDate = createdDate ?? DateTime.UtcNow
        };
        db.Products.Add(product);
        db.SaveChanges();
        return product;
    }

    [Fact]
    public async Task SearchProductsAsync_ReturnsEmptyPaged_WhenSearchTermBlank()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var sut = CreateSut(db);

        var result = await sut.SearchProductsAsync("   ");

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task SearchProductsAsync_ReturnsLocalMatches_ExcludingDeletedAndInactive()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var category = SeedCategory(db);
        SeedProduct(db, category, "Cookies A", 5m);
        SeedProduct(db, category, "Cookies B", 7m);
        SeedProduct(db, category, "Cookies Deleted", 9m, isDeleted: true);
        SeedProduct(db, category, "Cookies Inactive", 9m, status: ProductStatus.Inactive);

        var sut = CreateSut(db);

        var result = await sut.SearchProductsAsync("Cookies");

        result.TotalCount.Should().Be(2);
        result.Items.Select(i => i.Name).Should().BeEquivalentTo("Cookies A", "Cookies B");
    }

    [Fact]
    public async Task SearchProductsAsync_SortsByPriceDescending_WhenRequested()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var category = SeedCategory(db);
        SeedProduct(db, category, "Chips Cheap", 2m);
        SeedProduct(db, category, "Chips Expensive", 20m);

        var sut = CreateSut(db);

        var result = await sut.SearchProductsAsync("Chips", sortBy: "price", descending: true);

        result.Items.Select(i => i.Price).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task SearchProductsAsync_ClampsPageSize_WhenAboveOneHundred()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var category = SeedCategory(db);
        SeedProduct(db, category, "Bulk Item", 1m);

        var sut = CreateSut(db);

        var result = await sut.SearchProductsAsync("Bulk", pageSize: 500);

        result.PageSize.Should().Be(100);
    }

    [Fact]
    public async Task SearchProductsAsync_ImportsFromOpenFoodFacts_WhenNoLocalMatches()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var external = new ProductResponseDto
        {
            Name = "Imported Cookies",
            Barcode = "1234567890123",
            Price = 0m,
            Currency = "USD",
            ImportSource = "OpenFoodFacts"
        };

        _openFoodFacts
            .Setup(s => s.SearchProductsAsync("importtest", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProductResponseDto> { external });

        var sut = CreateSut(db);

        var result = await sut.SearchProductsAsync("importtest");

        result.TotalCount.Should().Be(1);
        result.Items.Single().Name.Should().Be("Imported Cookies");

        db.Products.Should().ContainSingle(p => p.Barcode == "1234567890123");
        db.Inventories.Should().ContainSingle(i => i.QuantityAvailable == 0);
    }

    [Fact]
    public async Task SearchProductsAsync_ReturnsEmpty_WhenNoLocalAndNoExternalMatches()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        _openFoodFacts
            .Setup(s => s.SearchProductsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ProductResponseDto>());

        var sut = CreateSut(db);

        var result = await sut.SearchProductsAsync("nothing-matches-anywhere");

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetProductByIdAsync_ReturnsNull_WhenNotFound()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var sut = CreateSut(db);

        var result = await sut.GetProductByIdAsync(999);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetProductByIdAsync_ReturnsNull_WhenDeleted()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var category = SeedCategory(db);
        var product = SeedProduct(db, category, "Gone", 3m, isDeleted: true);

        var sut = CreateSut(db);

        var result = await sut.GetProductByIdAsync(product.ProductId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetProductByIdAsync_ReturnsProduct_WhenFound()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var category = SeedCategory(db);
        var product = SeedProduct(db, category, "Findable", 4.5m);

        var sut = CreateSut(db);

        var result = await sut.GetProductByIdAsync(product.ProductId);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Findable");
        result.Price.Should().Be(4.5m);
    }

    [Fact]
    public async Task GetProductByBarcodeAsync_ReturnsLocal_WhenExists()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var category = SeedCategory(db);
        SeedProduct(db, category, "Local Barcode Item", 6m, barcode: "999");

        var sut = CreateSut(db);

        var result = await sut.GetProductByBarcodeAsync("999");

        result.Should().NotBeNull();
        result!.Name.Should().Be("Local Barcode Item");
        _openFoodFacts.Verify(s => s.GetProductByBarcodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetProductByBarcodeAsync_FallsBackToExternal_WhenNotLocal()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var external = new ProductResponseDto { Name = "External Item", Barcode = "777" };

        _openFoodFacts
            .Setup(s => s.GetProductByBarcodeAsync("777", It.IsAny<CancellationToken>()))
            .ReturnsAsync(external);

        var sut = CreateSut(db);

        var result = await sut.GetProductByBarcodeAsync("777");

        result.Should().NotBeNull();
        result!.Name.Should().Be("External Item");
    }

    [Fact]
    public async Task GetProductByBarcodeAsync_ReturnsNull_WhenNotFoundAnywhere()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        _openFoodFacts
            .Setup(s => s.GetProductByBarcodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductResponseDto?)null);

        var sut = CreateSut(db);

        var result = await sut.GetProductByBarcodeAsync("doesnotexist");

        result.Should().BeNull();
    }

    [Fact]
    public async Task ImportFromOpenFoodFactsAsync_ReturnsExisting_WhenAlreadyLocal()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var category = SeedCategory(db);
        SeedProduct(db, category, "Already Here", 2m, barcode: "555");

        var sut = CreateSut(db);

        var result = await sut.ImportFromOpenFoodFactsAsync("555");

        result!.Name.Should().Be("Already Here");
        _openFoodFacts.Verify(s => s.GetProductByBarcodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ImportFromOpenFoodFactsAsync_CreatesProduct_WhenExternalFound()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var external = new ProductResponseDto
        {
            Name = "Newly Imported",
            Barcode = "111",
            ExternalProductId = "ext-1"
        };

        _openFoodFacts
            .Setup(s => s.GetProductByBarcodeAsync("111", It.IsAny<CancellationToken>()))
            .ReturnsAsync(external);

        var sut = CreateSut(db);

        var result = await sut.ImportFromOpenFoodFactsAsync("111");

        result!.Name.Should().Be("Newly Imported");
        result.Price.Should().Be(0m);
        db.Products.Should().ContainSingle(p => p.Barcode == "111");
        db.Categories.Should().ContainSingle(c => c.Name == "Imported");
    }

    [Fact]
    public async Task ImportFromOpenFoodFactsAsync_ReturnsNull_WhenExternalNotFound()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        _openFoodFacts
            .Setup(s => s.GetProductByBarcodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductResponseDto?)null);

        var sut = CreateSut(db);

        var result = await sut.ImportFromOpenFoodFactsAsync("does-not-exist");

        result.Should().BeNull();
    }
}
