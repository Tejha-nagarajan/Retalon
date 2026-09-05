using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Retalon.Services.Interfaces;
namespace Retalon.Controllers;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;

[ApiController]
[ApiVersion("1.0")]
[Route("api/products")]
public class ProductController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductController(IProductService productService)
    {
        _productService = productService;
    }

    [HttpGet("search")]
    [AllowAnonymous]
    [EnableRateLimiting("SearchPolicy")]
    public async Task<IActionResult> Search(
    [FromQuery] string query,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    [FromQuery] string? sortBy = null,
    [FromQuery] bool descending = false,
    CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return BadRequest(new { message = "Search query is required." });

        if (page < 1)
            return BadRequest(new { message = "Page must be greater than 0." });

        if (pageSize < 1 || pageSize > 100)
            return BadRequest(new { message = "Page size must be between 1 and 100." });

        var products = await _productService.SearchProductsAsync(
            query,
            page,
            pageSize,
            sortBy,
            descending,
            cancellationToken);

        return Ok(products);
    }

    [HttpGet("{productId:long}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(
        long productId,
        CancellationToken cancellationToken)
    {
        var product = await _productService.GetProductByIdAsync(
            productId,
            cancellationToken);

        if (product == null)
        {
            return NotFound(new
            {
                message = "Product not found."
            });
        }

        return Ok(product);
    }

    [HttpGet("barcode/{barcode}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetByBarcode(
        string barcode,
        CancellationToken cancellationToken)
    {
        var product =
            await _productService.GetProductByBarcodeAsync(
                barcode,
                cancellationToken);

        if (product == null)
        {
            return NotFound(new
            {
                message = "Product not found."
            });
        }

        return Ok(product);
    }
    [HttpPost("import/{barcode}")]
    [AllowAnonymous]
    public async Task<IActionResult> Import(
    string barcode,
    CancellationToken cancellationToken)
    {
        var product =
            await _productService.ImportFromOpenFoodFactsAsync(
                barcode,
                cancellationToken);

        if (product == null)
        {
            return NotFound(new
            {
                message = "Product not found in Open Food Facts."
            });
        }

        return Ok(product);
    }
}