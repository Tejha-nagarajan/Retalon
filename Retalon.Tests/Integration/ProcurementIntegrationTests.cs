using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Retalon.Data;
using Retalon.DTOs.Cart;
using Retalon.DTOs.Orders;
using Retalon.DTOs.Procurement;
using Retalon.Models.Enums;
using Retalon.Tests.Infrastructure;
using Xunit;

namespace Retalon.Tests.Integration;

/// <summary>
/// Not part of the shared "Integration" collection: "LoginPolicy" is a global (unpartitioned)
/// 5-requests/minute limiter shared with several other auth-heavy test classes, so this class
/// gets its own factory/host per test (a fresh in-memory limiter) via IAsyncLifetime — xUnit
/// creates a new instance of the test class per [Fact], giving per-test isolation for free.
/// </summary>
public class ProcurementIntegrationTests : IAsyncLifetime
{
    private readonly RetalonWebApplicationFactory _factory = new();

    public Task InitializeAsync() => _factory.InitializeDatabaseAsync();

    public async Task DisposeAsync()
    {
        await _factory.DisposeDatabaseAsync();
        await _factory.DisposeAsync();
    }

    private async Task<long> SeedProductAsync(int qty)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var product = await DbSeedHelper.SeedProductWithInventoryAsync(
            db, $"ProcWidget_{Guid.NewGuid():N}", 6m, qty);
        return product.ProductId;
    }

    private async Task<(HttpClient client, long orderId)> CreateOrderWithShortageAsync()
    {
        var productId = await SeedProductAsync(2);
        var client = _factory.CreateClient();
        var user = await AuthTestHelper.RegisterAndLoginAsync(client);
        AuthTestHelper.SetBearerToken(client, user.AccessToken);

        await client.PostAsJsonAsync("/api/cart/items",
            new AddCartItemRequestDto { ProductId = productId, Quantity = 2 });
        var created = await (await client.PostAsync("/api/orders", null))
            .Content.ReadFromJsonAsync<CreateOrderResponseDto>();

        // Reduce available stock below what was ordered, to create a real shortage.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var inventory = db.Inventories.Single(i => i.ProductId == productId);
        inventory.QuantityAvailable = 0;
        await db.SaveChangesAsync();

        return (client, created!.OrderId);
    }

    [Fact]
    public async Task CreateProcurement_CreatesRecord_WhenShortageExists()
    {
        var (client, orderId) = await CreateOrderWithShortageAsync();

        var response = await client.PostAsJsonAsync("/api/procurement",
            new CreateProcurementRequestDto { OrderId = orderId });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var results = await response.Content.ReadFromJsonAsync<List<ProcurementResponseDto>>();
        results.Should().ContainSingle();
    }

    [Fact]
    public async Task UpdateProcurementStatus_ReturnsForbidden_ForCustomerRole()
    {
        var (client, orderId) = await CreateOrderWithShortageAsync();
        var createResponse = await client.PostAsJsonAsync("/api/procurement",
            new CreateProcurementRequestDto { OrderId = orderId });
        var created = (await createResponse.Content.ReadFromJsonAsync<List<ProcurementResponseDto>>())!.Single();

        var response = await client.PutAsJsonAsync(
            $"/api/procurement/{created.ProcurementId}/status",
            ProcurementStatus.Ordered);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetProcurements_ReturnsUnauthorized_WithoutBearerToken()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/procurement");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
