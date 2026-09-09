using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Retalon.Data;
using Retalon.DTOs.Products;
using Retalon.Models.Entities;
using Retalon.Models.Enums;
using Retalon.Services.Interfaces;

namespace Retalon.Services;

public class VendorProductImportService : IVendorProductImportService
{
    private readonly ApplicationDbContext _context;

    public VendorProductImportService(
        ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<VendorProductImportResultDto> ImportAsync(
        IFormFile file,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
        {
            throw new ArgumentException("Excel file is required.");
        }

        if (!Path.GetExtension(file.FileName)
            .Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Only .xlsx Excel files are supported.");
        }

        using var stream = file.OpenReadStream();
        using var workbook = new XLWorkbook(stream);

        var worksheet = workbook.Worksheets.FirstOrDefault();

        if (worksheet == null)
        {
            throw new ArgumentException(
                "The Excel file does not contain a worksheet.");
        }

        var headerRow = worksheet.FirstRowUsed();

        if (headerRow == null)
        {
            throw new ArgumentException(
                "The Excel worksheet is empty.");
        }

        var requiredHeaders = new[]
        {
            "ExternalProductId",
            "Name",
            "Barcode",
            "Description",
            "ImageUrl",
            "Price",
            "Currency",
            "Category",
            "QuantityAvailable",
            "SafetyStockLevel",
            "ProcurementLeadTimeDays"
        };

        var headers = headerRow
            .CellsUsed()
            .Select(cell => cell.GetString().Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingHeaders = requiredHeaders
            .Where(header => !headers.Contains(header))
            .ToList();

        if (missingHeaders.Count > 0)
        {
            throw new ArgumentException(
                $"Missing required Excel columns: " +
                $"{string.Join(", ", missingHeaders)}");
        }

        var rows = new List<VendorProductImportRowDto>();

        var firstDataRow = headerRow.RowNumber();

        var lastDataRow = worksheet.LastRowUsed()?.RowNumber()
            ?? firstDataRow;

        for (var rowNumber = firstDataRow + 1;
             rowNumber <= lastDataRow;
             rowNumber++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var row = worksheet.Row(rowNumber);

            if (row.CellsUsed()
                .All(cell => string.IsNullOrWhiteSpace(cell.GetString())))
            {
                continue;
            }

            var productRow = new VendorProductImportRowDto
            {
                ExternalProductId = row.Cell(1).GetString().Trim(),
                Name = row.Cell(2).GetString().Trim(),
                Barcode = row.Cell(3).GetString().Trim(),
                Description = row.Cell(4).GetString().Trim(),
                ImageUrl = row.Cell(5).GetString().Trim(),
                Price = row.Cell(6).GetValue<decimal?>(),
                Currency = row.Cell(7).GetString().Trim(),
                Category = row.Cell(8).GetString().Trim(),
                QuantityAvailable = row.Cell(9).GetValue<int?>(),
                SafetyStockLevel = row.Cell(10).GetValue<int?>(),
                ProcurementLeadTimeDays = row.Cell(11).GetValue<int?>()
            };

            rows.Add(productRow);
        }

        var errors = new List<VendorProductImportErrorDto>();

        var validRows =
            new List<(VendorProductImportRowDto Product, int RowNumber)>();

        for (var index = 0; index < rows.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var product = rows[index];

            var rowNumber = firstDataRow + 1 + index;

            var rowErrors = new List<string>();

            if (string.IsNullOrWhiteSpace(product.Name))
            {
                rowErrors.Add("Name is required.");
            }

            if (string.IsNullOrWhiteSpace(product.Category))
            {
                rowErrors.Add("Category is required.");
            }

            if (!product.Price.HasValue)
            {
                rowErrors.Add("Price is required.");
            }
            else if (product.Price.Value < 0)
            {
                rowErrors.Add("Price cannot be negative.");
            }

            if (product.QuantityAvailable.HasValue &&
                product.QuantityAvailable.Value < 0)
            {
                rowErrors.Add(
                    "QuantityAvailable cannot be negative.");
            }

            if (product.SafetyStockLevel.HasValue &&
                product.SafetyStockLevel.Value < 0)
            {
                rowErrors.Add(
                    "SafetyStockLevel cannot be negative.");
            }

            if (product.ProcurementLeadTimeDays.HasValue &&
                product.ProcurementLeadTimeDays.Value < 0)
            {
                rowErrors.Add(
                    "ProcurementLeadTimeDays cannot be negative.");
            }

            if (rowErrors.Count > 0)
            {
                errors.Add(new VendorProductImportErrorDto
                {
                    RowNumber = rowNumber,
                    Error = string.Join(" ", rowErrors)
                });

                continue;
            }

            validRows.Add((product, rowNumber));
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                cancellationToken);

        try
        {
            var importBatch = new ImportBatch
            {
                UserId = userId,
                FileName = Path.GetFileName(file.FileName),
                Status = ImportBatchStatus.Processing,
                TotalRows = rows.Count,
                FailedRows = errors.Count,
                StartedDate = DateTime.UtcNow
            };

            _context.ImportBatches.Add(importBatch);

            await _context.SaveChangesAsync(cancellationToken);

            var categoryNames = validRows
                .Select(x => x.Product.Category!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var existingCategories = await _context.Categories
                .Where(x => categoryNames.Contains(x.Name))
                .ToListAsync(cancellationToken);

            var categoriesByName = existingCategories
                .ToDictionary(
                    x => x.Name,
                    x => x,
                    StringComparer.OrdinalIgnoreCase);

            foreach (var categoryName in categoryNames)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (categoriesByName.ContainsKey(categoryName))
                {
                    continue;
                }

                var category = new Category
                {
                    Name = categoryName
                };

                _context.Categories.Add(category);

                categoriesByName[categoryName] = category;
            }

            await _context.SaveChangesAsync(cancellationToken);

            var externalProductIds = validRows
                .Select(x => x.Product.ExternalProductId)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var barcodes = validRows
                .Select(x => x.Product.Barcode)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var existingProducts = await _context.Products
                .Where(x =>
                    (!string.IsNullOrWhiteSpace(x.ExternalProductId) &&
                     externalProductIds.Contains(x.ExternalProductId)) ||
                    (!string.IsNullOrWhiteSpace(x.Barcode) &&
                     barcodes.Contains(x.Barcode)))
                .ToListAsync(cancellationToken);

            var productsByExternalId = existingProducts
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x.ExternalProductId))
                .ToDictionary(
                    x => x.ExternalProductId!,
                    x => x,
                    StringComparer.OrdinalIgnoreCase);

            var productsByBarcode = existingProducts
                .Where(x => !string.IsNullOrWhiteSpace(x.Barcode))
                .ToDictionary(
                    x => x.Barcode!,
                    x => x,
                    StringComparer.OrdinalIgnoreCase);

            var inserted = 0;
            var updated = 0;

            foreach (var item in validRows)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var productRow = item.Product;

                Product? existingProduct = null;

                if (!string.IsNullOrWhiteSpace(
                    productRow.ExternalProductId) &&
                    productsByExternalId.TryGetValue(
                        productRow.ExternalProductId.Trim(),
                        out var externalMatch))
                {
                    existingProduct = externalMatch;
                }
                else if (!string.IsNullOrWhiteSpace(productRow.Barcode) &&
                         productsByBarcode.TryGetValue(
                             productRow.Barcode.Trim(),
                             out var barcodeMatch))
                {
                    existingProduct = barcodeMatch;
                }

                var category =
                    categoriesByName[productRow.Category!.Trim()];

                if (existingProduct == null)
                {
                    var newProduct = new Product
                    {
                        CategoryId = category.CategoryId,

                        ExternalProductId =
                            string.IsNullOrWhiteSpace(
                                productRow.ExternalProductId)
                                ? null
                                : productRow.ExternalProductId.Trim(),

                        Name = productRow.Name!.Trim(),

                        Barcode =
                            string.IsNullOrWhiteSpace(
                                productRow.Barcode)
                                ? null
                                : productRow.Barcode.Trim(),

                        Description = productRow.Description,
                        ImageUrl = productRow.ImageUrl,
                        Price = productRow.Price!.Value,

                        Currency =
                            string.IsNullOrWhiteSpace(
                                productRow.Currency)
                                ? "USD"
                                : productRow.Currency.Trim(),

                        ImportSource = "VendorBulkImport",

                        ProductStatus = ProductStatus.Active,

                        IsDeleted = false,

                        CreatedDate = DateTime.UtcNow
                    };

                    _context.Products.Add(newProduct);

                    inserted++;

                    if (!string.IsNullOrWhiteSpace(
                        newProduct.ExternalProductId))
                    {
                        productsByExternalId[
                            newProduct.ExternalProductId] = newProduct;
                    }

                    if (!string.IsNullOrWhiteSpace(
                        newProduct.Barcode))
                    {
                        productsByBarcode[
                            newProduct.Barcode] = newProduct;
                    }
                }
                else
                {
                    existingProduct.CategoryId =
                        category.CategoryId;

                    existingProduct.Name =
                        productRow.Name!.Trim();

                    existingProduct.Barcode =
                        string.IsNullOrWhiteSpace(productRow.Barcode)
                            ? existingProduct.Barcode
                            : productRow.Barcode.Trim();

                    existingProduct.Description =
                        productRow.Description;

                    existingProduct.ImageUrl =
                        productRow.ImageUrl;

                    existingProduct.Price =
                        productRow.Price!.Value;

                    existingProduct.Currency =
                        string.IsNullOrWhiteSpace(productRow.Currency)
                            ? existingProduct.Currency
                            : productRow.Currency.Trim();

                    existingProduct.ImportSource =
                        "VendorBulkImport";

                    existingProduct.IsDeleted = false;

                    existingProduct.LastUpdated =
                        DateTime.UtcNow;

                    updated++;
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            /*
             * Inventory
             */
            var productIds = validRows
                .Select(item =>
                {
                    var productRow = item.Product;

                    if (!string.IsNullOrWhiteSpace(
                        productRow.ExternalProductId) &&
                        productsByExternalId.TryGetValue(
                            productRow.ExternalProductId.Trim(),
                            out var externalProduct))
                    {
                        return externalProduct.ProductId;
                    }

                    if (!string.IsNullOrWhiteSpace(productRow.Barcode) &&
                        productsByBarcode.TryGetValue(
                            productRow.Barcode.Trim(),
                            out var barcodeProduct))
                    {
                        return barcodeProduct.ProductId;
                    }

                    return 0L;
                })
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            var existingInventories = await _context.Inventories
                .Where(x => productIds.Contains(x.ProductId))
                .ToListAsync(cancellationToken);

            var inventoriesByProductId = existingInventories
                .ToDictionary(
                    x => x.ProductId,
                    x => x);

            foreach (var item in validRows)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var productRow = item.Product;

                Product? product = null;

                if (!string.IsNullOrWhiteSpace(
                    productRow.ExternalProductId) &&
                    productsByExternalId.TryGetValue(
                        productRow.ExternalProductId.Trim(),
                        out var externalProduct))
                {
                    product = externalProduct;
                }
                else if (!string.IsNullOrWhiteSpace(productRow.Barcode) &&
                         productsByBarcode.TryGetValue(
                             productRow.Barcode.Trim(),
                             out var barcodeProduct))
                {
                    product = barcodeProduct;
                }

                if (product == null)
                {
                    continue;
                }

                if (inventoriesByProductId.TryGetValue(
                    product.ProductId,
                    out var inventory))
                {
                    inventory.QuantityAvailable =
                        productRow.QuantityAvailable ?? 0;

                    inventory.SafetyStockLevel =
                        productRow.SafetyStockLevel ?? 0;

                    inventory.ProcurementLeadTimeDays =
                        productRow.ProcurementLeadTimeDays ?? 0;

                    inventory.LastUpdated =
                        DateTime.UtcNow;
                }
                else
                {
                    var newInventory = new Inventory
                    {
                        ProductId = product.ProductId,

                        QuantityAvailable =
                            productRow.QuantityAvailable ?? 0,

                        QuantityReserved = 0,

                        SafetyStockLevel =
                            productRow.SafetyStockLevel ?? 0,

                        ProcurementLeadTimeDays =
                            productRow.ProcurementLeadTimeDays ?? 0,

                        LastUpdated = DateTime.UtcNow
                    };

                    _context.Inventories.Add(newInventory);

                    inventoriesByProductId[
                        product.ProductId] = newInventory;
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            importBatch.InsertedRows = inserted;
            importBatch.UpdatedRows = updated;
            importBatch.FailedRows = errors.Count;

            importBatch.Status = errors.Count == 0
                ? ImportBatchStatus.Completed
                : ImportBatchStatus.CompletedWithErrors;

            importBatch.CompletedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return new VendorProductImportResultDto
            {
                Success = errors.Count == 0,

                ImportBatchId =
                    importBatch.ImportBatchId,

                TotalRows = rows.Count,

                Inserted = inserted,

                Updated = updated,

                Failed = errors.Count,

                Status = errors.Count == 0
                    ? "Completed"
                    : "CompletedWithErrors",

                Errors = errors
            };
        }
        catch
        {
            await transaction.RollbackAsync(
                cancellationToken);

            throw;
        }
    }
}