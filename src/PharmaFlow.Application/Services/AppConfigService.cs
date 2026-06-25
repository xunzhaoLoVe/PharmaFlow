using Microsoft.EntityFrameworkCore;
using PharmaFlow.Infrastructure.Data;

namespace PharmaFlow.Application.Services;

/// <summary>读取系统配置</summary>
public class AppConfigService
{
    private readonly AppDbContext _db;
    private string? _cachedCompanyName;

    public AppConfigService(AppDbContext db) => _db = db;

    public async Task<string> GetCompanyNameAsync()
    {
        if (_cachedCompanyName != null) return _cachedCompanyName;

        var config = await _db.SystemConfigs.FirstOrDefaultAsync(c => c.Key == "CompanyName");
        _cachedCompanyName = config?.Value ?? "本公司";
        return _cachedCompanyName;
    }

    public void ClearCache() => _cachedCompanyName = null;
}
