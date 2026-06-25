using Microsoft.EntityFrameworkCore;
using PharmaFlow.Application.DTOs;
using PharmaFlow.Domain.Entities;
using PharmaFlow.Domain.Enums;
using PharmaFlow.Infrastructure.Data;

namespace PharmaFlow.Application.Services;

public class PurchaseService : IPurchaseService
{
    private readonly AppDbContext _db;
    public PurchaseService(AppDbContext db) => _db = db;

    public async Task<PagedResult<PurchaseOrderDto>> GetPagedAsync(int pageIndex, int pageSize, string? status = null)
    {
        var query = _db.PurchaseOrders.Include(p => p.Supplier).Include(p => p.Items).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<OrderStatus>(status, out var s))
            query = query.Where(p => p.Status == s);

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(p => p.OrderDate).Skip(pageIndex * pageSize).Take(pageSize).ToListAsync();

        return new PagedResult<PurchaseOrderDto>
        {
            Items = items.Select(p => new PurchaseOrderDto
            {
                Id = p.Id, OrderNo = p.OrderNo, SupplierName = p.Supplier.Name, SupplierId = p.SupplierId,
                OrderDate = p.OrderDate, TotalAmount = p.TotalAmount, Status = p.Status.ToString(),
                Operator = p.Operator, ItemCount = p.Items.Count,
            }).ToList(),
            TotalCount = total, PageIndex = pageIndex, PageSize = pageSize,
        };
    }

    public async Task<PurchaseOrderDto?> GetByIdAsync(Guid id)
    {
        var p = await _db.PurchaseOrders.Include(x => x.Supplier).Include(x => x.Items).ThenInclude(i => i.Drug)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return null;
        return new PurchaseOrderDto
        {
            Id = p.Id, OrderNo = p.OrderNo, SupplierName = p.Supplier.Name, SupplierId = p.SupplierId,
            OrderDate = p.OrderDate, TotalAmount = p.TotalAmount, Status = p.Status.ToString(), Operator = p.Operator,
            ItemCount = p.Items.Count, Remark = p.Remark,
        };
    }

    public async Task<PurchaseOrderDetailDto?> GetDetailAsync(Guid id)
    {
        var p = await _db.PurchaseOrders.Include(x => x.Supplier).Include(x => x.Items).ThenInclude(i => i.Drug)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return null;
        return new PurchaseOrderDetailDto
        {
            Id = p.Id, OrderNo = p.OrderNo, SupplierName = p.Supplier.Name,
            OrderDate = p.OrderDate, TotalAmount = p.TotalAmount,
            Status = p.Status.ToString(), Operator = p.Operator, Remark = p.Remark,
            Items = p.Items.Select(i => new PurchaseOrderItemDetailDto
            {
                DrugName = i.Drug.Name,
                BatchNo = i.BatchNo,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                TotalPrice = i.TotalPrice,
                ProductionDate = i.ProductionDate,
                ExpiryDate = i.ExpiryDate,
            }).ToList(),
        };
    }

    public async Task<string> CreateOrderAsync(Guid supplierId, string? remark, List<PurchaseOrderItemDto> items, string operatorName)
    {
        var orderNo = "PO" + DateTime.Now.ToString("yyyyMMddHHmmss");
        var order = new PurchaseOrder
        {
            OrderNo = orderNo,
            SupplierId = supplierId,
            OrderDate = DateTime.Now,
            TotalAmount = items.Sum(i => i.Quantity * i.TaxedUnitPrice),
            Status = OrderStatus.待审,
            Operator = operatorName,
            Remark = remark,
        };

        foreach (var item in items)
        {
            var totalPrice = item.Quantity * item.TaxedUnitPrice;
            order.Items.Add(new PurchaseOrderItem
            {
                DrugId = item.DrugId,
                BatchNo = item.BatchNo,
                Quantity = item.Quantity,
                UnitPrice = item.TaxedUnitPrice,
                TotalPrice = totalPrice,
                ProductionDate = item.ProductionDate,
                ExpiryDate = item.ExpiryDate,
            });
        }

        _db.PurchaseOrders.Add(order);
        await _db.SaveChangesAsync();
        return orderNo;
    }

    public async Task AuditOrderAsync(Guid id, string auditor)
    {
        var order = await _db.PurchaseOrders.FindAsync(id);
        if (order == null || order.Status != OrderStatus.待审) return;
        order.Status = OrderStatus.已审;
        order.Auditor = auditor;
        order.AuditTime = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task CompleteOrderAsync(Guid id)
    {
        var order = await _db.PurchaseOrders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
        if (order == null || order.Status != OrderStatus.已审) return;

        // 获取本公司(SELF)用于入库和流向记录
        var self = await _db.Customers.FirstOrDefaultAsync(c => c.Code == "SELF");
        if (self == null) return;

        foreach (var item in order.Items)
        {
            // 更新本公司库存
            var inv = await _db.Inventories
                .FirstOrDefaultAsync(i => i.DrugId == item.DrugId && i.BatchNo == item.BatchNo && i.CustomerId == self.Id);
            var beforeQty = inv?.Quantity ?? 0;
            if (inv != null)
            {
                inv.Quantity += item.Quantity;
            }
            else
            {
                _db.Inventories.Add(new Inventory
                {
                    DrugId = item.DrugId,
                    BatchNo = item.BatchNo,
                    Quantity = item.Quantity,
                    ProductionDate = item.ProductionDate,
                    ExpiryDate = item.ExpiryDate,
                    CustomerId = self?.Id,
                });
            }

            // 创建采购来源流向记录：供应商 → 本公司
            // 使采购数据在流向查询中能显示来源
            _db.DistributionRecords.Add(new DistributionRecord
            {
                DrugId = item.DrugId,
                BatchNo = item.BatchNo,
                SupplierId = order.SupplierId,     // 来源：供应商
                ToCustomerId = self?.Id,            // 去向：本公司
                Quantity = item.Quantity,
                TaxedUnitPrice = item.UnitPrice,
                DistributionDate = DateTime.Now,
                Operator = order.Operator,
            });
        }

        order.Status = OrderStatus.已完成;
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task CancelOrderAsync(Guid id)
    {
        var order = await _db.PurchaseOrders.FindAsync(id);
        if (order == null || order.Status == OrderStatus.已完成) return;
        order.Status = OrderStatus.已取消;
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }
}
