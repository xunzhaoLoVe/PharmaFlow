using Microsoft.AspNetCore.Mvc;
using PharmaFlow.Application.DTOs;
using PharmaFlow.Application.Services;
using PharmaFlow.WebApi.Models;

namespace PharmaFlow.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SalesController : ControllerBase
{
    private readonly ISalesService _salesService;

    public SalesController(ISalesService salesService) => _salesService = salesService;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<SalesOrderDto>>>> GetPaged(
        [FromQuery] int page = 0, [FromQuery] int size = 20,
        [FromQuery] string? status = null)
    {
        var result = await _salesService.GetPagedAsync(page, size, status);
        return Ok(ApiResponse<PagedResult<SalesOrderDto>>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<SalesOrderDto>>> GetById(Guid id)
    {
        var dto = await _salesService.GetByIdAsync(id);
        return dto == null
            ? NotFound(ApiResponse<SalesOrderDto>.Fail("销售单不存在"))
            : Ok(ApiResponse<SalesOrderDto>.Ok(dto));
    }

    [HttpGet("{id:guid}/detail")]
    public async Task<ActionResult<ApiResponse<SalesOrderDetailDto>>> GetDetail(Guid id)
    {
        var dto = await _salesService.GetDetailAsync(id);
        return dto == null
            ? NotFound(ApiResponse<SalesOrderDetailDto>.Fail("销售单不存在"))
            : Ok(ApiResponse<SalesOrderDetailDto>.Ok(dto));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<string>>> Create(
        [FromBody] CreateSalesRequest request)
    {
        var orderNo = await _salesService.CreateOrderAsync(
            request.CustomerId, request.SalesType, request.Remark,
            request.Items, request.Operator ?? "系统");
        return Ok(ApiResponse<string>.Ok(orderNo, "销售单创建成功"));
    }

    [HttpPut("{id:guid}/audit")]
    public async Task<ActionResult<ApiResponse>> Audit(Guid id,
        [FromQuery] string auditor = "系统")
    {
        await _salesService.AuditOrderAsync(id, auditor);
        return Ok(ApiResponse.Ok("审核成功"));
    }

    [HttpPut("{id:guid}/complete")]
    public async Task<ActionResult<ApiResponse>> Complete(Guid id)
    {
        await _salesService.CompleteOrderAsync(id);
        return Ok(ApiResponse.Ok("完成出库"));
    }

    [HttpPut("{id:guid}/cancel")]
    public async Task<ActionResult<ApiResponse>> Cancel(Guid id)
    {
        await _salesService.CancelOrderAsync(id);
        return Ok(ApiResponse.Ok("已取消"));
    }
}

public record CreateSalesRequest(
    Guid CustomerId, string SalesType, string? Remark,
    List<SalesOrderItemDto> Items, string? Operator);
