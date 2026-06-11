'use client';

import { useParams, useRouter } from 'next/navigation';
import { WizardLayout } from '@/features/toolbox-talks/components/learning-wizard/components/WizardLayout';
import { LoadingState } from '@/features/toolbox-talks/components/learning-wizard/components/LoadingState';
import { ErrorState } from '@/features/toolbox-talks/components/learning-wizard/components/ErrorState';
import { QuizStep } from '@/features/toolbox-talks/components/learning-wizard/steps/QuizStep';
import { useTalk } from '@/features/toolbox-talks/components/learning-wizard/hooks/useTalk';
import { useStepNavigation } from '@/features/toolbox-talks/components/learning-wizard/hooks/useStepNavigation';
import { getDraftsUrl } from '@/features/toolbox-talks/components/learning-wizard/lib/urlState';

export default function LearningWizardQuizPage() {
  const params = useParams();
  const talkId = params.talkId as string;
  const router = useRouter();

  const { talk, isLoading, isError, error, refetch } = useTalk(talkId);
  const { reachableSteps, canGoBack, canGoNext, goBack, goNext, goToStep, isNavigating } =
    useStepNavigation({ talkId, currentStep: 3, talk: talk ?? null });

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
      currentStep={3}
      onStepClick={goToStep}
      canGoBack={canGoBack}
      canGoNext={false}
      onBack={goBack}
      isNavigating={isNavigating}
    >
      <QuizStep talkId={talkId} onContinue={goNext} />
    </WizardLayout>
  );
}
