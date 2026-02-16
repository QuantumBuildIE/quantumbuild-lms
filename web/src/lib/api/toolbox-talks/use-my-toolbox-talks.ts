import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  getMyToolboxTalks,
  getMyToolboxTalkById,
  getMyPendingToolboxTalks,
  getMyInProgressToolboxTalks,
  getMyOverdueToolboxTalks,
  getMyCompletedToolboxTalks,
  getMyTrainingSummary,
  getToolboxTalkSlides,
  getSlideshowHtml,
  startToolboxTalk,
  markSectionRead,
  updateVideoProgress,
  resetVideoProgress,
  submitQuizAnswers,
  completeToolboxTalk,
} from './my-toolbox-talks';
import type {
  GetMyToolboxTalksParams,
  StartTalkRequest,
  MarkSectionReadRequest,
  UpdateVideoProgressRequest,
  SubmitQuizRequest,
  CompleteToolboxTalkRequest,
} from '@/types/toolbox-talks';

// ============================================
// Query Keys
// ============================================

export const MY_TOOLBOX_TALKS_KEY = ['my-toolbox-talks'];

// ============================================
// My Toolbox Talks Query Hooks
// ============================================

export function useMyToolboxTalks(params?: GetMyToolboxTalksParams) {
  return useQuery({
    queryKey: [...MY_TOOLBOX_TALKS_KEY, 'list', params],
    queryFn: () => getMyToolboxTalks(params),
  });
}

export function useMyToolboxTalk(id: string) {
  return useQuery({
    queryKey: [...MY_TOOLBOX_TALKS_KEY, id],
    queryFn: () => getMyToolboxTalkById(id),
    enabled: !!id,
  });
}

export function useMyPendingToolboxTalks(params?: Omit<GetMyToolboxTalksParams, 'status'>) {
  return useQuery({
    queryKey: [...MY_TOOLBOX_TALKS_KEY, 'pending', params],
    queryFn: () => getMyPendingToolboxTalks(params),
  });
}

export function useMyInProgressToolboxTalks(params?: Omit<GetMyToolboxTalksParams, 'status'>) {
  return useQuery({
    queryKey: [...MY_TOOLBOX_TALKS_KEY, 'in-progress', params],
    queryFn: () => getMyInProgressToolboxTalks(params),
  });
}

export function useMyOverdueToolboxTalks(params?: Omit<GetMyToolboxTalksParams, 'status'>) {
  return useQuery({
    queryKey: [...MY_TOOLBOX_TALKS_KEY, 'overdue', params],
    queryFn: () => getMyOverdueToolboxTalks(params),
  });
}

export function useMyCompletedToolboxTalks(params?: Omit<GetMyToolboxTalksParams, 'status'>) {
  return useQuery({
    queryKey: [...MY_TOOLBOX_TALKS_KEY, 'completed', params],
    queryFn: () => getMyCompletedToolboxTalks(params),
  });
}

export function useMyTrainingSummary() {
  return useQuery({
    queryKey: [...MY_TOOLBOX_TALKS_KEY, 'summary'],
    queryFn: getMyTrainingSummary,
    staleTime: 5 * 60 * 1000, // 5 minutes
    refetchOnWindowFocus: true,
  });
}

export function useToolboxTalkSlides(scheduledTalkId: string, lang?: string) {
  return useQuery({
    queryKey: [...MY_TOOLBOX_TALKS_KEY, 'slides', scheduledTalkId, lang],
    queryFn: () => getToolboxTalkSlides(scheduledTalkId, lang),
    enabled: !!scheduledTalkId,
  });
}

export function useSlideshowHtml(scheduledTalkId: string, lang?: string, enabled = true) {
  return useQuery({
    queryKey: [...MY_TOOLBOX_TALKS_KEY, 'slideshow-html', scheduledTalkId, lang],
    queryFn: () => getSlideshowHtml(scheduledTalkId, lang),
    enabled: !!scheduledTalkId && enabled,
  });
}

// ============================================
// Start Talk Mutation Hook
// ============================================

export function useStartToolboxTalk() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({
      scheduledTalkId,
      data,
    }: {
      scheduledTalkId: string;
      data?: StartTalkRequest;
    }) => startToolboxTalk(scheduledTalkId, data),
    onSuccess: (_, { scheduledTalkId }) => {
      queryClient.invalidateQueries({ queryKey: [...MY_TOOLBOX_TALKS_KEY, scheduledTalkId] });
    },
  });
}

// ============================================
// Progress Mutation Hooks
// ============================================

export function useMarkSectionRead() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({
      scheduledTalkId,
      sectionId,
      data,
    }: {
      scheduledTalkId: string;
      sectionId: string;
      data?: MarkSectionReadRequest;
    }) => markSectionRead(scheduledTalkId, sectionId, data),
    onSuccess: (_, { scheduledTalkId }) => {
      queryClient.invalidateQueries({ queryKey: [...MY_TOOLBOX_TALKS_KEY, scheduledTalkId] });
      queryClient.invalidateQueries({ queryKey: MY_TOOLBOX_TALKS_KEY });
    },
  });
}

export function useUpdateVideoProgress() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({
      scheduledTalkId,
      data,
    }: {
      scheduledTalkId: string;
      data: UpdateVideoProgressRequest;
    }) => updateVideoProgress(scheduledTalkId, data),
    onSuccess: (_, { scheduledTalkId }) => {
      queryClient.invalidateQueries({ queryKey: [...MY_TOOLBOX_TALKS_KEY, scheduledTalkId] });
    },
  });
}

export function useResetVideoProgress() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ scheduledTalkId }: { scheduledTalkId: string }) =>
      resetVideoProgress(scheduledTalkId),
    onSuccess: (_, { scheduledTalkId }) => {
      queryClient.invalidateQueries({ queryKey: [...MY_TOOLBOX_TALKS_KEY, scheduledTalkId] });
    },
  });
}

// ============================================
// Quiz Mutation Hooks
// ============================================

export function useSubmitQuizAnswers() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({
      scheduledTalkId,
      data,
    }: {
      scheduledTalkId: string;
      data: SubmitQuizRequest;
    }) => submitQuizAnswers(scheduledTalkId, data),
    onSuccess: (_, { scheduledTalkId }) => {
      queryClient.invalidateQueries({ queryKey: [...MY_TOOLBOX_TALKS_KEY, scheduledTalkId] });
      queryClient.invalidateQueries({ queryKey: MY_TOOLBOX_TALKS_KEY });
    },
  });
}

// ============================================
// Completion Mutation Hooks
// ============================================

export function useCompleteToolboxTalk() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({
      scheduledTalkId,
      data,
    }: {
      scheduledTalkId: string;
      data: CompleteToolboxTalkRequest;
    }) => completeToolboxTalk(scheduledTalkId, data),
    onSuccess: (_, { scheduledTalkId }) => {
      queryClient.invalidateQueries({ queryKey: [...MY_TOOLBOX_TALKS_KEY, scheduledTalkId] });
      queryClient.invalidateQueries({ queryKey: MY_TOOLBOX_TALKS_KEY });
    },
  });
}
