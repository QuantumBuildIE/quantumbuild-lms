'use client';

import { useState, useCallback } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { PencilIcon, XIcon, SaveIcon, SlidersHorizontalIcon } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Switch } from '@/components/ui/switch';
import {
  Form,
  FormField,
  FormItem,
  FormLabel,
  FormControl,
  FormMessage,
} from '@/components/ui/form';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { useUpdateToolboxTalk } from '@/lib/api/toolbox-talks';
import { usePermission } from '@/lib/auth/use-auth';
import type { ToolboxTalk } from '@/types/toolbox-talks';
import { toast } from 'sonner';

// ============================================
// Schema
// ============================================

const settingsEditSchema = z.object({
  requiresQuiz: z.boolean(),
  passingScore: z.number().int().min(0).max(100),
  shuffleQuestions: z.boolean(),
  shuffleOptions: z.boolean(),
  useQuestionPool: z.boolean(),
  allowRetry: z.boolean(),
  requiresRefresher: z.boolean(),
  refresherIntervalMonths: z.number().int().min(1).max(60),
  generateCertificate: z.boolean(),
  autoAssignDueDays: z.number().int().min(1).max(365),
});

type SettingsEditFormData = z.infer<typeof settingsEditSchema>;

// ============================================
// Helpers
// ============================================

function talkToFormData(talk: ToolboxTalk): SettingsEditFormData {
  return {
    requiresQuiz: talk.requiresQuiz,
    passingScore: talk.passingScore ?? 80,
    shuffleQuestions: talk.shuffleQuestions,
    shuffleOptions: talk.shuffleOptions,
    useQuestionPool: talk.useQuestionPool,
    allowRetry: talk.allowRetry,
    requiresRefresher: talk.requiresRefresher,
    refresherIntervalMonths: talk.refresherIntervalMonths,
    generateCertificate: talk.generateCertificate,
    autoAssignDueDays: talk.autoAssignDueDays,
  };
}

// ============================================
// Sub-components
// ============================================

interface ToggleRowProps {
  id: string;
  label: string;
  description: string;
  checked: boolean;
  onCheckedChange: (v: boolean) => void;
  disabled?: boolean;
}

function ToggleRow({ id, label, description, checked, onCheckedChange, disabled }: ToggleRowProps) {
  return (
    <div className="flex items-start justify-between gap-4 rounded-lg border p-3">
      <div className="space-y-0.5">
        <label htmlFor={id} className="text-sm font-medium cursor-pointer">
          {label}
        </label>
        <p className="text-xs text-muted-foreground">{description}</p>
      </div>
      <Switch
        id={id}
        checked={checked}
        onCheckedChange={onCheckedChange}
        disabled={disabled}
        className="shrink-0 mt-0.5"
      />
    </div>
  );
}

interface ViewRowProps {
  label: string;
  value: string;
}

function ViewRow({ label, value }: ViewRowProps) {
  return (
    <div>
      <p className="text-xs text-muted-foreground">{label}</p>
      <p className="text-sm font-medium mt-0.5">{value}</p>
    </div>
  );
}

// ============================================
// Props
// ============================================

interface SettingsEditPanelProps {
  talk: ToolboxTalk;
  onRefetch: () => void;
}

// ============================================
// Component
// ============================================

export function SettingsEditPanel({ talk, onRefetch }: SettingsEditPanelProps) {
  const canManage = usePermission('Learnings.Manage');
  const [isEditMode, setIsEditMode] = useState(false);
  const [confirmDiscardOpen, setConfirmDiscardOpen] = useState(false);
  const updateMutation = useUpdateToolboxTalk();

  const form = useForm<SettingsEditFormData>({
    resolver: zodResolver(settingsEditSchema),
    defaultValues: talkToFormData(talk),
  });

  const openEditMode = useCallback(() => {
    onRefetch();
    form.reset(talkToFormData(talk));
    setIsEditMode(true);
  }, [talk, onRefetch, form]);

  const onSubmit = useCallback(
    async (values: SettingsEditFormData) => {
      try {
        await updateMutation.mutateAsync({
          id: talk.id,
          data: {
            id: talk.id,
            code: talk.code,
            title: talk.title,
            description: talk.description ?? undefined,
            category: talk.category ?? undefined,
            frequency: talk.frequency,
            videoUrl: talk.videoUrl ?? undefined,
            videoSource: talk.videoSource,
            attachmentUrl: talk.attachmentUrl ?? undefined,
            minimumVideoWatchPercent: talk.minimumVideoWatchPercent,
            isActive: talk.isActive,
            quizQuestionCount: talk.quizQuestionCount ?? undefined,
            sourceLanguageCode: talk.sourceLanguageCode,
            autoAssignToNewEmployees: talk.autoAssignToNewEmployees,
            generateSlidesFromPdf: talk.generateSlidesFromPdf,
            // Preserve existing sections and questions unchanged
            sections: talk.sections.map((s, i) => ({
              id: s.id,
              sectionNumber: i + 1,
              title: s.title,
              content: s.content,
              requiresAcknowledgment: s.requiresAcknowledgment,
              source: s.source,
            })),
            questions: talk.questions.map((q, i) => ({
              id: q.id,
              questionNumber: i + 1,
              questionText: q.questionText,
              questionType: q.questionType,
              options: q.options ?? undefined,
              correctAnswer: q.correctAnswer ?? '',
              points: q.points,
              source: q.source,
            })),
            // Edited settings fields
            requiresQuiz: values.requiresQuiz,
            passingScore: values.passingScore,
            shuffleQuestions: values.shuffleQuestions,
            shuffleOptions: values.shuffleOptions,
            useQuestionPool: values.useQuestionPool,
            allowRetry: values.allowRetry,
            requiresRefresher: values.requiresRefresher,
            refresherIntervalMonths: values.refresherIntervalMonths,
            generateCertificate: values.generateCertificate,
            autoAssignDueDays: values.autoAssignDueDays,
          },
        });
        toast.success('Settings saved');
        setIsEditMode(false);
      } catch (error) {
        const msg = error instanceof Error ? error.message : 'Failed to save settings';
        toast.error('Save failed', { description: msg });
      }
    },
    [talk, updateMutation]
  );

  const handleCancelClick = useCallback(() => {
    if (form.formState.isDirty) {
      setConfirmDiscardOpen(true);
    } else {
      setIsEditMode(false);
    }
  }, [form.formState.isDirty]);

  const handleConfirmDiscard = useCallback(() => {
    setConfirmDiscardOpen(false);
    form.reset(talkToFormData(talk));
    setIsEditMode(false);
  }, [form, talk]);

  const requiresQuiz = form.watch('requiresQuiz');
  const requiresRefresher = form.watch('requiresRefresher');

  return (
    <>
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle className="flex items-center gap-2">
              <SlidersHorizontalIcon className="h-5 w-5" />
              Settings
            </CardTitle>
            {canManage && !isEditMode && (
              <Button variant="outline" size="sm" onClick={openEditMode}>
                <PencilIcon className="mr-2 h-4 w-4" />
                Edit Settings
              </Button>
            )}
            {isEditMode && (
              <div className="flex gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  onClick={handleCancelClick}
                  disabled={updateMutation.isPending}
                >
                  <XIcon className="mr-2 h-4 w-4" />
                  Cancel
                </Button>
                <Button
                  size="sm"
                  onClick={form.handleSubmit(onSubmit)}
                  disabled={updateMutation.isPending || !form.formState.isDirty}
                >
                  <SaveIcon className="mr-2 h-4 w-4" />
                  {updateMutation.isPending ? 'Saving...' : 'Save Settings'}
                </Button>
              </div>
            )}
          </div>
        </CardHeader>

        <CardContent>
          {isEditMode ? (
            <Form {...form}>
              <form
                onSubmit={form.handleSubmit(onSubmit)}
                noValidate
                className="space-y-6"
              >
                {/* ── Quiz ─────────────────────────────────────── */}
                <section>
                  <h3 className="text-sm font-semibold mb-3">Quiz</h3>
                  <div className="space-y-3">
                    <FormField
                      control={form.control}
                      name="requiresQuiz"
                      render={({ field }) => (
                        <FormItem>
                          <ToggleRow
                            id="requiresQuiz"
                            label="Quiz required"
                            description="Employees must pass a quiz to complete this learning"
                            checked={field.value}
                            onCheckedChange={field.onChange}
                            disabled={updateMutation.isPending}
                          />
                          <FormMessage />
                        </FormItem>
                      )}
                    />

                    {requiresQuiz && (
                      <>
                        <FormField
                          control={form.control}
                          name="passingScore"
                          render={({ field }) => (
                            <FormItem className="pl-3">
                              <FormLabel>Passing score (%)</FormLabel>
                              <FormControl>
                                <Input
                                  type="number"
                                  min={0}
                                  max={100}
                                  className="w-24"
                                  {...field}
                                  value={field.value}
                                  onChange={(e) => field.onChange(Number(e.target.value))}
                                  disabled={updateMutation.isPending}
                                />
                              </FormControl>
                              <FormMessage />
                            </FormItem>
                          )}
                        />

                        <FormField
                          control={form.control}
                          name="shuffleQuestions"
                          render={({ field }) => (
                            <FormItem className="pl-3">
                              <ToggleRow
                                id="shuffleQuestions"
                                label="Shuffle questions"
                                description="Randomise the order of questions each time"
                                checked={field.value}
                                onCheckedChange={field.onChange}
                                disabled={updateMutation.isPending}
                              />
                              <FormMessage />
                            </FormItem>
                          )}
                        />

                        <FormField
                          control={form.control}
                          name="shuffleOptions"
                          render={({ field }) => (
                            <FormItem className="pl-3">
                              <ToggleRow
                                id="shuffleOptions"
                                label="Shuffle answer options"
                                description="Randomise the order of multiple-choice options"
                                checked={field.value}
                                onCheckedChange={field.onChange}
                                disabled={updateMutation.isPending}
                              />
                              <FormMessage />
                            </FormItem>
                          )}
                        />

                        <FormField
                          control={form.control}
                          name="useQuestionPool"
                          render={({ field }) => (
                            <FormItem className="pl-3">
                              <ToggleRow
                                id="useQuestionPool"
                                label="Use question pool"
                                description="Draw a random subset from all available questions"
                                checked={field.value}
                                onCheckedChange={field.onChange}
                                disabled={updateMutation.isPending}
                              />
                              <FormMessage />
                            </FormItem>
                          )}
                        />

                        <FormField
                          control={form.control}
                          name="allowRetry"
                          render={({ field }) => (
                            <FormItem className="pl-3">
                              <ToggleRow
                                id="allowRetry"
                                label="Allow retry"
                                description="Employees may retake a failed quiz without rewatching the video"
                                checked={field.value}
                                onCheckedChange={field.onChange}
                                disabled={updateMutation.isPending}
                              />
                              <FormMessage />
                            </FormItem>
                          )}
                        />
                      </>
                    )}
                  </div>
                </section>

                {/* ── Refresher ─────────────────────────────────── */}
                <section>
                  <h3 className="text-sm font-semibold mb-3">Refresher</h3>
                  <div className="space-y-3">
                    <FormField
                      control={form.control}
                      name="requiresRefresher"
                      render={({ field }) => (
                        <FormItem>
                          <ToggleRow
                            id="requiresRefresher"
                            label="Requires refresher"
                            description="Employees must repeat this learning on a recurring schedule"
                            checked={field.value}
                            onCheckedChange={field.onChange}
                            disabled={updateMutation.isPending}
                          />
                          <FormMessage />
                        </FormItem>
                      )}
                    />

                    {requiresRefresher && (
                      <FormField
                        control={form.control}
                        name="refresherIntervalMonths"
                        render={({ field }) => (
                          <FormItem className="pl-3">
                            <FormLabel>Refresher interval (months)</FormLabel>
                            <FormControl>
                              <Input
                                type="number"
                                min={1}
                                max={60}
                                className="w-24"
                                {...field}
                                value={field.value}
                                onChange={(e) => field.onChange(Number(e.target.value))}
                                disabled={updateMutation.isPending}
                              />
                            </FormControl>
                            <FormMessage />
                          </FormItem>
                        )}
                      />
                    )}
                  </div>
                </section>

                {/* ── Certificate ───────────────────────────────── */}
                <section>
                  <h3 className="text-sm font-semibold mb-3">Certificate</h3>
                  <FormField
                    control={form.control}
                    name="generateCertificate"
                    render={({ field }) => (
                      <FormItem>
                        <ToggleRow
                          id="generateCertificate"
                          label="Generate certificate on completion"
                          description="A PDF certificate is emailed to the employee when they complete this learning"
                          checked={field.value}
                          onCheckedChange={field.onChange}
                          disabled={updateMutation.isPending}
                        />
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                </section>

                {/* ── Schedule ──────────────────────────────────── */}
                <section>
                  <h3 className="text-sm font-semibold mb-3">Schedule</h3>
                  <FormField
                    control={form.control}
                    name="autoAssignDueDays"
                    render={({ field }) => (
                      <FormItem>
                        <FormLabel>Auto-assign due within (days)</FormLabel>
                        <FormControl>
                          <Input
                            type="number"
                            min={1}
                            max={365}
                            className="w-24"
                            {...field}
                            value={field.value}
                            onChange={(e) => field.onChange(Number(e.target.value))}
                            disabled={updateMutation.isPending}
                          />
                        </FormControl>
                        <FormMessage />
                      </FormItem>
                    )}
                  />
                </section>
              </form>
            </Form>
          ) : (
            <div className="space-y-6">
              {/* Quiz */}
              <section>
                <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground mb-3">
                  Quiz
                </h3>
                <div className="grid gap-3 sm:grid-cols-2">
                  <ViewRow
                    label="Quiz required"
                    value={talk.requiresQuiz ? 'Yes' : 'No'}
                  />
                  {talk.requiresQuiz && (
                    <>
                      <ViewRow
                        label="Passing score"
                        value={`${talk.passingScore ?? 80}%`}
                      />
                      <ViewRow
                        label="Shuffle questions"
                        value={talk.shuffleQuestions ? 'Yes' : 'No'}
                      />
                      <ViewRow
                        label="Shuffle answer options"
                        value={talk.shuffleOptions ? 'Yes' : 'No'}
                      />
                      <ViewRow
                        label="Use question pool"
                        value={talk.useQuestionPool ? 'Yes' : 'No'}
                      />
                      <ViewRow
                        label="Allow retry"
                        value={talk.allowRetry ? 'Yes' : 'No'}
                      />
                    </>
                  )}
                </div>
              </section>

              {/* Refresher */}
              <section>
                <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground mb-3">
                  Refresher
                </h3>
                <div className="grid gap-3 sm:grid-cols-2">
                  <ViewRow
                    label="Requires refresher"
                    value={talk.requiresRefresher ? 'Yes' : 'No'}
                  />
                  {talk.requiresRefresher && (
                    <ViewRow
                      label="Refresher interval"
                      value={`${talk.refresherIntervalMonths} month${talk.refresherIntervalMonths !== 1 ? 's' : ''}`}
                    />
                  )}
                </div>
              </section>

              {/* Certificate */}
              <section>
                <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground mb-3">
                  Certificate
                </h3>
                <div className="grid gap-3 sm:grid-cols-2">
                  <ViewRow
                    label="Generate certificate on completion"
                    value={talk.generateCertificate ? 'Yes' : 'No'}
                  />
                </div>
              </section>

              {/* Schedule */}
              <section>
                <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground mb-3">
                  Schedule
                </h3>
                <div className="grid gap-3 sm:grid-cols-2">
                  <ViewRow
                    label="Auto-assign due within"
                    value={`${talk.autoAssignDueDays} day${talk.autoAssignDueDays !== 1 ? 's' : ''}`}
                  />
                </div>
              </section>
            </div>
          )}
        </CardContent>
      </Card>

      <AlertDialog open={confirmDiscardOpen} onOpenChange={setConfirmDiscardOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Discard unsaved changes?</AlertDialogTitle>
            <AlertDialogDescription>
              Your settings edits have not been saved. This will discard all changes.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Keep editing</AlertDialogCancel>
            <AlertDialogAction onClick={handleConfirmDiscard}>Discard changes</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}
