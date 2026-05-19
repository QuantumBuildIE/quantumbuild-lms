"use client";

import * as React from "react";
import {
  AlertTriangle,
  CheckCircle2,
  Download,
  Mail,
  PlusCircle,
  UserCheck,
  Users,
  XCircle,
} from "lucide-react";
import { toast } from "sonner";
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
import {
  useDownloadFailedRows,
  type BulkImportOutcome,
  type BulkImportOutcomeStatus,
  type BulkImportProcessingSummary,
} from "@/lib/api/admin/use-bulk-import";
import { getApiErrorMessage } from "@/lib/utils";

interface Props {
  sessionId: string;
  processing: BulkImportProcessingSummary;
  /** Whether the job itself reached Completed (true) or Failed (false). */
  jobSucceeded: boolean;
  onImportAnother: () => void;
}

const outcomeBadge: Record<BulkImportOutcomeStatus, React.ReactNode> = {
  Created: (
    <Badge className="bg-green-100 text-green-800 border-green-200 hover:bg-green-100 dark:bg-green-950/40 dark:text-green-300 dark:border-green-900">
      Created
    </Badge>
  ),
  AlreadyExisted: (
    <Badge variant="secondary">Already Existed</Badge>
  ),
  Failed: <Badge variant="destructive">Failed</Badge>,
};

const outcomeColumns: Column<BulkImportOutcome>[] = [
  {
    key: "rowNumber",
    header: "Row",
    className: "w-16 font-medium tabular-nums",
  },
  {
    key: "status",
    header: "Outcome",
    className: "w-36",
    render: (row) => outcomeBadge[row.status],
  },
  {
    key: "failureReason",
    header: "Detail",
    render: (row) =>
      row.failureReason ? (
        <span className="text-sm text-muted-foreground">{row.failureReason}</span>
      ) : row.status === "AlreadyExisted" ? (
        <span className="text-sm text-muted-foreground">
          An employee with this email already exists in this tenant.
        </span>
      ) : null,
  },
];

export function BulkImportResultsPanel({
  sessionId,
  processing,
  jobSucceeded,
  onImportAnother,
}: Props) {
  const downloadFailed = useDownloadFailedRows();

  const handleDownload = async () => {
    try {
      await downloadFailed.mutateAsync(sessionId);
    } catch (error) {
      toast.error("Download failed", {
        description: getApiErrorMessage(error, "Could not download failed rows."),
      });
    }
  };

  // Job itself errored (not just individual row failures)
  if (!jobSucceeded) {
    return (
      <div className="space-y-6 max-w-2xl">
        <div className="rounded-md border border-destructive/40 bg-destructive/5 p-5">
          <div className="flex items-start gap-3">
            <XCircle className="h-5 w-5 text-destructive mt-0.5 shrink-0" />
            <div>
              <p className="font-medium text-destructive">Import job failed</p>
              <p className="mt-1 text-sm text-muted-foreground">
                The import job encountered an unexpected error before it could
                complete. Your uploaded file has been retained — you can try
                again or contact support if the problem persists.
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

  const { createdCount, failedCount, alreadyExistedCount, invitationEmailsSent, totalAttempted, rows } =
    processing;

  const showAlreadyExisted = alreadyExistedCount > 0;
  const showFailed = failedCount > 0;

  // Only show rows that aren't plain successes — keeps the table concise on clean imports
  const notableRows = rows.filter(
    (r) => r.status === "Failed" || r.status === "AlreadyExisted"
  );

  return (
    <div className="space-y-6 max-w-3xl">
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
                ? `Import completed with ${failedCount} failed row${failedCount !== 1 ? "s" : ""}`
                : "Import completed successfully"}
            </p>
            <p
              className={`mt-0.5 text-sm ${
                failedCount > 0
                  ? "text-yellow-700 dark:text-yellow-300"
                  : "text-green-700 dark:text-green-300"
              }`}
            >
              {createdCount} employee{createdCount !== 1 ? "s" : ""} created from {totalAttempted} attempted row
              {totalAttempted !== 1 ? "s" : ""}.
            </p>
          </div>
        </div>
      </div>

      {/* Summary stat cards */}
      <div
        className={`grid gap-4 ${showAlreadyExisted ? "grid-cols-2 sm:grid-cols-4" : "grid-cols-2 sm:grid-cols-3"}`}
      >
        <StatCard
          label="Total Rows"
          value={totalAttempted}
          icon={<Users className="h-4 w-4 text-muted-foreground" />}
        />
        <StatCard
          label="Created"
          value={createdCount}
          icon={<UserCheck className="h-4 w-4 text-green-500" />}
          valueClassName="text-green-600 dark:text-green-400"
        />
        {showAlreadyExisted && (
          <StatCard
            label="Already Existed"
            value={alreadyExistedCount}
            icon={<UserCheck className="h-4 w-4 text-muted-foreground" />}
          />
        )}
        <StatCard
          label={showFailed ? "Failed" : "Invites Sent"}
          value={showFailed ? failedCount : invitationEmailsSent}
          icon={
            showFailed ? (
              <XCircle className="h-4 w-4 text-destructive" />
            ) : (
              <Mail className="h-4 w-4 text-muted-foreground" />
            )
          }
          valueClassName={showFailed ? "text-destructive" : undefined}
        />
      </div>

      {/* Invites sent — secondary stat when failed rows are shown in the grid */}
      {showFailed && (
        <div className="rounded-md border bg-muted/40 px-4 py-2.5 flex items-center gap-2 text-sm text-muted-foreground">
          <Mail className="h-4 w-4 shrink-0" />
          <span>
            <span className="font-medium text-foreground">{invitationEmailsSent}</span>{" "}
            invitation email{invitationEmailsSent !== 1 ? "s" : ""} sent
          </span>
        </div>
      )}

      {/* Notable rows table */}
      {notableRows.length > 0 ? (
        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-base">Row Outcomes</CardTitle>
            <CardDescription>
              {notableRows.length} row{notableRows.length !== 1 ? "s" : ""} with
              issues or pre-existing records — created rows are not listed
            </CardDescription>
          </CardHeader>
          <CardContent className="p-0">
            <DataTable
              columns={outcomeColumns}
              data={notableRows}
              isLoading={false}
              emptyMessage="No issues"
              keyExtractor={(row) => String(row.rowNumber)}
            />
          </CardContent>
        </Card>
      ) : (
        <div className="rounded-md border bg-card p-6 text-center">
          <CheckCircle2 className="mx-auto h-8 w-8 text-green-500 mb-2" />
          <p className="text-sm font-medium">All rows imported successfully</p>
          <p className="text-xs text-muted-foreground mt-1">
            {createdCount} employee{createdCount !== 1 ? "s" : ""} created
          </p>
        </div>
      )}

      {/* Actions */}
      <div className="flex items-center justify-between border-t pt-4">
        <Button onClick={onImportAnother} variant="outline">
          <PlusCircle className="mr-2 h-4 w-4" />
          Import Another File
        </Button>
        {showFailed && (
          <Button
            onClick={handleDownload}
            disabled={downloadFailed.isPending}
            variant="secondary"
          >
            <Download className="mr-2 h-4 w-4" />
            {downloadFailed.isPending ? "Downloading…" : "Download Failed Rows"}
          </Button>
        )}
      </div>
    </div>
  );
}

function StatCard({
  label,
  value,
  icon,
  valueClassName,
}: {
  label: string;
  value: number;
  icon: React.ReactNode;
  valueClassName?: string;
}) {
  return (
    <Card>
      <CardContent className="pt-4 pb-4">
        <div className="flex items-center justify-between mb-1">
          <p className="text-xs text-muted-foreground">{label}</p>
          {icon}
        </div>
        <p className={`text-2xl font-semibold tabular-nums ${valueClassName ?? ""}`}>
          {value}
        </p>
      </CardContent>
    </Card>
  );
}
