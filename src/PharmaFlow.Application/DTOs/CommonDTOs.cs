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
