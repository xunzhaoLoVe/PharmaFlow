using PharmaFlow.Application.DTOs;

namespace PharmaFlow.Application.Services;

public interface ISupplierService
{
    Task<PagedResult<SupplierDto>> GetPagedAsync(int pageIndex, int pageSize, string? search = null);
    Task<SupplierDto?> GetByIdAsync(Guid id);
    Task<SupplierDto> CreateAsync(SupplierDto dto);
    Task UpdateAsync(SupplierDto dto);
    Task DeleteAsync(Guid id);
    Task<IReadOnlyList<DropdownItem>> GetDropdownAsync();
}
