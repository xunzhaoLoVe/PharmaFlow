namespace PharmaFlow.Domain.Entities;

/// <summary>
/// 采集同步追踪 — 记录每次采集汇总同步时「新建」的主数据实体，
/// 用于在取消提交 / 清空汇总时联动清理这些由采集创建的主数据。
/// </summary>
public class CollectionSyncEntity : BaseEntity
{
    /// <summary>实体类型: Drug / Customer / Supplier</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>被新建的主数据实体 Id</summary>
    public Guid EntityId { get; set; }

    /// <summary>采集源 Id（冗余，便于按源清理）</summary>
    public Guid? SourceId { get; set; }

    /// <summary>采集任务 Id（冗余，便于按任务清理）</summary>
    public Guid? TaskId { get; set; }
}
