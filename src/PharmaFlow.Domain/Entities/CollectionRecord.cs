using PharmaFlow.Domain.Enums;

namespace PharmaFlow.Domain.Entities;

/// <summary>采集记录 — 单条数据，从原始到清洗到审核到导入的全生命周期</summary>
public class CollectionRecord : BaseEntity
{
    /// <summary>所属采集任务</summary>
    public Guid TaskId { get; set; }
    public CollectionTask Task { get; set; } = null!;

    /// <summary>所属采集源 (冗余，方便按源查询)</summary>
    public Guid SourceId { get; set; }
    public CollectionSource Source { get; set; } = null!;

    /// <summary>记录状态</summary>
    public CollectionRecordStatus Status { get; set; } = CollectionRecordStatus.原始;

    /// <summary>原始数据 JSON（从外部平台提取的原始记录）</summary>
    public string RawDataJson { get; set; } = "{}";

    /// <summary>清洗后的数据 JSON（变换后的规范化记录）</summary>
    public string? CleanedDataJson { get; set; }

    /// <summary>目标实体类型名称，如 "DistributionRecord"</summary>
    public string? TargetEntityType { get; set; }

    /// <summary>导入后关联的主数据实体 Id</summary>
    public Guid? TargetEntityId { get; set; }

    /// <summary>审核人</summary>
    public Guid? ReviewerId { get; set; }
    public User? Reviewer { get; set; }

    /// <summary>审核时间</summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>驳回原因</summary>
    public string? RejectReason { get; set; }

    /// <summary>处理错误信息</summary>
    public string? ErrorMessage { get; set; }
}
