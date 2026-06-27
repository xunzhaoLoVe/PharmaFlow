using PharmaFlow.Domain.Enums;

namespace PharmaFlow.Domain.Entities;

/// <summary>采集任务 — 单次采集执行记录</summary>
public class CollectionTask : BaseEntity
{
    /// <summary>所属采集源</summary>
    public Guid SourceId { get; set; }
    public CollectionSource Source { get; set; } = null!;

    /// <summary>触发方式</summary>
    public TriggerType TriggerType { get; set; } = TriggerType.手动;

    /// <summary>任务状态</summary>
    public CollectionTaskStatus Status { get; set; } = CollectionTaskStatus.待执行;

    /// <summary>错误信息</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>开始执行时间</summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>完成时间</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>本次采集到的记录总数</summary>
    public int TotalRecords { get; set; }

    /// <summary>成功清洗的记录数</summary>
    public int SuccessCount { get; set; }

    /// <summary>异常/错误的记录数</summary>
    public int ErrorCount { get; set; }

    /// <summary>重复跳过的记录数</summary>
    public int SkippedCount { get; set; }

    /// <summary>是否已提交到采集汇总</summary>
    public bool IsSubmitted { get; set; }

    public ICollection<CollectionRecord> Records { get; set; } = new List<CollectionRecord>();
}
