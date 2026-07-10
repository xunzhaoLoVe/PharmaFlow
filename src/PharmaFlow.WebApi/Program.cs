using Microsoft.EntityFrameworkCore;
using PharmaFlow.Application.Services;
using PharmaFlow.Infrastructure.Data;
using PharmaFlow.WebApi.HostedServices;
using PharmaFlow.WebApi.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// ── Encoding ──
System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

// ── Database ──
var dbProvider = builder.Configuration.GetValue<string>("DatabaseProvider", "SQLite");
var connStr = (dbProvider != null ? builder.Configuration.GetConnectionString(dbProvider) : null)
              ?? builder.Configuration.GetConnectionString("SQLite")
              ?? "Data Source=PharmaFlow.db";

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (dbProvider == "PostgreSQL")
        options.UseNpgsql(connStr);
    else
        options.UseSqlite(connStr);
});

// ── HttpClient ──
builder.Services.AddHttpClient("CollectionClient", client =>
{
    client.DefaultRequestHeaders.Add("User-Agent",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
    client.Timeout = TimeSpan.FromSeconds(60);
});

// ── Services ──
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IDrugService, DrugService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<ISupplierService, SupplierService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IDistributionService, DistributionService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IPurchaseService, PurchaseService>();
builder.Services.AddScoped<ISalesService, SalesService>();
builder.Services.AddScoped<ICollectionService, CollectionService>();
builder.Services.AddScoped<DataCleaner>();
builder.Services.AddScoped<SmartDataCleaner>();
builder.Services.AddScoped<SpecNormalizer>();
builder.Services.AddScoped<IImportRuleService, ImportRuleService>();
builder.Services.AddScoped<BatchProcessor>();
builder.Services.AddScoped<CollectionExecutionService>();
builder.Services.AddScoped<AppConfigService>();

// ── Background Services ──
builder.Services.AddHostedService<CollectionSchedulerHostedService>();

// ── MVC + Swagger + CORS ──
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// ── DB Init ──
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    try
    {
        using var cmd = db.Database.GetDbConnection().CreateCommand();
        db.Database.GetDbConnection().Open();
        cmd.CommandText = "SELECT COUNT(*) FROM pragma_table_info('DistributionRecords') WHERE name = 'Province'";
        if ((long)cmd.ExecuteScalar()! == 0)
        {
            cmd.CommandText = "ALTER TABLE DistributionRecords ADD COLUMN Province TEXT NULL";
            cmd.ExecuteNonQuery();
        }
        cmd.CommandText = "SELECT COUNT(*) FROM pragma_table_info('DistributionRecords') WHERE name = 'DistributionDateDisplay'";
        if ((long)cmd.ExecuteScalar()! == 0)
        {
            cmd.CommandText = "ALTER TABLE DistributionRecords ADD COLUMN DistributionDateDisplay TEXT NULL";
            cmd.ExecuteNonQuery();
        }
    }
    catch { /* ignore */ }
    finally { db.Database.GetDbConnection().Close(); }

    // Excel 导入批次字段：用于按每次导入管理销售/采购流向
    try
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) conn.Open();
        using var batchCmd = conn.CreateCommand();
        if (dbProvider == "PostgreSQL")
        {
            batchCmd.CommandText = @"ALTER TABLE ""DistributionRecords"" ADD COLUMN IF NOT EXISTS ""ImportBatchId"" uuid NULL";
            batchCmd.ExecuteNonQuery();
            batchCmd.CommandText = @"ALTER TABLE ""DistributionRecords"" ADD COLUMN IF NOT EXISTS ""ImportBatchName"" character varying(200) NULL";
            batchCmd.ExecuteNonQuery();
        }
        else
        {
            batchCmd.CommandText = "SELECT COUNT(*) FROM pragma_table_info('DistributionRecords') WHERE name = 'ImportBatchId'";
            if ((long)batchCmd.ExecuteScalar()! == 0)
            {
                batchCmd.CommandText = "ALTER TABLE DistributionRecords ADD COLUMN ImportBatchId TEXT NULL";
                batchCmd.ExecuteNonQuery();
            }
            batchCmd.CommandText = "SELECT COUNT(*) FROM pragma_table_info('DistributionRecords') WHERE name = 'ImportBatchName'";
            if ((long)batchCmd.ExecuteScalar()! == 0)
            {
                batchCmd.CommandText = "ALTER TABLE DistributionRecords ADD COLUMN ImportBatchName TEXT NULL";
                batchCmd.ExecuteNonQuery();
            }
        }
        conn.Close();
    }
    catch { /* ignore */ }

    // Excel 导入明细表：用于查询每行导入成功/重复/无效情况
    try
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) conn.Open();
        using var detailCmd = conn.CreateCommand();
        if (dbProvider == "PostgreSQL")
        {
            detailCmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS ""DistributionImportDetails"" (
                    ""Id"" uuid NOT NULL,
                    ""ImportBatchId"" uuid NOT NULL,
                    ""ImportBatchName"" character varying(200) NOT NULL,
                    ""IsPurchase"" boolean NOT NULL,
                    ""RowNumber"" integer NOT NULL,
                    ""Status"" character varying(20) NOT NULL,
                    ""Reason"" character varying(500) NULL,
                    ""DrugName"" character varying(200) NULL,
                    ""Specification"" character varying(100) NULL,
                    ""BatchNo"" character varying(100) NULL,
                    ""FromCustomerName"" character varying(200) NULL,
                    ""ToCustomerName"" character varying(200) NULL,
                    ""Quantity"" numeric(18,2) NOT NULL,
                    ""DistributionDate"" character varying(50) NULL,
                    ""Province"" character varying(50) NULL,
                    ""CreatedAt"" timestamptz NOT NULL,
                    ""UpdatedAt"" timestamptz NULL,
                    CONSTRAINT ""PK_DistributionImportDetails"" PRIMARY KEY (""Id"")
                );";
            detailCmd.ExecuteNonQuery();
            detailCmd.CommandText = @"CREATE INDEX IF NOT EXISTS ""IX_DistributionImportDetails_ImportBatchId"" ON ""DistributionImportDetails"" (""ImportBatchId"");";
            detailCmd.ExecuteNonQuery();
        }
        else
        {
            detailCmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS DistributionImportDetails (
                    Id TEXT NOT NULL PRIMARY KEY,
                    ImportBatchId TEXT NOT NULL,
                    ImportBatchName TEXT NOT NULL,
                    IsPurchase INTEGER NOT NULL,
                    RowNumber INTEGER NOT NULL,
                    Status TEXT NOT NULL,
                    Reason TEXT NULL,
                    DrugName TEXT NULL,
                    Specification TEXT NULL,
                    BatchNo TEXT NULL,
                    FromCustomerName TEXT NULL,
                    ToCustomerName TEXT NULL,
                    Quantity TEXT NOT NULL,
                    DistributionDate TEXT NULL,
                    Province TEXT NULL,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT NULL
                );";
            detailCmd.ExecuteNonQuery();
            detailCmd.CommandText = "CREATE INDEX IF NOT EXISTS IX_DistributionImportDetails_ImportBatchId ON DistributionImportDetails (ImportBatchId);";
            detailCmd.ExecuteNonQuery();
        }
        conn.Close();
    }
    catch { /* ignore */ }

    // 采集同步追踪表：EnsureCreated 不会为已存在的数据库创建新表，需按数据库类型手动建表
    try
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) conn.Open();
        using var createCmd = conn.CreateCommand();
        if (dbProvider == "PostgreSQL")
        {
            createCmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS ""CollectionSyncEntities"" (
                    ""Id"" uuid NOT NULL,
                    ""EntityType"" text NOT NULL,
                    ""EntityId"" uuid NOT NULL,
                    ""SourceId"" uuid NULL,
                    ""TaskId"" uuid NULL,
                    ""CreatedAt"" timestamptz NOT NULL,
                    ""UpdatedAt"" timestamptz NULL,
                    CONSTRAINT ""PK_CollectionSyncEntities"" PRIMARY KEY (""Id"")
                );";
            createCmd.ExecuteNonQuery();
        }
        else
        {
            createCmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS ""CollectionSyncEntities"" (
                    ""Id"" TEXT NOT NULL,
                    ""EntityType"" TEXT NOT NULL,
                    ""EntityId"" TEXT NOT NULL,
                    ""SourceId"" TEXT NULL,
                    ""TaskId"" TEXT NULL,
                    ""CreatedAt"" TEXT NOT NULL,
                    ""UpdatedAt"" TEXT NULL,
                    CONSTRAINT ""PK_CollectionSyncEntities"" PRIMARY KEY (""Id"")
                );";
            createCmd.ExecuteNonQuery();
        }

        foreach (var idx in new[]
        {
            @"CREATE INDEX IF NOT EXISTS ""IX_CSE_EntityType_EntityId"" ON ""CollectionSyncEntities"" (""EntityType"", ""EntityId"")",
            @"CREATE INDEX IF NOT EXISTS ""IX_CSE_SourceId"" ON ""CollectionSyncEntities"" (""SourceId"")",
            @"CREATE INDEX IF NOT EXISTS ""IX_CSE_TaskId"" ON ""CollectionSyncEntities"" (""TaskId"")",
        })
        {
            createCmd.CommandText = idx;
            createCmd.ExecuteNonQuery();
        }
        conn.Close();
    }
    catch { /* ignore */ }

    // 经销商机构角色列：EnsureCreated 不会为已有 Customers 表自动补列
    try
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) conn.Open();
        using var roleCmd = conn.CreateCommand();
        if (dbProvider == "PostgreSQL")
        {
            roleCmd.CommandText = @"ALTER TABLE ""Customers"" ADD COLUMN IF NOT EXISTS ""DistributorRole"" integer NULL";
            roleCmd.ExecuteNonQuery();
        }
        else
        {
            roleCmd.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Customers') WHERE name = 'DistributorRole'";
            if ((long)roleCmd.ExecuteScalar()! == 0)
            {
                roleCmd.CommandText = "ALTER TABLE Customers ADD COLUMN DistributorRole INTEGER NULL";
                roleCmd.ExecuteNonQuery();
            }
        }
        conn.Close();
    }
    catch { /* ignore */ }

    // 终端机构类型归并：卫生院/卫生服务中心/卫生室/卫生服务站 → 基层医疗机构，药房 → 药店，门诊 → 诊所，二级经销商 → 商业
    try
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) conn.Open();
        using var normalizeCmd = conn.CreateCommand();
        var customerTypeColumn = dbProvider == "PostgreSQL" ? "\"CustomerType\"" : "CustomerType";
        var customersTable = dbProvider == "PostgreSQL" ? "\"Customers\"" : "Customers";
        foreach (var sql in new[]
        {
            $"UPDATE {customersTable} SET {customerTypeColumn} = 4 WHERE {customerTypeColumn} IN (6, 7, 10)",
            $"UPDATE {customersTable} SET {customerTypeColumn} = 1 WHERE {customerTypeColumn} = 8",
            $"UPDATE {customersTable} SET {customerTypeColumn} = 2 WHERE {customerTypeColumn} = 9",
            $"UPDATE {customersTable} SET {customerTypeColumn} = 3 WHERE {customerTypeColumn} = 11",
        })
        {
            normalizeCmd.CommandText = sql;
            normalizeCmd.ExecuteNonQuery();
        }
        conn.Close();
    }
    catch { /* ignore */ }

    EnsureEssentialData(db);
}

// ── Middleware Pipeline ──
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseMiddleware<JwtMiddleware>();
app.MapControllers();

// 根路径重定向到 Swagger
app.MapGet("/", () => Results.Redirect("/swagger"));

app.Run();

// ── 种子数据 ──
static void EnsureEssentialData(AppDbContext db)
{
    // 按名称逐个检查，避免新增角色被跳过
    void EnsureRole(string name, string desc, string[] perms)
    {
        if (!db.Roles.Any(r => r.Name == name))
        {
            db.Roles.Add(new PharmaFlow.Domain.Entities.Role
            {
                Name = name,
                Description = desc,
                Permissions = System.Text.Json.JsonSerializer.Serialize(perms)
            });
        }
    }
    EnsureRole("管理员", "全部权限", new[] { "*" });
    EnsureRole("操作员", "日常操作权限", new[] {
        "dashboard", "drugs", "customers", "purchases", "sales",
        "inventory", "distribution", "reports", "datacollection"
    });
    EnsureRole("只读用户", "仅查看权限", new[] {
        "dashboard", "drugs", "customers", "purchases", "sales",
        "inventory", "distribution", "reports", "datacollection"
    });
    db.SaveChanges();
    if (!db.Users.Any(u => u.Username == "admin"))
    {
        var adminRole = db.Roles.First(r => r.Name == "管理员");
        var adminSalt = AuthService.GenerateSalt();
        var adminHash = AuthService.HashPassword("admin123", adminSalt);
        db.Users.Add(new PharmaFlow.Domain.Entities.User
        {
            Username = "admin",
            PasswordHash = adminHash,
            PasswordSalt = adminSalt,
            DisplayName = "管理员",
            IsActive = true,
            Role = adminRole
        });
        db.SaveChanges();
    }
    if (!db.Customers.Any(c => c.Code == "SELF"))
    {
        db.Customers.Add(new PharmaFlow.Domain.Entities.Customer
        {
            Code = "SELF",
            Name = "本公司(制药厂)",
            Province = "北京市",
            Phone = "400-8888888",
            CustomerType = PharmaFlow.Domain.Enums.CustomerType.商业
        });
    }
    db.SaveChanges();
}
