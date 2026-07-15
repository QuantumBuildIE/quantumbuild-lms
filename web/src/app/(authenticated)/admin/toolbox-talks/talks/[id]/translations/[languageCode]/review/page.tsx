'use client';

import { useParams, useRouter } from 'next/navigation';
import { useQueryClient } from '@tanstack/react-query';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { ChevronLeft } from 'lucide-react';
import { WorkflowStateBadge } from '@/features/toolbox-talks/components/WorkflowStateBadge';
import { ReviewScreen } from '@/features/toolbox-talks/components/ReviewScreen';
import { useWorkflowSubscription } from '@/features/toolbox-talks/components/learning-wizard/hooks/useWorkflowSubscription';
import {
  useToolboxTalk,
  useValidationRun,
  contentCreationKeys,
} from '@/lib/api/toolbox-talks';

export default function TranslationReviewPage() {
  const params = useParams();
  const router = useRouter();
  const queryClient = useQueryClient();
  const talkId = params.id as string;
  const languageCode = params.languageCode as string;

  const { data: talk, isLoading: talkLoading } = useToolboxTalk(talkId);
  // Polls at a 5s cadence while any language is Translating/Validating — same mechanism
  // as TranslationWorkflowPanel's fix, since the header badge here shows the same
  // language-level workflow state.
  const { data: workflowStates, isLoading: statesLoading } = useWorkflowSubscription(talkId);

  const matchedState = useMemo(
    () => workflowStates?.find((s) => s.languageCode === languageCode) ?? null,
    [workflowStates, languageCode]
  );

  const runId = matchedState?.lastValidationRunId ?? null;

  // Edit/Retry enqueue a background re-validation job for a single section, which does
  // not flip the language's overall workflow state — mirrors the `revalidating` /
  // `hasSeenRunning` pattern in learning-wizard/steps/ValidateStep.tsx.
  const [revalidating, setRevalidating] = useState<{
    sectionIndex: number;
    hasSeenRunning: boolean;
  } | null>(null);

  const { data: runDetail, isLoading: runLoading } = useValidationRun(
    runId ? talkId : null,
    runId,
    { refetchInterval: revalidating ? 2000 : false }
  );

  useEffect(() => {
    setRevalidating(null);
  }, [runId]);

  useEffect(() => {
    if (!revalidating) return;
    if (runDetail?.status === 'Running') {
      if (!revalidating.hasSeenRunning) {
        setRevalidating((prev) => (prev ? { ...prev, hasSeenRunning: true } : prev));
      }
      return;
    }
    if (
      revalidating.hasSeenRunning &&
      (runDetail?.status === 'Completed' ||
        runDetail?.status === 'Failed' ||
        runDetail?.status === 'Cancelled')
    ) {
      setRevalidating(null);
    }
  }, [revalidating, runDetail?.status]);

  const canAccept = useMemo(
    () =>
      runDetail?.results.every(
        (r) => r.reviewerDecision != null && r.reviewerDecision !== 'Pending'
      ) ?? false,
    [runDetail]
  );

  const onSectionsChanged = useCallback(
    (info?: { sectionIndex: number; action: 'accept' | 'edit' | 'retry' }) => {
      if (runId) {
        queryClient.invalidateQueries({
          queryKey: contentCreationKeys.validationRun(talkId, runId),
        });
      }
      if (info && (info.action === 'edit' || info.action === 'retry')) {
        setRevalidating({ sectionIndex: info.sectionIndex, hasSeenRunning: false });
      }
    },
    [queryClient, talkId, runId]
  );

  const isLoading = talkLoading || statesLoading;

  if (isLoading) {
    return (
      <div className="space-y-6">
        <div className="flex items-center gap-4">
          <Skeleton className="h-10 w-10 rounded-md" />
          <div>
            <Skeleton className="mb-2 h-8 w-64" />
            <Skeleton className="h-4 w-48" />
          </div>
        </div>
        <Skeleton className="h-24 w-full" />
        <Skeleton className="h-64 w-full" />
      </div>
    );
  }

  if (!talk) {
    return (
      <div className="space-y-6">
        <div className="flex items-center gap-4">
          <Button variant="ghost" size="icon" onClick={() => router.back()}>
            <ChevronLeft className="h-4 w-4" />
          </Button>
        </div>
        <Card className="p-8 text-center">
          <p className="text-destructive">Learning not found.</p>
          <Button
            className="mt-4"
            onClick={() => router.push('/admin/toolbox-talks/talks')}
          >
            Back to Learnings
          </Button>
        </Card>
      </div>
    );
  }

  if (!matchedState || !runId) {
    return (
      <div className="space-y-6">
        <div className="flex items-center gap-4">
          <Button
            variant="ghost"
            size="icon"
            onClick={() => router.push(`/admin/toolbox-talks/talks/${talkId}`)}
          >
            <ChevronLeft className="h-4 w-4" />
          </Button>
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">{talk.title}</h1>
            <p className="text-muted-foreground">
              Translation Review — {languageCode.toUpperCase()}
            </p>
          </div>
        </div>
        <Card className="p-8 text-center">
          <p className="text-muted-foreground">
            No validation run available for this language.
          </p>
          <p className="mt-1 text-sm text-muted-foreground">
            Run validation first from the talk detail page.
          </p>
          <Button
            className="mt-4"
            onClick={() => router.push(`/admin/toolbox-talks/talks/${talkId}`)}
          >
            Back to Learning
          </Button>
        </Card>
      </div>
    );
  }

  if (runLoading || !runDetail) {
    return (
      <div className="space-y-6">
        <div className="flex items-center gap-4">
          <Skeleton className="h-10 w-10 rounded-md" />
          <div>
            <Skeleton className="mb-2 h-8 w-64" />
            <Skeleton className="h-4 w-48" />
          </div>
        </div>
        <Skeleton className="h-24 w-full" />
        <Skeleton className="h-64 w-full" />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center gap-4">
        <Button
          variant="ghost"
          size="icon"
          onClick={() => router.push(`/admin/toolbox-talks/talks/${talkId}`)}
        >
          <ChevronLeft className="h-4 w-4" />
        </Button>
        <div className="min-w-0 flex-1">
          <h1 className="truncate text-2xl font-semibold tracking-tight">
            {talk.title}
          </h1>
          <p className="text-muted-foreground">
            Translation Review — {languageCode.toUpperCase()}
          </p>
        </div>
        <WorkflowStateBadge state={matchedState.state} />
      </div>

      <ReviewScreen
        toolboxTalkId={talkId}
        languageCode={languageCode}
        runId={runId}
        runDetail={runDetail}
        canAccept={canAccept}
        onSectionsChanged={onSectionsChanged}
        revalidatingSectionIndex={revalidating?.sectionIndex ?? null}
      />
    </div>
  );
}
