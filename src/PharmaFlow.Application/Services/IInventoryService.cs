using PharmaFlow.Application.DTOs;

namespace PharmaFlow.Application.Services;

public interface IInventoryService
{
    Task<PagedResult<InventoryDto>> GetPagedAsync(int pageIndex, int pageSize, string? search = null, string? batchNo = null, bool? lowStock = null);
    Task<IReadOnlyList<InventoryDto>> GetLowStockAsync();
    Task<IReadOnlyList<InventoryDto>> GetByDrugAsync(Guid drugId);
}
