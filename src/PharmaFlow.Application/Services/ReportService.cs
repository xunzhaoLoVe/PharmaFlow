using Microsoft.EntityFrameworkCore;
using PharmaFlow.Application.DTOs;
using PharmaFlow.Domain.Enums;
using PharmaFlow.Infrastructure.Data;

namespace PharmaFlow.Application.Services;

public class ReportService : IReportService
{
    private readonly AppDbContext _db;
    public ReportService(AppDbContext db) => _db = db;

    public async Task<List<EndCustomerSaleDto>> GetEndCustomerSalesAsync(
        string? drugName = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        // 终端客户 = 非经销商 (CustomerType != 商业 或 Level 为 null)
        var query = _db.DistributionRecords
            .Include(r => r.ToCustomer)
            .Include(r => r.Drug)
            .Where(r => r.ToCustomer != null
                && !r.ToCustomer.IsDeleted
                && !(r.ToCustomer.CustomerType == CustomerType.商业 && r.ToCustomer.Level != null)
                && r.Quantity > 0);

        if (!string.IsNullOrWhiteSpace(drugName))
            query = query.Where(r => r.Drug!.Name.Contains(drugName));

        if (startDate.HasValue)
            query = query.Where(r => r.DistributionDate >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(r => r.DistributionDate < endDate.Value.AddDays(1));

        var data = await query
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

        return data;
    }
}
