using Retalon.DTOs.Products;
using Retalon.Services.Interfaces;

namespace Retalon.Tests.Infrastructure;

/// <summary>
/// Deterministic test double for IOpenFoodFactsService. Never makes a real HTTP call.
/// </summary>
public class FakeOpenFoodFactsService : IOpenFoodFactsService
{
    public static readonly ProductResponseDto DefaultBarcodeProduct = new()
    {
        ExternalProductId = "0000000000001",
        Name = "Fake Imported Cookies",
        Barcode = "0000000000001",
        Description = "Deterministic test-double product for import flows.",
        ImageUrl = null,
        Price = 0m,
        Currency = "USD",
        ImportSource = "OpenFoodFacts"
    };

    public const string ImportSearchTerm = "importtest";

    public Task<List<ProductResponseDto>> SearchProductsAsync(
        string searchTerm,
        CancellationToken cancellationToken = default)
    {
        if (string.Equals(searchTerm, ImportSearchTerm, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new List<ProductResponseDto> { DefaultBarcodeProduct });
        }

        return Task.FromResult(new List<ProductResponseDto>());
    }

    public Task<ProductResponseDto?> GetProductByBarcodeAsync(
        string barcode,
        CancellationToken cancellationToken = default)
    {
        if (barcode == DefaultBarcodeProduct.Barcode)
        {
            return Task.FromResult<ProductResponseDto?>(DefaultBarcodeProduct);
        }

        return Task.FromResult<ProductResponseDto?>(null);
    }
}
