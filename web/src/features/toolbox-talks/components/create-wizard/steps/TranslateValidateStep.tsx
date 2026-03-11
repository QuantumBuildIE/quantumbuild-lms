'use client';

import { useState, useMemo, useEffect, useCallback } from 'react';
import { Button } from '@/components/ui/button';
import { Alert, AlertDescription } from '@/components/ui/alert';
import {
  ArrowRight,
  ArrowLeft,
  Loader2,
  AlertTriangle,
  Languages,
} from 'lucide-react';
import { toast } from 'sonner';
import {
  useCreationSession,
  useSessionValidationRun,
  useSessionSectionDecision,
} from '@/lib/api/toolbox-talks/use-content-creation';
import { useValidationHub } from '@/features/toolbox-talks/hooks/use-validation-hub';
import type { WizardState } from '../CreateWizard';
import type { SectionValidationResult } from '@/types/content-creation';
import { ValidationProgressPanel } from './validate/ValidationProgressPanel';
import { ValidationSectionCard } from './validate/ValidationSectionCard';

// ============================================
// Props
// ============================================

interface TranslateValidateStepProps {
  state: WizardState;
  updateState: (updates: Partial<WizardState>) => void;
  onNext: () => void;
  onBack: () => void;
}

// ============================================
// Types
// ============================================

interface RunEntry {
  runId: string;
  languageCode: string;
}

interface MergedSection {
  index: number;
  title: string;
  result: SectionValidationResult | null;
  isRunning: boolean;
}

// ============================================
// Component
// ============================================

export function TranslateValidateStep({
  state,
  updateState,
  onNext,
  onBack,
}: TranslateValidateStepProps) {
  const sessionId = state.sessionId;

  // Fetch session to get validation run IDs and track overall status
  const { data: session, refetch: refetchSession } = useCreationSession(sessionId);

  // Parse validation run entries from session JSON
  const runEntries = useMemo<RunEntry[]>(() => {
    if (!session?.validationRunIds) return [];
    try {
      const parsed = JSON.parse(session.validationRunIds);
      if (!Array.isArray(parsed)) return [];
      // Pair run IDs with target language codes in order
      return parsed.map((id: string, i: number) => ({
        runId: id,
        languageCode: state.targetLanguageCodes[i] ?? 'unknown',
      }));
    } catch {
      return [];
    }
  }, [session?.validationRunIds, state.targetLanguageCodes]);

  // Active language tab (for multi-language support)
  const [activeIndex, setActiveIndex] = useState(0);
  const activeEntry = runEntries[activeIndex] ?? null;

  // SignalR hub connection for active run
  const hub = useValidationHub(activeEntry?.runId ?? null);

  // The talk ID created by content generation — needed for validation API calls
  const talkId = session?.outputId ?? null;

  // REST data for active run
  const {
    data: runDetail,
    refetch: refetchRun,
    isLoading: isRunLoading,
  } = useSessionValidationRun(talkId, activeEntry?.runId ?? null);

  // Section decision mutation
  const sectionDecision = useSessionSectionDecision();

  // Poll session status while validation is running across all languages
  useEffect(() => {
    if (!sessionId || session?.status !== 'TranslatingValidating') return;
    const interval = setInterval(() => refetchSession(), 5000);
    return () => clearInterval(interval);
  }, [sessionId, session?.status, refetchSession]);

  // Refetch run detail when hub reports section completions or overall completion
  const prevCompletedCount = useMemo(
    () => hub.completedSections.size,
    [hub.completedSections]
  );

  useEffect(() => {
    if (prevCompletedCount > 0 || hub.isComplete) {
      refetchRun();
    }
  }, [prevCompletedCount, hub.isComplete, refetchRun]);

  // When active run's hub signals complete, also refetch session for status transition
  useEffect(() => {
    if (hub.isComplete) {
      refetchSession();
    }
  }, [hub.isComplete, refetchSession]);

  // ============================================
  // Merge section data from hub + REST
  // ============================================

  const mergedSections = useMemo<MergedSection[]>(() => {
    const total = state.parsedSections.length;
    return Array.from({ length: total }, (_, i) => {
      // REST is authoritative
      const restResult =
        runDetail?.results?.find((r) => r.sectionIndex === i) ?? null;
      // Hub data as real-time fallback
      const hubResult = hub.completedSections.get(i) ?? null;
      const result = restResult ?? hubResult;

      // Is this section currently being processed?
      const isRunning =
        !result &&
        !hub.isComplete &&
        hub.progress?.sectionIndex === i;

      return {
        index: i,
        title: state.parsedSections[i]?.title ?? `Section ${i + 1}`,
        result,
        isRunning,
      };
    });
  }, [
    state.parsedSections,
    runDetail,
    hub.completedSections,
    hub.progress,
    hub.isComplete,
  ]);

  // ============================================
  // Compute stats
  // ============================================

  const stats = useMemo(() => {
    let pass = 0,
      review = 0,
      fail = 0,
      running = 0,
      pending = 0;
    const scores: number[] = [];

    for (const s of mergedSections) {
      if (s.result) {
        scores.push(s.result.finalScore);
        if (s.result.outcome === 'Pass') pass++;
        else if (s.result.outcome === 'Review') review++;
        else if (s.result.outcome === 'Fail') fail++;
      } else if (s.isRunning) {
        running++;
      } else {
        pending++;
      }
    }

    const overallScore =
      scores.length > 0
        ? Math.round(scores.reduce((a, b) => a + b, 0) / scores.length)
        : 0;

    const percentComplete = hub.progress?.percentComplete
      ?? (mergedSections.length > 0
        ? (scores.length / mergedSections.length) * 100
        : 0);

    return {
      overallScore,
      percentComplete,
      sectionsComplete: scores.length,
      totalSections: mergedSections.length,
      statusCounts: { pass, review, fail, running, pending },
    };
  }, [mergedSections, hub.progress]);

  // Use run detail for authoritative aggregates when available
  const safetyVerdict = runDetail?.safetyVerdict ?? null;
  const sourceDialect = runDetail?.sourceDialect ?? null;
  const passThreshold = runDetail?.passThreshold ?? state.passThreshold;
  const languageCode = activeEntry?.languageCode ?? '';

  // ============================================
  // Section action handlers
  // ============================================

  const handleSectionAction = useCallback(
    (
      sectionIndex: number,
      action: 'accept' | 'edit' | 'retry',
      editedTranslation?: string
    ) => {
      if (!talkId || !activeEntry?.runId) return;

      sectionDecision.mutate(
        {
          talkId,
          runId: activeEntry.runId,
          sectionIndex,
          action,
          editedTranslation,
        },
        {
          onSuccess: () => {
            const label =
              action === 'accept'
                ? 'accepted'
                : action === 'edit'
                  ? 'edited & re-validating'
                  : 'retrying';
            toast.success(`Section ${sectionIndex + 1} ${label}`);
          },
          onError: (error) => {
            toast.error('Action failed', {
              description:
                error instanceof Error ? error.message : 'Unknown error',
            });
          },
        }
      );
    },
    [talkId, activeEntry?.runId, sectionDecision]
  );

  // ============================================
  // Continue check
  // ============================================

  // Gate on session-level status — all language runs must be complete
  const canContinue = session?.status === 'Validated';

  const handleContinue = () => {
    onNext();
  };

  // ============================================
  // Loading state
  // ============================================

  if (!session || runEntries.length === 0) {
    return (
      <div className="flex items-center justify-center py-12">
        <Loader2 className="mr-2 h-5 w-5 animate-spin text-muted-foreground" />
        <span className="text-muted-foreground">
          Initializing validation...
        </span>
      </div>
    );
  }

  // ============================================
  // Initial loading — run detail not yet fetched and no hub progress received
  // ============================================

  const hasHubProgress =
    hub.isConnected || hub.completedSections.size > 0 || hub.isComplete;
  const isInitialLoading = isRunLoading && !hasHubProgress;

  // ============================================
  // Render
  // ============================================

  if (isInitialLoading) {
    return (
      <div className="space-y-6">
        <div className="flex flex-col items-center justify-center gap-3 rounded-lg border bg-card py-16">
          <Loader2 className="h-8 w-8 animate-spin text-primary" />
          <p className="text-sm font-medium text-muted-foreground">
            Starting translation validation&hellip;
          </p>
          <p className="text-xs text-muted-foreground/70">
            This may take a few minutes. Sections will appear as they are
            validated.
          </p>
        </div>

        {/* Bottom bar (back only) */}
        <div className="flex items-center justify-between border-t pt-4">
          <Button variant="outline" onClick={onBack}>
            <ArrowLeft className="mr-2 h-4 w-4" />
            Back
          </Button>
          <Button disabled>
            Continue
            <ArrowRight className="ml-2 h-4 w-4" />
          </Button>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Language tabs (if multiple languages) */}
      {runEntries.length > 1 && (
        <div className="flex gap-2">
          {runEntries.map((entry, i) => (
            <Button
              key={entry.runId}
              variant={i === activeIndex ? 'default' : 'outline'}
              size="sm"
              onClick={() => setActiveIndex(i)}
              className="gap-1.5"
            >
              <Languages className="h-3.5 w-3.5" />
              {entry.languageCode.toUpperCase()}
            </Button>
          ))}
        </div>
      )}

      {/* Hub error */}
      {hub.error && (
        <Alert variant="destructive">
          <AlertTriangle className="h-4 w-4" />
          <AlertDescription>{hub.error}</AlertDescription>
        </Alert>
      )}

      {/* Progress panel */}
      <ValidationProgressPanel
        overallScore={runDetail?.overallScore ?? stats.overallScore}
        percentComplete={stats.percentComplete}
        sectionsComplete={stats.sectionsComplete}
        totalSections={stats.totalSections}
        statusCounts={stats.statusCounts}
        safetyVerdict={safetyVerdict}
        sourceDialect={sourceDialect}
        progressMessage={hub.progress?.message ?? ''}
        isConnected={hub.isConnected}
      />

      {/* Section cards (flat layout) */}
      <div className="space-y-3">
        {mergedSections.map((section) => (
          <ValidationSectionCard
            key={section.index}
            sectionIndex={section.index}
            sectionTitle={section.title}
            result={section.result}
            isRunning={section.isRunning}
            languageCode={languageCode}
            passThreshold={passThreshold}
            onAccept={() =>
              handleSectionAction(section.index, 'accept')
            }
            onEdit={(text) =>
              handleSectionAction(section.index, 'edit', text)
            }
            onRetry={() =>
              handleSectionAction(section.index, 'retry')
            }
            isDecisionPending={sectionDecision.isPending}
            defaultExpanded={section.result?.outcome === 'Review'}
          />
        ))}
      </div>

      {/* Bottom bar */}
      <div className="flex items-center justify-between border-t pt-4">
        <Button variant="outline" onClick={onBack}>
          <ArrowLeft className="mr-2 h-4 w-4" />
          Back
        </Button>

        <div className="flex items-center gap-4">
          {/* Summary */}
          <span className="text-sm text-muted-foreground">
            {canContinue ? (
              <>
                All {runEntries.length} language
                {runEntries.length !== 1 ? 's' : ''} validated
              </>
            ) : (
              <>
                {stats.sectionsComplete}/{stats.totalSections} sections
                {runEntries.length > 1 && (
                  <> &middot; {runEntries.length} languages</>
                )}
              </>
            )}
          </span>

          <Button onClick={handleContinue} disabled={!canContinue}>
            Continue
            <ArrowRight className="ml-2 h-4 w-4" />
          </Button>
        </div>
      </div>
    </div>
  );
}
