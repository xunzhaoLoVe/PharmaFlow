using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PharmaFlow.Application.DTOs;
using PharmaFlow.Application.Services;
using PharmaFlow.Domain.Enums;
using PharmaFlow.Infrastructure.Data;
using PharmaFlow.WebApi.Models;

namespace PharmaFlow.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;
    private readonly AppDbContext _db;

    public DashboardController(IDashboardService dashboardService, AppDbContext db)
    {
        _dashboardService = dashboardService;
        _db = db;
    }

    [HttpGet("stats")]
    public async Task<ActionResult<ApiResponse<DashboardStats>>> GetStats()
    {
        var stats = await _dashboardService.GetStatsAsync();
        return Ok(ApiResponse<DashboardStats>.Ok(stats));
    }

    [HttpGet("summary")]
    public async Task<ActionResult<ApiResponse<object>>> GetSummary([FromQuery] string? drugName = null)
    {
        var custQuery = _db.Customers.Where(c => !c.IsDeleted);
        if (!string.IsNullOrWhiteSpace(drugName))
            custQuery = custQuery.Where(c => c.DrugName != null && c.DrugName.Contains(drugName));

        var distributorCount = await custQuery.Where(c => c.CustomerType == CustomerType.商业 && c.Code != "SELF").Select(c => c.Name).Distinct().CountAsync();
        var endCustomerCount = await custQuery.Where(c => c.CustomerType != CustomerType.商业 && c.Code != "SELF" && (int)c.CustomerType != 11).Select(c => c.Name).Distinct().CountAsync();
        var drugCount = await _db.Drugs.Where(d => !d.IsDeleted).Select(d => d.Name).Distinct().CountAsync();
        // 低库存：经销商库存 <= 预警线（默认10）
        var records = await _db.DistributionRecords.Include(r => r.Drug).Include(r => r.ToCustomer).Include(r => r.FromCustomer).ToListAsync();
        var stockMap = new Dictionary<(Guid, string, Guid), (decimal InQ, decimal OutQ, int Warn, bool IsBiz)>();
        foreach (var r in records)
        {
            if (r.ToCustomerId.HasValue && r.ToCustomer != null && r.ToCustomer.CustomerType == CustomerType.商业)
                Add(r.DrugId, r.BatchNo, r.ToCustomerId.Value, r.Quantity, 0, r.Drug.WarningLowQuantity, true);
            if (r.FromCustomerId.HasValue && r.FromCustomer != null && r.FromCustomer.CustomerType == CustomerType.商业)
                Add(r.DrugId, r.BatchNo, r.FromCustomerId.Value, 0, r.Quantity, r.Drug.WarningLowQuantity, true);
        }
        void Add(Guid did, string bn, Guid cid, decimal iQ, decimal oQ, int warn, bool biz)
        {
            var k = (did, bn, cid);
            if (stockMap.TryGetValue(k, out var e)) stockMap[k] = (e.InQ + iQ, e.OutQ + oQ, warn, true);
            else stockMap[k] = (iQ, oQ, warn, true);
        }
        var lowStockCount = stockMap.Values.Count(x => x.InQ - x.OutQ > 0 && x.InQ - x.OutQ <= (x.Warn > 0 ? x.Warn : 10));
        // 按名称去重后再分组（与卡片统计保持一致）
        var distNames = await custQuery.Where(c => c.CustomerType == CustomerType.商业 && c.Code != "SELF").Select(c => new { c.Name, c.Level }).ToListAsync();
        var distByName = distNames.GroupBy(c => c.Name).Select(g => g.First()).ToList();
        var distByLevelResult = distByName.GroupBy(c => c.Level switch { DistributorLevel.一级经销商 => "一级经销商", DistributorLevel.二级经销商 => "二级经销商", DistributorLevel.三级经销商 => "三级经销商", _ => "未分级" }).Select(g => new { name = g.Key, count = g.Count(), samples = g.Key == "未分级" ? g.Select(c => c.Name).Take(5).ToList() : null }).ToList();

        // 按名称去重
        var endNames = await custQuery.Where(c => c.CustomerType != CustomerType.商业 && (int)c.CustomerType != 11 && c.Code != "SELF").Select(c => new { c.Name, c.Province, c.CustomerType }).ToListAsync();
        var endByName = endNames.GroupBy(c => c.Name).Select(g => g.First()).ToList();
        var endByProv = endByName.GroupBy(c => string.IsNullOrEmpty(c.Province) ? "未知" : c.Province).Select(g => new { name = g.Key, count = g.Count() }).OrderByDescending(g => g.count).ToList();
        var endByType = endByName.GroupBy(c => c.CustomerType.ToString()).Select(g => new { name = g.Key, count = g.Count() }).ToList();

        return Ok(ApiResponse<object>.Ok(new { distributorCount, endCustomerCount, drugCount, lowStockCount, distByLevel = distByLevelResult, endByProv, endByType }));
    }
}
