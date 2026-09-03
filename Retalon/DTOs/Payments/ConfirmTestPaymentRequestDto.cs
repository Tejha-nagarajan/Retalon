namespace Retalon.DTOs.Payments;

public class ConfirmTestPaymentRequestDto
{
    public long PaymentId { get; set; }
    public string TestPaymentMethod { get; set; } = "pm_card_visa";
}