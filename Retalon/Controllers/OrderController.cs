using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Retalon.DTOs.Orders;
using Retalon.Services.Interfaces;

namespace Retalon.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
public class OrderController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrderController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder(
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if (userId == null)
        {
            return Unauthorized(new
            {
                message = "Invalid user identity."
            });
        }

        try
        {
            var order = await _orderService.CreateOrderAsync(
                userId.Value,
                cancellationToken);

            if (order == null)
            {
                return BadRequest(new
                {
                    message = "Cart is empty."
                });
            }

            return CreatedAtAction(
                nameof(GetOrderById),
                new { orderId = order.OrderId },
                order);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetOrders(
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if (userId == null)
        {
            return Unauthorized(new
            {
                message = "Invalid user identity."
            });
        }

        var orders = await _orderService.GetOrdersAsync(
            userId.Value,
            cancellationToken);

        return Ok(orders);
    }

    [HttpGet("{orderId:long}")]
    public async Task<IActionResult> GetOrderById(
        long orderId,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if (userId == null)
        {
            return Unauthorized(new
            {
                message = "Invalid user identity."
            });
        }

        var order = await _orderService.GetOrderByIdAsync(
            userId.Value,
            orderId,
            cancellationToken);

        if (order == null)
        {
            return NotFound(new
            {
                message = "Order not found."
            });
        }

        return Ok(order);
    }

    private Guid? GetUserId()
    {
        var userIdValue = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        return Guid.TryParse(userIdValue, out var userId)
            ? userId
            : null;
    }
}