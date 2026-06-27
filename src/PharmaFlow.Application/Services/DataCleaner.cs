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