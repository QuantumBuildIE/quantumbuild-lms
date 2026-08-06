"use client";

import * as React from "react";
import Link from "next/link";
import {
  AlertTriangle,
  CheckCircle2,
  FileText,
  PlusCircle,
  XCircle,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { DataTable, type Column } from "@/components/shared/data-table";
import type {
  BulkSopImportOutcome,
  BulkSopImportItemStatus,
  BulkSopImportProcessingSummary,
} from "@/lib/api/toolbox-talks/use-bulk-sop-import";
import { getStepUrl, getDraftsUrl } from "@/features/toolbox-talks/components/learning-wizard/lib/urlState";

interface Props {
  processing: BulkSopImportProcessingSummary;
  /** Whether the job itself reached Completed (true) or Failed (false). */
  jobSucceeded: boolean;
  onImportAnother: () => void;
}

const outcomeBadge: Record<BulkSopImportItemStatus, React.ReactNode> = {
  Succeeded: (
    <Badge className="bg-green-100 text-green-800 border-green-200 hover:bg-green-100 dark:bg-green-950/40 dark:text-green-300 dark:border-green-900">
      Succeeded
    </Badge>
  ),
  AlreadyExisted: <Badge variant="secondary">Already Existed</Badge>,
  Failed: <Badge variant="destructive">Failed</Badge>,
};

function ResultDetail({ item }: { item: BulkSopImportOutcome }) {
  if (item.status === "Failed") {
    return (
      <span className="text-sm text-muted-foreground">
        {item.failureReason ?? "Unknown error"}
      </span>
    );
  }

  // Succeeded or AlreadyExisted — both carry a ToolboxTalkId to review.
  if (!item.toolboxTalkId) {
    return null;
  }

  // No Warning means quiz generation completed (step 3); a Warning means the
  // learning stopped after parsing only (step 2).
  const reviewUrl = getStepUrl(item.toolboxTalkId, item.warning ? 2 : 3);

  return (
    <div className="space-y-1">
      <Link href={reviewUrl} className="text-sm font-medium text-primary hover:underline">
        {item.toolboxTalkTitle ?? "View draft"}
      </Link>
      {item.status === "AlreadyExisted" && (
        <p className="text-xs text-muted-foreground">
          This PDF was already imported in a previous run.
        </p>
      )}
      {item.warning && (
        <p className="text-xs text-yellow-700 dark:text-yellow-400">{item.warning}</p>
      )}
    </div>
  );
}

const outcomeColumns: Column<BulkSopImportOutcome>[] = [
  {
    key: "fileName",
    header: "File",
    className: "font-medium",
    render: (row) => (
      <span className="flex items-center gap-2">
        <FileText className="h-3.5 w-3.5 text-muted-foreground shrink-0" />
        <span className="truncate">{row.fileName}</span>
      </span>
    ),
  },
  {
    key: "status",
    header: "Outcome",
    className: "w-36",
    render: (row) => outcomeBadge[row.status],
  },
  {
    key: "result",
    header: "Result",
    render: (row) => <ResultDetail item={row} />,
  },
];

export function BulkSopImportResultsPanel({ processing, jobSucceeded, onImportAnother }: Props) {
  // Job itself errored (not just individual item failures)
  if (!jobSucceeded) {
    return (
      <div className="space-y-6 max-w-2xl">
        <div className="rounded-md border border-destructive/40 bg-destructive/5 p-5">
          <div className="flex items-start gap-3">
            <XCircle className="h-5 w-5 text-destructive mt-0.5 shrink-0" />
            <div>
              <p className="font-medium text-destructive">Import job failed</p>
              <p className="mt-1 text-sm text-muted-foreground">
                The import job encountered an unexpected error before it could complete. Your
                uploaded ZIP has been retained — you can try again or contact support if the
                problem persists.
              </p>
            </div>
          </div>
        </div>
        <div className="flex items-center justify-end border-t pt-4">
          <Button onClick={onImportAnother}>
            <PlusCircle className="mr-2 h-4 w-4" />
            Start a New Import
          </Button>
        </div>
      </div>
    );
  }

  const { totalAttempted, succeededCount, failedCount, alreadyExistedCount, items } = processing;

  const showAlreadyExisted = alreadyExistedCount > 0;
  const showFailed = failedCount > 0;

  const gridColsClass = showAlreadyExisted && showFailed
    ? "grid-cols-2 sm:grid-cols-4"
    : showAlreadyExisted || showFailed
      ? "grid-cols-2 sm:grid-cols-3"
      : "grid-cols-2 sm:grid-cols-2";

  return (
    <div className="space-y-6 max-w-4xl">
      {/* Outcome banner */}
      <div
        className={`rounded-md border px-4 py-3 ${
          failedCount > 0
            ? "border-yellow-200 bg-yellow-50 dark:border-yellow-900/50 dark:bg-yellow-950/20"
            : "border-green-200 bg-green-50 dark:border-green-900/50 dark:bg-green-950/20"
        }`}
      >
        <div className="flex items-start gap-3">
          {failedCount > 0 ? (
            <AlertTriangle className="h-5 w-5 text-yellow-600 dark:text-yellow-400 mt-0.5 shrink-0" />
          ) : (
            <CheckCircle2 className="h-5 w-5 text-green-600 dark:text-green-400 mt-0.5 shrink-0" />
          )}
          <div>
            <p
              className={`font-medium ${
                failedCount > 0
                  ? "text-yellow-800 dark:text-yellow-200"
                  : "text-green-800 dark:text-green-200"
              }`}
            >
              {failedCount > 0
                ? `Import completed with ${failedCount} failed PDF${failedCount !== 1 ? "s" : ""}`
                : "Import completed successfully"}
            </p>
            <p
              className={`mt-0.5 text-sm ${
                failedCount > 0
                  ? "text-yellow-700 dark:text-yellow-300"
                  : "text-green-700 dark:text-green-300"
              }`}
            >
              {succeededCount} Draft learning{succeededCount !== 1 ? "s" : ""} created from{" "}
              {totalAttempted} PDF{totalAttempted !== 1 ? "s" : ""} attempted.
            </p>
          </div>
        </div>
      </div>

      {/* Summary stat cards */}
      <div className={`grid gap-4 ${gridColsClass}`}>
        <StatCard label="Total PDFs" value={totalAttempted} />
        <StatCard
          label="Succeeded"
          value={succeededCount}
          valueClassName="text-green-600 dark:text-green-400"
        />
        {showAlreadyExisted && (
          <StatCard label="Already Existed" value={alreadyExistedCount} />
        )}
        {showFailed && (
          <StatCard label="Failed" value={failedCount} valueClassName="text-destructive" />
        )}
      </div>

      {/* Per-item report */}
      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Import Report</CardTitle>
          <CardDescription>
            Every PDF in the ZIP, its outcome, and a link to review each Draft learning
          </CardDescription>
        </CardHeader>
        <CardContent className="p-0">
          <DataTable
            columns={outcomeColumns}
            data={items}
            isLoading={false}
            emptyMessage="No items processed"
            keyExtractor={(row) => String(row.itemIndex)}
          />
        </CardContent>
      </Card>

      {/* Actions */}
      <div className="flex items-center justify-between border-t pt-4">
        <Button onClick={onImportAnother} variant="outline">
          <PlusCircle className="mr-2 h-4 w-4" />
          Import Another ZIP
        </Button>
        {succeededCount > 0 && (
          <Button asChild variant="secondary">
            <Link href={getDraftsUrl()}>Review Draft Learnings</Link>
          </Button>
        )}
      </div>
    </div>
  );
}

function StatCard({
  label,
  value,
  valueClassName,
}: {
  label: string;
  value: number;
  valueClassName?: string;
}) {
  return (
    <Card>
      <CardContent className="pt-4 pb-4">
        <p className="text-xs text-muted-foreground mb-1">{label}</p>
        <p className={`text-2xl font-semibold tabular-nums ${valueClassName ?? ""}`}>{value}</p>
      </CardContent>
    </Card>
  );
}
