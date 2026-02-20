import { apiClient } from '@/lib/api/client';
import type { ApiResponse } from '@/types/auth';

// ============================================
// Certificate Types
// ============================================

export interface CertificateDto {
  id: string;
  certificateNumber: string;
  certificateType: string; // "Talk" or "Course"
  trainingCode: string;
  trainingTitle: string;
  includedTalks?: string[];
  issuedAt: string;
  expiresAt?: string;
  isExpired: boolean;
  isExpiringSoon: boolean;
  isRefresher: boolean;
}

// ============================================
// My Certificates (Employee Portal)
// ============================================

export async function getMyCertificates(): Promise<CertificateDto[]> {
  const response = await apiClient.get<ApiResponse<CertificateDto[]>>(
    '/my/toolbox-talks/certificates'
  );

  return response.data.data ?? [];
}

export async function downloadCertificate(id: string): Promise<Blob> {
  const response = await apiClient.get(
    `/my/toolbox-talks/certificates/${id}/download`,
    {
      responseType: 'blob',
    }
  );
  return response.data;
}

// ============================================
// Admin Certificate Functions
// ============================================

export async function getEmployeeCertificates(employeeId: string): Promise<CertificateDto[]> {
  const response = await apiClient.get<ApiResponse<CertificateDto[]>>(
    `/toolbox-talks/certificates/by-employee/${employeeId}`
  );
  return response.data.data ?? [];
}

export async function downloadCertificateAdmin(id: string): Promise<Blob> {
  const response = await apiClient.get(
    `/toolbox-talks/certificates/${id}/download`,
    {
      responseType: 'blob',
    }
  );
  return response.data;
}

// ============================================
// Certificate Report Types
// ============================================

export interface CertificateReportDto {
  items: CertificateReportItemDto[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  totalCertificates: number;
  validCertificates: number;
  expiredCertificates: number;
  expiringSoonCertificates: number;
}

export interface CertificateReportItemDto {
  id: string;
  certificateNumber: string;
  certificateType: string;
  trainingCode: string;
  trainingTitle: string;
  employeeName: string;
  employeeCode?: string;
  employeeId: string;
  issuedAt: string;
  expiresAt?: string;
  isExpired: boolean;
  isExpiringSoon: boolean;
  isRefresher: boolean;
}

export interface CertificateReportParams {
  status?: string;
  type?: string;
  search?: string;
  page?: number;
  pageSize?: number;
}

// ============================================
// Certificate Report Functions
// ============================================

export async function getCertificateReport(params: CertificateReportParams): Promise<CertificateReportDto> {
  const searchParams = new URLSearchParams();
  if (params.status) searchParams.set('status', params.status);
  if (params.type) searchParams.set('type', params.type);
  if (params.search) searchParams.set('search', params.search);
  if (params.page) searchParams.set('page', params.page.toString());
  if (params.pageSize) searchParams.set('pageSize', params.pageSize.toString());

  const response = await apiClient.get<ApiResponse<CertificateReportDto>>(
    `/toolbox-talks/certificates/report?${searchParams}`
  );
  return response.data.data!;
}
