import apiClient, { getData } from './client';
import type { ApiResponse, DrugDto, DropdownItem, PagedResult } from '../types';

export async function getDrugs(params: {
  page?: number; size?: number; search?: string; categoryId?: string;
}): Promise<PagedResult<DrugDto>> {
  return getData(await apiClient.get<ApiResponse<PagedResult<DrugDto>>>('/drugs', { params }));
}

export async function getDrugById(id: string): Promise<DrugDto> {
  return getData(await apiClient.get<ApiResponse<DrugDto>>(`/drugs/${id}`));
}

export async function createDrug(drug: Partial<DrugDto>): Promise<DrugDto> {
  return getData(await apiClient.post<ApiResponse<DrugDto>>('/drugs', drug));
}

export async function updateDrug(id: string, drug: Partial<DrugDto>): Promise<void> {
  await apiClient.put(`/drugs/${id}`, drug);
}

export async function deleteDrug(id: string): Promise<void> {
  await apiClient.delete(`/drugs/${id}`);
}

export async function getDrugDropdown(): Promise<DropdownItem[]> {
  return getData(await apiClient.get<ApiResponse<DropdownItem[]>>('/drugs/dropdown'));
}

export async function getDrugCategories(): Promise<DropdownItem[]> {
  return getData(await apiClient.get<ApiResponse<DropdownItem[]>>('/drugs/categories'));
}
