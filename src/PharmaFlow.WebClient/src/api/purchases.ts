import apiClient, { getData } from './client';
import type { ApiResponse, PurchaseOrderDto, PurchaseOrderDetailDto, PurchaseOrderItemDto, PagedResult } from '../types';

export async function getPurchases(params: {
  page?: number; size?: number; status?: string;
}): Promise<PagedResult<PurchaseOrderDto>> {
  return getData(await apiClient.get<ApiResponse<PagedResult<PurchaseOrderDto>>>('/purchases', { params }));
}

export async function getPurchaseById(id: string): Promise<PurchaseOrderDto> {
  return getData(await apiClient.get<ApiResponse<PurchaseOrderDto>>(`/purchases/${id}`));
}

export async function getPurchaseDetail(id: string): Promise<PurchaseOrderDetailDto> {
  return getData(await apiClient.get<ApiResponse<PurchaseOrderDetailDto>>(`/purchases/${id}/detail`));
}

export async function createPurchase(data: {
  supplierId: string; remark?: string; items: PurchaseOrderItemDto[]; operator?: string;
}): Promise<string> {
  return getData(await apiClient.post<ApiResponse<string>>('/purchases', data));
}

export async function auditPurchase(id: string, auditor?: string): Promise<void> {
  await apiClient.put(`/purchases/${id}/audit`, null, { params: { auditor } });
}

export async function completePurchase(id: string): Promise<void> {
  await apiClient.put(`/purchases/${id}/complete`);
}

export async function cancelPurchase(id: string): Promise<void> {
  await apiClient.put(`/purchases/${id}/cancel`);
}
