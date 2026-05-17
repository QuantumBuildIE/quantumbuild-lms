'use client';

import { useState, useEffect, useRef } from 'react';
import { useFieldArray, type UseFormReturn } from 'react-hook-form';
import {
  GripVerticalIcon,
  PlusIcon,
  TrashIcon,
  ChevronUpIcon,
  ChevronDownIcon,
  AlertCircleIcon,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Checkbox } from '@/components/ui/checkbox';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
  FormDescription,
} from '@/components/ui/form';
import { cn } from '@/lib/utils';

interface SectionFormData {
  sectionNumber: number;
  title: string;
  content: string;
  requiresAcknowledgment: boolean;
}

interface SectionEditorProps {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  form: UseFormReturn<any>;
  fieldName?: string;
}

export function SectionEditor({ form, fieldName = 'sections' }: SectionEditorProps) {
  const { fields, append, remove, move } = useFieldArray({
    control: form.control,
    name: fieldName,
  });

  const [openSections, setOpenSections] = useState<Set<string>>(
    () => new Set(fields.map((f) => f.id))
  );

  const prevFieldCountRef = useRef(fields.length);

  // Auto-open newly appended sections
  useEffect(() => {
    if (fields.length > prevFieldCountRef.current) {
      const newField = fields[fields.length - 1];
      if (newField) {
        setOpenSections((prev) => new Set([...prev, newField.id]));
      }
    }
    prevFieldCountRef.current = fields.length;
  }, [fields]);

  // Auto-open any section that gains a validation error
  useEffect(() => {
    const sectionErrors = form.formState.errors[fieldName] as
      | Record<number, object>
      | undefined;
    if (!sectionErrors) return;
    const erroredIds = fields
      .filter((_, i) => {
        const err = sectionErrors[i];
        return err && typeof err === 'object' && Object.keys(err).length > 0;
      })
      .map((f) => f.id);
    if (erroredIds.length === 0) return;
    setOpenSections((prev) => {
      const next = new Set(prev);
      erroredIds.forEach((id) => next.add(id));
      return next;
    });
  }, [form.formState.errors, fieldName, fields]);

  const sectionHasErrors = (index: number): boolean => {
    const errors = form.formState.errors[fieldName] as
      | Record<number, object>
      | undefined;
    if (!errors) return false;
    const err = errors[index];
    return !!(err && typeof err === 'object' && Object.keys(err).length > 0);
  };

  const toggleSection = (id: string) => {
    setOpenSections((prev) => {
      const next = new Set(prev);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  };

  const handleAddSection = () => {
    append({
      sectionNumber: fields.length + 1,
      title: '',
      content: '',
      requiresAcknowledgment: false,
    });
  };

  const handleMoveUp = (index: number) => {
    if (index > 0) {
      move(index, index - 1);
      updateSectionNumbers();
    }
  };

  const handleMoveDown = (index: number) => {
    if (index < fields.length - 1) {
      move(index, index + 1);
      updateSectionNumbers();
    }
  };

  const handleRemove = (index: number) => {
    remove(index);
    setTimeout(() => updateSectionNumbers(), 0);
  };

  const updateSectionNumbers = () => {
    const sections = form.getValues(fieldName) as SectionFormData[];
    sections.forEach((_, index) => {
      form.setValue(`${fieldName}.${index}.sectionNumber`, index + 1);
    });
  };

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between">
        <CardTitle>Sections</CardTitle>
        <Button type="button" variant="outline" size="sm" onClick={handleAddSection}>
          <PlusIcon className="mr-2 h-4 w-4" />
          Add Section
        </Button>
      </CardHeader>
      <CardContent className="space-y-4">
        {fields.length === 0 ? (
          <div className="rounded-lg border border-dashed p-8 text-center">
            <p className="text-muted-foreground">No sections added yet.</p>
            <p className="text-sm text-muted-foreground mt-1">
              Add sections to structure the content of your learning.
            </p>
            <Button
              type="button"
              variant="outline"
              size="sm"
              className="mt-4"
              onClick={handleAddSection}
            >
              <PlusIcon className="mr-2 h-4 w-4" />
              Add First Section
            </Button>
          </div>
        ) : (
          <div className="space-y-4">
            {fields.map((field, index) => {
              const isOpen = openSections.has(field.id);
              const hasErrors = sectionHasErrors(index);
              return (
                <div
                  key={field.id}
                  className={cn(
                    'rounded-lg border bg-card',
                    hasErrors && 'border-destructive'
                  )}
                >
                  {/* Section header */}
                  <div className="flex items-center justify-between gap-2 px-4 py-3">
                    <div className="flex items-center gap-2">
                      <GripVerticalIcon className="h-5 w-5 text-muted-foreground cursor-grab" />
                      <span className="font-semibold text-sm">
                        Section {index + 1}
                      </span>
                      {hasErrors && (
                        <AlertCircleIcon className="h-4 w-4 text-destructive" />
                      )}
                    </div>
                    <div className="flex items-center gap-1">
                      <Button
                        type="button"
                        variant="ghost"
                        size="icon"
                        className="h-8 w-8"
                        onClick={() => handleMoveUp(index)}
                        disabled={index === 0}
                      >
                        <ChevronUpIcon className="h-4 w-4" />
                      </Button>
                      <Button
                        type="button"
                        variant="ghost"
                        size="icon"
                        className="h-8 w-8"
                        onClick={() => handleMoveDown(index)}
                        disabled={index === fields.length - 1}
                      >
                        <ChevronDownIcon className="h-4 w-4" />
                      </Button>
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
                      <Button
                        type="button"
                        variant="ghost"
                        size="icon"
                        className="h-8 w-8"
                        onClick={() => toggleSection(field.id)}
                        aria-expanded={isOpen}
                        aria-label={isOpen ? 'Collapse section' : 'Expand section'}
                      >
                        <ChevronDownIcon
                          className={cn(
                            'h-4 w-4 transition-transform duration-200',
                            isOpen && 'rotate-180'
                          )}
                        />
                      </Button>
                    </div>
                  </div>

                  {/* Collapsible content */}
                  {isOpen && (
                    <div className="px-4 pb-4 space-y-4">
                      <FormField
                        control={form.control}
                        name={`${fieldName}.${index}.title`}
                        render={({ field }) => (
                          <FormItem>
                            <FormLabel>Title *</FormLabel>
                            <FormControl>
                              <Input
                                placeholder="Enter section title..."
                                {...field}
                              />
                            </FormControl>
                            <FormMessage />
                          </FormItem>
                        )}
                      />

                      <FormField
                        control={form.control}
                        name={`${fieldName}.${index}.content`}
                        render={({ field }) => (
                          <FormItem>
                            <FormLabel>Content *</FormLabel>
                            <FormControl>
                              <Textarea
                                placeholder="Enter section content... (Supports markdown formatting)"
                                className="min-h-[150px]"
                                {...field}
                              />
                            </FormControl>
                            <FormDescription>
                              Use markdown formatting for rich text content.
                            </FormDescription>
                            <FormMessage />
                          </FormItem>
                        )}
                      />

                      <FormField
                        control={form.control}
                        name={`${fieldName}.${index}.requiresAcknowledgment`}
                        render={({ field }) => (
                          <FormItem className="flex flex-row items-start space-x-3 space-y-0 rounded-md border p-4">
                            <FormControl>
                              <Checkbox
                                checked={field.value}
                                onCheckedChange={field.onChange}
                              />
                            </FormControl>
                            <div className="space-y-1 leading-none">
                              <FormLabel>Requires Acknowledgment</FormLabel>
                              <FormDescription>
                                Employees must explicitly acknowledge they have read and understood this section.
                              </FormDescription>
                            </div>
                          </FormItem>
                        )}
                      />
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        )}

        {/* Validation error for sections array */}
        {form.formState.errors[fieldName] && (
          <p className="text-sm text-destructive">
            {(form.formState.errors[fieldName]?.root?.message ||
              form.formState.errors[fieldName]?.message) as string}
          </p>
        )}
      </CardContent>
    </Card>
  );
}
