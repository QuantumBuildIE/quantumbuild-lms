'use client';

import { useState, useEffect, useRef, useCallback } from 'react';
import {
  HubConnectionBuilder,
  HubConnection,
  LogLevel,
} from '@microsoft/signalr';
import { getStoredToken } from '@/lib/api/client';
import type {
  ValidationProgressEvent,
  SectionCompletedEvent,
  SectionValidationResult,
} from '@/types/content-creation';

const API_BASE_URL =
  (process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5222/api').replace(/\/api\/?$/, '');

/** Delays for automatic reconnect — extends to ~2 min total for long-running jobs */
const RECONNECT_DELAYS = [0, 1000, 2000, 5000, 10000, 15000, 20000, 30000, 30000, 30000];

interface UseValidationHubReturn {
  isConnected: boolean;
  progress: ValidationProgressEvent | null;
  completedSections: Map<number, SectionValidationResult>;
  isComplete: boolean;
  error: string | null;
  reset: () => void;
}

/**
 * SignalR hook for real-time translation validation progress.
 * Connects to /api/hubs/translation-validation and subscribes to a validation run.
 * Includes manual reconnect fallback when automatic reconnect exhausts its retries.
 */
export function useValidationHub(
  runId: string | null
): UseValidationHubReturn {
  const [isConnected, setIsConnected] = useState(false);
  const [progress, setProgress] = useState<ValidationProgressEvent | null>(
    null
  );
  const [completedSections, setCompletedSections] = useState<
    Map<number, SectionValidationResult>
  >(new Map());
  const [isComplete, setIsComplete] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const connectionRef = useRef<HubConnection | null>(null);
  const isCompleteRef = useRef(false);
  const manualReconnectTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Keep ref in sync so the onclose handler can read latest value
  useEffect(() => {
    isCompleteRef.current = isComplete;
  }, [isComplete]);

  useEffect(() => {
    if (!runId) return;

    let isActive = true;

    const connection = new HubConnectionBuilder()
      .withUrl(`${API_BASE_URL}/api/hubs/translation-validation`, {
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

      console.warn('[ValidationHub] Attempting manual reconnect...');
      try {
        // Build a fresh connection
        const fresh = new HubConnectionBuilder()
          .withUrl(`${API_BASE_URL}/api/hubs/translation-validation`, {
            accessTokenFactory: () => getStoredToken('accessToken') || '',
          })
          .withAutomaticReconnect(RECONNECT_DELAYS)
          .configureLogging(LogLevel.Warning)
          .build();

        // Re-register all handlers on the new connection
        registerHandlers(fresh);

        await fresh.start();
        if (!isActive) {
          fresh.stop();
          return;
        }

        connectionRef.current = fresh;
        setIsConnected(true);
        setError(null);
        await fresh.invoke('SubscribeToValidationRun', runId);
        console.info('[ValidationHub] Manual reconnect succeeded, re-subscribed to', runId);
      } catch (err) {
        if (!isActive) return;
        console.error('[ValidationHub] Manual reconnect failed:', err);
        // Retry again after 10s
        manualReconnectTimerRef.current = setTimeout(manualReconnect, 10000);
      }
    };

    /**
     * Registers SignalR event handlers on a connection instance.
     * Extracted so both the initial and manually-reconnected connections share the same logic.
     */
    const registerHandlers = (conn: HubConnection) => {
      // Handle validation progress updates
      conn.on(
        'ValidationProgress',
        (payload: ValidationProgressEvent) => {
          if (!isActive) return;
          // New progress arriving means validation is (re-)running — clear stale isComplete
          setIsComplete(false);
          isCompleteRef.current = false;
          setProgress(payload);

          if (payload.percentComplete >= 100) {
            setIsComplete(true);
            isCompleteRef.current = true;
          }
        }
      );

      // Handle individual section completion
      conn.on(
        'SectionCompleted',
        (payload: SectionCompletedEvent) => {
          if (!isActive) return;
          setCompletedSections((prev) => {
            const next = new Map(prev);
            next.set(payload.sectionIndex, payload.result);
            return next;
          });
        }
      );

      // Handle validation run completion — force progress to 100%
      conn.on(
        'ValidationComplete',
        (payload: { validationRunId: string; success: boolean; message: string }) => {
          if (!isActive) return;
          setIsComplete(true);
          isCompleteRef.current = true;
          // Force progress to 100% so the progress bar reaches the end
          setProgress((prev) => prev
            ? { ...prev, percentComplete: 100, stage: 'Complete', message: payload.message }
            : null
          );
          if (!payload.success) {
            setError(payload.message);
          }
        }
      );

      conn.onreconnecting(() => {
        if (!isActive) return;
        setIsConnected(false);
      });

      conn.onreconnected(async () => {
        if (!isActive) return;
        setIsConnected(true);
        try {
          await conn.invoke('SubscribeToValidationRun', runId);
        } catch (err) {
          console.error('[ValidationHub] Failed to re-subscribe after reconnection:', err);
        }
      });

      conn.onclose(() => {
        if (!isActive) return;
        setIsConnected(false);
        // If the run isn't complete, attempt manual reconnect as a fallback
        if (!isCompleteRef.current) {
          console.warn('[ValidationHub] Connection closed while run is still active — scheduling manual reconnect');
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

        await connection.invoke('SubscribeToValidationRun', runId);
      } catch (err) {
        if (!isActive) return;
        if (
          err instanceof Error &&
          err.message.includes('stopped during negotiation')
        ) {
          return;
        }
        console.error('[ValidationHub] Failed to connect:', err);
        setError('Failed to establish real-time connection.');
        setIsConnected(false);
        // Try manual reconnect
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
          .invoke('UnsubscribeFromValidationRun', runId)
          .catch(() => {});
        connectionRef.current.stop();
        connectionRef.current = null;
      }
    };
  }, [runId]);

  const reset = useCallback(() => {
    setProgress(null);
    setCompletedSections(new Map());
    setIsComplete(false);
    isCompleteRef.current = false;
    setError(null);
  }, []);

  return {
    isConnected,
    progress,
    completedSections,
    isComplete,
    error,
    reset,
  };
}
