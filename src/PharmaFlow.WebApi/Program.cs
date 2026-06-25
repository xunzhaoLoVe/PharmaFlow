using Microsoft.EntityFrameworkCore;
using PharmaFlow.Application.Services;
using PharmaFlow.Infrastructure.Data;
using PharmaFlow.WebApi.Middleware;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddScoped<AppConfigService>();

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
    if (!db.Users.Any())
    {
        var adminRole = new PharmaFlow.Domain.Entities.Role
        {
            Name = "管理员",
            Description = "系统管理员",
            Permissions = System.Text.Json.JsonSerializer.Serialize(new[] { "*" })
        };
        db.Roles.Add(adminRole);
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes("admin123"));
        var hash = Convert.ToHexString(bytes).ToLower();
        db.Users.Add(new PharmaFlow.Domain.Entities.User
        {
            Username = "admin",
            PasswordHash = hash,
            DisplayName = "管理员",
            IsActive = true,
            Role = adminRole
        });
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
