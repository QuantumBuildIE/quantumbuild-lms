'use client';

import { useState, useCallback } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { TOOLBOX_TALKS_KEY } from '@/lib/api/toolbox-talks/use-toolbox-talks';
import { getWorkflowStates } from '@/lib/api/toolbox-talks/toolbox-talks';
import type { TranslationWorkflowStateDto } from '@/types/workflows';

const ACTIVE_STATES = new Set<string>(['Translating', 'Validating']);

/**
 * Fetches workflow states for a talk. Does NOT poll — consumers must render one
 * <WorkflowSubscriber /> per activeRunId (see WorkflowSubscriber.tsx) to receive
 * SignalR-driven invalidations when jobs complete.
 */
export function useWorkflowSubscription(talkId: string) {
  const queryClient = useQueryClient();

  const [progress, setProgress] = useState<
    Map<string, { completed: number; total: number }>
  >(new Map());

  const query = useQuery<TranslationWorkflowStateDto[]>({
    queryKey: [...TOOLBOX_TALKS_KEY, talkId, 'workflow-state'],
    queryFn: () => getWorkflowStates(talkId),
    enabled: !!talkId,
  });

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
  };
}
