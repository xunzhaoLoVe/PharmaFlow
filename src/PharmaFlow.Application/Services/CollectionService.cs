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
    private readonly IDistributionService _distributionService;
    private readonly ILogger<CollectionService> _logger;
    private const string SummarySyncOperator = "采集汇总同步";
    private static readonly (string FullName, string[] Aliases)[] ProvinceAliases =
    {
        ("北京市", new[] { "北京" }),
        ("天津市", new[] { "天津" }),
        ("上海市", new[] { "上海" }),
        ("重庆市", new[] { "重庆" }),
        ("河北省", new[] { "河北" }),
        ("山西省", new[] { "山西" }),
        ("辽宁省", new[] { "辽宁" }),
        ("吉林省", new[] { "吉林" }),
        ("黑龙江省", new[] { "黑龙江" }),
        ("江苏省", new[] { "江苏" }),
        ("浙江省", new[] { "浙江" }),
        ("安徽省", new[] { "安徽" }),
        ("福建省", new[] { "福建" }),
        ("江西省", new[] { "江西" }),
        ("山东省", new[] { "山东" }),
        ("河南省", new[] { "河南" }),
        ("湖北省", new[] { "湖北" }),
        ("湖南省", new[] { "湖南" }),
        ("广东省", new[] { "广东" }),
        ("海南省", new[] { "海南" }),
        ("四川省", new[] { "四川" }),
        ("贵州省", new[] { "贵州" }),
        ("云南省", new[] { "云南" }),
        ("陕西省", new[] { "陕西" }),
        ("甘肃省", new[] { "甘肃" }),
        ("青海省", new[] { "青海" }),
        ("台湾省", new[] { "台湾" }),
        ("内蒙古自治区", new[] { "内蒙古" }),
        ("广西壮族自治区", new[] { "广西" }),
        ("西藏自治区", new[] { "西藏" }),
        ("宁夏回族自治区", new[] { "宁夏" }),
        ("新疆维吾尔自治区", new[] { "新疆" }),
        ("香港特别行政区", new[] { "香港" }),
        ("澳门特别行政区", new[] { "澳门" }),
    };

    public CollectionService(
        AppDbContext db,
        CollectionExecutionService executionService,
        SmartDataCleaner smartDataCleaner,
        IDistributionService distributionService,
        ILogger<CollectionService> logger)
    {
        _db = db;
        _executionService = executionService;
        _smartDataCleaner = smartDataCleaner;
        _distributionService = distributionService;
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
            await RemoveBusinessDataForSubmittedRecordsAsync(sourceId: id);
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
        await RemoveBusinessDataForSubmittedRecordsAsync();
        var tasks = await _db.CollectionTasks.Where(t => t.IsSubmitted).ToListAsync();
        foreach (var t in tasks) t.IsSubmitted = false;
        await _db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<DropdownItem>> GetSummarySourceDropdownAsync()
    {
        var records = await _db.CollectionRecords
            .Include(r => r.Task)
            .Include(r => r.Source)
            .Where(r => r.Task.IsSubmitted && !r.Source.IsDeleted)
            .Select(r => new
            {
                r.SourceId,
                SourceName = r.Source.Name,
                r.CleanedDataJson,
            })
            .ToListAsync();

        return records
            .Where(r => !string.IsNullOrWhiteSpace(r.CleanedDataJson) && !IsExcludedFromSummary(r.CleanedDataJson))
            .GroupBy(r => new { r.SourceId, r.SourceName })
            .OrderBy(g => g.Key.SourceName)
            .Select(g => new DropdownItem { Id = g.Key.SourceId, Name = g.Key.SourceName })
            .ToList();
    }

    public async Task<CollectionSummaryFilterOptionsDto> GetSummaryFilterOptionsAsync(Guid? sourceId = null)
    {
        var rows = await BuildSummaryRowsAsync(sourceId);
        return new CollectionSummaryFilterOptionsDto
        {
            DrugNames = BuildDistinctOptions(rows.Select(r => r.DrugName)),
            Specifications = BuildDistinctOptions(rows.Select(r => r.Specification)),
            BatchNos = BuildDistinctOptions(rows.Select(r => r.BatchNo)),
            Provinces = BuildDistinctOptions(rows.Select(r => r.Province)),
        };
    }

    public async Task<PagedResult<CollectionSummaryRecordDto>> GetSummaryPagedAsync(int pageIndex, int pageSize,
        Guid? sourceId = null, string? search = null, string? dataType = null,
        DateTime? dateFrom = null, DateTime? dateTo = null, string? syncStatus = null,
        string? sortField = null, string? sortOrder = null,
        string? sourceSearch = null, string? targetSearch = null,
        string? drugName = null, string? specification = null,
        string? batchNo = null, string? province = null)
    {
        pageIndex = Math.Max(0, pageIndex);
        pageSize = Math.Clamp(pageSize, 1, 500);

        var rows = await BuildSummaryRowsAsync(sourceId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim();
            rows = rows.Where(r => SummaryRowContains(r, keyword)).ToList();
        }

        rows = FilterContains(rows, r => r.CompanyName, sourceSearch);
        rows = FilterContains(rows, r => r.ToCustomerName, targetSearch);
        rows = FilterEquals(rows, r => r.DrugName, drugName);
        rows = FilterEquals(rows, r => r.Specification, specification);
        rows = FilterEquals(rows, r => r.BatchNo, batchNo);
        rows = FilterEquals(rows, r => r.Province, province);

        if (!string.IsNullOrWhiteSpace(dataType))
        {
            var filter = dataType.Trim();
            rows = rows
                .Where(r => string.Equals(r.DataType, filter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(syncStatus))
        {
            var filter = syncStatus.Trim();
            rows = filter.Equals("Synced", StringComparison.OrdinalIgnoreCase)
                ? rows.Where(r => r.TargetEntityId.HasValue).ToList()
                : filter.Equals("Pending", StringComparison.OrdinalIgnoreCase)
                    ? rows.Where(r => !r.TargetEntityId.HasValue).ToList()
                    : rows;
        }

        if (dateFrom.HasValue)
        {
            var from = dateFrom.Value.Date;
            rows = rows
                .Where(r => TryParseDate(r.DistributionDate, out var date) && date >= from)
                .ToList();
        }

        if (dateTo.HasValue)
        {
            var toExclusive = dateTo.Value.Date.AddDays(1);
            rows = rows
                .Where(r => TryParseDate(r.DistributionDate, out var date) && date < toExclusive)
                .ToList();
        }

        rows = SortSummaryRows(rows, sortField, sortOrder);

        var total = rows.Count;
        var items = rows.Skip(pageIndex * pageSize).Take(pageSize).ToList();

        return new PagedResult<CollectionSummaryRecordDto>
        {
            Items = items,
            TotalCount = total,
            PageIndex = pageIndex,
            PageSize = pageSize,
        };
    }

    private static List<CollectionSummaryRecordDto> SortSummaryRows(
        List<CollectionSummaryRecordDto> rows,
        string? sortField,
        string? sortOrder)
    {
        if (string.IsNullOrWhiteSpace(sortField) || string.IsNullOrWhiteSpace(sortOrder))
            return rows;

        var descending = sortOrder.Equals("descend", StringComparison.OrdinalIgnoreCase);
        return sortField switch
        {
            "sourceName" => OrderText(rows, r => r.SourceName, descending),
            "syncStatus" => OrderText(rows, r => r.SyncStatus, descending),
            "syncTarget" or "DataType" => OrderText(rows, r => r.DataType, descending),
            "DrugName" => OrderText(rows, r => r.DrugName, descending),
            "Specification" => OrderText(rows, r => r.Specification, descending),
            "BatchNo" => OrderText(rows, r => r.BatchNo, descending),
            "CompanyName" => OrderText(rows, r => r.CompanyName, descending),
            "ToCustomerName" => OrderText(rows, r => r.ToCustomerName, descending),
            "TaxedUnitPrice" => descending
                ? rows.OrderByDescending(r => r.TaxedUnitPrice ?? 0).ToList()
                : rows.OrderBy(r => r.TaxedUnitPrice ?? 0).ToList(),
            "Quantity" => descending
                ? rows.OrderByDescending(r => r.Quantity ?? 0).ToList()
                : rows.OrderBy(r => r.Quantity ?? 0).ToList(),
            "DistributionDate" => OrderDate(rows, r => r.DistributionDate, descending),
            "Province" => OrderText(rows, r => r.Province, descending),
            "ExpiryDate" => OrderDate(rows, r => r.ExpiryDate, descending),
            _ => rows,
        };
    }

    private static List<CollectionSummaryRecordDto> FilterContains(
        IEnumerable<CollectionSummaryRecordDto> rows,
        Func<CollectionSummaryRecordDto, string?> selector,
        string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return rows.ToList();

        var filter = keyword.Trim();
        return rows
            .Where(row => (selector(row) ?? "").Contains(filter, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static List<CollectionSummaryRecordDto> FilterEquals(
        IEnumerable<CollectionSummaryRecordDto> rows,
        Func<CollectionSummaryRecordDto, string?> selector,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return rows.ToList();

        var filter = value.Trim();
        return rows
            .Where(row => string.Equals((selector(row) ?? "").Trim(), filter, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private async Task<List<CollectionSummaryRecordDto>> BuildSummaryRowsAsync(Guid? sourceId = null)
    {
        var query = _db.CollectionRecords
            .Include(r => r.Task)
            .Include(r => r.Source)
            .Where(r => r.Task.IsSubmitted)
            .AsQueryable();

        if (sourceId.HasValue)
            query = query.Where(r => r.SourceId == sourceId.Value);

        var records = await query.OrderByDescending(r => r.CreatedAt).ToListAsync();
        var seenRawRows = new HashSet<string>(StringComparer.Ordinal);
        var rows = new List<CollectionSummaryRecordDto>();

        foreach (var record in records)
        {
            if (!seenRawRows.Add(record.RawDataJson))
                continue;

            var row = MapSummaryRecord(record);
            if (row == null)
                continue;

            rows.Add(row);
        }

        return rows;
    }

    private static List<string> BuildDistinctOptions(IEnumerable<string?> values)
        => values
            .Select(v => v?.Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
            .Select(v => v!)
            .ToList();

    private static List<CollectionSummaryRecordDto> OrderText(
        IEnumerable<CollectionSummaryRecordDto> rows,
        Func<CollectionSummaryRecordDto, string?> selector,
        bool descending)
        => descending
            ? rows.OrderByDescending(selector, StringComparer.OrdinalIgnoreCase).ToList()
            : rows.OrderBy(selector, StringComparer.OrdinalIgnoreCase).ToList();

    private static List<CollectionSummaryRecordDto> OrderDate(
        IEnumerable<CollectionSummaryRecordDto> rows,
        Func<CollectionSummaryRecordDto, string?> selector,
        bool descending)
        => descending
            ? rows.OrderByDescending(r => TryParseDate(selector(r), out var date) ? date : DateTime.MinValue).ToList()
            : rows.OrderBy(r => TryParseDate(selector(r), out var date) ? date : DateTime.MinValue).ToList();

    public async Task<SyncSummaryResultDto> SyncSummaryAsync(Guid? sourceId = null)
    {
        await RemoveBusinessDataForSubmittedRecordsAsync(sourceId: sourceId, removeCreatedMasterData: false);
        await _db.SaveChangesAsync();
        return await SyncSubmittedRecordsAsync(sourceId: sourceId);
    }

    private async Task<SyncSummaryResultDto> SyncSubmittedRecordsAsync(Guid? sourceId = null, Guid? taskId = null)
    {
        var query = _db.CollectionRecords
            .Include(r => r.Task)
            .Include(r => r.Source)
            .Where(r => r.Task.IsSubmitted)
            .AsQueryable();

        if (sourceId.HasValue)
            query = query.Where(r => r.SourceId == sourceId.Value);
        if (taskId.HasValue)
            query = query.Where(r => r.TaskId == taskId.Value);

        var records = await query.OrderBy(r => r.CreatedAt).ToListAsync();
        var result = new SyncSummaryResultDto { TotalRecords = records.Count };

        var suppliers = await _db.Suppliers.Where(s => !s.IsDeleted).ToListAsync();
        var supplierCodes = new HashSet<string>(await _db.Suppliers.IgnoreQueryFilters().Select(s => s.Code).ToListAsync(), StringComparer.OrdinalIgnoreCase);
        var createdDrugCountBefore = await _db.Drugs.CountAsync(d => !d.IsDeleted);
        var createdCustomerCountBefore = await _db.Customers.CountAsync(c => !c.IsDeleted);
        var flowRows = new List<(CollectionRecord Record, DownstreamRecordDto Dto, DateTime DistributionDate)>();

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
                var dataType = FirstNonEmpty(GetString(root, "DataType"), GetSourceType(record.Source?.TransformConfig));
                var isPurchaseFlow = IsPurchaseFlow(dataType);
                var fromName = FirstNonEmpty(
                    GetString(root, "FromCustomerName"),
                    GetString(root, "CompanyName"),
                    GetString(root, "SupplierShortName"),
                    GetString(root, "OriginalSupplierShortName"));
                var toName = FirstNonEmpty(
                    GetString(root, "ToCustomerName"),
                    GetString(root, "CustomerName"),
                    GetString(root, "CustomerShortName"));

                Supplier? supplier = null;
                if (isPurchaseFlow)
                {
                    supplier = FindOrCreateSupplier(root, FirstNonEmpty(fromName, "江苏神龙药业有限公司"), suppliers, supplierCodes);
                    if (_db.Entry(supplier).State == EntityState.Detached)
                    {
                        _db.Suppliers.Add(supplier);
                        RecordCreatedEntity(record, "Supplier", supplier.Id);
                    }
                }

                if ((!isPurchaseFlow && string.IsNullOrWhiteSpace(fromName)) || string.IsNullOrWhiteSpace(toName) || (isPurchaseFlow && supplier == null))
                {
                    result.SkippedRecords++;
                    continue;
                }

                var batchNo = FirstNonEmpty(GetString(root, "BatchNo"), "-");
                var quantity = isPurchaseFlow
                    ? FirstNonZero(GetDecimal(root, "PurchaseQuantity"), GetDecimal(root, "Quantity"), GetDecimal(root, "SalesQuantity"))
                    : FirstNonZero(GetDecimal(root, "SalesQuantity"), GetDecimal(root, "Quantity"), GetDecimal(root, "PurchaseQuantity"));
                if (quantity == 0)
                {
                    result.SkippedRecords++;
                    continue;
                }

                var distributionDate = GetDateTime(root, "DistributionDate") ?? DateTime.UtcNow;
                var distributionDateDisplay = FormatDateOnly(distributionDate);
                var syncedDistributionDate = DateTime.TryParse(distributionDateDisplay, out var parsedDistributionDate)
                    ? parsedDistributionDate
                    : distributionDate;
                var deliveryOrderNo = GetString(root, "DeliveryOrderNo");

                flowRows.Add((record, new DownstreamRecordDto
                {
                    DrugName = drugName,
                    Specification = specification,
                    BatchNo = batchNo,
                    SupplierId = isPurchaseFlow ? supplier?.Id : null,
                    FromCustomerName = isPurchaseFlow ? string.Empty : fromName,
                    ToCustomerName = toName,
                    Quantity = quantity,
                    SalesPerson = FirstNonEmpty(GetString(root, "SalesPerson"), GetString(root, "Operator")),
                    DeliveryOrderNo = deliveryOrderNo,
                    TaxedUnitPrice = GetDecimal(root, "TaxedUnitPrice"),
                    DistributionDate = syncedDistributionDate,
                    DistributionDateDisplay = distributionDateDisplay,
                    Province = GetProvince(root),
                }, syncedDistributionDate));
            }
            catch
            {
                result.SkippedRecords++;
            }
        }

        if (flowRows.Count > 0)
        {
            var importResult = await _distributionService.RecordDownstreamBatchAsync(
                flowRows.Select(r => r.Dto).ToList(),
                SummarySyncOperator,
                deduplicateWithinBatch: false);
            result.CreatedDistributionRecords += importResult.Imported;
            result.SkippedRecords += importResult.Skipped;

            var drugIds = flowRows.Select(r => r.Dto.DrugId).Where(id => id != Guid.Empty).Distinct().ToList();
            var batchNos = flowRows.Select(r => r.Dto.BatchNo).Distinct().ToList();
            var distributions = await _db.DistributionRecords
                .Where(d => drugIds.Contains(d.DrugId) && batchNos.Contains(d.BatchNo))
                .Select(d => new
                {
                    d.Id,
                    d.DrugId,
                    d.BatchNo,
                    d.SupplierId,
                    d.FromCustomerId,
                    d.ToCustomerId,
                    d.Quantity,
                    d.DeliveryOrderNo,
                    d.DistributionDate,
                    d.Province
                })
                .ToListAsync();
            var distributionByKey = distributions
                .GroupBy(d => DistributionSyncKey(d.DrugId, d.BatchNo, d.SupplierId, d.FromCustomerId, d.ToCustomerId, d.Quantity, d.DeliveryOrderNo, d.DistributionDate, d.Province), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => new Queue<Guid>(g.Select(d => d.Id)), StringComparer.OrdinalIgnoreCase);

            foreach (var (record, dto, distributionDate) in flowRows)
            {
                var hasSupplier = dto.SupplierId.HasValue && dto.SupplierId != Guid.Empty;
                var key = DistributionSyncKey(dto.DrugId, dto.BatchNo, hasSupplier ? dto.SupplierId : null, hasSupplier ? null : dto.FromCustomerId, dto.ToCustomerId, dto.Quantity, dto.DeliveryOrderNo, distributionDate, dto.Province);
                if (!distributionByKey.TryGetValue(key, out var distributionIds) || !distributionIds.TryDequeue(out var distributionId))
                    continue;

                record.TargetEntityType = "DistributionRecord";
                record.TargetEntityId = distributionId;
                record.UpdatedAt = DateTime.UtcNow;
            }

            result.CreatedDrugs = Math.Max(0, await _db.Drugs.CountAsync(d => !d.IsDeleted) - createdDrugCountBefore);
            result.CreatedCustomers = Math.Max(0, await _db.Customers.CountAsync(c => !c.IsDeleted) - createdCustomerCountBefore);
        }

        await _db.SaveChangesAsync();
        return result;
    }

    private void RecordCreatedEntity(CollectionRecord record, string entityType, Guid entityId)
    {
        _db.CollectionSyncEntities.Add(new CollectionSyncEntity
        {
            EntityType = entityType,
            EntityId = entityId,
            SourceId = record.SourceId,
            TaskId = record.TaskId,
        });
    }

    private async Task RemoveBusinessDataForSubmittedRecordsAsync(Guid? sourceId = null, Guid? taskId = null, bool removeCreatedMasterData = true)
    {
        var query = _db.CollectionRecords
            .Include(r => r.Task)
            .Include(r => r.Source)
            .Where(r => r.Task.IsSubmitted)
            .AsQueryable();

        if (sourceId.HasValue)
            query = query.Where(r => r.SourceId == sourceId.Value);
        if (taskId.HasValue)
            query = query.Where(r => r.TaskId == taskId.Value);

        var records = await query.ToListAsync();
        if (records.Count == 0)
            return;

        var distributionIds = records
            .Where(r => r.TargetEntityType == "DistributionRecord" && r.TargetEntityId.HasValue)
            .Select(r => r.TargetEntityId!.Value)
            .Distinct()
            .ToList();

        var fallbackDistributionIds = await FindSyncedDistributionIdsForCollectionRecordsAsync(records);
        distributionIds = distributionIds
            .Concat(fallbackDistributionIds)
            .Distinct()
            .ToList();

        var removedIds = distributionIds.ToHashSet();
        var distributions = await _db.DistributionRecords
            .Where(d => removedIds.Contains(d.Id) && d.Operator == SummarySyncOperator)
            .ToListAsync();

        _db.DistributionRecords.RemoveRange(distributions);
        foreach (var record in records)
        {
            if (record.TargetEntityType == "DistributionRecord")
            {
                record.TargetEntityId = null;
                record.UpdatedAt = DateTime.UtcNow;
            }
        }

        if (removeCreatedMasterData)
            await RemoveCreatedMasterDataAsync(sourceId, taskId, removedIds);
    }

    /// <summary>
    /// 联动清理本次采集同步「新建」的主数据（药品/客户/供应商）。
    /// 仅当该主数据已无任何流向记录（含其它采集源、手动录入）引用时才软删除，
    /// 避免误删用户已有的主数据或其它采集源仍在使用的主数据。
    /// </summary>
    private async Task RemoveCreatedMasterDataAsync(Guid? sourceId, Guid? taskId, HashSet<Guid> removedDistributionIds)
    {
        var syncEntityQuery = _db.CollectionSyncEntities.AsQueryable();
        if (taskId.HasValue)
            syncEntityQuery = syncEntityQuery.Where(e => e.TaskId == taskId.Value);
        else if (sourceId.HasValue)
            syncEntityQuery = syncEntityQuery.Where(e => e.SourceId == sourceId.Value);

        var syncEntities = await syncEntityQuery.ToListAsync();
        if (syncEntities.Count == 0)
            return;

        foreach (var se in syncEntities)
        {
            var referenced = se.EntityType switch
            {
                "Drug" => await _db.DistributionRecords
                    .AnyAsync(d => d.DrugId == se.EntityId && !removedDistributionIds.Contains(d.Id)),
                "Supplier" => await _db.DistributionRecords
                    .AnyAsync(d => d.SupplierId == se.EntityId && !removedDistributionIds.Contains(d.Id)),
                "Customer" => await _db.DistributionRecords
                    .AnyAsync(d => (d.FromCustomerId == se.EntityId || d.ToCustomerId == se.EntityId)
                        && !removedDistributionIds.Contains(d.Id)),
                _ => true,
            };

            if (referenced)
                continue;

            switch (se.EntityType)
            {
                case "Drug":
                    var drug = await _db.Drugs.IgnoreQueryFilters().FirstOrDefaultAsync(d => d.Id == se.EntityId);
                    if (drug != null && !drug.IsDeleted) { drug.IsDeleted = true; drug.UpdatedAt = DateTime.UtcNow; }
                    break;
                case "Customer":
                    var customer = await _db.Customers.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == se.EntityId);
                    if (customer != null && !customer.IsDeleted) { customer.IsDeleted = true; customer.UpdatedAt = DateTime.UtcNow; }
                    break;
                case "Supplier":
                    var supplier = await _db.Suppliers.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == se.EntityId);
                    if (supplier != null && !supplier.IsDeleted) { supplier.IsDeleted = true; supplier.UpdatedAt = DateTime.UtcNow; }
                    break;
            }
        }

        _db.CollectionSyncEntities.RemoveRange(syncEntities);
    }

    private async Task<List<Guid>> FindSyncedDistributionIdsForCollectionRecordsAsync(List<CollectionRecord> records)
    {
        var drugs = await _db.Drugs.Where(d => !d.IsDeleted).ToListAsync();
        var customers = await _db.Customers.Where(c => !c.IsDeleted).ToListAsync();
        var suppliers = await _db.Suppliers.Where(s => !s.IsDeleted).ToListAsync();

        var syncedDistributions = await _db.DistributionRecords
            .Where(d => d.Operator == SummarySyncOperator)
            .Select(d => new
            {
                d.Id,
                d.DrugId,
                d.BatchNo,
                d.SupplierId,
                d.FromCustomerId,
                d.ToCustomerId,
                d.Quantity,
                d.DeliveryOrderNo,
                d.DistributionDate,
                d.Province
            })
            .ToListAsync();

        var distributionByKey = syncedDistributions
            .GroupBy(d => DistributionSyncKey(d.DrugId, d.BatchNo, d.SupplierId, d.FromCustomerId, d.ToCustomerId, d.Quantity, d.DeliveryOrderNo, d.DistributionDate, d.Province), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(d => d.Id).ToList(), StringComparer.OrdinalIgnoreCase);

        var ids = new List<Guid>();
        foreach (var record in records)
        {
            var key = TryBuildDistributionSyncKey(record, drugs, customers, suppliers);
            if (key != null && distributionByKey.TryGetValue(key, out var distributionIds))
                ids.AddRange(distributionIds);
        }

        return ids;
    }

    private static string? TryBuildDistributionSyncKey(
        CollectionRecord record,
        List<Drug> drugs,
        List<Customer> customers,
        List<Supplier> suppliers)
    {
        if (string.IsNullOrWhiteSpace(record.CleanedDataJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(record.CleanedDataJson);
            var root = doc.RootElement;
            var drugName = GetString(root, "DrugName");
            if (string.IsNullOrWhiteSpace(drugName))
                return null;

            var specification = GetString(root, "Specification");
            var drug = FindExistingDrug(root, drugName, specification, drugs);
            if (drug == null)
                return null;

            var dataType = FirstNonEmpty(GetString(root, "DataType"), GetSourceType(record.Source?.TransformConfig));
            var isPurchaseFlow = IsPurchaseFlow(dataType);
            var fromName = FirstNonEmpty(
                GetString(root, "FromCustomerName"),
                GetString(root, "CompanyName"),
                GetString(root, "SupplierShortName"),
                GetString(root, "OriginalSupplierShortName"));
            var toName = FirstNonEmpty(
                GetString(root, "ToCustomerName"),
                GetString(root, "CustomerName"),
                GetString(root, "CustomerShortName"));

            Supplier? supplier = null;
            Customer? fromCustomer = null;
            if (isPurchaseFlow)
                supplier = FindExistingSupplier(root, FirstNonEmpty(fromName, "江苏神龙药业有限公司"), suppliers);
            else if (!string.IsNullOrWhiteSpace(fromName))
                fromCustomer = FindUniqueCustomerByName(customers.Where(c => c.CustomerType == CustomerType.商业), fromName);

            var toCustomer = !string.IsNullOrWhiteSpace(toName)
                ? FindExistingCustomer(toName, GetString(root, "CustomerCode"), customers, preferDealer: isPurchaseFlow)
                : null;

            if ((!isPurchaseFlow && fromCustomer == null) || toCustomer == null || (isPurchaseFlow && supplier == null))
                return null;

            var batchNo = FirstNonEmpty(GetString(root, "BatchNo"), "-");
            var quantity = isPurchaseFlow
                ? FirstNonZero(GetDecimal(root, "PurchaseQuantity"), GetDecimal(root, "Quantity"), GetDecimal(root, "SalesQuantity"))
                : FirstNonZero(GetDecimal(root, "SalesQuantity"), GetDecimal(root, "Quantity"), GetDecimal(root, "PurchaseQuantity"));
            if (quantity == 0)
                return null;

            var distributionDate = GetDateTime(root, "DistributionDate") ?? DateTime.UtcNow;
            var deliveryOrderNo = GetString(root, "DeliveryOrderNo");
            return DistributionSyncKey(drug.Id, batchNo, supplier?.Id, fromCustomer?.Id, toCustomer.Id, quantity, deliveryOrderNo, distributionDate, GetProvince(root));
        }
        catch
        {
            return null;
        }
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
        await RemoveBusinessDataForSubmittedRecordsAsync(taskId: id, removeCreatedMasterData: false);
        await _db.SaveChangesAsync();
        await SyncSubmittedRecordsAsync(taskId: id);
    }

    public async Task UnsubmitTaskAsync(Guid id)
    {
        var task = await _db.CollectionTasks.FindAsync(id);
        if (task != null)
        {
            await RemoveBusinessDataForSubmittedRecordsAsync(taskId: id);
            task.IsSubmitted = false;
            await _db.SaveChangesAsync();
        }
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
        if (await _db.CollectionTasks.AnyAsync(t => t.SourceId == sourceId && t.IsSubmitted))
        {
            await RemoveBusinessDataForSubmittedRecordsAsync(sourceId: sourceId, removeCreatedMasterData: false);
            await _db.SaveChangesAsync();
            await SyncSubmittedRecordsAsync(sourceId: sourceId);
        }

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
        await RemoveBusinessDataForSubmittedRecordsAsync(taskId: id);
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
        if (task.Status == CollectionTaskStatus.已完成 && task.IsSubmitted)
            await SyncSubmittedRecordsAsync(taskId: task.Id);

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

    private static CollectionSummaryRecordDto? MapSummaryRecord(CollectionRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.CleanedDataJson) || IsExcludedFromSummary(record.CleanedDataJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(record.CleanedDataJson);
            var root = doc.RootElement;
            var distributionDate = GetDateTime(root, "DistributionDate");
            var expiryDate = GetDateTime(root, "ExpiryDate");
            var sourceType = GetSourceType(record.Source?.TransformConfig);
            var cleanedDataType = GetString(root, "DataType");
            var quantity = FirstNonZero(
                GetDecimal(root, "SalesQuantity"),
                GetDecimal(root, "Quantity"),
                GetDecimal(root, "PurchaseQuantity"));

            return new CollectionSummaryRecordDto
            {
                Id = record.Id,
                TaskId = record.TaskId,
                SourceId = record.SourceId,
                SourceName = record.Source?.Name ?? "",
                DataType = FirstNonEmpty(cleanedDataType, sourceType),
                DrugName = GetString(root, "DrugName"),
                Specification = GetString(root, "Specification"),
                BatchNo = GetString(root, "BatchNo"),
                CompanyName = GetString(root, "CompanyName", "FromCustomerName", "SupplierShortName", "OriginalSupplierShortName"),
                ToCustomerName = GetString(root, "ToCustomerName", "CustomerName", "CustomerShortName"),
                TaxedUnitPrice = GetDecimal(root, "TaxedUnitPrice"),
                Quantity = quantity,
                DistributionDate = distributionDate.HasValue ? FormatDateOnly(distributionDate.Value) : GetString(root, "DistributionDate"),
                Province = GetProvince(root),
                ExpiryDate = expiryDate.HasValue ? FormatDateOnly(expiryDate.Value) : GetString(root, "ExpiryDate"),
                TargetEntityId = record.TargetEntityId,
                SyncStatus = record.TargetEntityId.HasValue ? "Synced" : "Pending",
                CreatedAt = record.CreatedAt,
            };
        }
        catch
        {
            return null;
        }
    }

    private static bool SummaryRowContains(CollectionSummaryRecordDto row, string keyword)
    {
        var values = new[]
        {
            row.SourceName,
            row.DataType,
            row.DrugName,
            row.Specification,
            row.BatchNo,
            row.CompanyName,
            row.ToCustomerName,
            row.Province,
            row.DistributionDate,
            row.ExpiryDate,
            row.TaxedUnitPrice?.ToString(),
            row.Quantity?.ToString(),
        };

        return values.Any(v => !string.IsNullOrWhiteSpace(v)
            && v.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetSourceType(string? transformConfig)
    {
        if (string.IsNullOrWhiteSpace(transformConfig)) return "";
        try
        {
            using var doc = JsonDocument.Parse(transformConfig);
            if (doc.RootElement.TryGetProperty("sourceType", out var sourceType))
                return sourceType.GetString()?.Trim() ?? "";
        }
        catch { }
        return "";
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

    private static string GetProvince(JsonElement root)
        => NormalizeProvinceName(GetString(root, "Province", "Area", "Region", "RegionName", "CustomerAddress", "Address"));

    private static string NormalizeProvinceName(string? value)
    {
        var text = string.Concat((value ?? "").Where(c => !char.IsWhiteSpace(c))).Trim();
        if (string.IsNullOrWhiteSpace(text) || text == "未知")
            return "";

        foreach (var (fullName, _) in ProvinceAliases)
        {
            if (text.Contains(fullName, StringComparison.OrdinalIgnoreCase))
                return fullName;
        }

        foreach (var (fullName, aliases) in ProvinceAliases)
        {
            if (aliases.Any(alias => text.StartsWith(alias, StringComparison.OrdinalIgnoreCase)))
                return fullName;
        }

        return text;
    }

    private static decimal GetDecimal(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value)) return 0m;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number)) return number;
        if (value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(), out var parsed)) return parsed;
        return 0m;
    }

    private static bool TryParseDate(string? value, out DateTime date)
    {
        if (!string.IsNullOrWhiteSpace(value) && DateTime.TryParse(value, out date))
            return true;

        date = default;
        return false;
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

    private static bool IsPurchaseFlow(string dataType)
        => dataType.Contains("采购", StringComparison.OrdinalIgnoreCase);

    private static string DistributionSyncKey(
        Guid drugId,
        string batchNo,
        Guid? supplierId,
        Guid? fromCustomerId,
        Guid? toCustomerId,
        decimal quantity,
        string? deliveryOrderNo,
        DateTime distributionDate,
        string? province = null)
        => MasterDataMatchHelper.DistributionRecordKey(
            drugId,
            batchNo,
            supplierId,
            fromCustomerId,
            toCustomerId,
            quantity,
            deliveryOrderNo,
            distributionDate,
            province);

    private static Drug FindOrCreateDrug(
        JsonElement root,
        string drugName,
        string specification,
        List<Drug> drugs,
        HashSet<string> usedCodes,
        SyncSummaryResultDto result)
    {
        var drugCode = GetString(root, "DrugCode");
        var drug = drugs.FirstOrDefault(d => SameDrugIdentity(d, drugName, specification));

        if (drug == null && string.IsNullOrWhiteSpace(specification))
        {
            drug = !string.IsNullOrWhiteSpace(drugCode)
                ? drugs.FirstOrDefault(d =>
                    d.Code.Equals(drugCode, StringComparison.OrdinalIgnoreCase)
                    && SameText(d.Name, drugName))
                : null;
            drug ??= drugs.FirstOrDefault(d => SameText(d.Name, drugName) && string.IsNullOrWhiteSpace(d.Specification));
        }

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

    private static Drug? FindExistingDrug(JsonElement root, string drugName, string specification, List<Drug> drugs)
    {
        var drugCode = GetString(root, "DrugCode");
        var drug = drugs.FirstOrDefault(d => SameDrugIdentity(d, drugName, specification));

        if (drug == null && string.IsNullOrWhiteSpace(specification))
        {
            drug = !string.IsNullOrWhiteSpace(drugCode)
                ? drugs.FirstOrDefault(d =>
                    d.Code.Equals(drugCode, StringComparison.OrdinalIgnoreCase)
                    && SameText(d.Name, drugName))
                : null;
            drug ??= drugs.FirstOrDefault(d => SameText(d.Name, drugName) && string.IsNullOrWhiteSpace(d.Specification));
        }

        return drug;
    }

    private static bool SameDrugIdentity(Drug drug, string name, string specification)
        => MasterDataMatchHelper.DrugKey(drug.Name, drug.Specification) == MasterDataMatchHelper.DrugKey(name, specification);

    private static bool SameText(string? left, string? right)
        => MasterDataMatchHelper.SameText(left, right);

    private static string NormalizeLookupText(string? value)
        => MasterDataMatchHelper.NormalizeText(value);

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
    {
        var customerType = ResolveDestinationCustomerType(root, name);
        return FindOrCreateCustomer(
            root,
            name,
            preferredCode,
            customers,
            usedCodes,
            result,
            customerType,
            preferDealer: customerType == CustomerType.商业,
            convertExistingToTargetType: false);
    }

    private static Customer FindOrCreatePurchaseDealer(
        JsonElement root,
        string name,
        string? preferredCode,
        List<Customer> customers,
        HashSet<string> usedCodes,
        SyncSummaryResultDto result)
        => FindOrCreateCustomer(root, name, preferredCode, customers, usedCodes, result, CustomerType.商业, preferDealer: true, convertExistingToTargetType: true);

    private static Customer? FindCustomerByBusinessKey(
        IEnumerable<Customer> customers,
        string name,
        string? drugName,
        string? province,
        CustomerType customerType,
        DistributorRole? distributorRole,
        DistributorLevel? level)
    {
        var key = customerType == CustomerType.商业
            ? MasterDataMatchHelper.DealerCustomerKey(name, drugName, province, distributorRole, level)
            : MasterDataMatchHelper.EndCustomerKey(name, drugName, province, customerType);

        return customers.FirstOrDefault(c => customerType == CustomerType.商业
            ? MasterDataMatchHelper.DealerCustomerKey(c.Name, c.DrugName, c.Province, c.DistributorRole, c.Level) == key
            : MasterDataMatchHelper.EndCustomerKey(c.Name, c.DrugName, c.Province, c.CustomerType) == key);
    }

    private static Customer? FindUniqueCustomerByName(IEnumerable<Customer> customers, string name)
    {
        var matches = customers.Where(c => SameText(c.Name, name)).Take(2).ToList();
        return matches.Count == 1 ? matches[0] : null;
    }

    private static CustomerType ResolveDestinationCustomerType(JsonElement root, string name)
    {
        var explicitType = ResolveCustomerTypeFromText(FirstNonEmpty(
            GetString(root, "ToCustomerType"),
            GetString(root, "DestinationCustomerType"),
            GetString(root, "CustomerTypeName"),
            GetString(root, "CustomerType"),
            GetString(root, "InstitutionType"),
            GetString(root, "OrganizationType"),
            GetString(root, "机构类型"),
            GetString(root, "客户类型"),
            GetString(root, "终端类型")));
        if (explicitType.HasValue)
            return explicitType.Value;

        var distributorLevel = ResolveDistributorLevel(root, CustomerType.商业);
        var distributorRole = ResolveDistributorRole(root, CustomerType.商业);
        if (distributorLevel.HasValue || IsDealerRoleSignal(distributorRole))
            return CustomerType.商业;

        return InferCustomerTypeByName(name) ?? CustomerType.其他;
    }

    private static CustomerType? ResolveCustomerTypeFromText(string? value)
    {
        var text = NormalizeLookupText(value);
        if (string.IsNullOrWhiteSpace(text))
            return null;

        if (Enum.TryParse<CustomerType>(text, out var parsed))
            return parsed;

        if (ContainsAny(text, "医院", "医疗中心", "医学院附属", "卫生院", "卫生室", "卫生服务中心", "卫生服务站", "社区卫生", "基层医疗", "药店", "药房", "大药房", "诊所", "门诊"))
            return InferCustomerTypeByName(text) ?? CustomerType.其他;

        if (ContainsAny(text, "商业", "经销", "配送", "批发", "代理商", "商业公司", "流通"))
            return CustomerType.商业;

        if (ContainsAny(text, "终端", "终端机构", "医疗机构"))
            return null;

        return null;
    }

    private static CustomerType? InferCustomerTypeByName(string? name)
    {
        var text = NormalizeLookupText(name);
        if (string.IsNullOrWhiteSpace(text))
            return null;

        if (ContainsAny(text, "卫生院", "卫生室", "卫生服务中心", "卫生服务站", "社区卫生", "基层医疗"))
            return CustomerType.基层医疗机构;
        if (ContainsAny(text, "医院", "医疗中心", "医学院附属"))
            return CustomerType.医院;
        if (ContainsAny(text, "药店", "药房", "大药房", "连锁药"))
            return CustomerType.药店;
        if (ContainsAny(text, "诊所", "门诊"))
            return CustomerType.诊所;
        if (ContainsAny(text, "医药公司", "药业公司", "商业公司", "配送", "批发", "经销", "代理", "供应链", "流通", "物流", "有限公司", "集团"))
            return CustomerType.商业;

        return null;
    }

    private static bool IsDealerRoleSignal(DistributorRole? role)
        => role is DistributorRole.商业公司 or DistributorRole.配送公司 or DistributorRole.代理商;

    private static bool ContainsAny(string text, params string[] values)
        => values.Any(v => text.Contains(v, StringComparison.OrdinalIgnoreCase));

    private static DistributorRole? ResolveDistributorRole(JsonElement root, CustomerType customerType)
    {
        if (customerType != CustomerType.商业) return null;
        var value = FirstNonEmpty(GetString(root, "DistributorRole"), GetString(root, "CustomerRole"), GetString(root, "Role"), GetString(root, "机构角色"));
        return value switch
        {
            "商业公司" => DistributorRole.商业公司,
            "配送公司" => DistributorRole.配送公司,
            "代理商" => DistributorRole.代理商,
            "其他" => DistributorRole.其他,
            _ => Enum.TryParse<DistributorRole>(value, out var parsed) ? parsed : null,
        };
    }

    private static DistributorLevel? ResolveDistributorLevel(JsonElement root, CustomerType customerType)
    {
        if (customerType != CustomerType.商业) return null;
        var value = FirstNonEmpty(GetString(root, "DistributorLevel"), GetString(root, "CustomerLevel"), GetString(root, "Level"), GetString(root, "经销商级别"));
        return value switch
        {
            "一级经销商" => DistributorLevel.一级经销商,
            "二级经销商" => DistributorLevel.二级经销商,
            "三级经销商" => DistributorLevel.三级经销商,
            _ => Enum.TryParse<DistributorLevel>(value, out var parsed) ? parsed : null,
        };
    }

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
        var province = GetProvince(root);
        var drugName = GetString(root, "DrugName");
        var distributorRole = ResolveDistributorRole(root, createType);
        var distributorLevel = ResolveDistributorLevel(root, createType);
        var customer = FindCustomerByBusinessKey(customers, name, drugName, province, createType, distributorRole, distributorLevel);

        if (customer == null && preferDealer)
        {
            customer = !string.IsNullOrWhiteSpace(code)
                ? customers.FirstOrDefault(c => c.CustomerType == CustomerType.商业 && c.Code.Equals(code, StringComparison.OrdinalIgnoreCase))
                : null;
            customer ??= FindUniqueCustomerByName(customers.Where(c => c.CustomerType == CustomerType.商业), name);
        }

        customer ??= !string.IsNullOrWhiteSpace(code)
            ? customers.FirstOrDefault(c => c.Code.Equals(code, StringComparison.OrdinalIgnoreCase))
            : null;
        customer ??= FindUniqueCustomerByName(customers, name);

        if (customer != null)
        {
            var changed = false;
            var shortName = GetString(root, "CustomerShortName");
            var salesPerson = GetString(root, "SalesPerson");
            if (string.IsNullOrWhiteSpace(customer.ShortName) && !string.IsNullOrWhiteSpace(shortName)) { customer.ShortName = shortName; changed = true; }
            if (!string.IsNullOrWhiteSpace(province) && !SameText(customer.Province, province)) { customer.Province = province; changed = true; }
            if (string.IsNullOrWhiteSpace(customer.SalesPerson) && !string.IsNullOrWhiteSpace(salesPerson)) { customer.SalesPerson = salesPerson; changed = true; }
            if (string.IsNullOrWhiteSpace(customer.DrugName) && !string.IsNullOrWhiteSpace(drugName)) { customer.DrugName = drugName; changed = true; }
            if (customer.CustomerType == CustomerType.商业 && !customer.DistributorRole.HasValue && distributorRole.HasValue) { customer.DistributorRole = distributorRole; changed = true; }
            if (customer.CustomerType == CustomerType.商业 && !customer.Level.HasValue && distributorLevel.HasValue) { customer.Level = distributorLevel; changed = true; }
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
            Province = GetProvince(root),
            SalesPerson = GetString(root, "SalesPerson"),
            DrugName = drugName,
            CustomerType = createType,
            DistributorRole = createType == CustomerType.商业 ? distributorRole : null,
            Level = createType == CustomerType.商业 ? distributorLevel : null,
        };
        customers.Add(customer);
        result.CreatedCustomers++;
        return customer;
    }

    private static Customer? FindExistingCustomer(
        string name,
        string? preferredCode,
        List<Customer> customers,
        bool preferDealer)
    {
        var code = preferredCode?.Trim() ?? "";
        Customer? customer = null;

        if (preferDealer)
        {
            customer = !string.IsNullOrWhiteSpace(code)
                ? customers.FirstOrDefault(c => c.CustomerType == CustomerType.商业 && c.Code.Equals(code, StringComparison.OrdinalIgnoreCase))
                : null;
            customer ??= FindUniqueCustomerByName(customers.Where(c => c.CustomerType == CustomerType.商业), name);
        }

        customer ??= !string.IsNullOrWhiteSpace(code)
            ? customers.FirstOrDefault(c => c.Code.Equals(code, StringComparison.OrdinalIgnoreCase))
            : null;
        customer ??= FindUniqueCustomerByName(customers, name);

        return customer;
    }

    private static Supplier FindOrCreateSupplier(
        JsonElement root,
        string name,
        List<Supplier> suppliers,
        HashSet<string> usedCodes)
    {
        var supplierName = FirstNonEmpty(name, "江苏神龙药业有限公司");
        var code = FirstNonEmpty(GetString(root, "SupplierCode"), GetString(root, "OriginalSupplierCode"));
        var supplier = !string.IsNullOrWhiteSpace(code)
            ? suppliers.FirstOrDefault(s => s.Code.Equals(code, StringComparison.OrdinalIgnoreCase))
            : null;
        supplier ??= suppliers.FirstOrDefault(s => s.Name.Equals(supplierName, StringComparison.OrdinalIgnoreCase));

        if (supplier != null)
        {
            var changed = false;
            var address = GetString(root, "SupplierAddress", "Address");
            var phone = GetString(root, "SupplierPhone", "Phone");
            var contact = GetString(root, "SupplierContact", "ContactPerson");
            if (string.IsNullOrWhiteSpace(supplier.Address) && !string.IsNullOrWhiteSpace(address)) { supplier.Address = address; changed = true; }
            if (string.IsNullOrWhiteSpace(supplier.Phone) && !string.IsNullOrWhiteSpace(phone)) { supplier.Phone = phone; changed = true; }
            if (string.IsNullOrWhiteSpace(supplier.ContactPerson) && !string.IsNullOrWhiteSpace(contact)) { supplier.ContactPerson = contact; changed = true; }
            if (changed) supplier.UpdatedAt = DateTime.UtcNow;
            return supplier;
        }

        if (string.IsNullOrWhiteSpace(code) || usedCodes.Contains(code))
            code = NextSupplierCode(usedCodes);
        else
            usedCodes.Add(code);

        supplier = new Supplier
        {
            Code = code,
            Name = supplierName,
            ContactPerson = GetString(root, "SupplierContact", "ContactPerson"),
            Phone = GetString(root, "SupplierPhone", "Phone"),
            Address = GetString(root, "SupplierAddress", "Address"),
        };
        suppliers.Add(supplier);
        return supplier;
    }

    private static Supplier? FindExistingSupplier(JsonElement root, string name, List<Supplier> suppliers)
    {
        var supplierName = FirstNonEmpty(name, "江苏神龙药业有限公司");
        var code = FirstNonEmpty(GetString(root, "SupplierCode"), GetString(root, "OriginalSupplierCode"));
        var supplier = !string.IsNullOrWhiteSpace(code)
            ? suppliers.FirstOrDefault(s => s.Code.Equals(code, StringComparison.OrdinalIgnoreCase))
            : null;
        supplier ??= suppliers.FirstOrDefault(s => s.Name.Equals(supplierName, StringComparison.OrdinalIgnoreCase));
        return supplier;
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

    private static string NextSupplierCode(HashSet<string> usedCodes)
    {
        var seq = 1;
        var prefix = "SUPP" + DateTime.UtcNow.ToString("yyyyMMdd");
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
            Province = GetProvince(root),
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
