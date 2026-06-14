'use client';

import { useCallback } from 'react';
import { useRouter } from 'next/navigation';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { updateLastEditedStep } from '@/lib/api/toolbox-talks/toolbox-talks';
import { isStepReachable, isStepSkipped, findNextReachableStep, WIZARD_STEPS, TOTAL_STEPS } from '../lib/stepOrder';
import { getStepUrl } from '../lib/urlState';
import type { ToolboxTalk } from '@/types/toolbox-talks';
import type { ValidationRunSummary } from '@/types/content-creation';

interface UseStepNavigationOptions {
  talkId: string | null;
  currentStep: number;
  talk: ToolboxTalk | null;
  /** Passed to isStepReachable for step 7 gate — required when target languages are declared */
  validationRuns?: ValidationRunSummary[] | null;
}

export function useUpdateTalkStep(talkId: string | null) {
  const queryClient = useQueryClient();

  return useMutation<void, Error, number>({
    mutationFn: (step: number) => {
      if (!talkId) return Promise.resolve();
      return updateLastEditedStep(talkId, step);
    },
    onSuccess: () => {
      if (talkId) {
        queryClient.invalidateQueries({ queryKey: ['learnings', talkId] });
        queryClient.invalidateQueries({ queryKey: ['learnings', 'drafts'] });
      }
    },
  });
}

export function useStepNavigation({ talkId, currentStep, talk, validationRuns }: UseStepNavigationOptions) {
  const router = useRouter();
  const updateStep = useUpdateTalkStep(talkId);

  const canGoBack = currentStep > 2;
  const canGoNext = currentStep < TOTAL_STEPS;

  const goBack = useCallback(() => {
    const prevStep = currentStep - 1;
    if (prevStep < 2) return;
    router.push(getStepUrl(talkId, prevStep));
  }, [currentStep, talkId, router]);

  const goNext = useCallback(async () => {
    const next = findNextReachableStep(currentStep, talk, validationRuns);
    if (next === null) return;
    if (talkId) {
      await updateStep.mutateAsync(next);
    }
    router.push(getStepUrl(talkId, next));
  }, [currentStep, talk, validationRuns, talkId, updateStep, router]);

  const goToStep = useCallback(async (step: number) => {
    if (!isStepReachable(step, talk, validationRuns)) return;
    if (step > currentStep && talkId) {
      await updateStep.mutateAsync(step);
    }
    router.push(getStepUrl(talkId, step));
  }, [talk, validationRuns, currentStep, talkId, updateStep, router]);

  const reachableSteps = WIZARD_STEPS.map((s) => ({
    ...s,
    reachable: isStepReachable(s.number, talk, validationRuns),
    skipped: isStepSkipped(s.number, talk),
  }));

  return {
    canGoBack,
    canGoNext,
    goBack,
    goNext,
    goToStep,
    reachableSteps,
    isNavigating: updateStep.isPending,
  };
}
