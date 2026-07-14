'use client';

import { useEffect } from 'react';
import { useValidationHub } from '@/features/toolbox-talks/hooks/use-validation-hub';

interface WorkflowSubscriberProps {
  runId: string;
  onComplete: (runId: string) => void;
  onSectionCompleted?: (runId: string, completed: number, total: number) => void;
}

/**
 * Render-side SignalR companion for useWorkflowSubscription.
 * Mount one instance per activeRunId; calls onComplete when ValidationComplete fires,
 * triggering workflow-state query invalidation. React hooks cannot be called in a loop,
 * so this component wraps one useValidationHub call per active language.
 */
export function WorkflowSubscriber({
  runId,
  onComplete,
  onSectionCompleted,
}: WorkflowSubscriberProps) {
  const { isComplete, completedSections, progress } = useValidationHub(runId);

  useEffect(() => {
    if (isComplete) onComplete(runId);
  }, [isComplete, runId, onComplete]);

  useEffect(() => {
    if (!onSectionCompleted || !progress) return;
    onSectionCompleted(runId, completedSections.size, progress.totalSections);
  }, [completedSections.size, progress, runId, onSectionCompleted]);

  return null;
}
