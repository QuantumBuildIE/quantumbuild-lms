'use client';

import { useState, useMemo, useCallback } from 'react';
import { Button } from '@/components/ui/button';
import { WizardSectionDivider } from '@/components/ui/wizard-section-divider';
import {
  ArrowRight,
  ArrowLeft,
  Languages,
} from 'lucide-react';
import { toast } from 'sonner';
import {
  useCreationSession,
  useSessionValidationRun,
  useSessionSectionDecision,
} from '@/lib/api/toolbox-talks/use-content-creation';
import type { WizardState } from '../CreateWizard';
import { ValidationProgressPanel } from './validate/ValidationProgressPanel';
import { ValidationSectionCard } from './validate/ValidationSectionCard';

// ============================================
// Props
// ============================================

interface ValidateStepProps {
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

// ============================================
// Component
// ============================================

export function ValidateStep({
  state,
  updateState,
  onNext,
  onBack,
}: ValidateStepProps) {
  const sessionId = state.sessionId;

  const { data: session } = useCreationSession(sessionId);

  const runEntries = useMemo<RunEntry[]>(() => {
    if (!session?.validationRunIds) return [];
    try {
      const parsed = JSON.parse(session.validationRunIds);
      if (!Array.isArray(parsed)) return [];
      return parsed.map((id: string, i: number) => ({
        runId: id,
        languageCode: state.targetLanguageCodes[i] ?? 'unknown',
      }));
    } catch {
      return [];
    }
  }, [session?.validationRunIds, state.targetLanguageCodes]);

  const [activeIndex, setActiveIndex] = useState(0);
  const activeEntry = runEntries[activeIndex] ?? null;

  const talkId = session?.outputTalkId ?? null;

  const { data: runDetail, refetch: refetchRun } = useSessionValidationRun(
    talkId,
    activeEntry?.runId ?? null
  );

  const sectionDecision = useSessionSectionDecision();

  const passThreshold = runDetail?.passThreshold ?? state.passThreshold;
  const languageCode = activeEntry?.languageCode ?? '';

  // Map parsed sections to REST results
  const mergedSections = useMemo(() => {
    const total = state.parsedSections.length;
    return Array.from({ length: total }, (_, i) => ({
      index: i,
      title: state.parsedSections[i]?.title ?? `Section ${i + 1}`,
      result: runDetail?.results?.find((r) => r.sectionIndex === i) ?? null,
    }));
  }, [state.parsedSections, runDetail]);

  // Aggregate stats for summary panel
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

  // ============================================
  // Section action handlers (Accept / Edit / Retry)
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
            // Refetch after a brief delay to pick up the updated decision
            setTimeout(() => refetchRun(), 1500);
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
    [talkId, activeEntry?.runId, sectionDecision, refetchRun]
  );

  // ============================================
  // Continue check
  // ============================================

  // All sections need a non-Pending reviewer decision, or the session is already Validated
  const allSectionsDecided = mergedSections.every(
    (s) =>
      s.result?.reviewerDecision != null &&
      s.result.reviewerDecision !== 'Pending'
  );
  const canContinue = allSectionsDecided || session?.status === 'Validated';

  const safetyVerdict = runDetail?.safetyVerdict ?? null;
  const sourceDialect = runDetail?.sourceDialect ?? null;

  // ============================================
  // Render
  // ============================================

  return (
    <div className="space-y-6">
      <WizardSectionDivider number="6a" label="Validation Results" firstSection />

      {/* Language tabs */}
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

      {/* Aggregate progress summary */}
      <ValidationProgressPanel
        overallScore={runDetail?.overallScore ?? stats.overallScore}
        percentComplete={100}
        sectionsComplete={stats.sectionsComplete}
        totalSections={stats.totalSections}
        statusCounts={stats.statusCounts}
        safetyVerdict={safetyVerdict}
        sourceDialect={sourceDialect}
        progressMessage=""
        isConnected={false}
      />

      {/* Section cards with reviewer actions */}
      <div className="space-y-3">
        {mergedSections.map((section) => (
          <ValidationSectionCard
            key={section.index}
            sectionIndex={section.index}
            sectionTitle={section.title}
            result={section.result}
            isRunning={false}
            languageCode={languageCode}
            passThreshold={passThreshold}
            onAccept={() => handleSectionAction(section.index, 'accept')}
            onEdit={(text) => handleSectionAction(section.index, 'edit', text)}
            onRetry={() => handleSectionAction(section.index, 'retry')}
            isDecisionPending={sectionDecision.isPending}
            defaultExpanded={section.result?.outcome === 'Review'}
          />
        ))}
      </div>

      <WizardSectionDivider number="6b" label="Summary" />

      {/* Summary bar */}
      <div className="flex items-center justify-between rounded-lg border bg-muted/30 px-4 py-3 text-sm">
        <span className="text-muted-foreground">
          {stats.statusCounts.pass} passed &middot;{' '}
          {stats.statusCounts.review} for review &middot;{' '}
          {stats.statusCounts.fail} failed
        </span>
        {canContinue && (
          <span className="font-medium text-green-600 dark:text-green-400">
            Ready to publish
          </span>
        )}
      </div>

      {/* Bottom bar */}
      <div className="flex items-center justify-between border-t pt-4">
        <Button variant="outline" onClick={onBack}>
          <ArrowLeft className="mr-2 h-4 w-4" />
          Back
        </Button>

        <div className="flex items-center gap-4">
          <span className="text-sm text-muted-foreground">
            {canContinue ? (
              <>
                All {runEntries.length} language
                {runEntries.length !== 1 ? 's' : ''} reviewed
              </>
            ) : (
              <>
                {stats.sectionsComplete}/{stats.totalSections} sections reviewed
              </>
            )}
          </span>

          <Button onClick={onNext} disabled={!canContinue}>
            Continue
            <ArrowRight className="ml-2 h-4 w-4" />
          </Button>
        </div>
      </div>
    </div>
  );
}
