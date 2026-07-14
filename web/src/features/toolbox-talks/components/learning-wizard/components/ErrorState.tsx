'use client';

import { AlertCircle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';

interface ErrorStateProps {
  heading?: string;
  message?: string;
  onRetry?: () => void;
  retryLabel?: string;
  onBack?: () => void;
  backLabel?: string;
}

export function ErrorState({
  heading = 'Something went wrong',
  message = 'An unexpected error occurred. Please try again.',
  onRetry,
  retryLabel = 'Try again',
  onBack,
  backLabel = 'Back to drafts',
}: ErrorStateProps) {
  return (
    <div className="space-y-4" role="alert">
      <Alert variant="destructive">
        <AlertCircle className="h-4 w-4" aria-hidden="true" />
        <AlertTitle>{heading}</AlertTitle>
        <AlertDescription>{message}</AlertDescription>
      </Alert>
      <div className="flex gap-3">
        {onRetry && (
          <Button variant="default" onClick={onRetry}>
            {retryLabel}
          </Button>
        )}
        {onBack && (
          <Button variant="outline" onClick={onBack}>
            {backLabel}
          </Button>
        )}
      </div>
    </div>
  );
}
