using Microsoft.EntityFrameworkCore;
using PharmaFlow.Application.DTOs;
using PharmaFlow.Domain.Entities;
using PharmaFlow.Infrastructure.Data;

namespace PharmaFlow.Application.Services;

public class DashboardService : IDashboardService
{
    private readonly AppDbContext _db;
    public DashboardService(AppDbContext db) => _db = db;

    public async Task<DashboardStats> GetStatsAsync()
    {
        var todayStart = DateTime.Today;
        var todayEnd = todayStart.AddDays(1);
        var selfId = await _db.Customers
            .Where(c => c.Code == "SELF")
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync();

        var todayFlows = await _db.DistributionRecords
            .Where(r => r.DistributionDate >= todayStart && r.DistributionDate < todayEnd)
            .ToListAsync();
        var todayPurchases = todayFlows.Where(r => IsPurchaseManagementRecord(r, selfId)).ToList();
        var todaySales = todayFlows.Where(r => IsSalesManagementRecord(r, selfId)).ToList();

        var totalDrugTypes = await _db.Drugs.Where(d => !d.IsDeleted).CountAsync();
        var lowStockCount = await BuildLowStockCountAsync();
        var expiringCount = await _db.Inventories
            .CountAsync(i => i.ExpiryDate.HasValue && i.ExpiryDate <= DateTime.UtcNow.AddDays(180) && i.Quantity > 0);

        // Sales trend for last 7 days
        var salesTrend = new List<ChartDataPoint>();
        var trendStart = todayStart.AddDays(-6);
        var trendFlows = await _db.DistributionRecords
            .Where(r => r.DistributionDate >= trendStart && r.DistributionDate < todayEnd)
            .ToListAsync();
        for (int i = 6; i >= 0; i--)
        {
            var day = todayStart.AddDays(-i);
            var nextDay = day.AddDays(1);
            var amount = trendFlows
                .Where(r => r.DistributionDate >= day
                    && r.DistributionDate < nextDay
                    && IsSalesManagementRecord(r, selfId))
                .Sum(r => r.Quantity * r.TaxedUnitPrice);
            salesTrend.Add(new ChartDataPoint { Label = day.ToString("MM/dd"), Value = amount });
        }

        return new DashboardStats
        {
            TodayPurchaseCount = todayPurchases.Count,
            TodaySalesCount = todaySales.Count,
            TotalDrugTypes = totalDrugTypes,
            LowStockCount = lowStockCount,
            ExpiringCount = expiringCount,
            TodayPurchaseAmount = todayPurchases.Sum(r => r.Quantity * r.TaxedUnitPrice),
            TodaySalesAmount = todaySales.Sum(r => r.Quantity * r.TaxedUnitPrice),
            SalesTrend = salesTrend,
        };
    }

    private async Task<int> BuildLowStockCountAsync()
    {
        var records = await _db.DistributionRecords
            .Include(r => r.Drug)
            .Include(r => r.ToCustomer)
            .Include(r => r.FromCustomer)
            .ToListAsync();
        var stockMap = new Dictionary<(Guid DrugId, string BatchNo, Guid CustomerId), (decimal InQ, decimal OutQ, int Warn)>();

        void Add(Guid drugId, string batchNo, Guid customerId, decimal inQ, decimal outQ, int warn)
        {
            var key = (drugId, batchNo, customerId);
            if (stockMap.TryGetValue(key, out var current))
                stockMap[key] = (current.InQ + inQ, current.OutQ + outQ, warn);
            else
                stockMap[key] = (inQ, outQ, warn);
        }

        foreach (var record in records)
        {
            if (record.ToCustomerId.HasValue && record.ToCustomer != null && record.ToCustomer.CustomerType == Domain.Enums.CustomerType.商业)
                Add(record.DrugId, record.BatchNo, record.ToCustomerId.Value, record.Quantity, 0, record.Drug.WarningLowQuantity);
            if (record.FromCustomerId.HasValue && record.FromCustomer != null && record.FromCustomer.CustomerType == Domain.Enums.CustomerType.商业)
                Add(record.DrugId, record.BatchNo, record.FromCustomerId.Value, 0, record.Quantity, record.Drug.WarningLowQuantity);
        }

        return stockMap.Values.Count(x =>
        {
            var balance = x.InQ - x.OutQ;
            var warningLine = x.Warn > 0 ? x.Warn : 10;
            return balance > 0 && balance <= warningLine;
        });
    }

    private static bool IsPurchaseManagementRecord(DistributionRecord record, Guid? selfId)
        => record.SupplierId.HasValue
            || (selfId.HasValue && (record.FromCustomerId == selfId.Value || record.ToCustomerId == selfId.Value));

    private static bool IsSalesManagementRecord(DistributionRecord record, Guid? selfId)
        => !record.SupplierId.HasValue
            && (!selfId.HasValue || (record.FromCustomerId != selfId.Value && record.ToCustomerId != selfId.Value));
}
