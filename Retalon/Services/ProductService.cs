using Microsoft.EntityFrameworkCore;
using Retalon.Data;
using Retalon.DTOs.Products;
using Retalon.Models.Entities;
using Retalon.Models.Enums;
using Retalon.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace Retalon.Services;

public class ProductService : IProductService
{
    private readonly ApplicationDbContext _context;
    private readonly IOpenFoodFactsService _openFoodFactsService;
    private readonly IMemoryCache _cache;

    public ProductService(
        ApplicationDbContext context,
        IOpenFoodFactsService openFoodFactsService,
        IMemoryCache cache)
    {
        _context = context;
        _openFoodFactsService = openFoodFactsService;
        _cache = cache;
    }

    public async Task<List<ProductResponseDto>> SearchProductsAsync(
        string searchTerm,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return new List<ProductResponseDto>();
        }

        searchTerm = searchTerm.Trim();

        var cacheKey = $"product-search:{searchTerm.ToLowerInvariant()}";

        if (_cache.TryGetValue(
            cacheKey,
            out List<ProductResponseDto>? cachedProducts))
        {
            return cachedProducts!;
        }

        var localProducts = await _context.Products
            .AsNoTracking()
            .Where(p =>
                !p.IsDeleted &&
                p.ProductStatus != Models.Enums.ProductStatus.Inactive &&
                (p.Name.Contains(searchTerm) ||
                 (p.Barcode != null &&
                  p.Barcode.Contains(searchTerm))))
            .OrderBy(p => p.Name)
            .Take(20)
            .Select(p => MapToDto(p))
            .ToListAsync(cancellationToken);

        if (localProducts.Count > 0)
        {
            _cache.Set(
                cacheKey,
                localProducts,
                TimeSpan.FromMinutes(5));

            return localProducts;
        }

        var externalProducts =
    await _openFoodFactsService.SearchProductsAsync(
        searchTerm,
        cancellationToken);

        var localResults = new List<ProductResponseDto>();

        foreach (var externalProduct in externalProducts)
        {
            if (string.IsNullOrWhiteSpace(externalProduct.Barcode))
                continue;

            var existingProduct = await _context.Products
                .FirstOrDefaultAsync(
                    p => p.Barcode == externalProduct.Barcode &&
                         !p.IsDeleted,
                    cancellationToken);

            if (existingProduct == null)
            {
                var category = await _context.Categories
                    .FirstOrDefaultAsync(
                        c => c.Name == "Imported",
                        cancellationToken);

                if (category == null)
                {
                    category = new Category
                    {
                        Name = "Imported",
                        Description = "Products imported from external sources."
                    };

                    _context.Categories.Add(category);
                    await _context.SaveChangesAsync(cancellationToken);
                }

                existingProduct = new Product
                {
                    CategoryId = category.CategoryId,
                    ExternalProductId = externalProduct.ExternalProductId,
                    Name = externalProduct.Name,
                    Barcode = externalProduct.Barcode,
                    Description = externalProduct.Description,
                    ImageUrl = externalProduct.ImageUrl,
                    Price = externalProduct.Price,
                    Currency = externalProduct.Currency,
                    ImportSource = "OpenFoodFacts",
                    ProductStatus = ProductStatus.Active,
                    IsDeleted = false,
                    CreatedDate = DateTime.UtcNow,
                    LastUpdated = DateTime.UtcNow
                };

                _context.Products.Add(existingProduct);
                await _context.SaveChangesAsync(cancellationToken);

                var inventory = new Inventory
                {
                    ProductId = existingProduct.ProductId,
                    QuantityAvailable = 0,
                    QuantityReserved = 0,
                    SafetyStockLevel = 0,
                    ProcurementLeadTimeDays = 0,
                    LastUpdated = DateTime.UtcNow
                };

                _context.Inventories.Add(inventory);
                await _context.SaveChangesAsync(cancellationToken);
            }

            localResults.Add(MapToDto(existingProduct));
        }

        _cache.Set(
            cacheKey,
            localResults,
            TimeSpan.FromMinutes(5));

        return localResults;
    }

    public async Task<ProductResponseDto?> GetProductByIdAsync(
    long productId,
    CancellationToken cancellationToken = default)
    {
        var cacheKey = $"product:{productId}";

        if (_cache.TryGetValue(
            cacheKey,
            out ProductResponseDto? cachedProduct))
        {
            return cachedProduct;
        }

        var product = await _context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.ProductId == productId &&
                     !p.IsDeleted,
                cancellationToken);

        if (product == null)
        {
            return null;
        }

        var result = MapToDto(product);

        _cache.Set(
            cacheKey,
            result,
            TimeSpan.FromMinutes(5));

        return result;
    }

    public async Task<ProductResponseDto?> GetProductByBarcodeAsync(
    string barcode,
    CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return null;
        }

        barcode = barcode.Trim();

        var cacheKey = $"product-barcode:{barcode}";

        if (_cache.TryGetValue(
            cacheKey,
            out ProductResponseDto? cachedProduct))
        {
            return cachedProduct;
        }

        var localProduct = await _context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.Barcode == barcode &&
                     !p.IsDeleted,
                cancellationToken);

        if (localProduct != null)
        {
            var result = MapToDto(localProduct);

            _cache.Set(
                cacheKey,
                result,
                TimeSpan.FromMinutes(5));

            return result;
        }

        var externalProduct =
            await _openFoodFactsService.GetProductByBarcodeAsync(
                barcode,
                cancellationToken);

        if (externalProduct == null)
        {
            return null;
        }

        _cache.Set(
            cacheKey,
            externalProduct,
            TimeSpan.FromMinutes(5));

        return externalProduct;
    }

    private static ProductResponseDto MapToDto(Product product)
    {
        return new ProductResponseDto
        {
            ProductId = product.ProductId,
            CategoryId = product.CategoryId,
            ExternalProductId = product.ExternalProductId,
            Name = product.Name,
            Barcode = product.Barcode,
            Description = product.Description,
            ImageUrl = product.ImageUrl,
            Price = product.Price,
            Currency = product.Currency,
            ImportSource = product.ImportSource,
            ProductStatus = product.ProductStatus.ToString()
        };
    }
    public async Task<ProductResponseDto?> ImportFromOpenFoodFactsAsync(
    string barcode,
    CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return null;
        }

        barcode = barcode.Trim();

        // Check whether the product already exists locally.
        var existingProduct = await _context.Products
            .FirstOrDefaultAsync(
                p => p.Barcode == barcode && !p.IsDeleted,
                cancellationToken);

        if (existingProduct != null)
        {
            return MapToDto(existingProduct);
        }

        // Get the product from Open Food Facts.
        var externalProduct =
            await _openFoodFactsService.GetProductByBarcodeAsync(
                barcode,
                cancellationToken);

        if (externalProduct == null)
        {
            return null;
        }

        // Find or create a category.
        var category = await _context.Categories
            .FirstOrDefaultAsync(
                c => c.Name == "Imported",
                cancellationToken);

        if (category == null)
        {
            category = new Category
            {
                Name = "Imported",
                Description = "Products imported from external sources."
            };

            _context.Categories.Add(category);

            await _context.SaveChangesAsync(cancellationToken);
        }

        var product = new Product
        {
            CategoryId = category.CategoryId,
            ExternalProductId = externalProduct.ExternalProductId,
            Name = externalProduct.Name,
            Barcode = externalProduct.Barcode,
            Description = externalProduct.Description,
            ImageUrl = externalProduct.ImageUrl,

            // External API doesn't provide our selling price.
            Price = 0m,

            Currency = "USD",
            ImportSource = "OpenFoodFacts",
            ProductStatus = ProductStatus.Active,
            IsDeleted = false,
            CreatedDate = DateTime.UtcNow,
            LastUpdated = DateTime.UtcNow
        };

        _context.Products.Add(product);

        await _context.SaveChangesAsync(cancellationToken);

        return MapToDto(product);
    }
}