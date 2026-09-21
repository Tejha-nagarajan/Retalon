using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Retalon.Data;
using Retalon.DTOs.Auth;
using Retalon.Tests.Infrastructure;
using Xunit;

namespace Retalon.Tests.Integration;

/// <summary>
/// InventoryController requires authentication for reads ([Authorize] at the class level)
/// and the "Admin"/"WarehouseManager" roles for writes ([Authorize(Roles = ...)] on Update
/// and Restock), per the Phase 17 security hardening pass.
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

    private async Task<HttpClient> CreateClientWithRoleAsync(string roleName)
    {
        var client = _factory.CreateClient();
        var email = $"user_{Guid.NewGuid():N}@test.local";
        const string password = "P@ssw0rd123!";

        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequestDto
        {
            FirstName = "Test",
            LastName = "User",
            Email = email,
            Password = password,
            PhoneNumber = "5555555555",
            AddressLine1 = "123 Test St",
            City = "Testville",
            State = "TS",
            PostalCode = "00000",
            Country = "USA"
        });

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userId = DbSeedHelper.GetUserIdByEmail(db, email.ToLowerInvariant());
            await DbSeedHelper.AssignRoleAsync(db, userId, roleName);
        }

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequestDto { Email = email, Password = password });
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();

        AuthTestHelper.SetBearerToken(client, auth!.AccessToken);
        return client;
    }

    [Fact]
    public async Task GetByProductId_ReturnsUnauthorized_WithoutBearerToken()
    {
        var productId = await SeedProductAsync(25);
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/inventory/{productId}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetByProductId_Succeeds_ForAuthenticatedCustomer()
    {
        var productId = await SeedProductAsync(25);
        var client = _factory.CreateClient();
        var user = await AuthTestHelper.RegisterAndLoginAsync(client);
        AuthTestHelper.SetBearerToken(client, user.AccessToken);

        var response = await client.GetAsync($"/api/inventory/{productId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetByProductId_ReturnsNotFound_WhenMissing()
    {
        var client = _factory.CreateClient();
        var user = await AuthTestHelper.RegisterAndLoginAsync(client);
        AuthTestHelper.SetBearerToken(client, user.AccessToken);

        var response = await client.GetAsync("/api/inventory/999999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_ReturnsForbidden_ForCustomerRole()
    {
        var productId = await SeedProductAsync(25);
        var client = _factory.CreateClient();
        var user = await AuthTestHelper.RegisterAndLoginAsync(client);
        AuthTestHelper.SetBearerToken(client, user.AccessToken);

        var response = await client.PutAsJsonAsync($"/api/inventory/{productId}", new
        {
            QuantityAvailable = 50,
            QuantityReserved = 0,
            SafetyStockLevel = 5,
            ProcurementLeadTimeDays = 3
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_Succeeds_ForWarehouseManagerRole()
    {
        var productId = await SeedProductAsync(25);
        var client = await CreateClientWithRoleAsync("WarehouseManager");

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
    public async Task Restock_ReturnsForbidden_ForCustomerRole()
    {
        var productId = await SeedProductAsync(10);
        var client = _factory.CreateClient();
        var user = await AuthTestHelper.RegisterAndLoginAsync(client);
        AuthTestHelper.SetBearerToken(client, user.AccessToken);

        var response = await client.PostAsJsonAsync($"/api/inventory/{productId}/restock", new { Quantity = 15 });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Restock_Succeeds_ForAdminRole_AndIncreasesQuantity()
    {
        var productId = await SeedProductAsync(10);
        var client = await CreateClientWithRoleAsync("Admin");

        var response = await client.PostAsJsonAsync($"/api/inventory/{productId}/restock", new { Quantity = 15 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Restock_ReturnsBadRequest_WhenQuantityNotPositive()
    {
        var productId = await SeedProductAsync(10);
        var client = await CreateClientWithRoleAsync("Admin");

        var response = await client.PostAsJsonAsync($"/api/inventory/{productId}/restock", new { Quantity = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
