'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { format } from 'date-fns';
import { FileText, Play, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { DeleteDraftDialog } from '@/features/toolbox-talks/components/learning-wizard/components/DeleteDraftDialog';
import { useDraftsList } from '@/features/toolbox-talks/components/learning-wizard/hooks/useDraftsList';
import { useDeleteDraft } from '@/features/toolbox-talks/components/learning-wizard/hooks/useDeleteDraft';
import { WIZARD_STEPS } from '@/features/toolbox-talks/components/learning-wizard/lib/stepOrder';
import { getStepUrl as buildStepUrl } from '@/features/toolbox-talks/components/learning-wizard/lib/urlState';
import type { ToolboxTalkListItem } from '@/types/toolbox-talks';

function stepLabel(step: number | null): string {
  if (step === null || step === undefined) return 'Step 1 — Input & Config';
  const def = WIZARD_STEPS.find((s) => s.number === step);
  return def ? `Step ${def.number} — ${def.label}` : `Step ${step}`;
}

interface DraftRowProps {
  draft: ToolboxTalkListItem;
  onDelete: (draft: ToolboxTalkListItem) => void;
  onResume: (draft: ToolboxTalkListItem) => void;
}

function DraftRow({ draft, onDelete, onResume }: DraftRowProps) {
  return (
    <div className="flex flex-col gap-2 p-4 border rounded-lg sm:flex-row sm:items-center sm:gap-4">
      <FileText className="hidden h-5 w-5 shrink-0 text-muted-foreground sm:block" aria-hidden="true" />

      <div className="flex-1 min-w-0 space-y-0.5">
        <p className="font-medium truncate">{draft.title}</p>
        <p className="text-xs text-muted-foreground">
          {draft.createdByName ?? draft.createdBy}
          {' · '}
          {format(new Date(draft.createdAt), 'dd MMM yyyy, HH:mm')}
        </p>
        <p className="text-xs text-muted-foreground">
          {stepLabel(draft.lastEditedStep)}
        </p>
      </div>

      <div className="flex items-center gap-2 sm:shrink-0">
        <Button
          size="sm"
          variant="default"
          onClick={() => onResume(draft)}
          aria-label={`Resume draft: ${draft.title}`}
          className="gap-1.5"
        >
          <Play className="h-3.5 w-3.5" aria-hidden="true" />
          Resume
        </Button>
        <Button
          size="sm"
          variant="ghost"
          onClick={() => onDelete(draft)}
          aria-label={`Delete draft: ${draft.title}`}
          className="text-destructive hover:text-destructive hover:bg-destructive/10"
        >
          <Trash2 className="h-4 w-4" aria-hidden="true" />
          <span className="sr-only">Delete</span>
        </Button>
      </div>
    </div>
  );
}

function LoadingSkeleton() {
  return (
    <div className="space-y-3" role="status" aria-label="Loading drafts…">
      {[1, 2, 3].map((i) => (
        <div key={i} className="flex items-center gap-4 p-4 border rounded-lg">
          <Skeleton className="h-5 w-5 shrink-0 rounded" />
          <div className="flex-1 space-y-2">
            <Skeleton className="h-4 w-48" />
            <Skeleton className="h-3 w-32" />
          </div>
          <Skeleton className="h-8 w-20" />
        </div>
      ))}
      <span className="sr-only">Loading drafts…</span>
    </div>
  );
}

function EmptyState({ onNew }: { onNew: () => void }) {
  return (
    <div className="flex flex-col items-center justify-center py-16 text-center gap-4">
      <div className="rounded-full bg-muted p-4">
        <FileText className="h-8 w-8 text-muted-foreground" aria-hidden="true" />
      </div>
      <div className="space-y-1">
        <p className="font-medium">No draft learnings</p>
        <p className="text-sm text-muted-foreground">
          Drafts you start in the learning wizard will appear here.
        </p>
      </div>
      <Button onClick={onNew}>Start a new learning</Button>
    </div>
  );
}

export default function LearningDraftsPage() {
  const router = useRouter();
  const { drafts, isLoading, isError, error, refetch } = useDraftsList();
  const deleteDraft = useDeleteDraft();

  const [deleteTarget, setDeleteTarget] = useState<ToolboxTalkListItem | null>(null);

  const handleResume = (draft: ToolboxTalkListItem) => {
    // Default to step 2 (Parse) if no step was saved — step 1 creates the talk,
    // so any talk in the list must have passed step 1.
    const step = draft.lastEditedStep ?? 2;
    router.push(buildStepUrl(draft.id, step));
  };

  const handleDeleteConfirm = () => {
    if (!deleteTarget) return;
    deleteDraft.mutate(deleteTarget.id, {
      onSuccess: () => setDeleteTarget(null),
    });
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Draft Learnings</h1>
          <p className="text-muted-foreground text-sm">Resume or discard in-progress learnings.</p>
        </div>
        <Button
          variant="outline"
          onClick={() => router.push('/admin/toolbox-talks/learnings/new')}
        >
          New learning
        </Button>
      </div>

      {isLoading && <LoadingSkeleton />}

      {isError && (
        <div className="rounded-lg border border-destructive/40 bg-destructive/5 p-4 text-sm text-destructive">
          {error?.message ?? 'Failed to load drafts.'}
          <Button
            size="sm"
            variant="ghost"
            className="ml-2"
            onClick={() => refetch()}
          >
            Retry
          </Button>
        </div>
      )}

      {!isLoading && !isError && drafts.length === 0 && (
        <EmptyState onNew={() => router.push('/admin/toolbox-talks/learnings/new')} />
      )}

      {!isLoading && !isError && drafts.length > 0 && (
        <div className="space-y-3">
          {drafts.map((draft) => (
            <DraftRow
              key={draft.id}
              draft={draft}
              onDelete={setDeleteTarget}
              onResume={handleResume}
            />
          ))}
        </div>
      )}

      <DeleteDraftDialog
        open={deleteTarget !== null}
        onOpenChange={(open) => { if (!open) setDeleteTarget(null); }}
        talkTitle={deleteTarget?.title ?? ''}
        lastEditedAt={deleteTarget?.createdAt ?? null}
        onConfirm={handleDeleteConfirm}
        isDeleting={deleteDraft.isPending}
      />
    </div>
  );
}
