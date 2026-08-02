import { apiClient } from "@/lib/api/client";
import type { ApiResponse } from "@/types/auth";

// ── String-enum types (serialised from C# enum.ToString()) ───────────────────

export type BulkSopImportSessionStatus =
  | "Uploaded"
  | "Validated"
  | "Processing"
  | "Completed"
  | "Failed"
  | "Cancelled";

export type BulkSopImportItemStatus = "Succeeded" | "Failed" | "AlreadyExisted";

// ── Response types ────────────────────────────────────────────────────────────

export interface BulkSopImportValidationSummary {
  totalPdfCount: number;
  totalUncompressedBytes: number;
  files: string[];
  ignoredEntryNames: string[];
}

/** Returned by POST /toolbox-talks/bulk-sop-import */
export interface BulkSopImportUploadResponse {
  sessionId: string;
  validation: BulkSopImportValidationSummary;
}

export interface BulkSopImportOutcome {
  itemIndex: number;
  fileName: string;
  status: BulkSopImportItemStatus;
  toolboxTalkId: string | null;
  toolboxTalkTitle: string | null;
  failureReason: string | null;
  warning: string | null;
}

export interface BulkSopImportProcessingSummary {
  totalAttempted: number;
  succeededCount: number;
  failedCount: number;
  alreadyExistedCount: number;
  items: BulkSopImportOutcome[];
}

/** Returned by GET /toolbox-talks/bulk-sop-import/{id} */
export interface BulkSopImportSessionDto {
  sessionId: string;
  status: BulkSopImportSessionStatus;
  uploadedAt: string;
  validation: BulkSopImportValidationSummary | null;
  processing: BulkSopImportProcessingSummary | null;
}

/** Returned by POST /toolbox-talks/bulk-sop-import/{id}/confirm */
export interface BulkSopImportConfirmResponse {
  jobId: string;
}

// ── Request params ────────────────────────────────────────────────────────────

export interface UploadBulkSopImportParams {
  file: File;
  /**
   * When provided, sent as the X-Tenant-Id request header.
   * Required for SuperUser callers targeting a tenant other than their default.
   */
  targetTenantId?: string;
}

// ── API functions ─────────────────────────────────────────────────────────────

/**
 * Upload a ZIP of SOP PDFs, validate it, and receive a session ID + validation summary.
 * Uses Result<T> envelope — reads response.data.data.
 */
export async function uploadBulkSopImport({
  file,
  targetTenantId,
}: UploadBulkSopImportParams): Promise<BulkSopImportUploadResponse> {
  const formData = new FormData();
  formData.append("file", file);

  // Clear the instance-default Content-Type so the browser can set the correct
  // multipart/form-data boundary automatically.
  const headers: Record<string, string | undefined> = {
    "Content-Type": undefined,
  };
  if (targetTenantId) {
    headers["X-Tenant-Id"] = targetTenantId;
  }

  const response = await apiClient.post<ApiResponse<BulkSopImportUploadResponse>>(
    "/toolbox-talks/bulk-sop-import",
    formData,
    { headers }
  );
  return response.data.data;
}

/**
 * Enqueue the processing job for a Validated session.
 * Uses Result<T> envelope — reads response.data.data.
 */
export async function confirmBulkSopImport(
  sessionId: string
): Promise<BulkSopImportConfirmResponse> {
  const response = await apiClient.post<ApiResponse<BulkSopImportConfirmResponse>>(
    `/toolbox-talks/bulk-sop-import/${sessionId}/confirm`
  );
  return response.data.data;
}

/**
 * Poll session status, validation summary, and processing results.
 * Uses Result<T> envelope — reads response.data.data.
 */
export async function getBulkSopImportSession(
  sessionId: string
): Promise<BulkSopImportSessionDto> {
  const response = await apiClient.get<ApiResponse<BulkSopImportSessionDto>>(
    `/toolbox-talks/bulk-sop-import/${sessionId}`
  );
  return response.data.data;
}
