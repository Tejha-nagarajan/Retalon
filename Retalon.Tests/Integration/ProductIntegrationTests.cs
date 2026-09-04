using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Retalon.Data;
using Retalon.DTOs.Common;
using Retalon.DTOs.Products;
using Retalon.Tests.Infrastructure;
using Xunit;

namespace Retalon.Tests.Integration;

[Collection("Integration")]
public class ProductIntegrationTests
{
    private readonly RetalonWebApplicationFactory _factory;

    public ProductIntegrationTests(SharedFactoryFixture fixture)
    {
        _factory = fixture.Factory;
    }

    private async Task<Retalon.Models.Entities.Product> SeedProductAsync(string name, decimal price, int qty)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await DbSeedHelper.SeedProductWithInventoryAsync(db, name, price, qty);
    }

    [Fact]
    public async Task Search_ReturnsLocalMatches()
    {
        var unique = Guid.NewGuid().ToString("N")[..8];
        await SeedProductAsync($"SearchWidget_{unique}", 19.99m, 10);

        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/products/search?query=SearchWidget_{unique}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var products = await response.Content.ReadFromJsonAsync<PagedResponseDto<ProductResponseDto>>();
        products!.Items.Should().ContainSingle(p => p.Name == $"SearchWidget_{unique}");
    }

    [Fact]
    public async Task Search_ReturnsBadRequest_WhenQueryBlank()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/products/search?query=");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Search_ReturnsBadRequest_WhenPageSizeExceedsMax()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/products/search?query=anything&pageSize=101");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Search_ImportsFromOpenFoodFacts_WhenNoLocalMatch()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/products/search?query={FakeOpenFoodFactsService.ImportSearchTerm}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var products = await response.Content.ReadFromJsonAsync<PagedResponseDto<ProductResponseDto>>();
        products!.Items.Should().ContainSingle(p => p.Barcode == FakeOpenFoodFactsService.DefaultBarcodeProduct.Barcode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Products.Should().Contain(p => p.Barcode == FakeOpenFoodFactsService.DefaultBarcodeProduct.Barcode);
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenProductDoesNotExist()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/products/999999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_ReturnsProduct_WhenExists()
    {
        var unique = Guid.NewGuid().ToString("N")[..8];
        var product = await SeedProductAsync($"GetByIdWidget_{unique}", 5m, 3);

        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/products/{product.ProductId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<ProductResponseDto>();
        dto!.Name.Should().Be($"GetByIdWidget_{unique}");
    }

    [Fact]
    public async Task GetByBarcode_FallsBackToOpenFoodFacts_WhenNotLocal()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/products/barcode/{FakeOpenFoodFactsService.DefaultBarcodeProduct.Barcode}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<ProductResponseDto>();
        dto!.Barcode.Should().Be(FakeOpenFoodFactsService.DefaultBarcodeProduct.Barcode);
    }

    [Fact]
    public async Task GetByBarcode_ReturnsNotFound_WhenNowhereFound()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/products/barcode/does-not-exist-anywhere");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
