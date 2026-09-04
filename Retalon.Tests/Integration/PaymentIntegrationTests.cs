using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Retalon.Data;
using Retalon.DTOs.Cart;
using Retalon.DTOs.Orders;
using Retalon.DTOs.Payments;
using Retalon.Tests.Infrastructure;
using Xunit;

namespace Retalon.Tests.Integration;

/// <summary>
/// appsettings.json ships with a blank Stripe SecretKey/WebhookSecret, and Testing
/// environment never loads User Secrets, so every Stripe-touching endpoint here hits
/// PaymentService's "not configured" guard clause deterministically — no real Stripe
/// network call is ever made.
/// </summary>
/// <summary>
/// Not part of the shared "Integration" collection: "LoginPolicy" is a global (unpartitioned)
/// 5-requests/minute limiter shared with several other auth-heavy test classes, so this class
/// gets its own factory/host per test (a fresh in-memory limiter) via IAsyncLifetime — xUnit
/// creates a new instance of the test class per [Fact], giving per-test isolation for free.
/// </summary>
public class PaymentIntegrationTests : IAsyncLifetime
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
            db, $"PayWidget_{Guid.NewGuid():N}", 7m, qty);
        return product.ProductId;
    }

    private async Task<(HttpClient client, long orderId)> CreateAuthenticatedOrderAsync()
    {
        var productId = await SeedProductAsync(30);
        var client = _factory.CreateClient();
        var user = await AuthTestHelper.RegisterAndLoginAsync(client);
        AuthTestHelper.SetBearerToken(client, user.AccessToken);

        await client.PostAsJsonAsync("/api/cart/items",
            new AddCartItemRequestDto { ProductId = productId, Quantity = 1 });
        var created = await (await client.PostAsync("/api/orders", null))
            .Content.ReadFromJsonAsync<CreateOrderResponseDto>();

        return (client, created!.OrderId);
    }

    [Fact]
    public async Task CreatePayment_ReturnsBadRequest_WhenStripeNotConfigured()
    {
        var (client, orderId) = await CreateAuthenticatedOrderAsync();

        var response = await client.PostAsJsonAsync("/api/payments/create",
            new CreatePaymentRequestDto { OrderId = orderId });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("not configured");
    }

    [Fact]
    public async Task CreatePayment_ReturnsNotFound_WhenOrderDoesNotExist()
    {
        var client = _factory.CreateClient();
        var user = await AuthTestHelper.RegisterAndLoginAsync(client);
        AuthTestHelper.SetBearerToken(client, user.AccessToken);

        var response = await client.PostAsJsonAsync("/api/payments/create",
            new CreatePaymentRequestDto { OrderId = 999999999 });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ConfirmTestPayment_ReturnsNotFound_WhenPaymentDoesNotExist()
    {
        var client = _factory.CreateClient();
        var user = await AuthTestHelper.RegisterAndLoginAsync(client);
        AuthTestHelper.SetBearerToken(client, user.AccessToken);

        var response = await client.PostAsJsonAsync("/api/payments/confirm-test",
            new ConfirmTestPaymentRequestDto { PaymentId = 999999999 });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreatePayment_ReturnsUnauthorized_WithoutBearerToken()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/payments/create",
            new CreatePaymentRequestDto { OrderId = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
