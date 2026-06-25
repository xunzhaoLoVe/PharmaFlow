namespace PharmaFlow.Domain.Entities;

/// <summary>角色</summary>
public class Role : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Permissions { get; set; } = "[]"; // JSON array of permission keys
    public ICollection<User> Users { get; set; } = new List<User>();
}
