import { apiClient } from '../client';
import type {
  ComplianceReport,
  OverdueItem,
  CompletionDetail,
  PaginatedResponse,
  SkillsMatrix,
} from '@/types/toolbox-talks';

// ============================================
// API Response Types
// ============================================

interface ApiResponse<T> {
  data: T;
  success?: boolean;
  message?: string;
}

// ============================================
// Report Parameters
// ============================================

export interface ComplianceReportParams {
  dateFrom?: string;
  dateTo?: string;
  siteId?: string;
}

export interface OverdueReportParams {
  siteId?: string;
  toolboxTalkId?: string;
}

export interface CompletionsReportParams {
  dateFrom?: string;
  dateTo?: string;
  toolboxTalkId?: string;
  siteId?: string;
  pageNumber?: number;
  pageSize?: number;
}

export interface SkillsMatrixParams {
  category?: string;
}

// ============================================
// API Functions
// ============================================

export async function getComplianceReport(params?: ComplianceReportParams): Promise<ComplianceReport> {
  const response = await apiClient.get<ApiResponse<ComplianceReport>>(
    '/toolbox-talks/reports/compliance',
    { params }
  );
  return response.data.data;
}

export async function getOverdueReport(params?: OverdueReportParams): Promise<OverdueItem[]> {
  const response = await apiClient.get<ApiResponse<OverdueItem[]>>(
    '/toolbox-talks/reports/overdue',
    { params }
  );
  return response.data.data;
}

export async function getCompletionsReport(
  params?: CompletionsReportParams
): Promise<PaginatedResponse<CompletionDetail>> {
  const response = await apiClient.get<ApiResponse<PaginatedResponse<CompletionDetail>>>(
    '/toolbox-talks/reports/completions',
    { params }
  );
  return response.data.data;
}

export async function exportOverdueReport(params?: OverdueReportParams): Promise<Blob> {
  const response = await apiClient.get('/toolbox-talks/reports/overdue/export', {
    params,
    responseType: 'blob',
  });
  return response.data;
}

export async function exportCompletionsReport(
  params?: Omit<CompletionsReportParams, 'pageNumber' | 'pageSize'>
): Promise<Blob> {
  const response = await apiClient.get('/toolbox-talks/reports/completions/export', {
    params,
    responseType: 'blob',
  });
  return response.data;
}

export async function getSkillsMatrix(params?: SkillsMatrixParams): Promise<SkillsMatrix> {
  const response = await apiClient.get<ApiResponse<SkillsMatrix>>(
    '/toolbox-talks/reports/skills-matrix',
    { params }
  );
  return response.data.data;
}

export async function exportSkillsMatrix(params?: SkillsMatrixParams): Promise<Blob> {
  const response = await apiClient.get('/toolbox-talks/reports/skills-matrix/export', {
    params,
    responseType: 'blob',
  });
  return response.data;
}

export async function exportComplianceReport(params?: ComplianceReportParams): Promise<Blob> {
  const response = await apiClient.get('/toolbox-talks/reports/compliance/export', {
    params,
    responseType: 'blob',
  });
  return response.data;
}
