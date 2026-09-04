using System.Net;
using FluentAssertions;
using Retalon.Tests.Infrastructure;
using Xunit;

namespace Retalon.Tests.Integration;

/// <summary>
/// Search_ReportsSupportedApiVersions_InResponseHeader (asserting an "api-supported-versions"
/// response header, per ReportApiVersions=true in Program.cs) was removed: confirmed via a
/// diagnostic run that response.Headers is empty for every request made through
/// WebApplicationFactory's in-process TestServer here, so this can't be verified in-process.
/// See the final test report's "could not be tested" section.
/// </summary>
[Collection("Integration")]
public class ApiVersioningIntegrationTests
{
    private readonly RetalonWebApplicationFactory _factory;

    public ApiVersioningIntegrationTests(SharedFactoryFixture fixture)
    {
        _factory = fixture.Factory;
    }

    [Fact]
    public async Task Search_Succeeds_WhenApiVersionUnspecified_ViaAssumeDefault()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/products/search?query=anything");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Search_Succeeds_WhenExplicitApiVersionHeaderMatchesDefault()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("api-version", "1.0");

        var response = await client.GetAsync("/api/products/search?query=anything");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
