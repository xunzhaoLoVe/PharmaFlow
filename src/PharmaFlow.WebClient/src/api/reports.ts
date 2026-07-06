import apiClient, { getData } from './client';
import type { ApiResponse } from '../types';

export interface ReportMetric {
  salesQuantity: number;
  salesAmount: number;
  purchaseQuantity: number;
  purchaseAmount: number;
  flowQuantity: number;
  inventoryQuantity: number;
  dealerPurchaseQuantity: number;
  dealerTerminalSalesQuantity: number;
  dealerInventoryQuantity: number;
  dealerTransferQuantity: number;
  flowRecordCount: number;
  batchCount: number;
  drugCount: number;
  dealerCount: number;
  terminalCustomerCount: number;
  provinceCount: number;
  lowStockCount: number;
  expiringCount: number;
  averageDailyFlow: number;
}

export interface ReportDimensionItem {
  name: string;
  quantity: number;
  amount: number;
  count: number;
  percent: number;
}

export interface ReportTrendPoint {
  period: string;
  salesQuantity: number;
  purchaseQuantity: number;
  flowQuantity: number;
  salesAmount: number;
  purchaseAmount: number;
}

export interface ReportRankItem {
  name: string;
  subName?: string;
  quantity: number;
  amount: number;
  count: number;
}

export interface ReportInventoryRisk {
  drugName: string;
  specification?: string;
  batchNo: string;
  customerName?: string;
  quantity: number;
  warningLowQuantity: number;
  expiryDate?: string;
  riskType: string;
}

export interface EndCustomerSale {
  customerName: string;
  province?: string;
  drugName: string;
  specification?: string;
  totalQuantity: number;
}

export interface ReportOverview {
  startDate: string;
  endDate: string;
  granularity: string;
  metrics: ReportMetric;
  drugDistribution: ReportDimensionItem[];
  customerDistribution: ReportDimensionItem[];
  provinceDistribution: ReportDimensionItem[];
  sourceDistribution: ReportDimensionItem[];
  dealerLevelDistribution: ReportDimensionItem[];
  trend: ReportTrendPoint[];
  topDrugs: ReportRankItem[];
  topCustomers: ReportRankItem[];
  inventoryRisks: ReportInventoryRisk[];
  endCustomerSales: EndCustomerSale[];
}

export async function getReportOverview(params: {
  startDate?: string;
  endDate?: string;
  granularity?: string;
  drugName?: string;
  customerName?: string;
}): Promise<ReportOverview> {
  return getData(await apiClient.get<ApiResponse<ReportOverview>>('/reports/overview', { params }));
}
