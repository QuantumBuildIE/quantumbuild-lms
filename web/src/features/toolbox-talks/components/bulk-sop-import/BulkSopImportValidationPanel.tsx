"use client";

import * as React from "react";
import { AlertTriangle, FileText, HardDrive } from "lucide-react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import {
  useConfirmBulkSopImport,
  type BulkSopImportValidationSummary,
} from "@/lib/api/toolbox-talks/use-bulk-sop-import";
import { getApiErrorMessage } from "@/lib/utils";

interface Props {
  sessionId: string;
  validation: BulkSopImportValidationSummary;
  onConfirmSuccess: () => void;
  onCancel: () => void;
}

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  const units = ["KB", "MB", "GB"];
  let value = bytes / 1024;
  let unitIndex = 0;
  while (value >= 1024 && unitIndex < units.length - 1) {
    value /= 1024;
    unitIndex++;
  }
  return `${value.toFixed(1)} ${units[unitIndex]}`;
}

export function BulkSopImportValidationPanel({
  sessionId,
  validation,
  onConfirmSuccess,
  onCancel,
}: Props) {
  const confirmMutation = useConfirmBulkSopImport();

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

  const { totalPdfCount, totalUncompressedBytes, files, ignoredEntryNames } = validation;

  return (
    <div className="space-y-6 max-w-3xl">
      {/* Summary stat cards */}
      <div className="grid grid-cols-2 gap-4 sm:grid-cols-3">
        <StatCard
          label="PDFs Found"
          value={totalPdfCount}
          icon={<FileText className="h-4 w-4 text-muted-foreground" />}
        />
        <StatCard
          label="Total Size"
          value={formatBytes(totalUncompressedBytes)}
          icon={<HardDrive className="h-4 w-4 text-muted-foreground" />}
        />
        <StatCard
          label="Ignored"
          value={ignoredEntryNames.length}
          icon={
            <AlertTriangle
              className={`h-4 w-4 ${ignoredEntryNames.length > 0 ? "text-yellow-500" : "text-muted-foreground"}`}
            />
          }
          valueClassName={
            ignoredEntryNames.length > 0
              ? "text-yellow-600 dark:text-yellow-400"
              : undefined
          }
        />
      </div>

      {/* Ignored entries notice */}
      {ignoredEntryNames.length > 0 && (
        <div className="rounded-md border border-yellow-200 bg-yellow-50 px-4 py-3 text-sm dark:border-yellow-900/50 dark:bg-yellow-950/20">
          <p className="font-medium text-yellow-800 dark:text-yellow-200">
            {ignoredEntryNames.length} entr{ignoredEntryNames.length !== 1 ? "ies" : "y"} in the ZIP
            will be ignored
          </p>
          <p className="mt-0.5 text-yellow-700 dark:text-yellow-300">
            These aren't PDF files, so they won't produce a learning. The other{" "}
            {totalPdfCount} PDF{totalPdfCount !== 1 ? "s" : ""} will still be imported.
          </p>
        </div>
      )}

      {/* Ignored entries list */}
      {ignoredEntryNames.length > 0 && (
        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-base">Ignored Entries</CardTitle>
            <CardDescription>Not PDF files — skipped without blocking the import</CardDescription>
          </CardHeader>
          <CardContent>
            <ul className="space-y-1.5">
              {ignoredEntryNames.map((name, i) => (
                <li key={i} className="text-sm text-muted-foreground">
                  {name}
                </li>
              ))}
            </ul>
          </CardContent>
        </Card>
      )}

      {/* PDFs to import */}
      {totalPdfCount > 0 ? (
        <Card>
          <CardHeader className="pb-3">
            <CardTitle className="text-base">PDFs to Import</CardTitle>
            <CardDescription>
              Each PDF becomes a Draft learning
            </CardDescription>
          </CardHeader>
          <CardContent>
            <ul className="space-y-1.5">
              {files.map((name, i) => (
                <li key={i} className="text-sm flex items-center gap-2">
                  <FileText className="h-3.5 w-3.5 text-muted-foreground shrink-0" />
                  <span className="truncate">{name}</span>
                </li>
              ))}
            </ul>
          </CardContent>
        </Card>
      ) : (
        <div className="rounded-md border bg-card p-6 text-center">
          <AlertTriangle className="mx-auto h-8 w-8 text-yellow-500 mb-2" />
          <p className="text-sm font-medium">No PDFs found in this ZIP</p>
          <p className="text-xs text-muted-foreground mt-1">
            Go back and upload a ZIP containing at least one PDF file
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
        <Button
          onClick={handleConfirm}
          disabled={confirmMutation.isPending || totalPdfCount === 0}
        >
          {confirmMutation.isPending
            ? "Starting import..."
            : `Confirm & Import ${totalPdfCount} PDF${totalPdfCount !== 1 ? "s" : ""}`}
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
  value: number | string;
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
