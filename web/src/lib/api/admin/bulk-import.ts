import { apiClient } from "@/lib/api/client";
import type { ApiResponse } from "@/types/auth";

// ── String-enum types (serialised from C# enum.ToString()) ───────────────────

export type BulkImportSessionStatus =
  | "Uploaded"
  | "Validated"
  | "Processing"
  | "Completed"
  | "Failed"
  | "Cancelled";

export type BulkImportRowStatus = "Valid" | "Warning" | "Failed";

export type BulkImportOutcomeStatus = "Created" | "Failed" | "AlreadyExisted";

// ── Response types ────────────────────────────────────────────────────────────

export interface BulkImportRowSummary {
  rowNumber: number;
  status: BulkImportRowStatus;
  messages: string[];
}

export interface BulkImportValidationSummary {
  totalRows: number;
  validRows: number;
  warningRows: number;
  failedRows: number;
  /** Only rows that have at least one message (warnings or failures). */
  rowsWithIssues: BulkImportRowSummary[];
}

/** Returned by POST /employees/bulk-import */
export interface BulkImportUploadResponse {
  sessionId: string;
  validation: BulkImportValidationSummary;
}

export interface BulkImportOutcome {
  rowNumber: number;
  status: BulkImportOutcomeStatus;
  employeeId: string | null;
  linkedUserId: string | null;
  invitationEmailSent: boolean;
  failureReason: string | null;
}

export interface BulkImportProcessingSummary {
  totalAttempted: number;
  createdCount: number;
  failedCount: number;
  alreadyExistedCount: number;
  invitationEmailsSent: number;
  rows: BulkImportOutcome[];
}

/** Returned by GET /employees/bulk-import/{id} */
export interface BulkImportSessionDto {
  sessionId: string;
  status: BulkImportSessionStatus;
  uploadedAt: string;
  validation: BulkImportValidationSummary | null;
  processing: BulkImportProcessingSummary | null;
}

/** Returned by POST /employees/bulk-import/{id}/confirm */
export interface BulkImportConfirmResponse {
  jobId: string;
}

// ── Request params ────────────────────────────────────────────────────────────

export interface UploadBulkImportParams {
  file: File;
  /**
   * When provided, sent as the X-Tenant-Id request header.
   * Required for SuperUser callers targeting a tenant other than their default.
   * Note: the global apiClient interceptor also injects X-Tenant-Id from
   * localStorage.activeTenantId — the caller must ensure these are consistent.
   */
  targetTenantId?: string;
}

// ── API functions ─────────────────────────────────────────────────────────────

/**
 * Upload a CSV, validate it, and receive a session ID + validation summary.
 * Uses Result<T> envelope — reads response.data.data.
 */
export async function uploadBulkImport({
  file,
  targetTenantId,
}: UploadBulkImportParams): Promise<BulkImportUploadResponse> {
  const formData = new FormData();
  formData.append("file", file);

  // Do NOT set Content-Type manually — Axios sets the correct
  // multipart/form-data boundary automatically from FormData.
  const headers: Record<string, string> = {};
  if (targetTenantId) {
    headers["X-Tenant-Id"] = targetTenantId;
  }

  const response = await apiClient.post<ApiResponse<BulkImportUploadResponse>>(
    "/employees/bulk-import",
    formData,
    { headers }
  );
  return response.data.data;
}

/**
 * Enqueue the processing job for a Validated session.
 * Uses Result<T> envelope — reads response.data.data.
 */
export async function confirmBulkImport(
  sessionId: string
): Promise<BulkImportConfirmResponse> {
  const response = await apiClient.post<ApiResponse<BulkImportConfirmResponse>>(
    `/employees/bulk-import/${sessionId}/confirm`
  );
  return response.data.data;
}

/**
 * Poll session status, validation summary, and processing results.
 * Uses Result<T> envelope — reads response.data.data.
 */
export async function getBulkImportSession(
  sessionId: string
): Promise<BulkImportSessionDto> {
  const response = await apiClient.get<ApiResponse<BulkImportSessionDto>>(
    `/employees/bulk-import/${sessionId}`
  );
  return response.data.data;
}

/**
 * Download failed-row CSV (validation failures, or post-processing failures if
 * processing has run). Returns a Blob — the hook layer triggers the file save.
 */
export async function downloadFailedRows(sessionId: string): Promise<Blob> {
  const response = await apiClient.get(
    `/employees/bulk-import/${sessionId}/failed-rows`,
    { responseType: "blob" }
  );
  return response.data;
}

/**
 * Download the static CSV import template (header row + sample rows).
 * Returns a Blob — the hook layer triggers the file save.
 */
export async function downloadBulkImportTemplate(): Promise<Blob> {
  const response = await apiClient.get("/employees/bulk-import/template", {
    responseType: "blob",
  });
  return response.data;
}
