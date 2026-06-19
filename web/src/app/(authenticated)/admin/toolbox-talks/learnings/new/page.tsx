'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
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
} from '@/components/ui/alert-dialog';
import { WizardLayout } from '@/features/toolbox-talks/components/learning-wizard/components/WizardLayout';
import { InputConfigStep } from '@/features/toolbox-talks/components/learning-wizard/steps/InputConfigStep';
import { useStepNavigation } from '@/features/toolbox-talks/components/learning-wizard/hooks/useStepNavigation';
import { useValidationRuns } from '@/lib/api/toolbox-talks/use-content-creation';
import { getDraftsUrl } from '@/features/toolbox-talks/components/learning-wizard/lib/urlState';

export default function LearningWizardNewPage() {
  const router = useRouter();
  const [showCancelConfirm, setShowCancelConfirm] = useState(false);
  const { data: validationRuns } = useValidationRuns(null);
  const { reachableSteps, goToStep } = useStepNavigation({
    talkId: null,
    currentStep: 1,
    talk: null,
    validationRuns,
  });

  const handleCancelConfirm = () => {
    setShowCancelConfirm(false);
    router.push('/admin/toolbox-talks/talks');
  };

  return (
    <>
      <WizardLayout
        title="New Learning"
        steps={reachableSteps}
        currentStep={1}
        onStepClick={goToStep}
        canGoBack={false}
        canGoNext={false}
        leftFooter={
          <Button variant="outline" onClick={() => setShowCancelConfirm(true)}>
            Cancel
          </Button>
        }
        footer={
          <button
            type="button"
            className="text-sm text-muted-foreground hover:text-foreground underline-offset-4 hover:underline"
            onClick={() => router.push(getDraftsUrl())}
          >
            View drafts
          </button>
        }
      >
        <InputConfigStep />
      </WizardLayout>

      <AlertDialog open={showCancelConfirm} onOpenChange={setShowCancelConfirm}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Cancel creation?</AlertDialogTitle>
            <AlertDialogDescription>
              Any changes you&apos;ve made won&apos;t be saved.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Keep editing</AlertDialogCancel>
            <AlertDialogAction onClick={handleCancelConfirm}>
              Yes, cancel
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}
