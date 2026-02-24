import { apiClient } from '@/lib/api/client';
import type { ApiResponse } from '@/types/auth';
import type {
  StartParseResponse,
  ParseJobsResponse,
  ParseJob,
} from '@/types/lesson-parser';

// ============================================
// Parse Endpoints
// ============================================

export async function submitDocument(
  file: File,
  connectionId: string
): Promise<StartParseResponse> {
  const formData = new FormData();
  formData.append('file', file);

  const response = await apiClient.post<StartParseResponse>(
    `/lesson-parser/parse/document?connectionId=${encodeURIComponent(connectionId)}`,
    formData,
    { headers: { 'Content-Type': 'multipart/form-data' } }
  );

  return response.data;
}

/** @deprecated Use submitDocument instead */
export async function submitPdf(
  file: File,
  connectionId: string
): Promise<StartParseResponse> {
  const formData = new FormData();
  formData.append('file', file);

  const response = await apiClient.post<StartParseResponse>(
    `/lesson-parser/parse/pdf?connectionId=${encodeURIComponent(connectionId)}`,
    formData,
    { headers: { 'Content-Type': 'multipart/form-data' } }
  );

  return response.data;
}

/** @deprecated Use submitDocument instead */
export async function submitDocx(
  file: File,
  connectionId: string
): Promise<StartParseResponse> {
  const formData = new FormData();
  formData.append('file', file);

  const response = await apiClient.post<StartParseResponse>(
    `/lesson-parser/parse/docx?connectionId=${encodeURIComponent(connectionId)}`,
    formData,
    { headers: { 'Content-Type': 'multipart/form-data' } }
  );

  return response.data;
}

export async function submitUrl(
  url: string,
  connectionId: string
): Promise<StartParseResponse> {
  const response = await apiClient.post<StartParseResponse>(
    `/lesson-parser/parse/url?connectionId=${encodeURIComponent(connectionId)}`,
    { url }
  );

  return response.data;
}

export async function submitText(
  content: string,
  title: string,
  connectionId: string
): Promise<StartParseResponse> {
  const response = await apiClient.post<StartParseResponse>(
    `/lesson-parser/parse/text?connectionId=${encodeURIComponent(connectionId)}`,
    { content, title }
  );

  return response.data;
}

// ============================================
// Job Endpoints
// ============================================

export async function getJobs(
  page: number = 1,
  pageSize: number = 10
): Promise<ParseJobsResponse> {
  const queryParams = new URLSearchParams();
  queryParams.append('page', String(page));
  queryParams.append('pageSize', String(pageSize));

  const response = await apiClient.get<ApiResponse<ParseJobsResponse>>(
    `/lesson-parser/jobs?${queryParams.toString()}`
  );

  const data = response.data.data;
  if (!data) {
    return {
      items: [],
      totalCount: 0,
      pageNumber: page,
      pageSize,
      totalPages: 0,
    };
  }

  return data;
}

export async function getJob(id: string): Promise<ParseJob> {
  const response = await apiClient.get<ParseJob>(`/lesson-parser/jobs/${id}`);
  return response.data;
}

export async function retryJob(id: string): Promise<void> {
  await apiClient.post(`/lesson-parser/jobs/${id}/retry`);
}

export const lessonParserApi = {
  submitDocument,
  submitPdf,
  submitDocx,
  submitUrl,
  submitText,
  getJobs,
  getJob,
  retryJob,
};
