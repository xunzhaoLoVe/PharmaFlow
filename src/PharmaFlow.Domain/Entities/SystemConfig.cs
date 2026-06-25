namespace PharmaFlow.Domain.Entities;

/// <summary>系统配置</summary>
public class SystemConfig : BaseEntity
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Group { get; set; }
}
