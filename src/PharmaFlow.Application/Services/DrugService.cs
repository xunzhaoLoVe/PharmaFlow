using Microsoft.EntityFrameworkCore;
using PharmaFlow.Application.DTOs;
using PharmaFlow.Domain.Entities;
using PharmaFlow.Domain.Enums;
using PharmaFlow.Infrastructure.Data;

namespace PharmaFlow.Application.Services;

public class DrugService : IDrugService
{
    private static int _codeSeq;
    private readonly AppDbContext _db;

    public DrugService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<DrugDto>> GetPagedAsync(int pageIndex, int pageSize, string? search = null, Guid? categoryId = null)
    {
        var query = _db.Drugs.Include(d => d.Category).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(d => d.Name.Contains(search) || d.Code.Contains(search));
        if (categoryId.HasValue)
            query = query.Where(d => d.CategoryId == categoryId);

        var total = await query.CountAsync();
        var items = await query.OrderBy(d => d.Code).Skip(pageIndex * pageSize).Take(pageSize).ToListAsync();

        return new PagedResult<DrugDto>
        {
            Items = items.Select(d => new DrugDto
            {
                Id = d.Id,
                Code = d.Code,
                Name = d.Name,
                GenericName = d.GenericName,
                Specification = d.Specification,
                ApprovalNumber = d.ApprovalNumber,
                CategoryName = d.Category?.Name,
                CategoryId = d.CategoryId,
            }).ToList(),
            TotalCount = total,
            PageIndex = pageIndex,
            PageSize = pageSize,
        };
    }

    public async Task<DrugDto?> GetByIdAsync(Guid id)
    {
        var d = await _db.Drugs.Include(x => x.Category).FirstOrDefaultAsync(x => x.Id == id);
        if (d == null) return null;
        return new DrugDto
        {
            Id = d.Id,
            Code = d.Code,
            Name = d.Name,
            GenericName = d.GenericName,
            Specification = d.Specification,
            ApprovalNumber = d.ApprovalNumber,
            CategoryName = d.Category?.Name,
            CategoryId = d.CategoryId,
        };
    }

    public async Task<DrugDto> CreateAsync(DrugDto dto)
    {
        var existing = await FindActiveByNameAndSpecAsync(dto.Name, dto.Specification);
        if (existing != null)
        {
            SupplementDrug(existing, dto);
            await AdoptCollectionCreatedMasterDataAsync("Drug", existing.Id);
            existing.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return MapDrugToDto(existing);
        }

        // Auto-generate drug code if empty or if the provided code already exists (including soft-deleted)
        if (string.IsNullOrWhiteSpace(dto.Code) ||
            await _db.Drugs.IgnoreQueryFilters().AnyAsync(d => d.Code == dto.Code))
        {
            string newCode;
            do
            {
                var seq = Interlocked.Increment(ref _codeSeq);
                newCode = "DRUG" + DateTime.Now.ToString("yyyyMMddHHmmss") + seq.ToString("D4");
            }
            while (await _db.Drugs.IgnoreQueryFilters().AnyAsync(d => d.Code == newCode));
            dto.Code = newCode;
        }

        var drug = new Drug
        {
            Code = dto.Code,
            Name = dto.Name,
            GenericName = dto.GenericName,
            Specification = dto.Specification,
            ApprovalNumber = dto.ApprovalNumber,
            CategoryId = dto.CategoryId,
            Unit = string.IsNullOrWhiteSpace(dto.Unit) ? "盒" : dto.Unit,
            WarningLowQuantity = dto.WarningLowQuantity,
        };
        _db.Drugs.Add(drug);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch
        {
            _db.Entry(drug).State = EntityState.Detached;
            throw;
        }
        return MapDrugToDto(drug);
    }

    public async Task UpdateAsync(DrugDto dto)
    {
        var drug = await _db.Drugs.FindAsync(dto.Id);
        if (drug == null) return;
        drug.Code = dto.Code;
        drug.Name = dto.Name;
        drug.GenericName = dto.GenericName;
        drug.Specification = dto.Specification;
        drug.ApprovalNumber = dto.ApprovalNumber;
        drug.CategoryId = dto.CategoryId;
        drug.UpdatedAt = DateTime.UtcNow;
        await AdoptCollectionCreatedMasterDataAsync("Drug", drug.Id);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var drug = await _db.Drugs.FindAsync(id);
        if (drug != null)
        {
            drug.IsDeleted = true;
            drug.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    public async Task<IReadOnlyList<DropdownItem>> GetDropdownAsync()
    {
        return await _db.Drugs.Where(d => !d.IsDeleted).OrderBy(d => d.Name)
            .Select(d => new DropdownItem { Id = d.Id, Name = d.Name + " [" + d.Specification + "]" })
            .ToListAsync();
    }

    public async Task<IReadOnlyList<DropdownItem>> GetCategoriesAsync()
    {
        return await _db.DrugCategories.OrderBy(c => c.Name)
            .Select(c => new DropdownItem { Id = c.Id, Name = c.Name }).ToListAsync();
    }

    public async Task<DropdownItem> CreateCategoryAsync(string name, Guid? parentId)
    {
        var cat = new DrugCategory { Name = name, ParentId = parentId };
        _db.DrugCategories.Add(cat);
        await _db.SaveChangesAsync();
        return new DropdownItem { Id = cat.Id, Name = cat.Name };
    }

    private async Task<Drug?> FindActiveByNameAndSpecAsync(string name, string? spec)
    {
        var drugs = await _db.Drugs.Where(d => !d.IsDeleted).ToListAsync();
        var key = MasterDataMatchHelper.DrugKey(name, spec);
        return drugs.FirstOrDefault(d => MasterDataMatchHelper.DrugKey(d.Name, d.Specification) == key);
    }

    private static DrugDto MapDrugToDto(Drug d) => new()
    {
        Id = d.Id,
        Code = d.Code,
        Name = d.Name,
        GenericName = d.GenericName,
        Specification = d.Specification,
        Manufacturer = d.Manufacturer,
        ApprovalNumber = d.ApprovalNumber,
        DosageForm = d.DosageForm,
        Unit = d.Unit,
        CategoryName = d.Category?.Name,
        CategoryId = d.CategoryId,
        WarningLowQuantity = d.WarningLowQuantity,
    };

    private static void SupplementDrug(Drug target, DrugDto source)
    {
        var changed = false;
        if (string.IsNullOrWhiteSpace(target.GenericName) && !string.IsNullOrWhiteSpace(source.GenericName)) { target.GenericName = source.GenericName; changed = true; }
        if (string.IsNullOrWhiteSpace(target.Manufacturer) && !string.IsNullOrWhiteSpace(source.Manufacturer)) { target.Manufacturer = source.Manufacturer; changed = true; }
        if (string.IsNullOrWhiteSpace(target.ApprovalNumber) && !string.IsNullOrWhiteSpace(source.ApprovalNumber)) { target.ApprovalNumber = source.ApprovalNumber; changed = true; }
        if (string.IsNullOrWhiteSpace(target.DosageForm) && !string.IsNullOrWhiteSpace(source.DosageForm)) { target.DosageForm = source.DosageForm; changed = true; }
        if ((string.IsNullOrWhiteSpace(target.Unit) || target.Unit == "盒") && !string.IsNullOrWhiteSpace(source.Unit)) { target.Unit = source.Unit; changed = true; }
        if (!target.CategoryId.HasValue && source.CategoryId.HasValue) { target.CategoryId = source.CategoryId; changed = true; }
        if (target.WarningLowQuantity == 0 && source.WarningLowQuantity > 0) { target.WarningLowQuantity = source.WarningLowQuantity; changed = true; }
        if (changed) target.UpdatedAt = DateTime.UtcNow;
    }

    private async Task AdoptCollectionCreatedMasterDataAsync(string entityType, Guid entityId)
    {
        var trackingRows = await _db.CollectionSyncEntities
            .Where(e => e.EntityType == entityType && e.EntityId == entityId)
            .ToListAsync();
        if (trackingRows.Count > 0)
            _db.CollectionSyncEntities.RemoveRange(trackingRows);
    }

    public async Task<bool> ExistsByNameAndSpec(string name, string? spec)
    {
        return await FindActiveByNameAndSpecAsync(name, spec) != null;
    }

    public async Task<string> GetCompanyNameAsync()
    {
        return await Task.FromResult("");
    }
}
