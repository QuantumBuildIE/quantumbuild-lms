'use client';

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { updateTalkSections, type UpdateTalkSectionRequest } from '@/lib/api/toolbox-talks/toolbox-talks';
import type { ToolboxTalk } from '@/types/toolbox-talks';

export function useUpdateTalkSections(talkId: string) {
  const queryClient = useQueryClient();

  return useMutation<ToolboxTalk, Error, UpdateTalkSectionRequest[]>({
    mutationFn: (sections) => updateTalkSections(talkId, sections),
    onSuccess: (data) => {
      queryClient.setQueryData(['learnings', talkId], data);
    },
  });
}
