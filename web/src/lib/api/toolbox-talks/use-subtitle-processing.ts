'use client';

import { useState, useEffect, useCallback, useRef } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import * as signalR from '@microsoft/signalr';
import { getStoredToken } from '@/lib/api/client';
import {
  startSubtitleProcessing,
  getSubtitleProcessingStatus,
  getAvailableLanguages,
  cancelSubtitleProcessing,
  retrySubtitleProcessing,
} from './subtitle-processing';
import type {
  SubtitleProcessingStatusResponse,
  SubtitleProgressUpdate,
  StartSubtitleProcessingRequest,
  AvailableLanguagesResponse,
} from '@/types/toolbox-talks';

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5222';

// ============================================
// Query Keys
// ============================================

export const subtitleProcessingKeys = {
  all: ['subtitle-processing'] as const,
  status: (toolboxTalkId: string) =>
    [...subtitleProcessingKeys.all, 'status', toolboxTalkId] as const,
  languages: () => [...subtitleProcessingKeys.all, 'languages'] as const,
};

// ============================================
// Hooks
// ============================================

/**
 * Hook for fetching subtitle processing status
 */
export function useSubtitleProcessingStatus(toolboxTalkId: string) {
  return useQuery({
    queryKey: subtitleProcessingKeys.status(toolboxTalkId),
    queryFn: () => getSubtitleProcessingStatus(toolboxTalkId),
    refetchInterval: (query) => {
      // Poll every 5 seconds if processing is in progress
      const data = query.state.data;
      if (data && !['Completed', 'Failed'].includes(data.status)) {
        return 5000;
      }
      return false;
    },
  });
}

/**
 * Hook for fetching available languages
 */
export function useAvailableLanguages() {
  return useQuery({
    queryKey: subtitleProcessingKeys.languages(),
    queryFn: getAvailableLanguages,
    staleTime: 5 * 60 * 1000, // 5 minutes
  });
}

/**
 * Hook for starting subtitle processing
 */
export function useStartSubtitleProcessing(toolboxTalkId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: StartSubtitleProcessingRequest) =>
      startSubtitleProcessing(toolboxTalkId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: subtitleProcessingKeys.status(toolboxTalkId),
      });
    },
  });
}

/**
 * Hook for canceling subtitle processing
 */
export function useCancelSubtitleProcessing(toolboxTalkId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => cancelSubtitleProcessing(toolboxTalkId),
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: subtitleProcessingKeys.status(toolboxTalkId),
      });
    },
  });
}

/**
 * Hook for retrying failed subtitle translations
 */
export function useRetrySubtitleProcessing(toolboxTalkId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => retrySubtitleProcessing(toolboxTalkId),
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: subtitleProcessingKeys.status(toolboxTalkId),
      });
    },
  });
}

/**
 * Hook for real-time subtitle processing updates via SignalR
 * Combines REST API status with SignalR real-time updates
 */
export function useSubtitleProcessing(toolboxTalkId: string) {
  const queryClient = useQueryClient();
  const connectionRef = useRef<signalR.HubConnection | null>(null);
  const [isConnected, setIsConnected] = useState(false);
  const [connectionError, setConnectionError] = useState<string | null>(null);

  // Use the REST API for initial status and as a fallback
  const {
    data: status,
    isLoading,
    error: queryError,
    refetch,
  } = useSubtitleProcessingStatus(toolboxTalkId);

  // Start processing mutation
  const startMutation = useStartSubtitleProcessing(toolboxTalkId);

  // Cancel processing mutation
  const cancelMutation = useCancelSubtitleProcessing(toolboxTalkId);

  // Retry processing mutation
  const retryMutation = useRetrySubtitleProcessing(toolboxTalkId);

  // Set up SignalR connection
  useEffect(() => {
    // Track if effect is still active (handles React Strict Mode double-mount)
    let isActive = true;

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${API_BASE_URL}/hubs/subtitle-processing`, {
        accessTokenFactory: () => {
          return getStoredToken('accessToken') || '';
        },
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    connectionRef.current = connection;

    // Handle progress updates
    connection.on('ProgressUpdate', (update: SubtitleProgressUpdate) => {
      if (!isActive) return;
      // Update the query cache with the new status
      queryClient.setQueryData<SubtitleProcessingStatusResponse | null>(
        subtitleProcessingKeys.status(toolboxTalkId),
        (prev) => {
          if (!prev) return prev;
          return {
            ...prev,
            status: update.overallStatus,
            overallPercentage: update.overallPercentage,
            currentStep: update.currentStep,
            errorMessage: update.errorMessage,
            languages: update.languages.map((lang) => ({
              ...lang,
              // Preserve existing srtUrl if the update doesn't have one
              srtUrl:
                lang.srtUrl ||
                prev.languages.find((l) => l.languageCode === lang.languageCode)
                  ?.srtUrl,
            })),
          };
        }
      );
    });

    // Start connection
    const startConnection = async () => {
      try {
        await connection.start();
        // Check if still active after async operation
        if (!isActive) {
          connection.stop();
          return;
        }
        setIsConnected(true);
        setConnectionError(null);

        // Subscribe to job updates if we have an active job
        if (status?.jobId) {
          await connection.invoke('SubscribeToJob', status.jobId);
        }
      } catch (err) {
        // Ignore errors if component unmounted during connection
        if (!isActive) return;
        // Ignore abort errors from Strict Mode cleanup
        if (err instanceof Error && err.message.includes('stopped during negotiation')) {
          return;
        }
        console.error('SignalR connection failed:', err);
        setConnectionError(
          err instanceof Error ? err.message : 'Connection failed'
        );
        setIsConnected(false);
      }
    };

    connection.onreconnected(async () => {
      if (!isActive) return;
      setIsConnected(true);
      setConnectionError(null);
      // Re-subscribe after reconnection
      if (status?.jobId) {
        try {
          await connection.invoke('SubscribeToJob', status.jobId);
        } catch (err) {
          console.error('Failed to re-subscribe after reconnection:', err);
        }
      }
    });

    connection.onreconnecting(() => {
      if (!isActive) return;
      setIsConnected(false);
    });

    connection.onclose(() => {
      if (!isActive) return;
      setIsConnected(false);
    });

    startConnection();

    return () => {
      isActive = false;
      connection.stop();
    };
  }, [toolboxTalkId, queryClient]);

  // Subscribe to job when status changes
  useEffect(() => {
    const connection = connectionRef.current;
    if (connection && isConnected && status?.jobId) {
      connection.invoke('SubscribeToJob', status.jobId).catch((err) => {
        console.error('Failed to subscribe to job:', err);
      });
    }
  }, [status?.jobId, isConnected]);

  // Start processing callback
  const startProcessing = useCallback(
    async (request: StartSubtitleProcessingRequest) => {
      const response = await startMutation.mutateAsync(request);

      // Subscribe to the new job
      const connection = connectionRef.current;
      if (connection && isConnected) {
        try {
          await connection.invoke('SubscribeToJob', response.jobId);
        } catch (err) {
          console.error('Failed to subscribe to new job:', err);
        }
      }

      return response;
    },
    [startMutation, isConnected]
  );

  // Cancel processing callback
  const cancelProcessing = useCallback(async () => {
    await cancelMutation.mutateAsync();
  }, [cancelMutation]);

  // Retry processing callback
  const retryProcessing = useCallback(async () => {
    const response = await retryMutation.mutateAsync();

    // Subscribe to the retried job
    const connection = connectionRef.current;
    if (connection && isConnected) {
      try {
        await connection.invoke('SubscribeToJob', response.jobId);
      } catch (err) {
        console.error('Failed to subscribe to retried job:', err);
      }
    }

    return response;
  }, [retryMutation, isConnected]);

  // Check if job has failed translations that can be retried
  const hasFailedTranslations =
    status?.languages.some((l) => l.status === 'Failed') ?? false;

  return {
    status,
    isLoading,
    error: queryError
      ? queryError instanceof Error
        ? queryError.message
        : 'Unknown error'
      : connectionError,
    isConnected,
    startProcessing,
    isStarting: startMutation.isPending,
    cancelProcessing,
    isCancelling: cancelMutation.isPending,
    cancelError: cancelMutation.error,
    retryProcessing,
    isRetrying: retryMutation.isPending,
    retryError: retryMutation.error,
    hasFailedTranslations,
    refreshStatus: refetch,
  };
}
