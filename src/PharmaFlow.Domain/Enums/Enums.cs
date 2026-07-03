namespace PharmaFlow.Domain.Enums;

/// <summary>客户类型</summary>
public enum CustomerType
{
    医院 = 0,
    药店 = 1,
    诊所 = 2,
    商业 = 3,
    卫生院 = 4,
    其他 = 5,
    卫生服务中心 = 6,
    卫生室 = 7,
    药房 = 8,
    门诊 = 9,
    卫生服务站 = 10,
    二级经销商 = 11
}

/// <summary>订单状态</summary>
public enum OrderStatus
{
    待审 = 0,
    已审 = 1,
    已完成 = 2,
    已取消 = 3
}

/// <summary>库存变动类型</summary>
public enum InventoryChangeType
{
    入库 = 0,
    出库 = 1,
}

/// <summary>销售类型</summary>
public enum SalesType
{
    批发 = 0,
}

/// <summary>经销商级别</summary>
public enum DistributorLevel
{
    一级经销商 = 1,
    二级经销商 = 2,
    三级经销商 = 3,
}

/// <summary>采集平台类型</summary>
public enum CollectionPlatformType
{
    流向平台 = 0,
    ERP系统 = 1,
    其他 = 2,
}

/// <summary>采集认证方式</summary>
public enum CollectionAuthType
{
    Cookie = 0,
    Token = 1,
    BasicAuth = 2,
}

/// <summary>采集任务状态</summary>
public enum CollectionTaskStatus
{
    待执行 = 0,
    执行中 = 1,
    已完成 = 2,
    失败 = 3,
}

/// <summary>采集记录状态</summary>
public enum CollectionRecordStatus
{
    原始 = 0,
    清洗中 = 1,
    待审核 = 2,
    已通过 = 3,
    已驳回 = 4,
    已导入 = 5,
    错误 = 6,
}

/// <summary>触发方式</summary>
public enum TriggerType
{
    手动 = 0,
    定时 = 1,
}

/// <summary>采集配置模式</summary>
public enum CollectionMode
{
    /// <summary>JSON配置：使用 AuthConfig + ExportConfig 进行 HTTP 认证和导出</summary>
    JsonConfig = 0,
    /// <summary>Python脚本：直接运行 Python 脚本接管采集流程</summary>
    PythonScript = 1,
}
