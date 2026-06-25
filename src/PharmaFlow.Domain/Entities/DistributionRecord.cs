namespace PharmaFlow.Domain.Entities;

/// <summary>流向记录 — 记录药品在供应链中的每一次流转</summary>
/// <remarks>
/// 采购来源: SupplierId 有值时表示从供应商采购，此时 FromCustomerId 为 null
/// 销售流转: FromCustomerId + ToCustomerId 记录经销商/终端之间的流转
/// </remarks>
public class DistributionRecord : BaseEntity
{
    public Guid DrugId { get; set; }
    public Drug Drug { get; set; } = null!;
    public string BatchNo { get; set; } = string.Empty;

    /// <summary>采购来源供应商（采购入库时使用，与 FromCustomerId 互斥）</summary>
    public Guid? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public Guid? FromCustomerId { get; set; }
    public Customer? FromCustomer { get; set; }
    public Guid? ToCustomerId { get; set; }
    public Customer? ToCustomer { get; set; }
    public Guid? SalesOrderId { get; set; }
    public SalesOrder? SalesOrder { get; set; }
    public decimal Quantity { get; set; }
    public string? SalesPerson { get; set; }
    public string? DeliveryOrderNo { get; set; }
    public decimal TaxedUnitPrice { get; set; }
    public DateTime DistributionDate { get; set; }
    public string? Operator { get; set; }
    public string? Province { get; set; }
    public string? DistributionDateDisplay { get; set; }
}
