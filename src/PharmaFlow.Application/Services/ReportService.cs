using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PharmaFlow.Application.DTOs;
using PharmaFlow.Domain.Entities;
using PharmaFlow.Domain.Enums;
using PharmaFlow.Infrastructure.Data;

namespace PharmaFlow.Application.Services;

public class ReportService : IReportService
{
    private readonly AppDbContext _db;
    public ReportService(AppDbContext db) => _db = db;

    public async Task<ReportOverviewDto> GetOverviewAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? granularity = null,
        string? drugName = null,
        string? customerName = null)
    {
        var start = (startDate ?? DateTime.Today.AddDays(-29)).Date;
        var end = (endDate ?? DateTime.Today).Date;
        if (end < start) (start, end) = (end, start);
        var endExclusive = end.AddDays(1);
        var grain = NormalizeGranularity(granularity);

        var selfId = await _db.Customers
            .Where(c => c.Code == "SELF")
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync();

        var distributionQuery = _db.DistributionRecords
            .Include(r => r.Drug)
            .Include(r => r.ToCustomer)
            .Include(r => r.FromCustomer)
            .Include(r => r.Supplier)
            .AsNoTracking()
            .Where(r => r.DistributionDate >= start && r.DistributionDate < endExclusive);

        if (!string.IsNullOrWhiteSpace(drugName))
            distributionQuery = distributionQuery.Where(r => r.Drug.Name.Contains(drugName));
        if (!string.IsNullOrWhiteSpace(customerName))
            distributionQuery = distributionQuery.Where(r =>
                (r.ToCustomer != null && r.ToCustomer.Name.Contains(customerName))
                || (r.FromCustomer != null && r.FromCustomer.Name.Contains(customerName))
                || (r.Supplier != null && r.Supplier.Name.Contains(customerName)));

        var distributionRecords = await distributionQuery.ToListAsync();

        var inventoryFlowQuery = _db.DistributionRecords
            .Include(r => r.Drug)
            .Include(r => r.ToCustomer)
            .Include(r => r.FromCustomer)
            .AsNoTracking()
            .Where(r => r.DistributionDate < endExclusive);
        if (!string.IsNullOrWhiteSpace(drugName))
            inventoryFlowQuery = inventoryFlowQuery.Where(r => r.Drug.Name.Contains(drugName));
        if (!string.IsNullOrWhiteSpace(customerName))
            inventoryFlowQuery = inventoryFlowQuery.Where(r =>
                (r.ToCustomer != null && r.ToCustomer.Name.Contains(customerName))
                || (r.FromCustomer != null && r.FromCustomer.Name.Contains(customerName)));
        var inventoryFlowRecords = await inventoryFlowQuery.ToListAsync();

        var purchaseManagementRecords = distributionRecords
            .Where(r => IsPurchaseManagementRecord(r, selfId))
            .ToList();
        var salesManagementRecords = distributionRecords
            .Where(r => IsSalesManagementRecord(r, selfId))
            .ToList();

        var inventoryPurchaseManagementRecords = inventoryFlowRecords
            .Where(r => IsPurchaseManagementRecord(r, selfId))
            .ToList();
        var inventorySalesManagementRecords = inventoryFlowRecords
            .Where(r => IsSalesManagementRecord(r, selfId))
            .ToList();

        var dealerTransferRecords = salesManagementRecords
            .Where(r => r.FromCustomer != null
                && r.ToCustomer != null
                && IsDealer(r.FromCustomer)
                && IsDealer(r.ToCustomer)
                && r.Quantity > 0
                && r.FromCustomerId != r.ToCustomerId)
            .ToList();

        var dealerTerminalSalesRecords = salesManagementRecords
            .Where(r => r.FromCustomer != null
                && r.ToCustomer != null
                && IsDealer(r.FromCustomer)
                && !IsDealer(r.ToCustomer)
                && r.ToCustomerId != selfId
                && r.Quantity > 0)
            .ToList();

        var channelFlowRecords = salesManagementRecords
            .Where(r => r.Quantity > 0
                && ((r.FromCustomer != null && IsDealer(r.FromCustomer))
                    || (r.ToCustomer != null && IsDealer(r.ToCustomer))))
            .ToList();

        var inventoryQuery = _db.Inventories
            .Include(i => i.Drug)
            .Include(i => i.Customer)
            .AsNoTracking()
            .AsQueryable();
        if (!string.IsNullOrWhiteSpace(drugName))
            inventoryQuery = inventoryQuery.Where(i => i.Drug.Name.Contains(drugName));
        if (!string.IsNullOrWhiteSpace(customerName))
            inventoryQuery = inventoryQuery.Where(i => i.Customer != null && i.Customer.Name.Contains(customerName));
        var inventories = await inventoryQuery.ToListAsync();

        var salesQuantity = salesManagementRecords.Sum(r => r.Quantity);
        var salesAmount = salesManagementRecords.Sum(r => r.Quantity * r.TaxedUnitPrice);
        var terminalSalesQuantity = dealerTerminalSalesRecords.Sum(r => r.Quantity);
        var purchaseQuantity = purchaseManagementRecords.Sum(r => r.Quantity);
        var purchaseAmount = purchaseManagementRecords.Sum(r => r.Quantity * r.TaxedUnitPrice);
        var flowQuantity = channelFlowRecords.Sum(r => r.Quantity);
        var dealerInventoryQuantity = BuildDealerInventoryQuantity(
            inventoryPurchaseManagementRecords,
            inventorySalesManagementRecords);
        var days = Math.Max(1, (end - start).Days + 1);

        var touchedCustomers = distributionRecords
            .SelectMany(r => new[] { r.ToCustomer, r.FromCustomer })
            .Where(c => c != null && c.Code != "SELF")
            .Select(c => c!)
            .GroupBy(c => CustomerNameKey(c.Name))
            .Select(g => g.First())
            .ToList();

        var dealerInventories = inventories
            .Where(i => i.Customer != null && IsDealer(i.Customer))
            .ToList();
        var lowStockRisks = dealerInventories
            .Where(i => i.Quantity > 0 && i.Quantity <= EffectiveLowStock(i.Drug))
            .ToList();
        var expiringRisks = dealerInventories
            .Where(i => i.Quantity > 0 && i.ExpiryDate.HasValue && i.ExpiryDate.Value.Date <= DateTime.Today.AddDays(180))
            .ToList();

        var endCustomerSales = BuildEndCustomerSales(dealerTerminalSalesRecords);

        return new ReportOverviewDto
        {
            StartDate = start,
            EndDate = end,
            Granularity = grain,
            Metrics = new ReportMetricDto
            {
                SalesQuantity = salesQuantity,
                SalesAmount = salesAmount,
                PurchaseQuantity = purchaseQuantity,
                PurchaseAmount = purchaseAmount,
                FlowQuantity = flowQuantity,
                InventoryQuantity = dealerInventoryQuantity,
                DealerPurchaseQuantity = purchaseQuantity,
                DealerTerminalSalesQuantity = terminalSalesQuantity,
                DealerInventoryQuantity = dealerInventoryQuantity,
                DealerTransferQuantity = dealerTransferRecords.Sum(r => r.Quantity),
                FlowRecordCount = distributionRecords.Count,
                BatchCount = distributionRecords.Select(r => r.BatchNo).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().Count(),
                DrugCount = distributionRecords.Select(r => r.DrugId)
                    .Distinct()
                    .Count(),
                DealerCount = touchedCustomers.Count(IsDealer),
                TerminalCustomerCount = touchedCustomers.Count(c => !IsDealer(c)),
                ProvinceCount = touchedCustomers.Select(c => c.Province).Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().Count(),
                LowStockCount = lowStockRisks.Count,
                ExpiringCount = expiringRisks.Count,
                AverageDailyFlow = decimal.Round(flowQuantity / days, 2),
            },
            DrugDistribution = DimensionFromRecords(salesManagementRecords, r => r.Drug.Name, r => r.Quantity, r => r.Quantity * r.TaxedUnitPrice, 8),
            CustomerDistribution = DimensionFromRecords(salesManagementRecords, r => r.ToCustomer?.Name ?? "未知客户", r => r.Quantity, r => r.Quantity * r.TaxedUnitPrice, 8),
            ProvinceDistribution = DimensionFromRecords(salesManagementRecords, r => FirstNonEmpty(r.ToCustomer?.Province, r.Province, "未知区域"), r => r.Quantity, r => r.Quantity * r.TaxedUnitPrice, 12),
            SourceDistribution = DimensionFromRecords(purchaseManagementRecords, r => r.ToCustomer?.Name ?? SourceName(r), r => r.Quantity, r => r.Quantity * r.TaxedUnitPrice, 8),
            DealerLevelDistribution = BuildDealerLevelDistribution(touchedCustomers),
            Trend = BuildTrend(
                start,
                end,
                grain,
                purchaseManagementRecords,
                salesManagementRecords,
                channelFlowRecords),
            TopDrugs = BuildTopDrugs(salesManagementRecords),
            TopCustomers = BuildTopCustomers(salesManagementRecords),
            InventoryRisks = BuildInventoryRisks(dealerInventories),
            EndCustomerSales = endCustomerSales.Take(300).ToList(),
        };
    }

    public async Task<List<EndCustomerSaleDto>> GetEndCustomerSalesAsync(
        string? drugName = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        var selfId = await _db.Customers
            .Where(c => c.Code == "SELF")
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync();

        var query = _db.DistributionRecords
            .Include(r => r.FromCustomer)
            .Include(r => r.ToCustomer)
            .Include(r => r.Drug)
            .Where(r => r.ToCustomer != null
                && !r.ToCustomer.IsDeleted
                && r.ToCustomer.CustomerType != CustomerType.商业
                && r.Quantity > 0
                && !r.SupplierId.HasValue);

        if (selfId.HasValue)
            query = query.Where(r => r.FromCustomerId != selfId.Value && r.ToCustomerId != selfId.Value);

        if (!string.IsNullOrWhiteSpace(drugName))
            query = query.Where(r => r.Drug!.Name.Contains(drugName));

        if (startDate.HasValue)
            query = query.Where(r => r.DistributionDate >= startDate.Value.Date);

        if (endDate.HasValue)
            query = query.Where(r => r.DistributionDate < endDate.Value.Date.AddDays(1));

        var records = await query.AsNoTracking().ToListAsync();
        return BuildEndCustomerSales(records);
    }

    private static string NormalizeGranularity(string? granularity)
        => granularity?.ToLowerInvariant() switch
        {
            "week" => "week",
            "month" => "month",
            _ => "day"
        };

    private static string Key(Guid drugId, string batchNo)
        => $"{drugId:N}|{batchNo.Trim()}";

    private static bool IsDealer(Customer c)
        => c.CustomerType == CustomerType.商业;

    private static bool IsPurchaseManagementRecord(DistributionRecord record, Guid? selfId)
        => record.SupplierId.HasValue
            || (selfId.HasValue && (record.FromCustomerId == selfId.Value || record.ToCustomerId == selfId.Value));

    private static bool IsSalesManagementRecord(DistributionRecord record, Guid? selfId)
        => !record.SupplierId.HasValue
            && (!selfId.HasValue || (record.FromCustomerId != selfId.Value && record.ToCustomerId != selfId.Value));

    private static int EffectiveLowStock(Drug drug)
        => drug.WarningLowQuantity > 0 ? drug.WarningLowQuantity : 10;

    private static decimal BuildDealerInventoryQuantity(
        IEnumerable<DistributionRecord> purchaseRecords,
        IEnumerable<DistributionRecord> flowRecords)
    {
        var balances = new Dictionary<(string DealerName, Guid DrugId, string BatchNo), decimal>();

        foreach (var record in purchaseRecords)
        {
            if (record.ToCustomer == null || !IsDealer(record.ToCustomer)) continue;
            var key = (record.ToCustomer.Name, record.DrugId, record.BatchNo);
            balances[key] = balances.GetValueOrDefault(key) + record.Quantity;
        }

        foreach (var record in flowRecords)
        {
            if (record.ToCustomer != null && IsDealer(record.ToCustomer) && record.ToCustomerId.HasValue)
            {
                var key = (record.ToCustomer.Name, record.DrugId, record.BatchNo);
                balances[key] = balances.GetValueOrDefault(key) + record.Quantity;
            }

            if (record.FromCustomer != null && IsDealer(record.FromCustomer) && record.FromCustomerId.HasValue)
            {
                var key = (record.FromCustomer.Name, record.DrugId, record.BatchNo);
                balances[key] = balances.GetValueOrDefault(key) - record.Quantity;
            }
        }

        return balances.Values.Where(q => q > 0).Sum();
    }

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? "";

    private static string CustomerNameKey(string? name)
        => string.IsNullOrWhiteSpace(name) ? "UNKNOWN" : name.Trim();

    private static string SourceName(DistributionRecord r)
        => FirstNonEmpty(r.Supplier?.Name, r.FromCustomer?.Name, "未知来源");

    private static List<ReportDimensionItemDto> DimensionFromRecords(
        IEnumerable<DistributionRecord> records,
        Func<DistributionRecord, string> nameSelector,
        Func<DistributionRecord, decimal> quantitySelector,
        Func<DistributionRecord, decimal> amountSelector,
        int take)
    {
        var grouped = records
            .GroupBy(r => FirstNonEmpty(nameSelector(r), "未分类"))
            .Select(g => new ReportDimensionItemDto
            {
                Name = g.Key,
                Quantity = g.Sum(quantitySelector),
                Amount = g.Sum(amountSelector),
                Count = g.Count(),
            })
            .OrderByDescending(x => x.Quantity)
            .Take(take)
            .ToList();

        var total = grouped.Sum(x => x.Quantity);
        foreach (var item in grouped)
            item.Percent = total == 0 ? 0 : decimal.Round(item.Quantity * 100 / total, 2);
        return grouped;
    }

    private static List<ReportDimensionItemDto> BuildDealerLevelDistribution(IEnumerable<Customer> customers)
    {
        var levelRows = customers
            .GroupBy(c => IsDealer(c) ? (c.Level?.ToString() ?? "未分级经销商") : "终端机构")
            .Select(g => new ReportDimensionItemDto
            {
                Name = g.Key,
                Count = g.Count(),
                Quantity = g.Count(),
            })
            .ToList();

        var grouped = levelRows.OrderByDescending(x => x.Count).ToList();
        var total = grouped.Sum(x => x.Count);
        foreach (var item in grouped)
            item.Percent = total == 0 ? 0 : decimal.Round(item.Count * 100m / total, 2);
        return grouped;
    }

    private static List<ReportTrendPointDto> BuildTrend(
        DateTime start,
        DateTime end,
        string grain,
        IEnumerable<DistributionRecord> purchaseRecords,
        IEnumerable<DistributionRecord> salesRecords,
        IEnumerable<DistributionRecord> downstreamFlowRecords)
    {
        var periods = EnumeratePeriods(start, end, grain)
            .ToDictionary(x => x.Key, x => new ReportTrendPointDto { Period = x.Label }, StringComparer.OrdinalIgnoreCase);

        foreach (var record in purchaseRecords)
        {
            var key = PeriodKey(record.DistributionDate, grain);
            if (!periods.TryGetValue(key, out var point)) continue;
            point.PurchaseQuantity += record.Quantity;
            point.PurchaseAmount += record.Quantity * record.TaxedUnitPrice;
        }
        foreach (var record in salesRecords)
        {
            var key = PeriodKey(record.DistributionDate, grain);
            if (!periods.TryGetValue(key, out var point)) continue;
            point.SalesQuantity += record.Quantity;
            point.SalesAmount += record.Quantity * record.TaxedUnitPrice;
        }
        foreach (var record in downstreamFlowRecords)
        {
            var key = PeriodKey(record.DistributionDate, grain);
            if (!periods.TryGetValue(key, out var point)) continue;
            point.FlowQuantity += record.Quantity;
        }

        return periods.Values.ToList();
    }

    private static List<(string Key, string Label)> EnumeratePeriods(DateTime start, DateTime end, string grain)
    {
        var result = new List<(string Key, string Label)>();
        var cursor = grain == "month"
            ? new DateTime(start.Year, start.Month, 1)
            : grain == "week"
                ? start.AddDays(-(int)start.DayOfWeek + 1)
                : start;

        while (cursor <= end)
        {
            result.Add((PeriodKey(cursor, grain), PeriodLabel(cursor, grain)));
            cursor = grain switch
            {
                "month" => cursor.AddMonths(1),
                "week" => cursor.AddDays(7),
                _ => cursor.AddDays(1),
            };
        }
        return result;
    }

    private static string PeriodKey(DateTime date, string grain)
        => grain switch
        {
            "month" => date.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            "week" => $"{ISOWeek.GetYear(date)}-W{ISOWeek.GetWeekOfYear(date):00}",
            _ => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        };

    private static string PeriodLabel(DateTime date, string grain)
        => grain switch
        {
            "month" => date.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            "week" => $"{date:MM/dd}周",
            _ => date.ToString("MM/dd", CultureInfo.InvariantCulture),
        };

    private static List<ReportRankItemDto> BuildTopDrugs(IEnumerable<DistributionRecord> salesRecords)
    {
        var rows = salesRecords.Select(r => new
            {
                Name = r.Drug.Name,
                SubName = r.Drug.Specification,
                Quantity = r.Quantity,
                Amount = r.Quantity * r.TaxedUnitPrice,
            });

        return rows.GroupBy(x => new { x.Name, x.SubName })
            .Select(g => new ReportRankItemDto
            {
                Name = g.Key.Name,
                SubName = g.Key.SubName,
                Quantity = g.Sum(x => x.Quantity),
                Amount = g.Sum(x => x.Amount),
                Count = g.Count(),
            })
            .OrderByDescending(x => x.Quantity)
            .Take(10)
            .ToList();
    }

    private static List<ReportRankItemDto> BuildTopCustomers(IEnumerable<DistributionRecord> salesRecords)
    {
        var rows = salesRecords.Select(r => new
            {
                Name = r.ToCustomer?.Name ?? "未知客户",
                SubName = r.ToCustomer?.Province ?? r.Province,
                Quantity = r.Quantity,
                Amount = r.Quantity * r.TaxedUnitPrice,
            });

        return rows.GroupBy(x => new { x.Name, x.SubName })
            .Select(g => new ReportRankItemDto
            {
                Name = g.Key.Name,
                SubName = g.Key.SubName,
                Quantity = g.Sum(x => x.Quantity),
                Amount = g.Sum(x => x.Amount),
                Count = g.Count(),
            })
            .OrderByDescending(x => x.Quantity)
            .Take(10)
            .ToList();
    }

    private static List<EndCustomerSaleDto> BuildEndCustomerSales(IEnumerable<DistributionRecord> records)
        => records
            .Where(r => r.ToCustomer != null && !IsDealer(r.ToCustomer))
            .GroupBy(r => new { CustomerKey = CustomerNameKey(r.ToCustomer!.Name), DrugName = r.Drug.Name, r.Drug.Specification })
            .Select(g => new EndCustomerSaleDto
            {
                CustomerName = FirstNonEmpty(g.First().ToCustomer?.Name, "未知客户"),
                Province = FirstNonEmpty(g.Select(r => r.ToCustomer?.Province).ToArray()),
                DrugName = g.Key.DrugName,
                Specification = g.Key.Specification,
                TotalQuantity = g.Sum(r => r.Quantity),
            })
            .OrderByDescending(x => x.TotalQuantity)
            .ToList();

    private static List<ReportInventoryRiskDto> BuildInventoryRisks(IEnumerable<Inventory> inventories)
        => inventories
            .Select(i =>
            {
                var isLow = i.Quantity > 0 && i.Quantity <= EffectiveLowStock(i.Drug);
                var isExpiring = i.Quantity > 0 && i.ExpiryDate.HasValue && i.ExpiryDate.Value.Date <= DateTime.Today.AddDays(180);
                if (!isLow && !isExpiring) return null;
                return new ReportInventoryRiskDto
                {
                    DrugName = i.Drug.Name,
                    Specification = i.Drug.Specification,
                    BatchNo = i.BatchNo,
                    CustomerName = i.Customer?.Name,
                    Quantity = i.Quantity,
                    WarningLowQuantity = EffectiveLowStock(i.Drug),
                    ExpiryDate = i.ExpiryDate,
                    RiskType = isLow && isExpiring ? "低库存/近效期" : isLow ? "低库存" : "近效期",
                };
            })
            .Where(x => x != null)
            .Select(x => x!)
            .OrderBy(x => x.RiskType)
            .ThenBy(x => x.ExpiryDate ?? DateTime.MaxValue)
            .ThenBy(x => x.Quantity)
            .Take(20)
            .ToList();
}
