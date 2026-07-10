using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PharmaFlow.Application.DTOs;
using PharmaFlow.Application.Services;
using PharmaFlow.Infrastructure.Data;
using PharmaFlow.WebApi.Extensions;
using PharmaFlow.WebApi.Models;

namespace PharmaFlow.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DistributionController : ControllerBase
{
    private readonly IDistributionService _distributionService;
    private readonly ISupplierService _supplierService;
    private Guid? _defaultSupplierId;

    public DistributionController(IDistributionService distributionService, ISupplierService supplierService)
    {
        _distributionService = distributionService;
        _supplierService = supplierService;
    }

    private async Task<Guid> GetDefaultSupplierIdAsync()
    {
        if (_defaultSupplierId.HasValue) return _defaultSupplierId.Value;
        var suppliers = await _supplierService.GetDropdownAsync();
        var shenlong = suppliers.FirstOrDefault(s => s.Name.Contains("江苏神龙"));
        if (shenlong != null) { _defaultSupplierId = shenlong.Id; return shenlong.Id; }
        var created = await _supplierService.CreateAsync(new SupplierDto { Name = "江苏神龙药业有限公司" });
        _defaultSupplierId = created.Id; return created.Id;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<DistributionRecordDto>>>> GetPaged(
        [FromQuery] int page = 0, [FromQuery] int size = 20,
        [FromQuery] string? drugName = null, [FromQuery] string? batchNo = null,
        [FromQuery] Guid? customerId = null, [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null, [FromQuery] bool? fromSelf = null)
    {
        var result = await _distributionService.GetPagedAsync(
            page, size, drugName, batchNo, customerId, from, to, fromSelf);

        foreach (var item in result.Items)
        {
            if (item.FromCustomerName == "本公司(制药厂)")
            {
                item.FromCustomerName = "江苏神龙药业有限公司";
                item.FromCustomerCode = "SHENLONG";
                item.Province = "江苏省";
            }
        }
        return Ok(ApiResponse<PagedResult<DistributionRecordDto>>.Ok(result));
    }

    [HttpGet("trace")]
    public async Task<ActionResult<ApiResponse<FlowGraphDto>>> GetTrace(
        [FromQuery] string batchNo, [FromQuery] Guid drugId)
    {
        var graph = await _distributionService.GetTraceAsync(batchNo, drugId);
        return graph == null
            ? NotFound(ApiResponse<FlowGraphDto>.Fail("未找到流向数据"))
            : Ok(ApiResponse<FlowGraphDto>.Ok(graph));
    }

    [HttpPost("downstream")]
    public async Task<ActionResult<ApiResponse>> RecordDownstream(
        [FromBody] DownstreamRecordDto dto)
    {
        // 采购出货（本司→经销商）：自动分配供应商
        if (dto.FromCustomerId == Guid.Empty && string.IsNullOrWhiteSpace(dto.FromCustomerName) && dto.SupplierId == null)
            dto.SupplierId = await GetDefaultSupplierIdAsync();
        var operatorName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "系统";
        var result = await _distributionService.RecordDownstreamAsync(dto, operatorName);
        return Ok(ApiResponse.Ok(result.Imported > 0 ? "流转记录成功" : "记录已存在，未重复新增"));
    }

    [HttpPost("downstream/batch")]
    public async Task<ActionResult<ApiResponse<DistributionImportResultDto>>> RecordDownstreamBatch(
        [FromBody] List<DownstreamRecordDto> dtos)
    {
        var supplierId = await GetDefaultSupplierIdAsync();
        foreach (var dto in dtos)
        {
            if (dto.FromCustomerId == Guid.Empty && string.IsNullOrWhiteSpace(dto.FromCustomerName) && dto.SupplierId == null)
                dto.SupplierId = supplierId;
        }
        var operatorName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "系统";
        var result = await _distributionService.RecordDownstreamBatchAsync(dtos, operatorName);
        return Ok(ApiResponse<DistributionImportResultDto>.Ok(result, $"新增 {result.Imported} 条，跳过重复 {result.Skipped} 条"));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Update(Guid id,
        [FromQuery] decimal quantity)
    {
        await _distributionService.UpdateDistributionRecordAsync(id, quantity);
        return Ok(ApiResponse.Ok("更新成功"));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id)
    {
        await _distributionService.DeleteDistributionRecordAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }

    [HttpDelete("clear")]
    public async Task<ActionResult<ApiResponse>> ClearAll(
        [FromQuery] bool? fromSelf = null)
    {
        if (fromSelf == true)
            await _distributionService.ClearBySourceAsync(isPurchase: true);
        else if (fromSelf == false)
            await _distributionService.ClearBySourceAsync(isPurchase: false);
        else
            await _distributionService.ClearAllAsync();
        return Ok(ApiResponse.Ok("已清空"));
    }

    [HttpDelete("clear-sales")]
    public async Task<ActionResult<ApiResponse>> ClearSalesByOrigin(
        [FromQuery] string origin)
    {
        if (origin != "manual")
            return BadRequest(ApiResponse.Fail("清空类型无效"));

        var count = await _distributionService.ClearSalesByOriginAsync(origin);
        return Ok(ApiResponse.Ok($"已清空Excel/手工销售数据 {count} 条"));
    }

    [HttpDelete("clear-purchases")]
    public async Task<ActionResult<ApiResponse>> ClearPurchasesByOrigin(
        [FromQuery] string origin)
    {
        if (origin != "manual")
            return BadRequest(ApiResponse.Fail("清空类型无效"));

        var count = await _distributionService.ClearPurchasesByOriginAsync(origin);
        return Ok(ApiResponse.Ok($"已清空Excel/手工采购数据 {count} 条"));
    }

    [HttpGet("import-batches")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ImportBatchDto>>>> GetImportBatches(
        [FromQuery] bool isPurchase)
    {
        var batches = await _distributionService.GetImportBatchesAsync(isPurchase);
        return Ok(ApiResponse<IReadOnlyList<ImportBatchDto>>.Ok(batches));
    }

    [HttpGet("import-batches/{batchId:guid}/details")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DistributionImportDetailDto>>>> GetImportDetails(Guid batchId)
    {
        var details = await _distributionService.GetImportDetailsAsync(batchId);
        return Ok(ApiResponse<IReadOnlyList<DistributionImportDetailDto>>.Ok(details));
    }

    [HttpPost("import-batches/{batchId:guid}/details")]
    public async Task<ActionResult<ApiResponse>> SaveImportDetails(Guid batchId, [FromBody] List<DistributionImportDetailDto> details)
    {
        foreach (var detail in details)
            detail.ImportBatchId = batchId;

        await _distributionService.SaveImportDetailsAsync(details);
        return Ok(ApiResponse.Ok("导入明细已保存"));
    }

    [HttpDelete("import-batches/{batchId:guid}")]
    public async Task<ActionResult<ApiResponse>> ClearImportBatch(Guid batchId)
    {
        var count = await _distributionService.ClearImportBatchAsync(batchId);
        return Ok(ApiResponse.Ok($"已清空该导入批次 {count} 条"));
    }

    [HttpGet("conflicts")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ChannelConflictDto>>>> GetConflicts(
        [FromQuery] string? batchNo = null)
    {
        var items = await _distributionService.GetChannelConflictsAsync(batchNo);
        return Ok(ApiResponse<IReadOnlyList<ChannelConflictDto>>.Ok(items));
    }

    // ═══════════════════════════════════════════
    // Review (审核)
    // ═══════════════════════════════════════════

    [HttpGet("review")]
    public async Task<ActionResult<ApiResponse<PagedResult<DistributionReviewDto>>>> GetPendingReview(
        [FromQuery] int page = 0, [FromQuery] int size = 20, [FromQuery] string? status = null)
    {
        var db = HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var query = db.DistributionRecords.Include(d => d.Drug).Include(d => d.ToCustomer)
            .Where(d => d.ReviewStatus != null);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(d => d.ReviewStatus == status);

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(d => d.CreatedAt).Skip(page * size).Take(size).ToListAsync();
        var dtos = items.Select(d => new DistributionReviewDto
        {
            Id = d.Id, DrugName = d.Drug?.Name ?? "", BatchNo = d.BatchNo,
            ToCustomerName = d.ToCustomer?.Name, Quantity = d.Quantity,
            TaxedUnitPrice = d.TaxedUnitPrice, DistributionDate = d.DistributionDate,
            Province = d.Province, ReviewStatus = d.ReviewStatus, RejectReason = d.RejectReason,
        }).ToList();
        return Ok(ApiResponse<PagedResult<DistributionReviewDto>>.Ok(new PagedResult<DistributionReviewDto>
        { Items = dtos, TotalCount = total, PageIndex = page, PageSize = size }));
    }

    [HttpPost("review/approve")]
    public async Task<ActionResult<ApiResponse>> ApproveReview([FromBody] ReviewRequest request)
    {
        var db = HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var reviewerId = User.GetUserId();
        var records = await db.DistributionRecords.Where(d => request.RecordIds.Contains(d.Id) && d.ReviewStatus == "待审核").ToListAsync();
        foreach (var r in records) { r.ReviewStatus = "已通过"; r.ReviewerId = reviewerId; r.ReviewedAt = DateTime.UtcNow; r.UpdatedAt = DateTime.UtcNow; }
        await db.SaveChangesAsync();
        return Ok(ApiResponse.Ok($"已通过 {records.Count} 条"));
    }

    [HttpPost("review/reject")]
    public async Task<ActionResult<ApiResponse>> RejectReview([FromBody] RejectRequest request)
    {
        var db = HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var reviewerId = User.GetUserId();
        var records = await db.DistributionRecords.Where(d => request.RecordIds.Contains(d.Id) && d.ReviewStatus == "待审核").ToListAsync();
        foreach (var r in records) { r.ReviewStatus = "已驳回"; r.ReviewerId = reviewerId; r.ReviewedAt = DateTime.UtcNow; r.RejectReason = request.Reason; r.UpdatedAt = DateTime.UtcNow; }
        await db.SaveChangesAsync();
        return Ok(ApiResponse.Ok($"已驳回 {records.Count} 条"));
    }
}
