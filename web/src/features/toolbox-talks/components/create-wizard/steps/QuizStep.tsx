'use client';

import { useState, useEffect, useCallback, useRef, useMemo } from 'react';
import { Button } from '@/components/ui/button';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Loader2, Sparkles, AlertCircle, RotateCcw } from 'lucide-react';
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
import type { WizardState } from '../CreateWizard';
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

export function QuizStep({ state, onNext, onBack }: QuizStepProps) {
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
  const [generateError, setGenerateError] = useState<string | null>(null);
  const pollRef = useRef<ReturnType<typeof setInterval> | null>(null);

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

  // Auto-generate quiz on mount if no questions exist and session is in Validated state
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
      setIsGenerating(false);
      if (pollRef.current) clearInterval(pollRef.current);
    }
    if (session?.status === 'Failed' && isGenerating) {
      setIsGenerating(false);
      setGenerateError('Quiz generation failed. Please try again.');
      if (pollRef.current) clearInterval(pollRef.current);
    }
  }, [session?.status, isGenerating]);

  const handleGenerateQuiz = useCallback(async () => {
    if (!sessionId) return;
    setIsGenerating(true);
    setGenerateError(null);
    try {
      await generateQuiz.mutateAsync(sessionId);
    } catch (err) {
      setIsGenerating(false);
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
          updateSettings.mutate(
            { sessionId, settings: newSettings },
            { onError: () => toast.error('Failed to save quiz settings') }
          );
        }
      }, 500);
    },
    [sessionId, updateSettings]
  );

  // Question CRUD
  const saveQuestionsToServer = useCallback(
    (updatedQuestions: QuizQuestion[]) => {
      if (!sessionId) return;
      updateQuestions.mutate(
        { sessionId, questions: updatedQuestions },
        { onError: () => toast.error('Failed to save questions') }
      );
    },
    [sessionId, updateQuestions]
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
      const newQuestions = questions.filter((q) => q.id !== questionId);
      setQuestions(newQuestions);
      if (editingQuestionId === questionId) setEditingQuestionId(null);
      saveQuestionsToServer(newQuestions);
      toast.success('Question deleted');
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

  // Loading state
  if (isLoadingQuiz && !quizData) {
    return (
      <div className="flex items-center justify-center py-12">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground mr-2" />
        <span className="text-muted-foreground">Loading quiz data...</span>
      </div>
    );
  }

  // Generating state
  if (isGenerating) {
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
            <Button variant="outline" size="sm" onClick={handleGenerateQuiz}>
              <RotateCcw className="h-3.5 w-3.5 mr-1.5" />
              Retry
            </Button>
          </AlertDescription>
        </Alert>
      )}

      {/* Quiz settings */}
      <QuizSettingsPanel
        settings={settings}
        onChange={handleSettingsChange}
        isSaving={isSaving}
      />

      {/* Section-grouped questions */}
      <div className="space-y-4">
        <div className="flex items-center justify-between">
          <h3 className="text-sm font-semibold">
            Questions ({totalQuestions})
          </h3>
          {totalQuestions > 0 && (
            <Button
              variant="outline"
              size="sm"
              onClick={handleGenerateQuiz}
              disabled={isGenerating}
              className="text-xs"
            >
              <Sparkles className="h-3.5 w-3.5 mr-1.5" />
              Regenerate All
            </Button>
          )}
        </div>

        {sections.map((section) => {
          const sectionQuestions = questionsBySection.get(section.suggestedOrder) ?? [];
          return (
            <SectionQuestionGroup
              key={section.suggestedOrder}
              sectionIndex={section.suggestedOrder}
              sectionTitle={section.title}
              questions={sectionQuestions}
              editingQuestionId={editingQuestionId}
              onStartEdit={setEditingQuestionId}
              onSaveQuestion={handleSaveQuestion}
              onCancelEdit={handleCancelEdit}
              onDeleteQuestion={handleDeleteQuestion}
              onAddQuestion={handleAddQuestion}
            />
          );
        })}

        {sections.length === 0 && (
          <p className="text-sm text-muted-foreground text-center py-8">
            No sections found. Go back to the Parse step to add sections.
          </p>
        )}
      </div>

      {/* Navigation */}
      <div className="flex justify-between pt-4 border-t">
        <Button variant="outline" onClick={onBack}>
          Back
        </Button>
        <Button
          onClick={onNext}
          disabled={editingQuestionId !== null || isSaving}
        >
          Continue
        </Button>
      </div>
    </div>
  );
}
