namespace PharmaFlow.Domain.Entities;

/// <summary>药品信息</summary>
public class Drug : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? GenericName { get; set; }
    public string? Specification { get; set; }
    public string? Manufacturer { get; set; }
    public string? ApprovalNumber { get; set; }
    public string? DosageForm { get; set; }
    public string Unit { get; set; } = "盒";
    public int WarningLowQuantity { get; set; }

    public Guid? CategoryId { get; set; }
    public DrugCategory? Category { get; set; }

    public ICollection<Inventory> Inventories { get; set; } = new List<Inventory>();
}
