'use client';

import { useQuery } from '@tanstack/react-query';
import { getToolboxTalks } from '@/lib/api/toolbox-talks/toolbox-talks';
import type { ToolboxTalkListItem } from '@/types/toolbox-talks';

export function useDraftsList() {
  const query = useQuery<ToolboxTalkListItem[], Error>({
    queryKey: ['learnings', 'drafts'],
    queryFn: async () => {
      const result = await getToolboxTalks({ status: 'Draft', pageSize: 100 });
      return result.items;
    },
    staleTime: 15_000,
  });

  return {
    drafts: query.data ?? [],
    isLoading: query.isLoading,
    isError: query.isError,
    error: query.error,
    refetch: query.refetch,
  };
}
