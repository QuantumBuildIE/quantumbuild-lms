import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  getToolboxTalkSchedules,
  getToolboxTalkSchedule,
  createToolboxTalkSchedule,
  updateToolboxTalkSchedule,
  cancelToolboxTalkSchedule,
  processToolboxTalkSchedule,
} from './schedules';
import { TOOLBOX_TALKS_KEY } from './use-toolbox-talks';
import type {
  CreateToolboxTalkScheduleRequest,
  UpdateToolboxTalkScheduleRequest,
  GetToolboxTalkSchedulesParams,
} from '@/types/toolbox-talks';

// ============================================
// Query Keys
// ============================================

export const TOOLBOX_TALK_SCHEDULES_KEY = [...TOOLBOX_TALKS_KEY, 'schedules'];

// ============================================
// Schedules Query Hooks
// ============================================

export function useToolboxTalkSchedules(params?: GetToolboxTalkSchedulesParams) {
  return useQuery({
    queryKey: [...TOOLBOX_TALK_SCHEDULES_KEY, 'list', params],
    queryFn: () => getToolboxTalkSchedules(params),
  });
}

export function useToolboxTalkSchedule(id: string) {
  return useQuery({
    queryKey: [...TOOLBOX_TALK_SCHEDULES_KEY, id],
    queryFn: () => getToolboxTalkSchedule(id),
    enabled: !!id,
  });
}

// ============================================
// Schedules Mutation Hooks
// ============================================

export function useCreateToolboxTalkSchedule() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: CreateToolboxTalkScheduleRequest) => createToolboxTalkSchedule(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: TOOLBOX_TALK_SCHEDULES_KEY });
      queryClient.invalidateQueries({ queryKey: [...TOOLBOX_TALKS_KEY, 'dashboard'] });
    },
  });
}

export function useUpdateToolboxTalkSchedule() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateToolboxTalkScheduleRequest }) =>
      updateToolboxTalkSchedule(id, data),
    onSuccess: (_, { id }) => {
      queryClient.invalidateQueries({ queryKey: TOOLBOX_TALK_SCHEDULES_KEY });
      queryClient.invalidateQueries({ queryKey: [...TOOLBOX_TALK_SCHEDULES_KEY, id] });
    },
  });
}

export function useCancelToolboxTalkSchedule() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) => cancelToolboxTalkSchedule(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: TOOLBOX_TALK_SCHEDULES_KEY });
      queryClient.invalidateQueries({ queryKey: [...TOOLBOX_TALKS_KEY, 'dashboard'] });
    },
  });
}

export function useProcessToolboxTalkSchedule() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) => processToolboxTalkSchedule(id),
    onSuccess: (_, id) => {
      queryClient.invalidateQueries({ queryKey: TOOLBOX_TALK_SCHEDULES_KEY });
      queryClient.invalidateQueries({ queryKey: [...TOOLBOX_TALK_SCHEDULES_KEY, id] });
      queryClient.invalidateQueries({ queryKey: [...TOOLBOX_TALKS_KEY, 'dashboard'] });
    },
  });
}
