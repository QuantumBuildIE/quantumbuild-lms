import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  uploadBulkImport,
  confirmBulkImport,
  getBulkImportSession,
  downloadFailedRows,
  downloadBulkImportTemplate,
  type UploadBulkImportParams,
} from "./bulk-import";

export const bulkImportSessionKey = (sessionId: string) => [
  "bulk-import-session",
  sessionId,
];

export function useUploadBulkImport() {
  return useMutation({
    mutationFn: (params: UploadBulkImportParams) => uploadBulkImport(params),
  });
}

export function useConfirmBulkImport() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (sessionId: string) => confirmBulkImport(sessionId),
    onSuccess: (_data, sessionId) => {
      queryClient.invalidateQueries({
        queryKey: bulkImportSessionKey(sessionId),
      });
    },
  });
}

/**
 * Query for session status + results. Supports polling while the job is running:
 * pass refetchIntervalMs (e.g. 3000) to poll, pass undefined to disable.
 * The query is disabled when sessionId is null.
 */
export function useBulkImportSession(
  sessionId: string | null,
  refetchIntervalMs?: number
) {
  return useQuery({
    queryKey: bulkImportSessionKey(sessionId ?? ""),
    queryFn: () => getBulkImportSession(sessionId!),
    enabled: !!sessionId,
    refetchInterval: refetchIntervalMs,
  });
}

/** Triggers a browser file-save of the failed-rows CSV for the given session. */
export function useDownloadFailedRows() {
  return useMutation({
    mutationFn: async (sessionId: string) => {
      const blob = await downloadFailedRows(sessionId);
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.download = `import-failed-rows-${sessionId.replace(/-/g, "").slice(0, 8)}.csv`;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      window.URL.revokeObjectURL(url);
    },
  });
}

/** Triggers a browser file-save of the CSV import template. */
export function useDownloadBulkImportTemplate() {
  return useMutation({
    mutationFn: async () => {
      const blob = await downloadBulkImportTemplate();
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.download = "employee-import-template.csv";
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      window.URL.revokeObjectURL(url);
    },
  });
}

export type {
  BulkImportUploadResponse,
  BulkImportValidationSummary,
  BulkImportRowSummary,
  BulkImportProcessingSummary,
  BulkImportOutcome,
  BulkImportSessionDto,
  BulkImportConfirmResponse,
  BulkImportSessionStatus,
  BulkImportRowStatus,
  BulkImportOutcomeStatus,
  UploadBulkImportParams,
} from "./bulk-import";
