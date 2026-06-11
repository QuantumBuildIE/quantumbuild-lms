'use client';

import { useMutation, useQueryClient } from '@tanstack/react-query';
import {
  updateTalkQuestions,
  type UpdateTalkQuestionRequest,
} from '@/lib/api/toolbox-talks/toolbox-talks';
import type { ToolboxTalk } from '@/types/toolbox-talks';

export function useUpdateTalkQuestions(talkId: string) {
  const queryClient = useQueryClient();

  return useMutation<ToolboxTalk, Error, UpdateTalkQuestionRequest[]>({
    mutationFn: (questions) => updateTalkQuestions(talkId, questions),
    onSuccess: (data) => {
      queryClient.invalidateQueries({ queryKey: ['learnings', talkId] });
      queryClient.setQueryData(['learnings', talkId], data);
    },
  });
}
