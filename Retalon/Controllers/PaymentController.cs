using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Retalon.DTOs.Payments;
using Retalon.Services.Interfaces;
using System.Security.Claims;

namespace Retalon.Controllers;

[ApiController]
[Route("api/payments")]
[Authorize]
public class PaymentController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    [HttpPost("create")]
    [EnableRateLimiting("PaymentPolicy")]
    public async Task<IActionResult> CreatePayment(
        [FromBody] CreatePaymentRequestDto request,
        CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        try
        {
            var result = await _paymentService.CreatePaymentIntentAsync(
                userId,
                request,
                cancellationToken);

            if (result == null)
            {
                return NotFound("Order not found.");
            }

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("confirm-test")]
    [EnableRateLimiting("PaymentPolicy")]
    public async Task<IActionResult> ConfirmTestPayment(
    [FromBody] ConfirmTestPaymentRequestDto request,
    CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        try
        {
            var result = await _paymentService.ConfirmTestPaymentAsync(
                userId,
                request,
                cancellationToken);

            if (result == null)
                return NotFound("Payment not found.");

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}