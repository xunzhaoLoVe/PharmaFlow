// ── 通用 ──
export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  pageIndex: number;
  pageSize: number;
  totalPages: number;
}

export interface DropdownItem {
  id: string;
  name: string;
}

// ── 认证 ──
export interface LoginRequest {
  username: string;
  password: string;
}

export interface LoginResult {
  success: boolean;
  message?: string;
  userId?: string;
  displayName?: string;
  role?: string;
  permissions: string[];
}

// ── 药品 ──
export interface DrugDto {
  id: string;
  code: string;
  name: string;
  genericName?: string;
  specification?: string;
  manufacturer?: string;
  approvalNumber?: string;
  dosageForm?: string;
  unit: string;
  categoryName?: string;
  categoryId?: string;
  warningLowQuantity: number;
}

// ── 客户 ──
export interface CustomerDto {
  id: string;
  code: string;
  name: string;
  shortName?: string;
  contactPerson?: string;
  phone?: string;
  address?: string;
  province?: string;
  salesPerson?: string;
  drugName?: string;
  customerTypeName?: string;
  categoryName?: string;
  categoryId?: string;
  level?: number;
  levelName?: string;
  distributorRole?: number;
  distributorRoleName?: string;
}

// ── 供应商 ──
export interface SupplierDto {
  id: string;
  code: string;
  name: string;
  contactPerson?: string;
  phone?: string;
  address?: string;
}

// ── 库存 ──
export interface InventoryDto {
  id: string;
  drugCode: string;
  drugName: string;
  specification: string;
  manufacturer: string;
  batchNo: string;
  quantity: number;
  expiryDate?: string;
  location?: string;
  isExpiring: boolean;
  isLowStock: boolean;
  customerName?: string;
  customerLevel?: string;
  province?: string;
}

// ── 采购 ──
export interface PurchaseOrderDto {
  id: string;
  orderNo: string;
  supplierName: string;
  supplierId: string;
  orderDate: string;
  totalAmount: number;
  status: string;
  operator?: string;
  remark?: string;
  itemCount: number;
}

export interface PurchaseOrderDetailDto {
  id: string;
  orderNo: string;
  supplierName: string;
  orderDate: string;
  totalAmount: number;
  status: string;
  operator?: string;
  remark?: string;
  items: PurchaseOrderItemDetailDto[];
}

export interface PurchaseOrderItemDetailDto {
  drugName: string;
  batchNo: string;
  quantity: number;
  unitPrice: number;
  totalPrice: number;
  productionDate?: string;
  expiryDate?: string;
}

export interface PurchaseOrderItemDto {
  drugId: string;
  drugName: string;
  batchNo: string;
  quantity: number;
  taxedUnitPrice: number;
  totalPrice: number;
  productionDate?: string;
  expiryDate?: string;
}

// ── 销售 ──
export interface SalesOrderDto {
  id: string;
  orderNo: string;
  customerName: string;
  customerId: string;
  orderDate: string;
  totalAmount: number;
  status: string;
  operator?: string;
  remark?: string;
  itemCount: number;
}

export interface SalesOrderDetailDto {
  id: string;
  orderNo: string;
  customerName: string;
  orderDate: string;
  totalAmount: number;
  status: string;
  operator?: string;
  remark?: string;
  items: SalesOrderItemDetailDto[];
}

export interface SalesOrderItemDetailDto {
  drugName: string;
  batchNo: string;
  quantity: number;
  unitPrice: number;
  totalPrice: number;
  productionDate?: string;
  expiryDate?: string;
}

export interface SalesOrderItemDto {
  drugId: string;
  drugName: string;
  batchNo: string;
  quantity: number;
  unitPrice: number;
  totalPrice: number;
  productionDate?: string;
  expiryDate?: string;
}

// ── 流向 ──
export interface DistributionRecordDto {
  id: string;
  drugName: string;
  specification?: string;
  batchNo: string;
  toCustomerName?: string;
  fromCustomerName?: string;
  quantity: number;
  taxedUnitPrice?: number;
  salesPerson?: string;
  deliveryOrderNo?: string;
  distributionDate: string;
  operator?: string;
  province?: string;
  distributionDateDisplay?: string;
}

export interface DistributionImportResultDto {
  imported: number;
  skipped: number;
  supplemented: number;
}

export interface DownstreamRecordDto {
  drugId: string;
  drugName?: string;
  batchNo: string;
  fromCustomerId?: string;
  toCustomerId?: string;
  toCustomerName?: string;
  quantity: number;
  taxedUnitPrice?: number;
  salesPerson?: string;
  deliveryOrderNo?: string;
  distributionDate: string;
  distributionDateDisplay?: string;
  province?: string;
}

// ── 仪表盘 ──
export interface DashboardStats {
  todayPurchaseCount: number;
  todaySalesCount: number;
  totalDrugTypes: number;
  lowStockCount: number;
  expiringCount: number;
  todayPurchaseAmount: number;
  todaySalesAmount: number;
  salesTrend: ChartDataPoint[];
}

export interface ChartDataPoint {
  label: string;
  value: number;
}

// ── API 响应 ──
export interface ApiResponse<T> {
  success: boolean;
  message?: string;
  data?: T;
}

// ── 数据采集 ──
export interface CollectionSourceDto {
  id: string;
  name: string;
  platformType: string;
  baseUrl: string;
  authType: string;
  collectionMode: string;
  authConfig: string;
  exportConfig: string;
  transformConfig: string;
  isEnabled: boolean;
  scheduleCron?: string;
  ruleId?: string;
  ruleName?: string;
  autoSubmit: boolean;
  lastCollectionAt?: string;
  lastCollectionStatus?: string;
}

export interface CollectionTaskDto {
  id: string;
  sourceId: string;
  sourceName: string;
  triggerType: string;
  status: string;
  startedAt?: string;
  completedAt?: string;
  totalRecords: number;
  successCount: number;
  errorCount: number;
  skippedCount: number;
  isSubmitted: boolean;
  errorMessage?: string;
}

export interface CollectionRecordDto {
  id: string;
  taskId: string;
  sourceId: string;
  sourceName: string;
  status: string;
  rawDataJson: string;
  cleanedDataJson?: string;
  targetEntityType?: string;
  targetEntityId?: string;
  reviewerName?: string;
  reviewedAt?: string;
  rejectReason?: string;
  errorMessage?: string;
}

export interface CollectionSummaryRecordDto {
  id: string;
  taskId: string;
  sourceId: string;
  sourceName: string;
  dataType: string;
  drugName: string;
  specification?: string;
  batchNo?: string;
  companyName?: string;
  toCustomerName?: string;
  taxedUnitPrice?: number;
  quantity?: number;
  distributionDate?: string;
  province?: string;
  expiryDate?: string;
  targetEntityId?: string;
  syncStatus: string;
  createdAt: string;
}
