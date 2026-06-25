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
