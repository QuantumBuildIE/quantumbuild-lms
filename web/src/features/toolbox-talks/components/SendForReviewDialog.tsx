'use client';

import { useEffect, useState } from 'react';
import { AlertTriangle, CheckCircle2, Mail, XCircle } from 'lucide-react';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { useSendForReviewPreview, useSendForReview } from '@/lib/api/toolbox-talks';
import type { BlockedLanguageDto, PreviewLanguageDto } from '@/types/toolbox-talks';
import { toast } from 'sonner';
import { cn } from '@/lib/utils';

// Mirrors the LANG_NAMES convention used in WizardTranslationPanel — this codebase does not
// have a shared language-name lookup, so each consumer keeps a local copy.
const LANG_NAMES: Record<string, string> = {
  fr: 'French',
  pl: 'Polish',
  ro: 'Romanian',
  uk: 'Ukrainian',
  pt: 'Portuguese',
  es: 'Spanish',
  lt: 'Lithuanian',
  de: 'German',
  lv: 'Latvian',
};

function languageName(code: string): string {
  return LANG_NAMES[code] ?? code.toUpperCase();
}

interface SendForReviewDialogProps {
  talkId: string;
  talkTitle: string;
  isOpen: boolean;
  onOpenChange: (open: boolean) => void;
}

// Helper to extract error message from various error types — mirrors the convention in
// SubtitleProcessingPanel.tsx.
function getErrorMessage(err: unknown): string {
  if (err instanceof Error) {
    const axiosError = err as { response?: { data?: { error?: string; Error?: string } } };
    if (axiosError.response?.data?.error) {
      return axiosError.response.data.error;
    }
    if (axiosError.response?.data?.Error) {
      return axiosError.response.data.Error;
    }
    return err.message;
  }
  return 'An unexpected error occurred';
}

function getBlockedLanguagesFromError(err: unknown): BlockedLanguageDto[] | null {
  const axiosError = err as { response?: { status?: number; data?: { blockedLanguages?: BlockedLanguageDto[] } } };
  if (axiosError.response?.status === 409 && axiosError.response.data?.blockedLanguages) {
    return axiosError.response.data.blockedLanguages;
  }
  return null;
}

export function SendForReviewDialog({ talkId, talkTitle, isOpen, onOpenChange }: SendForReviewDialogProps) {
  const {
    data: preview,
    isLoading,
    isError,
    error: previewError,
    refetch,
  } = useSendForReviewPreview(talkId, isOpen);

  const sendMutation = useSendForReview();

  const [sendErrorMessage, setSendErrorMessage] = useState<string | null>(null);
  const [sendBlockedLanguages, setSendBlockedLanguages] = useState<BlockedLanguageDto[] | null>(null);

  // Reset transient send-failure state each time the dialog is (re)opened.
  useEffect(() => {
    if (isOpen) {
      setSendErrorMessage(null);
      setSendBlockedLanguages(null);
    }
  }, [isOpen]);

  const handleSend = async () => {
    setSendErrorMessage(null);
    setSendBlockedLanguages(null);
    try {
      const result = await sendMutation.mutateAsync(talkId);
      const successCount = result.languageResults.filter((r) => r.success).length;
      const failCount = result.languageResults.length - successCount;
      onOpenChange(false);
      if (failCount === 0) {
        toast.success('Review invitations sent', {
          description: `${successCount} language${successCount === 1 ? '' : 's'} sent for external review.`,
        });
      } else {
        toast.error('Some review invitations failed', {
          description: `${successCount} sent, ${failCount} failed. Check the learning's validation history for details.`,
        });
      }
    } catch (err) {
      const blockedLanguages = getBlockedLanguagesFromError(err);
      if (blockedLanguages) {
        setSendBlockedLanguages(blockedLanguages);
      } else {
        setSendErrorMessage(getErrorMessage(err));
      }
    }
  };

  const isSending = sendMutation.isPending;
  const isBlocked = !!preview?.blocked || !!sendBlockedLanguages;

  return (
    <Dialog open={isOpen} onOpenChange={(open) => !isSending && onOpenChange(open)}>
      <DialogContent className="sm:max-w-[550px]">
        <DialogHeader>
          <DialogTitle>Send for Review</DialogTitle>
          <DialogDescription>
            <span className="font-medium text-foreground">{talkTitle}</span> has sections that
            failed translation validation. External review invitations will be sent per language.
          </DialogDescription>
        </DialogHeader>

        {isLoading && (
          <div className="space-y-2 py-2">
            <Skeleton className="h-16 w-full" />
            <Skeleton className="h-16 w-full" />
          </div>
        )}

        {isError && !isLoading && (
          <div className="space-y-3 py-2">
            <Alert variant="destructive">
              <XCircle className="h-4 w-4" />
              <AlertDescription>{getErrorMessage(previewError)}</AlertDescription>
            </Alert>
            <Button type="button" variant="outline" size="sm" onClick={() => refetch()}>
              Retry
            </Button>
          </div>
        )}

        {!isLoading && !isError && preview && preview.languages.length === 0 && (
          <Alert className="border-muted-foreground/30">
            <AlertDescription>
              No failures to send — every section has passed validation since this list was
              loaded.
            </AlertDescription>
          </Alert>
        )}

        {!isLoading && !isError && preview && preview.languages.length > 0 && (
          <div className="space-y-3 py-2">
            {isBlocked && (
              <Alert variant="destructive">
                <AlertTriangle className="h-4 w-4" />
                <AlertDescription>
                  Cannot send — one or more languages below are missing a reviewer or aren&apos;t
                  ready for review. Resolve these before sending.
                </AlertDescription>
              </Alert>
            )}

            <div className="space-y-2">
              {preview.languages.map((language) => (
                <LanguageRow
                  key={language.languageCode}
                  language={language}
                  blockedOverride={sendBlockedLanguages?.find((b) => b.languageCode === language.languageCode)}
                />
              ))}
            </div>

            {!isBlocked && (
              <p className="text-sm text-muted-foreground">
                Sending {preview.languages.reduce((sum, l) => sum + l.failingSectionCount, 0)} section
                {preview.languages.reduce((sum, l) => sum + l.failingSectionCount, 0) === 1 ? '' : 's'} across{' '}
                {preview.languages.length} language{preview.languages.length === 1 ? '' : 's'} to{' '}
                {new Set(preview.languages.map((l) => l.resolvedReviewerEmail)).size} reviewer
                {new Set(preview.languages.map((l) => l.resolvedReviewerEmail)).size === 1 ? '' : 's'}.
              </p>
            )}

            {sendErrorMessage && (
              <Alert variant="destructive">
                <XCircle className="h-4 w-4" />
                <AlertDescription>{sendErrorMessage}</AlertDescription>
              </Alert>
            )}
          </div>
        )}

        <DialogFooter>
          <Button type="button" variant="outline" onClick={() => onOpenChange(false)} disabled={isSending}>
            {isBlocked || (preview && preview.languages.length === 0) ? 'Close' : 'Cancel'}
          </Button>
          {preview && preview.languages.length > 0 && (
            <Button type="button" onClick={handleSend} disabled={isSending || isBlocked || isLoading || isError}>
              {isSending ? 'Sending…' : 'Send for review'}
            </Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function LanguageRow({
  language,
  blockedOverride,
}: {
  language: PreviewLanguageDto;
  blockedOverride?: BlockedLanguageDto;
}) {
  const reviewerMissing = blockedOverride?.reviewerMissing ?? !language.resolvedReviewerEmail;
  const workflowIneligible = blockedOverride?.workflowStateIneligible ?? !language.workflowStateEligible;
  const isRowBlocked = reviewerMissing || workflowIneligible;

  return (
    <div
      className={cn(
        'rounded-md border p-3',
        isRowBlocked
          ? 'border-destructive/40 bg-destructive/5'
          : 'border-border'
      )}
    >
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0">
          <div className="flex items-center gap-2">
            <span className="font-medium">{languageName(language.languageCode)}</span>
            {language.resolutionSource === 'Fallback' && !reviewerMissing && (
              <Badge variant="outline" className="text-xs font-normal text-muted-foreground">
                Fallback reviewer
              </Badge>
            )}
          </div>
          <p className="text-sm text-muted-foreground">
            {language.failingSectionCount} section{language.failingSectionCount === 1 ? '' : 's'} failed
          </p>
        </div>
        {isRowBlocked ? (
          <XCircle className="h-4 w-4 shrink-0 text-destructive mt-0.5" />
        ) : (
          <CheckCircle2 className="h-4 w-4 shrink-0 text-green-600 dark:text-green-500 mt-0.5" />
        )}
      </div>

      <div className="mt-2 space-y-1 text-sm">
        <div className="flex items-center gap-1.5">
          <Mail className="h-3.5 w-3.5 text-muted-foreground shrink-0" />
          {reviewerMissing ? (
            <span className="text-destructive">No reviewer configured</span>
          ) : (
            <span>
              {language.resolvedReviewerName ? `${language.resolvedReviewerName} · ` : ''}
              {language.resolvedReviewerEmail}
            </span>
          )}
        </div>
        {workflowIneligible && (
          <p className="text-destructive text-xs">
            Language not ready for review — it may already be under review, or hasn&apos;t been
            validated yet.
          </p>
        )}
        {reviewerMissing && (
          <p className="text-destructive text-xs">
            Add a reviewer for this language in Settings → Reviewers.
          </p>
        )}
      </div>

      {language.failingSections.length > 0 && (
        <div className="mt-2 space-y-1 border-t pt-2">
          {language.failingSections.map((section) => (
            <div key={section.index} className="flex items-center justify-between gap-2 text-xs">
              <span className="truncate text-muted-foreground">
                Section {section.index + 1}
                {section.title ? `: ${section.title}` : ''}
              </span>
              <span className="shrink-0 tabular-nums font-semibold text-red-700 dark:text-red-500">
                {section.score}
              </span>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
