'use client';

import { useMutation, useQueryClient } from '@tanstack/react-query';
import {
  updateTalkSettings,
  type UpdateTalkSettingsRequest,
} from '@/lib/api/toolbox-talks/toolbox-talks';
import type { ToolboxTalk } from '@/types/toolbox-talks';

export function useUpdateTalkSettings(talkId: string) {
  const queryClient = useQueryClient();

  return useMutation<ToolboxTalk, Error, UpdateTalkSettingsRequest>({
    mutationFn: (settings) => updateTalkSettings(talkId, settings),
    onSuccess: (data) => {
      queryClient.invalidateQueries({ queryKey: ['learnings', talkId] });
      queryClient.setQueryData(['learnings', talkId], data);
    },
  });
}
