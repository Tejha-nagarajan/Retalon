using Retalon.DTOs.Products;

namespace Retalon.Services.Interfaces;

public interface IProductService
{
    Task<List<ProductResponseDto>> SearchProductsAsync(
        string searchTerm,
        CancellationToken cancellationToken = default);

    Task<ProductResponseDto?> GetProductByIdAsync(
        long productId,
        CancellationToken cancellationToken = default);

    Task<ProductResponseDto?> GetProductByBarcodeAsync(
        string barcode,
        CancellationToken cancellationToken = default);
    Task<ProductResponseDto?> ImportFromOpenFoodFactsAsync(
    string barcode,
    CancellationToken cancellationToken = default);
}