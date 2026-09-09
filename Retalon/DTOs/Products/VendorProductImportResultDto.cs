namespace Retalon.DTOs.Products;

public class VendorProductImportResultDto
{
    public bool Success { get; set; }

    public long ImportBatchId { get; set; }

    public int TotalRows { get; set; }

    public int Inserted { get; set; }

    public int Updated { get; set; }

    public int Failed { get; set; }

    public string Status { get; set; } = string.Empty;

    public List<VendorProductImportErrorDto> Errors { get; set; } = new();
}

public class VendorProductImportErrorDto
{
    public int RowNumber { get; set; }

    public string Error { get; set; } = string.Empty;
}