namespace PharmaFlow.Domain.Entities;

/// <summary>实体基类</summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>软删除基类</summary>
public abstract class AuditableEntity : BaseEntity
{
    public bool IsDeleted { get; set; }
}
