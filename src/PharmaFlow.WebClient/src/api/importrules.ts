import apiClient, { getData } from './client';
import type { ApiResponse, PagedResult, DropdownItem } from '../types';

export interface ImportRuleDto {
  id: string;
  name: string;
  description?: string;
  moduleConfigs: ModuleRuleConfigDto[];
}

export interface ModuleRuleConfigDto {
  id: string;
  ruleId: string;
  moduleName: string;
  fieldMappings: string;
  normalizeRules?: string;
  matchConfig?: string;
  autoCreate: boolean;
}

export async function getRules(params: { page?: number; size?: number }): Promise<PagedResult<ImportRuleDto>> {
  return getData(await apiClient.get<ApiResponse<PagedResult<ImportRuleDto>>>('/importrules', { params }));
}

export async function getRuleById(id: string): Promise<ImportRuleDto> {
  return getData(await apiClient.get<ApiResponse<ImportRuleDto>>(`/importrules/${id}`));
}

export async function createRule(rule: Partial<ImportRuleDto>): Promise<ImportRuleDto> {
  return getData(await apiClient.post<ApiResponse<ImportRuleDto>>('/importrules', rule));
}

export async function updateRule(id: string, rule: Partial<ImportRuleDto>): Promise<void> {
  await apiClient.put(`/importrules/${id}`, rule);
}

export async function deleteRule(id: string): Promise<void> {
  await apiClient.delete(`/importrules/${id}`);
}

export async function getRuleDropdown(): Promise<DropdownItem[]> {
  return getData(await apiClient.get<ApiResponse<DropdownItem[]>>('/importrules/dropdown'));
}