'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import {
  AlertDialog,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { WizardLayout } from '@/features/toolbox-talks/components/learning-wizard/components/WizardLayout';
import { LoadingState } from '@/features/toolbox-talks/components/learning-wizard/components/LoadingState';
import { ErrorState } from '@/features/toolbox-talks/components/learning-wizard/components/ErrorState';
import { ParseStep } from '@/features/toolbox-talks/components/learning-wizard/steps/ParseStep';
import { useTalk } from '@/features/toolbox-talks/components/learning-wizard/hooks/useTalk';
import { useStepNavigation } from '@/features/toolbox-talks/components/learning-wizard/hooks/useStepNavigation';
import { useValidationRuns } from '@/lib/api/toolbox-talks/use-content-creation';
import { useDeleteToolboxTalk } from '@/lib/api/toolbox-talks';
import { getDraftsUrl } from '@/features/toolbox-talks/components/learning-wizard/lib/urlState';

export default function LearningWizardParsePage() {
  const params = useParams();
  const talkId = params.talkId as string;
  const router = useRouter();
  const [showCancelConfirm, setShowCancelConfirm] = useState(false);
  const deleteMutation = useDeleteToolboxTalk();

  const { talk, isLoading, isError, error, refetch } = useTalk(talkId);
  const { data: validationRuns } = useValidationRuns(talkId);
  const { reachableSteps, canGoBack, canGoNext, goBack, goNext, goToStep, isNavigating } =
    useStepNavigation({ talkId, currentStep: 2, talk: talk ?? null, validationRuns });

  const handleCancelConfirm = async () => {
    try {
      await deleteMutation.mutateAsync(talkId);
      setShowCancelConfirm(false);
      toast.success('Draft discarded.');
      router.push('/admin/toolbox-talks/talks');
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Failed to discard draft';
      toast.error('Error', { description: message });
    }
  };

  if (isLoading) return <LoadingState label="Loading learning…" />;
  if (isError)
    return (
      <ErrorState
        heading="Failed to load learning"
        message={error?.message}
        onRetry={refetch}
        onBack={() => router.push(getDraftsUrl())}
      />
    );

  return (
    <>
      <WizardLayout
        title="New Learning"
        steps={reachableSteps}
        currentStep={2}
        onStepClick={goToStep}
        canGoBack={canGoBack}
        canGoNext={false}
        onBack={goBack}
        isNavigating={isNavigating}
        leftFooter={
          <Button variant="ghost" onClick={() => setShowCancelConfirm(true)}>
            Cancel
          </Button>
        }
      >
        <ParseStep talkId={talkId} onContinue={goNext} />
      </WizardLayout>

      <AlertDialog open={showCancelConfirm} onOpenChange={setShowCancelConfirm}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Discard this learning?</AlertDialogTitle>
            <AlertDialogDescription>
              The draft will be permanently deleted and cannot be recovered.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={deleteMutation.isPending}>
              Keep editing
            </AlertDialogCancel>
            <Button
              onClick={handleCancelConfirm}
              disabled={deleteMutation.isPending}
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
            >
              {deleteMutation.isPending ? 'Discarding…' : 'Yes, discard'}
            </Button>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}
