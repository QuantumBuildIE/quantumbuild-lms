'use client';

import { useState, useEffect, useCallback, useRef } from 'react';
import { Button } from '@/components/ui/button';
import { Alert, AlertDescription } from '@/components/ui/alert';
import {
  Loader2,
  ArrowRight,
  ArrowLeft,
  RefreshCw,
  AlertTriangle,
  Info,
} from 'lucide-react';
import { toast } from 'sonner';
import { cn } from '@/lib/utils';
import {
  useCreationSession,
  useParseContent,
  useUpdateSections,
} from '@/lib/api/toolbox-talks/use-content-creation';
import type { WizardState } from '../CreateWizard';
import type {
  ParsedSection,
  OutputType,
  ContentCreationSessionStatus,
} from '@/types/content-creation';
import { AiSuggestionBanner } from './parse/AiSuggestionBanner';
import { OutputTypeSelector } from './parse/OutputTypeSelector';
import { SectionList } from './parse/SectionList';
import { ParseLogPanel } from './parse/ParseLogPanel';

/** Normalize section keys from PascalCase (legacy) or camelCase to the expected shape */
function normalizeSections(raw: Record<string, unknown>[]): ParsedSection[] {
  return raw.map((s, i) => ({
    title: (s.title ?? s.Title ?? 'Untitled') as string,
    content: (s.content ?? s.Content ?? '') as string,
    suggestedOrder: (s.suggestedOrder ?? s.SuggestedOrder ?? i) as number,
  }));
}

// ============================================
// Props
// ============================================

interface ParseStepProps {
  state: WizardState;
  updateState: (updates: Partial<WizardState>) => void;
  onNext: () => void;
  onBack: () => void;
}

// ============================================
// Parse log entry
// ============================================

interface ParseLogEntry {
  timestamp: Date;
  message: string;
}

// ============================================
// Component
// ============================================

export function ParseStep({ state, updateState, onNext, onBack }: ParseStepProps) {
  const parseContent = useParseContent();
  const updateSections = useUpdateSections();

  // Polling query — enabled only while parsing
  const [isPolling, setIsPolling] = useState(false);
  const { data: session, refetch } = useCreationSession(state.sessionId);

  // Local state
  const [parseLog, setParseLog] = useState<ParseLogEntry[]>([]);
  const [hasParsed, setHasParsed] = useState(false);
  const [isParseFailed, setIsParseFailed] = useState(false);
  const [isTakingLong, setIsTakingLong] = useState(false);
  const parseTriggered = useRef(false);
  const pollIntervalRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const pollStartRef = useRef<number | null>(null);
  const slowTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const mountedRef = useRef(true);

  // Polling timeout: 90 seconds to accommodate Polly retries (3 × 8s backoff)
  const POLL_TIMEOUT_MS = 90_000;
  // Show "taking longer than usual" after 15 seconds
  const SLOW_THRESHOLD_MS = 15_000;

  // Derived state
  const sessionStatus = session?.status ?? state.parsedSections.length > 0 ? 'Parsed' : 'Draft';
  const sections = state.parsedSections;

  // ============================================
  // Log helper
  // ============================================

  const addLog = useCallback((message: string) => {
    setParseLog((prev) => [...prev, { timestamp: new Date(), message }]);
  }, []);

  // ============================================
  // Trigger parse on mount
  // ============================================

  useEffect(() => {
    if (!state.sessionId) return;

    const status = session?.status;

    // Already parsed — hydrate sections from session. This must run even
    // when parseTriggered is true so that the synchronous mutation response
    // (which updates the query cache) triggers hydration.
    if (status === 'Parsed' && session?.parsedSectionsJson && !hasParsed) {
      parseTriggered.current = true;
      stopPolling();
      hydrateFromSession();
      return;
    }

    // Guard: only trigger parse/polling once
    if (parseTriggered.current) return;

    // Already parsing — just start polling
    if (status === 'Parsing') {
      parseTriggered.current = true;
      addLog('Resuming — parse already in progress...');
      startPolling();
      return;
    }

    // Draft — trigger parse
    if (status === 'Draft') {
      parseTriggered.current = true;
      triggerParse();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [state.sessionId, session?.status, hasParsed]);

  // ============================================
  // Cleanup polling on unmount
  // ============================================

  useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
      if (pollIntervalRef.current) {
        clearInterval(pollIntervalRef.current);
      }
      if (slowTimerRef.current) {
        clearTimeout(slowTimerRef.current);
      }
    };
  }, []);

  // ============================================
  // Hydrate sections from session JSON
  // ============================================

  function hydrateFromSession() {
    if (!session?.parsedSectionsJson) return;

    try {
      const raw = JSON.parse(session.parsedSectionsJson);
      const parsed = normalizeSections(raw);
      const outputType = session.outputType ?? 'Lesson';

      updateState({
        parsedSections: parsed,
        suggestedOutputType: 'Lesson' as OutputType,
        selectedOutputType: state.selectedOutputType ?? outputType,
      });

      addLog(`Loaded ${parsed.length} sections from previous parse`);
      setHasParsed(true);
    } catch {
      addLog('Failed to load previously parsed sections');
      setIsParseFailed(true);
    }
  }

  // ============================================
  // Trigger parse API call
  // ============================================

  async function triggerParse() {
    if (!state.sessionId) return;

    setIsParseFailed(false);
    setIsTakingLong(false);
    setParseLog([]);
    addLog('Starting content parse...');
    addLog('Parsing document structure...');

    try {
      const response = await parseContent.mutateAsync(state.sessionId);

      // The parse endpoint is synchronous — the response already contains the
      // parsed data. Use it directly instead of polling.
      if (response.status === 'Parsed' && response.parsedSectionsJson) {
        handleParseComplete(response.parsedSectionsJson, response.outputType);
      } else {
        // Fallback: if the endpoint ever becomes async, poll for results.
        addLog('Parse request submitted — waiting for results...');
        startPolling();
      }
    } catch {
      addLog('Error: Parse request failed');
      setIsParseFailed(true);
    }
  }

  // ============================================
  // Polling
  // ============================================

  function startPolling() {
    setIsPolling(true);
    setIsTakingLong(false);
    pollStartRef.current = Date.now();

    if (pollIntervalRef.current) {
      clearInterval(pollIntervalRef.current);
    }
    if (slowTimerRef.current) {
      clearTimeout(slowTimerRef.current);
    }

    // Show "taking longer than usual" after 15 seconds
    slowTimerRef.current = setTimeout(() => {
      if (mountedRef.current) {
        setIsTakingLong(true);
      }
    }, SLOW_THRESHOLD_MS);

    pollIntervalRef.current = setInterval(async () => {
      if (!mountedRef.current) return;

      // Check polling timeout (90 seconds)
      const elapsed = Date.now() - (pollStartRef.current ?? Date.now());
      if (elapsed >= POLL_TIMEOUT_MS) {
        stopPolling();
        addLog('Timed out waiting for parse results');
        setIsParseFailed(true);
        return;
      }

      try {
        const { data: freshSession } = await refetch();
        if (!mountedRef.current || !freshSession) return;

        const status = freshSession.status as ContentCreationSessionStatus;

        if (status === 'Parsed') {
          stopPolling();
          handleParseComplete(freshSession.parsedSectionsJson, freshSession.outputType);
        } else if (status === 'Failed') {
          stopPolling();
          addLog('Parse failed — the server encountered an error');
          setIsParseFailed(true);
        }
      } catch {
        // Silently retry on network errors during polling
      }
    }, 2000);
  }

  function stopPolling() {
    setIsPolling(false);
    setIsTakingLong(false);
    pollStartRef.current = null;
    if (pollIntervalRef.current) {
      clearInterval(pollIntervalRef.current);
      pollIntervalRef.current = null;
    }
    if (slowTimerRef.current) {
      clearTimeout(slowTimerRef.current);
      slowTimerRef.current = null;
    }
  }

  function handleParseComplete(sectionsJson: string | null, outputType: OutputType | null) {
    if (!sectionsJson) {
      addLog('Parse completed but no sections were extracted');
      setIsParseFailed(true);
      return;
    }

    try {
      const raw = JSON.parse(sectionsJson);
      const parsed = normalizeSections(raw);
      const suggested: OutputType = 'Lesson';

      updateState({
        parsedSections: parsed,
        suggestedOutputType: suggested,
        selectedOutputType: state.selectedOutputType ?? outputType ?? suggested,
      });

      addLog(`Found ${parsed.length} sections`);
      addLog('Parse complete');
      setHasParsed(true);
      toast.success(`Parsed ${parsed.length} sections`);
    } catch {
      addLog('Failed to parse section data');
      setIsParseFailed(true);
    }
  }

  // ============================================
  // Retry
  // ============================================

  function handleRetry() {
    // Keep session and uploaded file intact — only clear parse results
    setHasParsed(false);
    setIsParseFailed(false);
    setIsTakingLong(false);
    updateState({
      parsedSections: [],
      suggestedOutputType: null,
      selectedOutputType: null,
    });
    triggerParse();
  }

  // ============================================
  // Section handlers
  // ============================================

  const handleSectionsChange = useCallback(
    (updated: ParsedSection[]) => {
      updateState({
        parsedSections: updated.map((s, i) => ({ ...s, suggestedOrder: i })),
      });
    },
    [updateState]
  );

  const handleOutputTypeChange = useCallback(
    (type: OutputType) => {
      updateState({ selectedOutputType: type });
    },
    [updateState]
  );

  // ============================================
  // Continue — save sections then trigger translate-validate
  // ============================================

  const handleContinue = async () => {
    if (!state.sessionId || sections.length === 0) return;
    const outputType = state.selectedOutputType ?? state.suggestedOutputType ?? 'Lesson';

    try {
      // Save sections
      await updateSections.mutateAsync({
        sessionId: state.sessionId,
        request: {
          sections: sections.map((s, i) => ({
            title: s.title,
            content: s.content,
            order: i,
          })),
          outputType,
        },
      });

      onNext();
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Failed to proceed';
      toast.error('Error', { description: message });
    }
  };

  // Once sections have been parsed and hydrated, neither stale polling state nor
  // a lingering mutation pending state (from React strict-mode double-mount) should
  // block the Continue button. Only the save-sections mutation matters at that point.
  const isBusy = hasParsed
    ? updateSections.isPending
    : parseContent.isPending || updateSections.isPending || isPolling;

  const isParsing = parseContent.isPending || isPolling;
  const selectedType = state.selectedOutputType ?? state.suggestedOutputType;

  // ============================================
  // Render
  // ============================================

  return (
    <div className="space-y-6">
      {/* Parse Log — shown while parsing or on failure */}
      {(isParsing || isParseFailed || (parseLog.length > 0 && !hasParsed)) && (
        <ParseLogPanel
          entries={parseLog}
          isActive={isParsing}
          isFailed={isParseFailed}
        />
      )}

      {/* Slow-state hint while still polling */}
      {isParsing && isTakingLong && (
        <p className="text-sm text-muted-foreground text-center animate-pulse">
          This is taking longer than usual — still working…
        </p>
      )}

      {/* Failed state with retry */}
      {isParseFailed && !isParsing && (
        <Alert variant="destructive">
          <AlertTriangle className="h-4 w-4" />
          <AlertDescription className="flex items-center justify-between">
            <span>
              We had trouble processing your content. This can happen during high demand. Please try again.
            </span>
            <Button variant="outline" size="sm" onClick={handleRetry}>
              <RefreshCw className="mr-2 h-3 w-3" />
              Try Again
            </Button>
          </AlertDescription>
        </Alert>
      )}

      {/* Parsed content — shown after successful parse */}
      {hasParsed && sections.length > 0 && (
        <>
          {/* AI Suggestion Banner */}
          <AiSuggestionBanner
            sectionCount={sections.length}
            suggestedType={state.suggestedOutputType}
            selectedType={state.selectedOutputType}
          />

          {/* Output Type Selector */}
          <OutputTypeSelector
            suggestedType={state.suggestedOutputType}
            selectedType={selectedType}
            onSelect={handleOutputTypeChange}
          />

          {/* Video + Course informational banner */}
          {state.inputMode === 'Video' && selectedType === 'Course' && (
            <Alert>
              <Info className="h-4 w-4" />
              <AlertDescription>
                The full video will be added as the first learning in the course.
                Each section below will become a separate text-based learning that follows it.
              </AlertDescription>
            </Alert>
          )}

          {/* Section List */}
          <SectionList
            sections={sections}
            onChange={handleSectionsChange}
          />
        </>
      )}

      {/* Bottom Bar */}
      <div className="flex items-center justify-between border-t pt-4">
        <Button variant="outline" onClick={onBack} disabled={isBusy}>
          <ArrowLeft className="mr-2 h-4 w-4" />
          Back
        </Button>

        <div className="flex items-center gap-4">
          {/* Summary info */}
          {hasParsed && sections.length > 0 && (
            <span className="text-sm text-muted-foreground">
              {sections.length} section{sections.length !== 1 ? 's' : ''}
            </span>
          )}

          <Button
            onClick={handleContinue}
            disabled={
              !hasParsed ||
              sections.length === 0 ||
              isBusy
            }
          >
            {updateSections.isPending ? (
              <>
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                Saving...
              </>
            ) : (
              <>
                Continue
                <ArrowRight className="ml-2 h-4 w-4" />
              </>
            )}
          </Button>
        </div>
      </div>
    </div>
  );
}
