using Xunit;

namespace Retalon.Tests.Infrastructure;

/// <summary>
/// One RetalonWebApplicationFactory + one dedicated LocalDB test database, shared by every
/// integration test class in the "Integration" collection. xUnit runs classes within the
/// same collection sequentially, so tests can safely share this one database.
/// </summary>
public class SharedFactoryFixture : IAsyncLifetime
{
    public RetalonWebApplicationFactory Factory { get; } = new();

    public async Task InitializeAsync()
    {
        await Factory.InitializeDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeDatabaseAsync();
        await Factory.DisposeAsync();
    }
}

[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<SharedFactoryFixture>
{
}

/// <summary>
/// A separate factory/database, isolated in its own xUnit collection, for tests that need
/// fresh rate-limiter state (the fixed-window limiters share process-wide state with no
/// partition key, so they must not share a host with other Auth tests).
/// </summary>
public class RateLimitingFactoryFixture : IAsyncLifetime
{
    public RetalonWebApplicationFactory Factory { get; } = new();

    public async Task InitializeAsync()
    {
        await Factory.InitializeDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeDatabaseAsync();
        await Factory.DisposeAsync();
    }
}

[CollectionDefinition("RateLimiting")]
public class RateLimitingCollection : ICollectionFixture<RateLimitingFactoryFixture>
{
}
