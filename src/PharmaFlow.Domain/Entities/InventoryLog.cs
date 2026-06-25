using PharmaFlow.Domain.Enums;

namespace PharmaFlow.Domain.Entities;

/// <summary>库存流水</summary>
public class InventoryLog : BaseEntity
{
    public Guid DrugId { get; set; }
    public Drug Drug { get; set; } = null!;
    public string BatchNo { get; set; } = string.Empty;
    public InventoryChangeType ChangeType { get; set; }
    public int Quantity { get; set; }
    public int BeforeQuantity { get; set; }
    public int AfterQuantity { get; set; }
    public string? RelatedOrderNo { get; set; }
    public string? Operator { get; set; }
    public DateTime OperateTime { get; set; } = DateTime.UtcNow;
    public string? Remark { get; set; }
}
