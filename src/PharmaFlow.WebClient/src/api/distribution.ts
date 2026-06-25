import apiClient, { getData } from './client';
import type { ApiResponse, DistributionRecordDto, DownstreamRecordDto, PagedResult } from '../types';

export async function getDistribution(params: {
  page?: number; size?: number; drugName?: string; batchNo?: string; customerId?: string;
  from?: string; to?: string; fromSelf?: boolean;
}): Promise<PagedResult<DistributionRecordDto>> {
  return getData(await apiClient.get<ApiResponse<PagedResult<DistributionRecordDto>>>('/distribution', { params }));
}

export async function recordDownstream(dto: DownstreamRecordDto): Promise<void> {
  return getData(await apiClient.post<ApiResponse<unknown>>('/distribution/downstream', dto));
}

export async function deleteDistribution(id: string): Promise<void> {
  await apiClient.delete(`/distribution/${id}`);
}
