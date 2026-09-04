using Retalon.DTOs.Common;
using Retalon.DTOs.Products;

namespace Retalon.Services.Interfaces;

public interface IProductService
{
    Task<PagedResponseDto<ProductResponseDto>> SearchProductsAsync(
    string searchTerm,
    int page = 1,
    int pageSize = 20,
    string? sortBy = null,
    bool descending = false,
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