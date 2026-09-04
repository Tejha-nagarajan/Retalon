using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Retalon.Data;
using Retalon.Models.Entities;

namespace Retalon.Tests.Infrastructure;

public static class TestDbContextFactory
{
    public static ApplicationDbContext CreateInMemoryContext(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        var context = new ApplicationDbContext(options);
        context.Database.EnsureCreated();

        SeedRolesIfMissing(context);

        return context;
    }

    public static void SeedRolesIfMissing(ApplicationDbContext context)
    {
        if (!context.Roles.Any(r => r.Name == "Customer"))
        {
            context.Roles.Add(new Role
            {
                RoleId = Guid.NewGuid(),
                Name = "Customer",
                Description = "Customer user"
            });
        }

        if (!context.Roles.Any(r => r.Name == "Admin"))
        {
            context.Roles.Add(new Role
            {
                RoleId = Guid.NewGuid(),
                Name = "Admin",
                Description = "System administrator"
            });
        }

        if (!context.Roles.Any(r => r.Name == "WarehouseManager"))
        {
            context.Roles.Add(new Role
            {
                RoleId = Guid.NewGuid(),
                Name = "WarehouseManager",
                Description = "Warehouse manager"
            });
        }

        context.SaveChanges();
    }
}

public sealed class SqliteTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public ApplicationDbContext Context { get; }

    public SqliteTestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        // Entity configurations use SQL Server's GETUTCDATE() as a column default, which
        // SQLite has no built-in equivalent for. Register it as a user-defined function so
        // inserts that omit a CreatedDate/Timestamp (relying on the DB default) still work.
        _connection.CreateFunction("GETUTCDATE", () => DateTime.UtcNow);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        Context = new ApplicationDbContext(options);
        Context.Database.EnsureCreated();

        TestDbContextFactory.SeedRolesIfMissing(Context);
    }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}
