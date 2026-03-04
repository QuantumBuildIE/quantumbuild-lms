'use client';

import { useMemo } from 'react';
import { format } from 'date-fns';
import { DownloadIcon, Loader2, ChevronLeft } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { ValidationProgressPanel } from './create-wizard/steps/validate/ValidationProgressPanel';
import { ValidationSectionCard } from './create-wizard/steps/validate/ValidationSectionCard';
import {
  useValidationRun,
  useDownloadValidationReport,
} from '@/lib/api/toolbox-talks/use-content-creation';
import { toast } from 'sonner';
import type { ValidationRunDetail } from '@/types/content-creation';

// ============================================
// Component
// ============================================

interface ValidationRunDetailViewProps {
  talkId: string;
  runId: string;
  onBack: () => void;
}

export function ValidationRunDetailView({
  talkId,
  runId,
  onBack,
}: ValidationRunDetailViewProps) {
  const { data: run, isLoading, error } = useValidationRun(talkId, runId);
  const downloadMutation = useDownloadValidationReport();

  const statusCounts = useMemo(() => {
    if (!run) return { pass: 0, review: 0, fail: 0, running: 0, pending: 0 };
    return {
      pass: run.passedSections,
      review: run.reviewSections,
      fail: run.failedSections,
      running: 0,
      pending: 0,
    };
  }, [run]);

  if (isLoading) {
    return (
      <div className="space-y-6">
        <Skeleton className="h-8 w-48" />
        <Skeleton className="h-24 w-full" />
        <Skeleton className="h-64 w-full" />
      </div>
    );
  }

  if (error || !run) {
    return (
      <div className="space-y-4">
        <Button variant="ghost" onClick={onBack}>
          <ChevronLeft className="mr-2 h-4 w-4" />
          Back
        </Button>
        <div className="rounded-lg border border-destructive/50 bg-destructive/10 p-4">
          <p className="text-destructive">
            {error instanceof Error
              ? error.message
              : 'Validation run not found'}
          </p>
        </div>
      </div>
    );
  }

  const completedDate = run.completedAt
    ? format(new Date(run.completedAt), 'dd MMM yyyy HH:mm')
    : run.startedAt
      ? format(new Date(run.startedAt), 'dd MMM yyyy HH:mm')
      : format(new Date(run.createdAt), 'dd MMM yyyy HH:mm');

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Button variant="ghost" size="icon" onClick={onBack}>
            <ChevronLeft className="h-4 w-4" />
          </Button>
          <div>
            <h2 className="text-xl font-bold">
              Validation Run — {run.languageCode.toUpperCase()}
            </h2>
            <p className="text-sm text-muted-foreground">{completedDate}</p>
          </div>
        </div>
        {run.auditReportUrl && (
          <Button
            variant="outline"
            onClick={() =>
              downloadMutation.mutate(
                { talkId, runId },
                { onError: () => toast.error('Failed to download report') }
              )
            }
            disabled={downloadMutation.isPending}
          >
            {downloadMutation.isPending ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <DownloadIcon className="mr-2 h-4 w-4" />
            )}
            Download Report
          </Button>
        )}
      </div>

      {/* Metadata card */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Run Details</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <div>
              <label className="text-xs font-medium text-muted-foreground uppercase">
                Language
              </label>
              <p className="mt-1">
                <Badge variant="outline" className="font-mono">
                  {run.languageCode.toUpperCase()}
                </Badge>
              </p>
            </div>
            <div>
              <label className="text-xs font-medium text-muted-foreground uppercase">
                Source Language
              </label>
              <p className="mt-1 text-sm">{run.sourceLanguage}</p>
            </div>
            {run.sourceDialect && (
              <div>
                <label className="text-xs font-medium text-muted-foreground uppercase">
                  Source Dialect
                </label>
                <p className="mt-1 text-sm">{run.sourceDialect}</p>
              </div>
            )}
            <div>
              <label className="text-xs font-medium text-muted-foreground uppercase">
                Pass Threshold
              </label>
              <p className="mt-1 text-sm">{run.passThreshold}%</p>
            </div>
            {run.sectorKey && (
              <div>
                <label className="text-xs font-medium text-muted-foreground uppercase">
                  Sector
                </label>
                <p className="mt-1 text-sm">{run.sectorKey}</p>
              </div>
            )}
            {run.reviewerName && (
              <div>
                <label className="text-xs font-medium text-muted-foreground uppercase">
                  Reviewer
                </label>
                <p className="mt-1 text-sm">{run.reviewerName}</p>
              </div>
            )}
            {run.reviewerOrg && (
              <div>
                <label className="text-xs font-medium text-muted-foreground uppercase">
                  Organisation
                </label>
                <p className="mt-1 text-sm">{run.reviewerOrg}</p>
              </div>
            )}
            {run.documentRef && (
              <div>
                <label className="text-xs font-medium text-muted-foreground uppercase">
                  Document Ref
                </label>
                <p className="mt-1 text-sm">{run.documentRef}</p>
              </div>
            )}
            {run.clientName && (
              <div>
                <label className="text-xs font-medium text-muted-foreground uppercase">
                  Client
                </label>
                <p className="mt-1 text-sm">{run.clientName}</p>
              </div>
            )}
            {run.auditPurpose && (
              <div className="sm:col-span-2">
                <label className="text-xs font-medium text-muted-foreground uppercase">
                  Audit Purpose
                </label>
                <p className="mt-1 text-sm">{run.auditPurpose}</p>
              </div>
            )}
          </div>
        </CardContent>
      </Card>

      {/* Progress panel (read-only) */}
      <ValidationProgressPanel
        overallScore={Math.round(run.overallScore)}
        percentComplete={100}
        sectionsComplete={run.totalSections}
        totalSections={run.totalSections}
        statusCounts={statusCounts}
        safetyVerdict={run.safetyVerdict}
        sourceDialect={run.sourceDialect}
        progressMessage={`${run.totalSections} / ${run.totalSections} sections`}
        isConnected={false}
      />

      {/* Section results */}
      <div className="space-y-3">
        {run.results.map((result) => (
          <ValidationSectionCard
            key={result.id}
            sectionIndex={result.sectionIndex}
            sectionTitle={result.sectionTitle}
            result={result}
            isRunning={false}
            languageCode={run.languageCode}
            passThreshold={run.passThreshold}
            onAccept={() => {}}
            onReject={() => {}}
            onEdit={() => {}}
            onRetry={() => {}}
            isDecisionPending={false}
            readOnly
          />
        ))}
      </div>
    </div>
  );
}
