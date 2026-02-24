'use client';

import { useState, useEffect, useRef, useCallback } from 'react';
import { HubConnectionBuilder, HubConnection, LogLevel } from '@microsoft/signalr';
import { getStoredToken } from '@/lib/api/client';
import type { LessonParseProgress, LessonParseResult } from '@/types/lesson-parser';

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5222';

export function useLessonParserHub() {
  const [connectionId, setConnectionId] = useState<string | null>(null);
  const [progress, setProgress] = useState<LessonParseProgress | null>(null);
  const [result, setResult] = useState<LessonParseResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isConnected, setIsConnected] = useState(false);

  const connectionRef = useRef<HubConnection | null>(null);

  // Set up SignalR connection on mount
  useEffect(() => {
    let isActive = true;

    const connection = new HubConnectionBuilder()
      .withUrl(`${API_BASE_URL}/hubs/lesson-parser`, {
        accessTokenFactory: () => getStoredToken('accessToken') || '',
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(LogLevel.Warning)
      .build();

    connectionRef.current = connection;

    // Handle progress updates
    connection.on('ReceiveProgress', (payload: LessonParseProgress) => {
      if (!isActive) return;
      setProgress(payload);
    });

    // Handle completion
    connection.on('ReceiveCompleted', (payload: LessonParseResult) => {
      if (!isActive) return;
      setResult(payload);
      setProgress(null);
    });

    // Handle failure
    connection.on('ReceiveFailed', (errorMessage: string) => {
      if (!isActive) return;
      setError(errorMessage);
      setProgress(null);
    });

    connection.onreconnecting(() => {
      if (!isActive) return;
      setIsConnected(false);
    });

    connection.onreconnected(() => {
      if (!isActive) return;
      setIsConnected(true);
      setConnectionId(connection.connectionId ?? null);
    });

    connection.onclose(() => {
      if (!isActive) return;
      setIsConnected(false);
      setConnectionId(null);
    });

    const startConnection = async () => {
      try {
        await connection.start();
        if (!isActive) {
          connection.stop();
          return;
        }
        setIsConnected(true);
        setConnectionId(connection.connectionId ?? null);
      } catch (err) {
        if (!isActive) return;
        if (err instanceof Error && err.message.includes('stopped during negotiation')) {
          return;
        }
        console.error('Failed to connect to LessonParser hub:', err);
        setIsConnected(false);
      }
    };

    startConnection();

    return () => {
      isActive = false;
      connection.stop();
    };
  }, []);

  // Reset state for a new submission
  const reset = useCallback(() => {
    setProgress(null);
    setResult(null);
    setError(null);
  }, []);

  return {
    connectionId,
    progress,
    result,
    error,
    isConnected,
    reset,
  };
}
