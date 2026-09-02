using Microsoft.AspNetCore.Mvc;
using Retalon.Services.Interfaces;
using Microsoft.AspNetCore.RateLimiting;
namespace Retalon.Controllers;

[ApiController]
[Route("api/products")]
public class ProductController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductController(IProductService productService)
    {
        _productService = productService;
    }

    [HttpGet("search")]
    [EnableRateLimiting("SearchPolicy")]
    public async Task<IActionResult> Search(
        [FromQuery] string query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest(new
            {
                message = "Search query is required."
            });
        }

        var products = await _productService.SearchProductsAsync(
            query,
            cancellationToken);

        return Ok(products);
    }

    [HttpGet("{productId:long}")]
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