using Retalon.Models.Enums;

namespace Retalon.Models.Entities;

public class ImportBatch
{
    public long ImportBatchId { get; set; }

    public Guid UserId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public ImportBatchStatus Status { get; set; }

    public int TotalRows { get; set; }

    public int InsertedRows { get; set; }

    public int UpdatedRows { get; set; }

    public int FailedRows { get; set; }

    public string? ErrorSummary { get; set; }

    public DateTime StartedDate { get; set; }

    public DateTime? CompletedDate { get; set; }

    public User User { get; set; } = null!;
}