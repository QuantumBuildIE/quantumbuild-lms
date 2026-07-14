'use client';

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { publishTalk } from '@/lib/api/toolbox-talks/toolbox-talks';

export function usePublishTalk(talkId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => publishTalk(talkId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['learnings', talkId] });
      queryClient.invalidateQueries({ queryKey: ['learnings', 'drafts'] });
      queryClient.invalidateQueries({ queryKey: ['toolbox-talks'] });
    },
  });
}
