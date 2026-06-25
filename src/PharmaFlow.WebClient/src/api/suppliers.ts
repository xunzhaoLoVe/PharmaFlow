import apiClient, { getData } from './client';
import type { ApiResponse, SupplierDto, DropdownItem, PagedResult } from '../types';

export async function getSuppliers(params: {
  page?: number; size?: number; search?: string;
}): Promise<PagedResult<SupplierDto>> {
  return getData(await apiClient.get<ApiResponse<PagedResult<SupplierDto>>>('/suppliers', { params }));
}

export async function getSupplierById(id: string): Promise<SupplierDto> {
  return getData(await apiClient.get<ApiResponse<SupplierDto>>(`/suppliers/${id}`));
}

export async function createSupplier(supplier: Partial<SupplierDto>): Promise<SupplierDto> {
  return getData(await apiClient.post<ApiResponse<SupplierDto>>('/suppliers', supplier));
}

export async function updateSupplier(id: string, supplier: Partial<SupplierDto>): Promise<void> {
  await apiClient.put(`/suppliers/${id}`, supplier);
}

export async function deleteSupplier(id: string): Promise<void> {
  await apiClient.delete(`/suppliers/${id}`);
}

export async function getSupplierDropdown(): Promise<DropdownItem[]> {
  return getData(await apiClient.get<ApiResponse<DropdownItem[]>>('/suppliers/dropdown'));
}
