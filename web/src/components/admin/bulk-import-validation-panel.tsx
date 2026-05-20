"use client";

import * as React from "react";
import { AlertTriangle, CheckCircle2, Users, XCircle } from "lucide-react";
import { toast } from "sonner";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { DataTable, type Column } from "@/components/shared/data-table";
import {
  useConfirmBulkImport,
  type BulkImportRowSummary,
  type BulkImportRowStatus,
  type BulkImportValidationSummary,
} from "@/lib/api/admin/use-bulk-import";
import { getApiErrorMessage } from "@/lib/utils";

interface Props {
  sessionId: string;
  validation: BulkImportValidationSummary;
  onConfirmSuccess: () => void;
  onCancel: () => void;
}

const statusBadge: Record<BulkImportRowStatus, React.ReactNode> = {
  Valid: <Badge variant="default">Valid</Badge>,
  Warning: <Badge variant="secondary">Warning</Badge>,
  Failed: <Badge variant="destructive">Failed</Badge>,
};

const issueColumns: Column<BulkImportRowSummary>[] = [
  {
    key: "rowNumber",
    header: "Row",
    className: "w-16 font-medium",
  },
  {
    key: "status",
    header: "Status",
    className: "w-28",
    render: (row) => statusBadge[row.status],
  },
  {
    key: "messages",
    header: "Issues",
    render: (row) => (
      <ul className="space-y-0.5">
        {row.messages.map((msg, i) => (
          <li key={i} className="text-sm text-muted-foreground">
            {msg}
          </li>
        ))}
      </ul>
    ),
  },
];

export function BulkImportValidationPanel({
  sessionId,
  validation,
  onConfirmSuccess,
  onCancel,
}: Props) {
  const confirmMutation = useConfirmBulkImport();

  const handleConfirm = async () => {
    try {
      await confirmMutation.mutateAsync(sessionId);
      onConfirmSuccess();
    } catch (error) {
      toast.error("Failed to start import", {
        description: getApiErrorMessage(error, "An unexpected error occurred."),
      });
    }
  };

  const importableRows = validation.validRows + validation.warningRows;

  return (
    <div className="space-y-6 max-w-3xl">
      {/* Summary stat cards */}
      <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
        <StatCard
          label="Total Rows"
          value={validation.totalRows}
          icon={<Users className="h-4 w-4 text-muted-foreground" />}
        />
        <StatCard
          label="Valid"
          value={validation.validRows}
          icon={<CheckCircle2 className="h-4 w-4 text-green-500" />}
          valueClassName="text-green-600 dark:text-green-400"
        />
        <StatCard
          label="Warnings"
          value={validation.warningRows}
          icon={
            <AlertTriangle
              className={`h-4 w-4 ${validation.warningRows > 0 ? "text-yellow-500" : "text-muted-foreground"}`}
            />
          }
          valueClassName={
            validation.warningRows > 0
              ? "text-yellow-600 dark:text-yellow-400"
              : undefined
          }
        />
        <StatCard
          label="Errors"
          value={validation.failedRows}
          icon={
            <XCircle
              className={`h-4 w-4 ${validation.failedRows > 0 ? "text-destructive" : "text-muted-foreground"}`}
            />
          }
          valueClassName={
            validation.failedRows > 0 ? "text-destructive" : undefined
          }
        />
      </div>

      {/* Failed rows notice */}
      {validation.failedRows > 0 && (
        <div className="rounded-md border border-yellow-200 bg-yellow-50 px-4 py-3 text-sm dark:border-yellow-900/50 dark:bg-yellow-950/20">
          <p className="font-medium text-yellow-800 dark:text-yellow-200">
            {validation.failedRows} row{validation.failedRows !== 1 ? "s" : ""} will be skipped
          </p>
          <p className="mt-0.5 text-yellow-700 dark:text-yellow-300">
            The remaining {importableRows} row{importableRows !== 1 ? "s" : ""} (valid and warnings)
            will still be imported. You can correct the failed rows and re-import afterward.
          </p>
        </div>
      )}

      {/* Per-row issues table */}
      {validation.rowsWithIssues.length > 0 ? (
        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-base">Row Issues</CardTitle>
            <CardDescription>
              {validation.rowsWithIssues.length} row
              {validation.rowsWithIssues.length !== 1 ? "s" : ""} with warnings or errors — clean
              rows are not listed
            </CardDescription>
          </CardHeader>
          <CardContent className="p-0">
            <DataTable
              columns={issueColumns}
              data={validation.rowsWithIssues}
              isLoading={false}
              emptyMessage="No issues found"
              keyExtractor={(row) => String(row.rowNumber)}
            />
          </CardContent>
        </Card>
      ) : (
        <div className="rounded-md border bg-card p-6 text-center">
          <CheckCircle2 className="mx-auto h-8 w-8 text-green-500 mb-2" />
          <p className="text-sm font-medium">All rows passed validation</p>
          <p className="text-xs text-muted-foreground mt-1">
            {validation.totalRows} row{validation.totalRows !== 1 ? "s" : ""} ready to import
          </p>
        </div>
      )}

      {/* Actions */}
      <div className="flex items-center justify-between border-t pt-4">
        <Button
          variant="outline"
          onClick={onCancel}
          disabled={confirmMutation.isPending}
        >
          Cancel
        </Button>
        <Button onClick={handleConfirm} disabled={confirmMutation.isPending}>
          {confirmMutation.isPending
            ? "Starting import..."
            : importableRows > 0
              ? `Confirm & Import ${importableRows} row${importableRows !== 1 ? "s" : ""}`
              : "Confirm & Import"}
        </Button>
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
        <p className={`text-2xl font-semibold tabular-nums ${valueClassName ?? ""}`}>{value}</p>
      </CardContent>
    </Card>
  );
}
