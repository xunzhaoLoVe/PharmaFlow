using Microsoft.AspNetCore.Mvc;
using PharmaFlow.Application.DTOs;
using PharmaFlow.Application.Services;
using PharmaFlow.WebApi.Models;

namespace PharmaFlow.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PurchaseController : ControllerBase
{
    private readonly IPurchaseService _purchaseService;

    public PurchaseController(IPurchaseService purchaseService) => _purchaseService = purchaseService;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<PurchaseOrderDto>>>> GetPaged(
        [FromQuery] int page = 0, [FromQuery] int size = 20,
        [FromQuery] string? status = null)
    {
        var result = await _purchaseService.GetPagedAsync(page, size, status);
        return Ok(ApiResponse<PagedResult<PurchaseOrderDto>>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<PurchaseOrderDto>>> GetById(Guid id)
    {
        var dto = await _purchaseService.GetByIdAsync(id);
        return dto == null
            ? NotFound(ApiResponse<PurchaseOrderDto>.Fail("采购单不存在"))
            : Ok(ApiResponse<PurchaseOrderDto>.Ok(dto));
    }

    [HttpGet("{id:guid}/detail")]
    public async Task<ActionResult<ApiResponse<PurchaseOrderDetailDto>>> GetDetail(Guid id)
    {
        var dto = await _purchaseService.GetDetailAsync(id);
        return dto == null
            ? NotFound(ApiResponse<PurchaseOrderDetailDto>.Fail("采购单不存在"))
            : Ok(ApiResponse<PurchaseOrderDetailDto>.Ok(dto));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<string>>> Create(
        [FromBody] CreatePurchaseRequest request)
    {
        var orderNo = await _purchaseService.CreateOrderAsync(
            request.SupplierId, request.Remark, request.Items, request.Operator ?? "系统");
        return Ok(ApiResponse<string>.Ok(orderNo, "采购单创建成功"));
    }

    [HttpPut("{id:guid}/audit")]
    public async Task<ActionResult<ApiResponse>> Audit(Guid id,
        [FromQuery] string auditor = "系统")
    {
        await _purchaseService.AuditOrderAsync(id, auditor);
        return Ok(ApiResponse.Ok("审核成功"));
    }

    [HttpPut("{id:guid}/complete")]
    public async Task<ActionResult<ApiResponse>> Complete(Guid id)
    {
        await _purchaseService.CompleteOrderAsync(id);
        return Ok(ApiResponse.Ok("完成入库"));
    }

    [HttpPut("{id:guid}/cancel")]
    public async Task<ActionResult<ApiResponse>> Cancel(Guid id)
    {
        await _purchaseService.CancelOrderAsync(id);
        return Ok(ApiResponse.Ok("已取消"));
    }
}

public record CreatePurchaseRequest(
    Guid SupplierId, string? Remark,
    List<PurchaseOrderItemDto> Items, string? Operator);
