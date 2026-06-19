'use client';

import { useParams, useRouter } from 'next/navigation';
import { WizardLayout } from '@/features/toolbox-talks/components/learning-wizard/components/WizardLayout';
import { ErrorState } from '@/features/toolbox-talks/components/learning-wizard/components/ErrorState';
import { SettingsStep } from '@/features/toolbox-talks/components/learning-wizard/steps/SettingsStep';
import { useTalk } from '@/features/toolbox-talks/components/learning-wizard/hooks/useTalk';
import { useStepNavigation } from '@/features/toolbox-talks/components/learning-wizard/hooks/useStepNavigation';
import { useValidationRuns } from '@/lib/api/toolbox-talks/use-content-creation';
import { getDraftsUrl } from '@/features/toolbox-talks/components/learning-wizard/lib/urlState';

export default function LearningWizardSettingsPage() {
  const params = useParams();
  const talkId = params.talkId as string;
  const router = useRouter();

  const { talk, isError, error, refetch } = useTalk(talkId);
  const { data: validationRuns } = useValidationRuns(talkId);
  const { reachableSteps, canGoBack, goBack, goNext, goToStep, isNavigating } =
    useStepNavigation({ talkId, currentStep: 4, talk: talk ?? null, validationRuns });

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
    <WizardLayout
      title="New Learning"
      steps={reachableSteps}
      currentStep={4}
      onStepClick={goToStep}
      canGoBack={canGoBack}
      onBack={goBack}
      isNavigating={isNavigating}
    >
      {/* SettingsStep owns its own Continue button and saves on blur (see SettingsStep.tsx §4.4 deviation note) */}
      <SettingsStep
        talkId={talkId}
        onContinue={goNext}
      />
    </WizardLayout>
  );
}
