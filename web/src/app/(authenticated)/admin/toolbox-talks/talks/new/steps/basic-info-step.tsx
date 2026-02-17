'use client';

import { useState } from 'react';
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
import { TOOLBOX_TALK_CATEGORIES } from '@/features/toolbox-talks/constants';
import type { ToolboxTalkWizardData } from '../page';

const basicInfoSchema = z.object({
  title: z
    .string()
    .min(5, 'Title must be at least 5 characters')
    .max(200, 'Title must be less than 200 characters'),
  description: z
    .string()
    .min(10, 'Description must be at least 10 characters')
    .max(2000, 'Description must be less than 2000 characters'),
  category: z.string().min(1, 'Please select a category'),
  frequency: z.enum(['Once', 'Weekly', 'Monthly', 'Annually']),
});

type BasicInfoForm = z.infer<typeof basicInfoSchema>;


const FREQUENCY_OPTIONS = [
  { value: 'Once', label: 'Once', description: 'One-time training' },
  { value: 'Weekly', label: 'Weekly', description: 'Repeat every week' },
  { value: 'Monthly', label: 'Monthly', description: 'Repeat every month' },
  { value: 'Annually', label: 'Annually', description: 'Repeat every year' },
] as const;

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

  const form = useForm<BasicInfoForm>({
    resolver: zodResolver(basicInfoSchema),
    defaultValues: {
      title: data.title,
      description: data.description,
      category: data.category,
      frequency: data.frequency,
    },
  });

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

          <FormField
            control={form.control}
            name="title"
            render={({ field }) => (
              <FormItem>
                <FormLabel>Title *</FormLabel>
                <FormControl>
                  <Input
                    placeholder="e.g., Working at Heights Safety Training"
                    {...field}
                  />
                </FormControl>
                <FormDescription>
                  A clear, descriptive title for this safety training
                </FormDescription>
                <FormMessage />
              </FormItem>
            )}
          />

          <div className="grid gap-4 sm:grid-cols-2">
            <FormField
              control={form.control}
              name="category"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Category *</FormLabel>
                  <Select onValueChange={field.onChange} defaultValue={field.value}>
                    <FormControl>
                      <SelectTrigger>
                        <SelectValue placeholder="Select a category" />
                      </SelectTrigger>
                    </FormControl>
                    <SelectContent>
                      {TOOLBOX_TALK_CATEGORIES.map((category) => (
                        <SelectItem key={category} value={category}>
                          {category}
                        </SelectItem>
                      ))}
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
                      {FREQUENCY_OPTIONS.map((option) => (
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
