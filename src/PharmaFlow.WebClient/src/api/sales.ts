import apiClient, { getData } from './client';
import type { ApiResponse, SalesOrderDto, SalesOrderDetailDto, SalesOrderItemDto, PagedResult } from '../types';

export async function getSales(params: {
  page?: number; size?: number; status?: string;
}): Promise<PagedResult<SalesOrderDto>> {
  return getData(await apiClient.get<ApiResponse<PagedResult<SalesOrderDto>>>('/sales', { params }));
}

export async function getSaleById(id: string): Promise<SalesOrderDto> {
  return getData(await apiClient.get<ApiResponse<SalesOrderDto>>(`/sales/${id}`));
}

export async function getSaleDetail(id: string): Promise<SalesOrderDetailDto> {
  return getData(await apiClient.get<ApiResponse<SalesOrderDetailDto>>(`/sales/${id}/detail`));
}

export async function createSale(data: {
  customerId: string; salesType: string; remark?: string; items: SalesOrderItemDto[]; operator?: string;
}): Promise<string> {
  return getData(await apiClient.post<ApiResponse<string>>('/sales', data));
}

export async function auditSale(id: string, auditor?: string): Promise<void> {
  await apiClient.put(`/sales/${id}/audit`, null, { params: { auditor } });
}

export async function completeSale(id: string): Promise<void> {
  await apiClient.put(`/sales/${id}/complete`);
}

export async function cancelSale(id: string): Promise<void> {
  await apiClient.put(`/sales/${id}/cancel`);
}
