'use client';

import { useRouter } from 'next/navigation';
import { WizardLayout } from '@/features/toolbox-talks/components/learning-wizard/components/WizardLayout';
import { InputConfigStep } from '@/features/toolbox-talks/components/learning-wizard/steps/InputConfigStep';
import { useStepNavigation } from '@/features/toolbox-talks/components/learning-wizard/hooks/useStepNavigation';
import { getDraftsUrl } from '@/features/toolbox-talks/components/learning-wizard/lib/urlState';

export default function LearningWizardNewPage() {
  const router = useRouter();
  const { reachableSteps, goToStep } = useStepNavigation({
    talkId: null,
    currentStep: 1,
    talk: null,
  });

  return (
    <WizardLayout
      title="New Learning"
      steps={reachableSteps}
      currentStep={1}
      onStepClick={goToStep}
      canGoBack={false}
      canGoNext={false}
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
  );
}
