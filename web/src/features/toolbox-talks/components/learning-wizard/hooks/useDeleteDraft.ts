'use client';

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { deleteToolboxTalk } from '@/lib/api/toolbox-talks/toolbox-talks';
import { toast } from 'sonner';

export function useDeleteDraft() {
  const queryClient = useQueryClient();

  return useMutation<void, Error, string>({
    mutationFn: (talkId: string) => deleteToolboxTalk(talkId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['learnings', 'drafts'] });
      toast.success('Draft deleted');
    },
    onError: () => {
      toast.error('Failed to delete draft');
    },
  });
}
