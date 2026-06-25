using PharmaFlow.Application.DTOs;

namespace PharmaFlow.Application.Services;

public interface IDrugService
{
    Task<PagedResult<DrugDto>> GetPagedAsync(int pageIndex, int pageSize, string? search = null, Guid? categoryId = null);
    Task<DrugDto?> GetByIdAsync(Guid id);
    Task<DrugDto> CreateAsync(DrugDto drug);
    Task UpdateAsync(DrugDto drug);
    Task DeleteAsync(Guid id);
    Task<IReadOnlyList<DropdownItem>> GetDropdownAsync();
    Task<IReadOnlyList<DropdownItem>> GetCategoriesAsync();
    Task<DropdownItem> CreateCategoryAsync(string name, Guid? parentId);
    Task<bool> ExistsByNameAndSpec(string name, string? spec);
    Task<string> GetCompanyNameAsync();
}
