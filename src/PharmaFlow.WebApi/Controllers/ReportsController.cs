using Microsoft.AspNetCore.Mvc;
using PharmaFlow.Application.DTOs;
using PharmaFlow.Application.Services;
using PharmaFlow.WebApi.Models;

namespace PharmaFlow.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet("end-customer-sales")]
    public async Task<ActionResult<ApiResponse<List<EndCustomerSaleDto>>>> GetEndCustomerSales(
        [FromQuery] string? drugName = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var data = await _reportService.GetEndCustomerSalesAsync(drugName, startDate, endDate);
        return Ok(ApiResponse<List<EndCustomerSaleDto>>.Ok(data));
    }
}
