import apiClient, { getData } from './client';
import type { ApiResponse, LoginRequest, LoginResult } from '../types';

export async function login(data: LoginRequest): Promise<LoginResult> {
  return getData(await apiClient.post<ApiResponse<LoginResult>>('/auth/login', data));
}
