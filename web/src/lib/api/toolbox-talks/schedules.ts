import { apiClient } from '@/lib/api/client';
import type { ApiResponse } from '@/types/auth';
import type {
  ToolboxTalkSchedule,
  ToolboxTalkScheduleListItem,
  CreateToolboxTalkScheduleRequest,
  UpdateToolboxTalkScheduleRequest,
  ProcessScheduleResult,
  GetToolboxTalkSchedulesParams,
} from '@/types/toolbox-talks';
import type { PaginatedResponse } from './toolbox-talks';

// ============================================
// Schedules CRUD
// ============================================

export async function getToolboxTalkSchedules(
  params?: GetToolboxTalkSchedulesParams
): Promise<PaginatedResponse<ToolboxTalkScheduleListItem>> {
  const queryParams = new URLSearchParams();

  if (params?.toolboxTalkId) {
    queryParams.append('toolboxTalkId', params.toolboxTalkId);
  }
  if (params?.status) {
    queryParams.append('status', params.status);
  }
  if (params?.dateFrom) {
    queryParams.append('dateFrom', params.dateFrom);
  }
  if (params?.dateTo) {
    queryParams.append('dateTo', params.dateTo);
  }
  if (params?.pageNumber) {
    queryParams.append('pageNumber', String(params.pageNumber));
  }
  if (params?.pageSize) {
    queryParams.append('pageSize', String(params.pageSize));
  }

  const queryString = queryParams.toString();
  const url = queryString
    ? `/toolbox-talks/schedules?${queryString}`
    : '/toolbox-talks/schedules';

  const response = await apiClient.get<
    ApiResponse<PaginatedResponse<ToolboxTalkScheduleListItem>>
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

export async function getToolboxTalkSchedule(id: string): Promise<ToolboxTalkSchedule> {
  const response = await apiClient.get<ToolboxTalkSchedule>(`/toolbox-talks/schedules/${id}`);
  return response.data;
}

export async function createToolboxTalkSchedule(
  data: CreateToolboxTalkScheduleRequest
): Promise<ToolboxTalkSchedule> {
  const response = await apiClient.post<ToolboxTalkSchedule>('/toolbox-talks/schedules', data);
  return response.data;
}

export async function updateToolboxTalkSchedule(
  id: string,
  data: UpdateToolboxTalkScheduleRequest
): Promise<ToolboxTalkSchedule> {
  const response = await apiClient.put<ToolboxTalkSchedule>(`/toolbox-talks/schedules/${id}`, {
    ...data,
    id,
  });
  return response.data;
}

export async function cancelToolboxTalkSchedule(id: string): Promise<void> {
  await apiClient.delete(`/toolbox-talks/schedules/${id}`);
}

export async function processToolboxTalkSchedule(id: string): Promise<ProcessScheduleResult> {
  const response = await apiClient.post<ProcessScheduleResult>(
    `/toolbox-talks/schedules/${id}/process`
  );
  return response.data;
}
