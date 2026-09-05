using System.ComponentModel.DataAnnotations;

namespace Retalon.DTOs.Payments;

public class CreatePaymentRequestDto
{
    [Range(1, long.MaxValue)]
    public long OrderId { get; set; }
}