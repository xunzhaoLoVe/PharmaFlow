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

        var purchaseQuery = _db.PurchaseOrderItems
            .Include(i => i.Drug)
            .Include(i => i.PurchaseOrder).ThenInclude(o => o.Supplier)
            .AsNoTracking()
            .Where(i => i.PurchaseOrder.Status == OrderStatus.已完成
                && i.PurchaseOrder.OrderDate >= start
                && i.PurchaseOrder.OrderDate < endExclusive);
        if (!string.IsNullOrWhiteSpace(drugName))
            purchaseQuery = purchaseQuery.Where(i => i.Drug.Name.Contains(drugName));
        if (!string.IsNullOrWhiteSpace(customerName))
            purchaseQuery = purchaseQuery.Where(i => i.PurchaseOrder.Supplier.Name.Contains(customerName));
        var purchaseItems = await purchaseQuery.ToListAsync();

        var inventoryPurchaseQuery = _db.PurchaseOrderItems
            .Include(i => i.Drug)
            .Include(i => i.PurchaseOrder).ThenInclude(o => o.Supplier)
            .AsNoTracking()
            .Where(i => i.PurchaseOrder.Status == OrderStatus.已完成
                && i.PurchaseOrder.OrderDate < endExclusive);
        if (!string.IsNullOrWhiteSpace(drugName))
            inventoryPurchaseQuery = inventoryPurchaseQuery.Where(i => i.Drug.Name.Contains(drugName));
        if (!string.IsNullOrWhiteSpace(customerName))
            inventoryPurchaseQuery = inventoryPurchaseQuery.Where(i => i.PurchaseOrder.Supplier.Name.Contains(customerName));
        var inventoryPurchaseItems = await inventoryPurchaseQuery.ToListAsync();

        var salesQuery = _db.SalesOrderItems
            .Include(i => i.Drug)
            .Include(i => i.SalesOrder).ThenInclude(o => o.Customer)
            .AsNoTracking()
            .Where(i => i.SalesOrder.Status == OrderStatus.已完成
                && i.SalesOrder.OrderDate >= start
                && i.SalesOrder.OrderDate < endExclusive);
        if (!string.IsNullOrWhiteSpace(drugName))
            salesQuery = salesQuery.Where(i => i.Drug.Name.Contains(drugName));
        if (!string.IsNullOrWhiteSpace(customerName))
            salesQuery = salesQuery.Where(i => i.SalesOrder.Customer.Name.Contains(customerName));
        var salesItems = await salesQuery.ToListAsync();

        var firstLevelPurchaseFlowRecords = distributionRecords
            .Where(r => IsManufacturerToFirstLevelPurchase(r, selfId))
            .ToList();
        var usePurchaseOrdersForIntake = purchaseItems.Count > 0;

        var inventoryFirstLevelPurchaseFlowRecords = inventoryFlowRecords
            .Where(r => IsManufacturerToFirstLevelPurchase(r, selfId))
            .ToList();
        var usePurchaseOrdersForInventory = inventoryPurchaseItems.Count > 0;
        var inventoryBalanceFlowRecords = usePurchaseOrdersForInventory
            ? inventoryFlowRecords.Where(r => !IsManufacturerToFirstLevelPurchase(r, selfId)).ToList()
            : inventoryFlowRecords;

        var dealerTransferRecords = distributionRecords
            .Where(r => r.FromCustomer != null
                && r.ToCustomer != null
                && IsDealer(r.FromCustomer)
                && IsDealer(r.ToCustomer)
                && r.Quantity > 0
                && r.FromCustomerId != r.ToCustomerId)
            .ToList();

        var dealerTerminalSalesRecords = distributionRecords
            .Where(r => r.FromCustomer != null
                && r.ToCustomer != null
                && IsDealer(r.FromCustomer)
                && !IsDealer(r.ToCustomer)
                && r.ToCustomerId != selfId
                && r.Quantity > 0)
            .ToList();

        var channelFlowRecords = distributionRecords
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

        var salesQuantity = dealerTerminalSalesRecords.Sum(r => r.Quantity);
        var salesAmount = dealerTerminalSalesRecords.Sum(r => r.Quantity * r.TaxedUnitPrice);
        var purchaseQuantity = usePurchaseOrdersForIntake
            ? purchaseItems.Sum(i => (decimal)i.Quantity)
            : firstLevelPurchaseFlowRecords.Sum(r => r.Quantity);
        var purchaseAmount = usePurchaseOrdersForIntake
            ? purchaseItems.Sum(i => i.TotalPrice)
            : firstLevelPurchaseFlowRecords.Sum(r => r.Quantity * r.TaxedUnitPrice);
        var flowQuantity = channelFlowRecords.Sum(r => r.Quantity);
        var dealerInventoryQuantity = BuildDealerInventoryQuantity(
            usePurchaseOrdersForInventory ? inventoryPurchaseItems : Enumerable.Empty<PurchaseOrderItem>(),
            usePurchaseOrdersForInventory ? Enumerable.Empty<DistributionRecord>() : inventoryFirstLevelPurchaseFlowRecords,
            inventoryBalanceFlowRecords);
        var days = Math.Max(1, (end - start).Days + 1);

        var touchedCustomers = distributionRecords
            .SelectMany(r => new[] { r.ToCustomer, r.FromCustomer })
            .Where(c => c != null && c.Code != "SELF")
            .Select(c => c!)
            .GroupBy(c => c.Id)
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
        var topDrugs = BuildTopDrugs(Enumerable.Empty<SalesOrderItem>(), dealerTerminalSalesRecords);
        var topCustomers = BuildTopCustomers(Enumerable.Empty<SalesOrderItem>(), dealerTerminalSalesRecords);

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
                DealerTerminalSalesQuantity = salesQuantity,
                DealerInventoryQuantity = dealerInventoryQuantity,
                DealerTransferQuantity = dealerTransferRecords.Sum(r => r.Quantity),
                FlowRecordCount = distributionRecords.Count,
                BatchCount = distributionRecords.Select(r => r.BatchNo).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().Count(),
                DrugCount = distributionRecords.Select(r => r.DrugId)
                    .Concat(purchaseItems.Select(i => i.DrugId))
                    .Concat(salesItems.Select(i => i.DrugId))
                    .Distinct()
                    .Count(),
                DealerCount = touchedCustomers.Count(IsDealer)
                    + purchaseItems.Select(i => i.PurchaseOrder.Supplier.Name).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().Count(),
                TerminalCustomerCount = touchedCustomers.Count(c => !IsDealer(c)),
                ProvinceCount = touchedCustomers.Select(c => c.Province).Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().Count(),
                LowStockCount = lowStockRisks.Count,
                ExpiringCount = expiringRisks.Count,
                AverageDailyFlow = decimal.Round(flowQuantity / days, 2),
            },
            DrugDistribution = DimensionFromRecords(dealerTerminalSalesRecords, r => r.Drug.Name, r => r.Quantity, r => r.Quantity * r.TaxedUnitPrice, 8),
            CustomerDistribution = DimensionFromRecords(dealerTerminalSalesRecords, r => r.ToCustomer?.Name ?? "未知客户", r => r.Quantity, r => r.Quantity * r.TaxedUnitPrice, 8),
            ProvinceDistribution = DimensionFromRecords(dealerTerminalSalesRecords, r => FirstNonEmpty(r.ToCustomer?.Province, r.Province, "未知区域"), r => r.Quantity, r => r.Quantity * r.TaxedUnitPrice, 12),
            SourceDistribution = usePurchaseOrdersForIntake
                ? DimensionFromPurchaseItems(purchaseItems, 8)
                : DimensionFromRecords(firstLevelPurchaseFlowRecords, r => r.ToCustomer?.Name ?? SourceName(r), r => r.Quantity, r => r.Quantity * r.TaxedUnitPrice, 8),
            DealerLevelDistribution = BuildDealerLevelDistribution(touchedCustomers, purchaseItems),
            Trend = BuildTrend(
                start,
                end,
                grain,
                usePurchaseOrdersForIntake ? purchaseItems : Enumerable.Empty<PurchaseOrderItem>(),
                usePurchaseOrdersForIntake ? Enumerable.Empty<DistributionRecord>() : firstLevelPurchaseFlowRecords,
                Enumerable.Empty<SalesOrderItem>(),
                dealerTerminalSalesRecords,
                channelFlowRecords),
            TopDrugs = topDrugs,
            TopCustomers = topCustomers,
            InventoryRisks = BuildInventoryRisks(dealerInventories),
            EndCustomerSales = endCustomerSales.Take(300).ToList(),
        };
    }

    public async Task<List<EndCustomerSaleDto>> GetEndCustomerSalesAsync(
        string? drugName = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _db.DistributionRecords
            .Include(r => r.ToCustomer)
            .Include(r => r.Drug)
            .Where(r => r.ToCustomer != null
                && !r.ToCustomer.IsDeleted
                && r.ToCustomer.CustomerType != CustomerType.商业
                && r.Quantity > 0);

        if (!string.IsNullOrWhiteSpace(drugName))
            query = query.Where(r => r.Drug!.Name.Contains(drugName));

        if (startDate.HasValue)
            query = query.Where(r => r.DistributionDate >= startDate.Value.Date);

        if (endDate.HasValue)
            query = query.Where(r => r.DistributionDate < endDate.Value.Date.AddDays(1));

        return await query
            .GroupBy(r => new { ToName = r.ToCustomer!.Name, r.ToCustomer.Province, DrugName = r.Drug!.Name, r.Drug.Specification })
            .Select(g => new EndCustomerSaleDto
            {
                CustomerName = g.Key.ToName,
                Province = g.Key.Province,
                DrugName = g.Key.DrugName,
                Specification = g.Key.Specification,
                TotalQuantity = g.Sum(r => r.Quantity)
            })
            .OrderByDescending(r => r.TotalQuantity)
            .ToListAsync();
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

    private static bool IsFirstLevelDealer(Customer c)
        => c.CustomerType == CustomerType.商业 && c.Level == DistributorLevel.一级经销商;

    private static bool IsManufacturerToFirstLevelPurchase(DistributionRecord record, Guid? selfId)
        => record.ToCustomer != null
            && IsFirstLevelDealer(record.ToCustomer)
            && record.Quantity > 0
            && (record.SupplierId.HasValue
                || record.FromCustomerId == selfId
                || record.FromCustomer == null
                || !IsDealer(record.FromCustomer));

    private static int EffectiveLowStock(Drug drug)
        => drug.WarningLowQuantity > 0 ? drug.WarningLowQuantity : 10;

    private static decimal BuildDealerInventoryQuantity(
        IEnumerable<PurchaseOrderItem> purchaseItems,
        IEnumerable<DistributionRecord> firstLevelPurchaseRecords,
        IEnumerable<DistributionRecord> flowRecords)
    {
        var balances = new Dictionary<(string DealerName, Guid DrugId, string BatchNo), decimal>();

        foreach (var item in purchaseItems)
        {
            var dealerName = FirstNonEmpty(item.PurchaseOrder.Supplier?.Name, "一级经销商");
            var key = (dealerName, item.DrugId, item.BatchNo);
            balances[key] = balances.GetValueOrDefault(key) + item.Quantity;
        }

        foreach (var record in firstLevelPurchaseRecords)
        {
            if (record.ToCustomer == null) continue;
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

    private static List<ReportDimensionItemDto> DimensionFromPurchaseItems(IEnumerable<PurchaseOrderItem> items, int take)
    {
        var grouped = items
            .GroupBy(i => FirstNonEmpty(i.PurchaseOrder.Supplier?.Name, "一级经销商"))
            .Select(g => new ReportDimensionItemDto
            {
                Name = g.Key,
                Quantity = g.Sum(i => (decimal)i.Quantity),
                Amount = g.Sum(i => i.TotalPrice),
                Count = g.Select(i => i.PurchaseOrderId).Distinct().Count(),
            })
            .OrderByDescending(x => x.Quantity)
            .Take(take)
            .ToList();

        var total = grouped.Sum(x => x.Quantity);
        foreach (var item in grouped)
            item.Percent = total == 0 ? 0 : decimal.Round(item.Quantity * 100 / total, 2);
        return grouped;
    }

    private static List<ReportDimensionItemDto> BuildDealerLevelDistribution(IEnumerable<Customer> customers, IEnumerable<PurchaseOrderItem> purchaseItems)
    {
        var levelRows = customers
            .GroupBy(c => IsDealer(c) ? (c.Level?.ToString() ?? "未分级经销商") : "终端客户")
            .Select(g => new ReportDimensionItemDto
            {
                Name = g.Key,
                Count = g.Count(),
                Quantity = g.Count(),
            })
            .ToList();

        var purchaseDealerCount = purchaseItems
            .Select(i => i.PurchaseOrder.Supplier.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct()
            .Count();
        if (purchaseDealerCount > 0)
        {
            var tier1 = levelRows.FirstOrDefault(x => x.Name == DistributorLevel.一级经销商.ToString());
            if (tier1 == null)
            {
                levelRows.Add(new ReportDimensionItemDto
                {
                    Name = DistributorLevel.一级经销商.ToString(),
                    Count = purchaseDealerCount,
                    Quantity = purchaseDealerCount,
                });
            }
            else
            {
                tier1.Count += purchaseDealerCount;
                tier1.Quantity += purchaseDealerCount;
            }
        }

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
        IEnumerable<PurchaseOrderItem> purchaseItems,
        IEnumerable<DistributionRecord> supplierFallbackRecords,
        IEnumerable<SalesOrderItem> salesItems,
        IEnumerable<DistributionRecord> distributionSalesRecords,
        IEnumerable<DistributionRecord> downstreamFlowRecords)
    {
        var periods = EnumeratePeriods(start, end, grain)
            .ToDictionary(x => x.Key, x => new ReportTrendPointDto { Period = x.Label }, StringComparer.OrdinalIgnoreCase);

        foreach (var item in purchaseItems)
        {
            var key = PeriodKey(item.PurchaseOrder.OrderDate, grain);
            if (!periods.TryGetValue(key, out var point)) continue;
            point.PurchaseQuantity += item.Quantity;
            point.PurchaseAmount += item.TotalPrice;
        }
        foreach (var record in supplierFallbackRecords)
        {
            var key = PeriodKey(record.DistributionDate, grain);
            if (!periods.TryGetValue(key, out var point)) continue;
            point.PurchaseQuantity += record.Quantity;
            point.PurchaseAmount += record.Quantity * record.TaxedUnitPrice;
        }
        foreach (var item in salesItems)
        {
            var key = PeriodKey(item.SalesOrder.OrderDate, grain);
            if (!periods.TryGetValue(key, out var point)) continue;
            point.SalesQuantity += item.Quantity;
            point.SalesAmount += item.TotalPrice;
        }
        foreach (var record in distributionSalesRecords)
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

    private static List<ReportRankItemDto> BuildTopDrugs(
        IEnumerable<SalesOrderItem> salesItems,
        IEnumerable<DistributionRecord> distributionSalesRecords)
    {
        var rows = salesItems.Select(i => new
            {
                Name = i.Drug.Name,
                SubName = i.Drug.Specification,
                Quantity = (decimal)i.Quantity,
                Amount = i.TotalPrice,
            })
            .Concat(distributionSalesRecords.Select(r => new
            {
                Name = r.Drug.Name,
                SubName = r.Drug.Specification,
                Quantity = r.Quantity,
                Amount = r.Quantity * r.TaxedUnitPrice,
            }));

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

    private static List<ReportRankItemDto> BuildTopCustomers(
        IEnumerable<SalesOrderItem> salesItems,
        IEnumerable<DistributionRecord> distributionSalesRecords)
    {
        var rows = salesItems.Select(i => new
            {
                Name = i.SalesOrder.Customer.Name,
                SubName = i.SalesOrder.Customer.Province,
                Quantity = (decimal)i.Quantity,
                Amount = i.TotalPrice,
            })
            .Concat(distributionSalesRecords.Select(r => new
            {
                Name = r.ToCustomer?.Name ?? "未知客户",
                SubName = r.ToCustomer?.Province ?? r.Province,
                Quantity = r.Quantity,
                Amount = r.Quantity * r.TaxedUnitPrice,
            }));

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
            .GroupBy(r => new { r.ToCustomer!.Name, r.ToCustomer.Province, DrugName = r.Drug.Name, r.Drug.Specification })
            .Select(g => new EndCustomerSaleDto
            {
                CustomerName = g.Key.Name,
                Province = g.Key.Province,
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
