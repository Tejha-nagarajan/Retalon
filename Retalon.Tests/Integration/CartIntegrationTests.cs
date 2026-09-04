using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Retalon.Data;
using Retalon.DTOs.Cart;
using Retalon.Tests.Infrastructure;
using Xunit;

namespace Retalon.Tests.Integration;

/// <summary>
/// Not part of the shared "Integration" collection: "LoginPolicy" is a global (unpartitioned)
/// 5-requests/minute limiter, and this class alone issues 7+ real logins across its tests, so
/// each test gets its own factory/host (a fresh in-memory limiter) via IAsyncLifetime — xUnit
/// creates a new instance of the test class per [Fact], giving per-test isolation for free.
/// </summary>
public class CartIntegrationTests : IAsyncLifetime
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
            db, $"CartWidget_{Guid.NewGuid():N}", 12.5m, qty);
        return product.ProductId;
    }

    [Fact]
    public async Task GetCart_CreatesEmptyCart_ForNewUser()
    {
        var client = _factory.CreateClient();
        var user = await AuthTestHelper.RegisterAndLoginAsync(client);
        AuthTestHelper.SetBearerToken(client, user.AccessToken);

        var response = await client.GetAsync("/api/cart");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var cart = await response.Content.ReadFromJsonAsync<CartResponseDto>();
        cart!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task AddItem_ThenGetCart_ShowsItem()
    {
        var productId = await SeedProductAsync(20);
        var client = _factory.CreateClient();
        var user = await AuthTestHelper.RegisterAndLoginAsync(client);
        AuthTestHelper.SetBearerToken(client, user.AccessToken);

        var addResponse = await client.PostAsJsonAsync("/api/cart/items",
            new AddCartItemRequestDto { ProductId = productId, Quantity = 2 });

        addResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var cartResponse = await client.GetAsync("/api/cart");
        var cart = await cartResponse.Content.ReadFromJsonAsync<CartResponseDto>();
        cart!.Items.Should().ContainSingle(i => i.ProductId == productId && i.Quantity == 2);
    }

    [Fact]
    public async Task AddItem_ReturnsBadRequest_WhenInsufficientInventory()
    {
        var productId = await SeedProductAsync(1);
        var client = _factory.CreateClient();
        var user = await AuthTestHelper.RegisterAndLoginAsync(client);
        AuthTestHelper.SetBearerToken(client, user.AccessToken);

        var response = await client.PostAsJsonAsync("/api/cart/items",
            new AddCartItemRequestDto { ProductId = productId, Quantity = 999 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Cart_IsIsolated_BetweenDifferentUsers()
    {
        var productId = await SeedProductAsync(20);
        var clientA = _factory.CreateClient();
        var userA = await AuthTestHelper.RegisterAndLoginAsync(clientA);
        AuthTestHelper.SetBearerToken(clientA, userA.AccessToken);
        await clientA.PostAsJsonAsync("/api/cart/items",
            new AddCartItemRequestDto { ProductId = productId, Quantity = 1 });

        var clientB = _factory.CreateClient();
        var userB = await AuthTestHelper.RegisterAndLoginAsync(clientB);
        AuthTestHelper.SetBearerToken(clientB, userB.AccessToken);

        var cartBResponse = await clientB.GetAsync("/api/cart");
        var cartB = await cartBResponse.Content.ReadFromJsonAsync<CartResponseDto>();

        cartB!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveItem_ReturnsNotFound_WhenItemBelongsToAnotherUser()
    {
        var productId = await SeedProductAsync(20);
        var clientA = _factory.CreateClient();
        var userA = await AuthTestHelper.RegisterAndLoginAsync(clientA);
        AuthTestHelper.SetBearerToken(clientA, userA.AccessToken);
        await clientA.PostAsJsonAsync("/api/cart/items",
            new AddCartItemRequestDto { ProductId = productId, Quantity = 1 });
        var cartA = await (await clientA.GetAsync("/api/cart")).Content.ReadFromJsonAsync<CartResponseDto>();
        var cartItemId = cartA!.Items.Single().CartItemId;

        var clientB = _factory.CreateClient();
        var userB = await AuthTestHelper.RegisterAndLoginAsync(clientB);
        AuthTestHelper.SetBearerToken(clientB, userB.AccessToken);

        var response = await clientB.DeleteAsync($"/api/cart/items/{cartItemId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
