import { apiClient } from '@/lib/api/client';
import type { ApiResponse } from '@/types/auth';
import type {
  MyToolboxTalk,
  MyToolboxTalkListItem,
  MyTrainingSummary,
  ScheduledTalkSectionProgress,
  ScheduledTalkCompletion,
  QuizResult,
  VideoProgress,
  SlideDto,
  GetMyToolboxTalksParams,
  MarkSectionReadRequest,
  SubmitQuizRequest,
  UpdateVideoProgressRequest,
  CompleteToolboxTalkRequest,
  StartTalkRequest,
} from '@/types/toolbox-talks';
import type { PaginatedResponse } from './toolbox-talks';

// ============================================
// My Toolbox Talks (Employee Portal)
// ============================================

export async function getMyToolboxTalks(
  params?: GetMyToolboxTalksParams
): Promise<PaginatedResponse<MyToolboxTalkListItem>> {
  const queryParams = new URLSearchParams();

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
  const url = queryString ? `/my/toolbox-talks?${queryString}` : '/my/toolbox-talks';

  const response = await apiClient.get<ApiResponse<PaginatedResponse<MyToolboxTalkListItem>>>(
    url
  );

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

export async function getMyToolboxTalkById(id: string): Promise<MyToolboxTalk> {
  const response = await apiClient.get<MyToolboxTalk>(`/my/toolbox-talks/${id}`);
  return response.data;
}

// Status-filtered convenience functions
export async function getMyPendingToolboxTalks(
  params?: Omit<GetMyToolboxTalksParams, 'status'>
): Promise<PaginatedResponse<MyToolboxTalkListItem>> {
  const queryParams = new URLSearchParams();

  if (params?.pageNumber) {
    queryParams.append('pageNumber', String(params.pageNumber));
  }
  if (params?.pageSize) {
    queryParams.append('pageSize', String(params.pageSize));
  }

  const queryString = queryParams.toString();
  const url = queryString
    ? `/my/toolbox-talks/pending?${queryString}`
    : '/my/toolbox-talks/pending';

  const response = await apiClient.get<ApiResponse<PaginatedResponse<MyToolboxTalkListItem>>>(
    url
  );

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

export async function getMyInProgressToolboxTalks(
  params?: Omit<GetMyToolboxTalksParams, 'status'>
): Promise<PaginatedResponse<MyToolboxTalkListItem>> {
  const queryParams = new URLSearchParams();

  if (params?.pageNumber) {
    queryParams.append('pageNumber', String(params.pageNumber));
  }
  if (params?.pageSize) {
    queryParams.append('pageSize', String(params.pageSize));
  }

  const queryString = queryParams.toString();
  const url = queryString
    ? `/my/toolbox-talks/in-progress?${queryString}`
    : '/my/toolbox-talks/in-progress';

  const response = await apiClient.get<ApiResponse<PaginatedResponse<MyToolboxTalkListItem>>>(
    url
  );

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

export async function getMyOverdueToolboxTalks(
  params?: Omit<GetMyToolboxTalksParams, 'status'>
): Promise<PaginatedResponse<MyToolboxTalkListItem>> {
  const queryParams = new URLSearchParams();

  if (params?.pageNumber) {
    queryParams.append('pageNumber', String(params.pageNumber));
  }
  if (params?.pageSize) {
    queryParams.append('pageSize', String(params.pageSize));
  }

  const queryString = queryParams.toString();
  const url = queryString
    ? `/my/toolbox-talks/overdue?${queryString}`
    : '/my/toolbox-talks/overdue';

  const response = await apiClient.get<ApiResponse<PaginatedResponse<MyToolboxTalkListItem>>>(
    url
  );

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

export async function getMyCompletedToolboxTalks(
  params?: Omit<GetMyToolboxTalksParams, 'status'>
): Promise<PaginatedResponse<MyToolboxTalkListItem>> {
  const queryParams = new URLSearchParams();

  if (params?.pageNumber) {
    queryParams.append('pageNumber', String(params.pageNumber));
  }
  if (params?.pageSize) {
    queryParams.append('pageSize', String(params.pageSize));
  }

  const queryString = queryParams.toString();
  const url = queryString
    ? `/my/toolbox-talks/completed?${queryString}`
    : '/my/toolbox-talks/completed';

  const response = await apiClient.get<ApiResponse<PaginatedResponse<MyToolboxTalkListItem>>>(
    url
  );

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

// ============================================
// Summary (for banner/badge count)
// ============================================

export async function getMyTrainingSummary(): Promise<MyTrainingSummary> {
  const response = await apiClient.get<MyTrainingSummary>('/my/toolbox-talks/summary');
  return response.data;
}

// ============================================
// Start Talk (with optional geolocation)
// ============================================

export async function startToolboxTalk(
  scheduledTalkId: string,
  data?: StartTalkRequest
): Promise<void> {
  await apiClient.post(
    `/my/toolbox-talks/${scheduledTalkId}/start`,
    data || {}
  );
}

// ============================================
// Progress Tracking
// ============================================

export async function markSectionRead(
  scheduledTalkId: string,
  sectionId: string,
  data?: MarkSectionReadRequest
): Promise<ScheduledTalkSectionProgress> {
  const response = await apiClient.post<ScheduledTalkSectionProgress>(
    `/my/toolbox-talks/${scheduledTalkId}/sections/${sectionId}/read`,
    data || {}
  );
  return response.data;
}

export async function updateVideoProgress(
  scheduledTalkId: string,
  data: UpdateVideoProgressRequest
): Promise<VideoProgress> {
  const response = await apiClient.post<VideoProgress>(
    `/my/toolbox-talks/${scheduledTalkId}/video-progress`,
    data
  );
  return response.data;
}

export async function resetVideoProgress(
  scheduledTalkId: string
): Promise<VideoProgress> {
  const response = await apiClient.post<VideoProgress>(
    `/my/toolbox-talks/${scheduledTalkId}/reset-video-progress`
  );
  return response.data;
}

// ============================================
// Quiz
// ============================================

export async function submitQuizAnswers(
  scheduledTalkId: string,
  data: SubmitQuizRequest
): Promise<QuizResult> {
  const response = await apiClient.post<QuizResult>(
    `/my/toolbox-talks/${scheduledTalkId}/quiz/submit`,
    data
  );
  return response.data;
}

// ============================================
// Completion
// ============================================

export async function completeToolboxTalk(
  scheduledTalkId: string,
  data: CompleteToolboxTalkRequest
): Promise<ScheduledTalkCompletion> {
  const response = await apiClient.post<ScheduledTalkCompletion>(
    `/my/toolbox-talks/${scheduledTalkId}/complete`,
    data
  );
  return response.data;
}

// ============================================
// Slides
// ============================================

/**
 * Get slides for an assigned toolbox talk with optional translated text
 * @param scheduledTalkId The scheduled talk ID (not the toolbox talk ID)
 * @param lang Optional ISO 639-1 language code for translated slide text
 */
export async function getToolboxTalkSlides(
  scheduledTalkId: string,
  lang?: string
): Promise<SlideDto[]> {
  const params = lang ? { lang } : undefined;
  const response = await apiClient.get<SlideDto[]>(
    `/my/toolbox-talks/${scheduledTalkId}/slides`,
    { params }
  );
  return response.data;
}

// ============================================
// Slideshow HTML
// ============================================

import type { SlideshowHtmlResponse, SubtitleProcessingStatusResponse } from '@/types/toolbox-talks';

/**
 * Get slideshow HTML for an assigned toolbox talk with optional translation
 * @param scheduledTalkId The scheduled talk ID (not the toolbox talk ID)
 * @param lang Optional ISO 639-1 language code for translated slideshow
 */
export async function getSlideshowHtml(
  scheduledTalkId: string,
  lang?: string
): Promise<SlideshowHtmlResponse> {
  const params = lang ? { lang } : undefined;
  const response = await apiClient.get<SlideshowHtmlResponse>(
    `/my/toolbox-talks/${scheduledTalkId}/slideshow`,
    { params }
  );
  return response.data;
}

// ============================================
// Subtitles (Employee-specific endpoints)
// ============================================

/**
 * Get subtitle processing status for an assigned toolbox talk
 * Uses the employee-specific endpoint that verifies assignment
 * @param scheduledTalkId The scheduled talk ID (not the toolbox talk ID)
 */
export async function getMySubtitleStatus(
  scheduledTalkId: string
): Promise<SubtitleProcessingStatusResponse | null> {
  try {
    const response = await apiClient.get<SubtitleProcessingStatusResponse>(
      `/my/toolbox-talks/${scheduledTalkId}/subtitles/status`
    );
    return response.data;
  } catch (error) {
    // Return null if no subtitles available (404)
    if ((error as { response?: { status?: number } })?.response?.status === 404) {
      return null;
    }
    throw error;
  }
}

/**
 * Get WebVTT subtitle file for an assigned toolbox talk
 * Uses the employee-specific endpoint that verifies assignment
 * @param scheduledTalkId The scheduled talk ID (not the toolbox talk ID)
 * @param languageCode ISO 639-1 language code
 */
export async function getMyVttFile(
  scheduledTalkId: string,
  languageCode: string
): Promise<string> {
  const response = await apiClient.get<string>(
    `/my/toolbox-talks/${scheduledTalkId}/subtitles/${languageCode}`,
    {
      params: { format: 'vtt' },
      headers: {
        Accept: 'text/vtt, text/plain, */*',
      },
      responseType: 'text',
    }
  );
  return response.data;
}

/**
 * Get SRT subtitle file for an assigned toolbox talk
 * Uses the employee-specific endpoint that verifies assignment
 * @param scheduledTalkId The scheduled talk ID (not the toolbox talk ID)
 * @param languageCode ISO 639-1 language code
 */
export async function getMySrtFile(
  scheduledTalkId: string,
  languageCode: string
): Promise<string> {
  const response = await apiClient.get<string>(
    `/my/toolbox-talks/${scheduledTalkId}/subtitles/${languageCode}`,
    {
      params: { format: 'srt' },
      headers: {
        Accept: 'application/x-subrip, text/plain, */*',
      },
      responseType: 'text',
    }
  );
  return response.data;
}

/**
 * Download SRT file for an assigned toolbox talk
 * Triggers file download via blob
 */
export async function downloadMySrtFile(
  scheduledTalkId: string,
  languageCode: string
): Promise<void> {
  const content = await getMySrtFile(scheduledTalkId, languageCode);

  // Create a blob and download it
  const blob = new Blob([content], { type: 'application/x-subrip' });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = `subtitles_${languageCode}.srt`;
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  URL.revokeObjectURL(url);
}
