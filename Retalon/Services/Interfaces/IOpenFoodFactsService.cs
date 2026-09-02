using Retalon.DTOs.Products;

namespace Retalon.Services.Interfaces;

public interface IOpenFoodFactsService
{
    Task<List<ProductResponseDto>> SearchProductsAsync(
        string searchTerm,
        CancellationToken cancellationToken = default);

    Task<ProductResponseDto?> GetProductByBarcodeAsync(
        string barcode,
        CancellationToken cancellationToken = default);
}