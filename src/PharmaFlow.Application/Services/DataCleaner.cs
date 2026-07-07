using System.Text.Json;
using PharmaFlow.Application.Models;

namespace PharmaFlow.Application.Services;

/// <summary>基础数据清洗器 — 根据 TransformConfig 将原始数据转换为规范化 JSON</summary>
public class DataCleaner
{
    /// <summary>
    /// 清洗一行原始数据。仅做字段映射和类型转换，不做校验/匹配/智能清洗。
    /// </summary>
    public (string cleanedJson, string? targetEntity) Clean(Dictionary<string, string> rawRow, string transformConfigJson)
    {
        TransformConfig? config;
        try
        {
            config = JsonSerializer.Deserialize<TransformConfig>(transformConfigJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            throw new InvalidOperationException("TransformConfig JSON 格式无效");
        }

        if (config == null || config.FieldMappings == null || config.FieldMappings.Count == 0)
            return (JsonSerializer.Serialize(rawRow), null);

        var cleaned = new Dictionary<string, object?>();

        foreach (var mapping in config.FieldMappings)
        {
            var targetField = mapping.Key;
            var sourceField = mapping.Value?.Source ?? targetField;
            var fieldType = mapping.Value?.Type?.ToLower() ?? "string";
            var format = mapping.Value?.Format;
            var defaultVal = mapping.Value?.Default;

            if (!rawRow.TryGetValue(sourceField, out var rawValue) || string.IsNullOrWhiteSpace(rawValue))
            {
                if (defaultVal != null)
                    cleaned[targetField] = ConvertValue(defaultVal?.ToString() ?? "", fieldType, format);
                continue;
            }

            rawValue = rawValue.Trim();

            if (config.ValueMappings != null &&
                config.ValueMappings.TryGetValue(targetField, out var valueMap) &&
                valueMap != null && valueMap.TryGetValue(rawValue, out var mappedValue))
                rawValue = mappedValue;

            cleaned[targetField] = ConvertValue(rawValue, fieldType, format);
        }

        if (config.Defaults != null)
        {
            foreach (var kvp in config.Defaults)
                if (!cleaned.ContainsKey(kvp.Key))
                    cleaned[kvp.Key] = kvp.Value;
        }

        if (!string.IsNullOrWhiteSpace(config.SourceType)
            && (!cleaned.TryGetValue("DataType", out var dataType) || IsBlankValue(dataType)))
        {
            cleaned["DataType"] = config.SourceType.Trim();
        }

        ApplyPostProcessRules(cleaned, config.PostProcessRules);

        return (JsonSerializer.Serialize(cleaned), config.TargetEntity);
    }

    /// <summary>解析 TransformConfig（供 SmartDataCleaner 复用）</summary>
    public static TransformConfig? ParseConfig(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<TransformConfig>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch { return null; }
    }

    private static object? ConvertValue(string rawValue, string type, string? format)
    {
        return type switch
        {
            "int" => int.TryParse(rawValue, out var i) ? i : 0,
            "decimal" => decimal.TryParse(rawValue, out var d) ? d : 0m,
            "datetime" => TryParseDateTime(rawValue, format),
            "bool" => bool.TryParse(rawValue, out var b) && b,
            _ => rawValue
        };
    }

    private static bool IsBlankValue(object? value)
    {
        if (value == null) return true;
        if (value is string s) return string.IsNullOrWhiteSpace(s);
        if (value is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.Null || je.ValueKind == JsonValueKind.Undefined) return true;
            if (je.ValueKind == JsonValueKind.String) return string.IsNullOrWhiteSpace(je.GetString());
        }
        return false;
    }

    private static void ApplyPostProcessRules(
        Dictionary<string, object?> cleaned,
        List<PostProcessRule>? rules)
    {
        if (rules == null || rules.Count == 0) return;

        foreach (var rule in rules)
        {
            if (!rule.Enabled) continue;
            if (!MatchesAll(cleaned, rule.Conditions)) continue;

            var action = (rule.Action ?? "update").Trim().ToLowerInvariant();
            if (action == "exclude" || action == "excludefromsummary")
            {
                cleaned["_excludedFromSummary"] = true;
                cleaned["_excludeReason"] = string.IsNullOrWhiteSpace(rule.ExcludeReason)
                    ? rule.Name ?? ""
                    : rule.ExcludeReason;
                continue;
            }

            if (rule.Updates == null || rule.Updates.Count == 0) continue;

            foreach (var update in rule.Updates)
            {
                if (!string.IsNullOrWhiteSpace(update.Key))
                    cleaned[update.Key] = update.Value;
            }
        }
    }

    private static bool MatchesAll(
        Dictionary<string, object?> cleaned,
        List<PostProcessCondition>? conditions)
    {
        if (conditions == null || conditions.Count == 0) return true;
        return conditions.All(condition => Matches(cleaned, condition));
    }

    private static bool Matches(
        Dictionary<string, object?> cleaned,
        PostProcessCondition condition)
    {
        if (string.IsNullOrWhiteSpace(condition.Field)) return false;

        cleaned.TryGetValue(condition.Field, out var raw);
        var actual = (raw?.ToString() ?? "").Trim();
        var expected = (condition.Value ?? "").Trim();
        var op = (condition.Operator ?? "equals").Trim().ToLowerInvariant();

        return op switch
        {
            "equals" or "eq" => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
            "notEquals" or "notequals" or "ne" => !string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
            "contains" => actual.Contains(expected, StringComparison.OrdinalIgnoreCase),
            "notContains" or "notcontains" => !actual.Contains(expected, StringComparison.OrdinalIgnoreCase),
            "empty" or "isEmpty" or "isempty" => string.IsNullOrWhiteSpace(actual),
            "notEmpty" or "isNotEmpty" or "isnotempty" => !string.IsNullOrWhiteSpace(actual),
            _ => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
        };
    }

    private static DateTime? TryParseDateTime(string value, string? format)
    {
        if (!string.IsNullOrEmpty(format)
            && DateTime.TryParseExact(value, format, null, System.Globalization.DateTimeStyles.None, out var dt))
            return dt;
        if (DateTime.TryParse(value, out var dt2))
            return dt2;
        return null;
    }
}
