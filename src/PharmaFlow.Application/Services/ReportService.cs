using PharmaFlow.Infrastructure.Data;

namespace PharmaFlow.Application.Services;

public class ReportService : IReportService
{
    private readonly AppDbContext _db;
    public ReportService(AppDbContext db) => _db = db;
}
