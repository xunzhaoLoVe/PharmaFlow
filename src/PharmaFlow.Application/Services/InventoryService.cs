using Microsoft.EntityFrameworkCore;
using PharmaFlow.Application.DTOs;
using PharmaFlow.Domain.Entities;
using PharmaFlow.Domain.Enums;
using PharmaFlow.Infrastructure.Data;

namespace PharmaFlow.Application.Services;

public class InventoryService : IInventoryService
{
    private readonly AppDbContext _db;
    public InventoryService(AppDbContext db) => _db = db;

    public async Task<PagedResult<InventoryDto>> GetPagedAsync(int pageIndex, int pageSize, string? search = null, string? batchNo = null, bool? lowStock = null)
    {
        var records = await _db.DistributionRecords
            .Include(r => r.Drug)
            .Include(r => r.ToCustomer)
            .Include(r => r.FromCustomer)
            .ToListAsync();

        var allCustomers = records.Select(r => r.ToCustomer).Concat(records.Select(r => r.FromCustomer))
            .Where(c => c != null).DistinctBy(c => c!.Id).ToDictionary(c => c!.Id, c => c!);

        var invLookup = new Dictionary<(Guid DrugId, string BatchNo, Guid CustomerId), (Drug Drug, string CustomerName, decimal InQty, decimal OutQty)>();

        void Add(Guid drugId, string bn, Guid custId, Drug drug, string custName, decimal inQ, decimal outQ)
        {
            var key = (drugId, bn, custId);
            if (invLookup.TryGetValue(key, out var e))
                invLookup[key] = (e.Drug, e.CustomerName, e.InQty + inQ, e.OutQty + outQ);
            else
                invLookup[key] = (drug, custName, inQ, outQ);
        }

        foreach (var r in records)
        {
            if (r.ToCustomerId.HasValue && r.ToCustomerId != Guid.Empty && r.ToCustomer != null)
                Add(r.DrugId, r.BatchNo, r.ToCustomerId.Value, r.Drug, r.ToCustomer.Name, r.Quantity, 0);
            if (r.FromCustomerId.HasValue && r.FromCustomerId != Guid.Empty && r.FromCustomer != null)
                Add(r.DrugId, r.BatchNo, r.FromCustomerId.Value, r.Drug, r.FromCustomer.Name, 0, r.Quantity);
        }

        var items = invLookup.Select(kvp =>
        {
            var (drug, custName, inQ, outQ) = kvp.Value;
            var stock = inQ - outQ;
            if (stock <= 0) return null;
            var custId = kvp.Key.CustomerId;
            if (!allCustomers.TryGetValue(custId, out var cust) || cust.CustomerType != CustomerType.商业)
                return null;

            var levelName = cust.Level switch
            {
                DistributorLevel.一级经销商 => "一级经销商",
                DistributorLevel.二级经销商 => "二级经销商",
                DistributorLevel.三级经销商 => "三级经销商",
                _ => ""
            };

            return new InventoryDto
            {
                DrugCode = drug.Code, DrugName = drug.Name,
                Specification = drug.Specification ?? "",
                BatchNo = kvp.Key.BatchNo, Quantity = stock,
                IsLowStock = stock <= (drug.WarningLowQuantity > 0 ? drug.WarningLowQuantity : 10),
                CustomerName = custName, CustomerLevel = levelName,
                Province = cust.Province ?? "",
            };
        }).Where(i => i != null).Cast<InventoryDto>().ToList();

        // 客户名称搜索
        if (!string.IsNullOrWhiteSpace(search))
            items = items.Where(i => (i.CustomerName ?? "").Contains(search) || (i.DrugName ?? "").Contains(search)).ToList();
        // 批号搜索
        if (!string.IsNullOrWhiteSpace(batchNo))
            items = items.Where(i => (i.BatchNo ?? "").Contains(batchNo)).ToList();
        // 低库存
        if (lowStock == true)
            items = items.Where(i => i.IsLowStock).ToList();

        var total = items.Count;
        var paged = items.OrderBy(i => i.DrugName).ThenBy(i => i.BatchNo).Skip(pageIndex * pageSize).Take(pageSize).ToList();

        return new PagedResult<InventoryDto> { Items = paged, TotalCount = total, PageIndex = pageIndex, PageSize = pageSize };
    }

    public async Task<IReadOnlyList<InventoryDto>> GetLowStockAsync()
    {
        var all = await GetPagedAsync(0, 99999, lowStock: true);
        return all.Items;
    }

    public async Task<IReadOnlyList<InventoryDto>> GetByDrugAsync(Guid drugId)
    {
        var records = await _db.DistributionRecords.Include(r => r.Drug).Include(r => r.ToCustomer).Include(r => r.FromCustomer)
            .Where(r => r.DrugId == drugId).ToListAsync();

        var invLookup = new Dictionary<(Guid DrugId, string BatchNo, Guid CustomerId), (Drug Drug, string CustomerName, decimal InQty, decimal OutQty)>();
        void Add(Guid did, string bn, Guid cid, Drug drug, string cn, decimal iq, decimal oq)
        {
            var k = (did, bn, cid);
            if (invLookup.TryGetValue(k, out var e)) invLookup[k] = (e.Drug, e.CustomerName, e.InQty + iq, e.OutQty + oq);
            else invLookup[k] = (drug, cn, iq, oq);
        }
        foreach (var r in records)
        {
            if (r.ToCustomerId.HasValue && r.ToCustomerId != Guid.Empty && r.ToCustomer != null)
                Add(r.DrugId, r.BatchNo, r.ToCustomerId.Value, r.Drug, r.ToCustomer.Name, r.Quantity, 0);
            if (r.FromCustomerId.HasValue && r.FromCustomerId != Guid.Empty && r.FromCustomer != null)
                Add(r.DrugId, r.BatchNo, r.FromCustomerId.Value, r.Drug, r.FromCustomer.Name, 0, r.Quantity);
        }
        return invLookup.Select(kvp =>
        {
            var (drug, cn, iq, oq) = kvp.Value; var s = iq - oq; if (s <= 0) return null;
            return new InventoryDto { DrugCode = drug.Code, DrugName = drug.Name, Specification = drug.Specification ?? "", BatchNo = kvp.Key.BatchNo, Quantity = s, CustomerName = cn };
        }).Where(i => i != null).Cast<InventoryDto>().OrderBy(i => i.BatchNo).ToList();
    }
}
