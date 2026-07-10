using PharmaFlow.Application.DTOs;
using PharmaFlow.Domain.Enums;

namespace PharmaFlow.Application.Services;

public interface ICustomerService
{
    Task<PagedResult<CustomerDto>> GetPagedAsync(int pageIndex, int pageSize, string? search = null, Guid? categoryId = null, string? customerType = null);
    Task<CustomerDto?> GetByIdAsync(Guid id);
    Task<CustomerDto> CreateAsync(CustomerDto dto);
    Task UpdateAsync(CustomerDto dto);
    Task DeleteAsync(Guid id);
    /// <summary>清空指定类型的客户数据（软删除），返回删除条数</summary>
    Task<int> ClearAllAsync(string customerType);
    Task<IReadOnlyList<DropdownItem>> GetDropdownAsync();
    Task<IReadOnlyList<DropdownItem>> GetCategoriesAsync();
    Task<DropdownItem> CreateCategoryAsync(string name, Guid? parentId);
    /// <summary>获取经销商下拉列表，可按级别筛选</summary>
    Task<IReadOnlyList<DropdownItem>> GetDistributorDropdownAsync(DistributorLevel? level = null);
    /// <summary>获取终端机构下拉列表（医院、基层医疗机构、诊所、药店、其他）</summary>
    Task<IReadOnlyList<DropdownItem>> GetEndCustomerDropdownAsync();
    /// <summary>按名称查找客户</summary>
    Task<CustomerDto?> FindByNameAsync(string name);
    /// <summary>补充更新：只填充目标中为空的字段</summary>
    Task SupplementAsync(Guid id, CustomerDto source);
    /// <summary>批量导入客户，一次性提交。返回 (新增, 补充, 跳过) 条数</summary>
    /// <param name="isEndCustomer">true=终端机构去重键(名称+药品+省份+类型), false=经销商去重键(名称+药品+省份+机构角色+级别)</param>
    Task<(int imported, int supplemented, int skipped)> ImportBatchAsync(List<CustomerDto> dtos, bool isEndCustomer = false);
}
