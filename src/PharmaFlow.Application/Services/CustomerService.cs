using Microsoft.EntityFrameworkCore;
using PharmaFlow.Application.DTOs;
using PharmaFlow.Domain.Entities;
using PharmaFlow.Domain.Enums;
using PharmaFlow.Infrastructure.Data;

namespace PharmaFlow.Application.Services;

public class CustomerService : ICustomerService
{
    private readonly AppDbContext _db;
    public CustomerService(AppDbContext db) => _db = db;

    public async Task<PagedResult<CustomerDto>> GetPagedAsync(int pageIndex, int pageSize, string? search = null, Guid? categoryId = null, string? customerType = null)
    {
        var query = _db.Customers.Include(c => c.Category)
            .Where(c => c.Code != "SELF")  // 本公司不在经销商管理中显示
            .AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => c.Name.Contains(search) || c.Code.Contains(search) || c.ContactPerson!.Contains(search));
        if (categoryId.HasValue) query = query.Where(c => c.CategoryId == categoryId);
        if (!string.IsNullOrWhiteSpace(customerType))
        {
            if (customerType == "非商业")
                query = query.Where(c => c.CustomerType != CustomerType.商业);
            else if (Enum.TryParse<CustomerType>(customerType, out var ct))
                query = query.Where(c => c.CustomerType == ct);
        }

        var total = await query.CountAsync();
        var items = await query.OrderBy(c => c.Code).Skip(pageIndex * pageSize).Take(pageSize).ToListAsync();

        return new PagedResult<CustomerDto>
        {
            Items = items.Select(c => new CustomerDto
            {
                Id = c.Id,
                Code = c.Code,
                Name = c.Name,
                ShortName = c.ShortName,
                ContactPerson = c.ContactPerson,
                Phone = c.Phone,
                Address = c.Address,
                Province = c.Province,
                SalesPerson = c.SalesPerson,
                DrugName = c.DrugName,
                CustomerTypeName = c.CustomerType.ToString(),
                CategoryName = c.Category?.Name,
                CategoryId = c.CategoryId,
                Level = (int?)c.Level,
                LevelName = c.Level.HasValue ? c.Level.Value.ToString() : "",
            }).ToList(),
            TotalCount = total, PageIndex = pageIndex, PageSize = pageSize,
        };
    }

    public async Task<CustomerDto?> GetByIdAsync(Guid id)
    {
        var c = await _db.Customers.Include(x => x.Category).FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return null;
        return new CustomerDto
        {
            Id = c.Id, Code = c.Code, Name = c.Name, ShortName = c.ShortName,
            ContactPerson = c.ContactPerson, Phone = c.Phone, Address = c.Address,
            Province = c.Province, SalesPerson = c.SalesPerson, DrugName = c.DrugName,
            CustomerTypeName = c.CustomerType.ToString(), CategoryName = c.Category?.Name, CategoryId = c.CategoryId,
            Level = (int?)c.Level,
            LevelName = c.Level.HasValue ? c.Level.Value.ToString() : "",
        };
    }

    public async Task<CustomerDto> CreateAsync(CustomerDto dto)
    {
        // Check if the code needs regeneration (empty or conflicts with existing records including soft-deleted)
        if (string.IsNullOrWhiteSpace(dto.Code) ||
            await _db.Customers.IgnoreQueryFilters().AnyAsync(c => c.Code == dto.Code))
        {
            var today = DateTime.UtcNow.ToString("yyyyMMdd");
            var prefix = "CUST" + today;
            var lastCode = await _db.Customers
                .IgnoreQueryFilters()
                .Where(c => c.Code.StartsWith(prefix))
                .OrderByDescending(c => c.Code)
                .Select(c => c.Code)
                .FirstOrDefaultAsync();
            int seq = 1;
            if (lastCode != null && int.TryParse(lastCode[^3..], out var lastSeq))
                seq = lastSeq + 1;

            // Loop until a truly unique code is found (belt-and-suspenders)
            string newCode;
            do
            {
                newCode = prefix + seq.ToString("D3");
                seq++;
            }
            while (await _db.Customers.IgnoreQueryFilters().AnyAsync(c => c.Code == newCode));

            dto.Code = newCode;
        }

        var entity = new Customer
        {
            Code = dto.Code, Name = dto.Name, ShortName = dto.ShortName,
            ContactPerson = dto.ContactPerson, Phone = dto.Phone, Address = dto.Address,
            Province = dto.Province, SalesPerson = dto.SalesPerson, DrugName = dto.DrugName,
            CustomerType = Enum.TryParse<CustomerType>(dto.CustomerTypeName, out var ct) ? ct : CustomerType.医院,
            CategoryId = dto.CategoryId,
            Level = dto.Level.HasValue ? (DistributorLevel?)dto.Level.Value : null,
        };
        _db.Customers.Add(entity);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch
        {
            // Detach the failed entity so it doesn't pollute subsequent SaveChanges calls
            _db.Entry(entity).State = EntityState.Detached;
            throw;
        }
        dto.Id = entity.Id;
        return dto;
    }

    public async Task UpdateAsync(CustomerDto dto)
    {
        var c = await _db.Customers.FindAsync(dto.Id);
        if (c == null) return;
        c.Code = dto.Code; c.Name = dto.Name; c.ShortName = dto.ShortName;
        c.ContactPerson = dto.ContactPerson; c.Phone = dto.Phone; c.Address = dto.Address;
        c.Province = dto.Province; c.SalesPerson = dto.SalesPerson; c.DrugName = dto.DrugName;
        c.CustomerType = Enum.TryParse<CustomerType>(dto.CustomerTypeName, out var ct) ? ct : c.CustomerType;
        c.CategoryId = dto.CategoryId;
        c.Level = dto.Level.HasValue ? (DistributorLevel?)dto.Level.Value : null;
        c.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var c = await _db.Customers.FindAsync(id);
        if (c != null) { c.IsDeleted = true; c.UpdatedAt = DateTime.UtcNow; await _db.SaveChangesAsync(); }
    }

    public async Task<int> ClearAllAsync(string customerType)
    {
        var query = _db.Customers.Where(c => c.Code != "SELF" && !c.IsDeleted);
        if (customerType == "商业")
            query = query.Where(c => c.CustomerType == CustomerType.商业);
        else if (customerType == "非商业")
            query = query.Where(c => c.CustomerType != CustomerType.商业);

        var now = DateTime.UtcNow;
        var count = await query.ExecuteUpdateAsync(s => s
            .SetProperty(c => c.IsDeleted, true)
            .SetProperty(c => c.UpdatedAt, now));
        return count;
    }

    public async Task<IReadOnlyList<DropdownItem>> GetDropdownAsync() =>
        await _db.Customers.Where(c => !c.IsDeleted && c.Code != "SELF").OrderBy(c => c.Name)
            .Select(c => new DropdownItem { Id = c.Id, Name = c.Name }).ToListAsync();

    public async Task<IReadOnlyList<DropdownItem>> GetDistributorDropdownAsync(DistributorLevel? level = null)
    {
        var query = _db.Customers.Where(c => !c.IsDeleted && c.CustomerType == CustomerType.商业 && c.Code != "SELF");
        if (level.HasValue)
            query = query.Where(c => c.Level == level.Value);
        return await query.OrderBy(c => c.Name)
            .Select(c => new DropdownItem { Id = c.Id, Name = c.Name }).ToListAsync();
    }

    public async Task<IReadOnlyList<DropdownItem>> GetEndCustomerDropdownAsync()
    {
        return await _db.Customers
            .Where(c => !c.IsDeleted && c.CustomerType != CustomerType.商业)
            .OrderBy(c => c.Name)
            .Select(c => new DropdownItem { Id = c.Id, Name = c.Name }).ToListAsync();
    }

    public async Task<IReadOnlyList<DropdownItem>> GetCategoriesAsync() =>
        await _db.CustomerCategories.OrderBy(c => c.Name)
            .Select(c => new DropdownItem { Id = c.Id, Name = c.Name }).ToListAsync();

    public async Task<DropdownItem> CreateCategoryAsync(string name, Guid? parentId)
    {
        var cat = new CustomerCategory { Name = name, ParentId = parentId };
        _db.CustomerCategories.Add(cat);
        await _db.SaveChangesAsync();
        return new DropdownItem { Id = cat.Id, Name = cat.Name };
    }

    public async Task<CustomerDto?> FindByNameAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var c = await _db.Customers.FirstOrDefaultAsync(x => !x.IsDeleted && x.Name == name);
        if (c == null) return null;
        return new CustomerDto
        {
            Id = c.Id, Code = c.Code, Name = c.Name, ShortName = c.ShortName,
            ContactPerson = c.ContactPerson, Phone = c.Phone, Address = c.Address,
            Province = c.Province, SalesPerson = c.SalesPerson, DrugName = c.DrugName,
            CustomerTypeName = c.CustomerType.ToString(), CategoryId = c.CategoryId,
            Level = (int?)c.Level,
            LevelName = c.Level.HasValue ? c.Level.Value.ToString() : "",
        };
    }

    /// <summary>批量导入客户（按组合键去重，相同则跳过，不同则新增）</summary>
    public async Task<(int imported, int supplemented, int skipped)> ImportBatchAsync(List<CustomerDto> dtos, bool isEndCustomer = false)
    {
        if (dtos.Count == 0) return (0, 0, 0);

        // 1. 加载已有客户，构建去重集合
        var names = dtos.Where(d => !string.IsNullOrWhiteSpace(d.Name)).Select(d => d.Name!).Distinct().ToList();
        var existingCustomers = await _db.Customers
            .Where(c => !c.IsDeleted && names.Contains(c.Name))
            .ToListAsync();

        var existingSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in existingCustomers)
        {
            var k = isEndCustomer
                ? EndCustKey(c.Name, c.DrugName, c.Province, c.CustomerType.ToString())
                : DealerKey(c.Name, c.DrugName, (int?)c.Level, c.Province);
            existingSet.Add(k);
        }

        // 2. 一次性查出所有已有编码（含软删除），用于生成新编码
        var allCodes = await _db.Customers.IgnoreQueryFilters()
            .Select(c => c.Code)
            .ToListAsync();
        var codeSet = new HashSet<string>(allCodes);

        var today = DateTime.UtcNow.ToString("yyyyMMdd");
        var prefix = "CUST" + today;
        int seq = allCodes
            .Where(c => c.StartsWith(prefix))
            .Select(c => { int.TryParse(c[^3..], out var s); return s; })
            .DefaultIfEmpty(0).Max() + 1;

        string NextCode()
        {
            string code;
            do { code = prefix + (seq++).ToString("D3"); }
            while (codeSet.Contains(code));
            codeSet.Add(code);
            return code;
        }

        var imported = 0; var skipped = 0;

        foreach (var dto in dtos)
        {
            if (string.IsNullOrWhiteSpace(dto.Name)) { skipped++; continue; }

            var key = isEndCustomer
                ? EndCustKey(dto.Name, dto.DrugName, dto.Province, dto.CustomerTypeName)
                : DealerKey(dto.Name, dto.DrugName, dto.Level, dto.Province);

            // 三者组合已存在（数据库或本批次已导入）→ 跳过
            if (!existingSet.Add(key))
            { skipped++; continue; }

            // 新增
            var code = dto.Code;
            if (string.IsNullOrWhiteSpace(code) || codeSet.Contains(code))
                code = NextCode();
            else
                codeSet.Add(code);

            _db.Customers.Add(new Customer
            {
                Code = code, Name = dto.Name, ShortName = dto.ShortName,
                ContactPerson = dto.ContactPerson, Phone = dto.Phone, Address = dto.Address,
                Province = dto.Province, SalesPerson = dto.SalesPerson, DrugName = dto.DrugName,
                CustomerType = Enum.TryParse<CustomerType>(dto.CustomerTypeName, out var ct) ? ct : CustomerType.商业,
                CategoryId = dto.CategoryId,
                Level = dto.Level.HasValue ? (DistributorLevel?)dto.Level.Value : null,
            });
            imported++;
        }

        await _db.SaveChangesAsync();
        return (imported, supplemented: 0, skipped);
    }

    private static string DealerKey(string name, string? drugName, int? level, string? province = null)
        => $"{name.Trim()}|{drugName?.Trim() ?? ""}|{province?.Trim() ?? ""}|{level}";

    private static string EndCustKey(string name, string? drugName, string? province, string? customerType)
        => $"{name.Trim()}|{drugName?.Trim() ?? ""}|{province?.Trim() ?? ""}|{customerType?.Trim() ?? ""}";

    /// <summary>补充更新：只将 source 中非空值写入 target（空值不覆盖已有数据）</summary>
    public async Task SupplementAsync(Guid id, CustomerDto source)
    {
        var c = await _db.Customers.FindAsync(id);
        if (c == null) return;

        if (!string.IsNullOrWhiteSpace(source.ShortName)) c.ShortName = source.ShortName;
        if (!string.IsNullOrWhiteSpace(source.ContactPerson)) c.ContactPerson = source.ContactPerson;
        if (!string.IsNullOrWhiteSpace(source.Phone)) c.Phone = source.Phone;
        if (!string.IsNullOrWhiteSpace(source.Address)) c.Address = source.Address;
        if (!string.IsNullOrWhiteSpace(source.Province)) c.Province = source.Province;
        if (!string.IsNullOrWhiteSpace(source.SalesPerson)) c.SalesPerson = source.SalesPerson;
        if (!string.IsNullOrWhiteSpace(source.DrugName))
        {
            if (!string.IsNullOrWhiteSpace(c.DrugName) && c.DrugName != source.DrugName && !c.DrugName.Contains(source.DrugName))
                c.DrugName = c.DrugName + "\n" + source.DrugName;
            else if (string.IsNullOrWhiteSpace(c.DrugName))
                c.DrugName = source.DrugName;
        }
        if (!string.IsNullOrWhiteSpace(source.CustomerTypeName) && Enum.TryParse<CustomerType>(source.CustomerTypeName, out var ct))
            c.CustomerType = ct;
        if (source.Level.HasValue) c.Level = (DistributorLevel?)source.Level.Value;

        c.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }
}
