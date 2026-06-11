'use client';

/**
 * Save-on-blur deviation (§4.4):
 * PHASE_5_STANDARDS §4.4 recommends 500 ms debounce for text fields.
 * This step uses save-on-blur instead because every field change triggers
 * a MarkStale server call for title/description, and we do not want to
 * fire that on every keystroke. The trade-off: users who tab away quickly
 * may see a brief "Saving…" indicator, but the server state is always
 * consistent with what the user last confirmed.
 */

import { useEffect, useRef, useCallback } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { toast } from 'sonner';
import { Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Form,
  FormField,
  FormItem,
  FormLabel,
  FormControl,
  FormMessage,
} from '@/components/ui/form';
import { LoadingState } from '../components/LoadingState';
import { CoverImageUpload } from '../components/CoverImageUpload';
import { useTalk } from '../hooks/useTalk';
import { useUpdateTalkSettings } from '../hooks/useUpdateTalkSettings';
import { useLookupValues } from '@/hooks/use-lookups';
import {
  settingsSchema,
  REFRESHER_FREQUENCIES,
  type SettingsFormData,
} from '../schemas/settingsSchema';
import type { UpdateTalkSettingsRequest } from '@/lib/api/toolbox-talks/toolbox-talks';

export interface SettingsStepProps {
  talkId: string;
  onContinue: () => void | Promise<void>;
}

function refresherFromTalk(
  requiresRefresher: boolean,
  intervalMonths: number
): SettingsFormData['refresherFrequency'] {
  if (!requiresRefresher) return 'Once';
  if (intervalMonths <= 1) return 'Monthly';
  if (intervalMonths <= 3) return 'Quarterly';
  return 'Annually';
}

export function SettingsStep({ talkId, onContinue }: SettingsStepProps) {
  const { talk, isLoading } = useTalk(talkId);
  const updateMutation = useUpdateTalkSettings(talkId);
  const { data: categories = [] } = useLookupValues('TrainingCategory');

  const initializedRef = useRef(false);

  const form = useForm<SettingsFormData>({
    resolver: zodResolver(settingsSchema),
    mode: 'onBlur',
    defaultValues: {
      title: '',
      description: null,
      category: null,
      refresherFrequency: 'Once',
      isActiveOnPublish: true,
      generateCertificate: false,
      minimumWatchPercent: 90,
      autoAssign: false,
      autoAssignDueDays: 30,
      generateSlideshow: false,
    },
  });

  // Populate from server state once on first load
  useEffect(() => {
    if (!talk || initializedRef.current) return;
    initializedRef.current = true;
    form.reset({
      title: talk.title ?? '',
      description: talk.description ?? null,
      category: talk.category ?? null,
      refresherFrequency: refresherFromTalk(
        talk.requiresRefresher,
        talk.refresherIntervalMonths
      ),
      isActiveOnPublish: talk.isActive,
      generateCertificate: talk.generateCertificate,
      minimumWatchPercent: talk.minimumVideoWatchPercent,
      autoAssign: talk.autoAssignToNewEmployees,
      autoAssignDueDays: talk.autoAssignDueDays,
      generateSlideshow: talk.generateSlidesFromPdf,
    });
  }, [talk, form]);

  const saveField = useCallback(
    async (values: SettingsFormData) => {
      const valid = await form.trigger();
      if (!valid) return;
      const payload: UpdateTalkSettingsRequest = {
        title: values.title,
        description: values.description ?? null,
        category: values.category ?? null,
        refresherFrequency: values.refresherFrequency,
        isActive: values.isActiveOnPublish,
        generateCertificate: values.generateCertificate,
        minimumVideoWatchPercent: values.minimumWatchPercent,
        autoAssignToNewEmployees: values.autoAssign,
        autoAssignDueDays: values.autoAssignDueDays,
        generateSlidesFromPdf: values.generateSlideshow,
      };
      try {
        await updateMutation.mutateAsync(payload);
      } catch {
        toast.error('Failed to save settings. Please try again.');
      }
    },
    [form, updateMutation]
  );

  const handleContinue = useCallback(async () => {
    const values = form.getValues();
    const valid = await form.trigger();
    if (!valid) {
      toast.error('Please fix the errors before continuing.');
      return;
    }
    const payload: UpdateTalkSettingsRequest = {
      title: values.title,
      description: values.description ?? null,
      category: values.category ?? null,
      refresherFrequency: values.refresherFrequency,
      isActive: values.isActiveOnPublish,
      generateCertificate: values.generateCertificate,
      minimumVideoWatchPercent: values.minimumWatchPercent,
      autoAssignToNewEmployees: values.autoAssign,
      autoAssignDueDays: values.autoAssignDueDays,
      generateSlidesFromPdf: values.generateSlideshow,
    };
    try {
      await updateMutation.mutateAsync(payload);
      await onContinue();
    } catch {
      toast.error('Failed to save settings. Please try again.');
    }
  }, [form, updateMutation, onContinue]);

  if (isLoading) return <LoadingState label="Loading settings…" />;

  const isSaving = updateMutation.isPending;

  return (
    <Form {...form}>
      <form
        onSubmit={(e) => { e.preventDefault(); handleContinue(); }}
        noValidate
        className="space-y-8"
        aria-label="Learning settings"
      >
        {/* ── Core fields ─────────────────────────────────── */}
        <section aria-labelledby="core-fields-heading">
          <h2 id="core-fields-heading" className="text-base font-semibold mb-4">
            Details
          </h2>
          <div className="space-y-4">
            {/* Title */}
            <FormField
              control={form.control}
              name="title"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Title <span aria-hidden="true">*</span></FormLabel>
                  <FormControl>
                    <Input
                      {...field}
                      placeholder="e.g. Manual Handling Safety"
                      onBlur={async () => {
                        field.onBlur();
                        await saveField(form.getValues());
                      }}
                    />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            {/* Description */}
            <FormField
              control={form.control}
              name="description"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Description</FormLabel>
                  <FormControl>
                    <Textarea
                      {...field}
                      value={field.value ?? ''}
                      placeholder="Optional short description shown to employees"
                      rows={3}
                      onBlur={async () => {
                        field.onBlur();
                        await saveField(form.getValues());
                      }}
                    />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            {/* Category */}
            <FormField
              control={form.control}
              name="category"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Category</FormLabel>
                  <Select
                    value={field.value ?? '__none__'}
                    onValueChange={async (val) => {
                      const category = val === '__none__' ? null : val;
                      field.onChange(category);
                      await saveField({ ...form.getValues(), category });
                    }}
                  >
                    <FormControl>
                      <SelectTrigger>
                        <SelectValue placeholder="Select a category…" />
                      </SelectTrigger>
                    </FormControl>
                    <SelectContent>
                      <SelectItem value="__none__">None</SelectItem>
                      {categories.map((c) => (
                        <SelectItem key={c.id} value={c.name}>
                          {c.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  <FormMessage />
                </FormItem>
              )}
            />
          </div>
        </section>

        {/* ── Cover image ──────────────────────────────────── */}
        <section aria-labelledby="cover-image-heading">
          <h2 id="cover-image-heading" className="text-base font-semibold mb-1">
            Cover Image
          </h2>
          <p className="text-sm text-muted-foreground mb-4">
            Displayed on the employee training card. Optional.
          </p>
          <CoverImageUpload
            talkId={talkId}
            currentUrl={talk?.coverImageUrl ?? null}
          />
        </section>

        {/* ── Behaviour ────────────────────────────────────── */}
        <section aria-labelledby="behaviour-heading">
          <h2 id="behaviour-heading" className="text-base font-semibold mb-4">
            Behaviour
          </h2>
          <div className="space-y-6">
            {/* Refresher frequency */}
            <FormField
              control={form.control}
              name="refresherFrequency"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Refresher frequency</FormLabel>
                  <Select
                    value={field.value}
                    onValueChange={async (val) => {
                      field.onChange(val);
                      await saveField({
                        ...form.getValues(),
                        refresherFrequency: val as SettingsFormData['refresherFrequency'],
                      });
                    }}
                  >
                    <FormControl>
                      <SelectTrigger>
                        <SelectValue />
                      </SelectTrigger>
                    </FormControl>
                    <SelectContent>
                      {REFRESHER_FREQUENCIES.map((f) => (
                        <SelectItem key={f} value={f}>
                          {f}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  <FormMessage />
                </FormItem>
              )}
            />

            {/* Minimum watch % */}
            <FormField
              control={form.control}
              name="minimumWatchPercent"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Minimum video watch percentage</FormLabel>
                  <FormControl>
                    <Input
                      type="number"
                      min={50}
                      max={100}
                      {...field}
                      value={field.value}
                      onChange={(e) => field.onChange(Number(e.target.value))}
                      onBlur={async () => {
                        field.onBlur();
                        await saveField(form.getValues());
                      }}
                      className="w-24"
                    />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            {/* Generate certificate */}
            <FormField
              control={form.control}
              name="generateCertificate"
              render={({ field }) => (
                <FormItem className="flex items-center justify-between rounded-lg border p-4">
                  <div>
                    <FormLabel className="text-sm font-medium">Generate certificate on completion</FormLabel>
                    <p className="text-xs text-muted-foreground mt-0.5">
                      A PDF certificate is emailed to the employee when they complete this learning.
                    </p>
                  </div>
                  <FormControl>
                    <Switch
                      checked={field.value}
                      onCheckedChange={async (checked) => {
                        field.onChange(checked);
                        await saveField({ ...form.getValues(), generateCertificate: checked });
                      }}
                      aria-label="Generate certificate on completion"
                    />
                  </FormControl>
                </FormItem>
              )}
            />

            {/* Active on publish */}
            <FormField
              control={form.control}
              name="isActiveOnPublish"
              render={({ field }) => (
                <FormItem className="flex items-center justify-between rounded-lg border p-4">
                  <div>
                    <FormLabel className="text-sm font-medium">Active on publish</FormLabel>
                    <p className="text-xs text-muted-foreground mt-0.5">
                      When disabled, the learning is published but not schedulable.
                    </p>
                  </div>
                  <FormControl>
                    <Switch
                      checked={field.value}
                      onCheckedChange={async (checked) => {
                        field.onChange(checked);
                        await saveField({ ...form.getValues(), isActiveOnPublish: checked });
                      }}
                      aria-label="Active on publish"
                    />
                  </FormControl>
                </FormItem>
              )}
            />
          </div>
        </section>

        {/* ── Auto-assign ──────────────────────────────────── */}
        <section aria-labelledby="auto-assign-heading">
          <h2 id="auto-assign-heading" className="text-base font-semibold mb-4">
            Auto-assign
          </h2>
          <div className="space-y-4">
            <FormField
              control={form.control}
              name="autoAssign"
              render={({ field }) => (
                <FormItem className="flex items-center justify-between rounded-lg border p-4">
                  <div>
                    <FormLabel className="text-sm font-medium">Auto-assign to new employees</FormLabel>
                    <p className="text-xs text-muted-foreground mt-0.5">
                      New employees are automatically assigned this learning when they are created.
                    </p>
                  </div>
                  <FormControl>
                    <Switch
                      checked={field.value}
                      onCheckedChange={async (checked) => {
                        field.onChange(checked);
                        await saveField({ ...form.getValues(), autoAssign: checked });
                      }}
                      aria-label="Auto-assign to new employees"
                    />
                  </FormControl>
                </FormItem>
              )}
            />

            {form.watch('autoAssign') && (
              <FormField
                control={form.control}
                name="autoAssignDueDays"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Due within (days)</FormLabel>
                    <FormControl>
                      <Input
                        type="number"
                        min={1}
                        max={90}
                        {...field}
                        value={field.value}
                        onChange={(e) => field.onChange(Number(e.target.value))}
                        onBlur={async () => {
                          field.onBlur();
                          await saveField(form.getValues());
                        }}
                        className="w-24"
                      />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
            )}
          </div>
        </section>

        {/* ── Slideshow ────────────────────────────────────── */}
        {talk?.pdfUrl && (
          <section aria-labelledby="slideshow-heading">
            <h2 id="slideshow-heading" className="text-base font-semibold mb-4">
              Slideshow
            </h2>
            <FormField
              control={form.control}
              name="generateSlideshow"
              render={({ field }) => (
                <FormItem className="flex items-center justify-between rounded-lg border p-4">
                  <div>
                    <FormLabel className="text-sm font-medium">Generate slideshow from PDF</FormLabel>
                    <p className="text-xs text-muted-foreground mt-0.5">
                      Each PDF page is converted to a slide shown alongside the learning content.
                    </p>
                  </div>
                  <FormControl>
                    <Switch
                      checked={field.value}
                      onCheckedChange={async (checked) => {
                        field.onChange(checked);
                        await saveField({ ...form.getValues(), generateSlideshow: checked });
                      }}
                      aria-label="Generate slideshow from PDF"
                    />
                  </FormControl>
                </FormItem>
              )}
            />
          </section>
        )}

        {/* ── Continue ─────────────────────────────────────── */}
        <div className="flex justify-end border-t pt-4">
          {isSaving && (
            <span className="flex items-center gap-1.5 text-sm text-muted-foreground mr-4">
              <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
              Saving…
            </span>
          )}
          <Button type="submit" disabled={isSaving}>
            Continue
          </Button>
        </div>
      </form>
    </Form>
  );
}
