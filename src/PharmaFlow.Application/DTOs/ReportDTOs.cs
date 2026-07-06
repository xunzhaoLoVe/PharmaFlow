namespace PharmaFlow.Application.DTOs;

public record ReportOverviewDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Granularity { get; set; } = "day";
    public ReportMetricDto Metrics { get; set; } = new();
    public List<ReportDimensionItemDto> DrugDistribution { get; set; } = new();
    public List<ReportDimensionItemDto> CustomerDistribution { get; set; } = new();
    public List<ReportDimensionItemDto> ProvinceDistribution { get; set; } = new();
    public List<ReportDimensionItemDto> SourceDistribution { get; set; } = new();
    public List<ReportDimensionItemDto> DealerLevelDistribution { get; set; } = new();
    public List<ReportTrendPointDto> Trend { get; set; } = new();
    public List<ReportRankItemDto> TopDrugs { get; set; } = new();
    public List<ReportRankItemDto> TopCustomers { get; set; } = new();
    public List<ReportInventoryRiskDto> InventoryRisks { get; set; } = new();
    public List<EndCustomerSaleDto> EndCustomerSales { get; set; } = new();
}

public record ReportMetricDto
{
    public decimal SalesQuantity { get; set; }
    public decimal SalesAmount { get; set; }
    public decimal PurchaseQuantity { get; set; }
    public decimal PurchaseAmount { get; set; }
    public decimal FlowQuantity { get; set; }
    public decimal InventoryQuantity { get; set; }
    public decimal DealerPurchaseQuantity { get; set; }
    public decimal DealerTerminalSalesQuantity { get; set; }
    public decimal DealerInventoryQuantity { get; set; }
    public decimal DealerTransferQuantity { get; set; }
    public int FlowRecordCount { get; set; }
    public int BatchCount { get; set; }
    public int DrugCount { get; set; }
    public int DealerCount { get; set; }
    public int TerminalCustomerCount { get; set; }
    public int ProvinceCount { get; set; }
    public int LowStockCount { get; set; }
    public int ExpiringCount { get; set; }
    public decimal AverageDailyFlow { get; set; }
}

public record ReportDimensionItemDto
{
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal Amount { get; set; }
    public int Count { get; set; }
    public decimal Percent { get; set; }
}

public record ReportTrendPointDto
{
    public string Period { get; set; } = string.Empty;
    public decimal SalesQuantity { get; set; }
    public decimal PurchaseQuantity { get; set; }
    public decimal FlowQuantity { get; set; }
    public decimal SalesAmount { get; set; }
    public decimal PurchaseAmount { get; set; }
}

public record ReportRankItemDto
{
    public string Name { get; set; } = string.Empty;
    public string? SubName { get; set; }
    public decimal Quantity { get; set; }
    public decimal Amount { get; set; }
    public int Count { get; set; }
}

public record ReportInventoryRiskDto
{
    public string DrugName { get; set; } = string.Empty;
    public string? Specification { get; set; }
    public string BatchNo { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public decimal Quantity { get; set; }
    public int WarningLowQuantity { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string RiskType { get; set; } = string.Empty;
}
