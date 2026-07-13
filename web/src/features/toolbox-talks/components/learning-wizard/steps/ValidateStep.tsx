'use client';

import { useState, useMemo, useCallback, useEffect } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { Languages, Send } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { WorkflowSubscriber } from '../hooks/WorkflowSubscriber';
import { useTalk } from '../hooks/useTalk';
import { useWorkflowSubscription } from '../hooks/useWorkflowSubscription';
import {
  useValidationRuns,
  useValidationRun,
  useSectionDecision,
  contentCreationKeys,
} from '@/lib/api/toolbox-talks/use-content-creation';
import { useInitiateExternalReview } from '@/lib/api/toolbox-talks/use-toolbox-talks';
import { ValidationProgressPanel } from '@/features/toolbox-talks/components/create-wizard/steps/validate/ValidationProgressPanel';
import { ValidationSectionCard } from '@/features/toolbox-talks/components/create-wizard/steps/validate/ValidationSectionCard';
import { SendExternalReviewDialog } from '../../SendExternalReviewDialog';
import { LoadingState } from '../components/LoadingState';
import { parseLanguageCodes } from '@/features/toolbox-talks/utils/parseLanguageCodes';
import type { ValidationRunSummary } from '@/types/content-creation';

const LANG_NAMES: Record<string, string> = {
  fr: 'French',
  pl: 'Polish',
  ro: 'Romanian',
  uk: 'Ukrainian',
  pt: 'Portuguese',
  es: 'Spanish',
  lt: 'Lithuanian',
  de: 'German',
  lv: 'Latvian',
};

export interface ValidateStepProps {
  talkId: string;
}

export function ValidateStep({ talkId }: ValidateStepProps) {
  const queryClient = useQueryClient();
  const { talk, isLoading: talkLoading } = useTalk(talkId);
  const { data: workflowStates, activeRunIds, onValidationComplete, onSectionCompleted } =
    useWorkflowSubscription(talkId);
  const { data: validationRuns, isLoading: runsLoading } = useValidationRuns(talkId);

  const [activeIndex, setActiveIndex] = useState(0);

  const languages = parseLanguageCodes(talk?.targetLanguageCodes ?? null);

  // Latest completed (or most-recent) run per language
  const latestRunByCode = useMemo<Record<string, ValidationRunSummary>>(() => {
    const map: Record<string, ValidationRunSummary> = {};
    for (const run of validationRuns ?? []) {
      if (!map[run.languageCode]) map[run.languageCode] = run;
    }
    return map;
  }, [validationRuns]);

  const activeLangCode = languages[activeIndex] ?? null;
  const activeRun = activeLangCode ? (latestRunByCode[activeLangCode] ?? null) : null;
  const activeRunId = activeRun?.id ?? null;

  // Edit/Retry enqueue a background re-validation job (Accept is synchronous and
  // never sets this). Poll the run until the job has genuinely started and finished —
  // `hasSeenRunning` guards against the run's status still reading 'Completed' from
  // the *previous* job in the brief window before the new one is picked up.
  const [revalidating, setRevalidating] = useState<{
    sectionIndex: number;
    hasSeenRunning: boolean;
  } | null>(null);

  const { data: runDetail, refetch: refetchRun } = useValidationRun(talkId, activeRunId, {
    refetchInterval: revalidating ? 2000 : false,
  });

  // A run switch invalidates any in-flight tracking for the previous run
  useEffect(() => {
    setRevalidating(null);
  }, [activeRunId]);

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

  const sectionDecision = useSectionDecision();
  const initiateReviewMutation = useInitiateExternalReview();

  const [sendReviewLang, setSendReviewLang] = useState<{
    code: string;
    name: string;
    flaggedCount: number;
  } | null>(null);

  const handleSectionAction = useCallback(
    (
      sectionIndex: number,
      action: 'accept' | 'edit' | 'retry',
      editedTranslation?: string,
      editedOriginalText?: string
    ) => {
      if (!activeRunId) return;
      sectionDecision.mutate(
        { talkId, runId: activeRunId, sectionIndex, action, editedTranslation, editedOriginalText },
        {
          onSuccess: () => {
            const label =
              action === 'accept' ? 'accepted' : action === 'edit' ? 'edited & re-validating' : 'retrying';
            toast.success(`Section ${sectionIndex + 1} ${label}`);
            if (action === 'edit' || action === 'retry') {
              setRevalidating({ sectionIndex, hasSeenRunning: false });
            }
            refetchRun();
          },
          onError: (error: unknown) => {
            const isConflict = error instanceof Error && error.message.includes('409');
            toast.error(isConflict ? 'Revalidation already in progress' : 'Action failed', {
              description: isConflict
                ? 'A revalidation is already running for this section. Wait for it to complete.'
                : error instanceof Error
                ? error.message
                : 'Unknown error',
            });
          },
        }
      );
    },
    [talkId, activeRunId, sectionDecision, refetchRun]
  );

  const handleComplete = useCallback(
    (runId: string) => {
      onValidationComplete(runId);
      queryClient.invalidateQueries({ queryKey: contentCreationKeys.validationRuns(talkId) });
      queryClient.invalidateQueries({ queryKey: contentCreationKeys.validationRun(talkId, runId) });
    },
    [onValidationComplete, queryClient, talkId]
  );

  const passThreshold = runDetail?.passThreshold ?? activeRun?.passThreshold ?? 75;
  const safetyVerdict = runDetail?.safetyVerdict ?? null;
  const sourceDialect = runDetail?.sourceDialect ?? null;

  // Sections sorted by sectionNumber; matched to validation results by 0-based array index
  const mergedSections = useMemo(() => {
    const sorted = [...(talk?.sections ?? [])].sort((a, b) => a.sectionNumber - b.sectionNumber);
    return sorted.map((s, i) => ({
      index: i,
      title: s.title,
      result: runDetail?.results?.find((r) => r.sectionIndex === i) ?? null,
    }));
  }, [talk?.sections, runDetail]);

  // Aggregate stats for the progress panel (active language only)
  const stats = useMemo(() => {
    let pass = 0, review = 0, fail = 0;
    const scores: number[] = [];
    for (const s of mergedSections) {
      if (s.result) {
        scores.push(s.result.finalScore);
        if (s.result.outcome === 'Pass') pass++;
        else if (s.result.outcome === 'Review') review++;
        else if (s.result.outcome === 'Fail') fail++;
      }
    }
    const overallScore =
      scores.length > 0
        ? Math.round(scores.reduce((a, b) => a + b, 0) / scores.length)
        : 0;
    const pending = mergedSections.length - scores.length;
    return {
      overallScore,
      sectionsComplete: scores.length,
      totalSections: mergedSections.length,
      statusCounts: { pass, review, fail, running: 0, pending },
    };
  }, [mergedSections]);

  const activeWorkflowState = (workflowStates ?? []).find((s) => s.languageCode === activeLangCode) ?? null;
  const canSendForReview =
    activeWorkflowState?.state === 'Validated' || activeWorkflowState?.state === 'ReviewerAccepted';

  const handleSendForExternalReview = useCallback(
    async (email: string, editableSectionIndices: number[]) => {
      if (!sendReviewLang) return;
      try {
        await initiateReviewMutation.mutateAsync({
          toolboxTalkId: talkId,
          languageCode: sendReviewLang.code,
          reviewerEmail: email,
          editableSectionIndices,
        });
        toast.success(`Invitation sent to ${email}`);
        setSendReviewLang(null);
      } catch {
        toast.error(`Failed to send invitation for ${sendReviewLang.name}`);
      }
    },
    [talkId, sendReviewLang, initiateReviewMutation]
  );

  // All-language readiness: every completed run must have no pending non-Pass decisions
  const allLanguagesReady = useMemo(() => {
    const completed = (validationRuns ?? []).filter((r) => r.status === 'Completed');
    return completed.length > 0 && !completed.some((r) => r.hasPendingDecisions);
  }, [validationRuns]);

  const isLoading = talkLoading || runsLoading;

  if (isLoading) return <LoadingState label="Loading validation status…" />;

  if (languages.length === 0) {
    return (
      <div className="rounded-lg border border-dashed p-8 text-center text-muted-foreground">
        <p className="text-sm font-medium">No target languages configured</p>
      </div>
    );
  }

  return (
    <>
      {/* One SignalR subscriber per actively validating run */}
      {activeRunIds.map((runId) => (
        <WorkflowSubscriber
          key={runId}
          runId={runId}
          onComplete={handleComplete}
          onSectionCompleted={onSectionCompleted}
        />
      ))}

      {/* Send for external review dialog */}
      <SendExternalReviewDialog
        open={sendReviewLang !== null}
        onOpenChange={(open) => {
          if (!open) setSendReviewLang(null);
        }}
        onConfirm={handleSendForExternalReview}
        isLoading={initiateReviewMutation.isPending}
        flaggedWordCount={sendReviewLang?.flaggedCount ?? 0}
        languageName={
          sendReviewLang
            ? (LANG_NAMES[sendReviewLang.code] ?? sendReviewLang.code.toUpperCase())
            : ''
        }
        sections={mergedSections.map((s) => ({ title: s.title }))}
      />

      <div className="space-y-6">
        <div>
          <h2 className="text-base font-semibold">Validate</h2>
          <p className="text-sm text-muted-foreground mt-1">
            Review back-translation results for each language. Pass sections are accepted
            automatically. Accept, edit, or retry any Review or Fail sections before publishing.
          </p>
        </div>

        {/* Language tab strip — only shown when more than one language */}
        {languages.length > 1 && (
          <div className="flex gap-2 flex-wrap">
            {languages.map((code, i) => {
              const run = latestRunByCode[code];
              const hasPending = run?.status === 'Completed' && run.hasPendingDecisions;
              return (
                <Button
                  key={code}
                  variant={i === activeIndex ? 'default' : 'outline'}
                  size="sm"
                  onClick={() => setActiveIndex(i)}
                  className="gap-1.5"
                >
                  <Languages className="h-3.5 w-3.5" />
                  {LANG_NAMES[code] ?? code.toUpperCase()}
                  {hasPending && (
                    <span className="ml-0.5 h-2 w-2 rounded-full bg-amber-500 shrink-0" />
                  )}
                </Button>
              );
            })}
          </div>
        )}

        {activeRun ? (
          <>
            {/* Aggregate progress panel for the active language */}
            <ValidationProgressPanel
              overallScore={runDetail?.overallScore ?? stats.overallScore}
              percentComplete={100}
              sectionsComplete={stats.sectionsComplete}
              totalSections={stats.totalSections}
              statusCounts={stats.statusCounts}
              safetyVerdict={safetyVerdict}
              sourceDialect={sourceDialect}
              progressMessage=""
              passThreshold={passThreshold}
              isConnected={false}
            />

            {/* Send for external review — available when language is Validated or ReviewerAccepted */}
            {canSendForReview && activeLangCode && (
              <div className="flex justify-end">
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() =>
                    setSendReviewLang({
                      code: activeLangCode,
                      name: LANG_NAMES[activeLangCode] ?? activeLangCode.toUpperCase(),
                      flaggedCount: activeWorkflowState?.flaggedWordCount ?? 0,
                    })
                  }
                >
                  <Send className="mr-1.5 h-3.5 w-3.5" />
                  Send for external review
                </Button>
              </div>
            )}

            {/* Per-section review cards */}
            <div className="space-y-3">
              {mergedSections.map((section) => (
                <ValidationSectionCard
                  key={section.index}
                  sectionIndex={section.index}
                  sectionTitle={section.title}
                  result={section.result}
                  isRunning={
                    runDetail?.status === 'Running' || runDetail?.status === 'Pending'
                  }
                  isRevalidating={revalidating?.sectionIndex === section.index}
                  languageCode={activeLangCode ?? ''}
                  passThreshold={passThreshold}
                  onAccept={() => handleSectionAction(section.index, 'accept')}
                  onEdit={(editedTranslation, editedOriginalText) =>
                    handleSectionAction(section.index, 'edit', editedTranslation, editedOriginalText)
                  }
                  onRetry={() => handleSectionAction(section.index, 'retry')}
                  isDecisionPending={sectionDecision.isPending}
                  defaultExpanded={false}
                />
              ))}
            </div>
          </>
        ) : (
          <div className="rounded-lg border border-dashed p-8 text-center text-muted-foreground">
            <p className="text-sm font-medium">
              {LANG_NAMES[activeLangCode ?? ''] ?? activeLangCode} — no validation run yet
            </p>
          </div>
        )}

        {/* Summary bar */}
        <div className="flex items-center justify-between rounded-lg border bg-muted/30 px-4 py-3 text-sm">
          <span className="text-muted-foreground">
            {stats.statusCounts.pass} passed &middot; {stats.statusCounts.review} for review &middot;{' '}
            {stats.statusCounts.fail} failed
          </span>
          {allLanguagesReady && (
            <span className="font-medium text-green-600 dark:text-green-400">
              Ready to publish
            </span>
          )}
        </div>
      </div>
    </>
  );
}
