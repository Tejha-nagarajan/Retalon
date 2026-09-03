using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retalon.Services.Interfaces;

namespace Retalon.Controllers;

[ApiController]
[Route("api/payments")]
public class StripeWebhookController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public StripeWebhookController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook(
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);

        var json = await reader.ReadToEndAsync(
            cancellationToken);

        var stripeSignature =
            Request.Headers["Stripe-Signature"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(stripeSignature))
        {
            return BadRequest("Stripe-Signature header is missing.");
        }

        try
        {
            await _paymentService.HandleWebhookAsync(
                json,
                stripeSignature,
                cancellationToken);

            return Ok();
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