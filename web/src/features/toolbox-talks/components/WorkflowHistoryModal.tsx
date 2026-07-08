'use client';

import { format } from 'date-fns';
import { Circle, Loader2 } from 'lucide-react';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { useWorkflowHistory } from '@/lib/api/toolbox-talks';

interface WorkflowHistoryModalProps {
  toolboxTalkId: string;
  languageCode: string | null;
  languageName: string | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

const EVENT_TYPE_LABELS: Record<string, string> = {
  TranslationStarted: 'Translation started',
  TranslationCompleted: 'Translation completed',
  ValidationStarted: 'Validation started',
  ValidationCompleted: 'Validation completed',
  InternalReviewSubmitted: 'Internal review submitted',
  ExternalReviewInitiated: 'External review invited',
  ExternalReviewSubmitted: 'External review submitted',
  ExternalReviewRejected: 'External review rejected',
  AcceptedAsFinal: 'Accepted as final',
  MarkedStale: 'Marked stale',
};

function eventLabel(eventType: string): string {
  return EVENT_TYPE_LABELS[eventType] ?? eventType;
}

function HistoryBody({
  toolboxTalkId,
  languageCode,
}: {
  toolboxTalkId: string;
  languageCode: string;
}) {
  const { data: events, isLoading } = useWorkflowHistory(toolboxTalkId, languageCode);

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-8">
        <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (!events || events.length === 0) {
    return (
      <p className="py-6 text-center text-sm text-muted-foreground">
        No events recorded for this language yet.
      </p>
    );
  }

  return (
    <div className="space-y-2">
      {events.map((event, idx) => (
        <div key={idx} className="flex items-start gap-3 rounded-md border p-3">
          <Circle className="mt-0.5 h-3.5 w-3.5 shrink-0 text-muted-foreground" />
          <div className="min-w-0 flex-1">
            <p className="text-sm font-medium">{eventLabel(event.eventType)}</p>
            <p className="text-xs text-muted-foreground">
              {event.triggeredByType === 'User' ? 'by User' : 'by System'}
              {' · '}
              {format(new Date(event.occurredAt), 'dd MMM yyyy HH:mm')}
            </p>
          </div>
        </div>
      ))}
    </div>
  );
}

export function WorkflowHistoryModal({
  toolboxTalkId,
  languageCode,
  languageName,
  open,
  onOpenChange,
}: WorkflowHistoryModalProps) {
  const displayName = languageName ?? languageCode ?? '';

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[80vh] max-w-lg flex flex-col">
        <DialogHeader>
          <DialogTitle>History — {displayName}</DialogTitle>
          <DialogDescription>
            Workflow events recorded for this language, ordered oldest to newest.
          </DialogDescription>
        </DialogHeader>
        <div className="overflow-y-auto flex-1 pr-1">
          {languageCode ? (
            <HistoryBody toolboxTalkId={toolboxTalkId} languageCode={languageCode} />
          ) : null}
        </div>
      </DialogContent>
    </Dialog>
  );
}
