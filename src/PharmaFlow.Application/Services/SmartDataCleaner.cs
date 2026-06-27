using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PharmaFlow.Application.Models;
using PharmaFlow.Domain.Entities;
using PharmaFlow.Infrastructure.Data;

namespace PharmaFlow.Application.Services;

/// <summary>智能数据清洗器 — 三阶段：格式校验 → 主数据匹配 → 智能清洗</summary>
public class SmartDataCleaner
{
    private readonly AppDbContext _db;
    private readonly DataCleaner _basicCleaner;
    private readonly ILogger<SmartDataCleaner> _logger;

    private List<Drug>? _drugsCache;
    private List<Customer>? _customersCache;

    private static readonly Dictionary<string, string> ProvinceMap = new()
    {
        ["沪"] = "上海市", ["京"] = "北京市", ["津"] = "天津市", ["渝"] = "重庆市",
        ["冀"] = "河北省", ["晋"] = "山西省", ["辽"] = "辽宁省", ["吉"] = "吉林省",
        ["黑"] = "黑龙江省", ["苏"] = "江苏省", ["浙"] = "浙江省", ["皖"] = "安徽省",
        ["闽"] = "福建省", ["赣"] = "江西省", ["鲁"] = "山东省", ["豫"] = "河南省",
        ["鄂"] = "湖北省", ["湘"] = "湖南省", ["粤"] = "广东省", ["琼"] = "海南省",
        ["川"] = "四川省", ["黔"] = "贵州省", ["滇"] = "云南省", ["陕"] = "陕西省",
        ["甘"] = "甘肃省", ["青"] = "青海省", ["台"] = "台湾省", ["蒙"] = "内蒙古自治区",
        ["桂"] = "广西壮族自治区", ["藏"] = "西藏自治区", ["宁"] = "宁夏回族自治区",
        ["新"] = "新疆维吾尔自治区", ["港"] = "香港特别行政区", ["澳"] = "澳门特别行政区",
    };

    public SmartDataCleaner(AppDbContext db, DataCleaner basicCleaner, ILogger<SmartDataCleaner> logger)
    {
        _db = db; _basicCleaner = basicCleaner; _logger = logger;
    }

    public async Task<SmartCleanResult> CleanAsync(
        Dictionary<string, string> rawRow, string transformConfigJson,
        HashSet<string>? seenDedupKeys = null)
    {
        var result = new SmartCleanResult();
        var config = DataCleaner.ParseConfig(transformConfigJson);

        Dictionary<string, object?> cleaned;
        try
        {
            var (cleanedJson, targetEntity) = _basicCleaner.Clean(rawRow, transformConfigJson);
            result.TargetEntityType = targetEntity;
            cleaned = JsonSerializer.Deserialize<Dictionary<string, object?>>(cleanedJson) ?? new();
        }
        catch (Exception ex) { result.ErrorMessage = $"基础清洗失败: {ex.Message}"; return result; }

        if (config == null) { result.CleanedDataJson = JsonSerializer.Serialize(cleaned); return result; }

        if (config.Validation != null)
            result.ValidationIssues = ValidateFields(cleaned, config.Validation);

        if (config.MasterDataMatch != null)
        {
            await EnsureCacheAsync();
            result.MatchResults = await MatchMasterDataAsync(cleaned, config.MasterDataMatch);
        }

        if (config.SmartCleaning != null)
        {
            if (config.SmartCleaning.NormalizeText)
                cleaned = NormalizeText(cleaned);
            if (config.SmartCleaning.Deduplicate != null && seenDedupKeys != null)
                result.IsDuplicate = CheckDuplicate(cleaned, config.SmartCleaning.Deduplicate, seenDedupKeys);
        }

        EnrichCleanedData(cleaned, result);
        result.CleanedDataJson = JsonSerializer.Serialize(cleaned);
        return result;
    }

    public List<string> DetectBatchAnomalies(List<string> cleanedDataJsons, string transformConfigJson)
    {
        var config = DataCleaner.ParseConfig(transformConfigJson);
        if (config?.SmartCleaning?.AnomalyDetection == null) return cleanedDataJsons;

        var anomalyCfg = config.SmartCleaning.AnomalyDetection;
        var quantities = new List<decimal>();
        var prices = new List<decimal>();

        foreach (var json in cleanedDataJsons)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("Quantity", out var q) && q.TryGetDecimal(out var qv)) quantities.Add(qv);
                if (root.TryGetProperty("TaxedUnitPrice", out var p) && p.TryGetDecimal(out var pv)) prices.Add(pv);
            }
            catch { }
        }

        if (quantities.Count < 4 && prices.Count < 4) return cleanedDataJsons;

        var qIqr = ComputeIqr(quantities);
        var pIqr = ComputeIqr(prices);
        var results = new List<string>();

        for (int i = 0; i < cleanedDataJsons.Count; i++)
        {
            var json = cleanedDataJsons[i];
            var anomalies = new List<AnomalyInfo>();
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("Quantity", out var q) && q.TryGetDecimal(out var qv)
                    && qIqr.HasValue && (qv < qIqr.Value.lower || qv > qIqr.Value.upper))
                    anomalies.Add(new AnomalyInfo { Field = "Quantity", Reason = $"值 {qv} 超出IQR范围 [{qIqr.Value.lower:F0}, {qIqr.Value.upper:F0}]" });
                if (root.TryGetProperty("TaxedUnitPrice", out var p) && p.TryGetDecimal(out var pv)
                    && pIqr.HasValue && (pv < pIqr.Value.lower || pv > pIqr.Value.upper))
                    anomalies.Add(new AnomalyInfo { Field = "TaxedUnitPrice", Reason = $"值 {pv} 超出IQR范围 [{pIqr.Value.lower:F2}, {pIqr.Value.upper:F2}]" });
            }
            catch { }

            if (anomalies.Count > 0)
            {
                try
                {
                    var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(json) ?? new();
                    dict["_smartCleaning"] = new Dictionary<string, object?>
                    {
                        ["anomalies"] = anomalies.Select(a => new Dictionary<string, string> { ["field"] = a.Field, ["reason"] = a.Reason }).ToList()
                    };
                    json = JsonSerializer.Serialize(dict);
                }
                catch { }
            }
            results.Add(json);
        }
        return results;
    }

    private List<ValidationIssue> ValidateFields(Dictionary<string, object?> cleaned, ValidationConfig config)
    {
        var issues = new List<ValidationIssue>();
        if (config.RequiredFields != null)
        {
            foreach (var field in config.RequiredFields)
            {
                if (!cleaned.TryGetValue(field, out var val) || val == null
                    || (val is string s && string.IsNullOrWhiteSpace(s))
                    || (val is JsonElement je && je.ValueKind == JsonValueKind.Null))
                    issues.Add(new ValidationIssue { Field = field, Issue = "必填字段缺失", Severity = "error" });
            }
        }
        if (config.FieldRules != null)
        {
            foreach (var rule in config.FieldRules)
            {
                if (!cleaned.TryGetValue(rule.Key, out var val) || val == null) continue;
                decimal? numVal = val switch
                {
                    JsonElement je when je.ValueKind == JsonValueKind.Number => je.GetDecimal(),
                    decimal d => d, int i => i, double db => (decimal)db, _ => null
                };
                if (numVal.HasValue)
                {
                    if (rule.Value.Min.HasValue && numVal < (decimal)rule.Value.Min.Value)
                        issues.Add(new ValidationIssue { Field = rule.Key, Issue = $"值 {numVal} 小于最小 {rule.Value.Min}", Severity = "warning" });
                    if (rule.Value.Max.HasValue && numVal > (decimal)rule.Value.Max.Value)
                        issues.Add(new ValidationIssue { Field = rule.Key, Issue = $"值 {numVal} 大于最大 {rule.Value.Max}", Severity = "warning" });
                }
                if (rule.Value.AllowedValues != null && val != null)
                {
                    var strVal = val is JsonElement je2 ? je2.GetString() : val.ToString();
                    if (strVal != null && !rule.Value.AllowedValues.Contains(strVal))
                        issues.Add(new ValidationIssue { Field = rule.Key, Issue = $"值 '{strVal}' 不在允许范围", Severity = "warning" });
                }
            }
        }
        return issues;
    }

    private async Task<List<MatchResult>> MatchMasterDataAsync(Dictionary<string, object?> cleaned, MasterDataMatchConfig config)
    {
        var results = new List<MatchResult>();
        if (config.Drug != null) results.Add(await MatchDrugAsync(cleaned, config.Drug));
        if (config.Customer != null) results.Add(await MatchCustomerAsync(cleaned, config.Customer));
        return results;
    }

    private async Task<MatchResult> MatchDrugAsync(Dictionary<string, object?> cleaned, EntityMatchConfig config)
    {
        var sourceValue = GetString(cleaned, config.SourceField ?? "DrugName");
        var result = new MatchResult { EntityType = "Drug", SourceValue = sourceValue ?? "" };
        if (string.IsNullOrWhiteSpace(sourceValue) || _drugsCache == null) { result.MatchType = "none"; return result; }

        var normalized = NormalizeForMatch(sourceValue);

        var match = _drugsCache.FirstOrDefault(d => NormalizeForMatch(d.Name) == normalized);
        if (match != null) { SetMatch(result, match.Id, match.Name, 1.0, "exact"); return result; }

        match = _drugsCache.FirstOrDefault(d => d.GenericName != null && NormalizeForMatch(d.GenericName) == normalized);
        if (match != null) { SetMatch(result, match.Id, match.Name, 0.95, "generic"); return result; }

        match = _drugsCache.FirstOrDefault(d => NormalizeForMatch(d.Name).Contains(normalized) || normalized.Contains(NormalizeForMatch(d.Name)));
        if (match != null) { SetMatch(result, match.Id, match.Name, 0.85, "contains"); return result; }

        var bestScore = config.FuzzyThreshold;
        Drug? bestMatch = null;
        foreach (var drug in _drugsCache)
        {
            var score = ComputeSimilarity(normalized, NormalizeForMatch(drug.Name));
            if (score > bestScore) { bestScore = score; bestMatch = drug; }
        }
        if (bestMatch != null) { SetMatch(result, bestMatch.Id, bestMatch.Name, bestScore, "fuzzy"); return result; }

        result.MatchType = "none"; return result;
    }

    private async Task<MatchResult> MatchCustomerAsync(Dictionary<string, object?> cleaned, EntityMatchConfig config)
    {
        var sourceValue = GetString(cleaned, config.SourceField ?? "ToCustomerName");
        var result = new MatchResult { EntityType = "Customer", SourceValue = sourceValue ?? "" };
        if (string.IsNullOrWhiteSpace(sourceValue) || _customersCache == null) { result.MatchType = "none"; return result; }

        var normalized = NormalizeForMatch(sourceValue);

        var match = _customersCache.FirstOrDefault(c => NormalizeForMatch(c.Name) == normalized);
        if (match != null) { SetMatch(result, match.Id, match.Name, 1.0, "exact"); return result; }

        match = _customersCache.FirstOrDefault(c => c.ShortName != null && NormalizeForMatch(c.ShortName) == normalized);
        if (match != null) { SetMatch(result, match.Id, match.Name, 0.95, "shortname"); return result; }

        match = _customersCache.FirstOrDefault(c => c.Code == sourceValue.Trim());
        if (match != null) { SetMatch(result, match.Id, match.Name, 1.0, "code"); return result; }

        match = _customersCache.FirstOrDefault(c => NormalizeForMatch(c.Name).Contains(normalized) || normalized.Contains(NormalizeForMatch(c.Name)));
        if (match != null) { SetMatch(result, match.Id, match.Name, 0.85, "contains"); return result; }

        var bestScore = config.FuzzyThreshold;
        Customer? bestMatch = null;
        foreach (var cust in _customersCache)
        {
            var score = ComputeSimilarity(normalized, NormalizeForMatch(cust.Name));
            if (cust.ShortName != null) score = Math.Max(score, ComputeSimilarity(normalized, NormalizeForMatch(cust.ShortName)));
            if (score > bestScore) { bestScore = score; bestMatch = cust; }
        }
        if (bestMatch != null) { SetMatch(result, bestMatch.Id, bestMatch.Name, bestScore, "fuzzy"); return result; }

        result.MatchType = "none"; return result;
    }

    private Dictionary<string, object?> NormalizeText(Dictionary<string, object?> cleaned)
    {
        var result = new Dictionary<string, object?>();
        foreach (var kvp in cleaned)
        {
            if (kvp.Value is string s) result[kvp.Key] = Regex.Replace(s.Trim(), @"\s+", " ");
            else if (kvp.Value is JsonElement je && je.ValueKind == JsonValueKind.String) result[kvp.Key] = Regex.Replace(je.GetString()!.Trim(), @"\s+", " ");
            else result[kvp.Key] = kvp.Value;
        }
        return result;
    }

    private bool CheckDuplicate(Dictionary<string, object?> cleaned, DeduplicateConfig config, HashSet<string> seenKeys)
    {
        if (config.KeyFields == null || config.KeyFields.Count == 0) return false;
        var key = string.Join("|", config.KeyFields.Select(f => GetString(cleaned, f) ?? ""));
        if (seenKeys.Contains(key)) return true;
        seenKeys.Add(key);
        return false;
    }

    private void EnrichCleanedData(Dictionary<string, object?> cleaned, SmartCleanResult result)
    {
        if (result.MatchResults.Count > 0)
        {
            var m = new Dictionary<string, object?>();
            foreach (var r in result.MatchResults)
            {
                var p = r.EntityType.ToLower();
                m[$"{p}Id"] = r.MatchedId?.ToString();
                m[$"{p}Name"] = r.MatchedName;
                m[$"{p}Confidence"] = r.Confidence;
                m[$"{p}MatchType"] = r.MatchType;
            }
            cleaned["_matches"] = m;
        }
        if (result.ValidationIssues.Count > 0)
        {
            cleaned["_validation"] = new Dictionary<string, object?>
            {
                ["issues"] = result.ValidationIssues.Select(i => new Dictionary<string, string> { ["field"] = i.Field, ["issue"] = i.Issue, ["severity"] = i.Severity }).ToList()
            };
        }
        if (result.IsDuplicate)
            cleaned["_smartCleaning"] = new Dictionary<string, object?> { ["isDuplicate"] = true };
    }

    private async Task EnsureCacheAsync()
    {
        _drugsCache ??= await _db.Drugs.IgnoreQueryFilters().Where(d => !d.IsDeleted).ToListAsync();
        _customersCache ??= await _db.Customers.IgnoreQueryFilters().Where(c => !c.IsDeleted).ToListAsync();
    }

    private static string GetString(Dictionary<string, object?> dict, string key)
    {
        if (!dict.TryGetValue(key, out var val) || val == null) return "";
        if (val is JsonElement je) return je.GetString() ?? "";
        return val.ToString() ?? "";
    }

    private static void SetMatch(MatchResult r, Guid id, string name, double confidence, string type)
    { r.MatchedId = id; r.MatchedName = name; r.Confidence = confidence; r.MatchType = type; }

    private static string NormalizeForMatch(string s) => Regex.Replace(s.Trim(), @"\s+", "").ToLowerInvariant();

    private static double ComputeSimilarity(string a, string b)
    {
        if (a == b) return 1.0; if (a.Length == 0 || b.Length == 0) return 0.0;
        var d = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) d[0, j] = j;
        for (int i = 1; i <= a.Length; i++)
            for (int j = 1; j <= b.Length; j++)
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
        return 1.0 - (double)d[a.Length, b.Length] / Math.Max(a.Length, b.Length);
    }

    private static (decimal lower, decimal upper)? ComputeIqr(List<decimal> values)
    {
        if (values.Count < 4) return null;
        var sorted = values.OrderBy(v => v).ToList();
        int n = sorted.Count;
        return (sorted[n / 4] - 3 * (sorted[3 * n / 4] - sorted[n / 4]),
                sorted[3 * n / 4] + 3 * (sorted[3 * n / 4] - sorted[n / 4]));
    }
}