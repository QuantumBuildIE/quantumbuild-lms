'use client';

import { useQuery } from '@tanstack/react-query';
import { useEffect, useState, useCallback, useMemo } from 'react';
import { getSubtitleProcessingStatus, getVttFile } from './subtitle-processing';
import { getMySubtitleStatus, getMyVttFile } from './my-toolbox-talks';
import type { SubtitleProcessingStatusResponse, LanguageStatus } from '@/types/toolbox-talks';
import { useLookupValues } from '@/hooks/use-lookups';

// ============================================
// Types
// ============================================

export interface AvailableSubtitle {
  languageCode: string;
  languageName: string;
  /** Blob URL for the VTT content (authenticated fetch) */
  vttUrl: string;
}

export interface VideoSubtitlesResult {
  /** Available subtitles for this video (with blob URLs ready for video tracks) */
  subtitles: AvailableSubtitle[];
  /** The preferred subtitle based on language code, if available */
  preferredSubtitle: AvailableSubtitle | null;
  /** Whether subtitles are loading */
  isLoading: boolean;
  /** Error message if any */
  error: string | null;
  /** Whether any subtitles are available */
  hasSubtitles: boolean;
  /** Get VTT content for a specific language (for fallback/caching) */
  getVttContent: (languageCode: string) => Promise<string>;
}

// ============================================
// Query Keys
// ============================================

export const videoSubtitlesKeys = {
  all: ['video-subtitles'] as const,
  available: (toolboxTalkId: string) =>
    [...videoSubtitlesKeys.all, 'available', toolboxTalkId] as const,
  vttContent: (toolboxTalkId: string, languageCode: string) =>
    [...videoSubtitlesKeys.all, 'vtt', toolboxTalkId, languageCode] as const,
};

// ============================================
// Hooks
// ============================================

/**
 * Hook to get available subtitles for a toolbox talk video
 * Returns subtitles that are completed and ready to use
 * Fetches VTT content with authentication and creates blob URLs for video tracks
 *
 * @param toolboxTalkId The toolbox talk ID (for admin context) OR scheduled talk ID (for employee context)
 * @param preferredLanguageCode Employee's preferred language code
 * @param useEmployeeEndpoint If true, uses the employee-specific endpoints (for scheduled talk IDs)
 */
export function useVideoSubtitles(
  toolboxTalkId: string,
  preferredLanguageCode?: string,
  useEmployeeEndpoint = false
): VideoSubtitlesResult {
  const [subtitlesWithBlobs, setSubtitlesWithBlobs] = useState<AvailableSubtitle[]>([]);
  const [blobsLoading, setBlobsLoading] = useState(false);

  // Load language names from lookup
  const { data: lookupLanguages } = useLookupValues('Language');
  const languageNameMap = useMemo(() => {
    const map: Record<string, string> = {};
    if (lookupLanguages) {
      for (const lang of lookupLanguages) {
        map[lang.code.toLowerCase()] = lang.name;
      }
    }
    return map;
  }, [lookupLanguages]);

  const getLanguageName = useCallback((code: string): string => {
    return languageNameMap[code.toLowerCase()] || code.toUpperCase();
  }, [languageNameMap]);

  // Select the appropriate API functions based on context
  const getStatus = useEmployeeEndpoint ? getMySubtitleStatus : getSubtitleProcessingStatus;
  const getVtt = useEmployeeEndpoint ? getMyVttFile : getVttFile;

  // Fetch subtitle processing status to get available languages
  const {
    data: status,
    isLoading: statusLoading,
    error: queryError,
  } = useQuery({
    queryKey: useEmployeeEndpoint
      ? ['my-video-subtitles', 'available', toolboxTalkId]
      : videoSubtitlesKeys.available(toolboxTalkId),
    queryFn: () => getStatus(toolboxTalkId),
    enabled: !!toolboxTalkId,
    staleTime: 30000, // 30 seconds
  });

  // Extract completed subtitle info (without blob URLs yet)
  const completedSubtitleInfo = useMemo(() => {
    if (!status || !status.languages) {
      return [];
    }
    return status.languages
      .filter((lang: LanguageStatus) => lang.status === 'Completed')
      .map((lang: LanguageStatus) => ({
        languageCode: lang.languageCode,
        languageName: lang.language || getLanguageName(lang.languageCode),
      }));
  }, [status, getLanguageName]);

  // Fetch VTT content and create blob URLs
  useEffect(() => {
    if (!toolboxTalkId || completedSubtitleInfo.length === 0) {
      setSubtitlesWithBlobs([]);
      return;
    }

    let cancelled = false;

    const fetchVttContent = async () => {
      setBlobsLoading(true);
      const results: AvailableSubtitle[] = [];

      for (const info of completedSubtitleInfo) {
        if (cancelled) break;
        try {
          const vttContent = await getVtt(toolboxTalkId, info.languageCode);
          const blob = new Blob([vttContent], { type: 'text/vtt' });
          const blobUrl = URL.createObjectURL(blob);
          results.push({
            languageCode: info.languageCode,
            languageName: info.languageName,
            vttUrl: blobUrl,
          });
        } catch (err) {
          console.error(`Failed to fetch VTT for ${info.languageCode}:`, err);
          // Skip this language if fetch fails
        }
      }

      if (!cancelled) {
        setSubtitlesWithBlobs(results);
        setBlobsLoading(false);
      }
    };

    fetchVttContent();

    // Cleanup blob URLs on unmount
    return () => {
      cancelled = true;
      subtitlesWithBlobs.forEach((subtitle) => {
        if (subtitle.vttUrl.startsWith('blob:')) {
          URL.revokeObjectURL(subtitle.vttUrl);
        }
      });
    };
  }, [toolboxTalkId, JSON.stringify(completedSubtitleInfo), getVtt]);

  // Find preferred subtitle
  const preferredSubtitle = preferredLanguageCode
    ? subtitlesWithBlobs.find(
        (s) => s.languageCode.toLowerCase() === preferredLanguageCode.toLowerCase()
      ) || null
    : null;

  // Function to get VTT content
  const getVttContent = useCallback(async (languageCode: string): Promise<string> => {
    return getVtt(toolboxTalkId, languageCode);
  }, [toolboxTalkId, getVtt]);

  return {
    subtitles: subtitlesWithBlobs,
    preferredSubtitle,
    isLoading: statusLoading || blobsLoading,
    error: queryError ? (queryError instanceof Error ? queryError.message : 'Failed to load subtitles') : null,
    hasSubtitles: subtitlesWithBlobs.length > 0,
    getVttContent,
  };
}

/**
 * Hook to fetch VTT content for a specific language
 * Useful for pre-loading or caching subtitle content
 */
export function useVttContent(
  toolboxTalkId: string,
  languageCode: string,
  enabled = true
) {
  return useQuery({
    queryKey: videoSubtitlesKeys.vttContent(toolboxTalkId, languageCode),
    queryFn: () => getVttFile(toolboxTalkId, languageCode),
    enabled: enabled && !!toolboxTalkId && !!languageCode,
    staleTime: 5 * 60 * 1000, // 5 minutes
  });
}
