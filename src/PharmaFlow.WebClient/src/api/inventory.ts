import apiClient, { getData } from './client';
import type { ApiResponse, InventoryDto, PagedResult } from '../types';

export async function getInventory(params: {
  page?: number; size?: number; search?: string; expiring?: boolean; lowStock?: boolean;
}): Promise<PagedResult<InventoryDto>> {
  return getData(await apiClient.get<ApiResponse<PagedResult<InventoryDto>>>('/inventory', { params }));
}

export async function getExpiringInventory(days?: number): Promise<InventoryDto[]> {
  return getData(await apiClient.get<ApiResponse<InventoryDto[]>>('/inventory/expiring', { params: { days } }));
}

export async function getLowStockInventory(): Promise<InventoryDto[]> {
  return getData(await apiClient.get<ApiResponse<InventoryDto[]>>('/inventory/low-stock'));
}
