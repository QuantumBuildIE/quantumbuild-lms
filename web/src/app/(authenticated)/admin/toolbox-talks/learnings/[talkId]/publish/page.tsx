'use client';

import { useParams, useRouter } from 'next/navigation';
import { Loader2, Rocket } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { WizardLayout } from '@/features/toolbox-talks/components/learning-wizard/components/WizardLayout';
import { LoadingState } from '@/features/toolbox-talks/components/learning-wizard/components/LoadingState';
import { ErrorState } from '@/features/toolbox-talks/components/learning-wizard/components/ErrorState';
import { PublishStep } from '@/features/toolbox-talks/components/learning-wizard/steps/PublishStep';
import { useTalk } from '@/features/toolbox-talks/components/learning-wizard/hooks/useTalk';
import { useStepNavigation } from '@/features/toolbox-talks/components/learning-wizard/hooks/useStepNavigation';
import { usePublishTalk } from '@/features/toolbox-talks/components/learning-wizard/hooks/usePublishTalk';
import { useValidationRuns } from '@/lib/api/toolbox-talks/use-content-creation';
import { getDraftsUrl } from '@/features/toolbox-talks/components/learning-wizard/lib/urlState';

export default function LearningWizardPublishPage() {
  const params = useParams();
  const talkId = params.talkId as string;
  const router = useRouter();

  const { talk, isLoading, isError, error, refetch } = useTalk(talkId);
  const { data: validationRuns } = useValidationRuns(talkId);
  const { reachableSteps, canGoBack, goBack, goToStep, isNavigating } =
    useStepNavigation({ talkId, currentStep: 7, talk: talk ?? null, validationRuns });

  const { mutate: publishTalk, isPending: isPublishing, error: publishError } = usePublishTalk(talkId);

  const handlePublish = () => {
    publishTalk(undefined, {
      onSuccess: (result) => {
        toast.success('Talk published successfully');
        router.push(`/admin/toolbox-talks/talks/${result.talkId}`);
      },
      onError: (err) => {
        const axiosErr = err as import('axios').AxiosError<{ errors?: string[]; message?: string }>;
        const detail =
          axiosErr.response?.data?.errors?.[0] ??
          axiosErr.response?.data?.message ??
          err.message;
        toast.error('Publish failed', { description: detail });
      },
    });
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

  const publishFooter = (
    <Button
      size="lg"
      className="bg-green-600 hover:bg-green-700 text-white gap-2 px-8"
      onClick={handlePublish}
      disabled={isPublishing || isNavigating}
    >
      {isPublishing ? (
        <>
          <Loader2 className="h-4 w-4 animate-spin" />
          Publishing…
        </>
      ) : (
        <>
          <Rocket className="h-4 w-4" />
          Publish
        </>
      )}
    </Button>
  );

  return (
    <WizardLayout
      title="New Learning"
      steps={reachableSteps}
      currentStep={7}
      onStepClick={goToStep}
      canGoBack={canGoBack}
      canGoNext={false}
      onBack={goBack}
      isNavigating={isNavigating}
      footer={publishFooter}
    >
      <PublishStep
        talkId={talkId}
        isPublishing={isPublishing}
        publishError={publishError}
      />
    </WizardLayout>
  );
}
