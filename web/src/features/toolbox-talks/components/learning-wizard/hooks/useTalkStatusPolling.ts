'use client';

import { useQuery } from '@tanstack/react-query';
import { getToolboxTalk } from '@/lib/api/toolbox-talks/toolbox-talks';
import type { ToolboxTalk } from '@/types/toolbox-talks';

const POLL_INTERVAL_MS = 2_000;

/**
 * Polls the talk endpoint every 2 s while status is 'Processing'.
 * Stops polling once the talk moves to Draft (parse complete) or any terminal state.
 */
export function useTalkStatusPolling(talkId: string | null, enabled = true) {
  return useQuery<ToolboxTalk, Error>({
    queryKey: ['learnings', talkId],
    queryFn: () => getToolboxTalk(talkId!),
    enabled: !!talkId && enabled,
    refetchInterval: (query) => {
      const status = query.state.data?.status;
      return status === 'Processing' ? POLL_INTERVAL_MS : false;
    },
    staleTime: 0,
  });
}
