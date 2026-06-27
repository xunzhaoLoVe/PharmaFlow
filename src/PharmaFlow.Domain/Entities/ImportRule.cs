namespace PharmaFlow.Domain.Entities;

/// <summary>导入规则 — 配置数据采集后如何导入到各模块</summary>
public class ImportRule : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ICollection<ModuleRuleConfig> ModuleConfigs { get; set; } = new List<ModuleRuleConfig>();
}