using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Retalon.DTOs.Auth;
using Retalon.Tests.Infrastructure;
using Xunit;

namespace Retalon.Tests.Integration;

/// <summary>
/// Not part of the shared "Integration" collection: "LoginPolicy" is a global (unpartitioned)
/// 5-requests/minute limiter, and this class alone issues 5+ real logins across its tests, so
/// each test gets its own factory/host (a fresh in-memory limiter) via IAsyncLifetime — xUnit
/// creates a new instance of the test class per [Fact], giving per-test isolation for free.
/// </summary>
public class AuthIntegrationTests : IAsyncLifetime
{
    private readonly RetalonWebApplicationFactory _factory = new();

    public Task InitializeAsync() => _factory.InitializeDatabaseAsync();

    public async Task DisposeAsync()
    {
        await _factory.DisposeDatabaseAsync();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Register_ThenLogin_ReturnsTokens()
    {
        var client = _factory.CreateClient();
        var user = await AuthTestHelper.RegisterAndLoginAsync(client);

        user.AccessToken.Should().NotBeNullOrWhiteSpace();
        user.RefreshToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_WhenPasswordWrong()
    {
        var client = _factory.CreateClient();
        var email = $"user_{Guid.NewGuid():N}@test.local";
        await AuthTestHelper.RegisterAndLoginAsync(client, email);

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequestDto { Email = email, Password = "WrongPassword123!" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_ReturnsNewTokens_WhenRefreshTokenValid()
    {
        var client = _factory.CreateClient();
        var user = await AuthTestHelper.RegisterAndLoginAsync(client);

        var response = await client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshTokenRequestDto { RefreshToken = user.RefreshToken });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        auth!.AccessToken.Should().NotBeNullOrWhiteSpace();
        auth.RefreshToken.Should().NotBe(user.RefreshToken);
    }

    [Fact]
    public async Task Logout_RevokesRefreshToken()
    {
        var client = _factory.CreateClient();
        var user = await AuthTestHelper.RegisterAndLoginAsync(client);

        var logoutResponse = await client.PostAsJsonAsync("/api/auth/logout",
            new LogoutRequestDto { RefreshToken = user.RefreshToken });
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var refreshResponse = await client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshTokenRequestDto { RefreshToken = user.RefreshToken });
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_ReturnsUnauthorized_WithoutBearerToken()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/cart");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
