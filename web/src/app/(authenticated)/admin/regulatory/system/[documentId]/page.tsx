"use client";

import { useState, useCallback, useEffect } from "react";
import { useParams, useRouter } from "next/navigation";
import { useIsSuperUser } from "@/lib/auth/use-auth";
import {
  useIngestionStatus,
  useIngestionStatusPolling,
  useDraftRequirements,
  useStartIngestion,
  useApproveRequirement,
  useRejectRequirement,
  useUpdateDraftRequirement,
  useApproveAllDrafts,
} from "@/lib/api/admin/use-regulatory-ingestion";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Separator } from "@/components/ui/separator";
import { Skeleton } from "@/components/ui/skeleton";
import { Textarea } from "@/components/ui/textarea";
import { RegulatoryDocumentUpload } from "@/components/admin/regulatory-document-upload";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from "@/components/ui/dialog";
import {
  Accordion,
  AccordionContent,
  AccordionItem,
  AccordionTrigger,
} from "@/components/ui/accordion";
import {
  ArrowLeft,
  Loader2,
  Check,
  X,
  Pencil,
  FileText,
  CheckCircle2,
  XCircle,
} from "lucide-react";
import { toast } from "sonner";
import type { DraftRequirementDto } from "@/types/regulatory";

function formatDate(dateStr: string | null): string {
  if (!dateStr) return "—";
  return new Date(dateStr).toLocaleDateString("en-IE", {
    day: "numeric",
    month: "short",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

/**
 * Lightweight client-side guidance for the Source URL field. Backend validation
 * (RequirementIngestionService.StartIngestionAsync via SourceUrlValidator) is authoritative —
 * this only gives the user an earlier, friendlier signal before they submit.
 */
function checkSourceUrlInput(
  value: string
): { level: "error" | "warning"; message: string } | null {
  const trimmed = value.trim();

  if (!trimmed) {
    return { level: "error", message: "Source URL is required." };
  }

  if (/^[a-zA-Z]:[\\/]/.test(trimmed) || trimmed.startsWith("/")) {
    return {
      level: "error",
      message:
        "This looks like a local file path, not a web address. Enter a public https:// URL.",
    };
  }

  try {
    const url = new URL(trimmed);
    if (url.protocol !== "http:" && url.protocol !== "https:") {
      return {
        level: "warning",
        message: `Source URL should use http or https (found "${url.protocol}").`,
      };
    }
  } catch {
    return {
      level: "warning",
      message:
        "This doesn't look like a valid URL. It should start with https://",
    };
  }

  return null;
}

/**
 * Maps a backend LastIngestionErrorCode onto a friendlier explanation for the reviewer.
 * Falls back to the raw error message when the code doesn't match a known category —
 * matches the backend's own "don't force an unknown reason into a category" rule.
 */
function describeIngestionError(
  errorCode: string | null,
  errorMessage: string | null
): string {
  const detail = errorMessage ? ` (${errorMessage})` : "";
  switch (errorCode) {
    case "invalid_uri":
      return `Source URL must be a valid HTTPS URL, e.g. https://example.com/document.pdf.${detail}`;
    case "fetch_failed":
      return `Could not fetch the document from the source URL — the host may be unreachable, the URL may be broken, or the request timed out.${detail}`;
    case "parse_failed":
      return `The document was fetched but its content could not be read — it may be a scanned PDF with no extractable text, a corrupted file, or an unsupported format.${detail}`;
    default:
      return errorMessage || "An unexpected error occurred during ingestion.";
  }
}

function StatusDisplay({
  status,
  isPolling,
}: {
  status?: string;
  isPolling: boolean;
}) {
  if (status === "Failed") {
    return (
      <span className="flex items-center gap-1 text-destructive">
        <XCircle className="h-3 w-3" />
        Failed
      </span>
    );
  }
  if (status === "Success") {
    return (
      <span className="flex items-center gap-1 text-green-600">
        <CheckCircle2 className="h-3 w-3" />
        Success
      </span>
    );
  }
  if (status === "Ingesting" || isPolling) {
    return (
      <span className="flex items-center gap-1 text-amber-600">
        <Loader2 className="h-3 w-3 animate-spin" />
        Ingesting...
      </span>
    );
  }
  return <>{status || "Idle"}</>;
}

function PriorityBadge({ priority }: { priority: string }) {
  const variants: Record<string, string> = {
    high: "border-red-500 text-red-600",
    med: "border-amber-500 text-amber-600",
    low: "border-gray-400 text-gray-500",
  };
  return (
    <Badge variant="outline" className={variants[priority] || variants.med}>
      {priority}
    </Badge>
  );
}

interface EditState {
  title: string;
  description: string;
  section: string;
  sectionLabel: string;
  principle: string;
  principleLabel: string;
  priority: string;
  displayOrder: number;
}

function DraftRequirementCard({
  draft,
  documentId,
}: {
  draft: DraftRequirementDto;
  documentId: string;
}) {
  const [isEditing, setIsEditing] = useState(false);
  const [showRejectDialog, setShowRejectDialog] = useState(false);
  const [rejectNotes, setRejectNotes] = useState("");
  const [editState, setEditState] = useState<EditState>({
    title: draft.title,
    description: draft.description,
    section: draft.section || "",
    sectionLabel: draft.sectionLabel || "",
    principle: draft.principle || "",
    principleLabel: draft.principleLabel || "",
    priority: draft.priority,
    displayOrder: draft.displayOrder,
  });

  const approveMutation = useApproveRequirement(documentId);
  const rejectMutation = useRejectRequirement(documentId);
  const updateMutation = useUpdateDraftRequirement(documentId);

  const handleApprove = useCallback(() => {
    approveMutation.mutate(
      {
        requirementId: draft.id,
        data: {
          title: isEditing ? editState.title : draft.title,
          description: isEditing ? editState.description : draft.description,
          section: (isEditing ? editState.section : draft.section) || null,
          sectionLabel:
            (isEditing ? editState.sectionLabel : draft.sectionLabel) || null,
          principle:
            (isEditing ? editState.principle : draft.principle) || null,
          principleLabel:
            (isEditing ? editState.principleLabel : draft.principleLabel) ||
            null,
          priority: isEditing ? editState.priority : draft.priority,
          displayOrder: isEditing
            ? editState.displayOrder
            : draft.displayOrder,
        },
      },
      {
        onSuccess: () => {
          toast.success(`Approved: ${draft.title}`);
          setIsEditing(false);
        },
        onError: () => toast.error("Failed to approve requirement"),
      }
    );
  }, [approveMutation, draft, isEditing, editState]);

  const handleReject = useCallback(() => {
    rejectMutation.mutate(
      {
        requirementId: draft.id,
        data: { notes: rejectNotes },
      },
      {
        onSuccess: () => {
          toast.success(`Rejected: ${draft.title}`);
          setShowRejectDialog(false);
          setRejectNotes("");
        },
        onError: () => toast.error("Failed to reject requirement"),
      }
    );
  }, [rejectMutation, draft, rejectNotes]);

  const handleSaveEdit = useCallback(() => {
    updateMutation.mutate(
      {
        requirementId: draft.id,
        data: {
          title: editState.title,
          description: editState.description,
          section: editState.section || null,
          sectionLabel: editState.sectionLabel || null,
          principle: editState.principle || null,
          principleLabel: editState.principleLabel || null,
          priority: editState.priority,
          displayOrder: editState.displayOrder,
        },
      },
      {
        onSuccess: () => {
          toast.success("Draft updated");
          setIsEditing(false);
        },
        onError: () => toast.error("Failed to update draft"),
      }
    );
  }, [updateMutation, draft, editState]);

  const isBusy =
    approveMutation.isPending ||
    rejectMutation.isPending ||
    updateMutation.isPending;

  return (
    <>
      <Card>
        <CardContent className="pt-4 space-y-3">
          <div className="flex items-start justify-between gap-4">
            <div className="flex-1 space-y-1">
              {isEditing ? (
                <Input
                  value={editState.title}
                  onChange={(e) =>
                    setEditState((s) => ({ ...s, title: e.target.value }))
                  }
                  className="font-medium"
                />
              ) : (
                <h4 className="font-medium">{draft.title}</h4>
              )}
              <div className="flex items-center gap-2 text-xs text-muted-foreground">
                {draft.section && (
                  <span>
                    {draft.section}
                    {draft.sectionLabel ? ` — ${draft.sectionLabel}` : ""}
                  </span>
                )}
                {draft.principle && (
                  <>
                    <span>/</span>
                    <span>
                      {draft.principle}
                      {draft.principleLabel
                        ? ` — ${draft.principleLabel}`
                        : ""}
                    </span>
                  </>
                )}
                <PriorityBadge priority={draft.priority} />
                <Badge variant="outline" className="text-xs">
                  {draft.ingestionSource}
                </Badge>
                <Badge variant="outline" className="text-xs">
                  {draft.profileSectorKey}
                </Badge>
              </div>
            </div>
            <div className="flex items-center gap-1">
              <Button
                variant="ghost"
                size="sm"
                onClick={() => setIsEditing(!isEditing)}
                disabled={isBusy}
              >
                <Pencil className="h-4 w-4" />
              </Button>
              <Button
                variant="ghost"
                size="sm"
                className="text-green-600 hover:text-green-700 hover:bg-green-50"
                onClick={handleApprove}
                disabled={isBusy}
              >
                {approveMutation.isPending ? (
                  <Loader2 className="h-4 w-4 animate-spin" />
                ) : (
                  <Check className="h-4 w-4" />
                )}
              </Button>
              <Button
                variant="ghost"
                size="sm"
                className="text-red-600 hover:text-red-700 hover:bg-red-50"
                onClick={() => setShowRejectDialog(true)}
                disabled={isBusy}
              >
                <X className="h-4 w-4" />
              </Button>
            </div>
          </div>

          {isEditing ? (
            <div className="space-y-3">
              <Textarea
                value={editState.description}
                onChange={(e) =>
                  setEditState((s) => ({ ...s, description: e.target.value }))
                }
                rows={3}
              />
              <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
                <div>
                  <label className="text-xs text-muted-foreground">
                    Section
                  </label>
                  <Input
                    value={editState.section}
                    onChange={(e) =>
                      setEditState((s) => ({ ...s, section: e.target.value }))
                    }
                    placeholder="e.g. §7"
                  />
                </div>
                <div>
                  <label className="text-xs text-muted-foreground">
                    Section Label
                  </label>
                  <Input
                    value={editState.sectionLabel}
                    onChange={(e) =>
                      setEditState((s) => ({
                        ...s,
                        sectionLabel: e.target.value,
                      }))
                    }
                  />
                </div>
                <div>
                  <label className="text-xs text-muted-foreground">
                    Priority
                  </label>
                  <Select
                    value={editState.priority}
                    onValueChange={(v) =>
                      setEditState((s) => ({ ...s, priority: v }))
                    }
                  >
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="high">High</SelectItem>
                      <SelectItem value="med">Medium</SelectItem>
                      <SelectItem value="low">Low</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
                <div>
                  <label className="text-xs text-muted-foreground">
                    Order
                  </label>
                  <Input
                    type="number"
                    value={editState.displayOrder}
                    onChange={(e) =>
                      setEditState((s) => ({
                        ...s,
                        displayOrder: parseInt(e.target.value) || 0,
                      }))
                    }
                  />
                </div>
              </div>
              <div className="flex gap-2">
                <Button size="sm" onClick={handleSaveEdit} disabled={isBusy}>
                  {updateMutation.isPending && (
                    <Loader2 className="mr-1 h-3 w-3 animate-spin" />
                  )}
                  Save
                </Button>
                <Button
                  size="sm"
                  variant="ghost"
                  onClick={() => setIsEditing(false)}
                >
                  Cancel
                </Button>
              </div>
            </div>
          ) : (
            <p className="text-sm text-muted-foreground">
              {draft.description}
            </p>
          )}
        </CardContent>
      </Card>

      <Dialog open={showRejectDialog} onOpenChange={setShowRejectDialog}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Reject Requirement</DialogTitle>
          </DialogHeader>
          <div className="space-y-2">
            <p className="text-sm font-medium">{draft.title}</p>
            <Textarea
              placeholder="Rejection notes (required)"
              value={rejectNotes}
              onChange={(e) => setRejectNotes(e.target.value)}
              rows={3}
            />
          </div>
          <DialogFooter>
            <Button
              variant="ghost"
              onClick={() => setShowRejectDialog(false)}
            >
              Cancel
            </Button>
            <Button
              variant="destructive"
              onClick={handleReject}
              disabled={!rejectNotes.trim() || rejectMutation.isPending}
            >
              {rejectMutation.isPending && (
                <Loader2 className="mr-1 h-3 w-3 animate-spin" />
              )}
              Reject
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}

export default function RegulatoryDocumentDetailPage() {
  const params = useParams();
  const router = useRouter();
  const documentId = params.documentId as string;
  const isSuperUser = useIsSuperUser();

  const [sourceUrl, setSourceUrl] = useState("");
  const [isPolling, setIsPolling] = useState(false);

  const {
    data: status,
    isLoading: statusLoading,
  } = useIngestionStatus(documentId, !isPolling);

  const { data: pollingStatus } = useIngestionStatusPolling(
    documentId,
    isPolling
  );

  const currentStatus = isPolling ? pollingStatus : status;

  const { data: drafts, isLoading: draftsLoading } =
    useDraftRequirements(documentId);

  const startIngestion = useStartIngestion(documentId);
  const approveAll = useApproveAllDrafts(documentId);

  const effectiveSourceUrl =
    sourceUrl || currentStatus?.sourceUrl || "";

  const sourceUrlIssue = checkSourceUrlInput(effectiveSourceUrl);

  const handleStartIngestion = useCallback(() => {
    startIngestion.mutate(
      { sourceUrl: effectiveSourceUrl },
      {
        onSuccess: () => {
          toast.success("Ingestion job queued");
          setIsPolling(true);
          // Safety-net timeout in case the terminal-status check below never fires
          // (e.g. the job never updates status for some unforeseen reason).
          setTimeout(() => setIsPolling(false), 120000);
        },
        onError: (err: Error) =>
          toast.error(err.message || "Failed to start ingestion"),
      }
    );
  }, [startIngestion, effectiveSourceUrl]);

  // Stop polling as soon as the backend reports a terminal state, rather than waiting
  // blindly for the 120s timeout. Also correctly stops for a 0-draft Success (the old
  // "hasDrafts" heuristic never noticed those).
  useEffect(() => {
    if (!isPolling) return;
    if (currentStatus?.status === "Success" || currentStatus?.status === "Failed") {
      setIsPolling(false);
    }
  }, [isPolling, currentStatus?.status]);

  const handleApproveAll = useCallback(() => {
    approveAll.mutate(undefined, {
      onSuccess: (data) => {
        toast.success(`Approved ${data.approved} requirements`);
      },
      onError: () => toast.error("Failed to approve all"),
    });
  }, [approveAll]);

  if (!isSuperUser) {
    return (
      <div className="text-muted-foreground">
        You do not have permission to access this page.
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-4">
        <Button variant="ghost" size="sm" onClick={() => router.back()}>
          <ArrowLeft className="mr-1 h-4 w-4" />
          Back
        </Button>
        <div>
          <h2 className="text-xl font-semibold tracking-tight">
            {currentStatus?.documentTitle || "Regulatory Document"}
          </h2>
          <p className="text-sm text-muted-foreground">
            Manage requirement ingestion and review
          </p>
        </div>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">
            Document Details & Ingestion
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          {statusLoading ? (
            <div className="space-y-2">
              <Skeleton className="h-4 w-48" />
              <Skeleton className="h-10 w-full" />
            </div>
          ) : (
            <>
              <div className="grid grid-cols-2 gap-4 text-sm sm:grid-cols-4">
                <div>
                  <span className="text-muted-foreground">Status</span>
                  <p className="font-medium">
                    <StatusDisplay
                      status={currentStatus?.status}
                      isPolling={isPolling}
                    />
                  </p>
                </div>
                <div>
                  <span className="text-muted-foreground">Last Ingested</span>
                  <p className="font-medium">
                    {formatDate(currentStatus?.lastIngestedAt || null)}
                  </p>
                </div>
                <div>
                  <span className="text-muted-foreground">Draft</span>
                  <p className="font-medium">
                    {currentStatus?.draftCount || 0}
                  </p>
                </div>
                <div>
                  <span className="text-muted-foreground">Approved</span>
                  <p className="font-medium">
                    {currentStatus?.approvedCount || 0}
                  </p>
                </div>
              </div>

              <Separator />

              <div className="space-y-3">
                <div>
                  <label className="text-sm font-medium">Upload PDF</label>
                  <div className="mt-1">
                    <RegulatoryDocumentUpload
                      documentId={documentId}
                      currentSourceUrl={sourceUrl || currentStatus?.sourceUrl || null}
                      onUploaded={(url) => setSourceUrl(url)}
                    />
                  </div>
                </div>

                <div className="flex items-end gap-3">
                  <div className="flex-1">
                    <label className="text-sm font-medium">Source URL</label>
                    <Input
                      type="url"
                      value={sourceUrl || currentStatus?.sourceUrl || ""}
                      onChange={(e) => setSourceUrl(e.target.value)}
                      placeholder="https://example.com/document.pdf"
                      className={
                        sourceUrlIssue?.level === "error"
                          ? "border-destructive"
                          : undefined
                      }
                    />
                  </div>
                  <Button
                    onClick={handleStartIngestion}
                    disabled={
                      startIngestion.isPending ||
                      isPolling ||
                      sourceUrlIssue?.level === "error"
                    }
                  >
                    {startIngestion.isPending || isPolling ? (
                      <>
                        <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                        {isPolling ? "Ingesting..." : "Starting..."}
                      </>
                    ) : (
                      <>
                        <FileText className="mr-2 h-4 w-4" />
                        {currentStatus?.status === "Failed"
                          ? "Retry Ingestion"
                          : "Ingest Requirements"}
                      </>
                    )}
                  </Button>
                </div>
                {sourceUrlIssue && (
                  <p
                    className={
                      sourceUrlIssue.level === "error"
                        ? "text-xs text-destructive"
                        : "text-xs text-amber-600"
                    }
                  >
                    {sourceUrlIssue.message}
                  </p>
                )}
              </div>

              {!isPolling && currentStatus?.status === "Failed" && (
                <div className="rounded-md border border-destructive/50 bg-destructive/10 p-3 text-sm text-destructive">
                  <p className="font-medium">Ingestion failed</p>
                  <p>
                    {describeIngestionError(
                      currentStatus.lastIngestionErrorCode,
                      currentStatus.lastIngestionErrorMessage
                    )}
                  </p>
                </div>
              )}
            </>
          )}
        </CardContent>
      </Card>

      {(drafts && drafts.length > 0) && (
        <Card>
          <CardHeader>
            <div className="flex items-center justify-between">
              <CardTitle className="text-base">
                Draft Requirements ({drafts.length})
              </CardTitle>
              <Button
                size="sm"
                onClick={handleApproveAll}
                disabled={approveAll.isPending}
              >
                {approveAll.isPending ? (
                  <Loader2 className="mr-1 h-3 w-3 animate-spin" />
                ) : (
                  <CheckCircle2 className="mr-1 h-3 w-3" />
                )}
                Approve All
              </Button>
            </div>
          </CardHeader>
          <CardContent className="space-y-3">
            {drafts.map((draft) => (
              <DraftRequirementCard
                key={draft.id}
                draft={draft}
                documentId={documentId}
              />
            ))}
          </CardContent>
        </Card>
      )}

      {draftsLoading && (
        <Card>
          <CardContent className="pt-6 space-y-3">
            {Array.from({ length: 3 }).map((_, i) => (
              <Skeleton key={i} className="h-24 w-full" />
            ))}
          </CardContent>
        </Card>
      )}

      {currentStatus &&
        (currentStatus.approvedCount > 0 ||
          currentStatus.rejectedCount > 0) && (
          <Accordion type="multiple" className="w-full">
            {currentStatus.approvedCount > 0 && (
              <AccordionItem value="approved">
                <AccordionTrigger>
                  <span className="flex items-center gap-2">
                    <CheckCircle2 className="h-4 w-4 text-green-600" />
                    Approved Requirements ({currentStatus.approvedCount})
                  </span>
                </AccordionTrigger>
                <AccordionContent>
                  <p className="text-sm text-muted-foreground">
                    {currentStatus.approvedCount} requirements have been
                    approved and are visible to tenants.
                  </p>
                </AccordionContent>
              </AccordionItem>
            )}
            {currentStatus.rejectedCount > 0 && (
              <AccordionItem value="rejected">
                <AccordionTrigger>
                  <span className="flex items-center gap-2">
                    <XCircle className="h-4 w-4 text-red-500" />
                    Rejected Requirements ({currentStatus.rejectedCount})
                  </span>
                </AccordionTrigger>
                <AccordionContent>
                  <p className="text-sm text-muted-foreground">
                    {currentStatus.rejectedCount} requirements were rejected
                    during review.
                  </p>
                </AccordionContent>
              </AccordionItem>
            )}
          </Accordion>
        )}
    </div>
  );
}
