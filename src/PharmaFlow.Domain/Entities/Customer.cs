using PharmaFlow.Domain.Enums;

namespace PharmaFlow.Domain.Entities;

/// <summary>客户信息</summary>
public class Customer : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Province { get; set; }
    public string? SalesPerson { get; set; }
    /// <summary>经销药品名称</summary>
    public string? DrugName { get; set; }
    public CustomerType CustomerType { get; set; }
    /// <summary>经销商级别（仅对经销商业类型有效）</summary>
    public DistributorLevel? Level { get; set; }
    /// <summary>机构角色（仅对经销商业类型有效）</summary>
    public DistributorRole? DistributorRole { get; set; }

    public Guid? CategoryId { get; set; }
    public CustomerCategory? Category { get; set; }
}
