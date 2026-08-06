"use client";

import * as React from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { ChevronLeft, Loader2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { usePermission } from "@/lib/auth/use-auth";
import { BulkSopImportUploadPanel } from "@/features/toolbox-talks/components/bulk-sop-import/BulkSopImportUploadPanel";
import { BulkSopImportValidationPanel } from "@/features/toolbox-talks/components/bulk-sop-import/BulkSopImportValidationPanel";
import { BulkSopImportProcessingPanel } from "@/features/toolbox-talks/components/bulk-sop-import/BulkSopImportProcessingPanel";
import { BulkSopImportResultsPanel } from "@/features/toolbox-talks/components/bulk-sop-import/BulkSopImportResultsPanel";
import {
  useBulkSopImportSession,
  type BulkSopImportUploadResponse,
} from "@/lib/api/toolbox-talks/use-bulk-sop-import";

type FlowState = "upload" | "validation-summary" | "processing" | "results";

const POLL_INTERVAL_MS = 3000;
const BASE_PATH = "/admin/toolbox-talks/bulk-sop-import";
const BACK_PATH = "/admin/toolbox-talks/talks";

function BulkSopImportPageContent() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const urlSessionId = searchParams.get("session");
  const canManage = usePermission("Learnings.Manage");

  const [flowState, setFlowState] = React.useState<FlowState>("upload");
  const [uploadResponse, setUploadResponse] =
    React.useState<BulkSopImportUploadResponse | null>(null);
  const [sessionId, setSessionId] = React.useState<string | null>(urlSessionId);
  // False when we still need to resolve flowState from the URL session param.
  const [resumeResolved, setResumeResolved] = React.useState(!urlSessionId);

  const isPolling = flowState === "processing";

  const { data: sessionData, isError: sessionError } = useBulkSopImportSession(
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

  const handleUploadSuccess = (response: BulkSopImportUploadResponse) => {
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
  const totalPdfCount = validation?.totalPdfCount ?? 0;
  const activeSessionId = uploadResponse?.sessionId ?? sessionId;

  const showResumeSpinner = !resumeResolved;

  if (!canManage) {
    return (
      <div className="flex items-center justify-center py-12">
        <div className="text-muted-foreground">
          You do not have permission to bulk import SOPs.
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-4">
        <Button variant="ghost" size="icon" asChild>
          <Link href={BACK_PATH}>
            <ChevronLeft className="h-4 w-4" />
          </Link>
        </Button>
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Bulk SOP Import</h1>
          <p className="text-muted-foreground">
            Create multiple Draft learnings from a ZIP of SOP PDFs
          </p>
        </div>
      </div>

      {showResumeSpinner && (
        <div className="flex items-center justify-center py-16">
          <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
        </div>
      )}

      {!showResumeSpinner && flowState === "upload" && (
        <BulkSopImportUploadPanel onSuccess={handleUploadSuccess} />
      )}

      {!showResumeSpinner &&
        flowState === "validation-summary" &&
        validation &&
        activeSessionId && (
          <BulkSopImportValidationPanel
            sessionId={activeSessionId}
            validation={validation}
            onConfirmSuccess={handleConfirmSuccess}
            onCancel={handleCancel}
          />
        )}

      {!showResumeSpinner && flowState === "processing" && (
        <BulkSopImportProcessingPanel totalPdfCount={totalPdfCount} />
      )}

      {!showResumeSpinner &&
        flowState === "results" &&
        sessionData?.processing && (
          <BulkSopImportResultsPanel
            processing={sessionData.processing}
            jobSucceeded={sessionData.status === "Completed"}
            onImportAnother={handleImportAnother}
          />
        )}
    </div>
  );
}

export default function BulkSopImportPage() {
  return (
    <React.Suspense
      fallback={
        <div className="space-y-6">
          <div className="flex items-center gap-4">
            <Button variant="ghost" size="icon" asChild>
              <Link href={BACK_PATH}>
                <ChevronLeft className="h-4 w-4" />
              </Link>
            </Button>
            <div>
              <h1 className="text-2xl font-semibold tracking-tight">Bulk SOP Import</h1>
              <p className="text-muted-foreground">
                Create multiple Draft learnings from a ZIP of SOP PDFs
              </p>
            </div>
          </div>
          <div className="flex items-center justify-center py-16">
            <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
          </div>
        </div>
      }
    >
      <BulkSopImportPageContent />
    </React.Suspense>
  );
}
