using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Retalon.DTOs.Auth;
using Retalon.Tests.Infrastructure;
using Xunit;

namespace Retalon.Tests.Integration;

/// <summary>
/// "LoginPolicy" is a fixed-window limiter (5 requests/minute) applied to
/// POST /api/auth/login. This uses the isolated RateLimitingFactoryFixture/collection
/// so its global limiter state never bleeds into (or is polluted by) the shared
/// "Integration" collection's Auth tests.
/// </summary>
[Collection("RateLimiting")]
public class RateLimitingIntegrationTests
{
    private readonly RetalonWebApplicationFactory _factory;

    public RateLimitingIntegrationTests(RateLimitingFactoryFixture fixture)
    {
        _factory = fixture.Factory;
    }

    [Fact]
    public async Task Login_ReturnsTooManyRequests_AfterExceedingFixedWindowLimit()
    {
        var client = _factory.CreateClient();
        var request = new LoginRequestDto { Email = "nobody@test.local", Password = "wrong" };

        HttpStatusCode? lastStatus = null;
        for (var i = 0; i < 6; i++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", request);
            lastStatus = response.StatusCode;
        }

        lastStatus.Should().Be(HttpStatusCode.TooManyRequests);
    }
}
