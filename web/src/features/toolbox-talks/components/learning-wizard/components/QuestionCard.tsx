'use client';

import { useState } from 'react';
import { useForm, useFieldArray } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Check, ChevronDown, ChevronUp, Edit2, GripVertical, Plus, Trash2, X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { cn } from '@/lib/utils';
import { questionSchema, type QuestionFormData } from '../schemas/questionSchema';
import type { ToolboxTalkQuestion } from '@/types/toolbox-talks';

interface QuestionCardProps {
  question: ToolboxTalkQuestion;
  index: number;
  onSave: (data: QuestionFormData) => void;
  onDelete: () => void;
  isSaving?: boolean;
}

export function QuestionCard({ question, index, onSave, onDelete, isSaving }: QuestionCardProps) {
  const [isEditing, setIsEditing] = useState(false);
  const [isExpanded, setIsExpanded] = useState(false);

  const form = useForm<QuestionFormData>({
    resolver: zodResolver(questionSchema),
    defaultValues: {
      id: question.id,
      questionNumber: question.questionNumber,
      questionText: question.questionText,
      questionType: question.questionType,
      options: question.options ?? ['', '', '', ''],
      correctOptionIndex: question.correctOptionIndex ?? 0,
      points: question.points,
      source: question.source ?? 'Manual',
      isFromVideoFinalPortion: question.isFromVideoFinalPortion ?? false,
      videoTimestamp: question.videoTimestamp ?? null,
    },
  });

  const { fields: optionFields, append, remove } = useFieldArray({
    control: form.control,
    // @ts-expect-error - react-hook-form field array with string[] primitive
    name: 'options',
  });

  const questionType = form.watch('questionType');
  const options = form.watch('options');
  const correctOptionIndex = form.watch('correctOptionIndex');

  function handleSave(data: QuestionFormData) {
    onSave(data);
    setIsEditing(false);
    setIsExpanded(false);
  }

  function handleCancel() {
    form.reset();
    setIsEditing(false);
  }

  if (isEditing) {
    return (
      <div className="border rounded-lg p-4 bg-card space-y-4">
        <div className="flex items-center justify-between">
          <span className="text-sm font-medium text-muted-foreground">Q{question.questionNumber}</span>
          <div className="flex gap-2">
            <Button type="button" size="sm" variant="ghost" onClick={handleCancel} disabled={isSaving}>
              <X className="h-4 w-4" />
            </Button>
          </div>
        </div>

        <form onSubmit={form.handleSubmit(handleSave)} className="space-y-4">
          {/* Question type */}
          <div className="space-y-1.5">
            <Label>Question Type</Label>
            <Select
              value={questionType}
              onValueChange={(v) => form.setValue('questionType', v as QuestionFormData['questionType'])}
            >
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="MultipleChoice">Multiple Choice</SelectItem>
                <SelectItem value="TrueFalse">True / False</SelectItem>
                <SelectItem value="ShortAnswer">Short Answer</SelectItem>
              </SelectContent>
            </Select>
          </div>

          {/* Question text */}
          <div className="space-y-1.5">
            <Label>Question</Label>
            <Textarea
              {...form.register('questionText')}
              className="min-h-[80px] resize-none"
              placeholder="Enter question text…"
            />
            {form.formState.errors.questionText && (
              <p className="text-xs text-destructive">{form.formState.errors.questionText.message}</p>
            )}
          </div>

          {/* Options (MultipleChoice / TrueFalse) */}
          {questionType !== 'ShortAnswer' && (
            <div className="space-y-2">
              <Label>Options</Label>
              {questionType === 'TrueFalse' ? (
                <div className="space-y-1.5">
                  {['True', 'False'].map((opt, i) => (
                    <div key={opt} className="flex items-center gap-2">
                      <button
                        type="button"
                        onClick={() => form.setValue('correctOptionIndex', i)}
                        className={cn(
                          'h-5 w-5 rounded-full border-2 shrink-0 transition-colors',
                          correctOptionIndex === i
                            ? 'bg-primary border-primary'
                            : 'border-muted-foreground/40 hover:border-primary/60'
                        )}
                        aria-label={`Mark "${opt}" as correct`}
                      >
                        {correctOptionIndex === i && <Check className="h-3 w-3 text-primary-foreground m-auto" />}
                      </button>
                      <span className="text-sm">{opt}</span>
                    </div>
                  ))}
                </div>
              ) : (
                <div className="space-y-1.5">
                  {(options ?? []).map((opt, i) => (
                    <div key={i} className="flex items-center gap-2">
                      <button
                        type="button"
                        onClick={() => form.setValue('correctOptionIndex', i)}
                        className={cn(
                          'h-5 w-5 rounded-full border-2 shrink-0 transition-colors',
                          correctOptionIndex === i
                            ? 'bg-primary border-primary'
                            : 'border-muted-foreground/40 hover:border-primary/60'
                        )}
                        aria-label={`Mark option ${i + 1} as correct`}
                      >
                        {correctOptionIndex === i && <Check className="h-3 w-3 text-primary-foreground m-auto" />}
                      </button>
                      <Input
                        value={opt}
                        onChange={(e) => {
                          const updated = [...(options ?? [])];
                          updated[i] = e.target.value;
                          form.setValue('options', updated);
                        }}
                        placeholder={`Option ${i + 1}`}
                        className="flex-1"
                      />
                      {(options ?? []).length > 2 && (
                        <Button
                          type="button"
                          size="icon"
                          variant="ghost"
                          className="h-8 w-8 shrink-0"
                          onClick={() => remove(i)}
                        >
                          <Trash2 className="h-3.5 w-3.5" />
                        </Button>
                      )}
                    </div>
                  ))}
                  {(options ?? []).length < 6 && (
                    <Button
                      type="button"
                      variant="outline"
                      size="sm"
                      className="gap-1.5"
                      onClick={() => append('')}
                    >
                      <Plus className="h-3.5 w-3.5" />
                      Add Option
                    </Button>
                  )}
                </div>
              )}
            </div>
          )}

          <div className="flex justify-end gap-2 pt-2 border-t">
            <Button type="button" variant="outline" size="sm" onClick={handleCancel} disabled={isSaving}>
              Cancel
            </Button>
            <Button type="submit" size="sm" disabled={isSaving}>
              {isSaving ? 'Saving…' : 'Save'}
            </Button>
          </div>
        </form>
      </div>
    );
  }

  // Read mode
  const correctText =
    question.options && question.correctOptionIndex != null
      ? question.options[question.correctOptionIndex]
      : question.correctAnswer;

  return (
    <div className="border rounded-lg bg-card">
      <div className="flex items-start gap-3 p-4">
        <GripVertical className="h-4 w-4 mt-0.5 text-muted-foreground/40 shrink-0" aria-hidden="true" />
        <div className="flex-1 min-w-0">
          <div className="flex items-start gap-2">
            <span className="text-xs font-medium text-muted-foreground shrink-0 mt-0.5">
              Q{question.questionNumber}
            </span>
            <p className="text-sm leading-snug">{question.questionText}</p>
          </div>
          {isExpanded && question.options && (
            <ul className="mt-2 space-y-1 pl-5">
              {question.options.map((opt, i) => (
                <li
                  key={i}
                  className={cn(
                    'text-xs flex items-center gap-1.5',
                    i === question.correctOptionIndex ? 'text-foreground font-medium' : 'text-muted-foreground'
                  )}
                >
                  {i === question.correctOptionIndex && <Check className="h-3 w-3 text-primary shrink-0" />}
                  {i !== question.correctOptionIndex && <span className="h-3 w-3 shrink-0" />}
                  {opt}
                </li>
              ))}
            </ul>
          )}
          {isExpanded && correctText && !question.options && (
            <p className="mt-1 text-xs text-muted-foreground">Answer: <span className="text-foreground">{correctText}</span></p>
          )}
        </div>
        <div className="flex items-center gap-1 shrink-0">
          <Button
            type="button"
            size="icon"
            variant="ghost"
            className="h-7 w-7"
            onClick={() => setIsExpanded((v) => !v)}
            aria-label={isExpanded ? 'Collapse' : 'Expand'}
          >
            {isExpanded ? <ChevronUp className="h-3.5 w-3.5" /> : <ChevronDown className="h-3.5 w-3.5" />}
          </Button>
          <Button
            type="button"
            size="icon"
            variant="ghost"
            className="h-7 w-7"
            onClick={() => setIsEditing(true)}
            aria-label="Edit question"
          >
            <Edit2 className="h-3.5 w-3.5" />
          </Button>
          <Button
            type="button"
            size="icon"
            variant="ghost"
            className="h-7 w-7 text-destructive hover:text-destructive"
            onClick={onDelete}
            aria-label="Delete question"
          >
            <Trash2 className="h-3.5 w-3.5" />
          </Button>
        </div>
      </div>
    </div>
  );
}
