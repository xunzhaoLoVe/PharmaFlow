using Microsoft.EntityFrameworkCore;
using PharmaFlow.Application.DTOs;
using PharmaFlow.Domain.Entities;
using PharmaFlow.Domain.Enums;
using PharmaFlow.Infrastructure.Data;

namespace PharmaFlow.Application.Services;

public class SalesService : ISalesService
{
    private readonly AppDbContext _db;
    public SalesService(AppDbContext db) => _db = db;

    public async Task<PagedResult<SalesOrderDto>> GetPagedAsync(int pageIndex, int pageSize, string? status = null)
    {
        var query = _db.SalesOrders.Include(s => s.Customer).Include(s => s.Items).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<OrderStatus>(status, out var s))
            query = query.Where(o => o.Status == s);

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(o => o.OrderDate).Skip(pageIndex * pageSize).Take(pageSize).ToListAsync();

        return new PagedResult<SalesOrderDto>
        {
            Items = items.Select(o => new SalesOrderDto
            {
                Id = o.Id, OrderNo = o.OrderNo, CustomerName = o.Customer.Name, CustomerId = o.CustomerId,
                OrderDate = o.OrderDate, TotalAmount = o.TotalAmount, Status = o.Status.ToString(),
                Operator = o.Operator, ItemCount = o.Items.Count,
            }).ToList(),
            TotalCount = total, PageIndex = pageIndex, PageSize = pageSize,
        };
    }

    public async Task<SalesOrderDto?> GetByIdAsync(Guid id)
    {
        var o = await _db.SalesOrders.Include(x => x.Customer).Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id);
        if (o == null) return null;
        return new SalesOrderDto
        {
            Id = o.Id, OrderNo = o.OrderNo, CustomerName = o.Customer.Name, CustomerId = o.CustomerId,
            OrderDate = o.OrderDate, TotalAmount = o.TotalAmount, Status = o.Status.ToString(), Operator = o.Operator,
            Remark = o.Remark,
        };
    }

    public async Task<SalesOrderDetailDto?> GetDetailAsync(Guid id)
    {
        var o = await _db.SalesOrders.Include(x => x.Customer).Include(x => x.Items).ThenInclude(i => i.Drug)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (o == null) return null;
        return new SalesOrderDetailDto
        {
            Id = o.Id, OrderNo = o.OrderNo, CustomerName = o.Customer.Name,
            OrderDate = o.OrderDate, TotalAmount = o.TotalAmount,
            Status = o.Status.ToString(), Operator = o.Operator, Remark = o.Remark,
            Items = o.Items.Select(i => new SalesOrderItemDetailDto
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

    public async Task<string> CreateOrderAsync(Guid customerId, string salesType, string? remark, List<SalesOrderItemDto> items, string operatorName)
    {
        var orderNo = "SO" + DateTime.Now.ToString("yyyyMMddHHmmss");
        var order = new SalesOrder
        {
            OrderNo = orderNo,
            CustomerId = customerId,
            OrderDate = DateTime.Now,
            SalesType = Enum.TryParse<SalesType>(salesType, out var st) ? st : SalesType.批发,
            TotalAmount = items.Sum(i => i.Quantity * i.UnitPrice),
            Status = OrderStatus.待审,
            Operator = operatorName,
            Remark = remark,
        };

        foreach (var item in items)
        {
            var totalPrice = item.Quantity * item.UnitPrice;
            order.Items.Add(new SalesOrderItem
            {
                DrugId = item.DrugId,
                BatchNo = item.BatchNo,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                TotalPrice = totalPrice,
                ProductionDate = item.ProductionDate,
                ExpiryDate = item.ExpiryDate,
            });
        }

        _db.SalesOrders.Add(order);
        await _db.SaveChangesAsync();
        return orderNo;
    }

    public async Task AuditOrderAsync(Guid id, string auditor)
    {
        var order = await _db.SalesOrders.FindAsync(id);
        if (order == null || order.Status != OrderStatus.待审) return;
        order.Status = OrderStatus.已审;
        order.Auditor = auditor;
        order.AuditTime = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task CompleteOrderAsync(Guid id)
    {
        var order = await _db.SalesOrders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
        if (order == null || order.Status != OrderStatus.已审) return;

        foreach (var item in order.Items)
        {
            var inv = await _db.Inventories.FirstOrDefaultAsync(i => i.DrugId == item.DrugId && i.BatchNo == item.BatchNo);
            if (inv == null || inv.Quantity < item.Quantity)
                throw new InvalidOperationException($"库存不足: {item.DrugId} 批次 {item.BatchNo}");

            var beforeQty = inv.Quantity;
            inv.Quantity -= item.Quantity;

            // Find source - trace back to the original supplier for this batch
            var sourceSupplier = _db.PurchaseOrderItems
                .Include(pi => pi.PurchaseOrder)
                .Where(pi => pi.DrugId == item.DrugId && pi.BatchNo == item.BatchNo)
                .Select(pi => pi.PurchaseOrder.SupplierId)
                .FirstOrDefault();

            // See if the supplier has a matching customer record (as our company representation)
            var sourceCustomerId = _db.Customers
                .Where(c => c.Code == "SELF")
                .Select(c => (Guid?)c.Id)
                .FirstOrDefault();

            // Create distribution record with source tracking
            _db.DistributionRecords.Add(new DistributionRecord
            {
                DrugId = item.DrugId,
                BatchNo = item.BatchNo,
                FromCustomerId = sourceCustomerId,
                ToCustomerId = order.CustomerId,
                SalesOrderId = order.Id,
                Quantity = item.Quantity,
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
        var order = await _db.SalesOrders.FindAsync(id);
        if (order == null || order.Status == OrderStatus.已完成) return;
        order.Status = OrderStatus.已取消;
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }
}
