'use client';

import { useCallback } from 'react';
import { useRouter } from 'next/navigation';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { updateLastEditedStep } from '@/lib/api/toolbox-talks/toolbox-talks';
import { isStepReachable, isStepSkipped, WIZARD_STEPS, TOTAL_STEPS } from '../lib/stepOrder';
import { getStepUrl } from '../lib/urlState';
import type { ToolboxTalk } from '@/types/toolbox-talks';

interface UseStepNavigationOptions {
  talkId: string | null;
  currentStep: number;
  talk: ToolboxTalk | null;
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

export function useStepNavigation({ talkId, currentStep, talk }: UseStepNavigationOptions) {
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
    const nextStep = currentStep + 1;
    if (nextStep > TOTAL_STEPS) return;
    // Persist progress before navigating forward
    if (talkId) {
      await updateStep.mutateAsync(nextStep);
    }
    router.push(getStepUrl(talkId, nextStep));
  }, [currentStep, talkId, updateStep, router]);

  const goToStep = useCallback(async (step: number) => {
    if (!isStepReachable(step, talk)) return;
    if (step > currentStep && talkId) {
      await updateStep.mutateAsync(step);
    }
    router.push(getStepUrl(talkId, step));
  }, [talk, currentStep, talkId, updateStep, router]);

  const reachableSteps = WIZARD_STEPS.map((s) => ({
    ...s,
    reachable: isStepReachable(s.number, talk),
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
