'use client';

import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useQueryClient } from '@tanstack/react-query';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Checkbox } from '@/components/ui/checkbox';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
  FormDescription,
} from '@/components/ui/form';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { SOURCE_LANGUAGE_OPTIONS, TOOLBOX_TALK_CATEGORIES } from '../constants';
import { SectionEditor } from './SectionEditor';
import { QuestionEditor } from './QuestionEditor';
import { SubtitleProcessingPanel } from './SubtitleProcessingPanel';
import { ContentTranslationPanel } from './ContentTranslationPanel';
import { useCreateToolboxTalk, useUpdateToolboxTalk, TOOLBOX_TALKS_KEY } from '@/lib/api/toolbox-talks';
import { generateSlides } from '@/lib/api/toolbox-talks/toolbox-talks';
import type {
  ToolboxTalk,
  ToolboxTalkFrequency,
  VideoSource,
  QuestionType,
} from '@/types/toolbox-talks';
import { toast } from 'sonner';

// ============================================
// Form Schema
// ============================================

const sectionSchema = z.object({
  id: z.string().uuid().optional(),
  sectionNumber: z.number().min(1),
  title: z.string().min(1, 'Title is required'),
  content: z.string().min(1, 'Content is required'),
  requiresAcknowledgment: z.boolean(),
  source: z.enum(['Manual', 'Video', 'Pdf', 'Both'] as const).optional(),
});

const questionSchema = z.object({
  id: z.string().uuid().optional(),
  questionNumber: z.number().min(1),
  questionText: z.string().min(1, 'Question text is required'),
  questionType: z.enum(['MultipleChoice', 'TrueFalse', 'ShortAnswer'] as const),
  options: z.array(z.string()).nullable(),
  correctAnswer: z.string().min(1, 'Correct answer is required'),
  points: z.number().min(1),
  source: z.enum(['Manual', 'Video', 'Pdf', 'Both'] as const).optional(),
});

const toolboxTalkFormSchema = z.object({
  title: z.string().min(1, 'Title is required').max(200),
  description: z.string().max(2000).optional().nullable(),
  category: z.string().max(100).optional().nullable(),
  frequency: z.enum(['Once', 'Weekly', 'Monthly', 'Annually'] as const),
  videoUrl: z.string().url('Must be a valid URL').optional().nullable().or(z.literal('')),
  videoSource: z.enum(['None', 'YouTube', 'GoogleDrive', 'Vimeo', 'DirectUrl'] as const),
  attachmentUrl: z.string().url('Must be a valid URL').optional().nullable().or(z.literal('')),
  minimumVideoWatchPercent: z.number().min(50).max(100),
  requiresQuiz: z.boolean(),
  passingScore: z.number().min(50).max(100).optional().nullable(),
  shuffleQuestions: z.boolean(),
  shuffleOptions: z.boolean(),
  useQuestionPool: z.boolean(),
  quizQuestionCount: z.number().min(1).optional().nullable(),
  isActive: z.boolean(),
  sourceLanguageCode: z.string().default('en'),
  autoAssignToNewEmployees: z.boolean(),
  autoAssignDueDays: z.number().min(1).max(365),
  generateSlidesFromPdf: z.boolean(),
  sections: z.array(sectionSchema),
  questions: z.array(questionSchema).optional(),
}).refine(
  (data) => {
    // Sections are required only if no video is provided (matches backend validation)
    const hasVideo = data.videoUrl && data.videoUrl.trim() !== '' && data.videoSource !== 'None';
    if (!hasVideo && data.sections.length === 0) {
      return false;
    }
    return true;
  },
  {
    message: 'At least one section is required when no video is provided',
    path: ['sections'],
  }
);

type ToolboxTalkFormValues = z.infer<typeof toolboxTalkFormSchema>;

// ============================================
// Component
// ============================================

const FREQUENCY_OPTIONS: { value: ToolboxTalkFrequency; label: string }[] = [
  { value: 'Once', label: 'Once' },
  { value: 'Weekly', label: 'Weekly' },
  { value: 'Monthly', label: 'Monthly' },
  { value: 'Annually', label: 'Annually' },
];


const VIDEO_SOURCE_OPTIONS: { value: VideoSource; label: string; description?: string }[] = [
  { value: 'None', label: 'No Video' },
  { value: 'DirectUrl', label: 'Direct URL (Recommended)', description: 'Full subtitle overlay & progress tracking' },
  { value: 'YouTube', label: 'YouTube', description: 'Embedded player (limited features)' },
  { value: 'Vimeo', label: 'Vimeo', description: 'Embedded player (limited features)' },
  { value: 'GoogleDrive', label: 'Google Drive', description: 'Embedded player (limited features)' },
];

interface ToolboxTalkFormProps {
  talk?: ToolboxTalk;
  onSuccess?: (talk: ToolboxTalk) => void;
  onCancel?: () => void;
}

export function ToolboxTalkForm({ talk, onSuccess, onCancel }: ToolboxTalkFormProps) {
  const isEditing = !!talk;

  const queryClient = useQueryClient();
  const createMutation = useCreateToolboxTalk();
  const updateMutation = useUpdateToolboxTalk();
  const [isRegenerating, setIsRegenerating] = useState(false);

  const form = useForm<ToolboxTalkFormValues>({
    resolver: zodResolver(toolboxTalkFormSchema) as any,
    defaultValues: {
      title: talk?.title ?? '',
      description: talk?.description ?? '',
      category: talk?.category ?? '',
      frequency: talk?.frequency ?? 'Once',
      videoUrl: talk?.videoUrl ?? '',
      videoSource: talk?.videoSource ?? 'None',
      attachmentUrl: talk?.attachmentUrl ?? '',
      minimumVideoWatchPercent: talk?.minimumVideoWatchPercent ?? 80,
      requiresQuiz: talk?.requiresQuiz ?? false,
      passingScore: talk?.passingScore ?? 70,
      shuffleQuestions: talk?.shuffleQuestions ?? false,
      shuffleOptions: talk?.shuffleOptions ?? false,
      useQuestionPool: talk?.useQuestionPool ?? false,
      quizQuestionCount: talk?.quizQuestionCount ?? null,
      isActive: talk?.isActive ?? true,
      sourceLanguageCode: talk?.sourceLanguageCode ?? 'en',
      autoAssignToNewEmployees: talk?.autoAssignToNewEmployees ?? false,
      autoAssignDueDays: talk?.autoAssignDueDays ?? 14,
      generateSlidesFromPdf: talk?.generateSlidesFromPdf ?? false,
      sections: talk?.sections?.map((s) => ({
        id: s.id,
        sectionNumber: s.sectionNumber,
        title: s.title,
        content: s.content,
        requiresAcknowledgment: s.requiresAcknowledgment,
        source: s.source,
      })) ?? [],
      questions: talk?.questions?.map((q) => ({
        id: q.id,
        questionNumber: q.questionNumber,
        questionText: q.questionText,
        questionType: q.questionType as QuestionType,
        options: q.options,
        correctAnswer: q.correctAnswer ?? '',
        points: q.points,
        source: q.source,
      })) ?? [],
    },
  });

  const watchRequiresQuiz = form.watch('requiresQuiz');
  const watchVideoSource = form.watch('videoSource');
  const watchUseQuestionPool = form.watch('useQuestionPool');
  const watchAutoAssign = form.watch('autoAssignToNewEmployees');
  const watchQuestions = form.watch('questions');
  const questionCount = watchQuestions?.length ?? 0;

  // Reset quiz settings when quiz is disabled
  useEffect(() => {
    if (!watchRequiresQuiz) {
      form.setValue('passingScore', null);
      form.setValue('shuffleQuestions', false);
      form.setValue('shuffleOptions', false);
      form.setValue('useQuestionPool', false);
      form.setValue('quizQuestionCount', null);
    } else if (form.getValues('passingScore') === null) {
      form.setValue('passingScore', 70);
    }
  }, [watchRequiresQuiz, form]);

  // Reset question count when question pool is disabled
  useEffect(() => {
    if (!watchUseQuestionPool) {
      form.setValue('quizQuestionCount', null);
    }
  }, [watchUseQuestionPool, form]);

  const isSubmitting = createMutation.isPending || updateMutation.isPending;

  const handleRegenerateSlides = async () => {
    if (!talk?.id) return;

    setIsRegenerating(true);
    try {
      const result = await generateSlides(talk.id);
      toast.success(`Regenerated ${result.slidesGenerated} slides`);
      queryClient.invalidateQueries({ queryKey: ['toolbox-talk', talk.id] });
    } catch {
      toast.error('Failed to regenerate slides');
    } finally {
      setIsRegenerating(false);
    }
  };

  async function onSubmit(values: ToolboxTalkFormValues) {
    // Custom validation for quiz requirements
    if (values.requiresQuiz) {
      if (!values.questions || values.questions.length === 0) {
        form.setError('questions', { message: 'At least one question is required when quiz is enabled' });
        return;
      }
      if (values.passingScore == null) {
        form.setError('passingScore', { message: 'Passing score is required when quiz is enabled' });
        return;
      }
    }

    try {
      // Transform form values to API request format
      const sections = values.sections.map((s) => ({
        id: s.id,
        sectionNumber: s.sectionNumber,
        title: s.title,
        content: s.content,
        requiresAcknowledgment: s.requiresAcknowledgment,
        source: s.source,
      }));

      const questions = values.requiresQuiz && values.questions
        ? values.questions.map((q) => ({
            id: q.id,
            questionNumber: q.questionNumber,
            questionText: q.questionText,
            questionType: q.questionType,
            options: q.options?.filter(Boolean) ?? undefined,
            correctAnswer: q.correctAnswer,
            points: q.points,
            source: q.source,
          }))
        : undefined;

      const requestData = {
        title: values.title,
        description: values.description || undefined,
        category: values.category || undefined,
        frequency: values.frequency,
        videoUrl: values.videoUrl || undefined,
        videoSource: values.videoSource,
        attachmentUrl: values.attachmentUrl || undefined,
        minimumVideoWatchPercent: values.minimumVideoWatchPercent,
        requiresQuiz: values.requiresQuiz,
        passingScore: values.requiresQuiz && values.passingScore != null ? values.passingScore : undefined,
        shuffleQuestions: values.requiresQuiz ? values.shuffleQuestions : false,
        shuffleOptions: values.requiresQuiz ? values.shuffleOptions : false,
        useQuestionPool: values.requiresQuiz ? values.useQuestionPool : false,
        quizQuestionCount: values.requiresQuiz && values.useQuestionPool ? values.quizQuestionCount : undefined,
        isActive: values.isActive,
        sourceLanguageCode: values.sourceLanguageCode,
        autoAssignToNewEmployees: values.autoAssignToNewEmployees,
        autoAssignDueDays: values.autoAssignDueDays,
        generateSlidesFromPdf: values.generateSlidesFromPdf,
        sections,
        questions,
      };

      if (isEditing && talk) {
        const result = await updateMutation.mutateAsync({
          id: talk.id,
          data: {
            id: talk.id,
            ...requestData,
          },
        });
        toast.success('Toolbox talk updated successfully');
        onSuccess?.(result);
      } else {
        const result = await createMutation.mutateAsync(requestData);
        toast.success('Toolbox talk created successfully');
        onSuccess?.(result);
      }
    } catch (error) {
      const message = error instanceof Error ? error.message : 'An error occurred';
      toast.error(isEditing ? 'Failed to update toolbox talk' : 'Failed to create toolbox talk', {
        description: message,
      });
    }
  }

  return (
    <Form {...form}>
      <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
        {/* Basic Information */}
        <Card>
          <CardHeader>
            <CardTitle>Basic Information</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="grid gap-4 sm:grid-cols-2">
              <FormField
                control={form.control}
                name="title"
                render={({ field }) => (
                  <FormItem className="sm:col-span-2">
                    <FormLabel>Title *</FormLabel>
                    <FormControl>
                      <Input placeholder="Enter toolbox talk title..." {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <FormField
                control={form.control}
                name="description"
                render={({ field }) => (
                  <FormItem className="sm:col-span-2">
                    <FormLabel>Description</FormLabel>
                    <FormControl>
                      <Textarea
                        placeholder="Enter a brief description..."
                        className="min-h-[100px]"
                        {...field}
                        value={field.value ?? ''}
                      />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <FormField
                control={form.control}
                name="category"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Category</FormLabel>
                    <Select value={field.value || ''} onValueChange={field.onChange}>
                      <FormControl>
                        <SelectTrigger>
                          <SelectValue placeholder="Select category" />
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
                    <Select value={field.value} onValueChange={field.onChange}>
                      <FormControl>
                        <SelectTrigger>
                          <SelectValue placeholder="Select frequency" />
                        </SelectTrigger>
                      </FormControl>
                      <SelectContent>
                        {FREQUENCY_OPTIONS.map((option) => (
                          <SelectItem key={option.value} value={option.value}>
                            {option.label}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    <FormDescription>
                      How often should employees complete this talk?
                    </FormDescription>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <FormField
                control={form.control}
                name="isActive"
                render={({ field }) => (
                  <FormItem className="flex flex-row items-center justify-between rounded-lg border p-4">
                    <div className="space-y-0.5">
                      <FormLabel className="text-base">Active</FormLabel>
                      <FormDescription>
                        Only active talks can be scheduled
                      </FormDescription>
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
              <FormField
                control={form.control}
                name="sourceLanguageCode"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Original Language</FormLabel>
                    <Select value={field.value} onValueChange={field.onChange}>
                      <FormControl>
                        <SelectTrigger>
                          <SelectValue placeholder="Select language" />
                        </SelectTrigger>
                      </FormControl>
                      <SelectContent>
                        {SOURCE_LANGUAGE_OPTIONS.map((option) => (
                          <SelectItem key={option.value} value={option.value}>
                            {option.label}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    <FormDescription>
                      The language of the original content. Translations will be generated from this language.
                    </FormDescription>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </div>
          </CardContent>
        </Card>

        {/* Video Settings */}
        <Card>
          <CardHeader>
            <CardTitle>Video Content</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="grid gap-4 sm:grid-cols-2">
              <FormField
                control={form.control}
                name="videoSource"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Video Source</FormLabel>
                    <Select value={field.value} onValueChange={field.onChange}>
                      <FormControl>
                        <SelectTrigger>
                          <SelectValue placeholder="Select source" />
                        </SelectTrigger>
                      </FormControl>
                      <SelectContent>
                        {VIDEO_SOURCE_OPTIONS.map((option) => (
                          <SelectItem key={option.value} value={option.value}>
                            <div className="flex flex-col items-start">
                              <span>{option.label}</span>
                              {option.description && (
                                <span className="text-xs text-muted-foreground">{option.description}</span>
                              )}
                            </div>
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    <FormDescription>
                      Direct URL (Cloudflare R2, S3, etc.) enables subtitle overlay and accurate progress tracking.
                    </FormDescription>
                    <FormMessage />
                  </FormItem>
                )}
              />

              {watchVideoSource !== 'None' && (
                <FormField
                  control={form.control}
                  name="minimumVideoWatchPercent"
                  render={({ field }) => (
                    <FormItem>
                      <div className="flex items-center justify-between">
                        <FormLabel>Minimum Watch %</FormLabel>
                        <span className="text-2xl font-bold text-primary">
                          {field.value}%
                        </span>
                      </div>
                      <FormControl>
                        <input
                          type="range"
                          min={50}
                          max={100}
                          step={5}
                          value={field.value}
                          onChange={(e) => field.onChange(parseInt(e.target.value))}
                          className="w-full h-2 bg-muted rounded-lg appearance-none cursor-pointer accent-primary"
                        />
                      </FormControl>
                      <div className="flex justify-between text-xs text-muted-foreground">
                        <span>50%</span>
                        <span>75%</span>
                        <span>100%</span>
                      </div>
                      <FormDescription>
                        Employees must watch at least this percentage of the video
                      </FormDescription>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              )}

              {watchVideoSource !== 'None' && (
                <FormField
                  control={form.control}
                  name="videoUrl"
                  render={({ field }) => (
                    <FormItem className="sm:col-span-2">
                      <FormLabel>Video URL</FormLabel>
                      <FormControl>
                        <Input
                          placeholder={
                            watchVideoSource === 'DirectUrl'
                              ? 'https://pub-xxx.r2.dev/videos/training.mp4'
                              : 'https://...'
                          }
                          {...field}
                          value={field.value ?? ''}
                        />
                      </FormControl>
                      <FormDescription>
                        {watchVideoSource === 'YouTube' && (
                          <>Enter YouTube video URL. <span className="text-amber-600">Note: Subtitles will not overlay; progress tracking is limited.</span></>
                        )}
                        {watchVideoSource === 'Vimeo' && (
                          <>Enter Vimeo video URL. <span className="text-amber-600">Note: Subtitles will not overlay; progress tracking is limited.</span></>
                        )}
                        {watchVideoSource === 'GoogleDrive' && (
                          <>Enter Google Drive video URL. <span className="text-amber-600">Note: Subtitles will not overlay; progress tracking is limited due to CORS restrictions.</span></>
                        )}
                        {watchVideoSource === 'DirectUrl' && (
                          <>Enter direct video file URL (e.g., Cloudflare R2, S3, or any publicly accessible video URL). <span className="text-green-600">Supports subtitle overlay and accurate progress tracking.</span></>
                        )}
                      </FormDescription>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              )}

              <FormField
                control={form.control}
                name="attachmentUrl"
                render={({ field }) => (
                  <FormItem className="sm:col-span-2">
                    <FormLabel>Attachment URL</FormLabel>
                    <FormControl>
                      <Input
                        placeholder="https://..."
                        {...field}
                        value={field.value ?? ''}
                      />
                    </FormControl>
                    <FormDescription>
                      Link to downloadable attachment (PDF, document, etc.)
                    </FormDescription>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </div>
          </CardContent>
        </Card>

        {/* Quiz Settings */}
        <Card>
          <CardHeader>
            <CardTitle>Quiz Settings</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <FormField
              control={form.control}
              name="requiresQuiz"
              render={({ field }) => (
                <FormItem className="flex flex-row items-start space-x-3 space-y-0 rounded-md border p-4">
                  <FormControl>
                    <Checkbox
                      checked={field.value}
                      onCheckedChange={field.onChange}
                    />
                  </FormControl>
                  <div className="space-y-1 leading-none">
                    <FormLabel>Requires Quiz</FormLabel>
                    <FormDescription>
                      Employees must pass a quiz to complete this toolbox talk.
                    </FormDescription>
                  </div>
                </FormItem>
              )}
            />

            {watchRequiresQuiz && (
              <>
                <FormField
                  control={form.control}
                  name="passingScore"
                  render={({ field }) => (
                    <FormItem>
                      <div className="flex items-center justify-between">
                        <FormLabel>Passing Score (%) *</FormLabel>
                        <span className="text-2xl font-bold text-primary">
                          {field.value ?? 70}%
                        </span>
                      </div>
                      <FormControl>
                        <input
                          type="range"
                          min={50}
                          max={100}
                          step={5}
                          value={field.value ?? 70}
                          onChange={(e) => field.onChange(parseInt(e.target.value))}
                          className="w-full h-2 bg-muted rounded-lg appearance-none cursor-pointer accent-primary"
                        />
                      </FormControl>
                      <div className="flex justify-between text-xs text-muted-foreground">
                        <span>50%</span>
                        <span>75%</span>
                        <span>100%</span>
                      </div>
                      <FormDescription>
                        Minimum percentage required to pass the quiz
                      </FormDescription>
                      <FormMessage />
                    </FormItem>
                  )}
                />

                <div className="space-y-4 rounded-lg border p-4">
                  <h4 className="text-sm font-medium">Randomization Settings</h4>
                  <p className="text-sm text-muted-foreground">
                    Control how quiz questions and answer options are presented to employees.
                  </p>

                  <FormField
                    control={form.control}
                    name="shuffleQuestions"
                    render={({ field }) => (
                      <FormItem className="flex flex-row items-start space-x-3 space-y-0">
                        <FormControl>
                          <Checkbox
                            checked={field.value}
                            onCheckedChange={field.onChange}
                          />
                        </FormControl>
                        <div className="space-y-1 leading-none">
                          <FormLabel>Shuffle Question Order</FormLabel>
                          <FormDescription>
                            Randomize the order questions appear in for each attempt
                          </FormDescription>
                        </div>
                      </FormItem>
                    )}
                  />

                  <FormField
                    control={form.control}
                    name="shuffleOptions"
                    render={({ field }) => (
                      <FormItem className="flex flex-row items-start space-x-3 space-y-0">
                        <FormControl>
                          <Checkbox
                            checked={field.value}
                            onCheckedChange={field.onChange}
                          />
                        </FormControl>
                        <div className="space-y-1 leading-none">
                          <FormLabel>Shuffle Answer Options</FormLabel>
                          <FormDescription>
                            Randomize the order of multiple-choice answer options
                          </FormDescription>
                        </div>
                      </FormItem>
                    )}
                  />

                  <FormField
                    control={form.control}
                    name="useQuestionPool"
                    render={({ field }) => (
                      <FormItem className="flex flex-row items-start space-x-3 space-y-0">
                        <FormControl>
                          <Checkbox
                            checked={field.value}
                            onCheckedChange={field.onChange}
                          />
                        </FormControl>
                        <div className="space-y-1 leading-none">
                          <FormLabel>Use Question Pool</FormLabel>
                          <FormDescription>
                            Show a random subset of questions per attempt instead of all questions
                          </FormDescription>
                        </div>
                      </FormItem>
                    )}
                  />

                  {watchUseQuestionPool && (
                    <div className="ml-7 space-y-2">
                      <FormField
                        control={form.control}
                        name="quizQuestionCount"
                        render={({ field }) => (
                          <FormItem className="max-w-xs">
                            <FormLabel>Questions Per Attempt *</FormLabel>
                            <FormControl>
                              <Input
                                type="number"
                                min={1}
                                max={questionCount || undefined}
                                {...field}
                                value={field.value ?? ''}
                                onChange={(e) => {
                                  const val = e.target.value ? Number(e.target.value) : null;
                                  field.onChange(val);
                                }}
                              />
                            </FormControl>
                            <FormDescription>
                              Number of questions randomly selected for each attempt
                            </FormDescription>
                            <FormMessage />
                          </FormItem>
                        )}
                      />
                      {questionCount > 0 && (
                        <div className="text-sm">
                          <span className="text-muted-foreground">
                            Total questions in pool: <span className="font-medium text-foreground">{questionCount}</span>
                          </span>
                          {form.watch('quizQuestionCount') != null && (
                            <>
                              {' '}&middot;{' '}
                              {questionCount < (form.watch('quizQuestionCount') ?? 0) * 2 ? (
                                <span className="text-amber-600">
                                  Pool requires at least {(form.watch('quizQuestionCount') ?? 0) * 2} questions (2x quiz size). Add {(form.watch('quizQuestionCount') ?? 0) * 2 - questionCount} more.
                                </span>
                              ) : (
                                <span className="text-green-600">
                                  Pool size is sufficient ({questionCount} &ge; {(form.watch('quizQuestionCount') ?? 0) * 2} required)
                                </span>
                              )}
                            </>
                          )}
                        </div>
                      )}
                    </div>
                  )}
                </div>
              </>
            )}
          </CardContent>
        </Card>

        {/* Auto-Assignment Settings */}
        <Card>
          <CardHeader>
            <CardTitle>Auto-Assignment</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <FormField
              control={form.control}
              name="autoAssignToNewEmployees"
              render={({ field }) => (
                <FormItem className="flex flex-row items-center justify-between rounded-lg border p-4">
                  <div className="space-y-0.5">
                    <FormLabel className="text-base">Auto-Assign to New Employees</FormLabel>
                    <FormDescription>
                      Automatically assign this talk when a new employee is created.
                    </FormDescription>
                  </div>
                  <FormControl>
                    <Switch checked={field.value} onCheckedChange={field.onChange} />
                  </FormControl>
                </FormItem>
              )}
            />

            {watchAutoAssign && (
              <FormField
                control={form.control}
                name="autoAssignDueDays"
                render={({ field }) => (
                  <FormItem className="max-w-xs ml-4">
                    <FormLabel>Due Days</FormLabel>
                    <FormControl>
                      <Input
                        type="number"
                        min={1}
                        max={365}
                        {...field}
                        onChange={(e) => field.onChange(Number(e.target.value))}
                      />
                    </FormControl>
                    <FormDescription>
                      Number of days after employee start date for this talk to be due.
                    </FormDescription>
                    <FormMessage />
                  </FormItem>
                )}
              />
            )}
          </CardContent>
        </Card>

        {/* Animated Slideshow */}
        {isEditing && (talk?.pdfUrl || talk?.pdfFileName) && (
          <Card>
            <CardHeader>
              <CardTitle>Animated Slideshow</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <FormField
                control={form.control}
                name="generateSlidesFromPdf"
                render={({ field }) => (
                  <FormItem className="flex flex-row items-center justify-between rounded-lg border p-4">
                    <div className="space-y-0.5">
                      <FormLabel className="text-base">Generate Slideshow from PDF</FormLabel>
                      <FormDescription>
                        Generate an animated slideshow from the PDF pages for a more engaging experience.
                      </FormDescription>
                    </div>
                    <FormControl>
                      <Switch checked={field.value} onCheckedChange={field.onChange} />
                    </FormControl>
                  </FormItem>
                )}
              />

              {talk?.slidesGenerated && talk.slideCount > 0 && (
                <div className="flex items-center gap-2 text-sm">
                  <svg className="h-4 w-4 text-green-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                    <path strokeLinecap="round" strokeLinejoin="round" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
                  </svg>
                  <span className="text-muted-foreground">
                    {talk.slideCount} slide{talk.slideCount !== 1 ? 's' : ''} generated
                  </span>
                  <Button
                    type="button"
                    variant="ghost"
                    size="sm"
                    onClick={handleRegenerateSlides}
                    disabled={isRegenerating}
                  >
                    {isRegenerating ? (
                      <LoadingSpinner className="mr-1 h-3 w-3" />
                    ) : (
                      <svg className="mr-1 h-3 w-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                        <path strokeLinecap="round" strokeLinejoin="round" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
                      </svg>
                    )}
                    Regenerate
                  </Button>
                </div>
              )}

              {talk?.slidesGenerated && talk.slideCount === 0 && !talk?.slideshowHtml && (
                <div className="flex items-center gap-2 text-sm">
                  <svg className="h-4 w-4 text-yellow-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                    <path strokeLinecap="round" strokeLinejoin="round" d="M12 9v3.75m-9.303 3.376c-.866 1.5.217 3.374 1.948 3.374h14.71c1.73 0 2.813-1.874 1.948-3.374L13.949 3.378c-.866-1.5-3.032-1.5-3.898 0L2.697 16.126zM12 15.75h.007v.008H12v-.008z" />
                  </svg>
                  <span className="text-yellow-700">
                    Slide generation failed - try regenerating
                  </span>
                  <Button
                    type="button"
                    variant="ghost"
                    size="sm"
                    onClick={handleRegenerateSlides}
                    disabled={isRegenerating}
                  >
                    {isRegenerating ? (
                      <LoadingSpinner className="mr-1 h-3 w-3" />
                    ) : (
                      <svg className="mr-1 h-3 w-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                        <path strokeLinecap="round" strokeLinejoin="round" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
                      </svg>
                    )}
                    Regenerate
                  </Button>
                </div>
              )}

              {form.watch('generateSlidesFromPdf') && !talk?.slidesGenerated && (
                <p className="text-sm text-muted-foreground">
                  Slides will be generated when you click &quot;Generate Content&quot;.
                </p>
              )}
            </CardContent>
          </Card>
        )}

        {/* Sections */}
        <SectionEditor form={form} fieldName="sections" />

        {/* Questions (conditional) */}
        {watchRequiresQuiz && (
          <QuestionEditor form={form} fieldName="questions" />
        )}

        {/* Translation Panels - only visible when editing an existing talk */}
        {isEditing && talk && (
          <>
            {/* Subtitle Processing - only show if talk has a video */}
            {talk.videoSource !== 'None' && talk.videoUrl && (
              <SubtitleProcessingPanel
                toolboxTalkId={talk.id}
                currentVideoUrl={talk.videoUrl}
              />
            )}

            {/* Content Translations - show if talk has sections or questions */}
            {(talk.sections.length > 0 || talk.questions.length > 0) && (
              <ContentTranslationPanel
                toolboxTalkId={talk.id}
                existingTranslations={talk.translations}
                onTranslationsGenerated={() => {
                  queryClient.invalidateQueries({ queryKey: [...TOOLBOX_TALKS_KEY, talk.id] });
                }}
              />
            )}
          </>
        )}

        {/* Form actions */}
        <div className="flex justify-end gap-4">
          {onCancel && (
            <Button type="button" variant="outline" onClick={onCancel}>
              Cancel
            </Button>
          )}
          <Button type="submit" disabled={isSubmitting}>
            {isSubmitting ? (
              <>
                <LoadingSpinner className="mr-2 h-4 w-4" />
                {isEditing ? 'Updating...' : 'Creating...'}
              </>
            ) : isEditing ? (
              'Update Toolbox Talk'
            ) : (
              'Create Toolbox Talk'
            )}
          </Button>
        </div>
      </form>
    </Form>
  );
}

function LoadingSpinner({ className }: { className?: string }) {
  return (
    <svg
      className={`animate-spin ${className}`}
      xmlns="http://www.w3.org/2000/svg"
      fill="none"
      viewBox="0 0 24 24"
    >
      <circle
        className="opacity-25"
        cx="12"
        cy="12"
        r="10"
        stroke="currentColor"
        strokeWidth="4"
      />
      <path
        className="opacity-75"
        fill="currentColor"
        d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
      />
    </svg>
  );
}

