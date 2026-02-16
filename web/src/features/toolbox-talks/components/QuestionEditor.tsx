'use client';

import { useFieldArray, type UseFormReturn } from 'react-hook-form';
import { PlusIcon, TrashIcon, XIcon } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
  FormDescription,
} from '@/components/ui/form';
import type { QuestionType } from '@/types/toolbox-talks';
import { cn } from '@/lib/utils';

interface QuestionEditorProps {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  form: UseFormReturn<any>;
  fieldName?: string;
}

const QUESTION_TYPE_OPTIONS: { value: QuestionType; label: string }[] = [
  { value: 'MultipleChoice', label: 'Multiple Choice' },
  { value: 'TrueFalse', label: 'True/False' },
  { value: 'ShortAnswer', label: 'Short Answer' },
];

export function QuestionEditor({ form, fieldName = 'questions' }: QuestionEditorProps) {
  const { fields, append, remove, update } = useFieldArray({
    control: form.control,
    name: fieldName,
  });

  const handleAddQuestion = () => {
    append({
      questionNumber: fields.length + 1,
      questionText: '',
      questionType: 'MultipleChoice' as QuestionType,
      options: ['', ''],
      correctAnswer: '',
      points: 1,
    });
  };

  const handleRemove = (index: number) => {
    remove(index);
    // Update question numbers after removal
    setTimeout(() => {
      const questions = form.getValues(fieldName);
      questions.forEach((_: unknown, idx: number) => {
        form.setValue(`${fieldName}.${idx}.questionNumber`, idx + 1);
      });
    }, 0);
  };

  const handleTypeChange = (index: number, type: QuestionType) => {
    const currentQuestion = form.getValues(`${fieldName}.${index}`);

    // Reset options and correct answer based on type
    if (type === 'TrueFalse') {
      update(index, {
        ...currentQuestion,
        questionType: type,
        options: ['True', 'False'],
        correctAnswer: '',
      });
    } else if (type === 'ShortAnswer') {
      update(index, {
        ...currentQuestion,
        questionType: type,
        options: null,
        correctAnswer: '',
      });
    } else {
      update(index, {
        ...currentQuestion,
        questionType: type,
        options: currentQuestion.options?.length >= 2 ? currentQuestion.options : ['', ''],
        correctAnswer: '',
      });
    }
  };

  const handleAddOption = (questionIndex: number) => {
    const currentOptions = form.getValues(`${fieldName}.${questionIndex}.options`) || [];
    form.setValue(`${fieldName}.${questionIndex}.options`, [...currentOptions, '']);
  };

  const handleRemoveOption = (questionIndex: number, optionIndex: number) => {
    const currentOptions = form.getValues(`${fieldName}.${questionIndex}.options`) || [];
    const newOptions = currentOptions.filter((_: string, idx: number) => idx !== optionIndex);
    form.setValue(`${fieldName}.${questionIndex}.options`, newOptions);

    // Clear correct answer if it was the removed option
    const correctAnswer = form.getValues(`${fieldName}.${questionIndex}.correctAnswer`);
    if (correctAnswer === currentOptions[optionIndex]) {
      form.setValue(`${fieldName}.${questionIndex}.correctAnswer`, '');
    }
  };

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between">
        <CardTitle>Quiz Questions</CardTitle>
        <Button type="button" variant="outline" size="sm" onClick={handleAddQuestion}>
          <PlusIcon className="mr-2 h-4 w-4" />
          Add Question
        </Button>
      </CardHeader>
      <CardContent className="space-y-4">
        {fields.length === 0 ? (
          <div className="rounded-lg border border-dashed p-8 text-center">
            <p className="text-muted-foreground">No questions added yet.</p>
            <p className="text-sm text-muted-foreground mt-1">
              Add questions to create a quiz for this toolbox talk.
            </p>
            <Button
              type="button"
              variant="outline"
              size="sm"
              className="mt-4"
              onClick={handleAddQuestion}
            >
              <PlusIcon className="mr-2 h-4 w-4" />
              Add First Question
            </Button>
          </div>
        ) : (
          <div className="space-y-6">
            {fields.map((field, index) => {
              const questionType = form.watch(`${fieldName}.${index}.questionType`) as QuestionType;
              const options = form.watch(`${fieldName}.${index}.options`) as string[] | null;

              return (
                <div
                  key={field.id}
                  className="rounded-lg border bg-card p-4 space-y-4"
                >
                  {/* Question header */}
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-2">
                      <Badge variant="outline">Q{index + 1}</Badge>
                      <Badge variant="secondary">
                        {QUESTION_TYPE_OPTIONS.find(o => o.value === questionType)?.label || questionType}
                      </Badge>
                    </div>
                    <Button
                      type="button"
                      variant="ghost"
                      size="icon"
                      className={cn(
                        'h-8 w-8 text-destructive hover:text-destructive hover:bg-destructive/10'
                      )}
                      onClick={() => handleRemove(index)}
                    >
                      <TrashIcon className="h-4 w-4" />
                    </Button>
                  </div>

                  <div className="grid gap-4 sm:grid-cols-2">
                    {/* Question type */}
                    <FormField
                      control={form.control}
                      name={`${fieldName}.${index}.questionType`}
                      render={({ field: typeField }) => (
                        <FormItem>
                          <FormLabel>Question Type *</FormLabel>
                          <Select
                            value={typeField.value}
                            onValueChange={(value) => handleTypeChange(index, value as QuestionType)}
                          >
                            <FormControl>
                              <SelectTrigger>
                                <SelectValue placeholder="Select type" />
                              </SelectTrigger>
                            </FormControl>
                            <SelectContent>
                              {QUESTION_TYPE_OPTIONS.map((option) => (
                                <SelectItem key={option.value} value={option.value}>
                                  {option.label}
                                </SelectItem>
                              ))}
                            </SelectContent>
                          </Select>
                          <FormMessage />
                        </FormItem>
                      )}
                    />

                    {/* Points */}
                    <FormField
                      control={form.control}
                      name={`${fieldName}.${index}.points`}
                      render={({ field: pointsField }) => (
                        <FormItem>
                          <FormLabel>Points</FormLabel>
                          <FormControl>
                            <Input
                              type="number"
                              min={1}
                              {...pointsField}
                              onChange={(e) => pointsField.onChange(Number(e.target.value) || 1)}
                            />
                          </FormControl>
                          <FormMessage />
                        </FormItem>
                      )}
                    />
                  </div>

                  {/* Question text */}
                  <FormField
                    control={form.control}
                    name={`${fieldName}.${index}.questionText`}
                    render={({ field: textField }) => (
                      <FormItem>
                        <FormLabel>Question Text *</FormLabel>
                        <FormControl>
                          <Textarea
                            placeholder="Enter your question..."
                            className="min-h-[80px]"
                            {...textField}
                          />
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />

                  {/* Options for Multiple Choice */}
                  {questionType === 'MultipleChoice' && (
                    <div className="space-y-3">
                      <div className="flex items-center justify-between">
                        <FormLabel>Answer Options *</FormLabel>
                        <Button
                          type="button"
                          variant="ghost"
                          size="sm"
                          onClick={() => handleAddOption(index)}
                          disabled={(options?.length || 0) >= 6}
                        >
                          <PlusIcon className="mr-1 h-3 w-3" />
                          Add Option
                        </Button>
                      </div>
                      <div className="space-y-2">
                        {options?.map((_, optionIndex) => (
                          <div key={optionIndex} className="flex items-center gap-2">
                            <span className="text-sm text-muted-foreground w-6">
                              {String.fromCharCode(65 + optionIndex)}.
                            </span>
                            <FormField
                              control={form.control}
                              name={`${fieldName}.${index}.options.${optionIndex}`}
                              render={({ field: optionField }) => (
                                <FormItem className="flex-1 space-y-0">
                                  <FormControl>
                                    <Input
                                      placeholder={`Option ${String.fromCharCode(65 + optionIndex)}`}
                                      {...optionField}
                                    />
                                  </FormControl>
                                  <FormMessage />
                                </FormItem>
                              )}
                            />
                            {(options?.length || 0) > 2 && (
                              <Button
                                type="button"
                                variant="ghost"
                                size="icon"
                                className="h-8 w-8 shrink-0"
                                onClick={() => handleRemoveOption(index, optionIndex)}
                              >
                                <XIcon className="h-4 w-4" />
                              </Button>
                            )}
                          </div>
                        ))}
                      </div>
                    </div>
                  )}

                  {/* Correct answer */}
                  <FormField
                    control={form.control}
                    name={`${fieldName}.${index}.correctAnswer`}
                    render={({ field: answerField }) => (
                      <FormItem>
                        <FormLabel>Correct Answer *</FormLabel>
                        {questionType === 'MultipleChoice' ? (
                          <Select
                            value={answerField.value || ''}
                            onValueChange={answerField.onChange}
                          >
                            <FormControl>
                              <SelectTrigger>
                                <SelectValue placeholder="Select correct answer" />
                              </SelectTrigger>
                            </FormControl>
                            <SelectContent>
                              {options?.filter(Boolean).map((option, optIdx) => (
                                <SelectItem key={optIdx} value={option}>
                                  {String.fromCharCode(65 + optIdx)}. {option}
                                </SelectItem>
                              ))}
                            </SelectContent>
                          </Select>
                        ) : questionType === 'TrueFalse' ? (
                          <Select
                            value={answerField.value || ''}
                            onValueChange={answerField.onChange}
                          >
                            <FormControl>
                              <SelectTrigger>
                                <SelectValue placeholder="Select correct answer" />
                              </SelectTrigger>
                            </FormControl>
                            <SelectContent>
                              <SelectItem value="True">True</SelectItem>
                              <SelectItem value="False">False</SelectItem>
                            </SelectContent>
                          </Select>
                        ) : (
                          <FormControl>
                            <Input
                              placeholder="Enter the expected answer..."
                              {...answerField}
                            />
                          </FormControl>
                        )}
                        <FormDescription>
                          {questionType === 'ShortAnswer'
                            ? 'Enter the expected answer. Responses will be matched against this.'
                            : 'Select the correct answer from the options above.'}
                        </FormDescription>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                </div>
              );
            })}
          </div>
        )}

        {/* Validation error for questions array */}
        {form.formState.errors[fieldName]?.root && (
          <p className="text-sm text-destructive">
            {form.formState.errors[fieldName]?.root?.message as string}
          </p>
        )}
      </CardContent>
    </Card>
  );
}
