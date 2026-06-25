using PharmaFlow.Application.DTOs;

namespace PharmaFlow.Application.Services;

public interface IDashboardService
{
    Task<DashboardStats> GetStatsAsync();
}
