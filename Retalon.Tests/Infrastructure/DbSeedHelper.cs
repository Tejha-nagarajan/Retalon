using Retalon.Data;
using Retalon.Models.Entities;
using Retalon.Models.Enums;

namespace Retalon.Tests.Infrastructure;

/// <summary>
/// Seeds Category/Product/Inventory rows directly via ApplicationDbContext for integration
/// test setup, so tests don't depend on a product-creation endpoint that doesn't exist yet.
/// </summary>
public static class DbSeedHelper
{
    public static async Task<Product> SeedProductWithInventoryAsync(
        ApplicationDbContext db,
        string name,
        decimal price,
        int quantityAvailable,
        int quantityReserved = 0,
        string? barcode = null,
        string categoryName = "Test Category",
        int safetyStockLevel = 5,
        int procurementLeadTimeDays = 3,
        ProductStatus status = ProductStatus.Active,
        bool isDeleted = false)
    {
        var category = db.Categories.FirstOrDefault(c => c.Name == categoryName);
        if (category is null)
        {
            category = new Category { Name = categoryName, Description = "Seeded for tests" };
            db.Categories.Add(category);
            await db.SaveChangesAsync();
        }

        var product = new Product
        {
            CategoryId = category.CategoryId,
            Name = name,
            Barcode = barcode,
            Description = $"{name} (seeded)",
            Price = price,
            Currency = "USD",
            ProductStatus = status,
            IsDeleted = isDeleted,
            CreatedDate = DateTime.UtcNow
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var inventory = new Inventory
        {
            ProductId = product.ProductId,
            QuantityAvailable = quantityAvailable,
            QuantityReserved = quantityReserved,
            SafetyStockLevel = safetyStockLevel,
            ProcurementLeadTimeDays = procurementLeadTimeDays,
            LastUpdated = DateTime.UtcNow
        };
        db.Inventories.Add(inventory);
        await db.SaveChangesAsync();

        return product;
    }

    public static Guid GetUserIdByEmail(ApplicationDbContext db, string email)
    {
        return db.Users.Single(u => u.Email == email).UserId;
    }
}
