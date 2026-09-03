using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Retalon.Data;
using Retalon.DTOs.Payments;
using Retalon.Models.Configuration;
using Retalon.Models.Entities;
using Retalon.Models.Enums;
using Retalon.Services.Interfaces;
using Stripe;
using Microsoft.EntityFrameworkCore.Storage;

namespace Retalon.Services;

public class PaymentService : IPaymentService
{
    private readonly ApplicationDbContext _context;
    private readonly StripeSettings _stripeSettings;

    public PaymentService(
        ApplicationDbContext context,
        IOptions<StripeSettings> stripeOptions)
    {
        _context = context;
        _stripeSettings = stripeOptions.Value;
    }

    public async Task<PaymentResponseDto?> CreatePaymentIntentAsync(
        Guid userId,
        CreatePaymentRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var order = await _context.Orders
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(
                o => o.OrderId == request.OrderId &&
                     o.UserId == userId,
                cancellationToken);

        if (order == null)
        {
            return null;
        }

        if (order.TotalAmount <= 0)
        {
            throw new InvalidOperationException(
                "Order amount must be greater than zero.");
        }

        if (order.OrderStatus == OrderStatus.Cancelled)
        {
            throw new InvalidOperationException(
                "Cancelled orders cannot be paid.");
        }

        var existingPayment = order.Payments
            .FirstOrDefault(
                p => p.PaymentStatus == PaymentStatus.Succeeded);

        if (existingPayment != null)
        {
            throw new InvalidOperationException(
                "This order has already been paid.");
        }

        var existingPendingPayment = order.Payments
            .FirstOrDefault(
                p => p.PaymentStatus == PaymentStatus.Pending ||
                     p.PaymentStatus == PaymentStatus.Processing);

        if (existingPendingPayment != null)
        {
            if (string.IsNullOrWhiteSpace(
                    _stripeSettings.SecretKey))
            {
                throw new InvalidOperationException(
                    "Stripe test secret key is not configured.");
            }

            StripeConfiguration.ApiKey =
                _stripeSettings.SecretKey;

            var paymentIntentService =
                new PaymentIntentService();

            var existingPaymentIntent =
                await paymentIntentService.GetAsync(
                    existingPendingPayment.StripePaymentIntentId,
                    cancellationToken: cancellationToken);

            return new PaymentResponseDto
            {
                PaymentId = existingPendingPayment.PaymentId,
                OrderId = existingPendingPayment.OrderId,
                StripePaymentIntentId =
                    existingPendingPayment.StripePaymentIntentId,
                Amount = existingPendingPayment.Amount,
                Currency = existingPendingPayment.Currency,
                PaymentStatus =
                    existingPendingPayment.PaymentStatus.ToString(),
                ClientSecret =
                    existingPaymentIntent.ClientSecret,
                FailureReason =
                    existingPendingPayment.FailureReason,
                CreatedDate =
                    existingPendingPayment.CreatedDate,
                UpdatedDate =
                    existingPendingPayment.UpdatedDate
            };
        }

        if (string.IsNullOrWhiteSpace(
                _stripeSettings.SecretKey))
        {
            throw new InvalidOperationException(
                "Stripe test secret key is not configured.");
        }

        StripeConfiguration.ApiKey =
            _stripeSettings.SecretKey;

        var options = new PaymentIntentCreateOptions
        {
            Amount = Convert.ToInt64(
                Math.Round(
                    order.TotalAmount * 100m,
                    MidpointRounding.AwayFromZero)),

            Currency = "usd",

            Metadata = new Dictionary<string, string>
            {
                ["orderId"] = order.OrderId.ToString(),
                ["userId"] = userId.ToString()
            },

            AutomaticPaymentMethods =
                new PaymentIntentAutomaticPaymentMethodsOptions
                {
                    Enabled = true,
                    AllowRedirects = "never"
                }
        };

        var service = new PaymentIntentService();

        var paymentIntent =
            await service.CreateAsync(
                options,
                cancellationToken: cancellationToken);

        var payment = new Payment
        {
            OrderId = order.OrderId,
            StripePaymentIntentId = paymentIntent.Id,
            Amount = order.TotalAmount,
            Currency = "USD",
            PaymentStatus = PaymentStatus.Pending,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };

        _context.Payment.Add(payment);

        await _context.SaveChangesAsync(cancellationToken);

        return new PaymentResponseDto
        {
            PaymentId = payment.PaymentId,
            OrderId = payment.OrderId,
            StripePaymentIntentId =
                payment.StripePaymentIntentId,
            Amount = payment.Amount,
            Currency = payment.Currency,
            PaymentStatus =
                payment.PaymentStatus.ToString(),
            ClientSecret =
                paymentIntent.ClientSecret,
            CreatedDate = payment.CreatedDate,
            UpdatedDate = payment.UpdatedDate
        };
    }
    public async Task HandleWebhookAsync(
    string json,
    string stripeSignature,
    CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_stripeSettings.WebhookSecret))
        {
            throw new InvalidOperationException(
                "Stripe webhook secret is not configured.");
        }

        if (string.IsNullOrWhiteSpace(stripeSignature))
        {
            throw new ArgumentException(
                "Stripe signature is missing.");
        }

        Stripe.Event stripeEvent;

        try
        {
            stripeEvent = Stripe.EventUtility.ConstructEvent(
                json,
                stripeSignature,
                _stripeSettings.WebhookSecret);
        }
        catch (StripeException)
        {
            throw new ArgumentException(
                "Invalid Stripe webhook signature.");
        }

        if (stripeEvent.Type != "payment_intent.succeeded")
        {
            return;
        }

        var paymentIntent = stripeEvent.Data.Object as PaymentIntent;

        if (paymentIntent == null)
        {
            return;
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                cancellationToken);

        var payment = await _context.Payment
            .Include(p => p.Order)
            .FirstOrDefaultAsync(
                p => p.StripePaymentIntentId == paymentIntent.Id,
                cancellationToken);

        if (payment == null)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        if (payment.PaymentStatus == PaymentStatus.Succeeded)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        if (payment.Order.OrderStatus == OrderStatus.Cancelled)
        {
            throw new InvalidOperationException(
                "Cancelled orders cannot be completed.");
        }

        var orderItems = await _context.OrderItems
            .Where(i => i.OrderId == payment.OrderId)
            .ToListAsync(cancellationToken);

        var productIds = orderItems
            .Select(i => i.ProductId)
            .Distinct()
            .ToList();

        var inventories = await _context.Inventories
            .Where(i => productIds.Contains(i.ProductId))
            .ToDictionaryAsync(
                i => i.ProductId,
                cancellationToken);

        foreach (var item in orderItems)
        {
            if (!inventories.TryGetValue(
                    item.ProductId,
                    out var inventory))
            {
                throw new InvalidOperationException(
                    $"Inventory not found for product {item.ProductId}.");
            }

            var availableAfterReservation =
                inventory.QuantityAvailable -
                inventory.QuantityReserved;

            if (availableAfterReservation < item.Quantity)
            {
                throw new InvalidOperationException(
                    $"Insufficient inventory for product {item.ProductId}.");
            }
        }

        foreach (var item in orderItems)
        {
            var inventory = inventories[item.ProductId];

            inventory.QuantityAvailable -= item.Quantity;

            inventory.QuantityReserved =
                Math.Max(
                    0,
                    inventory.QuantityReserved - item.Quantity);

            inventory.LastUpdated = DateTime.UtcNow;
        }

        payment.PaymentStatus = PaymentStatus.Succeeded;
        payment.UpdatedDate = DateTime.UtcNow;

        payment.Order.OrderStatus = OrderStatus.Confirmed;
        payment.Order.UpdatedDate = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }
}