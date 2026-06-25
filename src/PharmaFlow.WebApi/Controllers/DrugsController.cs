using Microsoft.AspNetCore.Mvc;
using PharmaFlow.Application.DTOs;
using PharmaFlow.Application.Services;
using PharmaFlow.WebApi.Models;

namespace PharmaFlow.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DrugsController : ControllerBase
{
    private readonly IDrugService _drugService;

    public DrugsController(IDrugService drugService) => _drugService = drugService;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<DrugDto>>>> GetPaged(
        [FromQuery] int page = 0, [FromQuery] int size = 20,
        [FromQuery] string? search = null, [FromQuery] Guid? categoryId = null)
    {
        var result = await _drugService.GetPagedAsync(page, size, search, categoryId);
        return Ok(ApiResponse<PagedResult<DrugDto>>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<DrugDto>>> GetById(Guid id)
    {
        var dto = await _drugService.GetByIdAsync(id);
        return dto == null
            ? NotFound(ApiResponse<DrugDto>.Fail("药品不存在"))
            : Ok(ApiResponse<DrugDto>.Ok(dto));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<DrugDto>>> Create([FromBody] DrugDto drug)
    {
        var dto = await _drugService.CreateAsync(drug);
        return Ok(ApiResponse<DrugDto>.Ok(dto, "创建成功"));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Update(Guid id, [FromBody] DrugDto drug)
    {
        drug.Id = id;
        await _drugService.UpdateAsync(drug);
        return Ok(ApiResponse.Ok("更新成功"));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id)
    {
        await _drugService.DeleteAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }

    [HttpGet("dropdown")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DropdownItem>>>> GetDropdown()
    {
        var items = await _drugService.GetDropdownAsync();
        return Ok(ApiResponse<IReadOnlyList<DropdownItem>>.Ok(items));
    }

    [HttpGet("categories")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DropdownItem>>>> GetCategories()
    {
        var items = await _drugService.GetCategoriesAsync();
        return Ok(ApiResponse<IReadOnlyList<DropdownItem>>.Ok(items));
    }

    [HttpPost("categories")]
    public async Task<ActionResult<ApiResponse<DropdownItem>>> CreateCategory(
        [FromBody] CreateCategoryRequest request)
    {
        var item = await _drugService.CreateCategoryAsync(request.Name, request.ParentId);
        return Ok(ApiResponse<DropdownItem>.Ok(item, "分类创建成功"));
    }
}

public record CreateCategoryRequest(string Name, Guid? ParentId);
