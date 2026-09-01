using Retalon.Models.Enums;

namespace Retalon.Models.Entities;

public class Payment
{
    public long PaymentId { get; set; }

    public long OrderId { get; set; }

    public string StripePaymentIntentId { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "USD";

    public PaymentStatus PaymentStatus { get; set; }

    public string? FailureReason { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public Order Order { get; set; } = null!;
}