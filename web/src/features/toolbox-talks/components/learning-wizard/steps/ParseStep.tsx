'use client';

import { useState, useEffect, useRef, useCallback } from 'react';
import { toast } from 'sonner';
import { Loader2, Wand2, AlertTriangle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { SectionList, toSectionDrafts, type SectionDraft } from '../components/SectionList';
import { useParseTalk } from '../hooks/useParseTalk';
import { useUpdateTalkSections } from '../hooks/useUpdateTalkSections';
import { useTalkStatusPolling } from '../hooks/useTalkStatusPolling';
import type { ToolboxTalk } from '@/types/toolbox-talks';

// ============================================
// Props
// ============================================

export interface ParseStepProps {
  talkId: string;
  /** Called after sections are saved — typically navigates to Step 3. */
  onContinue: () => void | Promise<void>;
}

// ============================================
// Helpers
// ============================================

function inputModeLabel(talk: ToolboxTalk | undefined): string {
  switch (talk?.inputMode) {
    case 'Pdf':   return 'document';
    case 'Video': return 'video';
    default:      return 'text';
  }
}

// ============================================
// Component
// ============================================

export function ParseStep({ talkId, onContinue }: ParseStepProps) {
  const { data: talk, isLoading } = useTalkStatusPolling(talkId, true);
  const parseMutation = useParseTalk(talkId);
  const updateSectionsMutation = useUpdateTalkSections(talkId);

  // Local editable section state
  const [sections, setSections] = useState<SectionDraft[]>([]);
  // Guard: only auto-init once from server state (not on every poll re-render)
  const initializedRef = useRef(false);
  // Track previous status to detect Processing → Draft transition (video mode)
  const prevStatusRef = useRef<string | null>(null);

  // Initialize sections from an already-parsed talk (re-entering step or video completion)
  useEffect(() => {
    if (!talk) return;

    const current = talk.status;
    const prev = prevStatusRef.current;
    prevStatusRef.current = current;

    // Video mode: detect Processing → Draft transition — sections just arrived
    if (prev === 'Processing' && current === 'Draft' && talk.sections.length > 0) {
      setSections(toSectionDrafts(talk.sections));
      initializedRef.current = true;
      return;
    }

    // Initial load (re-entering step): sections already exist in the talk
    if (!initializedRef.current && current === 'Draft' && talk.sections.length > 0) {
      setSections(toSectionDrafts(talk.sections));
      initializedRef.current = true;
    }
  }, [talk]);

  // After a sync parse (Text / PDF), initialize sections from mutation result
  useEffect(() => {
    if (parseMutation.data && parseMutation.data.sections.length > 0) {
      setSections(toSectionDrafts(parseMutation.data.sections));
      initializedRef.current = true;
    }
  }, [parseMutation.data]);

  const handleParse = useCallback(async () => {
    try {
      initializedRef.current = false; // allow re-init after parse
      prevStatusRef.current = null;
      await parseMutation.mutateAsync();
    } catch {
      toast.error('Failed to start parsing. Please try again.');
    }
  }, [parseMutation]);

  // No cascade-reset needed at Step 2 — quiz, settings, validation, and translations don't exist yet when sections are saved. The old wizard's cascade-reset UI is intentionally not carried over.
  const handleContinue = useCallback(async () => {
    if (sections.length === 0) {
      toast.error('Add at least one section before continuing.');
      return;
    }
    try {
      await updateSectionsMutation.mutateAsync(
        sections.map((s, i) => ({
          id: s.id,
          sectionNumber: i + 1,
          title: s.title,
          content: s.content,
          requiresAcknowledgment: s.requiresAcknowledgment,
          source: s.source,
        }))
      );
      await onContinue();
    } catch {
      toast.error('Failed to save sections. Please try again.');
    }
  }, [sections, updateSectionsMutation, onContinue]);

  // ── Derived state ─────────────────────────────────────────────────────────

  const isProcessing = talk?.status === 'Processing';
  const isParsing = parseMutation.isPending || isProcessing;
  const hasSections = sections.length > 0;
  const isReady = !isLoading && !isParsing && !parseMutation.isError;
  const isSaving = updateSectionsMutation.isPending;

  // ── Loading skeleton ──────────────────────────────────────────────────────

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-16 text-muted-foreground">
        <Loader2 className="mr-2 h-5 w-5 animate-spin" aria-hidden="true" />
        <span>Loading…</span>
      </div>
    );
  }

  // ── In-progress parse (video background job) ──────────────────────────────

  if (isParsing) {
    return (
      <div
        role="status"
        aria-live="polite"
        aria-label="Parsing content"
        className="flex flex-col items-center justify-center gap-4 py-16 text-center"
      >
        <Loader2 className="h-8 w-8 animate-spin text-primary" aria-hidden="true" />
        <div>
          <p className="text-sm font-medium">
            {isProcessing
              ? 'Transcribing and parsing your video…'
              : 'Parsing your content…'}
          </p>
          <p className="mt-1 text-xs text-muted-foreground">
            {isProcessing
              ? 'This can take a few minutes. The page will update automatically.'
              : 'Generating sections from your content.'}
          </p>
        </div>
      </div>
    );
  }

  // ── Parse error ────────────────────────────────────────────────────────────

  if (parseMutation.isError) {
    return (
      <div className="space-y-4">
        <Alert variant="destructive" role="alert">
          <AlertTriangle className="h-4 w-4" aria-hidden="true" />
          <AlertDescription>
            {parseMutation.error?.message ?? 'Parsing failed. Please try again.'}
          </AlertDescription>
        </Alert>
        <Button
          type="button"
          variant="outline"
          onClick={() => {
            parseMutation.reset();
            handleParse();
          }}
        >
          Retry parsing
        </Button>
      </div>
    );
  }

  // ── No sections yet — trigger parse ───────────────────────────────────────

  if (!hasSections) {
    return (
      <div className="flex flex-col items-center justify-center gap-4 py-16 text-center">
        <div className="rounded-full border p-3 text-muted-foreground">
          <Wand2 className="h-6 w-6" aria-hidden="true" />
        </div>
        <div>
          <p className="text-sm font-medium">Ready to extract sections</p>
          <p className="mt-1 text-xs text-muted-foreground">
            The AI will read your {inputModeLabel(talk)} and generate structured sections.
          </p>
        </div>
        <Button
          type="button"
          onClick={handleParse}
          className="min-h-[44px] min-w-[140px]"
        >
          <Wand2 className="mr-2 h-4 w-4" aria-hidden="true" />
          Parse Content
        </Button>
      </div>
    );
  }

  // ── Sections ready — show editor ───────────────────────────────────────────

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-base font-semibold">Sections</h2>
          <p className="text-xs text-muted-foreground mt-0.5">
            {sections.length} section{sections.length !== 1 ? 's' : ''} — reorder, edit, or delete before continuing.
          </p>
        </div>
      </div>

      <SectionList
        sections={sections}
        onChange={setSections}
        disabled={isSaving}
      />

      {/* Save & Continue */}
      <div className="flex justify-end pt-2">
        <Button
          type="button"
          onClick={handleContinue}
          disabled={isSaving || sections.length === 0}
          aria-busy={isSaving}
          className="min-h-[44px] min-w-[160px]"
        >
          {isSaving ? (
            <>
              <Loader2 className="mr-2 h-4 w-4 animate-spin" aria-hidden="true" />
              Saving…
            </>
          ) : (
            'Save & Continue'
          )}
        </Button>
      </div>
    </div>
  );
}
