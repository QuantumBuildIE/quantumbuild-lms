import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  getScheduledTalks,
  getScheduledTalk,
  sendReminder,
  cancelScheduledTalk,
} from './scheduled-talks';
import { TOOLBOX_TALKS_KEY } from './use-toolbox-talks';
import type { GetScheduledTalksParams } from '@/types/toolbox-talks';

// ============================================
// Query Keys
// ============================================

export const SCHEDULED_TALKS_KEY = [...TOOLBOX_TALKS_KEY, 'scheduled-talks'];

// ============================================
// Scheduled Talks Query Hooks
// ============================================

export function useScheduledTalks(params?: GetScheduledTalksParams) {
  return useQuery({
    queryKey: [...SCHEDULED_TALKS_KEY, 'list', params],
    queryFn: () => getScheduledTalks(params),
  });
}

export function useScheduledTalk(id: string) {
  return useQuery({
    queryKey: [...SCHEDULED_TALKS_KEY, id],
    queryFn: () => getScheduledTalk(id),
    enabled: !!id,
  });
}

// ============================================
// Scheduled Talks Mutation Hooks
// ============================================

export function useSendReminder() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) => sendReminder(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: SCHEDULED_TALKS_KEY });
    },
  });
}

export function useCancelScheduledTalk() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) => cancelScheduledTalk(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: SCHEDULED_TALKS_KEY });
    },
  });
}
