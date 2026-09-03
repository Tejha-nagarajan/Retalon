using Retalon.DTOs.Payments;

namespace Retalon.Services.Interfaces;

public interface IPaymentService
{
    Task<PaymentResponseDto?> CreatePaymentIntentAsync(
        Guid userId,
        CreatePaymentRequestDto request,
        CancellationToken cancellationToken = default);
    Task HandleWebhookAsync(
    string json,
    string stripeSignature,
    CancellationToken cancellationToken = default);

    Task<PaymentResponseDto?> ConfirmTestPaymentAsync(
    Guid userId,
    ConfirmTestPaymentRequestDto request,
    CancellationToken cancellationToken = default);
}