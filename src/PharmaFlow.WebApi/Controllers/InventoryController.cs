using Microsoft.AspNetCore.Mvc;
using PharmaFlow.Application.DTOs;
using PharmaFlow.Application.Services;
using PharmaFlow.WebApi.Models;

namespace PharmaFlow.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventoryService;

    public InventoryController(IInventoryService inventoryService) => _inventoryService = inventoryService;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<InventoryDto>>>> GetPaged(
        [FromQuery] int page = 0, [FromQuery] int size = 20,
        [FromQuery] string? search = null, [FromQuery] string? batchNo = null,
        [FromQuery] bool? lowStock = null)
    {
        var result = await _inventoryService.GetPagedAsync(page, size, search, batchNo, lowStock);
        return Ok(ApiResponse<PagedResult<InventoryDto>>.Ok(result));
    }

    [HttpGet("low-stock")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<InventoryDto>>>> GetLowStock()
    {
        var items = await _inventoryService.GetLowStockAsync();
        return Ok(ApiResponse<IReadOnlyList<InventoryDto>>.Ok(items));
    }

    [HttpGet("by-drug/{drugId:guid}")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<InventoryDto>>>> GetByDrug(Guid drugId)
    {
        var items = await _inventoryService.GetByDrugAsync(drugId);
        return Ok(ApiResponse<IReadOnlyList<InventoryDto>>.Ok(items));
    }
}
