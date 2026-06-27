import apiClient, { getData } from './client';
import type { ApiResponse, PagedResult, DropdownItem, CollectionSourceDto, CollectionTaskDto, CollectionRecordDto } from '../types';

// ── Sources ──
export async function getSources(params: {
  page?: number; size?: number; search?: string;
}): Promise<PagedResult<CollectionSourceDto>> {
  return getData(await apiClient.get<ApiResponse<PagedResult<CollectionSourceDto>>>('/collection/sources', { params }));
}

export async function getSourceById(id: string): Promise<CollectionSourceDto> {
  return getData(await apiClient.get<ApiResponse<CollectionSourceDto>>(`/collection/sources/${id}`));
}

export async function createSource(source: Partial<CollectionSourceDto>): Promise<CollectionSourceDto> {
  return getData(await apiClient.post<ApiResponse<CollectionSourceDto>>('/collection/sources', source));
}

export async function updateSource(id: string, source: Partial<CollectionSourceDto>): Promise<void> {
  await apiClient.put(`/collection/sources/${id}`, source);
}

export async function deleteSource(id: string): Promise<void> {
  await apiClient.delete(`/collection/sources/${id}`);
}

export async function getSourceDropdown(): Promise<DropdownItem[]> {
  return getData(await apiClient.get<ApiResponse<DropdownItem[]>>('/collection/sources/dropdown'));
}

// ── Tasks ──
export async function getTasks(params: {
  page?: number; size?: number; sourceId?: string;
}): Promise<PagedResult<CollectionTaskDto>> {
  return getData(await apiClient.get<ApiResponse<PagedResult<CollectionTaskDto>>>('/collection/tasks', { params }));
}

export async function getTaskById(id: string): Promise<CollectionTaskDto> {
  return getData(await apiClient.get<ApiResponse<CollectionTaskDto>>(`/collection/tasks/${id}`));
}

export async function deleteTask(id: string): Promise<void> {
  await apiClient.delete(`/collection/tasks/${id}`);
}

export async function submitTask(id: string): Promise<void> {
  await apiClient.post(`/collection/tasks/${id}/submit`);
}

export async function clearSummary(): Promise<void> {
  await apiClient.post('/collection/summary/clear');
}

export async function triggerCollection(sourceId: string): Promise<CollectionTaskDto> {
  return getData(await apiClient.post<ApiResponse<CollectionTaskDto>>('/collection/trigger', { sourceId }));
}

// ── Records ──
export async function getRecords(params: {
  page?: number; size?: number; taskId?: string; sourceId?: string; status?: string;
}): Promise<PagedResult<CollectionRecordDto>> {
  return getData(await apiClient.get<ApiResponse<PagedResult<CollectionRecordDto>>>('/collection/records', { params }));
}

export async function approveRecords(recordIds: string[]): Promise<void> {
  await apiClient.post('/collection/records/approve', { recordIds });
}

export async function rejectRecords(recordIds: string[], reason: string): Promise<void> {
  await apiClient.post('/collection/records/reject', { recordIds, reason });
}

export async function importRecords(recordIds: string[]): Promise<void> {
  await apiClient.post('/collection/records/import', { recordIds });
}

// ── Batch ──
export interface BatchPreview {
  taskId: string;
  totalRecords: number;
  drugs: DrugMatchItem[];
  customers: CustomerMatchItem[];
}

export interface DrugMatchItem {
  name: string;
  originalSpec?: string;
  normalizedSpec?: string;
  manufacturer?: string;
  code?: string;
  matched: boolean;
  existingId?: string;
  existingName?: string;
  existingSpec?: string;
}

export interface CustomerMatchItem {
  name: string;
  code?: string;
  province?: string;
  matched: boolean;
  existingId?: string;
  existingName?: string;
}

export interface BatchImportResult {
  createdDrugs: number;
  createdCustomers: number;
  imported: number;
  skipped: number;
}

export async function previewBatch(taskId: string): Promise<BatchPreview> {
  return getData(await apiClient.get<ApiResponse<BatchPreview>>(`/collection/batch/${taskId}/preview`));
}

export async function importBatch(taskId: string): Promise<BatchImportResult> {
  return getData(await apiClient.post<ApiResponse<BatchImportResult>>(`/collection/batch/${taskId}/import`));
}

// ── Distribution Review ──
export interface DistributionReviewDto {
  id: string; drugName: string; batchNo?: string; toCustomerName?: string;
  quantity: number; taxedUnitPrice: number; distributionDate: string;
  province?: string; reviewStatus?: string; rejectReason?: string;
}

export async function getDistributionReview(params: { page?: number; size?: number; status?: string }): Promise<PagedResult<DistributionReviewDto>> {
  return getData(await apiClient.get<ApiResponse<PagedResult<DistributionReviewDto>>>('/distribution/review', { params }));
}

export async function approveDistributionReview(ids: string[]): Promise<void> {
  await apiClient.post('/distribution/review/approve', { recordIds: ids });
}

export async function rejectDistributionReview(ids: string[], reason: string): Promise<void> {
  await apiClient.post('/distribution/review/reject', { recordIds: ids, reason });
}
