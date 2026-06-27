using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PharmaFlow.Application.DTOs;
using PharmaFlow.Domain.Entities;
using PharmaFlow.Domain.Enums;
using PharmaFlow.Infrastructure.Data;

namespace PharmaFlow.Application.Services;

public class CollectionService : ICollectionService
{
    private readonly AppDbContext _db;
    private readonly CollectionExecutionService _executionService;
    private readonly DataCleaner _dataCleaner;
    private readonly ILogger<CollectionService> _logger;

    public CollectionService(
        AppDbContext db,
        CollectionExecutionService executionService,
        DataCleaner dataCleaner,
        ILogger<CollectionService> logger)
    {
        _db = db;
        _executionService = executionService;
        _dataCleaner = dataCleaner;
        _logger = logger;
    }

    // ═══════════════════════════════════════════
    // Source CRUD
    // ═══════════════════════════════════════════

    public async Task<PagedResult<CollectionSourceDto>> GetSourcesPagedAsync(int pageIndex, int pageSize, string? search = null)
    {
        var query = _db.CollectionSources.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(s => s.Name.Contains(search));

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(s => s.CreatedAt)
            .Skip(pageIndex * pageSize).Take(pageSize).ToListAsync();

        return new PagedResult<CollectionSourceDto>
        {
            Items = items.Select(MapSourceToDto).ToList(),
            TotalCount = total,
            PageIndex = pageIndex,
            PageSize = pageSize,
        };
    }

    public async Task<CollectionSourceDto?> GetSourceByIdAsync(Guid id)
    {
        var source = await _db.CollectionSources.FirstOrDefaultAsync(s => s.Id == id);
        return source == null ? null : MapSourceToDto(source);
    }

    public async Task<CollectionSourceDto> CreateSourceAsync(CollectionSourceDto dto)
    {
        var source = new CollectionSource
        {
            Name = dto.Name,
            PlatformType = Enum.TryParse<CollectionPlatformType>(dto.PlatformType, out var pt) ? pt : CollectionPlatformType.流向平台,
            BaseUrl = dto.BaseUrl,
            AuthType = Enum.TryParse<CollectionAuthType>(dto.AuthType, out var at) ? at : CollectionAuthType.Cookie,
            AuthConfig = dto.AuthConfig,
            ExportConfig = dto.ExportConfig,
            TransformConfig = dto.TransformConfig,
            IsEnabled = dto.IsEnabled,
            ScheduleCron = dto.ScheduleCron,
            RuleId = dto.RuleId,
        };
        _db.CollectionSources.Add(source);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch
        {
            _db.Entry(source).State = EntityState.Detached;
            throw;
        }
        dto.Id = source.Id;
        return dto;
    }

    public async Task UpdateSourceAsync(CollectionSourceDto dto)
    {
        var source = await _db.CollectionSources.FindAsync(dto.Id);
        if (source == null) return;

        source.Name = dto.Name;
        source.PlatformType = Enum.TryParse<CollectionPlatformType>(dto.PlatformType, out var pt) ? pt : CollectionPlatformType.流向平台;
        source.BaseUrl = dto.BaseUrl;
        source.AuthType = Enum.TryParse<CollectionAuthType>(dto.AuthType, out var at) ? at : CollectionAuthType.Cookie;
        source.AuthConfig = dto.AuthConfig;
        source.ExportConfig = dto.ExportConfig;
        source.TransformConfig = dto.TransformConfig;
        source.IsEnabled = dto.IsEnabled;
        source.ScheduleCron = dto.ScheduleCron;
        source.RuleId = dto.RuleId;
        source.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task DeleteSourceAsync(Guid id)
    {
        var source = await _db.CollectionSources.FindAsync(id);
        if (source != null)
        {
            source.IsDeleted = true;
            source.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    public async Task<IReadOnlyList<DropdownItem>> GetSourceDropdownAsync()
    {
        return await _db.CollectionSources.Where(s => !s.IsDeleted && s.IsEnabled)
            .OrderBy(s => s.Name)
            .Select(s => new DropdownItem { Id = s.Id, Name = s.Name })
            .ToListAsync();
    }

    // ═══════════════════════════════════════════
    // Task management
    // ═══════════════════════════════════════════

    public async Task<PagedResult<CollectionTaskDto>> GetTasksPagedAsync(int pageIndex, int pageSize, Guid? sourceId = null)
    {
        var query = _db.CollectionTasks.Include(t => t.Source).ThenInclude(s => s.Rule).AsQueryable();

        if (sourceId.HasValue)
            query = query.Where(t => t.SourceId == sourceId);

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(t => t.CreatedAt)
            .Skip(pageIndex * pageSize).Take(pageSize).ToListAsync();

        return new PagedResult<CollectionTaskDto>
        {
            Items = items.Select(MapTaskToDto).ToList(),
            TotalCount = total,
            PageIndex = pageIndex,
            PageSize = pageSize,
        };
    }

    public async Task<CollectionTaskDto?> GetTaskByIdAsync(Guid id)
    {
        var task = await _db.CollectionTasks.Include(t => t.Source).ThenInclude(s => s.Rule)
            .FirstOrDefaultAsync(t => t.Id == id);
        return task == null ? null : MapTaskToDto(task);
    }

    public async Task ClearSummaryAsync()
    {
        var tasks = await _db.CollectionTasks.Where(t => t.IsSubmitted).ToListAsync();
        foreach (var t in tasks) t.IsSubmitted = false;
        await _db.SaveChangesAsync();
    }

    public async Task SubmitTaskAsync(Guid id)
    {
        var task = await _db.CollectionTasks.FindAsync(id);
        if (task != null) { task.IsSubmitted = true; await _db.SaveChangesAsync(); }
    }

    public async Task DeleteTaskAsync(Guid id)
    {
        var records = await _db.CollectionRecords.Where(r => r.TaskId == id).ToListAsync();
        _db.CollectionRecords.RemoveRange(records);
        var task = await _db.CollectionTasks.FindAsync(id);
        if (task != null) _db.CollectionTasks.Remove(task);
        await _db.SaveChangesAsync();
    }

    // ═══════════════════════════════════════════
    // Manual trigger
    // ═══════════════════════════════════════════

    public async Task<CollectionTaskDto> TriggerManualAsync(Guid sourceId, Guid? triggeredByUserId = null)
    {
        var source = await _db.CollectionSources.FirstOrDefaultAsync(s => s.Id == sourceId && !s.IsDeleted);
        if (source == null)
            throw new InvalidOperationException("采集源不存在或已删除");

        // 每次采集创建新任务（数据管理按源合并显示，日志保留每次记录）
        var task = new CollectionTask { SourceId = sourceId, TriggerType = TriggerType.手动, Status = CollectionTaskStatus.执行中, StartedAt = DateTime.UtcNow };
        _db.CollectionTasks.Add(task);
        await _db.SaveChangesAsync();

        try
        {
            var results = await _executionService.ExecuteAsync(source.BaseUrl, source.AuthType.ToString(), source.AuthConfig, source.ExportConfig, source.TransformConfig);

            // 去重：获取已有 RawData 跳过重复
            var existingSet = new HashSet<string>(await _db.CollectionRecords.Where(r => r.SourceId == sourceId).Select(r => r.RawDataJson).ToListAsync());

            int successCount = 0, errorCount = 0, skipped = 0;
            foreach (var row in results)
            {
                if (existingSet.Contains(row.RawDataJson)) { skipped++; continue; }
                existingSet.Add(row.RawDataJson);
                _db.CollectionRecords.Add(new CollectionRecord
                {
                    TaskId = task.Id, SourceId = source.Id,
                    Status = string.IsNullOrEmpty(row.ErrorMessage) ? CollectionRecordStatus.待审核 : CollectionRecordStatus.错误,
                    RawDataJson = row.RawDataJson, CleanedDataJson = row.CleanedDataJson,
                    TargetEntityType = row.TargetEntityType, ErrorMessage = row.ErrorMessage,
                });
                if (string.IsNullOrEmpty(row.ErrorMessage)) successCount++; else errorCount++;
            }

            task.Status = CollectionTaskStatus.已完成; task.CompletedAt = DateTime.UtcNow;
            task.TotalRecords = successCount + errorCount + skipped;
            task.SuccessCount = successCount;
            task.ErrorCount = errorCount;
            task.SkippedCount = skipped;
            source.LastCollectionAt = DateTime.UtcNow;
            source.LastCollectionStatus = skipped > 0 ? $"成功(跳过{skipped}条重复)" : "成功";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "采集执行失败, SourceId={SourceId}", sourceId);
            task.Status = CollectionTaskStatus.失败; task.CompletedAt = DateTime.UtcNow; task.ErrorMessage = ex.Message;
            source.LastCollectionAt = DateTime.UtcNow; source.LastCollectionStatus = "失败";
        }

        await _db.SaveChangesAsync();
        task.Source = source;
        return MapTaskToDto(task);
    }

    // ═══════════════════════════════════════════
    // Records and review
    // ═══════════════════════════════════════════

    public async Task<PagedResult<CollectionRecordDto>> GetRecordsPagedAsync(int pageIndex, int pageSize,
        Guid? taskId = null, Guid? sourceId = null, string? status = null)
    {
        var query = _db.CollectionRecords
            .Include(r => r.Source)
            .Include(r => r.Reviewer)
            .AsQueryable();

        if (taskId.HasValue)
            query = query.Where(r => r.TaskId == taskId);
        if (sourceId.HasValue)
            query = query.Where(r => r.SourceId == sourceId);
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (Enum.TryParse<CollectionRecordStatus>(status, out var s))
                query = query.Where(r => r.Status == s);
        }

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(r => r.CreatedAt)
            .Skip(pageIndex * pageSize).Take(pageSize).ToListAsync();

        return new PagedResult<CollectionRecordDto>
        {
            Items = items.Select(MapRecordToDto).ToList(),
            TotalCount = total,
            PageIndex = pageIndex,
            PageSize = pageSize,
        };
    }

    public async Task ApproveRecordsAsync(List<Guid> recordIds, Guid reviewerId)
    {
        var records = await _db.CollectionRecords
            .Where(r => recordIds.Contains(r.Id) && r.Status == CollectionRecordStatus.待审核)
            .ToListAsync();

        foreach (var record in records)
        {
            record.Status = CollectionRecordStatus.已通过;
            record.ReviewerId = reviewerId;
            record.ReviewedAt = DateTime.UtcNow;
            record.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();
    }

    public async Task RejectRecordsAsync(List<Guid> recordIds, Guid reviewerId, string reason)
    {
        var records = await _db.CollectionRecords
            .Where(r => recordIds.Contains(r.Id) && r.Status == CollectionRecordStatus.待审核)
            .ToListAsync();

        foreach (var record in records)
        {
            record.Status = CollectionRecordStatus.已驳回;
            record.ReviewerId = reviewerId;
            record.ReviewedAt = DateTime.UtcNow;
            record.RejectReason = reason;
            record.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();
    }

    public async Task<int> ImportApprovedAsync(List<Guid> recordIds)
    {
        var records = await _db.CollectionRecords
            .Where(r => recordIds.Contains(r.Id) && r.Status == CollectionRecordStatus.已通过)
            .ToListAsync();

        int imported = 0;
        foreach (var record in records)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(record.CleanedDataJson))
                {
                    record.ErrorMessage = "清洗后的数据为空，无法导入";
                    record.Status = CollectionRecordStatus.错误;
                    continue;
                }

                if (record.TargetEntityType == "DistributionRecord")
                {
                    ImportAsDistributionRecord(record);
                }
                // 可扩展: 其他 TargetEntityType 的分支
                else
                {
                    // 通用处理: 标记已导入但不创建具体实体
                    record.TargetEntityId = null;
                }

                record.Status = CollectionRecordStatus.已导入;
                record.UpdatedAt = DateTime.UtcNow;
                imported++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "导入记录 {RecordId} 失败", record.Id);
                record.ErrorMessage = $"导入失败: {ex.Message}";
                record.Status = CollectionRecordStatus.错误;
            }
        }

        await _db.SaveChangesAsync();
        return imported;
    }

    private void ImportAsDistributionRecord(CollectionRecord record)
    {
        using var doc = JsonDocument.Parse(record.CleanedDataJson!);
        var root = doc.RootElement;

        // 优先从 _matches 读取预匹配的 ID（SmartDataCleaner 结果）
        Guid? drugId = null;
        Guid? toCustomerId = null;

        if (root.TryGetProperty("_matches", out var matchesEl))
        {
            if (matchesEl.TryGetProperty("drugId", out var dId) && dId.ValueKind != JsonValueKind.Null)
                drugId = dId.GetGuid();
            if (matchesEl.TryGetProperty("customerId", out var cId) && cId.ValueKind != JsonValueKind.Null)
                toCustomerId = cId.GetGuid();
        }

        // 回退：精确名称匹配（兼容无 SmartDataCleaner 的旧数据）
        if (drugId == null && root.TryGetProperty("DrugName", out var drugNameEl))
        {
            var drugName = drugNameEl.GetString();
            drugId = _db.Drugs.FirstOrDefault(d => d.Name == drugName && !d.IsDeleted)?.Id;
        }
        if (toCustomerId == null && root.TryGetProperty("ToCustomerName", out var custNameEl))
        {
            var custName = custNameEl.GetString();
            toCustomerId = _db.Customers.FirstOrDefault(c => c.Name == custName && !c.IsDeleted)?.Id;
        }

        // 药品未匹配时拒绝导入
        if (drugId == null || drugId == Guid.Empty)
            throw new InvalidOperationException("药品未匹配到主数据，无法导入");

        var distRecord = new DistributionRecord
        {
            DrugId = drugId.Value,
            BatchNo = root.TryGetProperty("BatchNo", out var bn) ? bn.GetString() ?? "" : "",
            Quantity = root.TryGetProperty("Quantity", out var qty) ? qty.GetDecimal() : 0,
            TaxedUnitPrice = root.TryGetProperty("TaxedUnitPrice", out var up) ? up.GetDecimal() : 0,
            ToCustomerId = toCustomerId,
            Province = root.TryGetProperty("Province", out var p) ? p.GetString() : null,
            SalesPerson = root.TryGetProperty("SalesPerson", out var sp) ? sp.GetString() : null,
            DeliveryOrderNo = root.TryGetProperty("DeliveryOrderNo", out var don) ? don.GetString() : null,
            Operator = root.TryGetProperty("Operator", out var op) ? op.GetString() : null,
            DistributionDate = root.TryGetProperty("DistributionDate", out var dd) && dd.TryGetDateTime(out var dt) ? dt : DateTime.UtcNow,
            DistributionDateDisplay = root.TryGetProperty("DistributionDateDisplay", out var ddd) ? ddd.GetString() : null,
        };

        _db.DistributionRecords.Add(distRecord);
        record.TargetEntityId = distRecord.Id;
    }

    // ═══════════════════════════════════════════
    // Mapping helpers
    // ═══════════════════════════════════════════

    private static CollectionSourceDto MapSourceToDto(CollectionSource s) => new()
    {
        Id = s.Id,
        Name = s.Name,
        PlatformType = s.PlatformType.ToString(),
        BaseUrl = s.BaseUrl,
        AuthType = s.AuthType.ToString(),
        AuthConfig = s.AuthConfig,
        ExportConfig = s.ExportConfig,
        TransformConfig = s.TransformConfig,
        IsEnabled = s.IsEnabled,
        ScheduleCron = s.ScheduleCron,
        RuleId = s.RuleId,
        RuleName = s.Rule?.Name,
        LastCollectionAt = s.LastCollectionAt,
        LastCollectionStatus = s.LastCollectionStatus,
    };

    private static CollectionTaskDto MapTaskToDto(CollectionTask t) => new()
    {
        Id = t.Id,
        SourceId = t.SourceId,
        SourceName = t.Source?.Name ?? "",
        TriggerType = t.TriggerType.ToString(),
        Status = t.Status.ToString(),
        StartedAt = t.StartedAt,
        CompletedAt = t.CompletedAt,
        TotalRecords = t.TotalRecords,
        SuccessCount = t.SuccessCount,
        ErrorCount = t.ErrorCount,
        SkippedCount = t.SkippedCount,
        IsSubmitted = t.IsSubmitted,
        ErrorMessage = t.ErrorMessage,
    };

    private static CollectionRecordDto MapRecordToDto(CollectionRecord r) => new()
    {
        Id = r.Id,
        TaskId = r.TaskId,
        SourceId = r.SourceId,
        SourceName = r.Source?.Name ?? "",
        Status = r.Status.ToString(),
        RawDataJson = r.RawDataJson,
        CleanedDataJson = r.CleanedDataJson,
        TargetEntityType = r.TargetEntityType,
        TargetEntityId = r.TargetEntityId,
        ReviewerName = r.Reviewer?.DisplayName,
        ReviewedAt = r.ReviewedAt,
        RejectReason = r.RejectReason,
        ErrorMessage = r.ErrorMessage,
    };
}
