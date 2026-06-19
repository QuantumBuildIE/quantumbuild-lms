'use client';

import { useEffect, useRef, useCallback } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { toast } from 'sonner';
import { AlertTriangle, Loader2, Wand2 } from 'lucide-react';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { WizardSectionDivider } from '@/components/ui/wizard-section-divider';
import { SectionQuestionGroup } from '../components/SectionQuestionGroup';
import { QuizSettingsPanel } from '../components/QuizSettingsPanel';
import { useGenerateQuiz } from '../hooks/useGenerateQuiz';
import { useUpdateTalkQuestions } from '../hooks/useUpdateTalkQuestions';
import { useUpdateQuizSettings } from '../hooks/useUpdateQuizSettings';
import { useTalkStatusPolling } from '../hooks/useTalkStatusPolling';
import { quizStepSchema, type QuizStepFormValues } from '../schemas/quizStepSchema';
import type { ToolboxTalkQuestion } from '@/types/toolbox-talks';
import type { QuestionFormData } from '../schemas/questionSchema';
import type { UpdateTalkQuizSettingsRequest } from '@/lib/api/toolbox-talks/toolbox-talks';

// ============================================
// Helpers — bridge between form data and ToolboxTalkQuestion display shape
// ============================================

function toDisplayQuestion(q: QuestionFormData, index: number): ToolboxTalkQuestion {
  const typeDisplay =
    q.questionType === 'MultipleChoice' ? 'Multiple Choice' : q.questionType;
  return {
    id: q.id ?? '',
    toolboxTalkId: '',
    questionNumber: index + 1,
    questionText: q.questionText,
    questionType: q.questionType,
    questionTypeDisplay: typeDisplay,
    options: q.options ?? null,
    correctAnswer: q.correctAnswer ?? null,
    correctOptionIndex: q.correctOptionIndex ?? null,
    points: q.points,
    source: (q.source as ToolboxTalkQuestion['source']) ?? 'Manual',
    isFromVideoFinalPortion: q.isFromVideoFinalPortion ?? false,
    videoTimestamp: q.videoTimestamp ?? null,
  };
}

function toFormQuestion(q: ToolboxTalkQuestion): QuestionFormData {
  return {
    id: q.id || undefined,
    questionNumber: q.questionNumber,
    questionText: q.questionText,
    questionType: q.questionType as QuestionFormData['questionType'],
    options: q.options,
    correctOptionIndex: q.correctOptionIndex,
    correctAnswer: q.correctAnswer,
    points: q.points,
    source: q.source ?? 'Manual',
    isFromVideoFinalPortion: q.isFromVideoFinalPortion ?? false,
    videoTimestamp: q.videoTimestamp ?? null,
  };
}

// ============================================
// Props
// ============================================

export interface QuizStepProps {
  talkId: string;
  onContinue: () => void | Promise<void>;
}

// ============================================
// Component
// ============================================

export function QuizStep({ talkId, onContinue }: QuizStepProps) {
  const { data: talk, isLoading } = useTalkStatusPolling(talkId, true);
  const generateMutation = useGenerateQuiz(talkId);
  const updateQuestionsMutation = useUpdateTalkQuestions(talkId);
  const updateSettingsMutation = useUpdateQuizSettings(talkId);

  const form = useForm<QuizStepFormValues>({
    resolver: zodResolver(quizStepSchema),
    mode: 'onBlur',
    defaultValues: { questions: [] },
  });

  // Guard: only auto-init once from server state
  const initializedRef = useRef(false);

  // Initialize questions from server state (re-entering step or after generation)
  useEffect(() => {
    if (!talk) return;
    if (initializedRef.current) return;
    if (talk.status !== 'Draft') return;
    if (talk.questions.length > 0) {
      form.reset({ questions: talk.questions.map(toFormQuestion) });
      initializedRef.current = true;
    }
  }, [talk, form]);

  // After a successful generate, sync the questions from mutation result
  useEffect(() => {
    if (generateMutation.data?.questions) {
      form.reset({ questions: generateMutation.data.questions.map(toFormQuestion) });
      initializedRef.current = true;
    }
  }, [generateMutation.data, form]);

  const handleGenerateQuiz = useCallback(async () => {
    try {
      initializedRef.current = false;
      form.reset({ questions: [] });
      await generateMutation.mutateAsync();
    } catch {
      toast.error('Failed to generate quiz. Please try again.');
    }
  }, [generateMutation, form]);

  const handleSaveQuestion = useCallback(
    (index: number, data: QuestionFormData) => {
      const current = form.getValues('questions');
      const updated = [...current];
      updated[index] = { ...data, questionNumber: index + 1 };
      form.setValue('questions', updated, { shouldValidate: true, shouldDirty: true });
    },
    [form]
  );

  const handleDeleteQuestion = useCallback(
    (index: number) => {
      const current = form.getValues('questions');
      const updated = current
        .filter((_, i) => i !== index)
        .map((q, i) => ({ ...q, questionNumber: i + 1 }));
      form.setValue('questions', updated, { shouldValidate: true, shouldDirty: true });
    },
    [form]
  );

  const handleAddQuestion = useCallback(() => {
    const current = form.getValues('questions');
    const newQ: QuestionFormData = {
      id: undefined,
      questionNumber: current.length + 1,
      questionText: '',
      questionType: 'MultipleChoice',
      options: ['', '', '', ''],
      correctAnswer: null,
      correctOptionIndex: 0,
      points: 1,
      source: 'Manual',
      isFromVideoFinalPortion: false,
      videoTimestamp: null,
    };
    form.setValue('questions', [...current, newQ], { shouldDirty: true });
  }, [form]);

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
    const valid = await form.trigger();
    if (!valid) {
      // Best-effort focus on the first erroring question field.
      // form.setFocus is a no-op when the field isn't RHF-registered (SectionQuestionGroup
      // uses uncontrolled inputs), so this is informational rather than guaranteed.
      const errors = form.formState.errors;
      if (Array.isArray(errors.questions)) {
        const idx = (errors.questions as Array<unknown>).findIndex(Boolean);
        if (idx >= 0) {
          form.setFocus(
            `questions.${idx}.questionText` as Parameters<typeof form.setFocus>[0]
          );
        }
      }
      toast.error('Please fix the errors before continuing.');
      return;
    }

    const questions = form.getValues('questions');
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
  }, [form, updateQuestionsMutation, onContinue]);

  // ── Derived state ──────────────────────────────────────────────────────────

  const questions = form.watch('questions');
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
      <div className="flex flex-col items-center justify-center gap-4 py-20 text-center">
        <div className="rounded-full bg-primary/10 p-4 text-primary">
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

  const questionsError = form.formState.errors.questions?.message;

  return (
    <div className="space-y-6">
      <div className="rounded-xl border shadow-sm bg-card overflow-hidden">
        <div className="flex items-center justify-between px-6 py-4 border-b">
          <div>
            <p className="font-semibold leading-none">Quiz Questions</p>
            <p className="text-sm text-muted-foreground mt-1">
              {questions.length} question{questions.length !== 1 ? 's' : ''} — edit, reorder, or delete before continuing.
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
          questions={questions.map(toDisplayQuestion)}
          onSaveQuestion={handleSaveQuestion}
          onDeleteQuestion={handleDeleteQuestion}
          onAddQuestion={handleAddQuestion}
          isSaving={isSaving}
        />
      </div>

      {/* Array-level validation error (e.g. "At least one question is required") */}
      {questionsError && (
        <p role="alert" className="text-sm text-destructive">
          {questionsError}
        </p>
      )}

      {talk && (
        <>
          <WizardSectionDivider number="2a" label="Quiz Settings" />
          <QuizSettingsPanel
            talk={talk}
            onSave={handleSaveSettings}
            isSaving={updateSettingsMutation.isPending}
          />
        </>
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
