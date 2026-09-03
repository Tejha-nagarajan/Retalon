using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retalon.DTOs.Procurement;
using Retalon.Models.Enums;
using Retalon.Services.Interfaces;
using System.Security.Claims;

namespace Retalon.Controllers;

[ApiController]
[Route("api/procurement")]
[Authorize]
public class ProcurementController : ControllerBase
{
    private readonly IProcurementService _procurementService;

    public ProcurementController(IProcurementService procurementService)
    {
        _procurementService = procurementService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateProcurement(
        [FromBody] CreateProcurementRequestDto request,
        CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        try
        {
            var result = await _procurementService.CreateProcurementAsync(
                userId,
                request,
                cancellationToken);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetProcurements(
        CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var result = await _procurementService.GetProcurementsAsync(
            userId,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("{procurementId:long}")]
    public async Task<IActionResult> GetProcurement(
        long procurementId,
        CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var result = await _procurementService.GetProcurementByIdAsync(
            userId,
            procurementId,
            cancellationToken);

        if (result == null)
            return NotFound();

        return Ok(result);
    }
    [HttpPut("{procurementId:long}/status")]
    [Authorize(Roles = "Admin,WarehouseManager")]
    public async Task<IActionResult> UpdateProcurementStatus(
    long procurementId,
    [FromBody] ProcurementStatus status,
    CancellationToken cancellationToken)
    {
        var result = await _procurementService.UpdateProcurementStatusAsync(
            procurementId,
            status,
            cancellationToken);

        if (result == null)
            return NotFound();

        return Ok(result);
    }
}