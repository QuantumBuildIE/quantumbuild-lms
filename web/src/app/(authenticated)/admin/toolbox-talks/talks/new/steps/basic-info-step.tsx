'use client';

import { useState, useRef, useCallback } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Form,
  FormControl,
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Loader2, AlertCircle } from 'lucide-react';
import { toast } from 'sonner';
import { createToolboxTalk } from '@/lib/api/toolbox-talks';
import { useLookupValues } from '@/hooks/use-lookups';
import { FREQUENCY_VALUES, FREQUENCY_OPTIONS_WITH_DESCRIPTIONS } from '@/lib/constants/frequency';
import type { ToolboxTalkWizardData } from '../page';

const COMMON_WORDS = new Set([
  'a', 'an', 'the', 'and', 'or', 'for', 'in', 'to', 'of', 'on', 'at', 'by', 'with', 'from', 'is', 'it',
]);

function generateCodeFromTitle(title: string): string {
  const words = title.trim().split(/\s+/).filter((w) => !COMMON_WORDS.has(w.toLowerCase()));
  const acronym = words.map((w) => w.charAt(0).toUpperCase()).join('');
  return acronym ? `${acronym}-001` : '';
}

const basicInfoSchema = z.object({
  code: z.string().max(50, 'Code must be less than 50 characters').optional().or(z.literal('')),
  title: z
    .string()
    .min(5, 'Title must be at least 5 characters')
    .max(200, 'Title must be less than 200 characters'),
  description: z
    .string()
    .min(10, 'Description must be at least 10 characters')
    .max(2000, 'Description must be less than 2000 characters'),
  category: z.string().min(1, 'Please select a category'),
  frequency: z.enum(FREQUENCY_VALUES),
});

type BasicInfoForm = z.infer<typeof basicInfoSchema>;

interface BasicInfoStepProps {
  data: ToolboxTalkWizardData;
  updateData: (updates: Partial<ToolboxTalkWizardData>) => void;
  onNext: () => void;
  onCancel: () => void;
}

export function BasicInfoStep({
  data,
  updateData,
  onNext,
  onCancel,
}: BasicInfoStepProps) {
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const codeDirtyRef = useRef(!!data.code);
  const { data: categories = [], isLoading: categoriesLoading } = useLookupValues('TrainingCategory');

  const form = useForm<BasicInfoForm>({
    resolver: zodResolver(basicInfoSchema),
    defaultValues: {
      code: data.code,
      title: data.title,
      description: data.description,
      category: data.category,
      frequency: data.frequency,
    },
  });

  const handleTitleChange = useCallback(
    (value: string, onChange: (v: string) => void) => {
      onChange(value);
      if (!codeDirtyRef.current) {
        form.setValue('code', generateCodeFromTitle(value));
      }
    },
    [form]
  );

  const onSubmit = async (formData: BasicInfoForm) => {
    console.log('📝 Step 1: onSubmit called');
    console.log('📝 Current data.id:', data.id);

    setIsSubmitting(true);
    setError(null);

    try {
      // If we don't have an ID yet, create the learning as a draft
      if (!data.id) {
        console.log('📝 No ID exists, creating new learning...');
        const response = await createToolboxTalk({
          code: formData.code || undefined,
          title: formData.title,
          description: formData.description,
          category: formData.category,
          frequency: formData.frequency,
          videoSource: 'None',
          isActive: false,
          sections: [], // Empty sections for now - will be added in later steps
        });
        console.log('📝 API Response:', response);
        console.log('📝 New ID from API:', response.id);

        updateData({
          id: response.id,
          code: formData.code || '',
          title: formData.title,
          description: formData.description,
          category: formData.category,
          frequency: formData.frequency,
        });
        console.log('📝 Called updateData with new ID');

        toast.success('Draft saved');
      } else {
        console.log('📝 ID already exists, updating:', data.id);
        // Just update local state, API will be called when content is added
        updateData({
          code: formData.code || '',
          title: formData.title,
          description: formData.description,
          category: formData.category,
          frequency: formData.frequency,
        });
      }

      console.log('📝 Calling onNext()');
      onNext();
    } catch (err: unknown) {
      console.error('📝 Create failed:', err);
      const message = err instanceof Error ? err.message : 'Failed to save. Please try again.';
      setError(message);
      toast.error('Failed to save draft', { description: message });
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <Form {...form}>
      <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
        <div className="space-y-4">
          <div>
            <h2 className="text-lg font-semibold">Basic Information</h2>
            <p className="text-sm text-muted-foreground">
              Enter the title and description for this Learning
            </p>
          </div>

          {error && (
            <Alert variant="destructive">
              <AlertCircle className="h-4 w-4" />
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          )}

          <div className="flex gap-4">
            <FormField
              control={form.control}
              name="title"
              render={({ field }) => (
                <FormItem className="flex-[7]">
                  <FormLabel>Title *</FormLabel>
                  <FormControl>
                    <Input
                      placeholder="e.g., Working at Heights Safety Training"
                      {...field}
                      onChange={(e) => handleTitleChange(e.target.value, field.onChange)}
                    />
                  </FormControl>
                  <FormDescription>
                    A clear, descriptive title for this safety training
                  </FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="code"
              render={({ field }) => (
                <FormItem className="flex-[3]">
                  <FormLabel>Code</FormLabel>
                  <FormControl>
                    <Input
                      placeholder="e.g., MHS-001"
                      {...field}
                      onChange={(e) => {
                        codeDirtyRef.current = e.target.value !== '';
                        field.onChange(e.target.value);
                      }}
                    />
                  </FormControl>
                  <FormDescription>
                    Auto-generated from title. (Editable)
                  </FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <FormField
              control={form.control}
              name="category"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Category *</FormLabel>
                  <Select onValueChange={field.onChange} defaultValue={field.value} disabled={categoriesLoading}>
                    <FormControl>
                      <SelectTrigger>
                        <SelectValue placeholder={categoriesLoading ? 'Loading...' : 'Select a category'} />
                      </SelectTrigger>
                    </FormControl>
                    <SelectContent>
                      {categories.length === 0 && !categoriesLoading ? (
                        <div className="px-2 py-4 text-sm text-muted-foreground text-center">
                          No categories configured — ask your admin to set up categories
                        </div>
                      ) : (
                        categories.map((cat) => (
                          <SelectItem key={cat.id} value={cat.name}>
                            {cat.name}
                          </SelectItem>
                        ))
                      )}
                    </SelectContent>
                  </Select>
                  <FormDescription>
                    The safety category this training falls under
                  </FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="frequency"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Frequency *</FormLabel>
                  <Select onValueChange={field.onChange} defaultValue={field.value}>
                    <FormControl>
                      <SelectTrigger>
                        <SelectValue placeholder="Select frequency" />
                      </SelectTrigger>
                    </FormControl>
                    <SelectContent>
                      {FREQUENCY_OPTIONS_WITH_DESCRIPTIONS.map((option) => (
                        <SelectItem key={option.value} value={option.value}>
                          <div className="flex flex-col">
                            <span>{option.label}</span>
                            <span className="text-xs text-muted-foreground">
                              {option.description}
                            </span>
                          </div>
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  <FormDescription>
                    How often should employees complete this training?
                  </FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />
          </div>

          <FormField
            control={form.control}
            name="description"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Description *</FormLabel>
                <FormControl>
                  <Textarea
                    placeholder="Describe what employees will learn from this Learning..."
                    className="min-h-[120px]"
                    {...field}
                  />
                </FormControl>
                <FormDescription>
                  A brief overview of the training content and learning objectives.
                  This will help employees understand what to expect.
                </FormDescription>
                <FormMessage />
              </FormItem>
            )}
          />
        </div>

        <div className="flex justify-between pt-4 border-t">
          <Button type="button" variant="outline" onClick={onCancel}>
            Cancel
          </Button>
          <Button type="submit" disabled={isSubmitting}>
            {isSubmitting && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Next: Add Content
          </Button>
        </div>
      </form>
    </Form>
  );
}
