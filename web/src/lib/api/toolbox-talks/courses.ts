import { apiClient } from '@/lib/api/client';
import type { ApiResponse } from '@/types/auth';

// ============================================
// Course DTOs
// ============================================

export interface ToolboxTalkCourseDto {
  id: string;
  title: string;
  description?: string;
  isActive: boolean;
  requireSequentialCompletion: boolean;
  requiresRefresher: boolean;
  refresherIntervalMonths: number;
  generateCertificate: boolean;
  autoAssignToNewEmployees: boolean;
  autoAssignDueDays: number;
  talkCount: number;
  items: ToolboxTalkCourseItemDto[];
  translations: ToolboxTalkCourseTranslationDto[];
  createdAt: string;
  updatedAt?: string;
}

export interface ToolboxTalkCourseItemDto {
  id: string;
  toolboxTalkId: string;
  orderIndex: number;
  isRequired: boolean;
  talkCode: string;
  talkTitle: string;
  talkDescription?: string;
  talkHasVideo: boolean;
  talkSectionCount: number;
  talkQuestionCount: number;
}

export interface ToolboxTalkCourseTranslationDto {
  id: string;
  languageCode: string;
  translatedTitle: string;
  translatedDescription?: string;
}

export interface ToolboxTalkCourseListDto {
  id: string;
  title: string;
  description?: string;
  isActive: boolean;
  requireSequentialCompletion: boolean;
  autoAssignToNewEmployees: boolean;
  talkCount: number;
  translationCount: number;
  createdAt: string;
}

export interface CreateToolboxTalkCourseDto {
  title: string;
  description?: string;
  isActive?: boolean;
  requireSequentialCompletion?: boolean;
  requiresRefresher?: boolean;
  refresherIntervalMonths?: number;
  generateCertificate?: boolean;
  autoAssignToNewEmployees?: boolean;
  autoAssignDueDays?: number;
  items?: CreateToolboxTalkCourseItemDto[];
}

export interface UpdateToolboxTalkCourseDto {
  title: string;
  description?: string;
  isActive: boolean;
  requireSequentialCompletion: boolean;
  requiresRefresher: boolean;
  refresherIntervalMonths: number;
  generateCertificate: boolean;
  autoAssignToNewEmployees: boolean;
  autoAssignDueDays: number;
}

export interface CreateToolboxTalkCourseItemDto {
  toolboxTalkId: string;
  orderIndex: number;
  isRequired?: boolean;
}

export interface UpdateCourseItemsDto {
  items: CourseItemOrderDto[];
}

export interface CourseItemOrderDto {
  toolboxTalkId: string;
  orderIndex: number;
  isRequired: boolean;
}

// ============================================
// Query Params
// ============================================

export interface GetToolboxTalkCoursesParams {
  searchTerm?: string;
  isActive?: boolean;
}

// ============================================
// API Functions
// ============================================

export async function getToolboxTalkCourses(
  params?: GetToolboxTalkCoursesParams
): Promise<ToolboxTalkCourseListDto[]> {
  const queryParams = new URLSearchParams();

  if (params?.searchTerm) {
    queryParams.append('searchTerm', params.searchTerm);
  }
  if (params?.isActive !== undefined) {
    queryParams.append('isActive', String(params.isActive));
  }

  const queryString = queryParams.toString();
  const url = queryString ? `/toolbox-talks/courses?${queryString}` : '/toolbox-talks/courses';

  const response = await apiClient.get<ApiResponse<ToolboxTalkCourseListDto[]>>(url);
  return response.data.data ?? [];
}

export async function getToolboxTalkCourse(id: string): Promise<ToolboxTalkCourseDto> {
  const response = await apiClient.get<ToolboxTalkCourseDto>(`/toolbox-talks/courses/${id}`);
  return response.data;
}

export async function createToolboxTalkCourse(
  data: CreateToolboxTalkCourseDto
): Promise<ToolboxTalkCourseDto> {
  const response = await apiClient.post<ToolboxTalkCourseDto>('/toolbox-talks/courses', data);
  return response.data;
}

export async function updateToolboxTalkCourse(
  id: string,
  data: UpdateToolboxTalkCourseDto
): Promise<ToolboxTalkCourseDto> {
  const response = await apiClient.put<ToolboxTalkCourseDto>(`/toolbox-talks/courses/${id}`, data);
  return response.data;
}

export async function deleteToolboxTalkCourse(id: string): Promise<void> {
  await apiClient.delete(`/toolbox-talks/courses/${id}`);
}

export async function addCourseItem(
  courseId: string,
  data: CreateToolboxTalkCourseItemDto
): Promise<ToolboxTalkCourseDto> {
  const response = await apiClient.post<ToolboxTalkCourseDto>(
    `/toolbox-talks/courses/${courseId}/items`,
    data
  );
  return response.data;
}

export async function removeCourseItem(
  courseId: string,
  talkId: string
): Promise<ToolboxTalkCourseDto> {
  const response = await apiClient.delete<ToolboxTalkCourseDto>(
    `/toolbox-talks/courses/${courseId}/items/${talkId}`
  );
  return response.data;
}

export async function updateCourseItems(
  courseId: string,
  data: UpdateCourseItemsDto
): Promise<ToolboxTalkCourseDto> {
  const response = await apiClient.put<ToolboxTalkCourseDto>(
    `/toolbox-talks/courses/${courseId}/items`,
    data
  );
  return response.data;
}
