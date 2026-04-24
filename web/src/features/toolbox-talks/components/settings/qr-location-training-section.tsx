'use client';

import { useState } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Switch } from '@/components/ui/switch';
import { Skeleton } from '@/components/ui/skeleton';
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
import { toast } from 'sonner';
import {
  useTenantSettings,
  useUpdateTenantSettings,
} from '@/lib/api/admin/use-tenant-settings';
import { useAllEmployees } from '@/lib/api/admin/use-employees';

const SETTINGS_KEY = 'QrLocationTrainingEnabled';

export function QrLocationTrainingSection() {
  const { data: settings, isLoading } = useTenantSettings();
  const updateMutation = useUpdateTenantSettings();
  const { data: allEmployees = [] } = useAllEmployees();
  const [confirmOpen, setConfirmOpen] = useState(false);

  const enabled = settings?.[SETTINGS_KEY] === 'true';
  const activeEmployeeCount = allEmployees.filter((e) => e.isActive).length;

  const handleToggle = async (checked: boolean) => {
    if (checked && !enabled) {
      // Enabling for the first time — show confirmation dialog
      setConfirmOpen(true);
      return;
    }

    // Disabling — no confirmation needed
    try {
      await updateMutation.mutateAsync({
        settings: [{ key: SETTINGS_KEY, value: 'false' }],
      });
      toast.success('QR Location Training disabled');
    } catch {
      toast.error('Failed to save setting');
    }
  };

  const handleConfirmEnable = async () => {
    setConfirmOpen(false);
    try {
      await updateMutation.mutateAsync({
        settings: [{ key: SETTINGS_KEY, value: 'true' }],
      });
      toast.success('QR Location Training enabled — generating PINs for all employees');
    } catch {
      toast.error('Failed to save setting');
    }
  };

  if (isLoading) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>QR Location Training</CardTitle>
        </CardHeader>
        <CardContent>
          <Skeleton className="h-6 w-10" />
        </CardContent>
      </Card>
    );
  }

  return (
    <>
      <Card>
        <CardHeader>
          <CardTitle>QR Location Training</CardTitle>
          <CardDescription>
            When enabled, employees receive a 6-digit workstation access PIN. This PIN is used to
            identify employees at QR-enabled training stations and worksite locations. Enabling this
            feature will generate PINs for all existing employees and send them a notification email.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <Switch
            checked={enabled}
            onCheckedChange={handleToggle}
            disabled={updateMutation.isPending}
            aria-label="Enable QR Location Training"
          />
          {enabled && (
            <p className="text-sm text-muted-foreground">
              QR Location Training is active. Employees can receive a new PIN at any time from their
              employee detail page.
            </p>
          )}
          {!enabled && (
            <div className="rounded-md border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800">
              <strong>Note:</strong> Enabling this feature will trigger a PIN notification email to
              all active employees. PINs are retained even if the feature is later disabled.
            </div>
          )}
        </CardContent>
      </Card>

      <AlertDialog open={confirmOpen} onOpenChange={setConfirmOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Enable QR Location Training?</AlertDialogTitle>
            <AlertDialogDescription>
              This will generate unique workstation access PINs for{' '}
              <strong>{activeEmployeeCount} active employee{activeEmployeeCount !== 1 ? 's' : ''}</strong>{' '}
              and send each of them a notification email with their PIN.
              <br />
              <br />
              This action cannot be undone — PINs are retained even if the feature is later
              disabled.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction onClick={handleConfirmEnable}>
              Enable &amp; Generate PINs
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}
