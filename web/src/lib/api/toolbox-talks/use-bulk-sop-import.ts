import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  uploadBulkSopImport,
  confirmBulkSopImport,
  getBulkSopImportSession,
  type UploadBulkSopImportParams,
} from "./bulk-sop-import";

export const bulkSopImportSessionKey = (sessionId: string) => [
  "bulk-sop-import-session",
  sessionId,
];

export function useUploadBulkSopImport() {
  return useMutation({
    mutationFn: (params: UploadBulkSopImportParams) => uploadBulkSopImport(params),
  });
}

export function useConfirmBulkSopImport() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (sessionId: string) => confirmBulkSopImport(sessionId),
    onSuccess: (_data, sessionId) => {
      queryClient.invalidateQueries({
        queryKey: bulkSopImportSessionKey(sessionId),
      });
    },
  });
}

/**
 * Query for session status + results. Supports polling while the job is running:
 * pass refetchIntervalMs (e.g. 3000) to poll, pass undefined to disable.
 * The query is disabled when sessionId is null.
 */
export function useBulkSopImportSession(
  sessionId: string | null,
  refetchIntervalMs?: number
) {
  return useQuery({
    queryKey: bulkSopImportSessionKey(sessionId ?? ""),
    queryFn: () => getBulkSopImportSession(sessionId!),
    enabled: !!sessionId,
    refetchInterval: refetchIntervalMs,
  });
}

export type {
  BulkSopImportUploadResponse,
  BulkSopImportValidationSummary,
  BulkSopImportProcessingSummary,
  BulkSopImportOutcome,
  BulkSopImportSessionDto,
  BulkSopImportConfirmResponse,
  BulkSopImportSessionStatus,
  BulkSopImportItemStatus,
  UploadBulkSopImportParams,
} from "./bulk-sop-import";
