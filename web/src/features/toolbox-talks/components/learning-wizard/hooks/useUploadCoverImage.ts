'use client';

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { uploadCoverImage } from '@/lib/api/toolbox-talks/toolbox-talks';
import type { ToolboxTalk } from '@/types/toolbox-talks';

export function useUploadCoverImage(talkId: string) {
  const queryClient = useQueryClient();

  return useMutation<{ coverImageUrl: string | null }, Error, File>({
    mutationFn: (file) => uploadCoverImage(talkId, file),
    onSuccess: ({ coverImageUrl }) => {
      queryClient.setQueryData<ToolboxTalk>(['learnings', talkId], (prev) =>
        prev ? { ...prev, coverImageUrl } : prev
      );
    },
  });
}
