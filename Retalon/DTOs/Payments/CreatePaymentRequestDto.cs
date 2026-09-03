namespace Retalon.DTOs.Payments;

public class PaymentResponseDto
{
    public long PaymentId { get; set; }
    public long OrderId { get; set; }

    public string StripePaymentIntentId { get; set; }
        = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; }
        = "USD";

    public string PaymentStatus { get; set; }
        = string.Empty;

    public string? ClientSecret { get; set; }

    public string? FailureReason { get; set; }

    public DateTime CreatedDate { get; set; }
    public DateTime? UpdatedDate { get; set; }
}