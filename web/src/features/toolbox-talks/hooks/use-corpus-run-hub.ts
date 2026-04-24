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

const RECONNECT_DELAYS = [0, 1000, 2000, 5000, 10000, 15000, 20000, 30000, 30000, 30000];

export interface CorpusRunProgressEvent {
  corpusRunId: string;
  entryIndex: number;
  totalEntries: number;
  percentComplete: number;
  currentEntryRef: string;
  message: string;
}

export interface CorpusRunCompleteEvent {
  corpusRunId: string;
  verdict: string;
  regressionCount: number;
  totalEntries: number;
  meanScore: number;
  estimatedCostEur: number;
  actualCostEur: number;
  message: string;
}

export interface CorpusRunFailedEvent {
  corpusRunId: string;
  errorMessage: string;
}

interface UseCorpusRunHubReturn {
  isConnected: boolean;
  progress: CorpusRunProgressEvent | null;
  isComplete: boolean;
  verdict: string | null;
  completeEvent: CorpusRunCompleteEvent | null;
  error: string | null;
  reset: () => void;
}

/**
 * SignalR hook for real-time corpus run progress.
 * Connects to /api/hubs/corpus-run and subscribes to a corpus run group.
 */
export function useCorpusRunHub(runId: string | null): UseCorpusRunHubReturn {
  const [isConnected, setIsConnected] = useState(false);
  const [progress, setProgress] = useState<CorpusRunProgressEvent | null>(null);
  const [isComplete, setIsComplete] = useState(false);
  const [verdict, setVerdict] = useState<string | null>(null);
  const [completeEvent, setCompleteEvent] = useState<CorpusRunCompleteEvent | null>(null);
  const [error, setError] = useState<string | null>(null);

  const connectionRef = useRef<HubConnection | null>(null);
  const isCompleteRef = useRef(false);
  const manualReconnectTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    isCompleteRef.current = isComplete;
  }, [isComplete]);

  useEffect(() => {
    if (!runId) return;

    let isActive = true;

    const connection = new HubConnectionBuilder()
      .withUrl(`${API_BASE_URL}/api/hubs/corpus-run`, {
        accessTokenFactory: () => getStoredToken('accessToken') || '',
      })
      .withAutomaticReconnect(RECONNECT_DELAYS)
      .configureLogging(LogLevel.Warning)
      .build();

    connectionRef.current = connection;

    const manualReconnect = async () => {
      if (!isActive || isCompleteRef.current) return;

      console.warn('[CorpusRunHub] Attempting manual reconnect...');
      try {
        const fresh = new HubConnectionBuilder()
          .withUrl(`${API_BASE_URL}/api/hubs/corpus-run`, {
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
        await fresh.invoke('SubscribeToCorpusRun', runId);
        console.info('[CorpusRunHub] Manual reconnect succeeded, re-subscribed to', runId);
      } catch (err) {
        if (!isActive) return;
        console.error('[CorpusRunHub] Manual reconnect failed:', err);
        manualReconnectTimerRef.current = setTimeout(manualReconnect, 10000);
      }
    };

    const registerHandlers = (conn: HubConnection) => {
      conn.on('CorpusRunProgress', (payload: CorpusRunProgressEvent) => {
        if (!isActive) return;
        setIsComplete(false);
        isCompleteRef.current = false;
        setProgress(payload);
      });

      conn.on('CorpusRunComplete', (payload: CorpusRunCompleteEvent) => {
        if (!isActive) return;
        setIsComplete(true);
        isCompleteRef.current = true;
        setVerdict(payload.verdict);
        setCompleteEvent(payload);
        setProgress((prev) =>
          prev
            ? { ...prev, percentComplete: 100, message: payload.message }
            : null
        );
      });

      conn.on('CorpusRunFailed', (payload: CorpusRunFailedEvent) => {
        if (!isActive) return;
        setIsComplete(true);
        isCompleteRef.current = true;
        setError(payload.errorMessage);
      });

      conn.onreconnecting(() => {
        if (!isActive) return;
        setIsConnected(false);
      });

      conn.onreconnected(async () => {
        if (!isActive) return;
        setIsConnected(true);
        try {
          await conn.invoke('SubscribeToCorpusRun', runId);
        } catch (err) {
          console.error('[CorpusRunHub] Failed to re-subscribe after reconnection:', err);
        }
      });

      conn.onclose(() => {
        if (!isActive) return;
        setIsConnected(false);
        if (!isCompleteRef.current) {
          console.warn('[CorpusRunHub] Connection closed while run is still active — scheduling manual reconnect');
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
        await connection.invoke('SubscribeToCorpusRun', runId);
      } catch (err) {
        if (!isActive) return;
        if (err instanceof Error && err.message.includes('stopped during negotiation')) return;
        console.error('[CorpusRunHub] Failed to connect:', err);
        setError('Failed to establish real-time connection.');
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
          .invoke('UnsubscribeFromCorpusRun', runId)
          .catch(() => {});
        connectionRef.current.stop();
        connectionRef.current = null;
      }
    };
  }, [runId]);

  const reset = useCallback(() => {
    setProgress(null);
    setIsComplete(false);
    isCompleteRef.current = false;
    setVerdict(null);
    setCompleteEvent(null);
    setError(null);
  }, []);

  return {
    isConnected,
    progress,
    isComplete,
    verdict,
    completeEvent,
    error,
    reset,
  };
}
