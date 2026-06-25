using PharmaFlow.Domain.Enums;

namespace PharmaFlow.Domain.Entities;

/// <summary>采购单</summary>
public class PurchaseOrder : BaseEntity
{
    public string OrderNo { get; set; } = string.Empty;
    public Guid SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.待审;
    public string? Operator { get; set; }
    public string? Remark { get; set; }
    public DateTime? AuditTime { get; set; }
    public string? Auditor { get; set; }

    public ICollection<PurchaseOrderItem> Items { get; set; } = new List<PurchaseOrderItem>();
}
