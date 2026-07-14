'use client';

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { generateQuiz } from '@/lib/api/toolbox-talks/toolbox-talks';
import type { ToolboxTalk } from '@/types/toolbox-talks';

export function useGenerateQuiz(talkId: string) {
  const queryClient = useQueryClient();

  return useMutation<ToolboxTalk, Error>({
    mutationFn: () => generateQuiz(talkId),
    onSuccess: (data) => {
      queryClient.invalidateQueries({ queryKey: ['learnings', talkId] });
      queryClient.setQueryData(['learnings', talkId], data);
    },
  });
}
