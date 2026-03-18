"use client";

import { useState, useCallback } from "react";
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

  // Initialize sourceUrl from status
  const effectiveSourceUrl =
    sourceUrl || currentStatus?.sourceUrl || "";

  const handleStartIngestion = useCallback(() => {
    startIngestion.mutate(
      { sourceUrl: effectiveSourceUrl },
      {
        onSuccess: () => {
          toast.success("Ingestion job queued");
          setIsPolling(true);
          // Stop polling after 2 minutes max
          setTimeout(() => setIsPolling(false), 120000);
        },
        onError: (err: Error) =>
          toast.error(err.message || "Failed to start ingestion"),
      }
    );
  }, [startIngestion, effectiveSourceUrl]);

  // Stop polling when drafts appear
  const hasDrafts =
    currentStatus?.draftCount && currentStatus.draftCount > 0;
  if (isPolling && hasDrafts) {
    setIsPolling(false);
  }

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

      {/* Section 1: Document details and ingestion trigger */}
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
                    {isPolling ? (
                      <span className="flex items-center gap-1 text-amber-600">
                        <Loader2 className="h-3 w-3 animate-spin" />
                        Processing...
                      </span>
                    ) : (
                      currentStatus?.status || "—"
                    )}
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

              <div className="flex items-end gap-3">
                <div className="flex-1">
                  <label className="text-sm font-medium">Source URL</label>
                  <Input
                    value={sourceUrl || currentStatus?.sourceUrl || ""}
                    onChange={(e) => setSourceUrl(e.target.value)}
                    placeholder="https://example.com/document.pdf"
                  />
                </div>
                <Button
                  onClick={handleStartIngestion}
                  disabled={
                    startIngestion.isPending ||
                    isPolling ||
                    !effectiveSourceUrl.trim()
                  }
                >
                  {startIngestion.isPending || isPolling ? (
                    <>
                      <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                      {isPolling ? "Processing..." : "Starting..."}
                    </>
                  ) : (
                    <>
                      <FileText className="mr-2 h-4 w-4" />
                      Ingest Requirements
                    </>
                  )}
                </Button>
              </div>
            </>
          )}
        </CardContent>
      </Card>

      {/* Section 2: Draft requirements review */}
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

      {/* Approved & Rejected sections */}
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
