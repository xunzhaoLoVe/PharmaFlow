namespace PharmaFlow.Domain.Entities;

/// <summary>客户分类</summary>
public class CustomerCategory : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public CustomerCategory? Parent { get; set; }
    public ICollection<CustomerCategory> Children { get; set; } = new List<CustomerCategory>();
    public string? Remark { get; set; }
    public ICollection<Customer> Customers { get; set; } = new List<Customer>();
}
