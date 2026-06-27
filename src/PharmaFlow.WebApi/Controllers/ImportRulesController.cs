using Microsoft.AspNetCore.Mvc;
using PharmaFlow.Application.DTOs;
using PharmaFlow.Application.Services;
using PharmaFlow.WebApi.Models;

namespace PharmaFlow.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ImportRulesController : ControllerBase
{
    private readonly IImportRuleService _ruleService;

    public ImportRulesController(IImportRuleService ruleService) => _ruleService = ruleService;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<ImportRuleDto>>>> GetAll(
        [FromQuery] int page = 0, [FromQuery] int size = 20)
    {
        var result = await _ruleService.GetPagedAsync(page, size);
        return Ok(ApiResponse<PagedResult<ImportRuleDto>>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ImportRuleDto>>> GetById(Guid id)
    {
        var result = await _ruleService.GetByIdAsync(id);
        if (result == null) return NotFound(ApiResponse<ImportRuleDto>.Fail("规则不存在"));
        return Ok(ApiResponse<ImportRuleDto>.Ok(result));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<ImportRuleDto>>> Create([FromBody] ImportRuleDto dto)
    {
        var result = await _ruleService.CreateAsync(dto);
        return Ok(ApiResponse<ImportRuleDto>.Ok(result, "创建成功"));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Update(Guid id, [FromBody] ImportRuleDto dto)
    {
        dto.Id = id;
        await _ruleService.UpdateAsync(dto);
        return Ok(ApiResponse.Ok("更新成功"));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id)
    {
        await _ruleService.DeleteAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }

    [HttpGet("dropdown")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DropdownItem>>>> GetDropdown()
    {
        var result = await _ruleService.GetDropdownAsync();
        return Ok(ApiResponse<IReadOnlyList<DropdownItem>>.Ok(result));
    }
}