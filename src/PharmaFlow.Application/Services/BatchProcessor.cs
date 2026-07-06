using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PharmaFlow.Application.DTOs;
using PharmaFlow.Application.Models;
using PharmaFlow.Domain.Entities;
using PharmaFlow.Domain.Enums;
using PharmaFlow.Infrastructure.Data;

namespace PharmaFlow.Application.Services;

/// <summary>批次处理器 — 整批提取、匹配、创建药品/客户/流向</summary>
public class BatchProcessor
{
    private readonly AppDbContext _db;
    private readonly SpecNormalizer _specNormalizer;
    private readonly ILogger<BatchProcessor> _logger;

    public BatchProcessor(AppDbContext db, SpecNormalizer specNormalizer, ILogger<BatchProcessor> logger)
    {
        _db = db; _specNormalizer = specNormalizer; _logger = logger;
    }

    /// <summary>预览批次：提取唯一药品/客户，匹配主数据，返回预览结果（可选按规则处理）</summary>
    public async Task<BatchPreviewDto> PreviewAsync(Guid taskId, Guid? ruleId = null)
    {
        var records = await _db.CollectionRecords
            .Where(r => r.TaskId == taskId && r.Status == CollectionRecordStatus.待审核)
            .ToListAsync();

        if (records.Count == 0) return new BatchPreviewDto { TaskId = taskId, TotalRecords = 0 };

        // 提取唯一药品
        var drugMap = new Dictionary<string, DrugInfo>(StringComparer.OrdinalIgnoreCase);
        var customerMap = new Dictionary<string, CustomerInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var record in records)
        {
            if (string.IsNullOrWhiteSpace(record.CleanedDataJson)) continue;
            try
            {
                using var doc = JsonDocument.Parse(record.CleanedDataJson);
                var root = doc.RootElement;
                if (IsExcludedFromSummary(root)) continue;

                var drugName = root.TryGetProperty("DrugName", out var dn) ? dn.GetString() : null;
                var spec = root.TryGetProperty("Specification", out var sp) ? sp.GetString() : null;
                var drugCode = root.TryGetProperty("DrugCode", out var dc) ? dc.GetString() : null;
                var manufacturer = root.TryGetProperty("Manufacturer", out var mf) ? mf.GetString() : null;

                var custName = root.TryGetProperty("ToCustomerName", out var cn) ? cn.GetString() : null;
                var custCode = root.TryGetProperty("ToCustomerCode", out var cc) ? cc.GetString() : null;
                var province = root.TryGetProperty("Province", out var pv) ? pv.GetString() : null;

                if (!string.IsNullOrWhiteSpace(drugName))
                {
                    var normalizedSpec = _specNormalizer.Normalize(spec ?? "");
                    var key = $"{drugName}|{normalizedSpec}";
                    if (!drugMap.ContainsKey(key))
                        drugMap[key] = new DrugInfo { Name = drugName, OriginalSpec = spec, NormalizedSpec = normalizedSpec, Code = drugCode, Manufacturer = manufacturer };
                }

                if (!string.IsNullOrWhiteSpace(custName))
                {
                    var key = custCode ?? custName;
                    if (!customerMap.ContainsKey(key))
                        customerMap[key] = new CustomerInfo { Name = custName, Code = custCode, Province = province };
                }
            }
            catch { }
        }

        // 匹配药品
        var allDrugs = await _db.Drugs.IgnoreQueryFilters().Where(d => !d.IsDeleted).ToListAsync();
        var allCustomers = await _db.Customers.IgnoreQueryFilters().Where(c => !c.IsDeleted).ToListAsync();

        var drugMatches = new List<DrugMatchItem>();
        foreach (var drug in drugMap.Values)
        {
            var match = allDrugs.FirstOrDefault(d =>
                d.Name == drug.Name && d.Specification == drug.NormalizedSpec);
            if (match == null && !string.IsNullOrWhiteSpace(drug.NormalizedSpec))
                match = allDrugs.FirstOrDefault(d => d.Name == drug.Name && d.Specification == drug.OriginalSpec);
            if (match == null)
                match = allDrugs.FirstOrDefault(d => d.Name == drug.Name);

            drugMatches.Add(new DrugMatchItem
            {
                Name = drug.Name,
                OriginalSpec = drug.OriginalSpec,
                NormalizedSpec = drug.NormalizedSpec,
                Manufacturer = drug.Manufacturer,
                Code = drug.Code,
                Matched = match != null,
                ExistingId = match?.Id,
                ExistingName = match?.Name,
                ExistingSpec = match?.Specification,
            });
        }

        var custMatches = new List<CustomerMatchItem>();
        foreach (var cust in customerMap.Values)
        {
            var match = allCustomers.FirstOrDefault(c => c.Name == cust.Name);
            if (match == null && !string.IsNullOrWhiteSpace(cust.Code))
                match = allCustomers.FirstOrDefault(c => c.Code == cust.Code);

            custMatches.Add(new CustomerMatchItem
            {
                Name = cust.Name,
                Code = cust.Code,
                Province = cust.Province,
                Matched = match != null,
                ExistingId = match?.Id,
                ExistingName = match?.Name,
            });
        }

        return new BatchPreviewDto
        {
            TaskId = taskId,
            TotalRecords = records.Count,
            Drugs = drugMatches,
            Customers = custMatches,
        };
    }

    /// <summary>执行批量导入：创建药品→创建客户→导入流向</summary>
    public async Task<BatchImportResult> ImportAsync(Guid taskId)
    {
        var preview = await PreviewAsync(taskId);
        var result = new BatchImportResult();

        // 1. 创建新药品
        var drugIdMap = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in preview.Drugs)
        {
            if (d.Matched && d.ExistingId.HasValue)
            {
                drugIdMap[$"{d.Name}|{d.NormalizedSpec}"] = d.ExistingId.Value;
                drugIdMap[$"{d.Name}|{d.OriginalSpec}"] = d.ExistingId.Value;
                continue;
            }

            var newDrug = new Drug
            {
                Code = d.Code ?? GenerateDrugCode(),
                Name = d.Name,
                Specification = d.NormalizedSpec ?? d.OriginalSpec,
                Manufacturer = d.Manufacturer,
                Unit = "盒",
            };
            _db.Drugs.Add(newDrug);
            await _db.SaveChangesAsync();
            drugIdMap[$"{d.Name}|{d.NormalizedSpec}"] = newDrug.Id;
            drugIdMap[$"{d.Name}|{d.OriginalSpec}"] = newDrug.Id;
            result.CreatedDrugs++;
        }

        // 2. 创建新客户
        var custIdMap = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in preview.Customers)
        {
            if (c.Matched && c.ExistingId.HasValue)
            {
                custIdMap[c.Name] = c.ExistingId.Value;
                if (!string.IsNullOrWhiteSpace(c.Code)) custIdMap[c.Code] = c.ExistingId.Value;
                continue;
            }

            var custCode = !string.IsNullOrWhiteSpace(c.Code) ? c.Code : "CUST" + DateTime.Now.ToString("yyyyMMddHHmmss") + Interlocked.Increment(ref _codeSeq).ToString("D4");
            var newCust = new Customer
            {
                Name = c.Name,
                Code = custCode,
                Province = c.Province,
                CustomerType = CustomerType.商业,
            };
            _db.Customers.Add(newCust);
            await _db.SaveChangesAsync();
            custIdMap[c.Name] = newCust.Id;
            if (!string.IsNullOrWhiteSpace(c.Code)) custIdMap[c.Code] = newCust.Id;
            result.CreatedCustomers++;
        }

        // 3. 批量导入流向记录
        var records = await _db.CollectionRecords
            .Where(r => r.TaskId == taskId && r.Status == CollectionRecordStatus.待审核)
            .ToListAsync();

        foreach (var record in records)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(record.CleanedDataJson)) continue;
                using var doc = JsonDocument.Parse(record.CleanedDataJson);
                var root = doc.RootElement;
                if (IsExcludedFromSummary(root))
                {
                    result.Skipped++;
                    continue;
                }

                var drugName = root.TryGetProperty("DrugName", out var dn) ? dn.GetString() : "";
                var spec = root.TryGetProperty("Specification", out var sp) ? sp.GetString() : "";
                var normalizedSpec = _specNormalizer.Normalize(spec ?? "");
                var custName = root.TryGetProperty("ToCustomerName", out var cn) ? cn.GetString() : "";
                var custCode = root.TryGetProperty("ToCustomerCode", out var cc) ? cc.GetString() : "";

                var drugKey = $"{drugName}|{normalizedSpec}";
                if (!drugIdMap.TryGetValue(drugKey, out var drugId) && !string.IsNullOrWhiteSpace(spec))
                    drugIdMap.TryGetValue($"{drugName}|{spec}", out drugId);

                var custKey = custName ?? "";
                if (!custIdMap.TryGetValue(custKey, out var custId) && !string.IsNullOrWhiteSpace(custCode))
                    custIdMap.TryGetValue(custCode, out custId);

                if (drugId == Guid.Empty)
                {
                    record.ErrorMessage = $"药品未匹配: {drugName}";
                    record.Status = CollectionRecordStatus.错误;
                    result.Skipped++;
                    continue;
                }

                var distRecord = new DistributionRecord
                {
                    DrugId = drugId,
                    BatchNo = root.TryGetProperty("BatchNo", out var bn) ? bn.GetString() ?? "" : "",
                    Quantity = root.TryGetProperty("Quantity", out var q) ? q.GetDecimal() : 0,
                    TaxedUnitPrice = root.TryGetProperty("TaxedUnitPrice", out var up) ? up.GetDecimal() : 0,
                    ToCustomerId = custId != Guid.Empty ? custId : null,
                    Province = root.TryGetProperty("Province", out var pv) ? pv.GetString() : null,
                    SalesPerson = root.TryGetProperty("SalesPerson", out var sl) ? sl.GetString() : null,
                    DeliveryOrderNo = root.TryGetProperty("DeliveryOrderNo", out var don) ? don.GetString() : null,
                    Operator = root.TryGetProperty("Operator", out var op) ? op.GetString() : null,
                    DistributionDate = root.TryGetProperty("DistributionDate", out var dd) && dd.TryGetDateTime(out var dt) ? dt : DateTime.UtcNow,
                    ReviewStatus = "待审核",
                };
                _db.DistributionRecords.Add(distRecord);
                record.TargetEntityId = distRecord.Id;
                record.Status = CollectionRecordStatus.已导入;
                record.UpdatedAt = DateTime.UtcNow;
                result.Imported++;
            }
            catch (Exception ex)
            {
                record.ErrorMessage = $"导入失败: {ex.Message}";
                record.Status = CollectionRecordStatus.错误;
                result.Skipped++;
            }
        }

        await _db.SaveChangesAsync();
        return result;
    }

    // ═══════════════════════════════════════════
    // 内部模型
    // ═══════════════════════════════════════════

    public class DrugInfo
    {
        public string Name { get; set; } = "";
        public string? OriginalSpec { get; set; }
        public string? NormalizedSpec { get; set; }
        public string? Code { get; set; }
        public string? Manufacturer { get; set; }
    }

    public class CustomerInfo
    {
        public string Name { get; set; } = "";
        public string? Code { get; set; }
        public string? Province { get; set; }
    }

    private static int _codeSeq;
    private static string GenerateDrugCode()
    {
        var seq = Interlocked.Increment(ref _codeSeq);
        return "DRUG" + DateTime.Now.ToString("yyyyMMddHHmmss") + seq.ToString("D4");
    }

    private static bool IsExcludedFromSummary(JsonElement root)
    {
        if (!root.TryGetProperty("_excludedFromSummary", out var excluded))
            return false;

        return excluded.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.String => bool.TryParse(excluded.GetString(), out var b) && b,
            JsonValueKind.Number => excluded.TryGetInt32(out var n) && n != 0,
            _ => false,
        };
    }
}
