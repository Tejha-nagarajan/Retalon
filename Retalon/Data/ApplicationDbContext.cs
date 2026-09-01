using Microsoft.EntityFrameworkCore;
using Retalon.Models.Entities;
namespace Retalon.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // DbSets for your entities
    public DbSet<User> Users { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<UserRole> UserRoles { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }

    public DbSet<Category> Categories { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<Inventory> Inventories { get; set; }

    public DbSet<Cart> Carts { get; set; }
    public DbSet<CartItem> CartItems { get; set; }

    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }
    public DbSet<Payment> Payment { get; set; }

    public DbSet<Procurement> Procurements { get; set; }

    public DbSet<Notification> Notifications { get; set; }
    public DbSet<AuditLog> auditLogs { get; set; }
    public DbSet<SecurityEvent> SecurityEvents { get; set; }
    public DbSet<SearchHistory> SearchHistories { get; set; }

    // Apply Fluent API configurations

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}