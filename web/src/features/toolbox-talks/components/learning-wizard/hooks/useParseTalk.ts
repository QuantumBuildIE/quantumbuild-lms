'use client';

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { parseTalk } from '@/lib/api/toolbox-talks/toolbox-talks';
import type { ToolboxTalk } from '@/types/toolbox-talks';

export function useParseTalk(talkId: string) {
  const queryClient = useQueryClient();

  return useMutation<ToolboxTalk, Error>({
    mutationFn: () => parseTalk(talkId),
    onSuccess: (data) => {
      queryClient.setQueryData(['learnings', talkId], data);
    },
  });
}
