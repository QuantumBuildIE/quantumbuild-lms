"use client";

import { useState } from "react";
import Link from "next/link";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Skeleton } from "@/components/ui/skeleton";
import { usePermission } from "@/lib/auth/use-auth";
import {
  usePendingMappings,
  useConfirmMapping,
  useRejectMapping,
  useConfirmAllMappings,
} from "@/lib/api/admin/use-requirement-mappings";
import type { PendingMappingDto } from "@/types/requirement-mappings";
import {
  CheckCircle2,
  XCircle,
  AlertTriangle,
  FileText,
  BookOpen,
} from "lucide-react";
import { toast } from "sonner";

function ConfidenceBadge({ score }: { score: number | null }) {
  if (score === null) return null;
  const color =
    score >= 80
      ? "bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400"
      : score >= 60
        ? "bg-amber-100 text-amber-800 dark:bg-amber-900/30 dark:text-amber-400"
        : "bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-400";

  return (
    <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${color}`}>
      {score}% confidence
    </span>
  );
}

function PriorityBadge({ priority }: { priority: string }) {
  const variant = priority === "high" ? "destructive" : priority === "low" ? "secondary" : "outline";
  return <Badge variant={variant}>{priority}</Badge>;
}

function MappingCard({ mapping }: { mapping: PendingMappingDto }) {
  const confirmMutation = useConfirmMapping();
  const rejectMutation = useRejectMapping();
  const [rejecting, setRejecting] = useState(false);

  const handleConfirm = () => {
    confirmMutation.mutate(mapping.id, {
      onSuccess: () => toast.success("Mapping confirmed"),
      onError: () => toast.error("Failed to confirm mapping"),
    });
  };

  const handleReject = () => {
    setRejecting(true);
    rejectMutation.mutate(
      { mappingId: mapping.id, notes: null },
      {
        onSuccess: () => {
          toast.success("Mapping rejected");
          setRejecting(false);
        },
        onError: () => {
          toast.error("Failed to reject mapping");
          setRejecting(false);
        },
      }
    );
  };

  const contentHref =
    mapping.contentType === "Talk"
      ? `/admin/toolbox-talks/talks/${mapping.contentId}`
      : `/admin/toolbox-talks/courses/${mapping.contentId}/edit`;

  return (
    <Card>
      <CardContent className="pt-6 space-y-4">
        {/* Content row */}
        <div className="flex items-center gap-2 text-sm">
          {mapping.contentType === "Talk" ? (
            <FileText className="h-4 w-4 text-muted-foreground" />
          ) : (
            <BookOpen className="h-4 w-4 text-muted-foreground" />
          )}
          <Link href={contentHref} className="font-medium hover:underline">
            {mapping.contentTitle}
          </Link>
          <Badge variant="outline">{mapping.contentType}</Badge>
          <ConfidenceBadge score={mapping.confidenceScore} />
        </div>

        {/* Requirement details */}
        <div className="space-y-1">
          <div className="flex items-center gap-2">
            <h4 className="font-medium">{mapping.requirementTitle}</h4>
            <PriorityBadge priority={mapping.requirementPriority} />
            {mapping.requirementSection && (
              <Badge variant="secondary">
                {mapping.requirementSection}
                {mapping.requirementSectionLabel
                  ? ` — ${mapping.requirementSectionLabel}`
                  : ""}
              </Badge>
            )}
            {mapping.requirementPrinciple && (
              <Badge variant="secondary">
                {mapping.requirementPrinciple}
                {mapping.requirementPrincipleLabel
                  ? ` — ${mapping.requirementPrincipleLabel}`
                  : ""}
              </Badge>
            )}
          </div>
          <p className="text-sm text-muted-foreground">
            {mapping.requirementDescription}
          </p>
        </div>

        {/* AI reasoning */}
        {mapping.aiReasoning && (
          <div className="rounded-md border border-muted bg-muted/50 px-3 py-2 text-sm text-muted-foreground">
            {mapping.aiReasoning}
          </div>
        )}

        {/* Actions */}
        <div className="flex items-center gap-2 pt-1">
          <Button
            size="sm"
            onClick={handleConfirm}
            disabled={confirmMutation.isPending || rejectMutation.isPending}
          >
            <CheckCircle2 className="mr-1 h-4 w-4" />
            Confirm
          </Button>
          <Button
            size="sm"
            variant="outline"
            onClick={handleReject}
            disabled={confirmMutation.isPending || rejecting}
          >
            <XCircle className="mr-1 h-4 w-4" />
            Reject
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}

export default function PendingMappingsPage() {
  const hasAdminPermission = usePermission("Learnings.Admin");
  const { data: summary, isLoading } = usePendingMappings();
  const confirmAllMutation = useConfirmAllMappings();

  if (!hasAdminPermission) {
    return (
      <div className="space-y-6">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">
            Pending Requirement Mappings
          </h1>
        </div>
        <Card className="p-8 text-center">
          <p className="text-muted-foreground">
            You do not have permission to manage requirement mappings.
          </p>
        </Card>
      </div>
    );
  }

  const handleConfirmAll = () => {
    confirmAllMutation.mutate(undefined, {
      onSuccess: (data) =>
        toast.success(`${data.confirmed} mapping(s) confirmed`),
      onError: () => toast.error("Failed to confirm all mappings"),
    });
  };

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">
          Pending Requirement Mappings
        </h1>
        <p className="text-muted-foreground">
          Review AI-suggested mappings between your training content and
          regulatory requirements.
        </p>
      </div>

      {/* Summary cards */}
      {isLoading ? (
        <div className="grid gap-4 md:grid-cols-3">
          {[1, 2, 3].map((i) => (
            <Skeleton key={i} className="h-24" />
          ))}
        </div>
      ) : summary ? (
        <div className="grid gap-4 md:grid-cols-3">
          <Card>
            <CardHeader className="pb-2">
              <CardDescription>Suggested</CardDescription>
              <CardTitle className="text-2xl text-amber-600">
                {summary.totalSuggested}
              </CardTitle>
            </CardHeader>
          </Card>
          <Card>
            <CardHeader className="pb-2">
              <CardDescription>Confirmed</CardDescription>
              <CardTitle className="text-2xl text-green-600">
                {summary.totalConfirmed}
              </CardTitle>
            </CardHeader>
          </Card>
          <Card>
            <CardHeader className="pb-2">
              <CardDescription>Rejected</CardDescription>
              <CardTitle className="text-2xl text-muted-foreground">
                {summary.totalRejected}
              </CardTitle>
            </CardHeader>
          </Card>
        </div>
      ) : null}

      {/* Confirm All button */}
      {summary && summary.pendingReview.length > 0 && (
        <div className="flex justify-end">
          <Button
            onClick={handleConfirmAll}
            disabled={confirmAllMutation.isPending}
          >
            <CheckCircle2 className="mr-2 h-4 w-4" />
            Confirm All ({summary.pendingReview.length})
          </Button>
        </div>
      )}

      {/* Mapping list or empty state */}
      {isLoading ? (
        <div className="space-y-4">
          {[1, 2, 3].map((i) => (
            <Skeleton key={i} className="h-40" />
          ))}
        </div>
      ) : summary && summary.pendingReview.length > 0 ? (
        <div className="space-y-4">
          {summary.pendingReview.map((mapping) => (
            <MappingCard key={mapping.id} mapping={mapping} />
          ))}
        </div>
      ) : (
        <Card className="p-8 text-center">
          <div className="flex flex-col items-center gap-2">
            <CheckCircle2 className="h-8 w-8 text-muted-foreground" />
            <p className="text-muted-foreground">
              No pending mappings. Mappings are generated automatically when
              content is published.
            </p>
          </div>
        </Card>
      )}
    </div>
  );
}
