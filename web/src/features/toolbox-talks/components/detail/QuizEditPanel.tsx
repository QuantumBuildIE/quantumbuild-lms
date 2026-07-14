'use client';

import { useState, useCallback, useMemo } from 'react';
import { PencilIcon, XIcon, SaveIcon, HelpCircleIcon, CheckCircle2Icon } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { SectionQuestionGroup } from '../learning-wizard/components/SectionQuestionGroup';
import { useUpdateToolboxTalk } from '@/lib/api/toolbox-talks';
import { usePermission } from '@/lib/auth/use-auth';
import type { ToolboxTalk, ToolboxTalkQuestion } from '@/types/toolbox-talks';
import type { QuestionFormData } from '../learning-wizard/schemas/questionSchema';
import { cn } from '@/lib/utils';
import { toast } from 'sonner';

// ============================================
// Helpers
// ============================================

const NEW_Q_PREFIX = '__new__';

function getQuestionTypeDisplay(type: string): string {
  if (type === 'MultipleChoice') return 'Multiple Choice';
  if (type === 'TrueFalse') return 'True/False';
  return type;
}

/**
 * Applies QuestionFormData edits onto a ToolboxTalkQuestion.
 * Preserves the question's existing id so the update payload can track
 * which DB row to update vs create.
 */
function applyFormDataToQuestion(
  existingId: string,
  talkId: string,
  data: QuestionFormData
): ToolboxTalkQuestion {
  const opts =
    data.questionType === 'TrueFalse'
      ? ['True', 'False']
      : data.questionType === 'MultipleChoice'
        ? (data.options ?? null)
        : null;

  const correctAnswer =
    data.questionType === 'ShortAnswer'
      ? (data.correctAnswer ?? null)
      : data.correctOptionIndex != null && opts
        ? (opts[data.correctOptionIndex] ?? null)
        : null;

  return {
    id: existingId,
    toolboxTalkId: talkId,
    questionNumber: data.questionNumber,
    questionText: data.questionText,
    questionType: data.questionType,
    questionTypeDisplay: getQuestionTypeDisplay(data.questionType),
    options: opts,
    correctAnswer,
    correctOptionIndex: data.correctOptionIndex ?? null,
    points: data.points,
    source: (data.source as ToolboxTalkQuestion['source']) ?? 'Manual',
    isFromVideoFinalPortion: data.isFromVideoFinalPortion,
    videoTimestamp: data.videoTimestamp ?? null,
  };
}

/** Creates a blank question for the "Add Question" action. */
function makeNewQuestion(talkId: string, questionNumber: number): ToolboxTalkQuestion {
  return {
    id: `${NEW_Q_PREFIX}${crypto.randomUUID()}`,
    toolboxTalkId: talkId,
    questionNumber,
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
}

/** Converts a display question to the request shape for PUT /toolbox-talks/{id}. */
function questionToSaveRequest(q: ToolboxTalkQuestion, number: number) {
  const isNew = q.id.startsWith(NEW_Q_PREFIX);
  const correctAnswer =
    q.correctAnswer ??
    (q.correctOptionIndex != null && q.options ? (q.options[q.correctOptionIndex] ?? '') : '');

  return {
    id: isNew ? undefined : q.id,
    questionNumber: number,
    questionText: q.questionText,
    questionType: q.questionType,
    options: q.options ?? undefined,
    correctAnswer,
    points: q.points,
    source: q.source,
  };
}

function questionsEqual(a: ToolboxTalkQuestion[], b: ToolboxTalkQuestion[]): boolean {
  if (a.length !== b.length) return false;
  const sig = (q: ToolboxTalkQuestion) =>
    JSON.stringify({
      id: q.id,
      questionText: q.questionText,
      questionType: q.questionType,
      options: q.options,
      correctAnswer: q.correctAnswer,
      correctOptionIndex: q.correctOptionIndex,
      points: q.points,
    });
  return a.every((q, i) => sig(q) === sig(b[i]));
}

// ============================================
// Props
// ============================================

interface QuizEditPanelProps {
  talk: ToolboxTalk;
  onRefetch: () => void;
}

// ============================================
// Component
// ============================================

export function QuizEditPanel({ talk, onRefetch }: QuizEditPanelProps) {
  const canManage = usePermission('Learnings.Manage');
  const [isEditMode, setIsEditMode] = useState(false);
  const [editedQuestions, setEditedQuestions] = useState<ToolboxTalkQuestion[]>([]);
  const [originalQuestions, setOriginalQuestions] = useState<ToolboxTalkQuestion[]>([]);
  const [confirmDiscardOpen, setConfirmDiscardOpen] = useState(false);
  const updateMutation = useUpdateToolboxTalk();

  const isDirty = useMemo(
    () => !questionsEqual(editedQuestions, originalQuestions),
    [editedQuestions, originalQuestions]
  );

  const openEditMode = useCallback(() => {
    onRefetch();
    const qs = [...talk.questions];
    setEditedQuestions(qs);
    setOriginalQuestions(qs);
    setIsEditMode(true);
  }, [talk.questions, onRefetch]);

  const handleSaveQuestion = useCallback(
    (index: number, data: QuestionFormData) => {
      setEditedQuestions((prev) => {
        const updated = [...prev];
        updated[index] = applyFormDataToQuestion(prev[index].id, talk.id, data);
        return updated;
      });
    },
    [talk.id]
  );

  const handleDeleteQuestion = useCallback((index: number) => {
    setEditedQuestions((prev) => prev.filter((_, i) => i !== index));
  }, []);

  const handleAddQuestion = useCallback(() => {
    setEditedQuestions((prev) => [
      ...prev,
      makeNewQuestion(talk.id, prev.length + 1),
    ]);
  }, [talk.id]);

  const handleSave = useCallback(async () => {
    try {
      await updateMutation.mutateAsync({
        id: talk.id,
        data: {
          id: talk.id,
          code: talk.code,
          title: talk.title,
          description: talk.description ?? undefined,
          category: talk.category ?? undefined,
          frequency: talk.frequency,
          videoUrl: talk.videoUrl ?? undefined,
          videoSource: talk.videoSource,
          attachmentUrl: talk.attachmentUrl ?? undefined,
          minimumVideoWatchPercent: talk.minimumVideoWatchPercent,
          requiresQuiz: talk.requiresQuiz,
          passingScore: talk.passingScore ?? undefined,
          isActive: talk.isActive,
          quizQuestionCount: talk.quizQuestionCount ?? undefined,
          shuffleQuestions: talk.shuffleQuestions,
          shuffleOptions: talk.shuffleOptions,
          useQuestionPool: talk.useQuestionPool,
          sourceLanguageCode: talk.sourceLanguageCode,
          autoAssignToNewEmployees: talk.autoAssignToNewEmployees,
          autoAssignDueDays: talk.autoAssignDueDays,
          generateSlidesFromPdf: talk.generateSlidesFromPdf,
          generateCertificate: talk.generateCertificate,
          allowRetry: talk.allowRetry,
          requiresRefresher: talk.requiresRefresher,
          refresherIntervalMonths: talk.refresherIntervalMonths,
          // Preserve existing sections unchanged
          sections: talk.sections.map((s, i) => ({
            id: s.id,
            sectionNumber: i + 1,
            title: s.title,
            content: s.content,
            requiresAcknowledgment: s.requiresAcknowledgment,
            source: s.source,
          })),
          questions: editedQuestions.map((q, i) => questionToSaveRequest(q, i + 1)),
        },
      });
      toast.success('Quiz saved');
      setIsEditMode(false);
    } catch (error) {
      const msg = error instanceof Error ? error.message : 'Failed to save quiz';
      toast.error('Save failed', { description: msg });
    }
  }, [talk, editedQuestions, updateMutation]);

  const handleCancelClick = useCallback(() => {
    if (isDirty) {
      setConfirmDiscardOpen(true);
    } else {
      setIsEditMode(false);
    }
  }, [isDirty]);

  const handleConfirmDiscard = useCallback(() => {
    setConfirmDiscardOpen(false);
    setIsEditMode(false);
  }, []);

  if (!talk.requiresQuiz) {
    return (
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <HelpCircleIcon className="h-5 w-5" />
            Quiz Questions
          </CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-sm text-muted-foreground">
            Quiz is disabled for this learning. Enable it in Settings to add questions.
          </p>
        </CardContent>
      </Card>
    );
  }

  return (
    <>
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <div>
              <CardTitle className="flex items-center gap-2">
                <HelpCircleIcon className="h-5 w-5" />
                Quiz Questions ({talk.questions.length})
              </CardTitle>
              <CardDescription>Passing score: {talk.passingScore}%</CardDescription>
            </div>
            {canManage && !isEditMode && (
              <Button variant="outline" size="sm" onClick={openEditMode}>
                <PencilIcon className="mr-2 h-4 w-4" />
                Edit Quiz
              </Button>
            )}
            {isEditMode && (
              <div className="flex gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  onClick={handleCancelClick}
                  disabled={updateMutation.isPending}
                >
                  <XIcon className="mr-2 h-4 w-4" />
                  Cancel
                </Button>
                <Button
                  size="sm"
                  onClick={handleSave}
                  disabled={updateMutation.isPending || !isDirty}
                >
                  <SaveIcon className="mr-2 h-4 w-4" />
                  {updateMutation.isPending ? 'Saving...' : 'Save Quiz'}
                </Button>
              </div>
            )}
          </div>
        </CardHeader>
        <CardContent>
          {isEditMode ? (
            <SectionQuestionGroup
              questions={editedQuestions}
              onSaveQuestion={handleSaveQuestion}
              onDeleteQuestion={handleDeleteQuestion}
              onAddQuestion={handleAddQuestion}
              isSaving={updateMutation.isPending}
            />
          ) : (
            <div className="space-y-4">
              {talk.questions.length === 0 ? (
                <p className="text-sm text-muted-foreground text-center py-4">No questions yet</p>
              ) : (
                talk.questions.map((question) => (
                  <div key={question.id} className="rounded-lg border p-4">
                    <div className="flex items-start gap-3">
                      <Badge variant="outline" className="shrink-0">
                        Q{question.questionNumber}
                      </Badge>
                      <div className="flex-1 space-y-2">
                        <p className="font-medium">{question.questionText}</p>
                        <div className="flex items-center gap-4 text-sm text-muted-foreground">
                          <span>{question.questionTypeDisplay}</span>
                          <span>
                            {question.points} point{question.points !== 1 ? 's' : ''}
                          </span>
                        </div>
                        {question.options && question.options.length > 0 && (
                          <div className="mt-2 space-y-1">
                            {question.options.map((option, idx) => (
                              <div
                                key={idx}
                                className={cn(
                                  'flex items-center gap-2 text-sm',
                                  option === question.correctAnswer &&
                                    'text-green-600 font-medium'
                                )}
                              >
                                <span className="w-6">{String.fromCharCode(65 + idx)}.</span>
                                <span>{option}</span>
                                {option === question.correctAnswer && (
                                  <CheckCircle2Icon className="h-4 w-4" />
                                )}
                              </div>
                            ))}
                          </div>
                        )}
                        {question.questionType === 'ShortAnswer' && (
                          <div className="mt-2 text-sm">
                            <span className="text-muted-foreground">Expected answer: </span>
                            <span className="font-medium text-green-600">
                              {question.correctAnswer}
                            </span>
                          </div>
                        )}
                      </div>
                    </div>
                  </div>
                ))
              )}
            </div>
          )}
        </CardContent>
      </Card>

      <AlertDialog open={confirmDiscardOpen} onOpenChange={setConfirmDiscardOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Discard unsaved changes?</AlertDialogTitle>
            <AlertDialogDescription>
              Your quiz edits have not been saved. This will discard all changes.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Keep editing</AlertDialogCancel>
            <AlertDialogAction onClick={handleConfirmDiscard}>Discard changes</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}
