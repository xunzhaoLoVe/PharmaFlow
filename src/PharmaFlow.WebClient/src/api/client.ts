import axios from 'axios';
import type { ApiResponse } from '../types';

const apiClient = axios.create({
  baseURL: '/api',
  timeout: 15000,
});

// 请求拦截器：附加 JWT Token
apiClient.interceptors.request.use((config) => {
  const token = localStorage.getItem('token');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// 响应拦截器：统一处理
apiClient.interceptors.response.use(
  (res) => res.data,
  (err) => {
    if (err.response?.status === 401) {
      localStorage.removeItem('token');
      localStorage.removeItem('user');
      window.location.href = '/login';
    }
    return Promise.reject(err);
  },
);

export default apiClient;

// 辅助函数：提取 data
export function getData<T>(res: ApiResponse<T>): T {
  return res.data!;
}
