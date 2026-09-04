using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Retalon.DTOs.Auth;
using Retalon.Tests.Infrastructure;
using Xunit;

namespace Retalon.Tests.Integration;

/// <summary>
/// AuthController.Refresh has no try/catch around RefreshTokenAsync, so an invalid
/// refresh token's UnauthorizedAccessException propagates unhandled to
/// GlobalExceptionHandler, which maps it to 401 with a ProblemDetails-shaped body.
/// Note: the handler writes that body via HttpResponse.WriteAsJsonAsync(problemDetails),
/// which serializes with Content-Type "application/json" (it never sets the special
/// "application/problem+json" media type) - confirmed empirically, asserted as-is below.
/// </summary>
[Collection("Integration")]
public class ErrorHandlingIntegrationTests
{
    private readonly RetalonWebApplicationFactory _factory;

    public ErrorHandlingIntegrationTests(SharedFactoryFixture fixture)
    {
        _factory = fixture.Factory;
    }

    [Fact]
    public async Task Refresh_WithGarbageToken_Returns401ProblemJson_ViaGlobalExceptionHandler()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshTokenRequestDto { RefreshToken = "not-a-real-token" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("\"status\":401");
    }
}
