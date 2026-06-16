'use client';

import { useEffect } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod/v4';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
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
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import {
  useToolboxTalkSettings,
  useUpdateToolboxTalkSettings,
} from '@/lib/api/toolbox-talks/use-toolbox-talks';

const REFRESHER_FREQUENCIES = ['Once', 'Monthly', 'Quarterly', 'Annually'] as const;

const wizardDefaultsSchema = z.object({
  defaultMinimumVideoWatchPercent: z.number().int().min(50).max(100),
  defaultAutoAssignDueDays: z.number().int().min(1).max(90),
  defaultGenerateCertificate: z.boolean(),
  defaultRefresherFrequency: z.enum(['Once', 'Monthly', 'Quarterly', 'Annually']),
  defaultIsActive: z.boolean(),
});

type WizardDefaultsFormData = z.infer<typeof wizardDefaultsSchema>;

export function WizardDefaultsSection() {
  const { data: settings, isLoading } = useToolboxTalkSettings();
  const updateMutation = useUpdateToolboxTalkSettings();

  const form = useForm<WizardDefaultsFormData>({
    resolver: zodResolver(wizardDefaultsSchema),
    defaultValues: {
      defaultMinimumVideoWatchPercent: 90,
      defaultAutoAssignDueDays: 14,
      defaultGenerateCertificate: true,
      defaultRefresherFrequency: 'Once',
      defaultIsActive: false,
    },
  });

  useEffect(() => {
    if (!settings) return;
    form.reset({
      defaultMinimumVideoWatchPercent: settings.defaultMinimumVideoWatchPercent,
      defaultAutoAssignDueDays: settings.defaultAutoAssignDueDays,
      defaultGenerateCertificate: settings.defaultGenerateCertificate,
      defaultRefresherFrequency: (settings.defaultRefresherFrequency ?? 'Once') as WizardDefaultsFormData['defaultRefresherFrequency'],
      defaultIsActive: settings.defaultIsActive,
    });
  }, [settings, form]);

  const onSubmit = async (values: WizardDefaultsFormData) => {
    try {
      await updateMutation.mutateAsync({
        defaultMinimumVideoWatchPercent: values.defaultMinimumVideoWatchPercent,
        defaultAutoAssignDueDays: values.defaultAutoAssignDueDays,
        defaultGenerateCertificate: values.defaultGenerateCertificate,
        defaultRefresherFrequency: values.defaultRefresherFrequency,
        defaultIsActive: values.defaultIsActive,
      });
      toast.success('Default settings saved');
      form.reset(values);
    } catch {
      toast.error('Failed to save default settings');
    }
  };

  if (isLoading) {
    return (
      <>
        <Card>
          <CardHeader>
            <Skeleton className="h-5 w-40" />
            <Skeleton className="h-4 w-64 mt-1" />
          </CardHeader>
          <CardContent>
            <Skeleton className="h-10 w-full" />
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <Skeleton className="h-5 w-40" />
          </CardHeader>
          <CardContent className="space-y-4">
            <Skeleton className="h-10 w-full" />
            <Skeleton className="h-16 w-full" />
            <Skeleton className="h-16 w-full" />
            <Skeleton className="h-10 w-full" />
          </CardContent>
        </Card>
      </>
    );
  }

  const isDirty = form.formState.isDirty;
  const isSubmitting = updateMutation.isPending;

  return (
    <Form {...form}>
      <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
        {/* Video & completion */}
        <Card>
          <CardHeader>
            <CardTitle>Video &amp; completion</CardTitle>
            <CardDescription>
              Default values applied when a new learning is created via the wizard.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <FormField
              control={form.control}
              name="defaultMinimumVideoWatchPercent"
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
                      className="w-24"
                    />
                  </FormControl>
                  <p className="text-xs text-muted-foreground">
                    Percentage of the video an employee must watch before the learning can be completed (50–100).
                  </p>
                  <FormMessage />
                </FormItem>
              )}
            />
          </CardContent>
        </Card>

        {/* Talk defaults */}
        <Card>
          <CardHeader>
            <CardTitle>Talk defaults</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <FormField
              control={form.control}
              name="defaultGenerateCertificate"
              render={({ field }) => (
                <FormItem className="flex items-center justify-between rounded-lg border p-4">
                  <div>
                    <FormLabel className="text-sm font-medium">Generate certificate on completion</FormLabel>
                    <p className="text-xs text-muted-foreground mt-0.5">
                      New learnings will generate a PDF certificate for employees on completion by default.
                    </p>
                  </div>
                  <FormControl>
                    <Switch
                      checked={field.value}
                      onCheckedChange={field.onChange}
                      aria-label="Generate certificate on completion"
                    />
                  </FormControl>
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="defaultIsActive"
              render={({ field }) => (
                <FormItem className="flex items-center justify-between rounded-lg border p-4">
                  <div>
                    <FormLabel className="text-sm font-medium">Active on create</FormLabel>
                    <p className="text-xs text-muted-foreground mt-0.5">
                      New learnings start with the Active toggle on. Admins can still change this per learning in wizard Step 4.
                    </p>
                  </div>
                  <FormControl>
                    <Switch
                      checked={field.value}
                      onCheckedChange={field.onChange}
                      aria-label="Active on create"
                    />
                  </FormControl>
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="defaultRefresherFrequency"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Default refresher frequency</FormLabel>
                  <Select value={field.value} onValueChange={field.onChange}>
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
                  <p className="text-xs text-muted-foreground">
                    How often employees must re-complete this learning after their first completion.
                  </p>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="defaultAutoAssignDueDays"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Auto-assign due within (days)</FormLabel>
                  <FormControl>
                    <Input
                      type="number"
                      min={1}
                      max={90}
                      {...field}
                      value={field.value}
                      onChange={(e) => field.onChange(Number(e.target.value))}
                      className="w-24"
                    />
                  </FormControl>
                  <p className="text-xs text-muted-foreground">
                    When a learning is set to auto-assign to new employees, this is the default number of days after hire they have to complete it (1–90).
                  </p>
                  <FormMessage />
                </FormItem>
              )}
            />
          </CardContent>
        </Card>

        {isDirty && (
          <div className="flex justify-end">
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? 'Saving…' : 'Save defaults'}
            </Button>
          </div>
        )}
      </form>
    </Form>
  );
}
