using PharmaFlow.Application.DTOs;

namespace PharmaFlow.Application.Services;

public interface IReportService
{
    Task<ReportOverviewDto> GetOverviewAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? granularity = null,
        string? drugName = null,
        string? customerName = null);

    Task<List<EndCustomerSaleDto>> GetEndCustomerSalesAsync(
        string? drugName = null, DateTime? startDate = null, DateTime? endDate = null);
}
