using Microsoft.EntityFrameworkCore;
using PharmaFlow.Application.DTOs;
using PharmaFlow.Domain.Entities;
using PharmaFlow.Domain.Enums;
using PharmaFlow.Infrastructure.Data;

namespace PharmaFlow.Application.Services;

public class DistributionService : IDistributionService
{
    private readonly AppDbContext _db;
    private const string SummarySyncOperator = "采集汇总同步";
    public DistributionService(AppDbContext db) => _db = db;

    private record TraceSupplierInfo(string NodeId, string Name, decimal Quantity, DateTime FirstDate);

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

    public async Task<DistributionImportResultDto> RecordDownstreamAsync(DownstreamRecordDto dto, string operatorName)
    {
        var self = await _db.Customers.FirstOrDefaultAsync(c => c.Code == "SELF");
        if (self == null) throw new InvalidOperationException("系统错误：本公司(SELF)记录不存在");
        await PrepareMasterDataAsync(new List<DownstreamRecordDto> { dto }, self.Id);
        var result = await RecordOneAsync(dto, operatorName, self.Id);
        await _db.SaveChangesAsync();
        return result;
    }

    /// <summary>批量导入下游出货记录，一次性提交（性能优化）</summary>
    public async Task<DistributionImportResultDto> RecordDownstreamBatchAsync(List<DownstreamRecordDto> dtos, string operatorName)
    {
        var result = new DistributionImportResultDto();
        if (dtos.Count == 0) return result;

        var self = await _db.Customers.FirstOrDefaultAsync(c => c.Code == "SELF");
        if (self == null) throw new InvalidOperationException("系统错误：本公司(SELF)记录不存在");

        await PrepareMasterDataAsync(dtos, self.Id);

        var drugIds = dtos.Select(d => d.DrugId).Distinct().ToList();
        var batchNos = dtos.Select(d => d.BatchNo).Distinct().ToList();
        var toCustomerIds = dtos.Select(d => d.ToCustomerId == Guid.Empty ? self.Id : d.ToCustomerId);
        var fromCustomerIds = dtos
            .Where(d => !d.SupplierId.HasValue || d.SupplierId == Guid.Empty)
            .Select(d => d.FromCustomerId == Guid.Empty ? self.Id : d.FromCustomerId);
        var customerIds = toCustomerIds.Concat(fromCustomerIds).Distinct().ToList();

        var inventories = await _db.Inventories
            .Where(i => drugIds.Contains(i.DrugId) && batchNos.Contains(i.BatchNo) && i.CustomerId.HasValue && customerIds.Contains(i.CustomerId.Value))
            .ToListAsync();
        var invDict = inventories.ToDictionary(i => (i.DrugId, i.BatchNo, i.CustomerId!.Value), i => i);

        var existingRecords = await _db.DistributionRecords
            .Where(r => drugIds.Contains(r.DrugId) && batchNos.Contains(r.BatchNo))
            .ToListAsync();
        var existingByKey = existingRecords
            .GroupBy(BuildDistributionKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var dto in dtos)
        {
            bool hasSupplier = dto.SupplierId.HasValue && dto.SupplierId != Guid.Empty;
            Guid? fromCustomerId = hasSupplier ? null : (dto.FromCustomerId == Guid.Empty ? self.Id : dto.FromCustomerId);
            Guid? toId = dto.ToCustomerId == Guid.Empty ? self.Id : dto.ToCustomerId;
            var distributionDate = ParseDistributionDate(dto);
            var key = BuildDistributionKey(dto.DrugId, dto.BatchNo, hasSupplier ? dto.SupplierId : null, fromCustomerId, toId, dto.Quantity, dto.DeliveryOrderNo, distributionDate);

            if (!seenKeys.Add(key))
            {
                result.Skipped++;
                continue;
            }

            if (existingByKey.TryGetValue(key, out var existing))
            {
                if (SupplementDistributionRecord(existing, dto, distributionDate)) result.Supplemented++;
                result.Skipped++;
                continue;
            }

            var record = new DistributionRecord
            {
                DrugId = dto.DrugId, BatchNo = dto.BatchNo,
                SupplierId = hasSupplier ? dto.SupplierId : null,
                FromCustomerId = fromCustomerId, ToCustomerId = toId,
                Quantity = dto.Quantity, SalesPerson = dto.SalesPerson,
                DeliveryOrderNo = dto.DeliveryOrderNo, TaxedUnitPrice = dto.TaxedUnitPrice,
                DistributionDate = distributionDate,
                DistributionDateDisplay = dto.DistributionDateDisplay, Operator = operatorName, Province = dto.Province,
            };
            _db.DistributionRecords.Add(record);
            existingByKey[key] = record;

            if (!hasSupplier && fromCustomerId.HasValue)
            {
                var fromKey = (dto.DrugId, dto.BatchNo, fromCustomerId.Value);
                if (invDict.TryGetValue(fromKey, out var fromInv))
                { fromInv.Quantity -= dto.Quantity; if (fromInv.Quantity < 0) fromInv.Quantity = 0; }
            }

            if (toId.HasValue)
            {
                var toKey = (dto.DrugId, dto.BatchNo, toId.Value);
                if (invDict.TryGetValue(toKey, out var toInv))
                { toInv.Quantity += dto.Quantity; if (toInv.Quantity < 0) toInv.Quantity = 0; }
                else if (dto.Quantity > 0)
                {
                    var newInv = new Inventory { DrugId = dto.DrugId, BatchNo = dto.BatchNo, Quantity = dto.Quantity, CustomerId = toId.Value };
                    _db.Inventories.Add(newInv);
                    invDict[toKey] = newInv;
                }
            }
            result.Imported++;
        }

        await _db.SaveChangesAsync();
        return result;
    }

    private async Task PrepareMasterDataAsync(List<DownstreamRecordDto> dtos, Guid selfId)
    {
        var drugs = await _db.Drugs.Where(d => !d.IsDeleted).ToListAsync();
        var customers = await _db.Customers.Where(c => !c.IsDeleted && c.Code != "SELF").ToListAsync();

        var inferredLevels = InferDistributorLevels(dtos, customers);

        foreach (var dto in dtos)
        {
            if (dto.DrugId == Guid.Empty && !string.IsNullOrWhiteSpace(dto.DrugName))
                dto.DrugId = ResolveDrug(dto, drugs).Id;

            if (dto.DrugId == Guid.Empty)
                continue;

            var drugName = ResolveDrugName(dto, drugs);
            var isPurchaseFlow = IsPurchaseFlow(dto);

            if (isPurchaseFlow)
            {
                if (dto.ToCustomerId == Guid.Empty && !string.IsNullOrWhiteSpace(dto.ToCustomerName))
                {
                    dto.ToCustomerId = ResolveCustomer(
                        dto.ToCustomerName,
                        drugName,
                        dto.Province,
                        CustomerType.商业,
                        DistributorLevel.一级经销商,
                        InferDistributorRole(dto.ToCustomerName),
                        customers).Id;
                }
                continue;
            }

            if (dto.FromCustomerId == Guid.Empty && !string.IsNullOrWhiteSpace(dto.FromCustomerName))
            {
                var fromLevel = LookupInferredLevel(inferredLevels, dto.FromCustomerName);
                dto.FromCustomerId = ResolveCustomer(
                    dto.FromCustomerName,
                    drugName,
                    null,
                    CustomerType.商业,
                    fromLevel,
                    InferDistributorRole(dto.FromCustomerName),
                    customers).Id;
            }

            if (dto.ToCustomerId == Guid.Empty && !string.IsNullOrWhiteSpace(dto.ToCustomerName))
            {
                var toType = InferCustomerType(dto.ToCustomerName);
                var toLevel = toType == CustomerType.商业 ? LookupInferredLevel(inferredLevels, dto.ToCustomerName) : null;
                dto.ToCustomerId = ResolveCustomer(
                    dto.ToCustomerName,
                    drugName,
                    dto.Province,
                    toType,
                    toLevel,
                    toType == CustomerType.商业 ? InferDistributorRole(dto.ToCustomerName) : null,
                    customers).Id;
            }
        }
    }

    private Drug ResolveDrug(DownstreamRecordDto dto, List<Drug> drugs)
    {
        var name = dto.DrugName.Trim();
        var specification = NormalizeOptional(dto.Specification);
        var drug = drugs.FirstOrDefault(d =>
            SameText(d.Name, name)
            && (string.IsNullOrWhiteSpace(specification) || SameText(d.Specification, specification)));

        drug ??= drugs.FirstOrDefault(d =>
            SameText(d.Name, name)
            && (string.IsNullOrWhiteSpace(specification) || ContainsNormalized(d.Specification, specification) || ContainsNormalized(specification, d.Specification)));

        if (drug != null)
            return drug;

        drug = new Drug
        {
            Code = $"AUTO-DRUG-{Guid.NewGuid():N}"[..18],
            Name = name,
            Specification = specification,
            Unit = "盒",
        };
        _db.Drugs.Add(drug);
        drugs.Add(drug);
        return drug;
    }

    private static string ResolveDrugName(DownstreamRecordDto dto, List<Drug> drugs)
    {
        var drug = drugs.FirstOrDefault(d => d.Id == dto.DrugId);
        return drug?.Name ?? dto.DrugName;
    }

    private Customer ResolveCustomer(
        string name,
        string drugName,
        string? province,
        CustomerType customerType,
        DistributorLevel? level,
        DistributorRole? role,
        List<Customer> customers)
    {
        name = name.Trim();
        var normalizedName = NormalizeKey(name);
        var customer = customers
            .Where(c => NormalizeKey(c.Name) == normalizedName)
            .OrderByDescending(c => CustomerMatchesDrug(c, drugName))
            .ThenBy(c => c.CustomerType == customerType ? 0 : 1)
            .FirstOrDefault();

        if (customer == null)
        {
            customer = new Customer
            {
                Code = $"AUTO-CUST-{Guid.NewGuid():N}"[..18],
                Name = name,
                Province = NormalizeOptional(province),
                DrugName = NormalizeOptional(drugName),
                CustomerType = customerType,
                Level = customerType == CustomerType.商业 ? level : null,
                DistributorRole = customerType == CustomerType.商业 ? role : null,
            };
            _db.Customers.Add(customer);
            customers.Add(customer);
            return customer;
        }

        var changed = false;
        if (!CustomerMatchesDrug(customer, drugName) && !string.IsNullOrWhiteSpace(drugName))
        {
            customer.DrugName = string.IsNullOrWhiteSpace(customer.DrugName)
                ? drugName
                : customer.DrugName + "\n" + drugName;
            changed = true;
        }
        if (string.IsNullOrWhiteSpace(customer.Province) && !string.IsNullOrWhiteSpace(province))
        {
            customer.Province = province.Trim();
            changed = true;
        }
        if (customer.CustomerType != customerType && customerType == CustomerType.商业)
        {
            customer.CustomerType = CustomerType.商业;
            changed = true;
        }
        if (customer.CustomerType == CustomerType.商业)
        {
            if (!customer.Level.HasValue && level.HasValue) { customer.Level = level; changed = true; }
            if (!customer.DistributorRole.HasValue && role.HasValue) { customer.DistributorRole = role; changed = true; }
        }
        if (changed) customer.UpdatedAt = DateTime.UtcNow;
        return customer;
    }

    private Dictionary<string, DistributorLevel> InferDistributorLevels(List<DownstreamRecordDto> dtos, List<Customer> customers)
    {
        var levels = customers
            .Where(c => c.CustomerType == CustomerType.商业 && c.Level.HasValue)
            .GroupBy(c => NormalizeKey(c.Name), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Min(c => c.Level!.Value), StringComparer.OrdinalIgnoreCase);

        var dealerNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var incoming = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var edges = new List<(string From, string To)>();

        foreach (var dto in dtos)
        {
            if (IsPurchaseFlow(dto))
            {
                if (!string.IsNullOrWhiteSpace(dto.ToCustomerName))
                {
                    var to = NormalizeKey(dto.ToCustomerName);
                    levels[to] = DistributorLevel.一级经销商;
                    dealerNames.Add(to);
                }
                continue;
            }

            if (string.IsNullOrWhiteSpace(dto.FromCustomerName))
                continue;

            var from = NormalizeKey(dto.FromCustomerName);
            dealerNames.Add(from);

            if (!string.IsNullOrWhiteSpace(dto.ToCustomerName) && InferCustomerType(dto.ToCustomerName) == CustomerType.商业)
            {
                var to = NormalizeKey(dto.ToCustomerName);
                dealerNames.Add(to);
                incoming.Add(to);
                edges.Add((from, to));
            }
        }

        foreach (var root in dealerNames.Where(n => !incoming.Contains(n)))
            levels.TryAdd(root, DistributorLevel.一级经销商);

        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var (from, to) in edges)
            {
                if (!levels.TryGetValue(from, out var fromLevel))
                    continue;

                var nextLevel = (DistributorLevel)Math.Min(3, (int)fromLevel + 1);
                if (!levels.TryGetValue(to, out var current) || nextLevel < current)
                {
                    levels[to] = nextLevel;
                    changed = true;
                }
            }
        }

        foreach (var name in dealerNames)
            levels.TryAdd(name, DistributorLevel.一级经销商);

        return levels;
    }

    private static DistributorLevel? LookupInferredLevel(Dictionary<string, DistributorLevel> levels, string name)
        => levels.TryGetValue(NormalizeKey(name), out var level) ? level : null;

    private static bool IsPurchaseFlow(DownstreamRecordDto dto)
        => (dto.SupplierId.HasValue && dto.SupplierId != Guid.Empty)
            || (dto.FromCustomerId == Guid.Empty && string.IsNullOrWhiteSpace(dto.FromCustomerName));

    private static CustomerType InferCustomerType(string? name)
    {
        var text = name ?? string.Empty;
        if (ContainsAny(text, "卫生院", "卫生室", "卫生服务中心", "社区卫生", "卫生服务站"))
            return CustomerType.基层医疗机构;
        if (ContainsAny(text, "医院", "卫生院"))
            return CustomerType.医院;
        if (ContainsAny(text, "药店", "药房", "大药房", "医药连锁"))
            return CustomerType.药店;
        if (ContainsAny(text, "诊所", "门诊"))
            return CustomerType.诊所;
        return CustomerType.商业;
    }

    private static DistributorRole? InferDistributorRole(string? name)
    {
        var text = name ?? string.Empty;
        if (ContainsAny(text, "配送")) return DistributorRole.配送公司;
        if (ContainsAny(text, "代理")) return DistributorRole.代理商;
        return DistributorRole.商业公司;
    }

    private static bool ContainsAny(string text, params string[] keywords)
        => keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));

    private static bool CustomerMatchesDrug(Customer customer, string drugName)
        => string.IsNullOrWhiteSpace(drugName)
            || string.IsNullOrWhiteSpace(customer.DrugName)
            || customer.DrugName.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(d => SameText(d, drugName));

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool SameText(string? left, string? right)
        => string.Equals(NormalizeKey(left), NormalizeKey(right), StringComparison.OrdinalIgnoreCase);

    private static bool ContainsNormalized(string? source, string? value)
    {
        var normalizedSource = NormalizeKey(source);
        var normalizedValue = NormalizeKey(value);
        return normalizedSource.Length > 0 && normalizedValue.Length > 0 && normalizedSource.Contains(normalizedValue, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeKey(string? value)
        => (value ?? string.Empty)
            .Trim()
            .Replace('（', '(')
            .Replace('）', ')')
            .Replace(" ", string.Empty)
            .Replace("\u3000", string.Empty);

    private async Task<DistributionImportResultDto> RecordOneAsync(DownstreamRecordDto dto, string operatorName, Guid selfId)
    {
        bool hasSupplier = dto.SupplierId.HasValue && dto.SupplierId != Guid.Empty;
        Guid? fromId = hasSupplier ? null : (dto.FromCustomerId == Guid.Empty ? selfId : dto.FromCustomerId);
        Guid? toId = dto.ToCustomerId == Guid.Empty ? selfId : dto.ToCustomerId;
        var distributionDate = ParseDistributionDate(dto);
        var key = BuildDistributionKey(dto.DrugId, dto.BatchNo, hasSupplier ? dto.SupplierId : null, fromId, toId, dto.Quantity, dto.DeliveryOrderNo, distributionDate);

        var existingRecords = await _db.DistributionRecords
            .Where(r => r.DrugId == dto.DrugId && r.BatchNo == dto.BatchNo)
            .ToListAsync();
        var existing = existingRecords.FirstOrDefault(r => string.Equals(BuildDistributionKey(r), key, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            var supplemented = SupplementDistributionRecord(existing, dto, distributionDate);
            return new DistributionImportResultDto { Skipped = 1, Supplemented = supplemented ? 1 : 0 };
        }

        _db.DistributionRecords.Add(new DistributionRecord
        {
            DrugId = dto.DrugId, BatchNo = dto.BatchNo,
            SupplierId = hasSupplier ? dto.SupplierId : null,
            FromCustomerId = fromId, ToCustomerId = toId,
            Quantity = dto.Quantity, SalesPerson = dto.SalesPerson,
            DeliveryOrderNo = dto.DeliveryOrderNo, TaxedUnitPrice = dto.TaxedUnitPrice,
            DistributionDate = distributionDate,
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

        return new DistributionImportResultDto { Imported = 1 };
    }

    private static DateTime ParseDistributionDate(DownstreamRecordDto dto)
        => DateTime.TryParse(dto.DistributionDateDisplay ?? dto.DistributionDate.ToString("yyyy-MM-dd"), out var date)
            ? date
            : DateTime.Now;

    private static string BuildDistributionKey(DistributionRecord r)
        => BuildDistributionKey(r.DrugId, r.BatchNo, r.SupplierId, r.FromCustomerId, r.ToCustomerId, r.Quantity, r.DeliveryOrderNo, r.DistributionDate);

    private static string BuildDistributionKey(
        Guid drugId,
        string? batchNo,
        Guid? supplierId,
        Guid? fromCustomerId,
        Guid? toCustomerId,
        decimal quantity,
        string? deliveryOrderNo,
        DateTime distributionDate)
        => MasterDataMatchHelper.DistributionRecordKey(
            drugId,
            batchNo,
            supplierId,
            fromCustomerId,
            toCustomerId,
            quantity,
            deliveryOrderNo,
            distributionDate);

    private static bool SupplementDistributionRecord(DistributionRecord target, DownstreamRecordDto source, DateTime distributionDate)
    {
        var changed = false;
        if (string.IsNullOrWhiteSpace(target.SalesPerson) && !string.IsNullOrWhiteSpace(source.SalesPerson)) { target.SalesPerson = source.SalesPerson; changed = true; }
        if (string.IsNullOrWhiteSpace(target.DeliveryOrderNo) && !string.IsNullOrWhiteSpace(source.DeliveryOrderNo)) { target.DeliveryOrderNo = source.DeliveryOrderNo; changed = true; }
        if (string.IsNullOrWhiteSpace(target.Province) && !string.IsNullOrWhiteSpace(source.Province)) { target.Province = source.Province; changed = true; }
        if (string.IsNullOrWhiteSpace(target.DistributionDateDisplay) && !string.IsNullOrWhiteSpace(source.DistributionDateDisplay)) { target.DistributionDateDisplay = source.DistributionDateDisplay; changed = true; }
        if (target.TaxedUnitPrice == 0 && source.TaxedUnitPrice != 0) { target.TaxedUnitPrice = source.TaxedUnitPrice; changed = true; }
        if (target.DistributionDate == default) { target.DistributionDate = distributionDate; changed = true; }
        if (changed) target.UpdatedAt = DateTime.UtcNow;
        return changed;
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

        var purchaseQuery = _db.PurchaseOrderItems
            .Include(pi => pi.Drug)
            .Include(pi => pi.PurchaseOrder).ThenInclude(po => po.Supplier)
            .Where(pi => pi.PurchaseOrder.Status == OrderStatus.已完成);
        if (!string.IsNullOrWhiteSpace(batchNo) && batchNo.Length >= 4)
            purchaseQuery = purchaseQuery.Where(pi => pi.BatchNo.Contains(batchNo));
        if (drugId != Guid.Empty)
            purchaseQuery = purchaseQuery.Where(pi => pi.DrugId == drugId);

        var purchaseItems = await purchaseQuery.OrderBy(pi => pi.PurchaseOrder.OrderDate).ToListAsync();
        var salesQuery = _db.SalesOrderItems
            .Include(si => si.Drug)
            .Include(si => si.SalesOrder).ThenInclude(so => so.Customer)
            .Where(si => si.SalesOrder.Status == OrderStatus.已完成);
        if (!string.IsNullOrWhiteSpace(batchNo) && batchNo.Length >= 4)
            salesQuery = salesQuery.Where(si => si.BatchNo.Contains(batchNo));
        if (drugId != Guid.Empty)
            salesQuery = salesQuery.Where(si => si.DrugId == drugId);

        var salesItems = await salesQuery.OrderBy(si => si.SalesOrder.OrderDate).ToListAsync();
        if (records.Count == 0 && purchaseItems.Count == 0 && salesItems.Count == 0) return null;

        var drug = records.FirstOrDefault()?.Drug ?? purchaseItems.FirstOrDefault()?.Drug ?? salesItems.First().Drug;
        var selfId = await _db.Customers.Where(c => c.Code == "SELF").Select(c => (Guid?)c.Id).FirstOrDefaultAsync();

        var batchNos = records.Select(r => r.BatchNo)
            .Concat(purchaseItems.Select(pi => pi.BatchNo))
            .Concat(salesItems.Select(si => si.BatchNo))
            .Distinct()
            .ToList();

        var inventories = await _db.Inventories
            .Where(i => i.DrugId == drug.Id && batchNos.Contains(i.BatchNo))
            .ToListAsync();

        var nodes = new Dictionary<string, FlowNodeDto>();
        var edges = new Dictionary<string, FlowEdgeDto>();

        var supplierGroups = records
            .Where(r => r.SupplierId.HasValue)
            .GroupBy(r => new { Id = r.SupplierId!.Value, Name = r.Supplier?.Name ?? "供应商" })
            .Select(g => new TraceSupplierInfo($"SUP_{g.Key.Id}", g.Key.Name, g.Sum(r => r.Quantity), g.Min(r => r.DistributionDate)))
            .Concat(purchaseItems
                .GroupBy(pi => new { pi.PurchaseOrder.SupplierId, pi.PurchaseOrder.Supplier.Name })
                .Select(g => new TraceSupplierInfo($"SUP_{g.Key.SupplierId}", g.Key.Name, (decimal)g.Sum(pi => pi.Quantity), g.Min(pi => pi.PurchaseOrder.OrderDate))))
            .GroupBy(s => s.NodeId)
            .Select(g => new TraceSupplierInfo(g.Key, g.First().Name, g.Sum(x => x.Quantity), g.Min(x => x.FirstDate)))
            .OrderBy(s => s.FirstDate)
            .ToList();

        foreach (var supplier in supplierGroups)
        {
            nodes[supplier.NodeId] = new FlowNodeDto
            {
                Id = supplier.NodeId,
                Name = supplier.Name,
                Type = "生产厂家",
                Tier = 0,
                Date = supplier.FirstDate,
                OutQuantity = supplier.Quantity,
            };
        }

        var fallbackSource = supplierGroups.FirstOrDefault();
        if (fallbackSource == null && (records.Any(r => r.FromCustomerId == selfId || r.ToCustomerId == selfId) || salesItems.Count > 0))
        {
            var fallbackDate = records.Select(r => r.DistributionDate)
                .Concat(salesItems.Select(si => si.SalesOrder.OrderDate))
                .DefaultIfEmpty(DateTime.UtcNow)
                .Min();
            fallbackSource = new TraceSupplierInfo("SELF_SOURCE", "本公司(制药厂)", 0, fallbackDate);
            nodes[fallbackSource.NodeId] = new FlowNodeDto
            {
                Id = fallbackSource.NodeId,
                Name = fallbackSource.Name,
                Type = "生产厂家",
                Tier = 0,
                Date = fallbackSource.FirstDate,
            };
            supplierGroups.Add(fallbackSource);
        }

        var allCustomerIds = new HashSet<Guid>();
        foreach (var r in records)
        {
            if (r.FromCustomerId.HasValue && r.FromCustomerId != selfId) allCustomerIds.Add(r.FromCustomerId.Value);
            if (r.ToCustomerId.HasValue && r.ToCustomerId != selfId) allCustomerIds.Add(r.ToCustomerId.Value);
        }
        foreach (var si in salesItems)
        {
            if (si.SalesOrder.CustomerId != selfId) allCustomerIds.Add(si.SalesOrder.CustomerId);
        }

        var allCustomers = await _db.Customers.Where(c => allCustomerIds.Contains(c.Id)).ToListAsync();
        foreach (var c in allCustomers)
        {
            var nodeRecords = records.Where(r => r.ToCustomerId == c.Id || r.FromCustomerId == c.Id).ToList();
            var nodeDates = nodeRecords.Select(r => r.DistributionDate)
                .Concat(salesItems.Where(si => si.SalesOrder.CustomerId == c.Id).Select(si => si.SalesOrder.OrderDate))
                .ToList();
            nodes[c.Id.ToString()] = new FlowNodeDto
            {
                Id = c.Id.ToString(),
                Name = c.Name,
                Type = CustomerTraceType(c),
                Province = c.Province,
                StockQuantity = inventories.Where(i => i.CustomerId == c.Id).Sum(i => i.Quantity),
                Date = nodeDates.Count > 0 ? nodeDates.Min() : null,
            };
        }

        foreach (var r in records)
        {
            string? fromId, toId;
            string? fromName, toName;

            if (r.SupplierId.HasValue)
            {
                fromId = $"SUP_{r.SupplierId.Value}";
                if (!nodes.ContainsKey(fromId))
                {
                    nodes[fromId] = new FlowNodeDto
                    {
                        Id = fromId,
                        Name = r.Supplier?.Name ?? "供应商",
                        Type = "生产厂家",
                        Tier = 0,
                        Date = r.DistributionDate,
                    };
                }
                if (!r.ToCustomerId.HasValue || r.ToCustomerId == selfId) continue;
                toId = r.ToCustomerId?.ToString();
                fromName = r.Supplier?.Name ?? "";
                toName = r.ToCustomer?.Name ?? "";
            }
            else
            {
                if (!r.ToCustomerId.HasValue || r.ToCustomerId == selfId) continue;

                if (r.FromCustomerId == selfId)
                {
                    if (fallbackSource == null) continue;
                    fromId = fallbackSource.NodeId;
                    fromName = fallbackSource.Name;
                }
                else
                {
                    fromId = r.FromCustomerId?.ToString();
                    fromName = r.FromCustomer?.Name ?? "";
                }

                toId = r.ToCustomerId?.ToString();
                toName = r.ToCustomer?.Name ?? "";
            }

            if (fromId == null || toId == null || !nodes.ContainsKey(fromId) || !nodes.ContainsKey(toId)) continue;

            AddOrMergeTraceEdge(edges, fromId, toId, fromName, toName, r.Quantity, r.DistributionDate);
        }

        var recordedSalesOrderIds = records
            .Where(r => r.SalesOrderId.HasValue)
            .Select(r => r.SalesOrderId!.Value)
            .ToHashSet();
        foreach (var si in salesItems.Where(si => !recordedSalesOrderIds.Contains(si.SalesOrderId)))
        {
            if (fallbackSource == null || si.SalesOrder.CustomerId == selfId) continue;
            var toId = si.SalesOrder.CustomerId.ToString();
            if (!nodes.ContainsKey(toId)) continue;
            AddOrMergeTraceEdge(edges, fallbackSource.NodeId, toId, fallbackSource.Name, si.SalesOrder.Customer.Name, si.Quantity, si.SalesOrder.OrderDate);
        }

        ComputeTraceTiers(nodes, edges);

        foreach (var node in nodes.Values)
        {
            node.InQuantity = edges.Values.Where(e => e.TargetId == node.Id).Sum(e => e.Quantity);
            node.OutQuantity = edges.Values.Where(e => e.SourceId == node.Id).Sum(e => e.Quantity);
        }

        foreach (var edge in edges.Values)
        {
            var src = nodes.TryGetValue(edge.SourceId, out var s) ? s : null;
            var tgt = nodes.TryGetValue(edge.TargetId, out var t) ? t : null;
            if (src == null || tgt == null) continue;

            edge.IsTransfer = IsDealerNode(src) && IsDealerNode(tgt) && src.Tier == tgt.Tier;
        }

        DetectChannelConflicts(nodes, edges, supplierGroups.Select(s => s.NodeId).ToList());

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
            SupplierName = supplierGroups.FirstOrDefault()?.Name,
            PurchaseDate = supplierGroups.FirstOrDefault()?.FirstDate ?? records.Select(r => r.DistributionDate).DefaultIfEmpty(DateTime.UtcNow).Min(),
            Nodes = nodes, Edges = edges, Tiers = tiers,
        };
    }

    private static string CustomerTraceType(Customer c) => c.CustomerType switch
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

    private static bool IsDealerNode(FlowNodeDto node)
        => node.Type is "一级经销" or "二级经销" or "三级经销" or "经销商";

    private static int FixedTierForNode(FlowNodeDto node) => node.Type switch
    {
        "生产厂家" or "供应商" or "生产出厂" => 0,
        "一级经销" => 1,
        "二级经销" => 2,
        "三级经销" => 3,
        _ => int.MaxValue
    };

    private static void AddOrMergeTraceEdge(
        Dictionary<string, FlowEdgeDto> edges,
        string fromId,
        string toId,
        string fromName,
        string toName,
        decimal quantity,
        DateTime date)
    {
        var edgeKey = $"{fromId}->{toId}";
        if (edges.TryGetValue(edgeKey, out var existing))
        {
            existing.Quantity += quantity;
            if (!existing.Date.HasValue || date < existing.Date.Value)
                existing.Date = date;
            return;
        }

        edges[edgeKey] = new FlowEdgeDto
        {
            SourceId = fromId,
            TargetId = toId,
            SourceName = fromName,
            TargetName = toName,
            Quantity = quantity,
            Date = date,
        };
    }

    private static void ComputeTraceTiers(
        Dictionary<string, FlowNodeDto> nodes,
        Dictionary<string, FlowEdgeDto> edges)
    {
        foreach (var node in nodes.Values)
        {
            var fixedTier = FixedTierForNode(node);
            node.Tier = fixedTier == int.MaxValue
                ? (node.Type == "经销商" ? 1 : 4)
                : fixedTier;
        }

        var changed = true;
        var guard = 0;
        while (changed && guard++ < nodes.Count + edges.Count + 5)
        {
            changed = false;
            foreach (var edge in edges.Values)
            {
                if (!nodes.TryGetValue(edge.SourceId, out var src) || !nodes.TryGetValue(edge.TargetId, out var tgt))
                    continue;

                if (tgt.Type == "终端销售")
                {
                    var desired = Math.Max(4, src.Tier + 1);
                    if (desired > tgt.Tier)
                    {
                        tgt.Tier = desired;
                        changed = true;
                    }
                }
                else if (tgt.Type == "经销商")
                {
                    var desired = Math.Min(3, Math.Max(1, src.Tier + 1));
                    if (desired > tgt.Tier)
                    {
                        tgt.Tier = desired;
                        changed = true;
                    }
                }
            }
        }
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

    public async Task<int> ClearSalesByOriginAsync(string origin)
        => await ClearDistributionByOriginAsync(isPurchase: false, origin);

    public async Task<int> ClearPurchasesByOriginAsync(string origin)
        => await ClearDistributionByOriginAsync(isPurchase: true, origin);

    private async Task<int> ClearDistributionByOriginAsync(bool isPurchase, string origin)
    {
        var normalizedOrigin = origin.Trim().ToLowerInvariant();
        if (normalizedOrigin is not ("manual" or "sync"))
            throw new ArgumentException("清空类型无效", nameof(origin));

        var selfId = await _db.Customers
            .Where(c => c.Code == "SELF")
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync();

        var query = _db.DistributionRecords.AsQueryable();
        if (isPurchase)
        {
            query = query.Where(r => r.SupplierId != null);
        }
        else
        {
            query = query.Where(r => r.SupplierId == null && r.FromCustomerId != null);
            if (selfId.HasValue)
                query = query.Where(r => r.FromCustomerId != selfId.Value && r.ToCustomerId != selfId.Value);
        }

        query = normalizedOrigin == "sync"
            ? query.Where(r => r.Operator == SummarySyncOperator)
            : query.Where(r => r.Operator == null || r.Operator != SummarySyncOperator);

        var records = await query.ToListAsync();
        if (records.Count == 0)
            return 0;

        if (isPurchase)
            await ReversePurchaseInventoryAsync(records);
        else
            await ReverseSalesInventoryAsync(records);

        if (normalizedOrigin == "sync")
        {
            var targetIds = records.Select(r => r.Id).ToList();
            var collectionRecords = await _db.CollectionRecords
                .Where(r => r.TargetEntityType == "DistributionRecord"
                    && r.TargetEntityId.HasValue
                    && targetIds.Contains(r.TargetEntityId.Value))
                .ToListAsync();

            foreach (var record in collectionRecords)
            {
                record.TargetEntityId = null;
                record.UpdatedAt = DateTime.UtcNow;
            }
        }

        _db.DistributionRecords.RemoveRange(records);
        await _db.SaveChangesAsync();
        return records.Count;
    }

    private async Task ReversePurchaseInventoryAsync(List<DistributionRecord> records)
    {
        var drugIds = records.Select(r => r.DrugId).Distinct().ToList();
        var batchNos = records.Select(r => r.BatchNo).Distinct().ToList();
        var customerIds = records
            .Where(r => r.ToCustomerId.HasValue)
            .Select(r => r.ToCustomerId!.Value)
            .Distinct()
            .ToList();

        var inventories = await _db.Inventories
            .Where(i => drugIds.Contains(i.DrugId)
                && batchNos.Contains(i.BatchNo)
                && i.CustomerId.HasValue
                && customerIds.Contains(i.CustomerId.Value))
            .ToListAsync();

        var inventoryByKey = inventories
            .GroupBy(i => (i.DrugId, i.BatchNo, i.CustomerId!.Value))
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var record in records)
        {
            if (record.ToCustomerId.HasValue
                && inventoryByKey.TryGetValue((record.DrugId, record.BatchNo, record.ToCustomerId.Value), out var toInv))
            {
                toInv.Quantity -= record.Quantity;
                if (toInv.Quantity < 0) toInv.Quantity = 0;
            }
        }
    }

    private async Task ReverseSalesInventoryAsync(List<DistributionRecord> records)
    {
        var drugIds = records.Select(r => r.DrugId).Distinct().ToList();
        var batchNos = records.Select(r => r.BatchNo).Distinct().ToList();
        var customerIds = records
            .SelectMany(r => new[] { r.FromCustomerId, r.ToCustomerId })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var inventories = await _db.Inventories
            .Where(i => drugIds.Contains(i.DrugId)
                && batchNos.Contains(i.BatchNo)
                && i.CustomerId.HasValue
                && customerIds.Contains(i.CustomerId.Value))
            .ToListAsync();

        var inventoryByKey = inventories
            .GroupBy(i => (i.DrugId, i.BatchNo, i.CustomerId!.Value))
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var record in records)
        {
            if (record.FromCustomerId.HasValue
                && inventoryByKey.TryGetValue((record.DrugId, record.BatchNo, record.FromCustomerId.Value), out var fromInv))
            {
                fromInv.Quantity += record.Quantity;
            }

            if (record.ToCustomerId.HasValue
                && inventoryByKey.TryGetValue((record.DrugId, record.BatchNo, record.ToCustomerId.Value), out var toInv))
            {
                toInv.Quantity -= record.Quantity;
                if (toInv.Quantity < 0) toInv.Quantity = 0;
            }
        }
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
