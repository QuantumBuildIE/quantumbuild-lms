'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { format } from 'date-fns';
import {
  EyeIcon,
  DownloadIcon,
  TrashIcon,
  Loader2,
  ShieldCheck,
  ShieldAlert,
  ShieldX,
  FileSearch,
  FileUp,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { DeleteConfirmationDialog } from '@/components/shared/delete-confirmation-dialog';
import {
  useValidationRuns,
  useCourseValidationRuns,
  useDeleteValidationRun,
  useDownloadValidationReport,
  useGenerateValidationReport,
} from '@/lib/api/toolbox-talks/use-content-creation';
import { cn } from '@/lib/utils';
import { toast } from 'sonner';
import type { ValidationOutcome, ValidationRunSummary } from '@/types/content-creation';

// ============================================
// Helpers
// ============================================

const outcomePillColor: Record<ValidationOutcome, string> = {
  Pass: 'bg-green-100 text-green-800',
  Review: 'bg-amber-100 text-amber-800',
  Fail: 'bg-red-100 text-red-800',
};

function SafetyIcon({ verdict }: { verdict: ValidationOutcome | null }) {
  if (!verdict) return <span className="text-muted-foreground">—</span>;
  if (verdict === 'Pass')
    return <ShieldCheck className="h-4 w-4 text-green-600" />;
  if (verdict === 'Review')
    return <ShieldAlert className="h-4 w-4 text-amber-600" />;
  return <ShieldX className="h-4 w-4 text-red-600" />;
}

// ============================================
// Component
// ============================================

interface ValidationHistoryTabProps {
  talkId?: string;
  courseId?: string;
  basePath?: string;
}

export function ValidationHistoryTab({
  talkId,
  courseId,
  basePath = '/admin/toolbox-talks/talks',
}: ValidationHistoryTabProps) {
  const router = useRouter();
  const [deleteRunId, setDeleteRunId] = useState<string | null>(null);

  const talkRunsQuery = useValidationRuns(talkId ?? null);
  const courseRunsQuery = useCourseValidationRuns(courseId ?? null);
  const activeQuery = courseId ? courseRunsQuery : talkRunsQuery;
  const { data: runs, isLoading } = activeQuery;

  const deleteMutation = useDeleteValidationRun();
  const downloadMutation = useDownloadValidationReport();
  const generateReportMutation = useGenerateValidationReport();

  const handleDelete = async () => {
    if (!deleteRunId || !talkId) return;
    try {
      await deleteMutation.mutateAsync({ talkId, runId: deleteRunId });
      toast.success('Validation run deleted');
      setDeleteRunId(null);
    } catch {
      toast.error('Failed to delete validation run');
    }
  };

  const handleDownload = (run: ValidationRunSummary) => {
    if (!talkId) return;
    downloadMutation.mutate(
      { talkId, runId: run.id },
      {
        onError: () => toast.error('Failed to download report'),
      }
    );
  };

  const handleGenerateReport = (run: ValidationRunSummary) => {
    if (!talkId) return;
    generateReportMutation.mutate(
      { talkId, runId: run.id },
      {
        onSuccess: () =>
          toast.success('Report generation started — refresh in a moment'),
        onError: () => toast.error('Failed to start report generation'),
      }
    );
  };

  if (isLoading) {
    return (
      <div className="space-y-3">
        {[...Array(3)].map((_, i) => (
          <Skeleton key={i} className="h-12 w-full" />
        ))}
      </div>
    );
  }

  if (!runs || runs.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center py-12 text-center">
        <FileSearch className="h-12 w-12 text-muted-foreground/50 mb-3" />
        <p className="text-muted-foreground">
          No validation runs yet. Validation runs are created during the content creation workflow.
        </p>
      </div>
    );
  }

  return (
    <>
      <div className="rounded-md border">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Date</TableHead>
              <TableHead>Language</TableHead>
              <TableHead className="text-center">Score</TableHead>
              <TableHead>Outcome</TableHead>
              <TableHead className="text-center">Safety</TableHead>
              <TableHead>Reviewer</TableHead>
              <TableHead className="text-right">Actions</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {runs.map((run) => (
              <TableRow key={run.id}>
                <TableCell className="whitespace-nowrap text-sm">
                  {run.completedAt
                    ? format(new Date(run.completedAt), 'dd MMM yyyy HH:mm')
                    : run.startedAt
                      ? format(new Date(run.startedAt), 'dd MMM yyyy HH:mm')
                      : format(new Date(run.createdAt), 'dd MMM yyyy HH:mm')}
                </TableCell>
                <TableCell>
                  <Badge variant="outline" className="font-mono text-xs">
                    {run.languageCode.toUpperCase()}
                  </Badge>
                </TableCell>
                <TableCell className="text-center">
                  <span
                    className={cn(
                      'tabular-nums font-semibold',
                      run.overallScore >= 75
                        ? 'text-green-700'
                        : run.overallScore >= 60
                          ? 'text-amber-700'
                          : 'text-red-700'
                    )}
                  >
                    {Math.round(run.overallScore)}
                  </span>
                </TableCell>
                <TableCell>
                  <Badge className={cn('text-xs', outcomePillColor[run.overallOutcome])}>
                    {run.overallOutcome}
                  </Badge>
                </TableCell>
                <TableCell className="text-center">
                  <SafetyIcon verdict={run.safetyVerdict} />
                </TableCell>
                <TableCell className="text-sm text-muted-foreground">
                  {/* Reviewer name is on the run detail, not summary — show section stats instead */}
                  <span className="text-xs">
                    {run.passedSections}P / {run.reviewSections}R / {run.failedSections}F
                  </span>
                </TableCell>
                <TableCell className="text-right">
                  <div className="flex items-center justify-end gap-1">
                    <Button
                      variant="ghost"
                      size="icon"
                      className="h-8 w-8"
                      title="View run details"
                      onClick={() => {
                        const parentId = talkId ?? courseId ?? '';
                        router.push(`${basePath}/${parentId}/validation/${run.id}`);
                      }}
                    >
                      <EyeIcon className="h-4 w-4" />
                    </Button>
                    {run.auditReportUrl ? (
                      <Button
                        variant="ghost"
                        size="icon"
                        className="h-8 w-8"
                        title="Download report"
                        onClick={() => handleDownload(run)}
                        disabled={downloadMutation.isPending}
                      >
                        {downloadMutation.isPending ? (
                          <Loader2 className="h-4 w-4 animate-spin" />
                        ) : (
                          <DownloadIcon className="h-4 w-4" />
                        )}
                      </Button>
                    ) : (
                      <Button
                        variant="ghost"
                        size="icon"
                        className="h-8 w-8"
                        title="Generate report"
                        onClick={() => handleGenerateReport(run)}
                        disabled={generateReportMutation.isPending}
                      >
                        {generateReportMutation.isPending ? (
                          <Loader2 className="h-4 w-4 animate-spin" />
                        ) : (
                          <FileUp className="h-4 w-4" />
                        )}
                      </Button>
                    )}
                    <Button
                      variant="ghost"
                      size="icon"
                      className="h-8 w-8 text-destructive hover:text-destructive"
                      title="Delete run"
                      onClick={() => setDeleteRunId(run.id)}
                    >
                      <TrashIcon className="h-4 w-4" />
                    </Button>
                  </div>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>

      <DeleteConfirmationDialog
        open={!!deleteRunId}
        onOpenChange={(open) => !open && setDeleteRunId(null)}
        title="Delete Validation Run"
        description="Are you sure you want to delete this validation run? This action cannot be undone."
        onConfirm={handleDelete}
        isLoading={deleteMutation.isPending}
      />
    </>
  );
}
