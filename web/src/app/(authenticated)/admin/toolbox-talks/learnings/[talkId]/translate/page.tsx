'use client';

import { useParams, useRouter } from 'next/navigation';
import { WizardLayout } from '@/features/toolbox-talks/components/learning-wizard/components/WizardLayout';
import { LoadingState } from '@/features/toolbox-talks/components/learning-wizard/components/LoadingState';
import { ErrorState } from '@/features/toolbox-talks/components/learning-wizard/components/ErrorState';
import { TranslateStep } from '@/features/toolbox-talks/components/learning-wizard/steps/TranslateStep';
import { useTalk } from '@/features/toolbox-talks/components/learning-wizard/hooks/useTalk';
import { useStepNavigation } from '@/features/toolbox-talks/components/learning-wizard/hooks/useStepNavigation';
import { useValidationRuns } from '@/lib/api/toolbox-talks/use-content-creation';
import { getDraftsUrl } from '@/features/toolbox-talks/components/learning-wizard/lib/urlState';

export default function LearningWizardTranslatePage() {
  const params = useParams();
  const talkId = params.talkId as string;
  const router = useRouter();

  const { talk, isLoading, isError, error, refetch } = useTalk(talkId);
  const { data: validationRuns } = useValidationRuns(talkId);
  const { reachableSteps, canGoBack, canGoNext, goBack, goNext, goToStep, isNavigating } =
    useStepNavigation({ talkId, currentStep: 5, talk: talk ?? null, validationRuns });

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
    <WizardLayout
      title="New Learning"
      steps={reachableSteps}
      currentStep={5}
      onStepClick={goToStep}
      canGoBack={canGoBack}
      canGoNext={canGoNext}
      onBack={goBack}
      onNext={goNext}
      isNavigating={isNavigating}
    >
      <TranslateStep talkId={talkId} />
    </WizardLayout>
  );
}
