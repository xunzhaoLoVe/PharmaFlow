import apiClient, { getData } from './client';
import type { ApiResponse, DistributionImportResultDto, DistributionRecordDto, DownstreamRecordDto, PagedResult } from '../types';

export async function getDistribution(params: {
  page?: number; size?: number; drugName?: string; batchNo?: string; customerId?: string;
  from?: string; to?: string; fromSelf?: boolean;
}): Promise<PagedResult<DistributionRecordDto>> {
  return getData(await apiClient.get<ApiResponse<PagedResult<DistributionRecordDto>>>('/distribution', { params }));
}

export async function recordDownstream(dto: DownstreamRecordDto): Promise<DistributionImportResultDto | void> {
  return getData(await apiClient.post<ApiResponse<DistributionImportResultDto | unknown>>('/distribution/downstream', dto)) as any;
}

export async function deleteDistribution(id: string): Promise<void> {
  await apiClient.delete(`/distribution/${id}`);
}
