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
      // This endpoint doesn't own coverImageUrl (that's uploaded/removed via its own
      // dedicated endpoint). Its response can carry a stale snapshot of that field if a
      // cover-image upload commits concurrently, so preserve whatever the cache already
      // has for it instead of letting this response clobber it.
      queryClient.setQueryData<ToolboxTalk>(['learnings', talkId], (prev) =>
        prev ? { ...data, coverImageUrl: prev.coverImageUrl } : data
      );
    },
  });
}
