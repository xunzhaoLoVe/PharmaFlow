using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PharmaFlow.Application.DTOs;
using PharmaFlow.Domain.Entities;
using PharmaFlow.Infrastructure.Data;

namespace PharmaFlow.Application.Services;

public class ImportRuleService : IImportRuleService
{
    private readonly AppDbContext _db;

    public ImportRuleService(AppDbContext db) => _db = db;

    public async Task<PagedResult<ImportRuleDto>> GetPagedAsync(int pageIndex, int pageSize)
    {
        var query = _db.ImportRules.Include(r => r.ModuleConfigs).AsQueryable();
        var total = await query.CountAsync();
        var items = await query.OrderByDescending(r => r.CreatedAt).Skip(pageIndex * pageSize).Take(pageSize).ToListAsync();
        return new PagedResult<ImportRuleDto>
        {
            Items = items.Select(MapToDto).ToList(),
            TotalCount = total, PageIndex = pageIndex, PageSize = pageSize,
        };
    }

    public async Task<ImportRuleDto?> GetByIdAsync(Guid id)
    {
        var rule = await _db.ImportRules.Include(r => r.ModuleConfigs).FirstOrDefaultAsync(r => r.Id == id);
        return rule == null ? null : MapToDto(rule);
    }

    public async Task<ImportRuleDto> CreateAsync(ImportRuleDto dto)
    {
        var rule = new ImportRule { Name = dto.Name, Description = dto.Description };
        _db.ImportRules.Add(rule);
        await _db.SaveChangesAsync();

        foreach (var mc in dto.ModuleConfigs)
        {
            _db.ModuleRuleConfigs.Add(new ModuleRuleConfig
            {
                RuleId = rule.Id, ModuleName = mc.ModuleName,
                FieldMappings = mc.FieldMappings, NormalizeRules = mc.NormalizeRules,
                MatchConfig = mc.MatchConfig, AutoCreate = mc.AutoCreate,
            });
        }
        await _db.SaveChangesAsync();
        dto.Id = rule.Id;
        return dto;
    }

    public async Task UpdateAsync(ImportRuleDto dto)
    {
        var rule = await _db.ImportRules.Include(r => r.ModuleConfigs).FirstOrDefaultAsync(r => r.Id == dto.Id);
        if (rule == null) return;
        rule.Name = dto.Name; rule.Description = dto.Description;
        rule.UpdatedAt = DateTime.UtcNow;

        _db.ModuleRuleConfigs.RemoveRange(rule.ModuleConfigs);
        foreach (var mc in dto.ModuleConfigs)
        {
            _db.ModuleRuleConfigs.Add(new ModuleRuleConfig
            {
                RuleId = rule.Id, ModuleName = mc.ModuleName,
                FieldMappings = mc.FieldMappings, NormalizeRules = mc.NormalizeRules,
                MatchConfig = mc.MatchConfig, AutoCreate = mc.AutoCreate,
            });
        }
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var rule = await _db.ImportRules.FindAsync(id);
        if (rule != null) { rule.IsDeleted = true; rule.UpdatedAt = DateTime.UtcNow; await _db.SaveChangesAsync(); }
    }

    public async Task<IReadOnlyList<DropdownItem>> GetDropdownAsync()
    {
        return await _db.ImportRules.Where(r => !r.IsDeleted).OrderBy(r => r.Name)
            .Select(r => new DropdownItem { Id = r.Id, Name = r.Name }).ToListAsync();
    }

    private static ImportRuleDto MapToDto(ImportRule r) => new()
    {
        Id = r.Id, Name = r.Name, Description = r.Description,
        ModuleConfigs = r.ModuleConfigs.Select(m => new ModuleRuleConfigDto
        {
            Id = m.Id, RuleId = m.RuleId, ModuleName = m.ModuleName,
            FieldMappings = m.FieldMappings, NormalizeRules = m.NormalizeRules,
            MatchConfig = m.MatchConfig, AutoCreate = m.AutoCreate,
        }).ToList(),
    };
}