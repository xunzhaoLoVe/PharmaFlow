import apiClient, { getData } from './client';
import type { ApiResponse, DashboardStats } from '../types';

export async function getDashboardStats(): Promise<DashboardStats> {
  return getData(await apiClient.get<ApiResponse<DashboardStats>>('/dashboard/stats'));
}
