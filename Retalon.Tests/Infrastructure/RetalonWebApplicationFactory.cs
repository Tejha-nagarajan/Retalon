using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Retalon.Data;
using Retalon.Services.Interfaces;

namespace Retalon.Tests.Infrastructure;

/// <summary>
/// Boots the real Retalon app under ASPNETCORE_ENVIRONMENT=Testing, so User Secrets
/// (real Stripe/Gmail credentials) are never loaded — only appsettings.json's blank
/// placeholders apply. Points at a dedicated, unique LocalDB database (never RetalonDb)
/// and swaps the real Open Food Facts HTTP client for a deterministic fake.
/// </summary>
public class RetalonWebApplicationFactory : WebApplicationFactory<Program>
{
    public string DatabaseName { get; } = $"RetalonTestDb_{Guid.NewGuid():N}";

    private string ConnectionString =>
        $"Server=(localdb)\\MSSQLLocalDB;Database={DatabaseName};Trusted_Connection=True;TrustServerCertificate=True;";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            var overrides = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = ConnectionString
            };

            config.AddInMemoryCollection(overrides);
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IOpenFoodFactsService>();
            services.AddSingleton<IOpenFoodFactsService, FakeOpenFoodFactsService>();
        });
    }

    /// <summary>
    /// Program.cs registers a Hangfire recurring job during host startup, which requires
    /// the target SQL Server database and schema to already exist. Since accessing Services
    /// is what triggers host startup, both must be created via a standalone DbContext BEFORE
    /// Services is ever touched — otherwise Hangfire's startup connection either fails outright
    /// (database missing) or the app's own queries fail with "Invalid object name" (database
    /// exists but EF's EnsureCreated is a no-op against an already-existing empty database).
    /// </summary>
    private ApplicationDbContext CreateStandaloneDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new ApplicationDbContext(options);
    }

    public async Task InitializeDatabaseAsync()
    {
        using var db = CreateStandaloneDbContext();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeDatabaseAsync()
    {
        using var db = CreateStandaloneDbContext();
        await db.Database.EnsureDeletedAsync();
    }
}
