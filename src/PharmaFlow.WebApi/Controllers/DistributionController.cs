using Microsoft.AspNetCore.Mvc;
using PharmaFlow.Application.DTOs;
using PharmaFlow.Application.Services;
using PharmaFlow.WebApi.Models;

namespace PharmaFlow.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DistributionController : ControllerBase
{
    private readonly IDistributionService _distributionService;
    private readonly ISupplierService _supplierService;
    private Guid? _defaultSupplierId;

    public DistributionController(IDistributionService distributionService, ISupplierService supplierService)
    {
        _distributionService = distributionService;
        _supplierService = supplierService;
    }

    private async Task<Guid> GetDefaultSupplierIdAsync()
    {
        if (_defaultSupplierId.HasValue) return _defaultSupplierId.Value;
        var suppliers = await _supplierService.GetDropdownAsync();
        var shenlong = suppliers.FirstOrDefault(s => s.Name.Contains("江苏神龙"));
        if (shenlong != null) { _defaultSupplierId = shenlong.Id; return shenlong.Id; }
        var created = await _supplierService.CreateAsync(new SupplierDto { Name = "江苏神龙药业有限公司" });
        _defaultSupplierId = created.Id; return created.Id;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<DistributionRecordDto>>>> GetPaged(
        [FromQuery] int page = 0, [FromQuery] int size = 20,
        [FromQuery] string? drugName = null, [FromQuery] string? batchNo = null,
        [FromQuery] Guid? customerId = null, [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null, [FromQuery] bool? fromSelf = null)
    {
        var result = await _distributionService.GetPagedAsync(
            page, size, drugName, batchNo, customerId, from, to, fromSelf);

        foreach (var item in result.Items)
        {
            if (item.FromCustomerName == "本公司(制药厂)")
            {
                item.FromCustomerName = "江苏神龙药业有限公司";
                item.FromCustomerCode = "SHENLONG";
                item.Province = "江苏省";
            }
        }
        return Ok(ApiResponse<PagedResult<DistributionRecordDto>>.Ok(result));
    }

    [HttpGet("trace")]
    public async Task<ActionResult<ApiResponse<FlowGraphDto>>> GetTrace(
        [FromQuery] string batchNo, [FromQuery] Guid drugId)
    {
        var graph = await _distributionService.GetTraceAsync(batchNo, drugId);
        if (graph != null)
        {
            graph.SupplierName = "江苏神龙药业有限公司";

            // 查找或创建生产厂家节点
            FlowNodeDto? supplierNode = null;
            foreach (var node in graph.Nodes.Values)
            {
                if (node.Name == "本公司(制药厂)" || node.Type == "生产厂家" || node.Name == "江苏神龙药业有限公司")
                {
                    node.Name = "江苏神龙药业有限公司";
                    node.Type = "生产厂家";
                    node.Tier = 0;
                    supplierNode = node;
                }
            }

            // 如果没有生产厂家节点，创建一个
            if (supplierNode == null)
            {
                var supplierId = Guid.NewGuid().ToString();
                supplierNode = new FlowNodeDto
                {
                    Id = supplierId,
                    Name = "江苏神龙药业有限公司",
                    Type = "生产厂家",
                    Tier = 0,
                    Province = "江苏省",
                };
                graph.Nodes[supplierId] = supplierNode;

                // 添加到 Tiers
                if (!graph.Tiers.ContainsKey(0))
                    graph.Tiers[0] = new List<FlowNodeDto>();
                graph.Tiers[0].Add(supplierNode);

                // 为所有一级经销节点创建边：生产厂家 → 一级经销
                if (graph.Tiers.TryGetValue(1, out var tier1Nodes))
                {
                    foreach (var t1Node in tier1Nodes)
                    {
                        var edgeKey = $"{supplierId}→{t1Node.Id}";
                        if (!graph.Edges.ContainsKey(edgeKey))
                        {
                            graph.Edges[edgeKey] = new FlowEdgeDto
                            {
                                SourceId = supplierId,
                                TargetId = t1Node.Id,
                                SourceName = "江苏神龙药业有限公司",
                                TargetName = t1Node.Name,
                                Quantity = t1Node.StockQuantity,
                                Date = t1Node.Date,
                            };
                        }
                    }
                    supplierNode.OutQuantity = tier1Nodes.Sum(n => n.StockQuantity);
                }
            }
            else
            {
                // 确保 supplierNode 在 Tiers 的第0层
                foreach (var tierKv in graph.Tiers)
                {
                    tierKv.Value.RemoveAll(n => n.Name == "江苏神龙药业有限公司" || n.Type == "生产厂家");
                }
                if (!graph.Tiers.ContainsKey(0))
                    graph.Tiers[0] = new List<FlowNodeDto>();
                graph.Tiers[0].Add(supplierNode);
            }

            // 替换边中的名称
            foreach (var edge in graph.Edges.Values)
            {
                if (edge.SourceName == "本公司(制药厂)")
                    edge.SourceName = "江苏神龙药业有限公司";
                if (edge.TargetName == "本公司(制药厂)")
                    edge.TargetName = "江苏神龙药业有限公司";
            }
        }
        return graph == null
            ? NotFound(ApiResponse<FlowGraphDto>.Fail("未找到流向数据"))
            : Ok(ApiResponse<FlowGraphDto>.Ok(graph));
    }

    [HttpPost("downstream")]
    public async Task<ActionResult<ApiResponse>> RecordDownstream(
        [FromBody] DownstreamRecordDto dto)
    {
        // 采购出货（本司→经销商）：自动分配供应商
        if (dto.FromCustomerId == Guid.Empty && dto.SupplierId == null)
            dto.SupplierId = await GetDefaultSupplierIdAsync();
        var operatorName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "系统";
        await _distributionService.RecordDownstreamAsync(dto, operatorName);
        return Ok(ApiResponse.Ok("流转记录成功"));
    }

    [HttpPost("downstream/batch")]
    public async Task<ActionResult<ApiResponse<int>>> RecordDownstreamBatch(
        [FromBody] List<DownstreamRecordDto> dtos)
    {
        var supplierId = await GetDefaultSupplierIdAsync();
        foreach (var dto in dtos)
        {
            if (dto.FromCustomerId == Guid.Empty && dto.SupplierId == null)
                dto.SupplierId = supplierId;
        }
        var operatorName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "系统";
        var count = await _distributionService.RecordDownstreamBatchAsync(dtos, operatorName);
        return Ok(ApiResponse<int>.Ok(count, $"成功记录 {count} 条"));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Update(Guid id,
        [FromQuery] decimal quantity)
    {
        await _distributionService.UpdateDistributionRecordAsync(id, quantity);
        return Ok(ApiResponse.Ok("更新成功"));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id)
    {
        await _distributionService.DeleteDistributionRecordAsync(id);
        return Ok(ApiResponse.Ok("删除成功"));
    }

    [HttpDelete("clear")]
    public async Task<ActionResult<ApiResponse>> ClearAll(
        [FromQuery] bool? fromSelf = null)
    {
        if (fromSelf == true)
            await _distributionService.ClearBySourceAsync(isPurchase: true);
        else if (fromSelf == false)
            await _distributionService.ClearBySourceAsync(isPurchase: false);
        else
            await _distributionService.ClearAllAsync();
        return Ok(ApiResponse.Ok("已清空"));
    }

    [HttpGet("conflicts")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ChannelConflictDto>>>> GetConflicts(
        [FromQuery] string? batchNo = null)
    {
        var items = await _distributionService.GetChannelConflictsAsync(batchNo);
        return Ok(ApiResponse<IReadOnlyList<ChannelConflictDto>>.Ok(items));
    }
}
