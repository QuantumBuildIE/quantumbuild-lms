'use client';

import { useQuery } from '@tanstack/react-query';
import { getToolboxTalk } from '@/lib/api/toolbox-talks/toolbox-talks';
import type { ToolboxTalk } from '@/types/toolbox-talks';

export function useTalk(talkId: string | null) {
  const query = useQuery<ToolboxTalk, Error>({
    queryKey: ['learnings', talkId],
    queryFn: () => getToolboxTalk(talkId!),
    enabled: !!talkId,
    staleTime: 30_000,
  });

  return {
    talk: query.data ?? null,
    isLoading: query.isLoading,
    isError: query.isError,
    error: query.error,
    refetch: query.refetch,
  };
}
