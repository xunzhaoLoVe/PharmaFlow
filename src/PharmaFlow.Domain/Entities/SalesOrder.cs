using PharmaFlow.Domain.Enums;

namespace PharmaFlow.Domain.Entities;

/// <summary>销售单</summary>
public class SalesOrder : BaseEntity
{
    public string OrderNo { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public DateTime OrderDate { get; set; }
    public SalesType SalesType { get; set; } = SalesType.批发;
    public decimal TotalAmount { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.待审;
    public string? Operator { get; set; }
    public string? Remark { get; set; }
    public DateTime? AuditTime { get; set; }
    public string? Auditor { get; set; }

    public ICollection<SalesOrderItem> Items { get; set; } = new List<SalesOrderItem>();
}
