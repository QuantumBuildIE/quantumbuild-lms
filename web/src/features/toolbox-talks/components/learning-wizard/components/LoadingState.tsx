'use client';

import { Skeleton } from '@/components/ui/skeleton';

interface LoadingStateProps {
  label?: string;
}

export function LoadingState({ label = 'Loading...' }: LoadingStateProps) {
  return (
    <div className="space-y-4" role="status" aria-label={label}>
      <div className="flex items-center gap-3">
        <Skeleton className="h-5 w-5 rounded-full" />
        <Skeleton className="h-5 w-48" />
      </div>
      <Skeleton className="h-32 w-full rounded-lg" />
      <Skeleton className="h-32 w-full rounded-lg" />
      <Skeleton className="h-10 w-32" />
      <span className="sr-only">{label}</span>
    </div>
  );
}
