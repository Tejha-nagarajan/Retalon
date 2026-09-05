using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retalon.Services.Interfaces;

namespace Retalon.Controllers;

[ApiController]
[Route("api/inventory")]
[Authorize]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventoryService;

    public InventoryController(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    [HttpGet("{productId:long}")]
    public async Task<IActionResult> GetByProductId(
        long productId,
        CancellationToken cancellationToken)
    {
        var inventory =
            await _inventoryService.GetByProductIdAsync(
                productId,
                cancellationToken);

        if (inventory == null)
        {
            return NotFound(new
            {
                message = "Inventory not found for this product."
            });
        }

        return Ok(inventory);
    }

    [HttpPut("{productId:long}")]
    [Authorize(Roles = "Admin,WarehouseManager")]
    public async Task<IActionResult> Update(
        long productId,
        [FromBody] UpdateInventoryRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var inventory =
                await _inventoryService.UpdateAsync(
                    productId,
                    request.QuantityAvailable,
                    request.QuantityReserved,
                    request.SafetyStockLevel,
                    request.ProcurementLeadTimeDays,
                    cancellationToken);

            if (inventory == null)
            {
                return NotFound(new
                {
                    message = "Product not found."
                });
            }

            return Ok(inventory);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    [HttpPost("{productId:long}/restock")]
    [Authorize(Roles = "Admin,WarehouseManager")]
    public async Task<IActionResult> Restock(
        long productId,
        [FromBody] RestockRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var inventory =
                await _inventoryService.RestockAsync(
                    productId,
                    request.Quantity,
                    cancellationToken);

            if (inventory == null)
            {
                return NotFound(new
                {
                    message = "Inventory not found."
                });
            }

            return Ok(inventory);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }
}

public class UpdateInventoryRequest
{
    public int QuantityAvailable { get; set; }
    public int QuantityReserved { get; set; }
    public int SafetyStockLevel { get; set; }
    public int ProcurementLeadTimeDays { get; set; }
}

public class RestockRequest
{
    public int Quantity { get; set; }
}