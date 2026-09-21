using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Retalon.DTOs.Products;
using Retalon.Models.Entities;
using Retalon.Services;
using Retalon.Tests.Infrastructure;
using Xunit;

namespace Retalon.Tests.Unit;

/// <summary>
/// Uses SqliteTestDatabase (a real relational provider), not the EF Core InMemory provider:
/// ImportAsync runs its upserts inside a real DB transaction (BeginTransactionAsync), which
/// the InMemory provider does not support.
/// </summary>
public class VendorProductImportServiceTests
{
    private static readonly string[] Headers =
    {
        "ExternalProductId", "Name", "Barcode", "Description", "ImageUrl",
        "Price", "Currency", "Category", "QuantityAvailable",
        "SafetyStockLevel", "ProcurementLeadTimeDays"
    };

    private static IFormFile BuildWorkbook(
        string externalProductId,
        string name,
        string category,
        decimal price,
        int? quantityAvailable,
        int? safetyStockLevel = null,
        int? procurementLeadTimeDays = null)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Products");

        for (var i = 0; i < Headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = Headers[i];
        }

        sheet.Cell(2, 1).Value = externalProductId;
        sheet.Cell(2, 2).Value = name;
        sheet.Cell(2, 3).Value = "";
        sheet.Cell(2, 4).Value = "";
        sheet.Cell(2, 5).Value = "";
        sheet.Cell(2, 6).Value = price;
        sheet.Cell(2, 7).Value = "USD";
        sheet.Cell(2, 8).Value = category;

        if (quantityAvailable.HasValue)
        {
            sheet.Cell(2, 9).Value = quantityAvailable.Value;
        }

        if (safetyStockLevel.HasValue)
        {
            sheet.Cell(2, 10).Value = safetyStockLevel.Value;
        }

        if (procurementLeadTimeDays.HasValue)
        {
            sheet.Cell(2, 11).Value = procurementLeadTimeDays.Value;
        }

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        return new FormFile(stream, 0, stream.Length, "file", "vendor-import.xlsx");
    }

    private static User SeedUser(Data.ApplicationDbContext db, Guid userId)
    {
        var user = new User
        {
            UserId = userId,
            Email = $"seeded_{userId:N}@test.local",
            PasswordHash = "hash",
            FirstName = "Seeded",
            LastName = "User",
            Address = "1 Test St",
            City = "Testville",
            PostalCode = "00000",
            Country = "USA",
            IsActive = true,
            CreatedDate = DateTime.UtcNow
        };
        db.Users.Add(user);
        db.SaveChanges();
        db.ChangeTracker.Clear();
        return user;
    }

    [Fact]
    public async Task ImportAsync_ExistingInventory_AddsToQuantityAvailable_RatherThanOverwriting()
    {
        using var sqlite = new SqliteTestDatabase();
        var db = sqlite.Context;
        var userId = Guid.NewGuid();
        SeedUser(db, userId);

        var category = new Category { Name = "Widgets" };
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        var product = new Product
        {
            CategoryId = category.CategoryId,
            ExternalProductId = "EXT-1",
            Name = "Widget",
            Price = 9.99m,
            Currency = "USD",
            CreatedDate = DateTime.UtcNow
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        db.Inventories.Add(new Inventory
        {
            ProductId = product.ProductId,
            QuantityAvailable = 10,
            SafetyStockLevel = 4,
            ProcurementLeadTimeDays = 2,
            LastUpdated = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var file = BuildWorkbook("EXT-1", "Widget", "Widgets", 9.99m, quantityAvailable: 5);
        var sut = new VendorProductImportService(db);

        var result = await sut.ImportAsync(file, userId);

        result.Failed.Should().Be(0);
        result.Updated.Should().Be(1);

        var inventory = db.Inventories.Single(i => i.ProductId == product.ProductId);
        inventory.QuantityAvailable.Should().Be(15);
    }

    [Fact]
    public async Task ImportAsync_ExistingInventory_PreservesSafetyStockLevel_WhenSheetOmitsValue()
    {
        using var sqlite = new SqliteTestDatabase();
        var db = sqlite.Context;
        var userId = Guid.NewGuid();
        SeedUser(db, userId);

        var category = new Category { Name = "Widgets" };
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        var product = new Product
        {
            CategoryId = category.CategoryId,
            ExternalProductId = "EXT-2",
            Name = "Gadget",
            Price = 19.99m,
            Currency = "USD",
            CreatedDate = DateTime.UtcNow
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        db.Inventories.Add(new Inventory
        {
            ProductId = product.ProductId,
            QuantityAvailable = 3,
            SafetyStockLevel = 7,
            ProcurementLeadTimeDays = 5,
            LastUpdated = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var file = BuildWorkbook(
            "EXT-2", "Gadget", "Widgets", 19.99m,
            quantityAvailable: 2, safetyStockLevel: null, procurementLeadTimeDays: null);
        var sut = new VendorProductImportService(db);

        await sut.ImportAsync(file, userId);

        var inventory = db.Inventories.Single(i => i.ProductId == product.ProductId);
        inventory.QuantityAvailable.Should().Be(5);
        inventory.SafetyStockLevel.Should().Be(7);
        inventory.ProcurementLeadTimeDays.Should().Be(5);
    }

    [Fact]
    public async Task ImportAsync_NewProduct_CreatesInventory_WithSheetQuantity()
    {
        using var sqlite = new SqliteTestDatabase();
        var db = sqlite.Context;
        var userId = Guid.NewGuid();
        SeedUser(db, userId);

        var file = BuildWorkbook("EXT-3", "Brand New Thing", "New Category", 4.5m, quantityAvailable: 8);
        var sut = new VendorProductImportService(db);

        var result = await sut.ImportAsync(file, userId);

        result.Inserted.Should().Be(1);

        var product = db.Products.Single(p => p.ExternalProductId == "EXT-3");
        var inventory = db.Inventories.Single(i => i.ProductId == product.ProductId);
        inventory.QuantityAvailable.Should().Be(8);
    }
}
