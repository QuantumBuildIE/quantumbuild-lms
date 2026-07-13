'use client';

import { useState, useCallback, useEffect, useRef } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { TOOLBOX_TALKS_KEY } from '@/lib/api/toolbox-talks/use-toolbox-talks';
import { getWorkflowStates } from '@/lib/api/toolbox-talks/toolbox-talks';
import type { TranslationWorkflowStateDto, TranslationWorkflowState } from '@/types/workflows';

const ACTIVE_STATES = new Set<string>(['Translating', 'Validating']);
const POLL_INTERVAL_MS = 5000;

/**
 * Fetches workflow states for a talk. Consumers still render one <WorkflowSubscriber />
 * per activeRunId (see WorkflowSubscriber.tsx) for real-time SignalR-driven invalidation,
 * but this hook also polls the REST endpoint directly (same "poll the honest state" fallback
 * used for the Validate step, commits d0996c4/cc55b71) whenever a language is in an active
 * state, or was just started via markStarted and hasn't yet been acknowledged by the backend.
 * That second condition closes the race where WorkflowSubscriber never mounts because
 * workflow-state still read the pre-start value the one time it was fetched.
 */
export function useWorkflowSubscription(talkId: string) {
  const queryClient = useQueryClient();

  const [progress, setProgress] = useState<
    Map<string, { completed: number; total: number }>
  >(new Map());

  // Languages just started by the user, keyed by their pre-start state. Cleared once
  // workflow-state confirms the language moved past that baseline. Kept in a ref (not
  // state) so the refetchInterval callback below always reads the latest value; the
  // paired version counter forces the re-render react-query needs to re-evaluate it.
  const pendingStartRef = useRef<Map<string, TranslationWorkflowState | undefined>>(new Map());
  const [, forcePendingReeval] = useState(0);

  const query = useQuery<TranslationWorkflowStateDto[]>({
    queryKey: [...TOOLBOX_TALKS_KEY, talkId, 'workflow-state'],
    queryFn: () => getWorkflowStates(talkId),
    enabled: !!talkId,
    refetchInterval: (q) => {
      const data = q.state.data ?? [];
      const hasActive = data.some((s) => ACTIVE_STATES.has(s.state));
      return hasActive || pendingStartRef.current.size > 0 ? POLL_INTERVAL_MS : false;
    },
  });

  // Reconcile pending-start tracking once fresh data confirms the backend acknowledged
  // the start (state moved away from its pre-click baseline).
  useEffect(() => {
    if (!query.data || pendingStartRef.current.size === 0) return;
    const stateByCode = new Map(query.data.map((s) => [s.languageCode, s.state]));
    let changed = false;
    for (const [code, baseline] of pendingStartRef.current) {
      if (stateByCode.get(code) !== baseline) {
        pendingStartRef.current.delete(code);
        changed = true;
      }
    }
    if (changed) forcePendingReeval((v) => v + 1);
  }, [query.data]);

  const markStarted = useCallback(
    (languageCode: string, previousState: TranslationWorkflowState | undefined) => {
      pendingStartRef.current.set(languageCode, previousState);
      forcePendingReeval((v) => v + 1);
    },
    []
  );

  const invalidate = useCallback(() => {
    queryClient.invalidateQueries({
      queryKey: [...TOOLBOX_TALKS_KEY, talkId, 'workflow-state'],
    });
  }, [queryClient, talkId]);

  const onValidationComplete = useCallback(
    (runId: string) => {
      setProgress((prev) => {
        const next = new Map(prev);
        next.delete(runId);
        return next;
      });
      invalidate();
    },
    [invalidate]
  );

  const onSectionCompleted = useCallback(
    (runId: string, completed: number, total: number) => {
      setProgress((prev) => {
        const next = new Map(prev);
        next.set(runId, { completed, total });
        return next;
      });
    },
    []
  );

  // Run IDs with active background jobs that need a real-time SignalR subscription
  const activeRunIds = (query.data ?? [])
    .filter((s) => ACTIVE_STATES.has(s.state) && s.lastValidationRunId)
    .map((s) => s.lastValidationRunId as string);

  return {
    ...query,
    invalidate,
    activeRunIds,
    progress,
    onValidationComplete,
    onSectionCompleted,
    markStarted,
  };
}
