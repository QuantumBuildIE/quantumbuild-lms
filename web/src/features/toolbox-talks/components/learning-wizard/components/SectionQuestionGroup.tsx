'use client';

import { useState } from 'react';
import { ChevronDown, ChevronUp, Plus } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { QuestionCard } from './QuestionCard';
import type { ToolboxTalkQuestion } from '@/types/toolbox-talks';
import type { QuestionFormData } from '../schemas/questionSchema';

interface SectionQuestionGroupProps {
  questions: ToolboxTalkQuestion[];
  onSaveQuestion: (index: number, data: QuestionFormData) => void;
  onDeleteQuestion: (index: number) => void;
  onAddQuestion: () => void;
  isSaving?: boolean;
}

export function SectionQuestionGroup({
  questions,
  onSaveQuestion,
  onDeleteQuestion,
  onAddQuestion,
  isSaving,
}: SectionQuestionGroupProps) {
  const [isCollapsed, setIsCollapsed] = useState(false);

  return (
    <div className="border rounded-lg overflow-hidden">
      {/* Group header */}
      <button
        type="button"
        className="w-full flex items-center justify-between px-4 py-3 bg-muted/50 hover:bg-muted/80 transition-colors text-left"
        onClick={() => setIsCollapsed((v) => !v)}
        aria-expanded={!isCollapsed}
      >
        <span className="text-sm font-medium">
          Quiz Questions
          <span className="ml-2 text-xs text-muted-foreground font-normal">
            ({questions.length})
          </span>
        </span>
        {isCollapsed ? (
          <ChevronDown className="h-4 w-4 text-muted-foreground" aria-hidden="true" />
        ) : (
          <ChevronUp className="h-4 w-4 text-muted-foreground" aria-hidden="true" />
        )}
      </button>

      {/* Question list */}
      {!isCollapsed && (
        <div className="p-4 space-y-3">
          {questions.length === 0 ? (
            <p className="text-sm text-muted-foreground text-center py-4">
              No questions yet. Generate or add questions below.
            </p>
          ) : (
            questions.map((q, i) => (
              <QuestionCard
                key={q.id}
                question={q}
                index={i}
                onSave={(data) => onSaveQuestion(i, data)}
                onDelete={() => onDeleteQuestion(i)}
                isSaving={isSaving}
              />
            ))
          )}

          <Button
            type="button"
            variant="outline"
            size="sm"
            className="gap-1.5 mt-2"
            onClick={onAddQuestion}
          >
            <Plus className="h-3.5 w-3.5" />
            Add Question
          </Button>
        </div>
      )}
    </div>
  );
}
