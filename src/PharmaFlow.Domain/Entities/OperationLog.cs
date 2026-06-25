namespace PharmaFlow.Domain.Entities;

/// <summary>操作日志</summary>
public class OperationLog : BaseEntity
{
    public Guid? UserId { get; set; }
    public User? User { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? Detail { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? IpAddress { get; set; }
}
