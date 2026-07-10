using Microsoft.EntityFrameworkCore;
using PharmaFlow.Domain.Entities;
using PharmaFlow.Infrastructure.Data.EntityConfigurations;

namespace PharmaFlow.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<DrugCategory> DrugCategories => Set<DrugCategory>();
    public DbSet<Drug> Drugs => Set<Drug>();
    public DbSet<CustomerCategory> CustomerCategories => Set<CustomerCategory>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();
    public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();
    public DbSet<SalesOrderItem> SalesOrderItems => Set<SalesOrderItem>();
    public DbSet<Inventory> Inventories => Set<Inventory>();
    public DbSet<DistributionRecord> DistributionRecords => Set<DistributionRecord>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<SystemConfig> SystemConfigs => Set<SystemConfig>();
    public DbSet<CollectionSource> CollectionSources => Set<CollectionSource>();
    public DbSet<CollectionTask> CollectionTasks => Set<CollectionTask>();
    public DbSet<CollectionRecord> CollectionRecords => Set<CollectionRecord>();
    public DbSet<CollectionSyncEntity> CollectionSyncEntities => Set<CollectionSyncEntity>();
    public DbSet<ImportRule> ImportRules => Set<ImportRule>();
    public DbSet<ModuleRuleConfig> ModuleRuleConfigs => Set<ModuleRuleConfig>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        // 强制所有 DateTime 为 UTC，兼容 PostgreSQL
        configurationBuilder.Properties<DateTime>()
            .HaveConversion(typeof(UtcDateTimeConverter));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DrugConfiguration).Assembly);
    }
}
