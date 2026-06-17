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
  useUpdateToolboxTalkNotificationSettings,
} from '@/lib/api/toolbox-talks/use-toolbox-talks';

const notificationsSchema = z.object({
  notifyOnTranslationComplete: z.boolean(),
  notifyOnValidationComplete: z.boolean(),
  notifyOnFailure: z.boolean(),
  notifyOnExternalReviewResponse: z.boolean(),
});

type NotificationsFormData = z.infer<typeof notificationsSchema>;

const TOGGLES: {
  name: keyof NotificationsFormData;
  label: string;
  description: string;
}[] = [
  {
    name: 'notifyOnTranslationComplete',
    label: 'Content translation complete',
    description:
      'Notify admins when AI content translation finishes (success or partial failure).',
  },
  {
    name: 'notifyOnValidationComplete',
    label: 'Translation validation complete',
    description:
      'Notify admins when a back-translation validation run finishes with a Pass, Review, or Fail outcome.',
  },
  {
    name: 'notifyOnFailure',
    label: 'Pipeline failures',
    description:
      'Notify admins when a translation or validation job crashes with an unhandled error.',
  },
  {
    name: 'notifyOnExternalReviewResponse',
    label: 'External review response',
    description:
      'Notify admins when an external reviewer submits their accept / reject decision.',
  },
];

export function NotificationsSettingsSection() {
  const { data: settings, isLoading } = useToolboxTalkSettings();
  const updateMutation = useUpdateToolboxTalkNotificationSettings();

  const form = useForm<NotificationsFormData>({
    resolver: zodResolver(notificationsSchema),
    defaultValues: {
      notifyOnTranslationComplete: true,
      notifyOnValidationComplete: true,
      notifyOnFailure: true,
      notifyOnExternalReviewResponse: true,
    },
  });

  useEffect(() => {
    if (!settings) return;
    form.reset({
      notifyOnTranslationComplete: settings.notifyOnTranslationComplete,
      notifyOnValidationComplete: settings.notifyOnValidationComplete,
      notifyOnFailure: settings.notifyOnFailure,
      notifyOnExternalReviewResponse: settings.notifyOnExternalReviewResponse,
    });
  }, [settings, form]);

  const onSubmit = async (values: NotificationsFormData) => {
    try {
      await updateMutation.mutateAsync(values);
      toast.success('Notification settings saved');
    } catch {
      toast.error('Failed to save notification settings');
    }
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle>Pipeline Notifications</CardTitle>
        <CardDescription>
          Choose which translation pipeline events trigger email notifications to all tenant admins.
          All notifications are on by default.
        </CardDescription>
      </CardHeader>
      <CardContent>
        {isLoading ? (
          <div className="space-y-4">
            {[1, 2, 3, 4].map((i) => (
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
                {updateMutation.isPending ? 'Saving…' : 'Save notification settings'}
              </Button>
            </form>
          </Form>
        )}
      </CardContent>
    </Card>
  );
}
