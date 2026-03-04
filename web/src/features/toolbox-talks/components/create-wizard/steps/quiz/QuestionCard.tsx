'use client';

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Textarea } from '@/components/ui/textarea';
import { Input } from '@/components/ui/input';
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group';
import { Label } from '@/components/ui/label';
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
import { Check, Pencil, Trash2, X, Sparkles } from 'lucide-react';
import { cn } from '@/lib/utils';
import type { QuizQuestion, QuestionType } from '@/types/content-creation';

const QUESTION_TYPE_CONFIG: Record<
  QuestionType,
  { label: string; color: string }
> = {
  MultipleChoice: {
    label: 'Multiple Choice',
    color: 'bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-200',
  },
  TrueFalse: {
    label: 'True / False',
    color:
      'bg-purple-100 text-purple-800 dark:bg-purple-900 dark:text-purple-200',
  },
  ShortAnswer: {
    label: 'Short Answer',
    color:
      'bg-amber-100 text-amber-800 dark:bg-amber-900 dark:text-amber-200',
  },
};

const DEFAULT_TRUE_FALSE_OPTIONS = ['True', 'False'];

interface QuestionCardProps {
  question: QuizQuestion;
  index: number;
  isEditing: boolean;
  onStartEdit: () => void;
  onSave: (updated: QuizQuestion) => void;
  onCancel: () => void;
  onDelete: () => void;
}

export function QuestionCard({
  question,
  index,
  isEditing,
  onStartEdit,
  onSave,
  onCancel,
  onDelete,
}: QuestionCardProps) {
  const [draft, setDraft] = useState<QuizQuestion>(question);

  const handleStartEdit = () => {
    setDraft({ ...question });
    onStartEdit();
  };

  const handleTypeChange = (newType: QuestionType) => {
    const updated = { ...draft, questionType: newType };
    if (newType === 'TrueFalse') {
      updated.options = [...DEFAULT_TRUE_FALSE_OPTIONS];
      updated.correctAnswerIndex = 0;
    } else if (newType === 'ShortAnswer') {
      updated.options = [];
      updated.correctAnswerIndex = 0;
    } else if (newType === 'MultipleChoice' && draft.questionType !== 'MultipleChoice') {
      updated.options = ['Option A', 'Option B', 'Option C', 'Option D'];
      updated.correctAnswerIndex = 0;
    }
    setDraft(updated);
  };

  const handleOptionChange = (optionIndex: number, value: string) => {
    const newOptions = [...draft.options];
    newOptions[optionIndex] = value;
    setDraft({ ...draft, options: newOptions });
  };

  const handleAddOption = () => {
    setDraft({
      ...draft,
      options: [...draft.options, `Option ${String.fromCharCode(65 + draft.options.length)}`],
    });
  };

  const handleRemoveOption = (optionIndex: number) => {
    if (draft.options.length <= 2) return;
    const newOptions = draft.options.filter((_, i) => i !== optionIndex);
    let newCorrectIndex = draft.correctAnswerIndex;
    if (optionIndex === draft.correctAnswerIndex) {
      newCorrectIndex = 0;
    } else if (optionIndex < draft.correctAnswerIndex) {
      newCorrectIndex = draft.correctAnswerIndex - 1;
    }
    setDraft({ ...draft, options: newOptions, correctAnswerIndex: newCorrectIndex });
  };

  const typeConfig = QUESTION_TYPE_CONFIG[question.questionType];

  if (isEditing) {
    return (
      <div className="rounded-lg border-2 border-primary/30 bg-muted/30 p-4 space-y-4">
        {/* Type selector */}
        <div className="space-y-2">
          <Label className="text-xs font-medium text-muted-foreground">Question Type</Label>
          <RadioGroup
            value={draft.questionType}
            onValueChange={(v) => handleTypeChange(v as QuestionType)}
            className="flex gap-4"
          >
            {(Object.keys(QUESTION_TYPE_CONFIG) as QuestionType[]).map((type) => (
              <div key={type} className="flex items-center gap-2">
                <RadioGroupItem value={type} id={`type-${question.id}-${type}`} />
                <Label htmlFor={`type-${question.id}-${type}`} className="text-sm cursor-pointer">
                  {QUESTION_TYPE_CONFIG[type].label}
                </Label>
              </div>
            ))}
          </RadioGroup>
        </div>

        {/* Question text */}
        <div className="space-y-1">
          <Label className="text-xs font-medium text-muted-foreground">Question</Label>
          <Textarea
            value={draft.questionText}
            onChange={(e) => setDraft({ ...draft, questionText: e.target.value })}
            rows={2}
            className="resize-none"
          />
        </div>

        {/* Answer options */}
        {draft.questionType === 'MultipleChoice' && (
          <div className="space-y-2">
            <Label className="text-xs font-medium text-muted-foreground">
              Options (click to mark correct)
            </Label>
            {draft.options.map((option, optIdx) => (
              <div key={optIdx} className="flex items-center gap-2">
                <button
                  type="button"
                  onClick={() => setDraft({ ...draft, correctAnswerIndex: optIdx })}
                  className={cn(
                    'flex h-6 w-6 shrink-0 items-center justify-center rounded-full border-2 transition-colors',
                    optIdx === draft.correctAnswerIndex
                      ? 'border-green-500 bg-green-500 text-white'
                      : 'border-muted-foreground/30 hover:border-green-400'
                  )}
                >
                  {optIdx === draft.correctAnswerIndex && <Check className="h-3 w-3" />}
                </button>
                <Input
                  value={option}
                  onChange={(e) => handleOptionChange(optIdx, e.target.value)}
                  className="h-8 text-sm"
                />
                {draft.options.length > 2 && (
                  <Button
                    variant="ghost"
                    size="icon"
                    className="h-6 w-6 shrink-0 text-muted-foreground hover:text-destructive"
                    onClick={() => handleRemoveOption(optIdx)}
                  >
                    <X className="h-3 w-3" />
                  </Button>
                )}
              </div>
            ))}
            {draft.options.length < 6 && (
              <Button variant="outline" size="sm" onClick={handleAddOption} className="text-xs">
                + Add Option
              </Button>
            )}
          </div>
        )}

        {draft.questionType === 'TrueFalse' && (
          <div className="space-y-2">
            <Label className="text-xs font-medium text-muted-foreground">
              Correct Answer
            </Label>
            <RadioGroup
              value={String(draft.correctAnswerIndex)}
              onValueChange={(v) => setDraft({ ...draft, correctAnswerIndex: parseInt(v) })}
              className="flex gap-4"
            >
              <div className="flex items-center gap-2">
                <RadioGroupItem value="0" id={`tf-${question.id}-true`} />
                <Label htmlFor={`tf-${question.id}-true`} className="cursor-pointer">True</Label>
              </div>
              <div className="flex items-center gap-2">
                <RadioGroupItem value="1" id={`tf-${question.id}-false`} />
                <Label htmlFor={`tf-${question.id}-false`} className="cursor-pointer">False</Label>
              </div>
            </RadioGroup>
          </div>
        )}

        {draft.questionType === 'ShortAnswer' && (
          <div className="space-y-1">
            <Label className="text-xs font-medium text-muted-foreground">Expected Answer</Label>
            <Input
              value={draft.options[0] ?? ''}
              onChange={(e) => setDraft({ ...draft, options: [e.target.value], correctAnswerIndex: 0 })}
              className="h-8 text-sm"
              placeholder="Expected answer..."
            />
          </div>
        )}

        {/* Save / Cancel */}
        <div className="flex justify-end gap-2 pt-1">
          <Button variant="outline" size="sm" onClick={onCancel}>
            Cancel
          </Button>
          <Button
            size="sm"
            onClick={() => onSave(draft)}
            disabled={!draft.questionText.trim()}
          >
            Save
          </Button>
        </div>
      </div>
    );
  }

  // Display mode
  return (
    <div className="rounded-lg border bg-card p-4 space-y-3 group">
      {/* Header row */}
      <div className="flex items-start justify-between gap-2">
        <div className="flex items-center gap-2 flex-wrap">
          <span className="text-xs font-medium text-muted-foreground">Q{index + 1}</span>
          <Badge variant="secondary" className={cn('text-xs', typeConfig.color)}>
            {typeConfig.label}
          </Badge>
          {question.isAiGenerated && (
            <Badge variant="outline" className="text-xs gap-1">
              <Sparkles className="h-3 w-3" />
              AI
            </Badge>
          )}
        </div>
        <div className="flex items-center gap-1 opacity-0 group-hover:opacity-100 transition-opacity">
          <Button variant="ghost" size="icon" className="h-7 w-7" onClick={handleStartEdit}>
            <Pencil className="h-3.5 w-3.5" />
          </Button>
          <AlertDialog>
            <AlertDialogTrigger asChild>
              <Button variant="ghost" size="icon" className="h-7 w-7 text-destructive hover:text-destructive">
                <Trash2 className="h-3.5 w-3.5" />
              </Button>
            </AlertDialogTrigger>
            <AlertDialogContent>
              <AlertDialogHeader>
                <AlertDialogTitle>Delete Question</AlertDialogTitle>
                <AlertDialogDescription>
                  Are you sure you want to delete this question? This action cannot be undone.
                </AlertDialogDescription>
              </AlertDialogHeader>
              <AlertDialogFooter>
                <AlertDialogCancel>Cancel</AlertDialogCancel>
                <AlertDialogAction onClick={onDelete} className="bg-destructive text-destructive-foreground hover:bg-destructive/90">
                  Delete
                </AlertDialogAction>
              </AlertDialogFooter>
            </AlertDialogContent>
          </AlertDialog>
        </div>
      </div>

      {/* Question text */}
      <p className="text-sm font-medium">{question.questionText}</p>

      {/* Options */}
      {question.questionType === 'MultipleChoice' && (
        <div className="space-y-1.5">
          {question.options.map((option, optIdx) => (
            <div
              key={optIdx}
              className={cn(
                'flex items-center gap-2 rounded-md px-3 py-1.5 text-sm',
                optIdx === question.correctAnswerIndex
                  ? 'bg-green-50 text-green-800 dark:bg-green-950 dark:text-green-200'
                  : 'bg-muted/50'
              )}
            >
              {optIdx === question.correctAnswerIndex ? (
                <Check className="h-3.5 w-3.5 text-green-600 shrink-0" />
              ) : (
                <span className="h-3.5 w-3.5 shrink-0" />
              )}
              <span>{option}</span>
            </div>
          ))}
        </div>
      )}

      {question.questionType === 'TrueFalse' && (
        <div className="flex gap-3">
          {DEFAULT_TRUE_FALSE_OPTIONS.map((opt, i) => (
            <span
              key={i}
              className={cn(
                'rounded-md px-3 py-1 text-sm',
                i === question.correctAnswerIndex
                  ? 'bg-green-50 text-green-800 font-medium dark:bg-green-950 dark:text-green-200'
                  : 'bg-muted/50 text-muted-foreground'
              )}
            >
              {i === question.correctAnswerIndex && <Check className="h-3 w-3 inline mr-1" />}
              {opt}
            </span>
          ))}
        </div>
      )}

      {question.questionType === 'ShortAnswer' && question.options[0] && (
        <div className="bg-green-50 dark:bg-green-950 rounded-md px-3 py-1.5 text-sm text-green-800 dark:text-green-200">
          <Check className="h-3.5 w-3.5 inline mr-1" />
          {question.options[0]}
        </div>
      )}
    </div>
  );
}
