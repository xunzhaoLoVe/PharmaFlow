using Microsoft.EntityFrameworkCore;
using PharmaFlow.Application.DTOs;
using PharmaFlow.Domain.Entities;
using PharmaFlow.Domain.Enums;
using PharmaFlow.Infrastructure.Data;

namespace PharmaFlow.Application.Services;

public class DistributionService : IDistributionService
{
    private readonly AppDbContext _db;
    public DistributionService(AppDbContext db) => _db = db;

    public async Task<PagedResult<DistributionRecordDto>> GetPagedAsync(int pageIndex, int pageSize, string? drugName = null, string? batchNo = null, Guid? customerId = null, DateTime? from = null, DateTime? to = null, bool? fromSelf = null)
    {
        var selfId = await _db.Customers.Where(c => c.Code == "SELF").Select(c => (Guid?)c.Id).FirstOrDefaultAsync();

        var query = _db.DistributionRecords
            .Include(r => r.Drug)
            .Include(r => r.ToCustomer)
            .Include(r => r.FromCustomer)
            .Include(r => r.Supplier)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(drugName))
            query = query.Where(r => r.Drug.Name.Contains(drugName));
        if (!string.IsNullOrWhiteSpace(batchNo))
            query = query.Where(r => r.BatchNo.Contains(batchNo));
        if (customerId.HasValue)
            query = query.Where(r => r.ToCustomerId == customerId || r.FromCustomerId == customerId);
        if (from.HasValue)
            query = query.Where(r => r.DistributionDate >= from.Value);
        if (to.HasValue)
            query = query.Where(r => r.DistributionDate <= to.Value);
        if (fromSelf == true && selfId.HasValue)
            query = query.Where(r => r.FromCustomerId == selfId.Value || r.ToCustomerId == selfId.Value || r.SupplierId != null);
        else if (fromSelf == false && selfId.HasValue)
            query = query.Where(r => r.FromCustomerId != selfId.Value && r.ToCustomerId != selfId.Value && r.SupplierId == null);

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(r => r.DistributionDate).Skip(pageIndex * pageSize).Take(pageSize).ToListAsync();

        return new PagedResult<DistributionRecordDto>
        {
            Items = items.Select(r => new DistributionRecordDto
            {
                Id = r.Id,
                DrugName = r.Drug.Name,
                Specification = r.Drug.Specification,
                BatchNo = r.BatchNo,
                ToCustomerName = r.ToCustomer?.Name,
                ToCustomerCode = r.ToCustomer?.Code,
                FromCustomerName = r.SupplierId.HasValue ? r.Supplier?.Name : r.FromCustomer?.Name,
                FromCustomerCode = r.SupplierId.HasValue ? null : r.FromCustomer?.Code,
                SupplierName = r.Supplier?.Name,
                Quantity = r.Quantity,
                SalesPerson = r.SalesPerson,
                DeliveryOrderNo = r.DeliveryOrderNo,
                TaxedUnitPrice = r.TaxedUnitPrice,
                DistributionDate = r.DistributionDate,
                Operator = r.Operator, Province = r.Province,
                DistributionDateDisplay = r.DistributionDateDisplay,
            }).ToList(),
            TotalCount = total, PageIndex = pageIndex, PageSize = pageSize,
        };
    }

    public async Task RecordDownstreamAsync(DownstreamRecordDto dto, string operatorName)
    {
        var self = await _db.Customers.FirstOrDefaultAsync(c => c.Code == "SELF");
        if (self == null) throw new InvalidOperationException("系统错误：本公司(SELF)记录不存在");
        await RecordOneAsync(dto, operatorName, self.Id);
        await _db.SaveChangesAsync();
    }

    /// <summary>批量导入下游出货记录，一次性提交（性能优化）</summary>
    public async Task<int> RecordDownstreamBatchAsync(List<DownstreamRecordDto> dtos, string operatorName)
    {
        if (dtos.Count == 0) return 0;

        var self = await _db.Customers.FirstOrDefaultAsync(c => c.Code == "SELF");
        if (self == null) throw new InvalidOperationException("系统错误：本公司(SELF)记录不存在");

        // 预加载所有相关库存
        var drugIds = dtos.Select(d => d.DrugId).Distinct().ToList();
        var batchNos = dtos.Select(d => d.BatchNo).Distinct().ToList();
        var customerIds = dtos.Select(d => d.ToCustomerId == Guid.Empty ? self.Id : d.ToCustomerId).Distinct().ToList();

        var inventories = await _db.Inventories
            .Where(i => drugIds.Contains(i.DrugId) && batchNos.Contains(i.BatchNo) && i.CustomerId.HasValue && customerIds.Contains(i.CustomerId.Value))
            .ToListAsync();
        var invDict = inventories.ToDictionary(i => (i.DrugId, i.BatchNo, i.CustomerId!.Value), i => i);

        var imported = 0;
        foreach (var dto in dtos)
        {
            bool hasSupplier = dto.SupplierId.HasValue && dto.SupplierId != Guid.Empty;
            Guid? fromCustomerId = hasSupplier ? null : (dto.FromCustomerId == Guid.Empty ? self.Id : dto.FromCustomerId);
            Guid toId = dto.ToCustomerId == Guid.Empty ? self.Id : dto.ToCustomerId;

            _db.DistributionRecords.Add(new DistributionRecord
            {
                DrugId = dto.DrugId, BatchNo = dto.BatchNo,
                SupplierId = hasSupplier ? dto.SupplierId : null,
                FromCustomerId = fromCustomerId, ToCustomerId = toId,
                Quantity = dto.Quantity, SalesPerson = dto.SalesPerson,
                DeliveryOrderNo = dto.DeliveryOrderNo, TaxedUnitPrice = dto.TaxedUnitPrice,
                DistributionDate = DateTime.TryParse(dto.DistributionDateDisplay ?? dto.DistributionDate.ToString("yyyy-MM-dd"), out var dd) ? dd : DateTime.Now,
                DistributionDateDisplay = dto.DistributionDateDisplay, Operator = operatorName, Province = dto.Province,
            });

            // 库存：有供应商时只增加到本公司(toId)库存；纯流转时From出货To入库
            if (!hasSupplier && fromCustomerId.HasValue)
            {
                var fromKey = (dto.DrugId, dto.BatchNo, fromCustomerId.Value);
                if (invDict.TryGetValue(fromKey, out var fromInv))
                { fromInv.Quantity -= dto.Quantity; if (fromInv.Quantity < 0) fromInv.Quantity = 0; }
            }

            var toKey = (dto.DrugId, dto.BatchNo, toId);
            if (invDict.TryGetValue(toKey, out var toInv))
            { toInv.Quantity += dto.Quantity; if (toInv.Quantity < 0) toInv.Quantity = 0; }
            else if (dto.Quantity > 0)
            {
                var newInv = new Inventory { DrugId = dto.DrugId, BatchNo = dto.BatchNo, Quantity = dto.Quantity, CustomerId = toId };
                _db.Inventories.Add(newInv);
                invDict[toKey] = newInv;
            }
            imported++;
        }

        await _db.SaveChangesAsync();
        return imported;
    }

    private async Task RecordOneAsync(DownstreamRecordDto dto, string operatorName, Guid selfId)
    {
        bool hasSupplier = dto.SupplierId.HasValue && dto.SupplierId != Guid.Empty;
        Guid? fromId = hasSupplier ? null : (dto.FromCustomerId == Guid.Empty ? selfId : dto.FromCustomerId);
        Guid? toId = dto.ToCustomerId == Guid.Empty ? selfId : dto.ToCustomerId;

        _db.DistributionRecords.Add(new DistributionRecord
        {
            DrugId = dto.DrugId, BatchNo = dto.BatchNo,
            SupplierId = hasSupplier ? dto.SupplierId : null,
            FromCustomerId = fromId, ToCustomerId = toId,
            Quantity = dto.Quantity, SalesPerson = dto.SalesPerson,
            DeliveryOrderNo = dto.DeliveryOrderNo, TaxedUnitPrice = dto.TaxedUnitPrice,
            DistributionDate = DateTime.TryParse(dto.DistributionDateDisplay ?? dto.DistributionDate.ToString("yyyy-MM-dd"), out var dd) ? dd : DateTime.Now,
                DistributionDateDisplay = dto.DistributionDateDisplay, Operator = operatorName, Province = dto.Province,
        });

        if (!hasSupplier && fromId.HasValue)
        {
            var fromInv = await _db.Inventories
                .FirstOrDefaultAsync(i => i.DrugId == dto.DrugId && i.BatchNo == dto.BatchNo && i.CustomerId == fromId);
            if (fromInv != null) { fromInv.Quantity -= dto.Quantity; if (fromInv.Quantity < 0) fromInv.Quantity = 0; }
        }

        if (toId.HasValue)
        {
            var toInv = await _db.Inventories
                .FirstOrDefaultAsync(i => i.DrugId == dto.DrugId && i.BatchNo == dto.BatchNo && i.CustomerId == toId);
            if (toInv != null) { toInv.Quantity += dto.Quantity; if (toInv.Quantity < 0) toInv.Quantity = 0; }
            else if (dto.Quantity > 0)
                _db.Inventories.Add(new Inventory { DrugId = dto.DrugId, BatchNo = dto.BatchNo, Quantity = dto.Quantity, CustomerId = toId });
        }
    }

    public async Task<IReadOnlyList<DropdownItem>> GetCustomerDropdownAsync()
    {
        return await _db.Customers.Where(c => !c.IsDeleted && c.Code != "SELF").OrderBy(c => c.Name)
            .Select(c => new DropdownItem { Id = c.Id, Name = c.Name })
            .ToListAsync();
    }

    public async Task<FlowGraphDto?> GetTraceAsync(string batchNo, Guid drugId)
    {
        var query = _db.DistributionRecords
            .Include(r => r.Drug)
            .Include(r => r.ToCustomer)
            .Include(r => r.FromCustomer)
            .Include(r => r.Supplier)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(batchNo) && batchNo.Length >= 4)
            query = query.Where(r => r.BatchNo.Contains(batchNo));
        if (drugId != Guid.Empty)
            query = query.Where(r => r.DrugId == drugId);

        var records = await query.OrderBy(r => r.DistributionDate).ToListAsync();
        if (records.Count == 0) return null;

        var drug = records.First().Drug!;
        var selfId = await _db.Customers.Where(c => c.Code == "SELF").Select(c => (Guid?)c.Id).FirstOrDefaultAsync();

        var batchNos = records.Select(r => r.BatchNo).Distinct().ToList();

        // Get inventory
        var inventories = await _db.Inventories
            .Where(i => batchNos.Contains(i.BatchNo))
            .ToListAsync();

        // ===== 从 DistributionRecord 中提取供应商信息（SupplierId 不为空的记录）=====
        var supplierRecords = records.Where(r => r.SupplierId.HasValue).ToList();
        var supplierGroups = supplierRecords
            .GroupBy(r => new { r.SupplierId!.Value, SupplierName = r.Supplier?.Name ?? "" })
            .Select(g => new { SupplierId = g.Key.Value.ToString(), SupplierName = g.Key.SupplierName, TotalQuantity = g.Sum(r => r.Quantity), FirstDate = g.Min(r => r.DistributionDate) })
            .ToList();

        // ===== 补充查询：从 PurchaseOrderItems 获取未记录在 DistributionRecord 中的采购来源 =====
        // （兼容旧数据：升级前已完成的采购单没有 DistributionRecord）
        var batchesFromPurchaseRecords = supplierRecords.Select(r => r.BatchNo).Distinct().ToHashSet();
        var batchesWithoutPurchaseRecord = batchNos.Where(b => !batchesFromPurchaseRecords.Contains(b)).ToList();

        if (batchesWithoutPurchaseRecord.Count > 0)
        {
            var fallbackPurchaseItems = await _db.PurchaseOrderItems
                .Include(pi => pi.PurchaseOrder).ThenInclude(po => po.Supplier)
                .Where(pi => batchesWithoutPurchaseRecord.Contains(pi.BatchNo) && pi.PurchaseOrder.Status == OrderStatus.已完成)
                .ToListAsync();

            var fallbackGroups = fallbackPurchaseItems
                .GroupBy(pi => new { pi.PurchaseOrder.SupplierId, pi.PurchaseOrder.Supplier.Name })
                .Select(g => new { SupplierId = g.Key.SupplierId.ToString(), SupplierName = g.Key.Name, TotalQuantity = (decimal)g.Sum(pi => pi.Quantity), FirstDate = g.Min(pi => pi.PurchaseOrder.OrderDate) })
                .ToList();

            // 合并，去重
            var existingSupplierIds = supplierGroups.Select(s => s.SupplierId).ToHashSet();
            supplierGroups.AddRange(fallbackGroups.Where(f => !existingSupplierIds.Contains(f.SupplierId)));
        }

        // ===== 构建节点字典 =====
        var nodes = new Dictionary<string, FlowNodeDto>();
        var allCustomerIds = new HashSet<Guid>();
        foreach (var r in records)
        {
            if (r.FromCustomer != null) allCustomerIds.Add(r.FromCustomer.Id);
            if (r.ToCustomer != null) allCustomerIds.Add(r.ToCustomer.Id);
        }

        // 加载所有涉及客户的完整信息
        var allCustomers = await _db.Customers.Where(c => allCustomerIds.Contains(c.Id)).ToListAsync();
        var customerMap = allCustomers.ToDictionary(c => c.Id);

        // 为每个唯一客户创建节点
        foreach (var c in allCustomers)
        {
            if (c.Id == selfId) continue; // SELF不再显示
            string type = c.CustomerType switch
            {
                CustomerType.商业 => c.Level switch
                {
                    DistributorLevel.一级经销商 => "一级经销",
                    DistributorLevel.二级经销商 => "二级经销",
                    DistributorLevel.三级经销商 => "三级经销",
                    _ => "经销商"
                },
                _ => "终端销售"
            };
            var inv = inventories.FirstOrDefault(i => i.CustomerId == c.Id);
            nodes[c.Id.ToString()] = new FlowNodeDto
            {
                Id = c.Id.ToString(), Name = c.Name, Type = type,
                Province = c.Province, StockQuantity = inv?.Quantity ?? 0,
                Date = records.Where(r => r.ToCustomerId == c.Id || r.FromCustomerId == c.Id).Min(r => (DateTime?)r.DistributionDate),
            };
        }

        // ===== 构建边字典 =====
        var edges = new Dictionary<string, FlowEdgeDto>();

        foreach (var r in records)
        {
            string? fromId, toId;
            string? fromName, toName;

            if (r.SupplierId.HasValue)
            {
                // 采购记录：供应商 → 本公司(SELF)
                fromId = $"SUP_{r.SupplierId.Value}";
                toId = r.ToCustomerId?.ToString();
                fromName = r.Supplier?.Name ?? "";
                toName = r.ToCustomer?.Name ?? "";
            }
            else
            {
                // 销售流转记录：FromCustomer → ToCustomer（跳过SELF）
                if (r.FromCustomerId == selfId || r.ToCustomerId == selfId) continue;
                fromId = r.FromCustomerId?.ToString();
                toId = r.ToCustomerId?.ToString();
                fromName = r.FromCustomer?.Name ?? "";
                toName = r.ToCustomer?.Name ?? "";
            }

            if (fromId == null || toId == null) continue;

            // 退货不反转方向，靠负数量和虚线标识
            var dateKey = r.DistributionDate;
            var edgeKey = $"{fromId}→{toId}|{dateKey}";
            if (edges.TryGetValue(edgeKey, out var existing))
                existing.Quantity += r.Quantity; // 负数会累减
            else
                edges[edgeKey] = new FlowEdgeDto
                {
                    SourceId = fromId, TargetId = toId,
                    SourceName = fromName, TargetName = toName,
                    Quantity = r.Quantity, Date = r.DistributionDate,
                };
        }

        // ===== 补充供应商节点（生产厂家）=====
        foreach (var sg in supplierGroups)
        {
            var supplierId = $"SUP_{sg.SupplierId}";
            if (!nodes.ContainsKey(supplierId))
            {
                nodes[supplierId] = new FlowNodeDto
                {
                    Id = supplierId, Name = sg.SupplierName, Type = "生产厂家",
                    Tier = 0, Date = sg.FirstDate,
                };
            }
        }

        // ===== BFS 计算各节点 Tier（从生产厂家出发）=====
        ComputeTiersFromSuppliers(nodes, edges, supplierGroups.Select(s => $"SUP_{s.SupplierId}").ToList());

        // ===== 汇总每个节点的入/出总量 =====
        foreach (var node in nodes.Values)
        {
            node.InQuantity = edges.Values.Where(e => e.TargetId == node.Id).Sum(e => e.Quantity);
            node.OutQuantity = edges.Values.Where(e => e.SourceId == node.Id).Sum(e => e.Quantity);
        }

        // ===== 标记调拨边（仅同级别经销商之间互流，不含流向终端）=====
        // 用经销商实际Level判定，不用BFS算出的Tier
        foreach (var edge in edges.Values)
        {
            var src = nodes.TryGetValue(edge.SourceId, out var s) ? s : null;
            var tgt = nodes.TryGetValue(edge.TargetId, out var t) ? t : null;
            if (src == null || tgt == null) continue;

            int srcLevel = DealerLevelFromType(src.Type);
            int tgtLevel = DealerLevelFromType(tgt.Type);
            edge.IsTransfer = srcLevel > 0 && tgtLevel > 0 && srcLevel == tgtLevel;
        }

        // ===== 检测串货（一级经销商省份与下游不一致）=====
        DetectChannelConflicts(nodes, edges, supplierGroups.Select(s => $"SUP_{s.SupplierId}").ToList());

        // ===== 按 Tier 分组 =====
        var tiers = new SortedDictionary<int, List<FlowNodeDto>>();
        foreach (var node in nodes.Values)
        {
            if (!tiers.ContainsKey(node.Tier))
                tiers[node.Tier] = new List<FlowNodeDto>();
            tiers[node.Tier].Add(node);
        }
        foreach (var kv in tiers)
            kv.Value.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));

        return new FlowGraphDto
        {
            DrugName = drug.Name,
            BatchNo = batchNo,
            SupplierName = supplierGroups.FirstOrDefault()?.SupplierName,
            PurchaseDate = supplierGroups.FirstOrDefault()?.FirstDate ?? records.Min(r => r.DistributionDate),
            Nodes = nodes, Edges = edges, Tiers = tiers,
        };
    }

    /// <summary>从节点类型解析经销商级别（1/2/3），非经销商返回0</summary>
    private static int DealerLevelFromType(string type) => type switch
    {
        "一级经销" => 1,
        "二级经销" => 2,
        "三级经销" => 3,
        _ => 0
    };

    /// <summary>计算各节点 Tier 用于流向图列布局。经销商按实际级别固定列，终端按最短路径计算</summary>
    private static void ComputeTiersFromSuppliers(Dictionary<string, FlowNodeDto> nodes, Dictionary<string, FlowEdgeDto> edges, List<string> supplierIds)
    {
        // 初始化
        foreach (var n in nodes.Values) n.Tier = int.MaxValue;

        // Tier 0: 生产厂家（供应商）
        foreach (var sid in supplierIds)
            if (nodes.TryGetValue(sid, out var s)) s.Tier = 0;

        // Tier 1-3: 经销商按实际Level固定列（一级=1, 二级=2, 三级=3, 无级别经销商=1）
        foreach (var n in nodes.Values)
        {
            int lvl = DealerLevelFromType(n.Type);
            if (lvl > 0) n.Tier = lvl; // 一级→1, 二级→2, 三级→3
            else if (n.Type == "经销商") n.Tier = 1; // 未分级经销商放一级列
        }

        // BFS: 计算终端客户 Tier（从已固定 Tier 的节点向下传播，取最大值确保在右侧）
        var queue = new Queue<string>();
        var visited = new HashSet<string>();

        // 从所有非终端节点出发
        foreach (var n in nodes.Values)
            if (n.Tier != int.MaxValue && n.Type != "终端销售")
            {
                queue.Enqueue(n.Id);
                visited.Add(n.Id);
            }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var currentTier = nodes[current].Tier;

            foreach (var edge in edges.Values.Where(e => e.SourceId == current))
            {
                if (nodes.TryGetValue(edge.TargetId, out var target))
                {
                    var newTier = currentTier + 1;
                    // 非经销商节点取最大 Tier（确保终端列在最右）
                    if (DealerLevelFromType(target.Type) == 0 && newTier > target.Tier)
                        target.Tier = newTier;
                    if (visited.Add(edge.TargetId))
                        queue.Enqueue(edge.TargetId);
                }
            }
        }

        // 仍未到达的节点归入最大 tier
        int maxTier = nodes.Values.Where(n => n.Tier != int.MaxValue).Select(n => n.Tier).DefaultIfEmpty(1).Max();
        foreach (var n in nodes.Values)
            if (n.Tier == int.MaxValue) n.Tier = maxTier + 1;
    }

    /// <summary>检测串货冲突</summary>
    private static void DetectChannelConflicts(Dictionary<string, FlowNodeDto> nodes, Dictionary<string, FlowEdgeDto> edges, List<string> supplierIds)
    {
        // 获取一级经销商的省份基准（从生产厂家直接出货的经销商）
        var tier1ProvMap = new Dictionary<string, string>(); // nodeId → province
        foreach (var edge in edges.Values.Where(e => supplierIds.Contains(e.SourceId)))
        {
            if (nodes.TryGetValue(edge.TargetId, out var t1Node) && !string.IsNullOrEmpty(t1Node.Province))
                tier1ProvMap[edge.TargetId] = t1Node.Province;
        }

        // 追溯每条从一级商出发的下游路径
        foreach (var kv in tier1ProvMap)
        {
            var t1Id = kv.Key;
            var expectedProv = kv.Value;
            PropagateConflictCheck(t1Id, expectedProv, nodes, edges);
        }
    }

    private static void PropagateConflictCheck(string sourceId, string expectedProv,
        Dictionary<string, FlowNodeDto> nodes, Dictionary<string, FlowEdgeDto> edges)
    {
        foreach (var edge in edges.Values.Where(e => e.SourceId == sourceId))
        {
            if (nodes.TryGetValue(edge.TargetId, out var target) && !string.IsNullOrEmpty(target.Province))
            {
                if (target.Province != expectedProv)
                {
                    target.IsChannelConflict = true;
                    target.ConflictDesc = $"串货预警: 应销[{expectedProv}], 实际流向[{target.Province}]";
                }
                // 递归检查下游
                PropagateConflictCheck(edge.TargetId, expectedProv, nodes, edges);
            }
        }
    }

    public async Task<int> ClearAllAsync()
    {
        var count = await _db.DistributionRecords.CountAsync();
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM DistributionRecords");
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM Inventories");
        return count;
    }

    public async Task<int> ClearBySourceAsync(bool isPurchase)
    {
        string where;
        if (isPurchase) where = "SupplierId IS NOT NULL";
        else where = "FromCustomerId IS NOT NULL";
        var count = await _db.DistributionRecords.CountAsync();
        await _db.Database.ExecuteSqlRawAsync($"DELETE FROM DistributionRecords WHERE {where}");
        return count;
    }

    public async Task DeleteDistributionRecordAsync(Guid recordId)
    {
        var record = await _db.DistributionRecords
            .Include(r => r.Drug)
            .FirstOrDefaultAsync(r => r.Id == recordId);

        if (record == null) return;

        // Reverse inventory changes
        if (record.SupplierId.HasValue)
        {
            // 采购记录：减少本公司库存
            if (record.ToCustomerId.HasValue && record.ToCustomerId != Guid.Empty)
            {
                var inv = await _db.Inventories
                    .FirstOrDefaultAsync(i => i.DrugId == record.DrugId && i.BatchNo == record.BatchNo && i.CustomerId == record.ToCustomerId);
                if (inv != null)
                {
                    inv.Quantity -= record.Quantity;
                    if (inv.Quantity < 0) inv.Quantity = 0;
                }
            }
        }
        else
        {
            // 销售流转记录：还原From库存，减少To库存
            if (record.FromCustomerId.HasValue && record.FromCustomerId != Guid.Empty)
            {
                var fromInv = await _db.Inventories
                    .FirstOrDefaultAsync(i => i.DrugId == record.DrugId && i.BatchNo == record.BatchNo && i.CustomerId == record.FromCustomerId);
                if (fromInv != null)
                {
                    fromInv.Quantity += record.Quantity;
                }
            }

            if (record.ToCustomerId.HasValue && record.ToCustomerId != Guid.Empty)
            {
                var toInv = await _db.Inventories
                    .FirstOrDefaultAsync(i => i.DrugId == record.DrugId && i.BatchNo == record.BatchNo && i.CustomerId == record.ToCustomerId);
                if (toInv != null)
                {
                    toInv.Quantity -= record.Quantity;
                    if (toInv.Quantity < 0) toInv.Quantity = 0;
                }
            }
        }

        _db.DistributionRecords.Remove(record);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateDistributionRecordAsync(Guid recordId, decimal newQuantity)
    {
        var record = await _db.DistributionRecords.FirstOrDefaultAsync(r => r.Id == recordId);
        if (record == null || newQuantity < 0) return;

        var oldQty = record.Quantity;
        var diff = newQuantity - oldQty;
        if (diff == 0) return;

        record.Quantity = newQuantity;

        if (record.SupplierId.HasValue)
        {
            // 采购记录：调整本公司库存
            if (record.ToCustomerId.HasValue && record.ToCustomerId != Guid.Empty)
            {
                var inv = await _db.Inventories
                    .FirstOrDefaultAsync(i => i.DrugId == record.DrugId && i.BatchNo == record.BatchNo && i.CustomerId == record.ToCustomerId);
                if (inv != null)
                {
                    inv.Quantity += diff;
                    if (inv.Quantity < 0) inv.Quantity = 0;
                }
            }
        }
        else
        {
            // 销售流转记录：调整From和To库存
            if (record.FromCustomerId.HasValue && record.FromCustomerId != Guid.Empty)
            {
                var fromInv = await _db.Inventories
                    .FirstOrDefaultAsync(i => i.DrugId == record.DrugId && i.BatchNo == record.BatchNo && i.CustomerId == record.FromCustomerId);
                if (fromInv != null)
                {
                    fromInv.Quantity -= diff;
                    if (fromInv.Quantity < 0) fromInv.Quantity = 0;
                }
            }

            if (record.ToCustomerId.HasValue && record.ToCustomerId != Guid.Empty)
            {
                var toInv = await _db.Inventories
                    .FirstOrDefaultAsync(i => i.DrugId == record.DrugId && i.BatchNo == record.BatchNo && i.CustomerId == record.ToCustomerId);
                if (toInv != null)
                {
                    toInv.Quantity += diff;
                    if (toInv.Quantity < 0) toInv.Quantity = 0;
                }
            }
        }

        await _db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<ChannelConflictDto>> GetChannelConflictsAsync(string? batchNo = null)
    {
        var result = new List<ChannelConflictDto>();
        var query = _db.DistributionRecords.Include(r => r.Drug).Include(r => r.ToCustomer).Include(r => r.FromCustomer).Include(r => r.Supplier).AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(batchNo)) query = query.Where(r => r.BatchNo.Contains(batchNo));
        var records = await query.OrderBy(r => r.DistributionDate).ToListAsync();
        // 从供应商直接出货的记录找到一级经销商（不再通过SELF）
        var level1 = records.Where(r => r.SupplierId != null && r.ToCustomer != null).ToList();
        foreach (var l1 in level1)
        {
            var t1Prov = l1.ToCustomer!.Province ?? ""; var t1Name = l1.ToCustomer!.Name;
            if (string.IsNullOrEmpty(t1Prov)) continue;
            var level2 = records.Where(r => r.FromCustomerId == l1.ToCustomerId && r.ToCustomer != null).ToList();
            foreach (var l2 in level2)
            {
                var l2Prov = l2.ToCustomer!.Province ?? "";
                if (!string.IsNullOrEmpty(l2Prov) && t1Prov != l2Prov)
                    result.Add(new ChannelConflictDto { DrugName = l2.Drug.Name, BatchNo = l2.BatchNo, Distributor = t1Name, ExpectedRegion = t1Prov, ActualCustomer = l2.ToCustomer!.Name, ActualRegion = l2Prov, Quantity = l2.Quantity, Date = l2.DistributionDate });
                var level3 = records.Where(r => r.FromCustomerId == l2.ToCustomerId && r.ToCustomer != null).ToList();
                foreach (var l3 in level3)
                {
                    var l3Prov = l3.ToCustomer!.Province ?? "";
                    if (!string.IsNullOrEmpty(l3Prov) && t1Prov != l3Prov)
                        result.Add(new ChannelConflictDto { DrugName = l3.Drug.Name, BatchNo = l3.BatchNo, Distributor = t1Name, ExpectedRegion = t1Prov, ActualCustomer = l3.ToCustomer!.Name, ActualRegion = l3Prov, Quantity = l3.Quantity, Date = l3.DistributionDate });
                }
            }
        }
        return result;
    }

    /// <summary>Determine the distribution level based on chain position</summary>
    private static int GetDistributionLevel(Guid? fromCustomerId, List<DistributionRecord> allRecords)
    {
        if (fromCustomerId == null) return 0;
        // Trace back to count how many hops from source
        var visited = new HashSet<Guid>();
        var current = fromCustomerId.Value;
        var level = 0;

        while (current != Guid.Empty && !visited.Contains(current))
        {
            visited.Add(current);
            var prev = allRecords.FirstOrDefault(r => r.ToCustomerId == current);
            if (prev?.FromCustomerId == null) break;
            current = prev.FromCustomerId.Value;
            level++;
            if (level > 10) break; // safety
        }

        return level + 1;
    }
}
