'use client';

import { useMutation, useQueryClient } from '@tanstack/react-query';
import {
  updateTalkQuizSettings,
  type UpdateTalkQuizSettingsRequest,
} from '@/lib/api/toolbox-talks/toolbox-talks';
import type { ToolboxTalk } from '@/types/toolbox-talks';

export function useUpdateQuizSettings(talkId: string) {
  const queryClient = useQueryClient();

  return useMutation<ToolboxTalk, Error, UpdateTalkQuizSettingsRequest>({
    mutationFn: (settings) => updateTalkQuizSettings(talkId, settings),
    onSuccess: (data) => {
      queryClient.invalidateQueries({ queryKey: ['learnings', talkId] });
      queryClient.setQueryData(['learnings', talkId], data);
    },
  });
}
