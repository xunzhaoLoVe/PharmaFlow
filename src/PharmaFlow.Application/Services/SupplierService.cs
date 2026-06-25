using Microsoft.EntityFrameworkCore;
using PharmaFlow.Application.DTOs;
using PharmaFlow.Domain.Entities;
using PharmaFlow.Infrastructure.Data;

namespace PharmaFlow.Application.Services;

public class SupplierService : ISupplierService
{
    private readonly AppDbContext _db;
    public SupplierService(AppDbContext db) => _db = db;

    public async Task<PagedResult<SupplierDto>> GetPagedAsync(int pageIndex, int pageSize, string? search = null)
    {
        var query = _db.Suppliers.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(s => s.Name.Contains(search) || s.Code.Contains(search));

        var total = await query.CountAsync();
        var items = await query.OrderBy(s => s.Code).Skip(pageIndex * pageSize).Take(pageSize).ToListAsync();

        return new PagedResult<SupplierDto>
        {
            Items = items.Select(s => new SupplierDto
            {
                Id = s.Id, Code = s.Code, Name = s.Name,
                ContactPerson = s.ContactPerson, Phone = s.Phone, Address = s.Address,
            }).ToList(),
            TotalCount = total, PageIndex = pageIndex, PageSize = pageSize,
        };
    }

    public async Task<SupplierDto?> GetByIdAsync(Guid id)
    {
        var s = await _db.Suppliers.FindAsync(id);
        if (s == null) return null;
        return new SupplierDto { Id = s.Id, Code = s.Code, Name = s.Name, ContactPerson = s.ContactPerson, Phone = s.Phone, Address = s.Address };
    }

    public async Task<SupplierDto> CreateAsync(SupplierDto dto)
    {
        // Auto-generate supplier code if empty or if the provided code already exists (including soft-deleted)
        if (string.IsNullOrWhiteSpace(dto.Code) ||
            await _db.Suppliers.IgnoreQueryFilters().AnyAsync(s => s.Code == dto.Code))
        {
            var today = DateTime.UtcNow.ToString("yyyyMMdd");
            var prefix = "SUPP" + today;
            var lastCode = await _db.Suppliers
                .IgnoreQueryFilters()
                .Where(s => s.Code.StartsWith(prefix))
                .OrderByDescending(s => s.Code)
                .Select(s => s.Code)
                .FirstOrDefaultAsync();
            int seq = 1;
            if (lastCode != null && int.TryParse(lastCode[^3..], out var lastSeq))
                seq = lastSeq + 1;

            string newCode;
            do
            {
                newCode = prefix + seq.ToString("D3");
                seq++;
            }
            while (await _db.Suppliers.IgnoreQueryFilters().AnyAsync(s => s.Code == newCode));

            dto.Code = newCode;
        }

        var entity = new Supplier { Code = dto.Code, Name = dto.Name, ContactPerson = dto.ContactPerson, Phone = dto.Phone, Address = dto.Address };
        _db.Suppliers.Add(entity);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch
        {
            _db.Entry(entity).State = EntityState.Detached;
            throw;
        }
        dto.Id = entity.Id;
        return dto;
    }

    public async Task UpdateAsync(SupplierDto dto)
    {
        var s = await _db.Suppliers.FindAsync(dto.Id);
        if (s == null) return;
        s.Code = dto.Code; s.Name = dto.Name; s.ContactPerson = dto.ContactPerson; s.Phone = dto.Phone; s.Address = dto.Address;
        s.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var s = await _db.Suppliers.FindAsync(id);
        if (s != null) { s.IsDeleted = true; s.UpdatedAt = DateTime.UtcNow; await _db.SaveChangesAsync(); }
    }

    public async Task<IReadOnlyList<DropdownItem>> GetDropdownAsync() =>
        await _db.Suppliers.Where(s => !s.IsDeleted).OrderBy(s => s.Name)
            .Select(s => new DropdownItem { Id = s.Id, Name = s.Name }).ToListAsync();
}
