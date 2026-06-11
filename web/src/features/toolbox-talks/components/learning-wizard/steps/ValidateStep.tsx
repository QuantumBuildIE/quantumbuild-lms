'use client';

import { useCallback } from 'react';
import Link from 'next/link';
import { CheckCircle2, Loader2, XCircle, AlertCircle, Clock, ExternalLink } from 'lucide-react';
import { useQueryClient } from '@tanstack/react-query';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { LoadingState } from '../components/LoadingState';
import { WorkflowSubscriber } from '../hooks/WorkflowSubscriber';
import { useTalk } from '../hooks/useTalk';
import { useWorkflowSubscription } from '../hooks/useWorkflowSubscription';
import { useValidationRuns, contentCreationKeys } from '@/lib/api/toolbox-talks/use-content-creation';
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

function parseLanguageCodes(json: string | null): string[] {
  if (!json) return [];
  try {
    const parsed = JSON.parse(json);
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
}

function RunStatusIcon({ run }: { run: ValidationRunSummary | undefined }) {
  if (!run) return <Clock className="h-5 w-5 text-muted-foreground shrink-0" aria-hidden="true" />;
  if (run.status === 'Running' || run.status === 'Pending') {
    return <Loader2 className="h-5 w-5 text-blue-500 animate-spin shrink-0" aria-hidden="true" />;
  }
  if (run.status === 'Completed') {
    if (run.overallOutcome === 'Pass') {
      return <CheckCircle2 className="h-5 w-5 text-green-600 shrink-0" aria-hidden="true" />;
    }
    if (run.overallOutcome === 'Review') {
      return <AlertCircle className="h-5 w-5 text-amber-500 shrink-0" aria-hidden="true" />;
    }
    return <XCircle className="h-5 w-5 text-red-500 shrink-0" aria-hidden="true" />;
  }
  if (run.status === 'Failed') {
    return <XCircle className="h-5 w-5 text-red-500 shrink-0" aria-hidden="true" />;
  }
  return <Clock className="h-5 w-5 text-muted-foreground shrink-0" aria-hidden="true" />;
}

function runBadgeVariant(run: ValidationRunSummary): 'default' | 'secondary' | 'destructive' | 'outline' {
  if (run.status === 'Running' || run.status === 'Pending') return 'secondary';
  if (run.overallOutcome === 'Pass') return 'default';
  if (run.overallOutcome === 'Review') return 'outline';
  return 'destructive';
}

function runLabel(run: ValidationRunSummary): string {
  if (run.status === 'Pending') return 'Pending';
  if (run.status === 'Running') return 'Running…';
  if (run.status === 'Failed') return 'Failed';
  if (run.status === 'Cancelled') return 'Cancelled';
  return run.overallOutcome; // Pass / Review / Fail
}

export interface ValidateStepProps {
  talkId: string;
}

export function ValidateStep({ talkId }: ValidateStepProps) {
  const queryClient = useQueryClient();
  const { talk, isLoading: talkLoading } = useTalk(talkId);
  const {
    data: workflowStates,
    activeRunIds,
    onValidationComplete,
    onSectionCompleted,
  } = useWorkflowSubscription(talkId);
  const { data: validationRuns, isLoading: runsLoading } = useValidationRuns(talkId);

  // On validation complete: update workflow state + refresh validation run results
  const handleComplete = useCallback(
    (runId: string) => {
      onValidationComplete(runId);
      queryClient.invalidateQueries({ queryKey: contentCreationKeys.validationRuns(talkId) });
    },
    [onValidationComplete, queryClient, talkId]
  );

  const languages = parseLanguageCodes(talk?.targetLanguageCodes ?? null);
  const isLoading = talkLoading || runsLoading;

  if (isLoading) return <LoadingState label="Loading validation status…" />;

  if (languages.length === 0) {
    return (
      <div className="rounded-lg border border-dashed p-8 text-center text-muted-foreground">
        <p className="text-sm font-medium">No target languages configured</p>
      </div>
    );
  }

  // Latest run per language (validation runs are ordered newest-first from the API)
  const latestRunByCode: Record<string, ValidationRunSummary> = {};
  for (const run of validationRuns ?? []) {
    if (!latestRunByCode[run.languageCode]) {
      latestRunByCode[run.languageCode] = run;
    }
  }

  const stateByCode = Object.fromEntries(
    (workflowStates ?? []).map((s) => [s.languageCode, s])
  );

  return (
    <>
      {/* One SignalR subscriber per actively validating run — invalidates both
          workflow-state and validation-runs queries on completion */}
      {activeRunIds.map((runId) => (
        <WorkflowSubscriber
          key={runId}
          runId={runId}
          onComplete={handleComplete}
          onSectionCompleted={onSectionCompleted}
        />
      ))}

      <div className="space-y-6">
        <div>
          <h2 className="text-base font-semibold">Validate</h2>
          <p className="text-sm text-muted-foreground mt-1">
            Review back-translation validation results for each language. A &quot;Pass&quot; result means
            the translation is accurate. &quot;Review&quot; means it passed but needs human review.
            You can proceed even if results are pending — validation runs in the background.
          </p>
        </div>

        <div className="space-y-3" role="list" aria-label="Validation results by language">
          {languages.map((code) => {
            const run = latestRunByCode[code];
            const wfState = stateByCode[code];

            return (
              <div
                key={code}
                className="flex items-center justify-between gap-4 rounded-lg border p-4"
                role="listitem"
              >
                <div className="flex items-center gap-3 min-w-0">
                  <RunStatusIcon run={run} />
                  <div className="min-w-0">
                    <p className="text-sm font-medium truncate">
                      {LANG_NAMES[code] ?? code.toUpperCase()}
                    </p>
                    <p className="text-xs text-muted-foreground">
                      {run
                        ? `${run.passedSections} / ${run.totalSections} sections passed`
                        : wfState?.state === 'Translating'
                        ? 'Translation in progress…'
                        : 'Awaiting translation'}
                    </p>
                  </div>
                </div>

                <div className="flex items-center gap-3 shrink-0">
                  {run && (
                    <Badge variant={runBadgeVariant(run)}>{runLabel(run)}</Badge>
                  )}
                  {run && (
                    <Button asChild size="sm" variant="outline">
                      <Link
                        href={`/admin/toolbox-talks/talks/${talkId}/validation/${run.id}`}
                        aria-label={`View validation details for ${LANG_NAMES[code] ?? code}`}
                      >
                        Details
                        <ExternalLink className="h-3.5 w-3.5 ml-1.5" aria-hidden="true" />
                      </Link>
                    </Button>
                  )}
                </div>
              </div>
            );
          })}
        </div>
      </div>
    </>
  );
}
