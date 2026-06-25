using Microsoft.EntityFrameworkCore;
using PharmaFlow.Application.DTOs;
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

        var todayPurchases = await _db.PurchaseOrders
            .Where(p => p.OrderDate >= todayStart && p.OrderDate < todayEnd && p.Status == Domain.Enums.OrderStatus.已完成)
            .ToListAsync();
        var todaySales = await _db.SalesOrders
            .Where(s => s.OrderDate >= todayStart && s.OrderDate < todayEnd && s.Status == Domain.Enums.OrderStatus.已完成)
            .ToListAsync();

        var totalDrugTypes = await _db.Drugs.CountAsync();
        var lowStockCount = await _db.Inventories.Include(i => i.Drug)
            .CountAsync(i => i.Quantity <= i.Drug.WarningLowQuantity && i.Quantity > 0);
        var expiringCount = await _db.Inventories
            .CountAsync(i => i.ExpiryDate.HasValue && i.ExpiryDate <= DateTime.UtcNow.AddDays(180) && i.Quantity > 0);

        // Sales trend for last 7 days
        var salesTrend = new List<ChartDataPoint>();
        for (int i = 6; i >= 0; i--)
        {
            var day = todayStart.AddDays(-i);
            var nextDay = day.AddDays(1);
            var amount = (decimal)await _db.SalesOrders
                .Where(s => s.OrderDate >= day && s.OrderDate < nextDay && s.Status == Domain.Enums.OrderStatus.已完成)
                .SumAsync(s => (double)s.TotalAmount);
            salesTrend.Add(new ChartDataPoint { Label = day.ToString("MM/dd"), Value = amount });
        }

        return new DashboardStats
        {
            TodayPurchaseCount = todayPurchases.Count,
            TodaySalesCount = todaySales.Count,
            TotalDrugTypes = totalDrugTypes,
            LowStockCount = lowStockCount,
            ExpiringCount = expiringCount,
            TodayPurchaseAmount = todayPurchases.Sum(p => p.TotalAmount),
            TodaySalesAmount = todaySales.Sum(s => s.TotalAmount),
            SalesTrend = salesTrend,
        };
    }
}
