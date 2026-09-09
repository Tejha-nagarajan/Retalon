using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Retalon.DTOs.Auth;
using Retalon.Tests.Infrastructure;
using Xunit;

namespace Retalon.Tests.Integration;

/// <summary>
/// Covers the security hardening added in Program.cs/DTOs/controllers: the security-headers
/// middleware, the global [Authorize] fallback policy (with [AllowAnonymous] carve-outs on
/// public endpoints), and DataAnnotations validation on auth request DTOs.
/// </summary>
[Collection("Integration")]
public class SecurityHardeningIntegrationTests
{
    private readonly RetalonWebApplicationFactory _factory;

    public SecurityHardeningIntegrationTests(SharedFactoryFixture fixture)
    {
        _factory = fixture.Factory;
    }

    [Fact]
    public async Task Response_IncludesSecurityHeaders()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.Headers.TryGetValues("X-Content-Type-Options", out var contentTypeOptions).Should().BeTrue();
        contentTypeOptions!.Should().ContainSingle().Which.Should().Be("nosniff");

        response.Headers.TryGetValues("X-Frame-Options", out var frameOptions).Should().BeTrue();
        frameOptions!.Should().ContainSingle().Which.Should().Be("DENY");

        response.Headers.TryGetValues("Referrer-Policy", out var referrerPolicy).Should().BeTrue();
        referrerPolicy!.Should().ContainSingle().Which.Should().Be("no-referrer");

        response.Headers.TryGetValues("Permissions-Policy", out var permissionsPolicy).Should().BeTrue();
        permissionsPolicy!.Should().ContainSingle().Which.Should().Be("camera=(), microphone=(), geolocation=()");
    }

    [Fact]
    public async Task ProtectedEndpoint_ReturnsUnauthorized_WithoutBearerToken()
    {
        // InventoryController.GetByProductId has no [AllowAnonymous]/[Authorize] of its own,
        // so it now relies entirely on the global fallback policy (RequireAuthenticatedUser).
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/inventory/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AllowAnonymousEndpoint_RemainsReachable_WithoutBearerToken()
    {
        // ProductController.Search is explicitly [AllowAnonymous], so it must still work
        // even though the global fallback policy now requires authentication by default.
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/products/search?query=anything");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Register_ReturnsBadRequest_WhenEmailIsInvalid()
    {
        var client = _factory.CreateClient();
        var request = new RegisterRequestDto
        {
            FirstName = "Test",
            LastName = "User",
            Email = "not-an-email",
            Password = "P@ssw0rd123!",
            AddressLine1 = "123 Test St",
            City = "Testville",
            PostalCode = "00000",
            Country = "USA"
        };

        var response = await client.PostAsJsonAsync("/api/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_ReturnsBadRequest_WhenPasswordShorterThanMinimumLength()
    {
        var client = _factory.CreateClient();
        var request = new LoginRequestDto { Email = "someone@test.local", Password = "short" };

        var response = await client.PostAsJsonAsync("/api/auth/login", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
