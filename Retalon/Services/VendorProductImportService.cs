using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Retalon.Data;
using Retalon.DTOs.Products;
using Retalon.Entities;
using Retalon.Models.Entities;
using Retalon.Models.Enums;
using Retalon.Services.Interfaces;

namespace Retalon.Services;

public class VendorProductImportService : IVendorProductImportService
{
    private const int BatchSize = 500;

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

        /*
         * Read Excel rows.
         *
         * We keep the actual Excel row number together with
         * the DTO so blank rows do not cause incorrect row numbers.
         */
        var rows =
            new List<(VendorProductImportRowDto Product, int RowNumber)>();

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
                ProcurementLeadTimeDays =
                    row.Cell(11).GetValue<int?>()
            };

            rows.Add((productRow, rowNumber));
        }

        /*
         * Validate the complete file first.
         *
         * Validation failures are kept aside and will never
         * prevent valid rows from being imported.
         */
        var errors = new List<VendorProductImportErrorDto>();

        var validRows =
            new List<(VendorProductImportRowDto Product, int RowNumber)>();

        foreach (var item in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var product = item.Product;
            var rowNumber = item.RowNumber;

            var rowErrors = ValidateRow(product);

            if (rowErrors.Count > 0)
            {
                errors.Add(new VendorProductImportErrorDto
                {
                    RowNumber = rowNumber,
                    Error = string.Join(" ", rowErrors)
                });

                continue;
            }

            validRows.Add(item);
        }

        /*
         * ImportBatch is intentionally created outside the individual
         * batch transactions.
         *
         * Therefore successful batches remain recorded even if a
         * later batch has a problem.
         */
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

        var inserted = 0;
        var updated = 0;

        /*
         * Process exactly 500 rows at a time.
         */
        for (var offset = 0;
             offset < validRows.Count;
             offset += BatchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batchRows = validRows
                .Skip(offset)
                .Take(BatchSize)
                .ToList();

            var batchResult = await ProcessBatchAsync(
                importBatch.ImportBatchId,
                batchRows,
                cancellationToken);

            inserted += batchResult.Inserted;
            updated += batchResult.Updated;

            errors.AddRange(batchResult.Errors);

            /*
             * Update the parent import record after every batch.
             *
             * This makes progress visible in the database.
             */
            importBatch.InsertedRows = inserted;
            importBatch.UpdatedRows = updated;
            importBatch.FailedRows = errors.Count;

            await _context.SaveChangesAsync(cancellationToken);

            /*
             * Clear tracked entities before moving to the next batch.
             */
            _context.ChangeTracker.Clear();

            /*
             * ChangeTracker.Clear() detaches importBatch too, so it has
             * to be re-attached or its progress silently stops being
             * saved after the first batch.
             */
            _context.ImportBatches.Attach(importBatch);
        }

        /*
         * Final import status.
         */
        importBatch.InsertedRows = inserted;
        importBatch.UpdatedRows = updated;
        importBatch.FailedRows = errors.Count;

        importBatch.Status = errors.Count == 0
            ? ImportBatchStatus.Completed
            : ImportBatchStatus.CompletedWithErrors;

        importBatch.CompletedDate = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

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

    private static List<string> ValidateRow(
        VendorProductImportRowDto product)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(product.Name))
        {
            errors.Add("Name is required.");
        }

        if (string.IsNullOrWhiteSpace(product.Category))
        {
            errors.Add("Category is required.");
        }

        if (!product.Price.HasValue)
        {
            errors.Add("Price is required.");
        }
        else if (product.Price.Value < 0)
        {
            errors.Add("Price cannot be negative.");
        }

        if (product.QuantityAvailable.HasValue &&
            product.QuantityAvailable.Value < 0)
        {
            errors.Add(
                "QuantityAvailable cannot be negative.");
        }

        if (product.SafetyStockLevel.HasValue &&
            product.SafetyStockLevel.Value < 0)
        {
            errors.Add(
                "SafetyStockLevel cannot be negative.");
        }

        if (product.ProcurementLeadTimeDays.HasValue &&
            product.ProcurementLeadTimeDays.Value < 0)
        {
            errors.Add(
                "ProcurementLeadTimeDays cannot be negative.");
        }

        return errors;
    }

    private async Task<BatchProcessResult> ProcessBatchAsync(
        long importBatchId,
        List<(VendorProductImportRowDto Product, int RowNumber)> batchRows,
        CancellationToken cancellationToken)
    {
        /*
         * First attempt:
         *
         * Process the complete 500-row batch in one transaction.
         */
        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                cancellationToken);

        try
        {
            var result = await ProcessRowsInternalAsync(
                importBatchId,
                batchRows,
                cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return result;
        }
        catch (DbUpdateException)
        {
            /*
             * One or more rows caused a database failure.
             *
             * Roll the whole batch back, then retry rows individually
             * so only the problematic rows are isolated.
             */
            await transaction.RollbackAsync(cancellationToken);

            _context.ChangeTracker.Clear();

            return await ProcessBatchRowByRowAsync(
                importBatchId,
                batchRows,
                cancellationToken);
        }
        catch
        {
            /*
             * Infrastructure/unexpected errors are not treated as
             * row-level validation errors.
             *
             * Do not silently continue when the database itself
             * is unavailable or the transaction is broken.
             */
            await transaction.RollbackAsync(cancellationToken);

            _context.ChangeTracker.Clear();

            throw;
        }
    }

    private async Task<BatchProcessResult> ProcessBatchRowByRowAsync(
        long importBatchId,
        List<(VendorProductImportRowDto Product, int RowNumber)> batchRows,
        CancellationToken cancellationToken)
    {
        var result = new BatchProcessResult();

        foreach (var item in batchRows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            /*
             * Each row gets its own transaction during fallback.
             *
             * This allows one bad row to be isolated while successful
             * rows in the same 500-row batch are still committed.
             */
            await using var rowTransaction =
                await _context.Database.BeginTransactionAsync(
                    cancellationToken);

            try
            {
                var rowResult = await ProcessRowsInternalAsync(
                    importBatchId,
                    new List<(VendorProductImportRowDto Product, int RowNumber)>
                    {
                        item
                    },
                    cancellationToken);

                await _context.SaveChangesAsync(cancellationToken);

                await rowTransaction.CommitAsync(cancellationToken);

                result.Inserted += rowResult.Inserted;
                result.Updated += rowResult.Updated;
                result.Errors.AddRange(rowResult.Errors);
            }
            catch (DbUpdateException ex)
            {
                await rowTransaction.RollbackAsync(cancellationToken);

                _context.ChangeTracker.Clear();

                var error = new VendorProductImportErrorDto
                {
                    RowNumber = item.RowNumber,
                    Error = GetDatabaseErrorMessage(ex)
                };

                result.Errors.Add(error);

                /*
                 * Record the failed row separately.
                 */
                await RecordFailedItemAsync(
                    importBatchId,
                    item.RowNumber,
                    error.Error,
                    cancellationToken);

                /*
                 * Continue to the next row.
                 */
                continue;
            }
            catch
            {
                await rowTransaction.RollbackAsync(cancellationToken);

                _context.ChangeTracker.Clear();

                throw;
            }

            /*
             * Do not allow tracked entities from the previous row
             * to accumulate.
             */
            _context.ChangeTracker.Clear();
        }

        return result;
    }

    private async Task<BatchProcessResult> ProcessRowsInternalAsync(
        long importBatchId,
        List<(VendorProductImportRowDto Product, int RowNumber)> rows,
        CancellationToken cancellationToken)
    {
        var result = new BatchProcessResult();

        /*
         * Categories are loaded only for this batch.
         */
        var categoryNames = rows
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

        /*
         * Load existing products for this batch only.
         */
        var externalProductIds = rows
            .Select(x => x.Product.ExternalProductId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var barcodes = rows
            .Select(x => x.Product.Barcode)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existingProducts = await _context.Products
            .Where(x =>
                (!string.IsNullOrWhiteSpace(x.ExternalProductId) &&
                 externalProductIds.Contains(x.ExternalProductId))
                ||
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
            .Where(x =>
                !string.IsNullOrWhiteSpace(x.Barcode))
            .ToDictionary(
                x => x.Barcode!,
                x => x,
                StringComparer.OrdinalIgnoreCase);

        /*
         * Product insert/update.
         */
        foreach (var item in rows)
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
            else if (!string.IsNullOrWhiteSpace(
                         productRow.Barcode) &&
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

                result.Inserted++;

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

                result.Updated++;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        /*
         * Inventory.
         */
        var productIds = rows
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

                if (!string.IsNullOrWhiteSpace(
                    productRow.Barcode) &&
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

        foreach (var item in rows)
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
            else if (!string.IsNullOrWhiteSpace(
                         productRow.Barcode) &&
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
                /*
                 * Vendor import represents incoming stock,
                 * so quantity is added rather than overwritten.
                 */
                inventory.QuantityAvailable +=
                    productRow.QuantityAvailable ?? 0;

                if (productRow.SafetyStockLevel.HasValue)
                {
                    inventory.SafetyStockLevel =
                        productRow.SafetyStockLevel.Value;
                }

                if (productRow.ProcurementLeadTimeDays.HasValue)
                {
                    inventory.ProcurementLeadTimeDays =
                        productRow.ProcurementLeadTimeDays.Value;
                }

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

        /*
         * Record successful rows.
         */
        foreach (var item in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Product? product = null;

            var productRow = item.Product;

            if (!string.IsNullOrWhiteSpace(
                productRow.ExternalProductId) &&
                productsByExternalId.TryGetValue(
                    productRow.ExternalProductId.Trim(),
                    out var externalProduct))
            {
                product = externalProduct;
            }
            else if (!string.IsNullOrWhiteSpace(
                         productRow.Barcode) &&
                     productsByBarcode.TryGetValue(
                         productRow.Barcode.Trim(),
                         out var barcodeProduct))
            {
                product = barcodeProduct;
            }

            _context.ImportBatchItems.Add(new ImportBatchItem
            {
                ImportBatchId = importBatchId,

                RowNumber = item.RowNumber,

                Status = "Success",

                ProductId = product?.ProductId
            });
        }

        await _context.SaveChangesAsync(cancellationToken);

        return result;
    }

    private async Task RecordFailedItemAsync(
        long importBatchId,
        int rowNumber,
        string error,
        CancellationToken cancellationToken)
    {
        _context.ImportBatchItems.Add(new ImportBatchItem
        {
            ImportBatchId = importBatchId,

            RowNumber = rowNumber,

            Status = "Failed",

            Error = error
        });

        await _context.SaveChangesAsync(cancellationToken);

        _context.ChangeTracker.Clear();
    }

    private static string GetDatabaseErrorMessage(
        DbUpdateException exception)
    {
        var message = exception.InnerException?.Message;

        if (!string.IsNullOrWhiteSpace(message))
        {
            return $"Database error: {message}";
        }

        return "Database error while importing this row.";
    }

    private sealed class BatchProcessResult
    {
        public int Inserted { get; set; }

        public int Updated { get; set; }

        public List<VendorProductImportErrorDto> Errors { get; } = new();
    }
}