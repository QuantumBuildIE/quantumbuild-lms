'use client';

import { useState, useEffect, useCallback, useRef, useMemo } from 'react';
import { Button } from '@/components/ui/button';
import { Alert, AlertDescription } from '@/components/ui/alert';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '@/components/ui/alert-dialog';
import { WizardSectionDivider } from '@/components/ui/wizard-section-divider';
import { Loader2, Sparkles, AlertCircle, RotateCcw, ArrowLeft, ArrowRight, AlertTriangle } from 'lucide-react';
import { cn } from '@/lib/utils';
import { toast } from 'sonner';
import {
  useCreationSession,
  useSessionQuizData,
  useGenerateQuiz,
  useUpdateSessionQuestions,
  useUpdateSessionQuizSettings,
} from '@/lib/api/toolbox-talks/use-content-creation';
import { QuizSettingsPanel } from './quiz/QuizSettingsPanel';
import { SectionQuestionGroup } from './quiz/SectionQuestionGroup';
import type { WizardState, QuestionsSnapshot } from '../CreateWizard';
import type { QuizQuestion, QuizSettings, ParsedSection } from '@/types/content-creation';

interface QuizStepProps {
  state: WizardState;
  updateState: (updates: Partial<WizardState>) => void;
  onNext: () => void;
  onBack: () => void;
}

const DEFAULT_SETTINGS: QuizSettings = {
  requireQuiz: true,
  passingScore: 80,
  shuffleQuestions: false,
  shuffleOptions: false,
  allowRetry: true,
};

const IN_FLIGHT_STATUS_LABEL: Record<string, string> = {
  GeneratingQuiz: 'Quiz Generation',
  TranslatingValidating: 'Translation & Validation',
  Publishing: 'Publishing',
};

export function QuizStep({ state, updateState, onNext, onBack }: QuizStepProps) {
  const sessionId = state.sessionId;

  // API hooks
  const { data: session, refetch: refetchSession } = useCreationSession(sessionId);
  const { data: quizData, isLoading: isLoadingQuiz } = useSessionQuizData(sessionId);
  const generateQuiz = useGenerateQuiz();
  const updateQuestions = useUpdateSessionQuestions();
  const updateSettings = useUpdateSessionQuizSettings();

  // Local state
  const [questions, setQuestions] = useState<QuizQuestion[]>([]);
  const [settings, setSettings] = useState<QuizSettings>(DEFAULT_SETTINGS);
  const [editingQuestionId, setEditingQuestionId] = useState<string | null>(null);
  const [isGenerating, setIsGenerating] = useState(false);
  const [regeneratingQuestionId, setRegeneratingQuestionId] = useState<string | null>(null);
  const [generateError, setGenerateError] = useState<string | null>(null);
  const [showCascadeResetDialog, setShowCascadeResetDialog] = useState(false);
  const pollRef = useRef<ReturnType<typeof setInterval> | null>(null);

  // Status-derived flags
  const status = session?.status ?? null;
  const isInFlight =
    status === 'GeneratingQuiz' ||
    status === 'TranslatingValidating' ||
    status === 'Publishing';
  const isValidated = status === 'Validated';
  const friendlyStatus = status ? (IN_FLIGHT_STATUS_LABEL[status] ?? status) : '';

  // Parse sections from session
  const sections: ParsedSection[] = useMemo(() => {
    if (!session?.parsedSectionsJson) return [];
    try {
      return JSON.parse(session.parsedSectionsJson);
    } catch {
      return [];
    }
  }, [session?.parsedSectionsJson]);

  // Hydrate local state from quiz data
  useEffect(() => {
    if (quizData) {
      setQuestions(quizData.questions);
      setSettings(quizData.settings);
    }
  }, [quizData]);

  // Snapshot questions on first populated visit (per session)
  useEffect(() => {
    if (questions.length > 0 && state.generatedQuestionsSnapshot === null) {
      updateState({
        generatedQuestionsSnapshot: {
          questions: questions.map((q) => ({
            id: q.id,
            sectionIndex: q.sectionIndex,
            questionText: q.questionText,
            options: [...q.options],
            correctAnswerIndex: q.correctAnswerIndex,
          })),
        } satisfies QuestionsSnapshot,
      });
    }
  }, [questions, state.generatedQuestionsSnapshot, updateState]);

  // Auto-generate quiz on mount if no questions exist
  useEffect(() => {
    if (!session || !quizData) return;
    if (
      quizData.questions.length === 0 &&
      (session.status === 'Validated' || session.status === 'Parsed') &&
      !isGenerating &&
      !generateError
    ) {
      handleGenerateQuiz();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [session?.status, quizData]);

  // Poll while generating
  useEffect(() => {
    if (isGenerating && sessionId) {
      pollRef.current = setInterval(() => {
        refetchSession();
      }, 2000);
    }
    return () => {
      if (pollRef.current) clearInterval(pollRef.current);
    };
  }, [isGenerating, sessionId, refetchSession]);

  // Stop polling when generation completes
  useEffect(() => {
    if (session?.status === 'QuizGenerated' && isGenerating) {
      if (pollRef.current) clearInterval(pollRef.current);
      setTimeout(() => {
        setIsGenerating(false);
        setRegeneratingQuestionId(null);
      }, 1500);
    }
    if (session?.status === 'Failed' && isGenerating) {
      if (pollRef.current) clearInterval(pollRef.current);
      setTimeout(() => {
        setIsGenerating(false);
        setRegeneratingQuestionId(null);
        setGenerateError('Quiz generation failed. Please try again.');
      }, 1500);
    }
  }, [session?.status, isGenerating]);

  const handleGenerateQuiz = useCallback(async (questionId?: string) => {
    if (!sessionId) return;
    if (questionId) setRegeneratingQuestionId(questionId);
    setIsGenerating(true);
    setGenerateError(null);
    try {
      await generateQuiz.mutateAsync(sessionId);
    } catch (err) {
      setIsGenerating(false);
      setRegeneratingQuestionId(null);
      setGenerateError(err instanceof Error ? err.message : 'Quiz generation failed');
    }
  }, [sessionId, generateQuiz]);

  // Settings change — debounced save
  const settingsSaveRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const handleSettingsChange = useCallback(
    (newSettings: QuizSettings) => {
      setSettings(newSettings);
      if (settingsSaveRef.current) clearTimeout(settingsSaveRef.current);
      settingsSaveRef.current = setTimeout(() => {
        if (sessionId) {
          updateSettings.mutate({ sessionId, settings: newSettings });
        }
      }, 500);
    },
    [sessionId, updateSettings]
  );

  // Question CRUD — auto-save blocked from Validated (questions held locally until Continue confirms)
  const saveQuestionsToServer = useCallback(
    (updatedQuestions: QuizQuestion[]) => {
      if (!sessionId) return;
      // From Validated: questions are held locally and saved atomically at handleContinue
      // after the user explicitly confirms the cascade-reset.
      if (session?.status === 'Validated') return;
      updateQuestions.mutate(
        { sessionId, questions: updatedQuestions },
        { onError: () => toast.error('Failed to save questions') }
      );
    },
    [sessionId, updateQuestions, session?.status]
  );

  const handleSaveQuestion = useCallback(
    (updated: QuizQuestion) => {
      const newQuestions = questions.map((q) => (q.id === updated.id ? updated : q));
      setQuestions(newQuestions);
      setEditingQuestionId(null);
      saveQuestionsToServer(newQuestions);
    },
    [questions, saveQuestionsToServer]
  );

  const handleDeleteQuestion = useCallback(
    (questionId: string) => {
      const captured = questions.find((q) => q.id === questionId);
      if (!captured) return;

      const newQuestions = questions.filter((q) => q.id !== questionId);
      setQuestions(newQuestions);
      if (editingQuestionId === questionId) setEditingQuestionId(null);
      saveQuestionsToServer(newQuestions);

      toast.success('Question deleted', {
        duration: 8000,
        action: {
          label: 'Undo',
          onClick: () => {
            const restored = [...newQuestions, captured].sort(
              (a, b) => (a.sectionIndex ?? 0) - (b.sectionIndex ?? 0)
            );
            setQuestions(restored);
            saveQuestionsToServer(restored);
          },
        },
      });
    },
    [questions, editingQuestionId, saveQuestionsToServer]
  );

  const handleAddQuestion = useCallback(
    (sectionIndex: number) => {
      const newQuestion: QuizQuestion = {
        id: crypto.randomUUID(),
        sectionIndex,
        questionText: '',
        questionType: 'MultipleChoice',
        options: ['Option A', 'Option B', 'Option C', 'Option D'],
        correctAnswerIndex: 0,
        points: 1,
        isAiGenerated: false,
      };
      const newQuestions = [...questions, newQuestion];
      setQuestions(newQuestions);
      setEditingQuestionId(newQuestion.id);
      // Don't save to server yet — user is editing
    },
    [questions]
  );

  const handleCancelEdit = useCallback(() => {
    // If the question being edited is new (no text), remove it
    const editing = questions.find((q) => q.id === editingQuestionId);
    if (editing && !editing.questionText.trim()) {
      setQuestions(questions.filter((q) => q.id !== editingQuestionId));
    }
    setEditingQuestionId(null);
  }, [questions, editingQuestionId]);

  // Snapshot refresh — called after a successful server save
  const refreshSnapshot = useCallback(() => {
    updateState({
      generatedQuestionsSnapshot: {
        questions: questions.map((q) => ({
          id: q.id,
          sectionIndex: q.sectionIndex,
          questionText: q.questionText,
          options: [...q.options],
          correctAnswerIndex: q.correctAnswerIndex,
        })),
      },
    });
  }, [questions, updateState]);

  // Execute PUT and advance — used by both normal and cascade-confirmed paths
  const executeQuestionsSave = useCallback(async () => {
    if (!sessionId) return;
    try {
      await updateQuestions.mutateAsync({ sessionId, questions });
      refreshSnapshot();
      onNext();
    } catch {
      // Error surfaced via mutation isError banner above Continue.
    }
  }, [sessionId, questions, updateQuestions, refreshSnapshot, onNext]);

  const handleCascadeResetConfirm = useCallback(async () => {
    setShowCascadeResetDialog(false);
    await executeQuestionsSave();
  }, [executeQuestionsSave]);

  // Continue — change detection + routing
  const handleContinue = useCallback(async () => {
    if (questions.length === 0) {
      onNext();
      return;
    }

    // Deep-compare current questions against the arrival snapshot
    const snapshot = state.generatedQuestionsSnapshot;
    let questionsChanged: boolean;

    if (!snapshot) {
      questionsChanged = true;
    } else if (snapshot.questions.length !== questions.length) {
      questionsChanged = true;
    } else {
      questionsChanged = questions.some((q, i) => {
        const snap = snapshot.questions[i];
        return (
          q.questionText !== snap.questionText ||
          q.correctAnswerIndex !== snap.correctAnswerIndex ||
          q.options.length !== snap.options.length ||
          q.options.some((opt, j) => opt !== snap.options[j])
        );
      });
    }

    // No changes — skip PUT and advance directly
    if (!questionsChanged) {
      onNext();
      return;
    }

    // Changes detected while session is Validated — require explicit cascade consent
    if (isValidated) {
      setShowCascadeResetDialog(true);
      return;
    }

    // Normal forward path (Parsed or QuizGenerated)
    await executeQuestionsSave();
  }, [questions, state.generatedQuestionsSnapshot, isValidated, executeQuestionsSave, onNext]);

  // Group questions by section
  const questionsBySection = useMemo(() => {
    const grouped = new Map<number, QuizQuestion[]>();
    for (const section of sections) {
      grouped.set(section.suggestedOrder, []);
    }
    for (const q of questions) {
      const existing = grouped.get(q.sectionIndex) ?? [];
      existing.push(q);
      grouped.set(q.sectionIndex, existing);
    }
    return grouped;
  }, [questions, sections]);

  const totalQuestions = questions.length;
  const isSaving = updateQuestions.isPending || updateSettings.isPending;
  const isContinueDisabled = editingQuestionId !== null || isSaving || isInFlight;

  // Loading state
  if (isLoadingQuiz && !quizData) {
    return (
      <div className="flex items-center justify-center py-12">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground mr-2" />
        <span className="text-muted-foreground">Loading quiz data...</span>
      </div>
    );
  }

  // Full-screen generating state — only on initial generation (no questions yet)
  if (isGenerating && questions.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center py-12 gap-4">
        <div className="flex items-center gap-2">
          <Sparkles className="h-5 w-5 text-primary animate-pulse" />
          <Loader2 className="h-5 w-5 animate-spin text-primary" />
        </div>
        <p className="text-sm font-medium">Generating quiz questions...</p>
        <p className="text-xs text-muted-foreground">
          AI is analysing your content and creating questions for each section
        </p>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Error state */}
      {generateError && (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription className="flex items-center justify-between">
            <span>{generateError}</span>
            <Button variant="outline" size="sm" onClick={() => handleGenerateQuiz()}>
              <RotateCcw className="h-3.5 w-3.5 mr-1.5" />
              Retry
            </Button>
          </AlertDescription>
        </Alert>
      )}

      {/* 3a — Quiz Settings */}
      <WizardSectionDivider number="3a" label="Quiz Settings" firstSection />

      {/* Quiz settings */}
      <QuizSettingsPanel
        settings={settings}
        onChange={handleSettingsChange}
        isSaving={isSaving}
      />

      {/* 3b — Questions */}
      <WizardSectionDivider number="3b" label="Questions" />

      {/* In-flight notice — editing paused while a job is running */}
      {isInFlight && (
        <div className="flex items-start gap-1.5 rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-700">
          <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" />
          Quiz editing is paused while {friendlyStatus} is in progress.
        </div>
      )}

      {/* Validated notice — editing will cascade-reset downstream work */}
      {isValidated && !isInFlight && (
        <div className="flex items-start gap-1.5 rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-700">
          <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" />
          This learning has progressed through translation and validation. Editing the quiz here will clear the translations and validation results, which will need to be regenerated.
        </div>
      )}

      {/* Section-grouped questions */}
      <div className="space-y-4">
        <div className="flex items-center justify-between">
          <h3 className="text-sm font-semibold">
            Questions ({totalQuestions})
          </h3>
          {totalQuestions > 0 && (
            <AlertDialog>
              <AlertDialogTrigger asChild>
                <Button
                  variant="outline"
                  size="sm"
                  disabled={isGenerating || isInFlight}
                  className="text-xs"
                >
                  {isGenerating ? (
                    <Loader2 className="h-3.5 w-3.5 mr-1.5 animate-spin" />
                  ) : (
                    <Sparkles className="h-3.5 w-3.5 mr-1.5" />
                  )}
                  Regenerate All
                </Button>
              </AlertDialogTrigger>
              <AlertDialogContent>
                <AlertDialogHeader>
                  <AlertDialogTitle>Regenerate all questions?</AlertDialogTitle>
                  <AlertDialogDescription>
                    This will replace all current questions and cannot be undone.
                  </AlertDialogDescription>
                </AlertDialogHeader>
                <AlertDialogFooter>
                  <AlertDialogCancel>Cancel</AlertDialogCancel>
                  <AlertDialogAction onClick={() => handleGenerateQuiz()}>
                    Regenerate
                  </AlertDialogAction>
                </AlertDialogFooter>
              </AlertDialogContent>
            </AlertDialog>
          )}
        </div>

        <div className={cn(isGenerating && 'opacity-50 pointer-events-none')}>
        {sections.map((section) => {
          const sectionQuestions = questionsBySection.get(section.suggestedOrder) ?? [];
          return (
            <SectionQuestionGroup
              key={section.suggestedOrder}
              sectionIndex={section.suggestedOrder}
              sectionTitle={section.title}
              questions={sectionQuestions}
              editingQuestionId={editingQuestionId}
              regeneratingQuestionId={regeneratingQuestionId}
              disabled={isInFlight}
              onStartEdit={setEditingQuestionId}
              onSaveQuestion={handleSaveQuestion}
              onCancelEdit={handleCancelEdit}
              onDeleteQuestion={handleDeleteQuestion}
              onAddQuestion={handleAddQuestion}
              onRegenerateQuestion={handleGenerateQuiz}
            />
          );
        })}
        </div>

        {sections.length === 0 && (
          <p className="text-sm text-muted-foreground text-center py-8">
            No sections found. Go back to the Parse step to add sections.
          </p>
        )}
      </div>

      {/* Form-level error: mutation failure shown above the action cluster per PHASE_5_STANDARDS §6.2. */}
      {(updateQuestions.isError || updateSettings.isError) && (
        <Alert variant="destructive" role="alert">
          <AlertTriangle className="h-4 w-4" aria-hidden="true" />
          <AlertDescription>
            {updateQuestions.isError
              ? (updateQuestions.error instanceof Error ? updateQuestions.error.message : 'Failed to save questions. Please try again.')
              : (updateSettings.error instanceof Error ? updateSettings.error.message : 'Failed to save quiz settings. Please try again.')}
          </AlertDescription>
        </Alert>
      )}

      {/* Navigation */}
      <div className="flex justify-between pt-4 border-t">
        <Button variant="outline" onClick={onBack} disabled={isSaving}>
          <ArrowLeft className="mr-2 h-4 w-4" />
          Back
        </Button>
        <Button
          onClick={handleContinue}
          disabled={isContinueDisabled}
        >
          {updateQuestions.isPending ? (
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

      {/* Cascade reset confirmation dialog */}
      <AlertDialog open={showCascadeResetDialog} onOpenChange={setShowCascadeResetDialog}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Quiz changes will reset translation work</AlertDialogTitle>
            <AlertDialogDescription>
              You&apos;ve edited the quiz on a learning that has already been translated and validated.
              Continuing will: clear the translations and validation results.
              Translation will need to be re-run before publishing.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction onClick={handleCascadeResetConfirm}>
              Continue and reset
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
