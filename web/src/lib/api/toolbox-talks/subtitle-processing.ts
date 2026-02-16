import { apiClient } from '@/lib/api/client';
import type {
  StartSubtitleProcessingRequest,
  StartProcessingResponse,
  SubtitleProcessingStatusResponse,
  AvailableLanguagesResponse,
} from '@/types/toolbox-talks';

// ============================================
// Subtitle Processing API Functions
// ============================================

/**
 * Start subtitle processing for a toolbox talk video
 */
export async function startSubtitleProcessing(
  toolboxTalkId: string,
  request: StartSubtitleProcessingRequest
): Promise<StartProcessingResponse> {
  const response = await apiClient.post<StartProcessingResponse>(
    `/toolbox-talks/${toolboxTalkId}/subtitles/process`,
    request
  );
  return response.data;
}

/**
 * Get the current status of subtitle processing for a toolbox talk
 * Returns null if no processing job exists
 */
export async function getSubtitleProcessingStatus(
  toolboxTalkId: string
): Promise<SubtitleProcessingStatusResponse | null> {
  try {
    const response = await apiClient.get<SubtitleProcessingStatusResponse>(
      `/toolbox-talks/${toolboxTalkId}/subtitles/status`
    );
    return response.data;
  } catch (error) {
    // Return null if no job exists (404)
    if ((error as { response?: { status?: number } })?.response?.status === 404) {
      return null;
    }
    throw error;
  }
}

/**
 * Get available languages for subtitle translation
 * Returns both employee languages (from employee records) and all supported languages
 */
export async function getAvailableLanguages(): Promise<AvailableLanguagesResponse> {
  const response = await apiClient.get<AvailableLanguagesResponse>(
    '/subtitles/available-languages'
  );
  return response.data;
}

/**
 * Cancel an ongoing subtitle processing job
 */
export async function cancelSubtitleProcessing(
  toolboxTalkId: string
): Promise<void> {
  await apiClient.post(`/toolbox-talks/${toolboxTalkId}/subtitles/cancel`);
}

/**
 * Retry failed translations for a subtitle processing job
 */
export async function retrySubtitleProcessing(
  toolboxTalkId: string
): Promise<StartProcessingResponse> {
  const response = await apiClient.post<StartProcessingResponse>(
    `/toolbox-talks/${toolboxTalkId}/subtitles/retry`
  );
  return response.data;
}

/**
 * Get the SRT file content for a specific language
 */
export async function getSrtFile(
  toolboxTalkId: string,
  languageCode: string,
  download = false
): Promise<string> {
  const response = await apiClient.get<string>(
    `/toolbox-talks/${toolboxTalkId}/subtitles/${languageCode}`,
    {
      params: { download, format: 'srt' },
      headers: {
        Accept: 'application/x-subrip, text/plain, */*',
      },
      responseType: 'text',
    }
  );
  return response.data;
}

/**
 * Get the WebVTT file content for a specific language
 * WebVTT format is required for HTML5 video track elements
 */
export async function getVttFile(
  toolboxTalkId: string,
  languageCode: string
): Promise<string> {
  const response = await apiClient.get<string>(
    `/toolbox-talks/${toolboxTalkId}/subtitles/${languageCode}`,
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
 * Build the URL for a WebVTT subtitle file
 * Note: This URL requires authentication - use getVttFile() for authenticated access
 */
export function getVttFileUrl(
  toolboxTalkId: string,
  languageCode: string
): string {
  const baseUrl = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5222';
  return `${baseUrl}/api/toolbox-talks/${toolboxTalkId}/subtitles/${languageCode}?format=vtt`;
}

/**
 * Download SRT file with authentication
 * Triggers file download via blob
 */
export async function downloadSrtFile(
  toolboxTalkId: string,
  languageCode: string
): Promise<void> {
  const content = await getSrtFile(toolboxTalkId, languageCode, false);

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
