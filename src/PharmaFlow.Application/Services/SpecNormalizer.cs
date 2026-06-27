using System.Text.RegularExpressions;

namespace PharmaFlow.Application.Services;

/// <summary>规格规范化引擎 — 将各种格式的药品规格统一为标准格式</summary>
public class SpecNormalizer
{
    // 单位替换映射
    private static readonly Dictionary<string, string> UnitMap = new()
    {
        ["s"] = "片", ["t"] = "片", ["tab"] = "片", ["tablet"] = "片",
        ["粒"] = "粒", ["l"] = "粒",
        ["支"] = "支", ["z"] = "支",
        ["瓶"] = "瓶", ["b"] = "瓶", ["bot"] = "瓶",
        ["盒"] = "盒", ["h"] = "盒", ["box"] = "盒",
        ["袋"] = "袋", ["d"] = "袋",
        ["板"] = "片", // 板需要计算
    };

    /// <summary>
    /// 规范化规格字符串
    /// 例: "0.15g*12s*2板" → "0.15g×24片"
    /// </summary>
    public string Normalize(string spec)
    {
        if (string.IsNullOrWhiteSpace(spec)) return spec;

        spec = spec.Trim();

        // 统一分隔符: * → ×
        spec = spec.Replace('*', '×');

        // 规范化空格
        spec = Regex.Replace(spec, @"\s+", "");

        // 尝试解析规格模式: 剂量×数量×板数/盒数
        // 例: "0.15g×12s×2板" → 剂量=0.15g, 数量=12, 单位=片, 板数=2
        spec = NormalizeMultiplier(spec);

        // 替换单位缩写
        foreach (var (abbr, full) in UnitMap)
        {
            // 匹配数字后面的单位缩写
            spec = Regex.Replace(spec, $@"(\d+){Regex.Escape(abbr)}(?![a-zA-Z])", $"$1{full}");
            // 匹配字符串末尾的单位缩写
            spec = Regex.Replace(spec, $@"^{Regex.Escape(abbr)}$", full);
        }

        return spec;
    }

    /// <summary>
    /// 处理规格中的乘法: 12s×2板 → 24片
    /// </summary>
    private string NormalizeMultiplier(string spec)
    {
        // 匹配 ×数字板/盒 模式: 12s×2板 → 24片
        var multiMatch = Regex.Match(spec, @"(\d+)\s*[sS片粒]\s*[×xX]\s*(\d+)\s*([板盒袋瓶])");
        if (multiMatch.Success)
        {
            var count = int.Parse(multiMatch.Groups[1].Value);
            var multiplier = int.Parse(multiMatch.Groups[2].Value);
            var unit = multiMatch.Groups[3].Value;
            var total = count * multiplier;
            var unitName = UnitMap.GetValueOrDefault(unit, unit);

            // 替换整个匹配为计算结果
            var replacement = $"{total}{unitName}";
            spec = spec.Remove(multiMatch.Index, multiMatch.Length)
                       .Insert(multiMatch.Index, replacement);
        }

        // 简单 ×N 模式: ×2板 → 删除（如果前面没有数字）
        // 不对，保留简单模式

        return spec;
    }

    /// <summary>
    /// 从规格中提取标准化的规格字符串用于匹配
    /// 例: "0.15g*12s*2板" 和 "0.15g×24片" 标准化后都得到 "0.15g×24片"
    /// </summary>
    public string NormalizeForMatch(string spec)
    {
        var normalized = Normalize(spec);
        // 去除所有空格
        normalized = Regex.Replace(normalized, @"\s+", "");
        // 统一为小写
        normalized = normalized.ToLowerInvariant();
        // 统一 × 和 x 和 X
        normalized = normalized.Replace('x', '×').Replace('X', '×');
        return normalized;
    }
}