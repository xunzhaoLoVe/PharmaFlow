namespace PharmaFlow.Application.DTOs;

/// <summary>流向图 — 包含所有节点和有向边</summary>
public record FlowGraphDto
{
    public string DrugName { get; set; } = string.Empty;
    public string BatchNo { get; set; } = string.Empty;
    public string? SupplierName { get; set; }
    public DateTime PurchaseDate { get; set; }
    /// <summary>所有唯一节点（Key = CustomerId字符串）</summary>
    public Dictionary<string, FlowNodeDto> Nodes { get; set; } = new();
    /// <summary>所有聚合边（Key = "SourceId→TargetId"）</summary>
    public Dictionary<string, FlowEdgeDto> Edges { get; set; } = new();
    /// <summary>按Tier分组后的节点列表，用于分层渲染</summary>
    public SortedDictionary<int, List<FlowNodeDto>> Tiers { get; set; } = new();
}

/// <summary>流向图节点 — 唯一客户实体</summary>
public record FlowNodeDto
{
    /// <summary>CustomerId 字符串，唯一标识</summary>
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>供应商 / 生产厂家 / 一级经销 / 二级经销 / 三级经销 / 终端销售</summary>
    public string Type { get; set; } = string.Empty;
    /// <summary>距离SELF的最短距离（BFS计算），0=供应商，1=SELF，2=一级经销...</summary>
    public int Tier { get; set; }
    /// <summary>所有入边数量之和（兼容旧名 InQuantity）</summary>
    public decimal InQuantity { get; set; }
    /// <summary>所有出边数量之和（兼容旧名 OutQuantity）</summary>
    public decimal OutQuantity { get; set; }
    public decimal StockQuantity { get; set; }
    public string? Province { get; set; }
    public bool IsChannelConflict { get; set; }
    public string? ConflictDesc { get; set; }
    /// <summary>最早流转日期（兼容旧名 Date）</summary>
    public DateTime? Date { get; set; }
}

/// <summary>流向图有向边 — 表示一次或多次聚合的流转</summary>
public record FlowEdgeDto
{
    public string SourceId { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public string TargetName { get; set; } = string.Empty;
    /// <summary>聚合后的流转总数量（负数=退货）</summary>
    public decimal Quantity { get; set; }
    public DateTime? Date { get; set; }
    /// <summary>是否为同级调拨（同一Tier内的流转）</summary>
    public bool IsTransfer { get; set; }
    /// <summary>是否为退货（数量为负）</summary>
    public bool IsReturn => Quantity < 0;
}
