using Retalon.Models.Entities;

namespace Retalon.Entities
{
    public class ImportBatchItem
    {
        public long ImportBatchItemId { get; set; }

        public long ImportBatchId { get; set; }

        public int RowNumber { get; set; }

        public string Status { get; set; } = string.Empty;

        public string? Error { get; set; }

        public long? ProductId { get; set; }

        public ImportBatch ImportBatch { get; set; } = null!;

        public Product? Product { get; set; }
    }
}