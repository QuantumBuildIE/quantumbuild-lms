'use client';

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { removeCoverImage } from '@/lib/api/toolbox-talks/toolbox-talks';
import type { ToolboxTalk } from '@/types/toolbox-talks';

export function useRemoveCoverImage(talkId: string) {
  const queryClient = useQueryClient();

  return useMutation<{ coverImageUrl: string | null }, Error, void>({
    mutationFn: () => removeCoverImage(talkId),
    onSuccess: () => {
      queryClient.setQueryData<ToolboxTalk>(['learnings', talkId], (prev) =>
        prev ? { ...prev, coverImageUrl: null } : prev
      );
    },
  });
}
