'use client';

import { useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { ChevronDown, ChevronRight, Plus } from 'lucide-react';
import { cn } from '@/lib/utils';
import { QuestionCard } from './QuestionCard';
import type { QuizQuestion } from '@/types/content-creation';

interface SectionQuestionGroupProps {
  sectionIndex: number;
  sectionTitle: string;
  questions: QuizQuestion[];
  editingQuestionId: string | null;
  regeneratingQuestionId?: string | null;
  disabled?: boolean;
  onStartEdit: (questionId: string) => void;
  onSaveQuestion: (question: QuizQuestion) => void;
  onCancelEdit: () => void;
  onDeleteQuestion: (questionId: string) => void;
  onAddQuestion: (sectionIndex: number) => void;
  onRegenerateQuestion?: (questionId: string) => void;
}

export function SectionQuestionGroup({
  sectionIndex,
  sectionTitle,
  questions,
  editingQuestionId,
  regeneratingQuestionId,
  disabled = false,
  onStartEdit,
  onSaveQuestion,
  onCancelEdit,
  onDeleteQuestion,
  onAddQuestion,
  onRegenerateQuestion,
}: SectionQuestionGroupProps) {
  const [isExpanded, setIsExpanded] = useState(true);

  const sectionLabel = `L${String(sectionIndex + 1).padStart(2, '0')}`;

  return (
    <div className="rounded-lg border">
      {/* Section header */}
      <button
        type="button"
        onClick={() => setIsExpanded(!isExpanded)}
        className="flex w-full items-center justify-between gap-3 px-4 py-3 hover:bg-muted/50 transition-colors"
      >
        <div className="flex items-center gap-3">
          {isExpanded ? (
            <ChevronDown className="h-4 w-4 text-muted-foreground" />
          ) : (
            <ChevronRight className="h-4 w-4 text-muted-foreground" />
          )}
          <Badge variant="outline" className="font-mono text-xs">
            {sectionLabel}
          </Badge>
          <span className="text-sm font-medium">{sectionTitle}</span>
        </div>
        <Badge variant="secondary" className="text-xs">
          {questions.length} {questions.length === 1 ? 'question' : 'questions'}
        </Badge>
      </button>

      {/* Questions */}
      {isExpanded && (
        <div className={cn('px-4 pb-4 space-y-3', questions.length > 0 && 'pt-1')}>
          {questions.map((question, qIdx) => (
            <QuestionCard
              key={question.id}
              question={question}
              index={qIdx}
              isEditing={editingQuestionId === question.id}
              isRegenerating={regeneratingQuestionId === question.id}
              disabled={disabled}
              onStartEdit={() => onStartEdit(question.id)}
              onSave={onSaveQuestion}
              onCancel={onCancelEdit}
              onDelete={() => onDeleteQuestion(question.id)}
              onRegenerateQuestion={
                onRegenerateQuestion ? () => onRegenerateQuestion(question.id) : undefined
              }
            />
          ))}

          <Button
            variant="outline"
            size="sm"
            className="w-full border-dashed"
            onClick={() => onAddQuestion(sectionIndex)}
            disabled={editingQuestionId !== null || disabled}
          >
            <Plus className="h-3.5 w-3.5 mr-1.5" />
            Add Question
          </Button>
        </div>
      )}
    </div>
  );
}
