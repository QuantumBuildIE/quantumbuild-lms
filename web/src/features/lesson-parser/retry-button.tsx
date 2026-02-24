'use client';

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '@/components/ui/alert-dialog';
import { RefreshCw } from 'lucide-react';
import { useRetryJob } from '@/features/lesson-parser/hooks/use-parse-jobs';
import { usePermission } from '@/lib/auth/use-auth';

interface RetryButtonProps {
  jobId: string;
}

export function RetryButton({ jobId }: RetryButtonProps) {
  const [open, setOpen] = useState(false);
  const hasAdminPermission = usePermission('LessonParser.Admin');
  const retryMutation = useRetryJob();

  if (!hasAdminPermission) return null;

  const handleRetry = () => {
    retryMutation.mutate(jobId);
    setOpen(false);
  };

  return (
    <AlertDialog open={open} onOpenChange={setOpen}>
      <AlertDialogTrigger asChild>
        <Button variant="ghost" size="sm" className="gap-1.5" disabled={retryMutation.isPending}>
          <RefreshCw className="h-3.5 w-3.5" />
          Retry
        </Button>
      </AlertDialogTrigger>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Retry Parse Job</AlertDialogTitle>
          <AlertDialogDescription>
            This will re-process the document using the previously extracted content. Are you sure you want to retry?
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>Cancel</AlertDialogCancel>
          <AlertDialogAction onClick={handleRetry}>Retry</AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
