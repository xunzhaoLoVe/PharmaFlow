namespace PharmaFlow.Domain.Entities;

/// <summary>库存（按客户层级：本司/经销商/终端）</summary>
public class Inventory : BaseEntity
{
    public Guid DrugId { get; set; }
    public Drug Drug { get; set; } = null!;
    public string BatchNo { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal LockedQuantity { get; set; }
    public DateTime? ProductionDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? Location { get; set; }
    /// <summary>所属客户(null=本司库存，其他=经销商/终端库存)</summary>
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }
}
