using PharmaFlow.Application.DTOs;

namespace PharmaFlow.Application.Services;

public interface ISalesService
{
    Task<PagedResult<SalesOrderDto>> GetPagedAsync(int pageIndex, int pageSize, string? status = null);
    Task<SalesOrderDto?> GetByIdAsync(Guid id);
    Task<SalesOrderDetailDto?> GetDetailAsync(Guid id);
    Task<string> CreateOrderAsync(Guid customerId, string salesType, string? remark, List<SalesOrderItemDto> items, string operatorName);
    Task AuditOrderAsync(Guid id, string auditor);
    Task CompleteOrderAsync(Guid id); // 出库
    Task CancelOrderAsync(Guid id);
}
