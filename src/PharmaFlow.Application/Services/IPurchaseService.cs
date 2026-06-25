using PharmaFlow.Application.DTOs;

namespace PharmaFlow.Application.Services;

public interface IPurchaseService
{
    Task<PagedResult<PurchaseOrderDto>> GetPagedAsync(int pageIndex, int pageSize, string? status = null);
    Task<PurchaseOrderDto?> GetByIdAsync(Guid id);
    Task<PurchaseOrderDetailDto?> GetDetailAsync(Guid id);
    Task<string> CreateOrderAsync(Guid supplierId, string? remark, List<PurchaseOrderItemDto> items, string operatorName);
    Task AuditOrderAsync(Guid id, string auditor);
    Task CompleteOrderAsync(Guid id); // 入库
    Task CancelOrderAsync(Guid id);
}
