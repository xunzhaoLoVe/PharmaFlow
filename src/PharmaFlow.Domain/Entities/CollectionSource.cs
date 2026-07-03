using PharmaFlow.Domain.Enums;

namespace PharmaFlow.Domain.Entities;

/// <summary>数据采集源 — 外部平台配置</summary>
public class CollectionSource : AuditableEntity
{
    /// <summary>采集源名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>平台类型</summary>
    public CollectionPlatformType PlatformType { get; set; } = CollectionPlatformType.流向平台;

    /// <summary>外部平台基础 URL</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>认证方式</summary>
    public CollectionAuthType AuthType { get; set; } = CollectionAuthType.Cookie;

    /// <summary>配置模式：JSON配置 或 Python脚本</summary>
    public CollectionMode CollectionMode { get; set; } = CollectionMode.JsonConfig;

    /// <summary>
    /// 认证配置 JSON。
    /// 示例: { "loginPath":"/login", "username":"...", "password":"...", "extraFields":{} }
    /// </summary>
    public string AuthConfig { get; set; } = "{}";

    /// <summary>
    /// 导出配置 JSON。
    /// 示例: { "exportPath":"/export", "method":"GET","queryParams":{}, "dataFormat":"html",
    ///         "tableSelector":"table.data-table", "rowSelector":"tbody tr" }
    /// </summary>
    public string ExportConfig { get; set; } = "{}";

    /// <summary>
    /// 字段映射/变换配置 JSON，用于数据清洗。
    /// 示例: { "targetEntity":"DistributionRecord", "fieldMappings":{...}, "valueMappings":{...}, "defaults":{...} }
    /// </summary>
    public string TransformConfig { get; set; } = "{}";

    /// <summary>是否启用</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>定时 Cron 表达式，null 表示仅手动触发</summary>
    public string? ScheduleCron { get; set; }

    /// <summary>最近一次采集时间</summary>
    public DateTime? LastCollectionAt { get; set; }

    /// <summary>最近一次采集状态 (空/成功/失败)</summary>
    public string? LastCollectionStatus { get; set; }

    /// <summary>采集完成后是否自动提交到汇总</summary>
    public bool AutoSubmit { get; set; }

    /// <summary>关联的导入规则 ID</summary>
    public Guid? RuleId { get; set; }
    public ImportRule? Rule { get; set; }

    public ICollection<CollectionTask> Tasks { get; set; } = new List<CollectionTask>();
    public ICollection<CollectionRecord> Records { get; set; } = new List<CollectionRecord>();
}
