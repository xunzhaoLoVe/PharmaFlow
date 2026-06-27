using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PharmaFlow.Domain.Entities;
using PharmaFlow.Domain.Enums;

namespace PharmaFlow.Infrastructure.Data.EntityConfigurations;

public class DrugCategoryConfiguration : IEntityTypeConfiguration<DrugCategory>
{
    public void Configure(EntityTypeBuilder<DrugCategory> builder)
    {
        builder.ToTable("DrugCategories");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.HasOne(e => e.Parent).WithMany(e => e.Children).HasForeignKey(e => e.ParentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class DrugConfiguration : IEntityTypeConfiguration<Drug>
{
    public void Configure(EntityTypeBuilder<Drug> builder)
    {
        builder.ToTable("Drugs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Code).HasMaxLength(50).IsRequired();
        builder.HasIndex(e => e.Code).IsUnique().HasFilter("\"IsDeleted\" = false");
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.GenericName).HasMaxLength(200);
        builder.Property(e => e.Specification).HasMaxLength(100);
        builder.Property(e => e.Manufacturer).HasMaxLength(200);
        builder.Property(e => e.ApprovalNumber).HasMaxLength(100);
        builder.Property(e => e.DosageForm).HasMaxLength(50);
        builder.Property(e => e.Unit).HasMaxLength(20).IsRequired();
        builder.HasOne(e => e.Category).WithMany(e => e.Drugs).HasForeignKey(e => e.CategoryId).OnDelete(DeleteBehavior.SetNull);
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class CustomerCategoryConfiguration : IEntityTypeConfiguration<CustomerCategory>
{
    public void Configure(EntityTypeBuilder<CustomerCategory> builder)
    {
        builder.ToTable("CustomerCategories");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.HasOne(e => e.Parent).WithMany(e => e.Children).HasForeignKey(e => e.ParentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Code).HasMaxLength(50).IsRequired();
        builder.HasIndex(e => e.Code).IsUnique().HasFilter("\"IsDeleted\" = false");
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.ShortName).HasMaxLength(100);
        builder.Property(e => e.ContactPerson).HasMaxLength(50);
        builder.Property(e => e.Phone).HasMaxLength(50);
        builder.Property(e => e.Address).HasMaxLength(300);
        builder.Property(e => e.DrugName).HasMaxLength(200);
        builder.HasOne(e => e.Category).WithMany(e => e.Customers).HasForeignKey(e => e.CategoryId).OnDelete(DeleteBehavior.SetNull);
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("Suppliers");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Code).HasMaxLength(50).IsRequired();
        builder.HasIndex(e => e.Code).IsUnique().HasFilter("\"IsDeleted\" = false");
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.ContactPerson).HasMaxLength(50);
        builder.Property(e => e.Phone).HasMaxLength(50);
        builder.Property(e => e.Address).HasMaxLength(300);
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.ToTable("PurchaseOrders");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.OrderNo).HasMaxLength(50).IsRequired();
        builder.HasIndex(e => e.OrderNo).IsUnique();
        builder.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
        builder.Property(e => e.Operator).HasMaxLength(50);
        builder.Property(e => e.Auditor).HasMaxLength(50);
        builder.Property(e => e.Remark).HasMaxLength(500);
        builder.HasOne(e => e.Supplier).WithMany().HasForeignKey(e => e.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.Items).WithOne(e => e.PurchaseOrder).HasForeignKey(e => e.PurchaseOrderId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PurchaseOrderItemConfiguration : IEntityTypeConfiguration<PurchaseOrderItem>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderItem> builder)
    {
        builder.ToTable("PurchaseOrderItems");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.BatchNo).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)");
        builder.Property(e => e.TotalPrice).HasColumnType("decimal(18,2)");
        builder.HasOne(e => e.Drug).WithMany().HasForeignKey(e => e.DrugId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class SalesOrderConfiguration : IEntityTypeConfiguration<SalesOrder>
{
    public void Configure(EntityTypeBuilder<SalesOrder> builder)
    {
        builder.ToTable("SalesOrders");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.OrderNo).HasMaxLength(50).IsRequired();
        builder.HasIndex(e => e.OrderNo).IsUnique();
        builder.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
        builder.Property(e => e.Operator).HasMaxLength(50);
        builder.Property(e => e.Auditor).HasMaxLength(50);
        builder.Property(e => e.Remark).HasMaxLength(500);
        builder.HasOne(e => e.Customer).WithMany().HasForeignKey(e => e.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.Items).WithOne(e => e.SalesOrder).HasForeignKey(e => e.SalesOrderId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class SalesOrderItemConfiguration : IEntityTypeConfiguration<SalesOrderItem>
{
    public void Configure(EntityTypeBuilder<SalesOrderItem> builder)
    {
        builder.ToTable("SalesOrderItems");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.BatchNo).HasMaxLength(100).IsRequired();
        builder.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)");
        builder.Property(e => e.TotalPrice).HasColumnType("decimal(18,2)");
        builder.HasOne(e => e.Drug).WithMany().HasForeignKey(e => e.DrugId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class InventoryConfiguration : IEntityTypeConfiguration<Inventory>
{
    public void Configure(EntityTypeBuilder<Inventory> builder)
    {
        builder.ToTable("Inventories");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.BatchNo).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Location).HasMaxLength(100);
        builder.HasOne(e => e.Drug).WithMany(e => e.Inventories).HasForeignKey(e => e.DrugId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.Customer).WithMany().HasForeignKey(e => e.CustomerId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(e => new { e.DrugId, e.BatchNo, e.CustomerId }).IsUnique();
    }
}

public class DistributionRecordConfiguration : IEntityTypeConfiguration<DistributionRecord>
{
    public void Configure(EntityTypeBuilder<DistributionRecord> builder)
    {
        builder.ToTable("DistributionRecords");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.BatchNo).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Operator).HasMaxLength(50);
        builder.Property(e => e.SalesPerson).HasMaxLength(50);
        builder.Property(e => e.DeliveryOrderNo).HasMaxLength(100);
        builder.Property(e => e.Quantity).HasColumnType("decimal(18,2)");
        builder.Property(e => e.TaxedUnitPrice).HasColumnType("decimal(18,2)");
        builder.HasOne(e => e.Drug).WithMany().HasForeignKey(e => e.DrugId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Supplier).WithMany().HasForeignKey(e => e.SupplierId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.FromCustomer).WithMany().HasForeignKey(e => e.FromCustomerId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.ToCustomer).WithMany().HasForeignKey(e => e.ToCustomerId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(e => e.SalesOrder).WithMany().HasForeignKey(e => e.SalesOrderId).OnDelete(DeleteBehavior.SetNull);
        builder.Property(e => e.ReviewStatus).HasMaxLength(20);
        builder.Property(e => e.RejectReason).HasMaxLength(500);
        builder.HasOne(e => e.Reviewer).WithMany().HasForeignKey(e => e.ReviewerId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Username).HasMaxLength(50).IsRequired();
        builder.HasIndex(e => e.Username).IsUnique();
        builder.Property(e => e.PasswordHash).HasMaxLength(200).IsRequired();
        builder.Property(e => e.DisplayName).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Phone).HasMaxLength(20);
        builder.Property(e => e.Email).HasMaxLength(100);
        builder.HasOne(e => e.Role).WithMany(e => e.Users).HasForeignKey(e => e.RoleId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(50).IsRequired();
        builder.HasIndex(e => e.Name).IsUnique();
        builder.Property(e => e.Description).HasMaxLength(200);
        builder.Property(e => e.Permissions).HasColumnType("text");
    }
}

public class SystemConfigConfiguration : IEntityTypeConfiguration<SystemConfig>
{
    public void Configure(EntityTypeBuilder<SystemConfig> builder)
    {
        builder.ToTable("SystemConfigs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Key).HasMaxLength(100).IsRequired();
        builder.HasIndex(e => e.Key).IsUnique();
        builder.Property(e => e.Value).HasMaxLength(500).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(200);
        builder.Property(e => e.Group).HasMaxLength(50);
    }
}

public class CollectionSourceConfiguration : IEntityTypeConfiguration<CollectionSource>
{
    public void Configure(EntityTypeBuilder<CollectionSource> builder)
    {
        builder.ToTable("CollectionSources");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.BaseUrl).HasMaxLength(500).IsRequired();
        builder.Property(e => e.AuthConfig).HasColumnType("text");
        builder.Property(e => e.ExportConfig).HasColumnType("text");
        builder.Property(e => e.TransformConfig).HasColumnType("text");
        builder.Property(e => e.ScheduleCron).HasMaxLength(100);
        builder.Property(e => e.LastCollectionStatus).HasMaxLength(50);
        builder.HasOne(e => e.Rule).WithMany().HasForeignKey(e => e.RuleId).OnDelete(DeleteBehavior.SetNull);
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class CollectionTaskConfiguration : IEntityTypeConfiguration<CollectionTask>
{
    public void Configure(EntityTypeBuilder<CollectionTask> builder)
    {
        builder.ToTable("CollectionTasks");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.ErrorMessage).HasMaxLength(2000);
        builder.HasOne(e => e.Source).WithMany(e => e.Tasks)
            .HasForeignKey(e => e.SourceId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ImportRuleConfiguration : IEntityTypeConfiguration<ImportRule>
{
    public void Configure(EntityTypeBuilder<ImportRule> builder)
    {
        builder.ToTable("ImportRules");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(500);
        builder.HasMany(e => e.ModuleConfigs).WithOne(e => e.Rule)
            .HasForeignKey(e => e.RuleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class ModuleRuleConfigConfiguration : IEntityTypeConfiguration<ModuleRuleConfig>
{
    public void Configure(EntityTypeBuilder<ModuleRuleConfig> builder)
    {
        builder.ToTable("ModuleRuleConfigs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.ModuleName).HasMaxLength(100).IsRequired();
        builder.Property(e => e.FieldMappings).HasColumnType("text").IsRequired();
        builder.Property(e => e.NormalizeRules).HasColumnType("text");
        builder.Property(e => e.MatchConfig).HasColumnType("text");
    }
}

public class CollectionRecordConfiguration : IEntityTypeConfiguration<CollectionRecord>
{
    public void Configure(EntityTypeBuilder<CollectionRecord> builder)
    {
        builder.ToTable("CollectionRecords");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.RawDataJson).HasColumnType("text").IsRequired();
        builder.Property(e => e.CleanedDataJson).HasColumnType("text");
        builder.Property(e => e.TargetEntityType).HasMaxLength(100);
        builder.Property(e => e.RejectReason).HasMaxLength(500);
        builder.Property(e => e.ErrorMessage).HasMaxLength(2000);
        builder.HasOne(e => e.Task).WithMany(e => e.Records)
            .HasForeignKey(e => e.TaskId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.Source).WithMany(e => e.Records)
            .HasForeignKey(e => e.SourceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Reviewer).WithMany()
            .HasForeignKey(e => e.ReviewerId).OnDelete(DeleteBehavior.SetNull);
    }
}
