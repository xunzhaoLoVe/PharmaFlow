namespace PharmaFlow.Application.Models;

/// <summary>TransformConfig 完整配置模型</summary>
public class TransformConfig
{
    public string? TargetEntity { get; set; }
    public Dictionary<string, FieldMapping>? FieldMappings { get; set; }
    public Dictionary<string, Dictionary<string, string>>? ValueMappings { get; set; }
    public Dictionary<string, object>? Defaults { get; set; }

    // 新增：三阶段智能清洗配置（全部可选，向后兼容）
    public ValidationConfig? Validation { get; set; }
    public MasterDataMatchConfig? MasterDataMatch { get; set; }
    public SmartCleaningConfig? SmartCleaning { get; set; }
    public List<PostProcessRule>? PostProcessRules { get; set; }
}

public class FieldMapping
{
    public string? Source { get; set; }
    public string? Type { get; set; }
    public string? Format { get; set; }
    public object? Default { get; set; }
}

public class PostProcessRule
{
    public string? Name { get; set; }
    public bool Enabled { get; set; } = true;
    public string Action { get; set; } = "update";
    public string? ExcludeReason { get; set; }
    public List<PostProcessCondition>? Conditions { get; set; }
    public Dictionary<string, string>? Updates { get; set; }
}

public class PostProcessCondition
{
    public string Field { get; set; } = "";
    public string Operator { get; set; } = "equals";
    public string? Value { get; set; }
}

// ═══════════════════════════════════════════
// Stage 1: 数据格式校验
// ═══════════════════════════════════════════

public class ValidationConfig
{
    public List<string>? RequiredFields { get; set; }
    public Dictionary<string, FieldRule>? FieldRules { get; set; }
}

public class FieldRule
{
    public string? Type { get; set; }
    public double? Min { get; set; }
    public double? Max { get; set; }
    public List<string>? AllowedValues { get; set; }
    public string? Pattern { get; set; }
    public string? Message { get; set; }
}

// ═══════════════════════════════════════════
// Stage 2: 主数据匹配
// ═══════════════════════════════════════════

public class MasterDataMatchConfig
{
    public EntityMatchConfig? Drug { get; set; }
    public EntityMatchConfig? Customer { get; set; }
    public bool StandardizeProvince { get; set; }
}

public class EntityMatchConfig
{
    public string? SourceField { get; set; }
    public List<string>? MatchFields { get; set; }
    public string? SpecField { get; set; }
    public double FuzzyThreshold { get; set; } = 0.8;
}

// ═══════════════════════════════════════════
// Stage 3: 智能清洗
// ═══════════════════════════════════════════

public class SmartCleaningConfig
{
    public bool NormalizeText { get; set; }
    public DeduplicateConfig? Deduplicate { get; set; }
    public AnomalyDetectionConfig? AnomalyDetection { get; set; }
}

public class DeduplicateConfig
{
    public List<string>? KeyFields { get; set; }
    public string Strategy { get; set; } = "first";
}

public class AnomalyDetectionConfig
{
    public double QuantityIqrMultiplier { get; set; } = 3.0;
    public double PriceIqrMultiplier { get; set; } = 3.0;
}
