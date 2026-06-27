namespace PharmaFlow.Domain.Entities;

/// <summary>模块规则配置 — 导入规则下每个目标模块的字段映射和规范化配置</summary>
public class ModuleRuleConfig : BaseEntity
{
    public Guid RuleId { get; set; }
    public ImportRule Rule { get; set; } = null!;

    /// <summary>目标模块: Drug/Customer/Purchase/Sales/DistributionRecord</summary>
    public string ModuleName { get; set; } = string.Empty;

    /// <summary>字段映射 JSON: { "Name": {"source":"FPRODUCTNAME"}, ... }</summary>
    public string FieldMappings { get; set; } = "{}";

    /// <summary>规范化规则 JSON: { "Specification": {"*":"×","calcMultiplier":true} }</summary>
    public string? NormalizeRules { get; set; }

    /// <summary>匹配配置 JSON: { "matchFields":["Name","Specification"], "fuzzyThreshold":0.8 }</summary>
    public string? MatchConfig { get; set; }

    /// <summary>匹配不到时是否自动创建</summary>
    public bool AutoCreate { get; set; }
}