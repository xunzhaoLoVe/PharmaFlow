namespace PharmaFlow.Domain.Entities;

/// <summary>药品分类</summary>
public class DrugCategory : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public DrugCategory? Parent { get; set; }
    public ICollection<DrugCategory> Children { get; set; } = new List<DrugCategory>();
    public string? Remark { get; set; }
    public ICollection<Drug> Drugs { get; set; } = new List<Drug>();
}
