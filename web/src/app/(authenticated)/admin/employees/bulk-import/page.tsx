"use client";

import * as React from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { ChevronLeft, Loader2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { BulkImportUploadPanel } from "@/components/admin/bulk-import-upload-panel";
import { BulkImportValidationPanel } from "@/components/admin/bulk-import-validation-panel";
import { BulkImportProcessingPanel } from "@/components/admin/bulk-import-processing-panel";
import { BulkImportResultsPanel } from "@/components/admin/bulk-import-results-panel";
import {
  useBulkImportSession,
  type BulkImportUploadResponse,
} from "@/lib/api/admin/use-bulk-import";

type FlowState = "upload" | "validation-summary" | "processing" | "results";

const POLL_INTERVAL_MS = 3000;
const BASE_PATH = "/admin/employees/bulk-import";

function BulkImportPageContent() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const urlSessionId = searchParams.get("session");

  const [flowState, setFlowState] = React.useState<FlowState>("upload");
  const [uploadResponse, setUploadResponse] =
    React.useState<BulkImportUploadResponse | null>(null);
  const [sessionId, setSessionId] = React.useState<string | null>(urlSessionId);
  // False when we still need to resolve flowState from the URL session param.
  const [resumeResolved, setResumeResolved] = React.useState(!urlSessionId);

  const isPolling = flowState === "processing";

  const { data: sessionData, isError: sessionError } = useBulkImportSession(
    sessionId,
    isPolling ? POLL_INTERVAL_MS : undefined
  );

  // Resolve the correct flow state from the URL session param on mount.
  React.useEffect(() => {
    if (resumeResolved) return;

    if (sessionError) {
      // Invalid or inaccessible session — fall back cleanly.
      setSessionId(null);
      setResumeResolved(true);
      router.replace(BASE_PATH);
      return;
    }

    if (!sessionData) return; // still loading

    const { status } = sessionData;
    if (status === "Uploaded" || status === "Validated") {
      setFlowState("validation-summary");
    } else if (status === "Processing") {
      setFlowState("processing");
    } else if (status === "Completed" || status === "Failed") {
      setFlowState("results");
    } else {
      // Cancelled or unknown — stale session, clear URL.
      setSessionId(null);
      router.replace(BASE_PATH);
    }
    setResumeResolved(true);
  }, [resumeResolved, sessionData, sessionError, router]);

  // Advance from processing → results once the job reaches a terminal status.
  React.useEffect(() => {
    if (!isPolling || !sessionData) return;
    const { status } = sessionData;
    if (status === "Completed" || status === "Failed") {
      setFlowState("results");
    }
  }, [isPolling, sessionData]);

  const handleUploadSuccess = (response: BulkImportUploadResponse) => {
    setUploadResponse(response);
    setSessionId(response.sessionId);
    setFlowState("validation-summary");
    router.replace(`${BASE_PATH}?session=${response.sessionId}`);
  };

  const handleConfirmSuccess = () => {
    setFlowState("processing");
  };

  const handleCancel = () => {
    setUploadResponse(null);
    setSessionId(null);
    setFlowState("upload");
    router.replace(BASE_PATH);
  };

  const handleImportAnother = () => {
    setUploadResponse(null);
    setSessionId(null);
    setFlowState("upload");
    router.replace(BASE_PATH);
  };

  // Prefer data from the fresh upload response; fall back to the session DTO on resume.
  const validation = uploadResponse?.validation ?? sessionData?.validation;
  const totalRows = validation?.totalRows ?? 0;
  const activeSessionId = uploadResponse?.sessionId ?? sessionId;

  const showResumeSpinner = !resumeResolved;

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-4">
        <Button variant="ghost" size="icon" asChild>
          <Link href="/admin/employees">
            <ChevronLeft className="h-4 w-4" />
          </Link>
        </Button>
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Bulk Import</h1>
          <p className="text-muted-foreground">
            Import multiple employees from a CSV file
          </p>
        </div>
      </div>

      {showResumeSpinner && (
        <div className="flex items-center justify-center py-16">
          <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
        </div>
      )}

      {!showResumeSpinner && flowState === "upload" && (
        <BulkImportUploadPanel onSuccess={handleUploadSuccess} />
      )}

      {!showResumeSpinner &&
        flowState === "validation-summary" &&
        validation &&
        activeSessionId && (
          <BulkImportValidationPanel
            sessionId={activeSessionId}
            validation={validation}
            onConfirmSuccess={handleConfirmSuccess}
            onCancel={handleCancel}
          />
        )}

      {!showResumeSpinner && flowState === "processing" && (
        <BulkImportProcessingPanel totalRows={totalRows} />
      )}

      {!showResumeSpinner &&
        flowState === "results" &&
        activeSessionId &&
        sessionData?.processing && (
          <BulkImportResultsPanel
            sessionId={activeSessionId}
            processing={sessionData.processing}
            jobSucceeded={sessionData.status === "Completed"}
            onImportAnother={handleImportAnother}
          />
        )}
    </div>
  );
}

export default function BulkImportPage() {
  return (
    <React.Suspense
      fallback={
        <div className="space-y-6">
          <div className="flex items-center gap-4">
            <Button variant="ghost" size="icon" asChild>
              <Link href="/admin/employees">
                <ChevronLeft className="h-4 w-4" />
              </Link>
            </Button>
            <div>
              <h1 className="text-2xl font-semibold tracking-tight">
                Bulk Import
              </h1>
              <p className="text-muted-foreground">
                Import multiple employees from a CSV file
              </p>
            </div>
          </div>
          <div className="flex items-center justify-center py-16">
            <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
          </div>
        </div>
      }
    >
      <BulkImportPageContent />
    </React.Suspense>
  );
}
