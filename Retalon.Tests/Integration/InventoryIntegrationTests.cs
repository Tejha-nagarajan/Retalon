using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Retalon.Data;
using Retalon.Tests.Infrastructure;
using Xunit;

namespace Retalon.Tests.Integration;

/// <summary>
/// InventoryController has NO [Authorize] anywhere (class or action level), confirmed
/// by reading the controller source. These tests document that actual, current behavior:
/// every endpoint is reachable without a bearer token.
/// </summary>
[Collection("Integration")]
public class InventoryIntegrationTests
{
    private readonly RetalonWebApplicationFactory _factory;

    public InventoryIntegrationTests(SharedFactoryFixture fixture)
    {
        _factory = fixture.Factory;
    }

    private async Task<long> SeedProductAsync(int qty)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var product = await DbSeedHelper.SeedProductWithInventoryAsync(
            db, $"InvWidget_{Guid.NewGuid():N}", 9.99m, qty);
        return product.ProductId;
    }

    [Fact]
    public async Task GetByProductId_Succeeds_WithoutAuthentication()
    {
        var productId = await SeedProductAsync(25);
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/inventory/{productId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetByProductId_ReturnsNotFound_WhenMissing()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/inventory/999999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_Succeeds_WithoutAuthentication()
    {
        var productId = await SeedProductAsync(25);
        var client = _factory.CreateClient();

        var response = await client.PutAsJsonAsync($"/api/inventory/{productId}", new
        {
            QuantityAvailable = 50,
            QuantityReserved = 0,
            SafetyStockLevel = 5,
            ProcurementLeadTimeDays = 3
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Restock_Succeeds_WithoutAuthentication_AndIncreasesQuantity()
    {
        var productId = await SeedProductAsync(10);
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/api/inventory/{productId}/restock", new { Quantity = 15 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Restock_ReturnsBadRequest_WhenQuantityNotPositive()
    {
        var productId = await SeedProductAsync(10);
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/api/inventory/{productId}/restock", new { Quantity = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
