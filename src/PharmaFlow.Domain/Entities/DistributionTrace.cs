namespace PharmaFlow.Domain.Entities;

/// <summary>流向追溯</summary>
public class DistributionTrace : BaseEntity
{
    public Guid DrugId { get; set; }
    public Drug Drug { get; set; } = null!;
    public string BatchNo { get; set; } = string.Empty;
    public string? TraceCode { get; set; }
    public Guid? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public DateTime PurchaseDate { get; set; }
    public string CustomerChain { get; set; } = string.Empty; // JSON array of customer names/ids
}
