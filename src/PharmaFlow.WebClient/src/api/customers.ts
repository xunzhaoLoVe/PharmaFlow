import apiClient, { getData } from './client';
import type { ApiResponse, CustomerDto, DropdownItem, PagedResult } from '../types';

export async function getCustomers(params: {
  page?: number; size?: number; search?: string; categoryId?: string; type?: string;
}): Promise<PagedResult<CustomerDto>> {
  return getData(await apiClient.get<ApiResponse<PagedResult<CustomerDto>>>('/customers', { params }));
}

export async function getCustomerById(id: string): Promise<CustomerDto> {
  return getData(await apiClient.get<ApiResponse<CustomerDto>>(`/customers/${id}`));
}

export async function createCustomer(customer: Partial<CustomerDto>): Promise<CustomerDto> {
  return getData(await apiClient.post<ApiResponse<CustomerDto>>('/customers', customer));
}

export async function updateCustomer(id: string, customer: Partial<CustomerDto>): Promise<void> {
  await apiClient.put(`/customers/${id}`, customer);
}

export async function deleteCustomer(id: string): Promise<void> {
  await apiClient.delete(`/customers/${id}`);
}

export async function getCustomerDropdown(level?: string): Promise<DropdownItem[]> {
  return getData(await apiClient.get<ApiResponse<DropdownItem[]>>('/customers/dropdown', { params: { level } }));
}

export async function getCustomerCategories(): Promise<DropdownItem[]> {
  return getData(await apiClient.get<ApiResponse<DropdownItem[]>>('/customers/categories'));
}
