namespace Retalon.DTOs.Procurement;

public class ProcurementResponseDto
{
    public long ProcurementId { get; set; }
    public long OrderId { get; set; }
    public long ProductId { get; set; }
    public int RequiredQuantity { get; set; }
    public string ProcurementStatus { get; set; } = string.Empty;
    public DateTime? ExpectedArrivalDate { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? UpdatedDate { get; set; }
}