namespace PharmaFlow.Domain.Entities;

/// <summary>流向 Excel 导入明细，用于查询每行导入结果</summary>
public class DistributionImportDetail : BaseEntity
{
    public Guid ImportBatchId { get; set; }
    public string ImportBatchName { get; set; } = string.Empty;
    public bool IsPurchase { get; set; }
    public int RowNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string? DrugName { get; set; }
    public string? Specification { get; set; }
    public string? BatchNo { get; set; }
    public string? FromCustomerName { get; set; }
    public string? ToCustomerName { get; set; }
    public decimal Quantity { get; set; }
    public string? DistributionDate { get; set; }
    public string? Province { get; set; }
}
