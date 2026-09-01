using Retalon.Models.Enums;

namespace Retalon.Models.Entities;

public class Procurement
{
    public long ProcurementId { get; set; }

    public long OrderId { get; set; }

    public long ProductId { get; set; }

    public int RequiredQuantity { get; set; }

    public ProcurementStatus ProcurementStatus { get; set; }

    public DateTime? ExpectedArrivalDate { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public Order Order { get; set; } = null!;

    public Product Product { get; set; } = null!;
}