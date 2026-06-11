'use client';

import { useState, useEffect, useRef, useCallback } from 'react';
import { toast } from 'sonner';
import { AlertTriangle, Loader2, Wand2 } from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { SectionQuestionGroup } from '../components/SectionQuestionGroup';
import { QuizSettingsPanel } from '../components/QuizSettingsPanel';
import { useGenerateQuiz } from '../hooks/useGenerateQuiz';
import { useUpdateTalkQuestions } from '../hooks/useUpdateTalkQuestions';
import { useUpdateQuizSettings } from '../hooks/useUpdateQuizSettings';
import { useTalkStatusPolling } from '../hooks/useTalkStatusPolling';
import type { ToolboxTalkQuestion } from '@/types/toolbox-talks';
import type { QuestionFormData } from '../schemas/questionSchema';
import type { UpdateTalkQuizSettingsRequest } from '@/lib/api/toolbox-talks/toolbox-talks';

export interface QuizStepProps {
  talkId: string;
  onContinue: () => void | Promise<void>;
}

export function QuizStep({ talkId, onContinue }: QuizStepProps) {
  const { data: talk, isLoading } = useTalkStatusPolling(talkId, true);
  const generateMutation = useGenerateQuiz(talkId);
  const updateQuestionsMutation = useUpdateTalkQuestions(talkId);
  const updateSettingsMutation = useUpdateQuizSettings(talkId);

  const [questions, setQuestions] = useState<ToolboxTalkQuestion[]>([]);
  // Guard: only auto-init once from server state
  const initializedRef = useRef(false);

  // Initialize questions from server state (re-entering step or after generation)
  useEffect(() => {
    if (!talk) return;
    if (initializedRef.current) return;
    if (talk.status !== 'Draft') return;
    if (talk.questions.length > 0) {
      setQuestions(talk.questions);
      initializedRef.current = true;
    }
  }, [talk]);

  // After a successful generate, sync the questions from mutation result
  useEffect(() => {
    if (generateMutation.data?.questions) {
      setQuestions(generateMutation.data.questions);
      initializedRef.current = true;
    }
  }, [generateMutation.data]);

  const handleGenerateQuiz = useCallback(async () => {
    try {
      initializedRef.current = false;
      await generateMutation.mutateAsync();
    } catch {
      toast.error('Failed to generate quiz. Please try again.');
    }
  }, [generateMutation]);

  const handleSaveQuestion = useCallback(
    (index: number, data: QuestionFormData) => {
      setQuestions((prev) => {
        const next = [...prev];
        const existing = next[index];
        next[index] = {
          ...existing,
          questionText: data.questionText,
          questionType: data.questionType,
          options: data.options ?? null,
          correctOptionIndex: data.correctOptionIndex ?? null,
          correctAnswer: data.options && data.correctOptionIndex != null
            ? data.options[data.correctOptionIndex]
            : data.correctAnswer ?? null,
          points: data.points,
          source: (data.source as ToolboxTalkQuestion['source']) ?? 'Manual',
          isFromVideoFinalPortion: data.isFromVideoFinalPortion ?? false,
          videoTimestamp: data.videoTimestamp ?? null,
        };
        return next;
      });
    },
    []
  );

  const handleDeleteQuestion = useCallback((index: number) => {
    setQuestions((prev) => {
      const next = prev.filter((_, i) => i !== index);
      return next.map((q, i) => ({ ...q, questionNumber: i + 1 }));
    });
  }, []);

  const handleAddQuestion = useCallback(() => {
    const newQ: ToolboxTalkQuestion = {
      id: '',
      toolboxTalkId: talkId,
      questionNumber: questions.length + 1,
      questionText: '',
      questionType: 'MultipleChoice',
      questionTypeDisplay: 'Multiple Choice',
      options: ['', '', '', ''],
      correctAnswer: null,
      correctOptionIndex: 0,
      points: 1,
      source: 'Manual',
      isFromVideoFinalPortion: false,
      videoTimestamp: null,
    };
    setQuestions((prev) => [...prev, newQ]);
  }, [questions.length, talkId]);

  const handleSaveSettings = useCallback(
    async (settings: UpdateTalkQuizSettingsRequest) => {
      try {
        await updateSettingsMutation.mutateAsync(settings);
      } catch {
        toast.error('Failed to save quiz settings.');
      }
    },
    [updateSettingsMutation]
  );

  // No cascade-reset needed at Step 3 — settings, validation, and translations don't exist yet when questions are saved. The old wizard's cascade-reset UI is intentionally not carried over.
  const handleContinue = useCallback(async () => {
    if (questions.length === 0) {
      toast.error('Add at least one question before continuing.');
      return;
    }

    // Map local state → API request shape
    const payload = questions.map((q, i) => ({
      id: q.id || undefined,
      questionNumber: i + 1,
      questionText: q.questionText,
      questionType: q.questionType,
      options: q.options,
      correctAnswer: q.correctAnswer,
      correctOptionIndex: q.correctOptionIndex,
      points: q.points,
      source: q.source ?? 'Manual',
      isFromVideoFinalPortion: q.isFromVideoFinalPortion ?? false,
      videoTimestamp: q.videoTimestamp ?? null,
    }));

    try {
      await updateQuestionsMutation.mutateAsync(payload);
      await onContinue();
    } catch {
      toast.error('Failed to save questions. Please try again.');
    }
  }, [questions, updateQuestionsMutation, onContinue]);

  // ── Derived state ──────────────────────────────────────────────────────────

  const isProcessing = talk?.status === 'Processing';
  const isGenerating = generateMutation.isPending || isProcessing;
  const isSaving = updateQuestionsMutation.isPending;
  const hasQuestions = questions.length > 0;

  // ── Loading skeleton ───────────────────────────────────────────────────────

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-16 text-muted-foreground">
        <Loader2 className="mr-2 h-5 w-5 animate-spin" aria-hidden="true" />
        <span>Loading…</span>
      </div>
    );
  }

  // ── Generating ─────────────────────────────────────────────────────────────

  if (isGenerating) {
    return (
      <div
        role="status"
        aria-live="polite"
        aria-label="Generating quiz"
        className="flex flex-col items-center justify-center gap-4 py-16 text-center"
      >
        <Loader2 className="h-8 w-8 animate-spin text-primary" aria-hidden="true" />
        <div>
          <p className="text-sm font-medium">Generating quiz questions…</p>
          <p className="mt-1 text-xs text-muted-foreground">
            The AI is creating questions from your content.
          </p>
        </div>
      </div>
    );
  }

  // ── Generation error ───────────────────────────────────────────────────────

  if (generateMutation.isError && !hasQuestions) {
    return (
      <div className="space-y-4">
        <Alert variant="destructive" role="alert">
          <AlertTriangle className="h-4 w-4" aria-hidden="true" />
          <AlertDescription>
            {generateMutation.error?.message ?? 'Quiz generation failed. Please try again.'}
          </AlertDescription>
        </Alert>
        <Button
          type="button"
          variant="outline"
          onClick={() => {
            generateMutation.reset();
            handleGenerateQuiz();
          }}
        >
          Retry generation
        </Button>
      </div>
    );
  }

  // ── No questions yet — manual trigger card ───────────────────────────────

  if (!hasQuestions) {
    return (
      <div className="flex flex-col items-center justify-center gap-4 py-16 text-center">
        <div className="rounded-full border p-3 text-muted-foreground">
          <Wand2 className="h-6 w-6" aria-hidden="true" />
        </div>
        <div>
          <p className="text-sm font-medium">Ready to generate quiz</p>
          <p className="mt-1 text-xs text-muted-foreground">
            The AI will create questions from your sections and content.
          </p>
        </div>
        {/* Manual trigger by design — explicit user confirmation before firing an AI call. Consistent with ParseStep. */}
        <Button
          type="button"
          onClick={handleGenerateQuiz}
          className="min-h-[44px] min-w-[140px]"
        >
          <Wand2 className="mr-2 h-4 w-4" aria-hidden="true" />
          Generate Quiz
        </Button>
      </div>
    );
  }

  // ── Questions editor ───────────────────────────────────────────────────────

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-base font-semibold">Quiz Questions</h2>
          <p className="text-xs text-muted-foreground mt-0.5">
            {hasQuestions
              ? `${questions.length} question${questions.length !== 1 ? 's' : ''} — edit, reorder, or delete before continuing.`
              : 'No questions yet.'}
          </p>
        </div>
        <Button
          type="button"
          variant="outline"
          size="sm"
          className="gap-1.5"
          onClick={handleGenerateQuiz}
          disabled={isGenerating}
        >
          <Wand2 className="h-3.5 w-3.5" aria-hidden="true" />
          Regenerate All
        </Button>
      </div>

      <SectionQuestionGroup
        questions={questions}
        onSaveQuestion={handleSaveQuestion}
        onDeleteQuestion={handleDeleteQuestion}
        onAddQuestion={handleAddQuestion}
        isSaving={isSaving}
      />

      {talk && (
        <QuizSettingsPanel
          talk={talk}
          onSave={handleSaveSettings}
          isSaving={updateSettingsMutation.isPending}
        />
      )}

      {/* Save & Continue */}
      <div className="flex justify-end pt-2">
        <Button
          type="button"
          onClick={handleContinue}
          disabled={isSaving || !hasQuestions}
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
