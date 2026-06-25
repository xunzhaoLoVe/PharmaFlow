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
            Specification = dto.Specification,
            Unit = "盒",
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
        dto.Id = drug.Id;
        return dto;
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

    public async Task<bool> ExistsByNameAndSpec(string name, string? spec)
    {
        return await _db.Drugs.AnyAsync(d => d.Name == name && d.Specification == spec && !d.IsDeleted);
    }

    public async Task<string> GetCompanyNameAsync()
    {
        return await Task.FromResult("");
    }
}
