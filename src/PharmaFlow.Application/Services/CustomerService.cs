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

    private static CustomerDto MapCustomerToDto(Customer c) => new()
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
        DistributorRole = (int?)c.DistributorRole,
        DistributorRoleName = c.DistributorRole.HasValue ? c.DistributorRole.Value.ToString() : "",
    };

    private static DistributorRole? ResolveDistributorRole(CustomerType customerType, int? roleValue, string? roleName)
    {
        if (customerType != CustomerType.商业)
            return null;

        if (roleValue.HasValue && Enum.IsDefined(typeof(DistributorRole), roleValue.Value))
            return (DistributorRole)roleValue.Value;

        return Enum.TryParse<DistributorRole>(roleName, out var role) ? role : null;
    }

    private async Task<Customer?> FindMatchingCustomerAsync(
        string name,
        string? drugName,
        string? province,
        CustomerType customerType,
        DistributorLevel? level,
        DistributorRole? distributorRole)
    {
        var customers = await _db.Customers.Where(c => !c.IsDeleted && c.Code != "SELF").ToListAsync();
        var key = customerType == CustomerType.商业
            ? MasterDataMatchHelper.DealerCustomerKey(name, drugName, province, distributorRole, level)
            : MasterDataMatchHelper.EndCustomerKey(name, drugName, province, customerType);

        return customers.FirstOrDefault(c => customerType == CustomerType.商业
            ? MasterDataMatchHelper.DealerCustomerKey(c.Name, c.DrugName, c.Province, c.DistributorRole, c.Level) == key
            : MasterDataMatchHelper.EndCustomerKey(c.Name, c.DrugName, c.Province, c.CustomerType) == key);
    }

    private static bool SupplementCustomer(Customer target, CustomerDto source)
    {
        var changed = false;
        if (string.IsNullOrWhiteSpace(target.ShortName) && !string.IsNullOrWhiteSpace(source.ShortName)) { target.ShortName = source.ShortName; changed = true; }
        if (string.IsNullOrWhiteSpace(target.ContactPerson) && !string.IsNullOrWhiteSpace(source.ContactPerson)) { target.ContactPerson = source.ContactPerson; changed = true; }
        if (string.IsNullOrWhiteSpace(target.Phone) && !string.IsNullOrWhiteSpace(source.Phone)) { target.Phone = source.Phone; changed = true; }
        if (string.IsNullOrWhiteSpace(target.Address) && !string.IsNullOrWhiteSpace(source.Address)) { target.Address = source.Address; changed = true; }
        if (string.IsNullOrWhiteSpace(target.Province) && !string.IsNullOrWhiteSpace(source.Province)) { target.Province = source.Province; changed = true; }
        if (string.IsNullOrWhiteSpace(target.SalesPerson) && !string.IsNullOrWhiteSpace(source.SalesPerson)) { target.SalesPerson = source.SalesPerson; changed = true; }
        if (string.IsNullOrWhiteSpace(target.DrugName) && !string.IsNullOrWhiteSpace(source.DrugName)) { target.DrugName = source.DrugName; changed = true; }
        if (!target.CategoryId.HasValue && source.CategoryId.HasValue) { target.CategoryId = source.CategoryId; changed = true; }
        if (!target.Level.HasValue && source.Level.HasValue) { target.Level = (DistributorLevel?)source.Level.Value; changed = true; }
        var role = ResolveDistributorRole(target.CustomerType, source.DistributorRole, source.DistributorRoleName);
        if (!target.DistributorRole.HasValue && role.HasValue) { target.DistributorRole = role; changed = true; }
        if (changed) target.UpdatedAt = DateTime.UtcNow;
        return changed;
    }

    private static bool CompatibleText(string? existingValue, string? incomingValue)
    {
        var existingBlank = string.IsNullOrWhiteSpace(existingValue);
        var incomingBlank = string.IsNullOrWhiteSpace(incomingValue);
        return existingBlank || incomingBlank || MasterDataMatchHelper.SameText(existingValue, incomingValue);
    }

    private static bool CompatibleNullable<T>(T? existingValue, T? incomingValue) where T : struct
        => !existingValue.HasValue || !incomingValue.HasValue || EqualityComparer<T>.Default.Equals(existingValue.Value, incomingValue.Value);

    private static bool IsSupplementMatch(
        Customer existing,
        string name,
        string? drugName,
        string? province,
        CustomerType customerType,
        DistributorLevel? level,
        DistributorRole? distributorRole)
    {
        if (!MasterDataMatchHelper.SameText(existing.Name, name)) return false;
        if (existing.CustomerType != customerType) return false;
        if (!CompatibleText(existing.DrugName, drugName)) return false;
        if (!CompatibleText(existing.Province, province)) return false;

        return (int)customerType != 3
            || (CompatibleNullable(existing.Level, level) && CompatibleNullable(existing.DistributorRole, distributorRole));
    }

    private static string BuildCustomerKey(Customer c, bool isEndCustomer)
        => isEndCustomer
            ? MasterDataMatchHelper.EndCustomerKey(c.Name, c.DrugName, c.Province, c.CustomerType)
            : MasterDataMatchHelper.DealerCustomerKey(c.Name, c.DrugName, c.Province, c.DistributorRole, c.Level);

    private static string BuildCustomerKey(CustomerDto dto, bool isEndCustomer, CustomerType customerType, DistributorRole? distributorRole)
        => isEndCustomer
            ? MasterDataMatchHelper.EndCustomerKey(dto.Name, dto.DrugName, dto.Province, customerType)
            : MasterDataMatchHelper.DealerCustomerKey(dto.Name, dto.DrugName, dto.Province, distributorRole, dto.Level.HasValue ? (DistributorLevel?)dto.Level.Value : null);

    private static Customer? FindSupplementMatch(
        List<Customer> customers,
        CustomerDto dto,
        bool isEndCustomer,
        CustomerType customerType,
        DistributorRole? distributorRole)
        => customers.FirstOrDefault(c => isEndCustomer == ((int)c.CustomerType != 3)
            && IsSupplementMatch(c, dto.Name, dto.DrugName, dto.Province, customerType,
                dto.Level.HasValue ? (DistributorLevel?)dto.Level.Value : null,
                distributorRole));

    private async Task AdoptCollectionCreatedMasterDataAsync(string entityType, Guid entityId)
    {
        var trackingRows = await _db.CollectionSyncEntities
            .Where(e => e.EntityType == entityType && e.EntityId == entityId)
            .ToListAsync();
        if (trackingRows.Count > 0)
            _db.CollectionSyncEntities.RemoveRange(trackingRows);
    }

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
            Items = items.Select(MapCustomerToDto).ToList(),
            TotalCount = total, PageIndex = pageIndex, PageSize = pageSize,
        };
    }

    public async Task<CustomerDto?> GetByIdAsync(Guid id)
    {
        var c = await _db.Customers.Include(x => x.Category).FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return null;
        return MapCustomerToDto(c);
    }

    public async Task<CustomerDto> CreateAsync(CustomerDto dto)
    {
        var customerType = Enum.TryParse<CustomerType>(dto.CustomerTypeName, out var ct) ? ct : CustomerType.医院;
        var level = dto.Level.HasValue ? (DistributorLevel?)dto.Level.Value : null;
        var distributorRole = ResolveDistributorRole(customerType, dto.DistributorRole, dto.DistributorRoleName);
        var existing = await FindMatchingCustomerAsync(dto.Name, dto.DrugName, dto.Province, customerType, level, distributorRole);
        if (existing != null)
        {
            SupplementCustomer(existing, dto);
            await AdoptCollectionCreatedMasterDataAsync("Customer", existing.Id);
            existing.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return MapCustomerToDto(existing);
        }

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
            CustomerType = customerType,
            CategoryId = dto.CategoryId,
            Level = level,
            DistributorRole = distributorRole,
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
        return MapCustomerToDto(entity);
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
        c.DistributorRole = ResolveDistributorRole(c.CustomerType, dto.DistributorRole, dto.DistributorRoleName);
        c.UpdatedAt = DateTime.UtcNow;
        await AdoptCollectionCreatedMasterDataAsync("Customer", c.Id);
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
        return MapCustomerToDto(c);
    }

    /// <summary>批量导入客户（按组合键去重，相同则跳过，不同则新增）</summary>
    public async Task<(int imported, int supplemented, int skipped)> ImportBatchAsync(List<CustomerDto> dtos, bool isEndCustomer = false)
    {
        if (dtos.Count == 0) return (0, 0, 0);

        // 1. 加载已有客户，构建标准化去重集合
        var existingCustomers = await _db.Customers
            .Where(c => !c.IsDeleted && c.Code != "SELF")
            .ToListAsync();

        var existingSet = new HashSet<string>(
            existingCustomers.Select(c => BuildCustomerKey(c, isEndCustomer)),
            StringComparer.OrdinalIgnoreCase);

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

        var imported = 0; var skipped = 0; var supplemented = 0;

        foreach (var dto in dtos)
        {
            if (string.IsNullOrWhiteSpace(dto.Name)) { skipped++; continue; }

            var customerType = Enum.TryParse<CustomerType>(dto.CustomerTypeName, out var ct) ? ct : (CustomerType)3;
            var distributorRole = ResolveDistributorRole(customerType, dto.DistributorRole, dto.DistributorRoleName);
            var key = BuildCustomerKey(dto, isEndCustomer, customerType, distributorRole);

            // 组合键已存在（数据库或本批次已导入）→ 复用/补充，不新增
            if (!existingSet.Add(key))
            {
                var existing = existingCustomers.FirstOrDefault(c => string.Equals(BuildCustomerKey(c, isEndCustomer), key, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    if (SupplementCustomer(existing, dto)) supplemented++;
                    existingSet.Add(BuildCustomerKey(existing, isEndCustomer));
                    await AdoptCollectionCreatedMasterDataAsync("Customer", existing.Id);
                }
                skipped++;
                continue;
            }

            var supplementMatch = FindSupplementMatch(existingCustomers, dto, isEndCustomer, customerType, distributorRole);
            if (supplementMatch != null)
            {
                if (SupplementCustomer(supplementMatch, dto)) supplemented++;
                existingSet.Add(BuildCustomerKey(supplementMatch, isEndCustomer));
                await AdoptCollectionCreatedMasterDataAsync("Customer", supplementMatch.Id);
                skipped++;
                continue;
            }

            // 新增
            var code = dto.Code;
            if (string.IsNullOrWhiteSpace(code) || codeSet.Contains(code))
                code = NextCode();
            else
                codeSet.Add(code);

            var entity = new Customer
            {
                Code = code, Name = dto.Name, ShortName = dto.ShortName,
                ContactPerson = dto.ContactPerson, Phone = dto.Phone, Address = dto.Address,
                Province = dto.Province, SalesPerson = dto.SalesPerson, DrugName = dto.DrugName,
                CustomerType = customerType,
                CategoryId = dto.CategoryId,
                Level = dto.Level.HasValue ? (DistributorLevel?)dto.Level.Value : null,
                DistributorRole = distributorRole,
            };
            _db.Customers.Add(entity);
            existingCustomers.Add(entity);
            imported++;
        }

        await _db.SaveChangesAsync();
        return (imported, supplemented, skipped);
    }

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
        var resolvedRole = ResolveDistributorRole(c.CustomerType, source.DistributorRole, source.DistributorRoleName);
        if (resolvedRole.HasValue || c.CustomerType != CustomerType.商业) c.DistributorRole = resolvedRole;

        c.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }
}
