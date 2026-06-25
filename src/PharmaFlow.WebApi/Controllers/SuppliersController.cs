using Microsoft.AspNetCore.Mvc;
using PharmaFlow.Application.DTOs;
using PharmaFlow.Application.Services;
using PharmaFlow.WebApi.Models;

namespace PharmaFlow.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SuppliersController : ControllerBase
{
    private readonly ISupplierService _supplierService;

    public SuppliersController(ISupplierService supplierService) => _supplierService = supplierService;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<SupplierDto>>>> GetPaged(
        [FromQuery] int page = 0, [FromQuery] int size = 20,
        [FromQuery] string? search = null)
    {
        var result = await _supplierService.GetPagedAsync(page, size, search);
        return Ok(ApiResponse<PagedResult<SupplierDto>>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<SupplierDto>>> GetById(Guid id)
    {
        var dto = await _supplierService.GetByIdAsync(id);
        return dto == null
            ? NotFound(ApiResponse<SupplierDto>.Fail("供应商不存在"))
            : Ok(ApiResponse<SupplierDto>.Ok(dto));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<SupplierDto>>> Create([FromBody] SupplierDto dto)
    {
        var result = await _supplierService.CreateAsync(dto);
        return Ok(ApiResponse<SupplierDto>.Ok(result, "创建成功"));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Update(Guid id, [FromBody] SupplierDto dto)
    {
        dto.Id = id;
        await _supplierService.UpdateAsync(dto);
        return Ok(ApiResponse.Ok("更新成功"));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id)
    {
        await _supplierService.DeleteAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }

    [HttpGet("dropdown")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DropdownItem>>>> GetDropdown()
    {
        var items = await _supplierService.GetDropdownAsync();
        return Ok(ApiResponse<IReadOnlyList<DropdownItem>>.Ok(items));
    }
}
