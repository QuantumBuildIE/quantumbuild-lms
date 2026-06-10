'use client';

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/lib/api/client';
import type { ToolboxTalk } from '@/types/toolbox-talks';
import type { InputMode } from '../schemas/inputConfigSchema';

export interface InitialiseToolboxTalkRequest {
  title: string;
  code?: string;
  description?: string;
  category?: string;
  inputMode: InputMode;
  sourceLanguageCode: string;
  sourceText?: string;
  sourceFileUrl?: string;
  sourceFileName?: string;
  sourceFileType?: string;
  videoUrl?: string;
  videoSource?: string;
  targetLanguageCodes: string[];
  reviewerName?: string;
  reviewerOrg?: string;
  reviewerRole?: string;
  documentRef?: string;
  clientName?: string;
  auditPurpose?: string;
  audienceRole: string;
  preserveSourceWording: boolean;
  includeQuiz: boolean;
}

async function initialiseToolboxTalk(request: InitialiseToolboxTalkRequest): Promise<ToolboxTalk> {
  const response = await apiClient.post<ToolboxTalk>('/toolbox-talks/initialise', request);
  return response.data;
}

export function useInitialiseToolboxTalk() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: initialiseToolboxTalk,
    onSuccess: (data) => {
      // Seed the query cache so the parse step can load immediately
      queryClient.setQueryData(['learnings', data.id], data);
      // Invalidate the drafts list so the new draft appears
      queryClient.invalidateQueries({ queryKey: ['learnings'] });
      queryClient.invalidateQueries({ queryKey: ['learnings', 'drafts'] });
    },
  });
}
