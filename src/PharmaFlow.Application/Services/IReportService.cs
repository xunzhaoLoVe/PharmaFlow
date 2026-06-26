using PharmaFlow.Application.DTOs;

namespace PharmaFlow.Application.Services;

public interface IReportService
{
    Task<List<EndCustomerSaleDto>> GetEndCustomerSalesAsync(
        string? drugName = null, DateTime? startDate = null, DateTime? endDate = null);
}
