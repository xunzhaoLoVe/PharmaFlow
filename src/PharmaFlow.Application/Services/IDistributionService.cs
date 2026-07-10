using PharmaFlow.Application.DTOs;

namespace PharmaFlow.Application.Services;

public interface IDistributionService
{
    Task<PagedResult<DistributionRecordDto>> GetPagedAsync(int pageIndex, int pageSize, string? drugName = null, string? batchNo = null, Guid? customerId = null, DateTime? from = null, DateTime? to = null, bool? fromSelf = null);
    Task<FlowGraphDto?> GetTraceAsync(string batchNo, Guid drugId);
    Task<DistributionImportResultDto> RecordDownstreamAsync(DownstreamRecordDto dto, string operatorName);
    /// <summary>批量导入下游出货记录，一次性提交</summary>
    Task<DistributionImportResultDto> RecordDownstreamBatchAsync(List<DownstreamRecordDto> dtos, string operatorName, bool deduplicateWithinBatch = true);
    Task<IReadOnlyList<DropdownItem>> GetCustomerDropdownAsync();
    Task DeleteDistributionRecordAsync(Guid recordId);
    /// <summary>清空所有流向记录和库存数据（物理删除），返回删除的流向记录条数</summary>
    Task<int> ClearAllAsync();
    Task<int> ClearBySourceAsync(bool isPurchase);
    Task<int> ClearSalesByOriginAsync(string origin);
    Task<int> ClearPurchasesByOriginAsync(string origin);
    Task<IReadOnlyList<ImportBatchDto>> GetImportBatchesAsync(bool isPurchase);
    Task<IReadOnlyList<DistributionImportDetailDto>> GetImportDetailsAsync(Guid batchId);
    Task SaveImportDetailsAsync(List<DistributionImportDetailDto> details);
    Task<int> ClearImportBatchAsync(Guid batchId);
    Task UpdateDistributionRecordAsync(Guid recordId, decimal newQuantity);
    Task<IReadOnlyList<ChannelConflictDto>> GetChannelConflictsAsync(string? batchNo = null);
}

public record DistributionImportResultDto
{
    public int Imported { get; set; }
    public int Skipped { get; set; }
    public int Supplemented { get; set; }
}

public record DistributionRecordDto
{
    public Guid Id { get; set; }
    public string DrugName { get; set; } = string.Empty;
    public string? Specification { get; set; }
    public string BatchNo { get; set; } = string.Empty;
    public string? ToCustomerName { get; set; }
    public string? ToCustomerCode { get; set; }
    public string? FromCustomerName { get; set; }
    public string? FromCustomerCode { get; set; }
    public string? SupplierName { get; set; }
    public decimal Quantity { get; set; }
    public string? SalesPerson { get; set; }
    public string? DeliveryOrderNo { get; set; }
    public string? Province { get; set; }
    public decimal TaxedUnitPrice { get; set; }
    public decimal TotalTaxedPrice => Quantity * TaxedUnitPrice;
    public DateTime DistributionDate { get; set; }
    public string? Operator { get; set; }
    public string? DistributionDateDisplay { get; set; }
    public Guid? ImportBatchId { get; set; }
    public string? ImportBatchName { get; set; }
}

public record ImportBatchDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
    public int SuccessCount { get; set; }
    public int DuplicateCount { get; set; }
    public int InvalidCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public record DistributionImportDetailDto
{
    public Guid Id { get; set; }
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
    public DateTime CreatedAt { get; set; }
}

public record DistributionTraceDto
{
    public string DrugName { get; set; } = string.Empty;
    public string BatchNo { get; set; } = string.Empty;
    public string? SupplierName { get; set; }
    public DateTime PurchaseDate { get; set; }
    public List<TraceTreeNode> FlowTree { get; set; } = new();
}

public record TraceTreeNode
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public decimal InQuantity { get; set; }
    public decimal OutQuantity { get; set; }
    public decimal StockQuantity { get; set; }
    public DateTime? Date { get; set; }
    public string? Province { get; set; }
    public bool IsChannelConflict { get; set; }
    public string? ConflictDesc { get; set; }
    public int Depth { get; set; }
    public List<TraceTreeNode> Children { get; set; } = new();
    public bool IsExpanded { get; set; } = true;
    public bool HasChildren => Children.Count > 0;
}

public record ChannelConflictDto
{
    public string DrugName { get; set; } = string.Empty;
    public string BatchNo { get; set; } = string.Empty;
    public string Distributor { get; set; } = string.Empty;
    public string ExpectedRegion { get; set; } = string.Empty;
    public string ActualCustomer { get; set; } = string.Empty;
    public string ActualRegion { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public DateTime Date { get; set; }
}

public record DownstreamRecordDto
{
    public Guid DrugId { get; set; }
    public string DrugName { get; set; } = string.Empty;
    public string? Specification { get; set; }
    public string BatchNo { get; set; } = string.Empty;
    /// <summary>供应商来源ID（与 FromCustomerId 互斥，供应商来源时设置此项）</summary>
    public Guid? SupplierId { get; set; }
    public Guid FromCustomerId { get; set; }
    public string FromCustomerName { get; set; } = string.Empty;
    public Guid ToCustomerId { get; set; }
    public string ToCustomerName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal TaxedUnitPrice { get; set; }
    public string? SalesPerson { get; set; }
    public string? DeliveryOrderNo { get; set; }
    public DateTime DistributionDate { get; set; } = DateTime.Now;
    public string? Province { get; set; }
    public string? DistributionDateDisplay { get; set; }
    public Guid? ImportBatchId { get; set; }
    public string? ImportBatchName { get; set; }
    public int? ImportRowNumber { get; set; }
}
