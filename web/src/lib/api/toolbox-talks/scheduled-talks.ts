import { apiClient } from '@/lib/api/client';
import type { ApiResponse } from '@/types/auth';
import type {
  ScheduledTalk,
  ScheduledTalkListItem,
  GetScheduledTalksParams,
} from '@/types/toolbox-talks';
import type { PaginatedResponse } from './toolbox-talks';

// ============================================
// Scheduled Talks (Assignments) CRUD
// ============================================

export async function getScheduledTalks(
  params?: GetScheduledTalksParams
): Promise<PaginatedResponse<ScheduledTalkListItem>> {
  const queryParams = new URLSearchParams();

  if (params?.toolboxTalkId) {
    queryParams.append('toolboxTalkId', params.toolboxTalkId);
  }
  if (params?.employeeId) {
    queryParams.append('employeeId', params.employeeId);
  }
  if (params?.scheduleId) {
    queryParams.append('scheduleId', params.scheduleId);
  }
  if (params?.status) {
    queryParams.append('status', params.status);
  }
  if (params?.pageNumber) {
    queryParams.append('pageNumber', String(params.pageNumber));
  }
  if (params?.pageSize) {
    queryParams.append('pageSize', String(params.pageSize));
  }

  const queryString = queryParams.toString();
  const url = queryString
    ? `/toolbox-talks/assigned?${queryString}`
    : '/toolbox-talks/assigned';

  const response = await apiClient.get<
    ApiResponse<PaginatedResponse<ScheduledTalkListItem>>
  >(url);

  const data = response.data.data;
  if (!data) {
    return {
      items: [],
      pageNumber: params?.pageNumber || 1,
      pageSize: params?.pageSize || 10,
      totalCount: 0,
      totalPages: 0,
      hasPreviousPage: false,
      hasNextPage: false,
    };
  }

  return data;
}

export async function getScheduledTalk(id: string): Promise<ScheduledTalk> {
  const response = await apiClient.get<ScheduledTalk>(`/toolbox-talks/assigned/${id}`);
  return response.data;
}

export async function sendReminder(id: string): Promise<void> {
  await apiClient.post(`/toolbox-talks/assigned/${id}/reminder`);
}

export async function cancelScheduledTalk(id: string): Promise<void> {
  await apiClient.delete(`/toolbox-talks/assigned/${id}`);
}
