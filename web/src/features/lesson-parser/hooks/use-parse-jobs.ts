'use client';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { lessonParserApi } from '@/lib/api/lesson-parser';
import { toast } from 'sonner';

// ============================================
// Query Keys
// ============================================

export const lessonParserKeys = {
  all: ['lesson-parser'] as const,
  jobs: () => [...lessonParserKeys.all, 'jobs'] as const,
  jobList: (page: number) => [...lessonParserKeys.jobs(), page] as const,
  job: (id: string) => [...lessonParserKeys.jobs(), id] as const,
};

// ============================================
// Hooks
// ============================================

/**
 * List jobs with automatic polling while any job is Processing
 */
export function useParseJobs(page: number = 1) {
  return useQuery({
    queryKey: lessonParserKeys.jobList(page),
    queryFn: () => lessonParserApi.getJobs(page),
    refetchInterval: (query) => {
      const hasProcessing = query.state.data?.items?.some(
        (j) => j.status === 'Processing'
      );
      return hasProcessing ? 5000 : false;
    },
  });
}

/**
 * Single job
 */
export function useParseJob(id: string) {
  return useQuery({
    queryKey: lessonParserKeys.job(id),
    queryFn: () => lessonParserApi.getJob(id),
    enabled: !!id,
  });
}

/**
 * Retry mutation
 */
export function useRetryJob() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) => lessonParserApi.retryJob(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: lessonParserKeys.jobs() });
      toast.success('Job requeued successfully');
    },
    onError: () => toast.error('Failed to retry job'),
  });
}
