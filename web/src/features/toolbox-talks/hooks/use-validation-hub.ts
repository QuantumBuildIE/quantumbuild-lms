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
  process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5222';

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

  useEffect(() => {
    if (!runId) return;

    let isActive = true;

    const connection = new HubConnectionBuilder()
      .withUrl(`${API_BASE_URL}/api/hubs/translation-validation`, {
        accessTokenFactory: () => getStoredToken('accessToken') || '',
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(LogLevel.Warning)
      .build();

    connectionRef.current = connection;

    // Handle validation progress updates
    connection.on(
      'ValidationProgress',
      (payload: ValidationProgressEvent) => {
        if (!isActive) return;
        setProgress(payload);

        // Check if overall validation is complete
        if (payload.percentComplete >= 100) {
          setIsComplete(true);
        }
      }
    );

    // Handle individual section completion
    connection.on(
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

    connection.onreconnecting(() => {
      if (!isActive) return;
      setIsConnected(false);
    });

    connection.onreconnected(async () => {
      if (!isActive) return;
      setIsConnected(true);
      try {
        await connection.invoke('SubscribeToValidationRun', runId);
      } catch (err) {
        console.error('Failed to re-subscribe after reconnection:', err);
      }
    });

    connection.onclose(() => {
      if (!isActive) return;
      setIsConnected(false);
    });

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
        console.error('Failed to connect to TranslationValidation hub:', err);
        setError('Failed to establish real-time connection.');
        setIsConnected(false);
      }
    };

    startConnection();

    return () => {
      isActive = false;
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
