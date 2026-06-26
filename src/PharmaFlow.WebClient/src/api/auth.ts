import apiClient, { getData } from './client';
import type { ApiResponse, LoginRequest, LoginResult } from '../types';

export async function login(data: LoginRequest & { remember?: boolean }): Promise<LoginResult> {
  return getData(await apiClient.post<ApiResponse<LoginResult>>(
    `/auth/login?rememberMe=${!!data.remember}`, data
  ));
}

export async function changePassword(oldPassword: string, newPassword: string): Promise<void> {
  await apiClient.post('/auth/change-password', { oldPassword, newPassword });
}
