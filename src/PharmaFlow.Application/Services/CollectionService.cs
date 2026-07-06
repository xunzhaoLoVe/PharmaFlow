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
    private readonly SmartDataCleaner _smartDataCleaner;
    private readonly ILogger<CollectionService> _logger;

    public CollectionService(
        AppDbContext db,
        CollectionExecutionService executionService,
        SmartDataCleaner smartDataCleaner,
        ILogger<CollectionService> logger)
    {
        _db = db;
        _executionService = executionService;
        _smartDataCleaner = smartDataCleaner;
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
            CollectionMode = Enum.TryParse<CollectionMode>(dto.CollectionMode, out var cm) ? cm : CollectionMode.JsonConfig,
            AuthConfig = dto.AuthConfig,
            ExportConfig = dto.ExportConfig,
            TransformConfig = dto.TransformConfig,
            IsEnabled = dto.IsEnabled,
            ScheduleCron = dto.ScheduleCron,
            RuleId = dto.RuleId,
            AutoSubmit = dto.AutoSubmit,
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
        source.CollectionMode = Enum.TryParse<CollectionMode>(dto.CollectionMode, out var cm) ? cm : CollectionMode.JsonConfig;
        source.AuthConfig = dto.AuthConfig;
        source.ExportConfig = dto.ExportConfig;
        source.TransformConfig = dto.TransformConfig;
        source.IsEnabled = dto.IsEnabled;
        source.ScheduleCron = dto.ScheduleCron;
        source.RuleId = dto.RuleId;
        source.AutoSubmit = dto.AutoSubmit;
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

    public async Task<SyncSummaryResultDto> SyncSummaryAsync(Guid? sourceId = null)
    {
        var query = _db.CollectionRecords
            .Include(r => r.Task)
            .Where(r => r.Task.IsSubmitted);

        if (sourceId.HasValue)
            query = query.Where(r => r.SourceId == sourceId.Value);

        var records = await query.OrderBy(r => r.CreatedAt).ToListAsync();
        var result = new SyncSummaryResultDto { TotalRecords = records.Count };

        var drugs = await _db.Drugs.Where(d => !d.IsDeleted).ToListAsync();
        var customers = await _db.Customers.Where(c => !c.IsDeleted).ToListAsync();
        var drugCodes = new HashSet<string>(await _db.Drugs.IgnoreQueryFilters().Select(d => d.Code).ToListAsync(), StringComparer.OrdinalIgnoreCase);
        var customerCodes = new HashSet<string>(await _db.Customers.IgnoreQueryFilters().Select(c => c.Code).ToListAsync(), StringComparer.OrdinalIgnoreCase);

        var existingDistributions = await _db.DistributionRecords
            .Select(d => new
            {
                d.DrugId,
                d.BatchNo,
                d.FromCustomerId,
                d.ToCustomerId,
                d.Quantity,
                d.DeliveryOrderNo,
                d.DistributionDate
            })
            .ToListAsync();
        var distributionKeys = existingDistributions
            .Select(d => DistributionSyncKey(d.DrugId, d.BatchNo, d.FromCustomerId, d.ToCustomerId, d.Quantity, d.DeliveryOrderNo, d.DistributionDate))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var record in records)
        {
            if (string.IsNullOrWhiteSpace(record.CleanedDataJson) || IsExcludedFromSummary(record.CleanedDataJson))
            {
                result.SkippedRecords++;
                continue;
            }

            try
            {
                using var doc = JsonDocument.Parse(record.CleanedDataJson);
                var root = doc.RootElement;

                var drugName = GetString(root, "DrugName");
                if (string.IsNullOrWhiteSpace(drugName))
                {
                    result.SkippedRecords++;
                    continue;
                }

                var specification = GetString(root, "Specification");
                var drug = FindOrCreateDrug(root, drugName, specification, drugs, drugCodes, result);
                if (_db.Entry(drug).State == EntityState.Detached)
                    _db.Drugs.Add(drug);

                var fromName = FirstNonEmpty(
                    GetString(root, "FromCustomerName"),
                    GetString(root, "CompanyName"),
                    GetString(root, "SupplierShortName"),
                    GetString(root, "OriginalSupplierShortName"));
                var toName = FirstNonEmpty(
                    GetString(root, "ToCustomerName"),
                    GetString(root, "CustomerName"),
                    GetString(root, "CustomerShortName"));

                Customer? fromCustomer = null;
                Customer? toCustomer = null;
                if (!string.IsNullOrWhiteSpace(fromName))
                {
                    fromCustomer = FindOrCreateSourceDealer(root, fromName, null, customers, customerCodes, result);
                    if (_db.Entry(fromCustomer).State == EntityState.Detached)
                        _db.Customers.Add(fromCustomer);
                }
                if (!string.IsNullOrWhiteSpace(toName))
                {
                    toCustomer = FindOrCreateDestinationCustomer(root, toName, GetString(root, "CustomerCode"), customers, customerCodes, result);
                    if (_db.Entry(toCustomer).State == EntityState.Detached)
                        _db.Customers.Add(toCustomer);
                }

                if (fromCustomer == null || toCustomer == null)
                {
                    result.SkippedRecords++;
                    continue;
                }

                var batchNo = FirstNonEmpty(GetString(root, "BatchNo"), "-");
                var quantity = FirstNonZero(
                    GetDecimal(root, "SalesQuantity"),
                    GetDecimal(root, "Quantity"),
                    GetDecimal(root, "PurchaseQuantity"));
                if (quantity == 0)
                {
                    result.SkippedRecords++;
                    continue;
                }

                var distributionDate = GetDateTime(root, "DistributionDate") ?? DateTime.UtcNow;
                var deliveryOrderNo = GetString(root, "DeliveryOrderNo");
                var key = DistributionSyncKey(drug.Id, batchNo, fromCustomer.Id, toCustomer.Id, quantity, deliveryOrderNo, distributionDate);
                if (!distributionKeys.Add(key))
                {
                    result.SkippedRecords++;
                    continue;
                }

                _db.DistributionRecords.Add(new DistributionRecord
                {
                    DrugId = drug.Id,
                    BatchNo = batchNo,
                    FromCustomerId = fromCustomer.Id,
                    ToCustomerId = toCustomer.Id,
                    Quantity = quantity,
                    SalesPerson = FirstNonEmpty(GetString(root, "SalesPerson"), GetString(root, "Operator")),
                    DeliveryOrderNo = deliveryOrderNo,
                    TaxedUnitPrice = GetDecimal(root, "TaxedUnitPrice"),
                    DistributionDate = distributionDate,
                    DistributionDateDisplay = FormatDateOnly(distributionDate),
                    Operator = "采集汇总同步",
                    Province = GetString(root, "Province"),
                });
                result.CreatedDistributionRecords++;
            }
            catch
            {
                result.SkippedRecords++;
            }
        }

        await _db.SaveChangesAsync();
        return result;
    }

    public async Task SubmitTaskAsync(Guid id)
    {
        var task = await _db.CollectionTasks
            .Include(t => t.Source)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (task == null) return;
        if (task.Source == null) return;

        var records = await _db.CollectionRecords
            .Where(r => r.TaskId == id)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync();

        await ReapplyTransformForRecordsAsync(task.Source, records);
        task.IsSubmitted = true;
        await _db.SaveChangesAsync();
    }

    public async Task UnsubmitTaskAsync(Guid id)
    {
        var task = await _db.CollectionTasks.FindAsync(id);
        if (task != null) { task.IsSubmitted = false; await _db.SaveChangesAsync(); }
    }

    public async Task<ReapplyTransformResultDto> ReapplyTransformAsync(Guid sourceId)
    {
        var source = await _db.CollectionSources.FirstOrDefaultAsync(s => s.Id == sourceId && !s.IsDeleted);
        if (source == null)
            throw new InvalidOperationException("采集源不存在或已删除");

        var records = await _db.CollectionRecords
            .Where(r => r.SourceId == sourceId)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync();

        var (updated, errors) = await ReapplyTransformForRecordsAsync(source, records);

        await _db.SaveChangesAsync();
        return new ReapplyTransformResultDto
        {
            SourceId = sourceId,
            TotalRecords = records.Count,
            UpdatedRecords = updated,
            ErrorRecords = errors,
        };
    }

    private async Task<(int updated, int errors)> ReapplyTransformForRecordsAsync(
        CollectionSource source,
        List<CollectionRecord> records)
    {
        var seenDedupKeys = new HashSet<string>();
        var cleanedJsons = new List<string>();
        var cleanedRecords = new List<CollectionRecord>();
        int updated = 0, errors = 0;

        foreach (var record in records)
        {
            try
            {
                var rawRow = ParseRawRow(record.RawDataJson);
                var result = await _smartDataCleaner.CleanAsync(rawRow, source.TransformConfig, seenDedupKeys);
                record.CleanedDataJson = result.CleanedDataJson;
                record.TargetEntityType = result.TargetEntityType ?? record.TargetEntityType;
                record.ErrorMessage = result.ErrorMessage;
                record.UpdatedAt = DateTime.UtcNow;
                updated++;

                if (string.IsNullOrWhiteSpace(result.ErrorMessage))
                {
                    cleanedRecords.Add(record);
                    cleanedJsons.Add(result.CleanedDataJson ?? "{}");
                }
                else
                {
                    errors++;
                    record.Status = (CollectionRecordStatus)6;
                }
            }
            catch (Exception ex)
            {
                errors++;
                record.ErrorMessage = $"重新应用规则失败: {ex.Message}";
                record.Status = (CollectionRecordStatus)6;
                record.UpdatedAt = DateTime.UtcNow;
            }
        }

        var enrichedJsons = _smartDataCleaner.DetectBatchAnomalies(cleanedJsons, source.TransformConfig);
        for (int i = 0; i < cleanedRecords.Count; i++)
            cleanedRecords[i].CleanedDataJson = enrichedJsons[i];

        return (updated, errors);
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
            var results = await _executionService.ExecuteAsync(source.BaseUrl, source.CollectionMode.ToString(), source.AuthType.ToString(), source.AuthConfig, source.ExportConfig, source.TransformConfig);

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

            // 自动提交
            if (source.AutoSubmit) task.IsSubmitted = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "采集执行失败, SourceId={SourceId}", sourceId);
            task.Status = CollectionTaskStatus.失败; task.CompletedAt = DateTime.UtcNow; task.ErrorMessage = ex.Message;
            source.LastCollectionAt = DateTime.UtcNow;
            source.LastCollectionStatus = ex.Message.Contains("401") || ex.Message.Contains("Unauthorized") || ex.Message.Contains("未授权") || ex.Message.Contains("tokenVersion expire")
                ? "Token已过期" : "失败";
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

    private static Dictionary<string, string> ParseRawRow(string rawDataJson)
    {
        var row = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(rawDataJson)) return row;

        using var doc = JsonDocument.Parse(rawDataJson);
        if (doc.RootElement.ValueKind != JsonValueKind.Object) return row;

        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            row[prop.Name] = prop.Value.ValueKind switch
            {
                JsonValueKind.String => prop.Value.GetString() ?? "",
                JsonValueKind.Null => "",
                JsonValueKind.Undefined => "",
                _ => prop.Value.GetRawText(),
            };
        }

        return row;
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
                if (IsExcludedFromSummary(record.CleanedDataJson))
                    continue;

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

    private static bool IsExcludedFromSummary(string? cleanedDataJson)
    {
        if (string.IsNullOrWhiteSpace(cleanedDataJson)) return false;
        try
        {
            using var doc = JsonDocument.Parse(cleanedDataJson);
            if (!doc.RootElement.TryGetProperty("_excludedFromSummary", out var excluded))
                return false;

            return excluded.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.String => bool.TryParse(excluded.GetString(), out var b) && b,
                JsonValueKind.Number => excluded.TryGetInt32(out var n) && n != 0,
                _ => false,
            };
        }
        catch { return false; }
    }

    private static string GetString(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var value)) continue;
            var text = value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null,
            };
            if (!string.IsNullOrWhiteSpace(text)) return text.Trim();
        }
        return "";
    }

    private static decimal GetDecimal(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value)) return 0m;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number)) return number;
        if (value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(), out var parsed)) return parsed;
        return 0m;
    }

    private static DateTime? GetDateTime(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value)) return null;
        if (value.ValueKind == JsonValueKind.String && DateTime.TryParse(value.GetString(), out var parsed)) return parsed;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var ticks))
        {
            try { return new DateTime(ticks); } catch { return null; }
        }
        return null;
    }

    private static string FormatDateOnly(DateTime date)
        => date.ToString("yyyy-MM-dd");

    private static string FirstNonEmpty(params string[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? "";

    private static decimal FirstNonZero(params decimal[] values)
        => values.FirstOrDefault(v => v != 0m);

    private static string DistributionSyncKey(
        Guid drugId,
        string batchNo,
        Guid? fromCustomerId,
        Guid? toCustomerId,
        decimal quantity,
        string? deliveryOrderNo,
        DateTime distributionDate)
        => string.Join("|",
            drugId,
            batchNo.Trim(),
            fromCustomerId?.ToString() ?? "",
            toCustomerId?.ToString() ?? "",
            quantity.ToString("0.####"),
            deliveryOrderNo?.Trim() ?? "",
            distributionDate.ToString("yyyy-MM-dd HH:mm:ss"));

    private static Drug FindOrCreateDrug(
        JsonElement root,
        string drugName,
        string specification,
        List<Drug> drugs,
        HashSet<string> usedCodes,
        SyncSummaryResultDto result)
    {
        var drugCode = GetString(root, "DrugCode");
        var drug = !string.IsNullOrWhiteSpace(drugCode)
            ? drugs.FirstOrDefault(d => d.Code.Equals(drugCode, StringComparison.OrdinalIgnoreCase))
            : null;
        drug ??= drugs.FirstOrDefault(d =>
            d.Name.Equals(drugName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(d.Specification ?? "", specification ?? "", StringComparison.OrdinalIgnoreCase));
        drug ??= drugs.FirstOrDefault(d => d.Name.Equals(drugName, StringComparison.OrdinalIgnoreCase));

        if (drug != null)
        {
            var changed = false;
            var manufacturer = GetString(root, "Manufacturer");
            var dosageForm = GetString(root, "DosageForm");
            var unit = GetString(root, "Unit");
            if (string.IsNullOrWhiteSpace(drug.Specification) && !string.IsNullOrWhiteSpace(specification)) { drug.Specification = specification; changed = true; }
            if (string.IsNullOrWhiteSpace(drug.Manufacturer) && !string.IsNullOrWhiteSpace(manufacturer)) { drug.Manufacturer = manufacturer; changed = true; }
            if (string.IsNullOrWhiteSpace(drug.DosageForm) && !string.IsNullOrWhiteSpace(dosageForm)) { drug.DosageForm = dosageForm; changed = true; }
            if ((string.IsNullOrWhiteSpace(drug.Unit) || drug.Unit == "盒") && !string.IsNullOrWhiteSpace(unit)) { drug.Unit = unit; changed = true; }
            if (changed) drug.UpdatedAt = DateTime.UtcNow;
            return drug;
        }

        var code = drugCode;
        if (string.IsNullOrWhiteSpace(code) || usedCodes.Contains(code))
            code = NextDrugCode(usedCodes);
        else
            usedCodes.Add(code);

        drug = new Drug
        {
            Code = code,
            Name = drugName,
            Specification = specification,
            Manufacturer = GetString(root, "Manufacturer"),
            DosageForm = GetString(root, "DosageForm"),
            Unit = FirstNonEmpty(GetString(root, "Unit"), "盒"),
        };
        drugs.Add(drug);
        result.CreatedDrugs++;
        return drug;
    }

    private static Customer FindOrCreateSourceDealer(
        JsonElement root,
        string name,
        string? preferredCode,
        List<Customer> customers,
        HashSet<string> usedCodes,
        SyncSummaryResultDto result)
        => FindOrCreateCustomer(root, name, preferredCode, customers, usedCodes, result, CustomerType.商业, preferDealer: true, convertExistingToTargetType: true);

    private static Customer FindOrCreateDestinationCustomer(
        JsonElement root,
        string name,
        string? preferredCode,
        List<Customer> customers,
        HashSet<string> usedCodes,
        SyncSummaryResultDto result)
        => FindOrCreateCustomer(root, name, preferredCode, customers, usedCodes, result, CustomerType.医院, preferDealer: true, convertExistingToTargetType: false);

    private static Customer FindOrCreateCustomer(
        JsonElement root,
        string name,
        string? preferredCode,
        List<Customer> customers,
        HashSet<string> usedCodes,
        SyncSummaryResultDto result,
        CustomerType createType,
        bool preferDealer,
        bool convertExistingToTargetType)
    {
        var code = preferredCode?.Trim() ?? "";
        Customer? customer = null;

        if (preferDealer)
        {
            customer = !string.IsNullOrWhiteSpace(code)
                ? customers.FirstOrDefault(c => c.CustomerType == CustomerType.商业 && c.Code.Equals(code, StringComparison.OrdinalIgnoreCase))
                : null;
            customer ??= customers.FirstOrDefault(c => c.CustomerType == CustomerType.商业 && c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        customer ??= !string.IsNullOrWhiteSpace(code)
            ? customers.FirstOrDefault(c => c.Code.Equals(code, StringComparison.OrdinalIgnoreCase))
            : null;
        customer ??= customers.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (customer != null)
        {
            var changed = false;
            var shortName = GetString(root, "CustomerShortName");
            var province = GetString(root, "Province");
            var salesPerson = GetString(root, "SalesPerson");
            var drugName = GetString(root, "DrugName");
            if (string.IsNullOrWhiteSpace(customer.ShortName) && !string.IsNullOrWhiteSpace(shortName)) { customer.ShortName = shortName; changed = true; }
            if (string.IsNullOrWhiteSpace(customer.Province) && !string.IsNullOrWhiteSpace(province)) { customer.Province = province; changed = true; }
            if (string.IsNullOrWhiteSpace(customer.SalesPerson) && !string.IsNullOrWhiteSpace(salesPerson)) { customer.SalesPerson = salesPerson; changed = true; }
            if (string.IsNullOrWhiteSpace(customer.DrugName) && !string.IsNullOrWhiteSpace(drugName)) { customer.DrugName = drugName; changed = true; }
            if (convertExistingToTargetType && customer.CustomerType != createType) { customer.CustomerType = createType; changed = true; }
            if (changed) customer.UpdatedAt = DateTime.UtcNow;
            return customer;
        }

        if (string.IsNullOrWhiteSpace(code) || usedCodes.Contains(code))
            code = NextCustomerCode(usedCodes);
        else
            usedCodes.Add(code);

        customer = new Customer
        {
            Code = code,
            Name = name,
            ShortName = GetString(root, "CustomerShortName"),
            Province = GetString(root, "Province"),
            SalesPerson = GetString(root, "SalesPerson"),
            DrugName = GetString(root, "DrugName"),
            CustomerType = createType,
        };
        customers.Add(customer);
        result.CreatedCustomers++;
        return customer;
    }

    private static string NextDrugCode(HashSet<string> usedCodes)
    {
        var seq = 1;
        string code;
        do { code = "DRUG" + DateTime.Now.ToString("yyyyMMddHHmmss") + (seq++).ToString("D4"); }
        while (!usedCodes.Add(code));
        return code;
    }

    private static string NextCustomerCode(HashSet<string> usedCodes)
    {
        var seq = 1;
        var prefix = "CUST" + DateTime.UtcNow.ToString("yyyyMMdd");
        string code;
        do { code = prefix + (seq++).ToString("D3"); }
        while (!usedCodes.Add(code));
        return code;
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

        var distributionDate = root.TryGetProperty("DistributionDate", out var dd) && dd.TryGetDateTime(out var dt) ? dt : DateTime.UtcNow;
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
            DistributionDate = distributionDate,
            DistributionDateDisplay = FormatDateOnly(distributionDate),
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
        CollectionMode = s.CollectionMode.ToString(),
        AuthConfig = s.AuthConfig,
        ExportConfig = s.ExportConfig,
        TransformConfig = s.TransformConfig,
        IsEnabled = s.IsEnabled,
        ScheduleCron = s.ScheduleCron,
        RuleId = s.RuleId,
        RuleName = s.Rule?.Name,
        AutoSubmit = s.AutoSubmit,
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
