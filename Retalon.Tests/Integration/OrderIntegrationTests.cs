using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Retalon.Data;
using Retalon.DTOs.Cart;
using Retalon.DTOs.Orders;
using Retalon.Tests.Infrastructure;
using Xunit;

namespace Retalon.Tests.Integration;

/// <summary>
/// Not part of the shared "Integration" collection: "LoginPolicy" is a global (unpartitioned)
/// 5-requests/minute limiter shared with several other auth-heavy test classes, so this class
/// gets its own factory/host per test (a fresh in-memory limiter) via IAsyncLifetime — xUnit
/// creates a new instance of the test class per [Fact], giving per-test isolation for free.
/// </summary>
public class OrderIntegrationTests : IAsyncLifetime
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
            db, $"OrderWidget_{Guid.NewGuid():N}", 8m, qty);
        return product.ProductId;
    }

    [Fact]
    public async Task CreateOrder_ReturnsBadRequest_WhenCartEmpty()
    {
        var client = _factory.CreateClient();
        var user = await AuthTestHelper.RegisterAndLoginAsync(client);
        AuthTestHelper.SetBearerToken(client, user.AccessToken);

        var response = await client.PostAsync("/api/orders", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateOrder_Succeeds_FromSeededCart_ThenListAndGetById()
    {
        var productId = await SeedProductAsync(30);
        var client = _factory.CreateClient();
        var user = await AuthTestHelper.RegisterAndLoginAsync(client);
        AuthTestHelper.SetBearerToken(client, user.AccessToken);

        await client.PostAsJsonAsync("/api/cart/items",
            new AddCartItemRequestDto { ProductId = productId, Quantity = 2 });

        var createResponse = await client.PostAsync("/api/orders", null);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateOrderResponseDto>();
        created!.TotalAmount.Should().Be(16m);

        var listResponse = await client.GetAsync("/api/orders");
        var orders = await listResponse.Content.ReadFromJsonAsync<List<OrderResponseDto>>();
        orders.Should().ContainSingle(o => o.OrderId == created.OrderId);

        var getResponse = await client.GetAsync($"/api/orders/{created.OrderId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetOrderById_ReturnsNotFound_WhenOrderBelongsToAnotherUser()
    {
        var productId = await SeedProductAsync(30);
        var clientA = _factory.CreateClient();
        var userA = await AuthTestHelper.RegisterAndLoginAsync(clientA);
        AuthTestHelper.SetBearerToken(clientA, userA.AccessToken);
        await clientA.PostAsJsonAsync("/api/cart/items",
            new AddCartItemRequestDto { ProductId = productId, Quantity = 1 });
        var created = await (await clientA.PostAsync("/api/orders", null))
            .Content.ReadFromJsonAsync<CreateOrderResponseDto>();

        var clientB = _factory.CreateClient();
        var userB = await AuthTestHelper.RegisterAndLoginAsync(clientB);
        AuthTestHelper.SetBearerToken(clientB, userB.AccessToken);

        var response = await clientB.GetAsync($"/api/orders/{created!.OrderId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
