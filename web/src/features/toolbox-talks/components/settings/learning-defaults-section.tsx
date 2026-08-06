'use client';

import { useEffect } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod/v4';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Switch } from '@/components/ui/switch';
import {
  Form,
  FormField,
  FormItem,
  FormLabel,
  FormControl,
  FormDescription,
} from '@/components/ui/form';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import {
  useToolboxTalkSettings,
  useUpdateToolboxTalkSettings,
} from '@/lib/api/toolbox-talks/use-toolbox-talks';

const learningDefaultsSchema = z.object({
  defaultVideoRightsConfirmed: z.boolean(),
  defaultUseQuestionPool: z.boolean(),
  defaultGenerateSlideshow: z.boolean(),
  defaultAutoAssign: z.boolean(),
  defaultPreserveSourceWording: z.boolean(),
  defaultShuffleQuestions: z.boolean(),
  defaultShuffleOptions: z.boolean(),
  defaultIncludeQuiz: z.boolean(),
  defaultAllowRetry: z.boolean(),
});

type LearningDefaultsFormData = z.infer<typeof learningDefaultsSchema>;

const TOGGLES: {
  name: keyof LearningDefaultsFormData;
  label: string;
  description: string;
}[] = [
  {
    name: 'defaultVideoRightsConfirmed',
    label: 'Default: Video rights confirmed',
    description:
      'New video learnings will assume rights are confirmed. Only enable if you have blanket rights for all video content.',
  },
  {
    name: 'defaultUseQuestionPool',
    label: 'Default: Draw questions from pool',
    description:
      'New learnings will draw a random subset of questions per employee. Note: different employees will be tested on different questions.',
  },
  {
    name: 'defaultGenerateSlideshow',
    label: 'Default: Generate slideshow from PDF',
    description:
      'New PDF-sourced learnings will auto-generate slideshows. Uses AI credits per learning.',
  },
  {
    name: 'defaultAutoAssign',
    label: 'Default: Auto-assign to new employees',
    description:
      'New learnings will be assigned to new employees automatically as they join.',
  },
  {
    name: 'defaultPreserveSourceWording',
    label: 'Default: Preserve source wording',
    description:
      'AI will keep original text verbatim rather than paraphrasing. Recommended for SOP or policy content.',
  },
  {
    name: 'defaultShuffleQuestions',
    label: 'Default: Shuffle quiz questions',
    description:
      'Question order will be randomized per employee. Helps prevent answer sharing.',
  },
  {
    name: 'defaultShuffleOptions',
    label: 'Default: Shuffle answer options',
    description:
      'Multiple choice options will be randomized per employee.',
  },
  {
    name: 'defaultIncludeQuiz',
    label: 'Default: Include quiz',
    description:
      'New learnings will include a knowledge check quiz.',
  },
  {
    name: 'defaultAllowRetry',
    label: 'Default: Allow quiz retry',
    description:
      'Employees can retake failed quizzes.',
  },
];

export function LearningDefaultsSection() {
  const { data: settings, isLoading } = useToolboxTalkSettings();
  const updateMutation = useUpdateToolboxTalkSettings();

  const form = useForm<LearningDefaultsFormData>({
    resolver: zodResolver(learningDefaultsSchema),
    defaultValues: {
      defaultVideoRightsConfirmed: false,
      defaultUseQuestionPool: false,
      defaultGenerateSlideshow: false,
      defaultAutoAssign: true,
      defaultPreserveSourceWording: true,
      defaultShuffleQuestions: true,
      defaultShuffleOptions: true,
      defaultIncludeQuiz: true,
      defaultAllowRetry: true,
    },
  });

  useEffect(() => {
    if (!settings) return;
    form.reset({
      defaultVideoRightsConfirmed: settings.defaultVideoRightsConfirmed,
      defaultUseQuestionPool: settings.defaultUseQuestionPool,
      defaultGenerateSlideshow: settings.defaultGenerateSlideshow,
      defaultAutoAssign: settings.defaultAutoAssign,
      defaultPreserveSourceWording: settings.defaultPreserveSourceWording,
      defaultShuffleQuestions: settings.defaultShuffleQuestions,
      defaultShuffleOptions: settings.defaultShuffleOptions,
      defaultIncludeQuiz: settings.defaultIncludeQuiz,
      defaultAllowRetry: settings.defaultAllowRetry,
    });
  }, [settings, form]);

  const onSubmit = async (values: LearningDefaultsFormData) => {
    try {
      await updateMutation.mutateAsync(values);
      toast.success('Learning defaults saved');
      form.reset(values);
    } catch {
      toast.error('Failed to save learning defaults');
    }
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle>Learning Defaults</CardTitle>
        <CardDescription>
          Set default toggle values for new learnings. Admins can override these per learning
          during creation.
        </CardDescription>
      </CardHeader>
      <CardContent>
        {isLoading ? (
          <div className="space-y-4">
            {[1, 2, 3, 4, 5, 6, 7, 8, 9].map((i) => (
              <div key={i} className="flex items-center justify-between">
                <div className="space-y-1">
                  <Skeleton className="h-4 w-48" />
                  <Skeleton className="h-3 w-72" />
                </div>
                <Skeleton className="h-6 w-11 rounded-full" />
              </div>
            ))}
          </div>
        ) : (
          <Form {...form}>
            <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
              <div className="space-y-4">
                {TOGGLES.map((toggle) => (
                  <FormField
                    key={toggle.name}
                    control={form.control}
                    name={toggle.name}
                    render={({ field }) => (
                      <FormItem className="flex flex-row items-center justify-between rounded-lg border p-4">
                        <div className="space-y-0.5">
                          <FormLabel className="text-base">{toggle.label}</FormLabel>
                          <FormDescription>{toggle.description}</FormDescription>
                        </div>
                        <FormControl>
                          <Switch
                            checked={field.value}
                            onCheckedChange={field.onChange}
                          />
                        </FormControl>
                      </FormItem>
                    )}
                  />
                ))}
              </div>

              <Button
                type="submit"
                disabled={!form.formState.isDirty || updateMutation.isPending}
              >
                {updateMutation.isPending ? 'Saving…' : 'Save learning defaults'}
              </Button>
            </form>
          </Form>
        )}
      </CardContent>
    </Card>
  );
}
