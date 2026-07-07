namespace PharmaFlow.Application.DTOs;

public record PurchaseOrderItemDto
{
    public Guid DrugId { get; set; }
    public string DrugName { get; set; } = string.Empty;
    public string BatchNo { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal TaxedUnitPrice { get; set; }
    public decimal TotalPrice => Quantity * TaxedUnitPrice;
    public decimal TotalTaxedPrice => Quantity * TaxedUnitPrice;
    public DateTime? ProductionDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
}

public record SalesOrderItemDto
{
    public Guid DrugId { get; set; }
    public string DrugName { get; set; } = string.Empty;
    public string BatchNo { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public DateTime? ProductionDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
}

public record PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int TotalCount { get; init; }
    public int PageIndex { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public record DropdownItem
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
}

public record DrugDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? GenericName { get; set; }
    public string? Specification { get; set; }
    public string? Manufacturer { get; set; }
    public string? ApprovalNumber { get; set; }
    public string? DosageForm { get; set; }
    public string Unit { get; set; } = "盒";
    public string? CategoryName { get; set; }
    public Guid? CategoryId { get; set; }
    public int WarningLowQuantity { get; set; }
}

public record CustomerDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Province { get; set; }
    public string? SalesPerson { get; set; }
    public string? DrugName { get; set; }
    public string? CustomerTypeName { get; set; }
    public string? CategoryName { get; set; }
    public Guid? CategoryId { get; set; }
    public int? Level { get; set; }
    public string? LevelName { get; set; }
}

public record SupplierDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
}

public record PurchaseOrderDto
{
    public Guid Id { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public Guid SupplierId { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Operator { get; set; }
    public string? Remark { get; set; }
    public int ItemCount { get; set; }
}

public record PurchaseOrderDetailDto
{
    public Guid Id { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Operator { get; set; }
    public string? Remark { get; set; }
    public List<PurchaseOrderItemDetailDto> Items { get; set; } = new();
}

public record PurchaseOrderItemDetailDto
{
    public string DrugName { get; set; } = string.Empty;
    public string BatchNo { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public DateTime? ProductionDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
}

public record SalesOrderDto
{
    public Guid Id { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Operator { get; set; }
    public string? Remark { get; set; }
    public int ItemCount { get; set; }
}

public record SalesOrderDetailDto
{
    public Guid Id { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Operator { get; set; }
    public string? Remark { get; set; }
    public List<SalesOrderItemDetailDto> Items { get; set; } = new();
}

public record SalesOrderItemDetailDto
{
    public string DrugName { get; set; } = string.Empty;
    public string BatchNo { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public DateTime? ProductionDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
}

public record InventoryDto
{
    public Guid Id { get; set; }
    public string DrugCode { get; set; } = string.Empty;
    public string DrugName { get; set; } = string.Empty;
    public string Specification { get; set; } = string.Empty;
    public string BatchNo { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string? Location { get; set; }
    public bool IsLowStock { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerLevel { get; set; }
    public string? Province { get; set; }
}

public record DashboardStats
{
    public int TodayPurchaseCount { get; set; }
    public int TodaySalesCount { get; set; }
    public int TotalDrugTypes { get; set; }
    public int LowStockCount { get; set; }
    public int ExpiringCount { get; set; }
    public decimal TodayPurchaseAmount { get; set; }
    public decimal TodaySalesAmount { get; set; }
    public List<ChartDataPoint> SalesTrend { get; set; } = new();
}

public record ChartDataPoint
{
    public string Label { get; set; } = string.Empty;
    public decimal Value { get; set; }
}

public record LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public record LoginResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public Guid? UserId { get; set; }
    public string? DisplayName { get; set; }
    public string? Role { get; set; }
    public List<string> Permissions { get; set; } = new();
}

public record EndCustomerSaleDto
{
    public string CustomerName { get; set; } = string.Empty;
    public string? Province { get; set; }
    public string DrugName { get; set; } = string.Empty;
    public string? Specification { get; set; }
    public decimal TotalQuantity { get; set; }
}

public record UserDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public bool IsActive { get; set; }
    public string? Role { get; set; }
    public Guid? RoleId { get; set; }
    public DateTime? LastLoginTime { get; set; }
}

public record CreateUserRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public Guid? RoleId { get; set; }
}

public record UpdateUserRequest
{
    public string? DisplayName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public Guid? RoleId { get; set; }
    public bool? IsActive { get; set; }
}

public record CollectionSourceDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PlatformType { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string AuthType { get; set; } = string.Empty;
    public string CollectionMode { get; set; } = "JsonConfig";
    public string AuthConfig { get; set; } = "{}";
    public string ExportConfig { get; set; } = "{}";
    public string TransformConfig { get; set; } = "{}";
    public bool IsEnabled { get; set; }
    public string? ScheduleCron { get; set; }
    public DateTime? LastCollectionAt { get; set; }
    public string? LastCollectionStatus { get; set; }
    public bool AutoSubmit { get; set; }
    public Guid? RuleId { get; set; }
    public string? RuleName { get; set; }
}

public record CollectionTaskDto
{
    public Guid Id { get; set; }
    public Guid SourceId { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public string TriggerType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int TotalRecords { get; set; }
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
    public int SkippedCount { get; set; }
    public bool IsSubmitted { get; set; }
    public string? ErrorMessage { get; set; }
}

public record CollectionRecordDto
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public Guid SourceId { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string RawDataJson { get; set; } = "{}";
    public string? CleanedDataJson { get; set; }
    public string? TargetEntityType { get; set; }
    public Guid? TargetEntityId { get; set; }
    public string? ReviewerName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? RejectReason { get; set; }
    public string? ErrorMessage { get; set; }
}

public record CollectionSummaryRecordDto
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public Guid SourceId { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public string DrugName { get; set; } = string.Empty;
    public string? Specification { get; set; }
    public string? BatchNo { get; set; }
    public string? CompanyName { get; set; }
    public string? ToCustomerName { get; set; }
    public decimal? TaxedUnitPrice { get; set; }
    public decimal? Quantity { get; set; }
    public string? DistributionDate { get; set; }
    public string? Province { get; set; }
    public string? ExpiryDate { get; set; }
    public Guid? TargetEntityId { get; set; }
    public string SyncStatus { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; }
}

public record CollectionSummaryFilterOptionsDto
{
    public List<string> DrugNames { get; set; } = new();
    public List<string> Specifications { get; set; } = new();
    public List<string> BatchNos { get; set; } = new();
    public List<string> Provinces { get; set; } = new();
}

public record ReviewRequest
{
    public List<Guid> RecordIds { get; set; } = new();
}

public record RejectRequest
{
    public List<Guid> RecordIds { get; set; } = new();
    public string Reason { get; set; } = string.Empty;
}

public record ManualTriggerRequest
{
    public Guid SourceId { get; set; }
}

public record TokenUpdateRequest
{
    public string Token { get; set; } = "";
    public string? CookieName { get; set; }
}

public record AutoSubmitRequest
{
    public bool AutoSubmit { get; set; }
}

public record ReapplyTransformResultDto
{
    public Guid SourceId { get; set; }
    public int TotalRecords { get; set; }
    public int UpdatedRecords { get; set; }
    public int ErrorRecords { get; set; }
}

public record SyncSummaryResultDto
{
    public int TotalRecords { get; set; }
    public int CreatedDrugs { get; set; }
    public int CreatedCustomers { get; set; }
    public int CreatedDistributionRecords { get; set; }
    public int SkippedRecords { get; set; }
}

// ── 批次处理 ──

public record BatchPreviewDto
{
    public Guid TaskId { get; set; }
    public int TotalRecords { get; set; }
    public List<DrugMatchItem> Drugs { get; set; } = new();
    public List<CustomerMatchItem> Customers { get; set; } = new();
}

public record DrugMatchItem
{
    public string Name { get; set; } = "";
    public string? OriginalSpec { get; set; }
    public string? NormalizedSpec { get; set; }
    public string? Manufacturer { get; set; }
    public string? Code { get; set; }
    public bool Matched { get; set; }
    public Guid? ExistingId { get; set; }
    public string? ExistingName { get; set; }
    public string? ExistingSpec { get; set; }
}

public record CustomerMatchItem
{
    public string Name { get; set; } = "";
    public string? Code { get; set; }
    public string? Province { get; set; }
    public bool Matched { get; set; }
    public Guid? ExistingId { get; set; }
    public string? ExistingName { get; set; }
}

public record BatchImportResult
{
    public int CreatedDrugs { get; set; }
    public int CreatedCustomers { get; set; }
    public int Imported { get; set; }
    public int Skipped { get; set; }
}

public record DistributionReviewDto
{
    public Guid Id { get; set; }
    public string DrugName { get; set; } = "";
    public string? BatchNo { get; set; }
    public string? ToCustomerName { get; set; }
    public decimal Quantity { get; set; }
    public decimal TaxedUnitPrice { get; set; }
    public DateTime DistributionDate { get; set; }
    public string? Province { get; set; }
    public string? ReviewStatus { get; set; }
    public string? RejectReason { get; set; }
    public Guid? TaskId { get; set; }
}

// ── 导入规则 ──

public record ImportRuleDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public List<ModuleRuleConfigDto> ModuleConfigs { get; set; } = new();
}

public record ModuleRuleConfigDto
{
    public Guid Id { get; set; }
    public Guid RuleId { get; set; }
    public string ModuleName { get; set; } = "";
    public string FieldMappings { get; set; } = "{}";
    public string? NormalizeRules { get; set; }
    public string? MatchConfig { get; set; }
    public bool AutoCreate { get; set; }
}
