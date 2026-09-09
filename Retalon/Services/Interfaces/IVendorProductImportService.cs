using Microsoft.AspNetCore.Http;
using Retalon.DTOs.Products;

namespace Retalon.Services.Interfaces;

public interface IVendorProductImportService
{
    Task<VendorProductImportResultDto> ImportAsync(
        IFormFile file,
        Guid userId,
        CancellationToken cancellationToken = default);
}