'use client';

import { useState, useEffect, useRef, useCallback } from 'react';
import {
  HubConnectionBuilder,
  HubConnection,
  LogLevel,
} from '@microsoft/signalr';
import { getStoredToken } from '@/lib/api/client';

const API_BASE_URL =
  (process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5222/api').replace(/\/api\/?$/, '');

/** Delays for automatic reconnect — extends to ~2 min total */
const RECONNECT_DELAYS = [0, 1000, 2000, 5000, 10000, 15000, 20000, 30000, 30000, 30000];

// ============================================
// Types matching backend SubtitleProgressUpdate
// ============================================

export type SubtitleProcessingStatus =
  | 'Pending'
  | 'Transcribing'
  | 'Translating'
  | 'Uploading'
  | 'Completed'
  | 'Failed'
  | 'Cancelled';

export type SubtitleTranslationStatus =
  | 'Pending'
  | 'InProgress'
  | 'Completed'
  | 'Failed';

export interface LanguageProgress {
  language: string;
  languageCode: string;
  status: SubtitleTranslationStatus;
  percentage: number;
  srtUrl: string | null;
}

export interface SubtitleProgressUpdate {
  overallStatus: SubtitleProcessingStatus;
  overallPercentage: number;
  currentStep: string;
  errorMessage: string | null;
  languages: LanguageProgress[];
}

interface UseSubtitleHubReturn {
  isConnected: boolean;
  overallStatus: SubtitleProcessingStatus | null;
  percentComplete: number;
  currentStep: string;
  languageProgress: LanguageProgress[];
  error: string | null;
  isComplete: boolean;
}

/**
 * SignalR hook for real-time subtitle processing progress.
 * Connects to /api/hubs/subtitle-processing and subscribes to a job.
 * Includes manual reconnect fallback when automatic reconnect exhausts its retries.
 */
export function useSubtitleHub(
  jobId: string | null
): UseSubtitleHubReturn {
  const [isConnected, setIsConnected] = useState(false);
  const [overallStatus, setOverallStatus] = useState<SubtitleProcessingStatus | null>(null);
  const [percentComplete, setPercentComplete] = useState(0);
  const [currentStep, setCurrentStep] = useState('');
  const [languageProgress, setLanguageProgress] = useState<LanguageProgress[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [isComplete, setIsComplete] = useState(false);

  const connectionRef = useRef<HubConnection | null>(null);
  const isCompleteRef = useRef(false);
  const manualReconnectTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Keep ref in sync so the onclose handler can read latest value
  useEffect(() => {
    isCompleteRef.current = isComplete;
  }, [isComplete]);

  useEffect(() => {
    if (!jobId) return;

    let isActive = true;

    const connection = new HubConnectionBuilder()
      .withUrl(`${API_BASE_URL}/api/hubs/subtitle-processing`, {
        accessTokenFactory: () => getStoredToken('accessToken') || '',
      })
      .withAutomaticReconnect(RECONNECT_DELAYS)
      .configureLogging(LogLevel.Warning)
      .build();

    connectionRef.current = connection;

    /**
     * Attempts a manual reconnect after automatic reconnect has been exhausted.
     * Builds a fresh connection, re-registers handlers, and re-subscribes to the group.
     */
    const manualReconnect = async () => {
      if (!isActive || isCompleteRef.current) return;

      console.warn('[SubtitleHub] Attempting manual reconnect...');
      try {
        const fresh = new HubConnectionBuilder()
          .withUrl(`${API_BASE_URL}/api/hubs/subtitle-processing`, {
            accessTokenFactory: () => getStoredToken('accessToken') || '',
          })
          .withAutomaticReconnect(RECONNECT_DELAYS)
          .configureLogging(LogLevel.Warning)
          .build();

        registerHandlers(fresh);

        await fresh.start();
        if (!isActive) {
          fresh.stop();
          return;
        }

        connectionRef.current = fresh;
        setIsConnected(true);
        setError(null);
        await fresh.invoke('SubscribeToJob', jobId);
        console.info('[SubtitleHub] Manual reconnect succeeded, re-subscribed to', jobId);
      } catch (err) {
        if (!isActive) return;
        console.error('[SubtitleHub] Manual reconnect failed:', err);
        manualReconnectTimerRef.current = setTimeout(manualReconnect, 10000);
      }
    };

    /**
     * Registers SignalR event handlers on a connection instance.
     * Extracted so both the initial and manually-reconnected connections share the same logic.
     */
    const registerHandlers = (conn: HubConnection) => {
      // Backend sends "ProgressUpdate" via SendAsync
      conn.on('ProgressUpdate', (payload: SubtitleProgressUpdate) => {
        if (!isActive) return;

        setOverallStatus(payload.overallStatus);
        setPercentComplete(payload.overallPercentage);
        setCurrentStep(payload.currentStep);
        setLanguageProgress(payload.languages ?? []);

        if (payload.errorMessage) {
          setError(payload.errorMessage);
        }

        const terminal: SubtitleProcessingStatus[] = ['Completed', 'Failed', 'Cancelled'];
        if (terminal.includes(payload.overallStatus)) {
          setIsComplete(true);
          isCompleteRef.current = true;
        }
      });

      conn.onreconnecting(() => {
        if (!isActive) return;
        setIsConnected(false);
      });

      conn.onreconnected(async () => {
        if (!isActive) return;
        setIsConnected(true);
        try {
          await conn.invoke('SubscribeToJob', jobId);
        } catch (err) {
          console.error('[SubtitleHub] Failed to re-subscribe after reconnection:', err);
        }
      });

      conn.onclose(() => {
        if (!isActive) return;
        setIsConnected(false);
        // If the job isn't complete, attempt manual reconnect as a fallback
        if (!isCompleteRef.current) {
          console.warn('[SubtitleHub] Connection closed while job is still active — scheduling manual reconnect');
          manualReconnectTimerRef.current = setTimeout(manualReconnect, 2000);
        }
      });
    };

    registerHandlers(connection);

    const startConnection = async () => {
      try {
        await connection.start();
        if (!isActive) {
          connection.stop();
          return;
        }
        setIsConnected(true);
        setError(null);

        await connection.invoke('SubscribeToJob', jobId);
      } catch (err) {
        if (!isActive) return;
        if (
          err instanceof Error &&
          err.message.includes('stopped during negotiation')
        ) {
          return;
        }
        console.error('[SubtitleHub] Failed to connect:', err);
        setError('Failed to establish real-time connection for subtitle processing.');
        setIsConnected(false);
        manualReconnectTimerRef.current = setTimeout(manualReconnect, 5000);
      }
    };

    startConnection();

    return () => {
      isActive = false;
      if (manualReconnectTimerRef.current) {
        clearTimeout(manualReconnectTimerRef.current);
        manualReconnectTimerRef.current = null;
      }
      if (connectionRef.current) {
        connectionRef.current
          .invoke('UnsubscribeFromJob', jobId)
          .catch(() => {});
        connectionRef.current.stop();
        connectionRef.current = null;
      }
    };
  }, [jobId]);

  return {
    isConnected,
    overallStatus,
    percentComplete,
    currentStep,
    languageProgress,
    error,
    isComplete,
  };
}
