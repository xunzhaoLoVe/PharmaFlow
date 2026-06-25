using Microsoft.AspNetCore.Mvc;
using PharmaFlow.Application.DTOs;
using PharmaFlow.Application.Services;
using PharmaFlow.WebApi.Models;

namespace PharmaFlow.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _customerService;

    public CustomersController(ICustomerService customerService) => _customerService = customerService;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<CustomerDto>>>> GetPaged(
        [FromQuery] int page = 0, [FromQuery] int size = 20,
        [FromQuery] string? search = null, [FromQuery] Guid? categoryId = null,
        [FromQuery] string? type = null)
    {
        var result = await _customerService.GetPagedAsync(page, size, search, categoryId, type);
        return Ok(ApiResponse<PagedResult<CustomerDto>>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<CustomerDto>>> GetById(Guid id)
    {
        var dto = await _customerService.GetByIdAsync(id);
        return dto == null
            ? NotFound(ApiResponse<CustomerDto>.Fail("客户不存在"))
            : Ok(ApiResponse<CustomerDto>.Ok(dto));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<CustomerDto>>> Create([FromBody] CustomerDto dto)
    {
        var result = await _customerService.CreateAsync(dto);
        return Ok(ApiResponse<CustomerDto>.Ok(result, "创建成功"));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Update(Guid id, [FromBody] CustomerDto dto)
    {
        dto.Id = id;
        await _customerService.UpdateAsync(dto);
        return Ok(ApiResponse.Ok("更新成功"));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id)
    {
        await _customerService.DeleteAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }

    [HttpGet("dropdown")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DropdownItem>>>> GetDropdown(
        [FromQuery] string? level = null)
    {
        IReadOnlyList<DropdownItem> items;
        if (!string.IsNullOrEmpty(level))
        {
            var distributorLevel = Enum.TryParse<Domain.Enums.DistributorLevel>(
                level, out var parsed) ? parsed : (Domain.Enums.DistributorLevel?)null;
            items = await _customerService.GetDistributorDropdownAsync(distributorLevel);
        }
        else
        {
            items = await _customerService.GetDropdownAsync();
        }
        return Ok(ApiResponse<IReadOnlyList<DropdownItem>>.Ok(items));
    }

    [HttpGet("end-dropdown")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DropdownItem>>>> GetEndCustomerDropdown()
    {
        var items = await _customerService.GetEndCustomerDropdownAsync();
        return Ok(ApiResponse<IReadOnlyList<DropdownItem>>.Ok(items));
    }

    [HttpGet("categories")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DropdownItem>>>> GetCategories()
    {
        var items = await _customerService.GetCategoriesAsync();
        return Ok(ApiResponse<IReadOnlyList<DropdownItem>>.Ok(items));
    }

    [HttpPost("categories")]
    public async Task<ActionResult<ApiResponse<DropdownItem>>> CreateCategory(
        [FromBody] CreateCategoryRequest request)
    {
        var item = await _customerService.CreateCategoryAsync(request.Name, request.ParentId);
        return Ok(ApiResponse<DropdownItem>.Ok(item, "分类创建成功"));
    }

    [HttpPost("import")]
    public async Task<ActionResult<ApiResponse<int>>> ImportBatch(
        [FromBody] List<CustomerDto> dtos, [FromQuery] bool isEndCustomer = false)
    {
        var result = await _customerService.ImportBatchAsync(dtos, isEndCustomer);
        return Ok(ApiResponse<int>.Ok(result.imported, $"成功 {result.imported} 条, 跳过 {result.skipped} 条"));
    }

    [HttpDelete("clear")]
    public async Task<ActionResult<ApiResponse>> ClearAll([FromQuery] string type = "商业")
    {
        var count = await _customerService.ClearAllAsync(type);
        return Ok(ApiResponse.Ok($"已清空 {count} 条记录"));
    }
}
