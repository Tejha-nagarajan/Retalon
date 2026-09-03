using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retalon.DTOs.Notifications;
using Retalon.Services.Interfaces;

namespace Retalon.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly IEmailService _emailService;

    public NotificationsController(IEmailService emailService)
    {
        _emailService = emailService;
    }

    [HttpPost("test-email")]
    public async Task<IActionResult> SendTestEmail(
        [FromBody] SendTestEmailRequestDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ToEmail))
            return BadRequest("ToEmail is required.");

        await _emailService.SendEmailAsync(
            request.ToEmail,
            request.Subject,
            request.Body,
            cancellationToken);

        return Ok(new
        {
            message = "Test email sent successfully."
        });
    }
}