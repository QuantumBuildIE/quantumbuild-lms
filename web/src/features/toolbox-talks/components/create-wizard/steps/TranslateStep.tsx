'use client';

import { useState, useMemo, useEffect } from 'react';
import { Button } from '@/components/ui/button';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { WizardSectionDivider } from '@/components/ui/wizard-section-divider';
import {
  ArrowRight,
  ArrowLeft,
  Loader2,
  AlertTriangle,
  Languages,
  Sparkles,
} from 'lucide-react';
import {
  useCreationSession,
  useSessionValidationRun,
} from '@/lib/api/toolbox-talks/use-content-creation';
import { useValidationHub } from '@/features/toolbox-talks/hooks/use-validation-hub';
import type { WizardState } from '../CreateWizard';
import { ValidationProgressPanel } from './validate/ValidationProgressPanel';
import { SubtitleProgressPanel } from './validate/SubtitleProgressPanel';

// ============================================
// Props
// ============================================

interface TranslateStepProps {
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

export function TranslateStep({
  state,
  updateState,
  onNext,
  onBack,
}: TranslateStepProps) {
  const sessionId = state.sessionId;

  const { data: session, refetch: refetchSession } = useCreationSession(sessionId);

  // Parse validation run entries from session JSON
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

  // Active language tab (for multi-language support)
  const [activeIndex, setActiveIndex] = useState(0);
  const activeEntry = runEntries[activeIndex] ?? null;

  // SignalR hub connection for active run — lives here while translation is running
  const hub = useValidationHub(activeEntry?.runId ?? null);

  const talkId = session?.outputTalkId ?? null;

  // REST data for active run (authoritative for aggregates)
  const { data: runDetail, refetch: refetchRun } = useSessionValidationRun(
    talkId,
    activeEntry?.runId ?? null
  );

  // Poll session status while translation/validation is running
  useEffect(() => {
    if (!sessionId || session?.status !== 'TranslatingValidating') return;
    const interval = setInterval(() => refetchSession(), 5000);
    return () => clearInterval(interval);
  }, [sessionId, session?.status, refetchSession]);

  // Refetch run detail when hub reports section completions
  const prevCompletedCount = useMemo(
    () => hub.completedSections.size,
    [hub.completedSections]
  );

  useEffect(() => {
    if (prevCompletedCount > 0 || hub.isComplete) {
      refetchRun();
    }
  }, [prevCompletedCount, hub.isComplete, refetchRun]);

  // When hub signals complete, also refetch session for status transition
  useEffect(() => {
    if (hub.isComplete) {
      refetchSession();
    }
  }, [hub.isComplete, refetchSession]);

  // ============================================
  // Compute progress stats
  // ============================================

  const stats = useMemo(() => {
    const results = runDetail?.results ?? [];
    let pass = 0, review = 0, fail = 0;
    const scores: number[] = [];
    for (const r of results) {
      scores.push(r.finalScore);
      if (r.outcome === 'Pass') pass++;
      else if (r.outcome === 'Review') review++;
      else if (r.outcome === 'Fail') fail++;
    }
    const overallScore =
      scores.length > 0
        ? Math.round(scores.reduce((a, b) => a + b, 0) / scores.length)
        : 0;
    const total = state.parsedSections.length;
    const pending = Math.max(0, total - scores.length);

    const percentComplete = hub.isComplete
      ? 100
      : hub.progress?.percentComplete
        ?? (total > 0 ? (scores.length / total) * 100 : 0);

    return {
      overallScore,
      percentComplete,
      sectionsComplete: scores.length,
      totalSections: total,
      statusCounts: { pass, review, fail, running: 0, pending },
    };
  }, [runDetail, state.parsedSections.length, hub.isComplete, hub.progress]);

  // Advance when all language runs are done ('Failed' covers validation failure)
  const canContinue =
    session?.status === 'Validated' || session?.status === 'Failed';

  const safetyVerdict = runDetail?.safetyVerdict ?? null;
  const sourceDialect = runDetail?.sourceDialect ?? null;

  // ============================================
  // Loading state — waiting for session/runs to initialise
  // ============================================

  if (!session || runEntries.length === 0) {
    return (
      <div className="flex items-center justify-center py-12">
        <Loader2 className="mr-2 h-5 w-5 animate-spin text-muted-foreground" />
        <span className="text-muted-foreground">Initializing translation…</span>
      </div>
    );
  }

  const isTranslating = session.status === 'TranslatingValidating';

  // ============================================
  // Render
  // ============================================

  return (
    <div className="space-y-6">
      <WizardSectionDivider number="5a" label="Translation Progress" firstSection />

      {/* Language tabs (multi-language) */}
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

      {/* In-progress animation */}
      {isTranslating && (
        <div className="flex flex-col items-center justify-center gap-4 rounded-lg border bg-card py-10">
          <div className="flex items-center gap-2">
            <Sparkles className="h-5 w-5 text-primary animate-pulse" />
            <Loader2 className="h-5 w-5 animate-spin text-primary" />
          </div>
          <p className="text-sm font-medium">Running translation validation…</p>
          <p className="text-xs text-muted-foreground">
            Sections will complete as each language is processed.
          </p>
        </div>
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

      {/* Subtitle processing progress (informational) */}
      {session?.inputMode === 'Video' && session?.subtitleJobId && (
        <SubtitleProgressPanel jobId={session.subtitleJobId} />
      )}

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

          <Button onClick={onNext} disabled={!canContinue}>
            Continue
            <ArrowRight className="ml-2 h-4 w-4" />
          </Button>
        </div>
      </div>
    </div>
  );
}
