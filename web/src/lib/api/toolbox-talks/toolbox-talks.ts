import { apiClient } from '@/lib/api/client';
import type { ApiResponse } from '@/types/auth';
import type {
  ToolboxTalk,
  ToolboxTalkListItem,
  ToolboxTalkDashboard,
  ToolboxTalkSettings,
  ToolboxTalkPreview,
  SlideDto,
  CreateToolboxTalkRequest,
  UpdateToolboxTalkRequest,
  UpdateToolboxTalkSettingsRequest,
  GetToolboxTalksParams,
  CheckDuplicateRequest,
  DuplicateCheckResponse,
  ReuseContentRequest,
  ContentReuseResponse,
  UpdateFileHashRequest,
  SlideshowHtmlResponse,
  SmartGenerateContentRequest,
  SmartGenerateContentResult,
} from '@/types/toolbox-talks';

export interface PaginatedResponse<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

// ============================================
// Toolbox Talks CRUD
// ============================================

export async function getToolboxTalks(
  params?: GetToolboxTalksParams
): Promise<PaginatedResponse<ToolboxTalkListItem>> {
  const queryParams = new URLSearchParams();

  if (params?.searchTerm) {
    queryParams.append('searchTerm', params.searchTerm);
  }
  if (params?.frequency) {
    queryParams.append('frequency', params.frequency);
  }
  if (params?.isActive !== undefined) {
    queryParams.append('isActive', String(params.isActive));
  }
  if (params?.pageNumber) {
    queryParams.append('pageNumber', String(params.pageNumber));
  }
  if (params?.pageSize) {
    queryParams.append('pageSize', String(params.pageSize));
  }

  const queryString = queryParams.toString();
  const url = queryString ? `/toolbox-talks?${queryString}` : '/toolbox-talks';

  const response = await apiClient.get<ApiResponse<PaginatedResponse<ToolboxTalkListItem>>>(url);

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

export async function getToolboxTalk(id: string): Promise<ToolboxTalk> {
  const response = await apiClient.get<ToolboxTalk>(`/toolbox-talks/${id}`);
  return response.data;
}

export async function createToolboxTalk(data: CreateToolboxTalkRequest): Promise<ToolboxTalk> {
  console.log('🌐 API: createToolboxTalk called with:', data);
  const response = await apiClient.post<ToolboxTalk>('/toolbox-talks', data);
  console.log('🌐 API: createToolboxTalk response:', response.data);
  return response.data;
}

export async function updateToolboxTalk(
  id: string,
  data: UpdateToolboxTalkRequest
): Promise<ToolboxTalk> {
  const response = await apiClient.put<ToolboxTalk>(`/toolbox-talks/${id}`, {
    ...data,
    id,
  });
  return response.data;
}

export async function deleteToolboxTalk(id: string): Promise<void> {
  await apiClient.delete(`/toolbox-talks/${id}`);
}

// ============================================
// Dashboard
// ============================================

export async function getToolboxTalkDashboard(): Promise<ToolboxTalkDashboard> {
  const response = await apiClient.get<ApiResponse<ToolboxTalkDashboard>>(
    '/toolbox-talks/dashboard'
  );
  return (
    response.data.data ?? {
      totalTalks: 0,
      activeTalks: 0,
      inactiveTalks: 0,
      totalAssignments: 0,
      pendingCount: 0,
      inProgressCount: 0,
      completedCount: 0,
      overdueCount: 0,
      completionRate: 0,
      overdueRate: 0,
      averageCompletionTimeHours: 0,
      averageQuizScore: 0,
      quizPassRate: 0,
      talksByStatus: {},
      talksByFrequency: {},
      recentCompletions: [],
      overdueAssignments: [],
      upcomingSchedules: [],
    }
  );
}

// ============================================
// Settings
// ============================================

export async function getToolboxTalkSettings(): Promise<ToolboxTalkSettings> {
  const response = await apiClient.get<ApiResponse<ToolboxTalkSettings>>(
    '/toolbox-talks/settings'
  );
  return response.data.data;
}

export async function updateToolboxTalkSettings(
  data: UpdateToolboxTalkSettingsRequest
): Promise<ToolboxTalkSettings> {
  const response = await apiClient.put<ApiResponse<ToolboxTalkSettings>>(
    '/toolbox-talks/settings',
    data
  );
  return response.data.data;
}

// ============================================
// File Upload & Content Generation
// ============================================

export interface VideoUploadResponse {
  videoUrl: string;
  fileName: string;
  fileSize: number;
  source: string;
}

export interface PdfUploadResponse {
  pdfUrl: string;
  fileName: string;
  fileSize: number;
}

export interface SetVideoUrlResponse {
  videoUrl: string;
  source: string;
}

export interface GenerateContentRequest {
  includeVideo: boolean;
  includePdf: boolean;
  minimumSections?: number;
  minimumQuestions?: number;
  passThreshold?: number;
  replaceExisting?: boolean;
  connectionId: string;
  sourceLanguageCode?: string;
  generateSlidesFromPdf?: boolean;
  slideshowSource?: string;
}

export interface GenerateContentResponse {
  jobId: string;
  message: string;
  toolboxTalkId: string;
}

export async function uploadToolboxTalkVideo(
  id: string,
  file: File,
  onProgress?: (progress: number) => void
): Promise<VideoUploadResponse> {
  console.log('🌐 API: uploadToolboxTalkVideo called with id:', id);
  console.log('🌐 API: uploadToolboxTalkVideo file:', file.name, file.size);

  const formData = new FormData();
  formData.append('file', file);

  const response = await apiClient.post<VideoUploadResponse>(
    `/toolbox-talks/${id}/video`,
    formData,
    {
      headers: { 'Content-Type': 'multipart/form-data' },
      onUploadProgress: (progressEvent) => {
        if (onProgress && progressEvent.total) {
          const percentCompleted = Math.round(
            (progressEvent.loaded * 100) / progressEvent.total
          );
          onProgress(percentCompleted);
        }
      },
    }
  );

  console.log('🌐 API: uploadToolboxTalkVideo response:', response.data);
  return response.data;
}

export async function setToolboxTalkVideoUrl(
  id: string,
  url: string
): Promise<SetVideoUrlResponse> {
  const response = await apiClient.put<SetVideoUrlResponse>(
    `/toolbox-talks/${id}/video-url`,
    { url }
  );
  return response.data;
}

export async function uploadToolboxTalkPdf(
  id: string,
  file: File,
  onProgress?: (progress: number) => void
): Promise<PdfUploadResponse> {
  console.log('🌐 API: uploadToolboxTalkPdf called with id:', id);
  console.log('🌐 API: uploadToolboxTalkPdf file:', file.name, file.size);

  const formData = new FormData();
  formData.append('file', file);

  const response = await apiClient.post<PdfUploadResponse>(
    `/toolbox-talks/${id}/pdf`,
    formData,
    {
      headers: { 'Content-Type': 'multipart/form-data' },
      onUploadProgress: (progressEvent) => {
        if (onProgress && progressEvent.total) {
          const percentCompleted = Math.round(
            (progressEvent.loaded * 100) / progressEvent.total
          );
          onProgress(percentCompleted);
        }
      },
    }
  );

  console.log('🌐 API: uploadToolboxTalkPdf response:', response.data);
  return response.data;
}

export async function deleteToolboxTalkVideo(id: string): Promise<void> {
  await apiClient.delete(`/toolbox-talks/${id}/video`);
}

export async function deleteToolboxTalkPdf(id: string): Promise<void> {
  await apiClient.delete(`/toolbox-talks/${id}/pdf`);
}

export async function generateToolboxTalkContent(
  id: string,
  options: GenerateContentRequest
): Promise<GenerateContentResponse> {
  const response = await apiClient.post<GenerateContentResponse>(
    `/toolbox-talks/${id}/generate`,
    options
  );
  return response.data;
}

// ============================================
// Slideshow Generation
// ============================================

export async function generateSlides(
  id: string,
  source: string = 'pdf'
): Promise<{ slidesGenerated: number }> {
  const response = await apiClient.post<{ slidesGenerated: number }>(
    `/toolbox-talks/${id}/generate-slides?source=${encodeURIComponent(source)}`
  );
  return response.data;
}

// ============================================
// Content Translations
// ============================================

export interface GenerateTranslationsRequest {
  languages: string[];
}

export interface LanguageTranslationResult {
  language: string;
  languageCode: string;
  success: boolean;
  errorMessage?: string;
  sectionsTranslated: number;
  questionsTranslated: number;
}

export interface GenerateTranslationsResponse {
  success: boolean;
  errorMessage?: string;
  languageResults: LanguageTranslationResult[];
}

export interface ContentTranslationInfo {
  languageCode: string;
  language: string;
  translatedTitle: string;
  translatedAt: string;
  translationProvider: string;
}

export async function generateContentTranslations(
  id: string,
  request: GenerateTranslationsRequest
): Promise<GenerateTranslationsResponse> {
  const response = await apiClient.post<GenerateTranslationsResponse>(
    `/toolbox-talks/${id}/translations/generate`,
    request
  );
  return response.data;
}

export async function getContentTranslations(
  id: string
): Promise<ContentTranslationInfo[]> {
  const response = await apiClient.get<ContentTranslationInfo[]>(
    `/toolbox-talks/${id}/translations`
  );
  return response.data;
}

// ============================================
// Content Deduplication
// ============================================

/**
 * Check if a file has been previously processed in another toolbox talk.
 * Returns information about the source toolbox talk if a duplicate is found.
 */
export async function checkForDuplicate(
  id: string,
  request: CheckDuplicateRequest
): Promise<DuplicateCheckResponse> {
  const response = await apiClient.post<DuplicateCheckResponse>(
    `/toolbox-talks/${id}/check-duplicate`,
    request
  );
  return response.data;
}

/**
 * Reuse content (sections, questions, translations) from a source toolbox talk.
 * Call this instead of generate when user chooses to reuse existing content.
 */
export async function reuseContent(
  id: string,
  request: ReuseContentRequest
): Promise<ContentReuseResponse> {
  const response = await apiClient.post<ContentReuseResponse>(
    `/toolbox-talks/${id}/reuse-content`,
    request
  );
  return response.data;
}

/**
 * Update the file hash for a toolbox talk after file upload.
 * Should be called after uploading a PDF or setting a video URL.
 */
export async function updateFileHash(
  id: string,
  request: UpdateFileHashRequest
): Promise<{ success: boolean; fileHash: string }> {
  const response = await apiClient.post<{ success: boolean; fileHash: string }>(
    `/toolbox-talks/${id}/update-file-hash`,
    request
  );
  return response.data;
}

// ============================================
// Smart Content Generation
// ============================================

/**
 * Smart content generation: automatically reuses existing content from duplicate
 * sources and generates only what's missing via AI.
 * Returns immediately with reuse results. If AI generation is needed, a background
 * job is queued and the generationJobId is returned for SignalR progress tracking.
 */
export async function smartGenerateContent(
  id: string,
  request: SmartGenerateContentRequest
): Promise<SmartGenerateContentResult> {
  const response = await apiClient.post<SmartGenerateContentResult>(
    `/toolbox-talks/${id}/smart-generate`,
    request
  );
  return response.data;
}

// ============================================
// Admin Preview (view as employee)
// ============================================

export async function getToolboxTalkPreview(
  id: string,
  lang?: string
): Promise<ToolboxTalkPreview> {
  const params = lang ? { lang } : undefined;
  const response = await apiClient.get<ToolboxTalkPreview>(
    `/toolbox-talks/${id}/preview`,
    { params }
  );
  return response.data;
}

export async function getToolboxTalkPreviewSlides(
  id: string,
  lang?: string
): Promise<SlideDto[]> {
  const params = lang ? { lang } : undefined;
  const response = await apiClient.get<SlideDto[]>(
    `/toolbox-talks/${id}/preview/slides`,
    { params }
  );
  return response.data;
}

export async function getAdminSlideshowHtml(
  id: string,
  lang?: string
): Promise<SlideshowHtmlResponse> {
  const params = lang ? { lang } : undefined;
  const response = await apiClient.get<SlideshowHtmlResponse>(
    `/toolbox-talks/${id}/slideshow-html`,
    { params }
  );
  return response.data;
}
