using Microsoft.AspNetCore.Mvc;
using PharmaFlow.Application.DTOs;
using PharmaFlow.Application.Services;
using PharmaFlow.WebApi.Extensions;
using PharmaFlow.WebApi.Models;

namespace PharmaFlow.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CollectionController : ControllerBase
{
    private readonly ICollectionService _collectionService;
    private readonly BatchProcessor _batchProcessor;

    public CollectionController(ICollectionService collectionService, BatchProcessor batchProcessor)
    {
        _collectionService = collectionService;
        _batchProcessor = batchProcessor;
    }

    // ═══════════════════════════════════════════
    // Sources
    // ═══════════════════════════════════════════

    [HttpGet("sources")]
    public async Task<ActionResult<ApiResponse<PagedResult<CollectionSourceDto>>>> GetSources(
        [FromQuery] int page = 0, [FromQuery] int size = 20, [FromQuery] string? search = null)
    {
        var result = await _collectionService.GetSourcesPagedAsync(page, size, search);
        return Ok(ApiResponse<PagedResult<CollectionSourceDto>>.Ok(result));
    }

    [HttpGet("sources/{id:guid}")]
    public async Task<ActionResult<ApiResponse<CollectionSourceDto>>> GetSource(Guid id)
    {
        var result = await _collectionService.GetSourceByIdAsync(id);
        if (result == null) return NotFound(ApiResponse<CollectionSourceDto>.Fail("采集源不存在"));
        return Ok(ApiResponse<CollectionSourceDto>.Ok(result));
    }

    [HttpPost("sources")]
    public async Task<ActionResult<ApiResponse<CollectionSourceDto>>> CreateSource(
        [FromBody] CollectionSourceDto dto)
    {
        var result = await _collectionService.CreateSourceAsync(dto);
        return Ok(ApiResponse<CollectionSourceDto>.Ok(result, "创建成功"));
    }

    [HttpPut("sources/{id:guid}")]
    public async Task<ActionResult<ApiResponse>> UpdateSource(
        Guid id, [FromBody] CollectionSourceDto dto)
    {
        dto.Id = id;
        await _collectionService.UpdateSourceAsync(dto);
        return Ok(ApiResponse.Ok("更新成功"));
    }

    [HttpPost("sources/{id:guid}/autosubmit")]
    public async Task<ActionResult<ApiResponse>> SetAutoSubmit(Guid id, [FromBody] AutoSubmitRequest req)
    {
        var source = await _collectionService.GetSourceByIdAsync(id);
        if (source == null) return NotFound(ApiResponse.Fail("采集源不存在"));
        source.AutoSubmit = req.AutoSubmit;
        await _collectionService.UpdateSourceAsync(source);
        return Ok(ApiResponse.Ok(req.AutoSubmit ? "已开启自动提交" : "已关闭自动提交"));
    }

    [HttpPut("sources/{id:guid}/token")]
    public async Task<ActionResult<ApiResponse>> UpdateToken(Guid id, [FromBody] TokenUpdateRequest req)
    {
        var source = await _collectionService.GetSourceByIdAsync(id);
        if (source == null) return NotFound(ApiResponse.Fail("采集源不存在"));
        source.AuthConfig = System.Text.Json.JsonSerializer.Serialize(new {
            token = new { cookie_name = req.CookieName ?? "FLOW_TOKEN", value = req.Token },
            headers = new { Origin = source.BaseUrl, Referer = source.BaseUrl + "/index.html" }
        });
        await _collectionService.UpdateSourceAsync(source);
        return Ok(ApiResponse.Ok("Token已更新"));
    }

    [HttpDelete("sources/{id:guid}")]
    public async Task<ActionResult<ApiResponse>> DeleteSource(Guid id)
    {
        await _collectionService.DeleteSourceAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }

    [HttpGet("sources/dropdown")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DropdownItem>>>> GetSourceDropdown()
    {
        var result = await _collectionService.GetSourceDropdownAsync();
        return Ok(ApiResponse<IReadOnlyList<DropdownItem>>.Ok(result));
    }

    // ═══════════════════════════════════════════
    // Tasks
    // ═══════════════════════════════════════════

    [HttpGet("tasks")]
    public async Task<ActionResult<ApiResponse<PagedResult<CollectionTaskDto>>>> GetTasks(
        [FromQuery] int page = 0, [FromQuery] int size = 20, [FromQuery] Guid? sourceId = null)
    {
        var result = await _collectionService.GetTasksPagedAsync(page, size, sourceId);
        return Ok(ApiResponse<PagedResult<CollectionTaskDto>>.Ok(result));
    }

    [HttpGet("tasks/{id:guid}")]
    public async Task<ActionResult<ApiResponse<CollectionTaskDto>>> GetTask(Guid id)
    {
        var result = await _collectionService.GetTaskByIdAsync(id);
        if (result == null) return NotFound(ApiResponse<CollectionTaskDto>.Fail("采集任务不存在"));
        return Ok(ApiResponse<CollectionTaskDto>.Ok(result));
    }

    [HttpPost("summary/clear")]
    public async Task<ActionResult<ApiResponse>> ClearSummary()
    {
        await _collectionService.ClearSummaryAsync();
        return Ok(ApiResponse.Ok("已清空采集汇总"));
    }

    [HttpPost("tasks/{id:guid}/submit")]
    public async Task<ActionResult<ApiResponse>> SubmitTask(Guid id)
    {
        await _collectionService.SubmitTaskAsync(id);
        return Ok(ApiResponse.Ok("已提交到采集汇总"));
    }

    [HttpDelete("tasks/{id:guid}")]
    public async Task<ActionResult<ApiResponse>> DeleteTask(Guid id)
    {
        await _collectionService.DeleteTaskAsync(id);
        return Ok(ApiResponse.Ok("已删除"));
    }

    [HttpPost("trigger")]
    public async Task<ActionResult<ApiResponse<CollectionTaskDto>>> TriggerManual(
        [FromBody] ManualTriggerRequest request)
    {
        var userId = User.GetUserId();
        var task = await _collectionService.TriggerManualAsync(request.SourceId,
            userId != Guid.Empty ? userId : null);
        return Ok(ApiResponse<CollectionTaskDto>.Ok(task, "采集任务已启动"));
    }

    // ═══════════════════════════════════════════
    // Records / Review
    // ═══════════════════════════════════════════

    [HttpGet("records")]
    public async Task<ActionResult<ApiResponse<PagedResult<CollectionRecordDto>>>> GetRecords(
        [FromQuery] int page = 0, [FromQuery] int size = 20,
        [FromQuery] Guid? taskId = null, [FromQuery] Guid? sourceId = null,
        [FromQuery] string? status = null)
    {
        var result = await _collectionService.GetRecordsPagedAsync(page, size, taskId, sourceId, status);
        return Ok(ApiResponse<PagedResult<CollectionRecordDto>>.Ok(result));
    }

    [HttpPost("records/approve")]
    public async Task<ActionResult<ApiResponse>> Approve([FromBody] ReviewRequest request)
    {
        var reviewerId = User.GetUserId();
        if (reviewerId == Guid.Empty)
            return Unauthorized(ApiResponse.Fail("未登录或Token无效"));
        await _collectionService.ApproveRecordsAsync(request.RecordIds, reviewerId);
        return Ok(ApiResponse.Ok($"已通过 {request.RecordIds.Count} 条记录"));
    }

    [HttpPost("records/reject")]
    public async Task<ActionResult<ApiResponse>> Reject([FromBody] RejectRequest request)
    {
        var reviewerId = User.GetUserId();
        if (reviewerId == Guid.Empty)
            return Unauthorized(ApiResponse.Fail("未登录或Token无效"));
        await _collectionService.RejectRecordsAsync(request.RecordIds, reviewerId, request.Reason);
        return Ok(ApiResponse.Ok($"已驳回 {request.RecordIds.Count} 条记录"));
    }

    [HttpPost("records/import")]
    public async Task<ActionResult<ApiResponse>> Import([FromBody] ReviewRequest request)
    {
        var count = await _collectionService.ImportApprovedAsync(request.RecordIds);
        return Ok(ApiResponse.Ok($"已导入 {count} 条记录"));
    }

    // ═══════════════════════════════════════════
    // Batch processing
    // ═══════════════════════════════════════════

    [HttpGet("batch/{taskId:guid}/preview")]
    public async Task<ActionResult<ApiResponse<BatchPreviewDto>>> PreviewBatch(Guid taskId, [FromQuery] Guid? ruleId = null)
    {
        var result = await _batchProcessor.PreviewAsync(taskId, ruleId);
        return Ok(ApiResponse<BatchPreviewDto>.Ok(result));
    }

    [HttpPost("batch/{taskId:guid}/import")]
    public async Task<ActionResult<ApiResponse<BatchImportResult>>> ImportBatch(Guid taskId, [FromQuery] Guid? ruleId = null)
    {
        var result = await _batchProcessor.ImportAsync(taskId);
        return Ok(ApiResponse<BatchImportResult>.Ok(result,
            $"创建药品 {result.CreatedDrugs} 个, 客户 {result.CreatedCustomers} 个, 导入 {result.Imported} 条, 跳过 {result.Skipped} 条"));
    }
}
