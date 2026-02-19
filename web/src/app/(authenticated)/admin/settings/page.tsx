'use client';

import { useEffect } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { toast } from 'sonner';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Form, FormControl, FormDescription, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form';
import { Skeleton } from '@/components/ui/skeleton';
import { useTenantSettings, useUpdateTenantSettings } from '@/lib/api/admin/use-tenant-settings';

const tenantSettingsSchema = z.object({
  EmailTeamName: z.string().min(1, 'Email team name is required').max(200),
  TalkCertificatePrefix: z.string().min(1, 'Talk certificate prefix is required').max(20),
  CourseCertificatePrefix: z.string().min(1, 'Course certificate prefix is required').max(20),
});

type TenantSettingsFormData = z.infer<typeof tenantSettingsSchema>;

export default function AdminSettingsGeneralPage() {
  const { data: settings, isLoading } = useTenantSettings();
  const updateMutation = useUpdateTenantSettings();

  const form = useForm<TenantSettingsFormData>({
    resolver: zodResolver(tenantSettingsSchema),
    defaultValues: {
      EmailTeamName: '',
      TalkCertificatePrefix: '',
      CourseCertificatePrefix: '',
    },
  });

  useEffect(() => {
    if (settings) {
      form.reset({
        EmailTeamName: settings.EmailTeamName ?? '',
        TalkCertificatePrefix: settings.TalkCertificatePrefix ?? '',
        CourseCertificatePrefix: settings.CourseCertificatePrefix ?? '',
      });
    }
  }, [settings, form]);

  async function onSubmit(data: TenantSettingsFormData) {
    try {
      await updateMutation.mutateAsync({
        settings: [
          { key: 'EmailTeamName', value: data.EmailTeamName },
          { key: 'TalkCertificatePrefix', value: data.TalkCertificatePrefix },
          { key: 'CourseCertificatePrefix', value: data.CourseCertificatePrefix },
        ],
      });
      toast.success('Settings saved successfully');
    } catch {
      toast.error('Failed to save settings');
    }
  }

  if (isLoading) {
    return (
      <div className="space-y-6">
        <div>
          <Skeleton className="h-8 w-48" />
          <Skeleton className="mt-2 h-4 w-80" />
        </div>
        <Card>
          <CardHeader>
            <Skeleton className="h-5 w-40" />
            <Skeleton className="h-4 w-64" />
          </CardHeader>
          <CardContent className="space-y-4">
            <Skeleton className="h-10 w-full" />
            <Skeleton className="h-10 w-full" />
            <Skeleton className="h-10 w-full" />
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">General Settings</h1>
        <p className="text-muted-foreground">
          Manage email preferences, certificate prefixes, and general configuration
        </p>
      </div>

      <Form {...form}>
        <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
          <Card>
            <CardHeader>
              <CardTitle>Company Configuration</CardTitle>
              <CardDescription>
                Email and certificate settings for your organisation
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <FormField
                control={form.control}
                name="EmailTeamName"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Email Team Name</FormLabel>
                    <FormControl>
                      <Input placeholder="Training Team" {...field} />
                    </FormControl>
                    <FormDescription>
                      The team name used in outgoing email sign-offs
                    </FormDescription>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={form.control}
                name="TalkCertificatePrefix"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Talk Certificate Prefix</FormLabel>
                    <FormControl>
                      <Input placeholder="LRN" {...field} />
                    </FormControl>
                    <FormDescription>
                      Prefix for individual talk certificate numbers (e.g. LRN-0001)
                    </FormDescription>
                    <FormMessage />
                  </FormItem>
                )}
              />
              <FormField
                control={form.control}
                name="CourseCertificatePrefix"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Course Certificate Prefix</FormLabel>
                    <FormControl>
                      <Input placeholder="TBC" {...field} />
                    </FormControl>
                    <FormDescription>
                      Prefix for course certificate numbers (e.g. TBC-0001)
                    </FormDescription>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </CardContent>
          </Card>

          <div className="flex justify-end">
            <Button type="submit" disabled={updateMutation.isPending}>
              {updateMutation.isPending ? 'Saving...' : 'Save Changes'}
            </Button>
          </div>
        </form>
      </Form>
    </div>
  );
}
