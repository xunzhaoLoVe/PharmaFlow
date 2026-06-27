using PharmaFlow.Application.DTOs;

namespace PharmaFlow.Application.Services;

public interface IImportRuleService
{
    Task<PagedResult<ImportRuleDto>> GetPagedAsync(int pageIndex, int pageSize);
    Task<ImportRuleDto?> GetByIdAsync(Guid id);
    Task<ImportRuleDto> CreateAsync(ImportRuleDto dto);
    Task UpdateAsync(ImportRuleDto dto);
    Task DeleteAsync(Guid id);
    Task<IReadOnlyList<DropdownItem>> GetDropdownAsync();
}