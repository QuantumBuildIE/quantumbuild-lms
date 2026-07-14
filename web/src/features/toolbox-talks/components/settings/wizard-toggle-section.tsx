'use client';

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Switch } from '@/components/ui/switch';
import { Skeleton } from '@/components/ui/skeleton';
import { toast } from 'sonner';
import {
  useTenantSettings,
  useUpdateTenantSettings,
} from '@/lib/api/admin/use-tenant-settings';

const SETTINGS_KEY = 'UseNewWizard';

export function WizardToggleSection() {
  const { data: settings, isLoading } = useTenantSettings();
  const updateMutation = useUpdateTenantSettings();

  const enabled = settings?.[SETTINGS_KEY] === 'true';

  const handleToggle = async (checked: boolean) => {
    try {
      await updateMutation.mutateAsync({
        settings: [{ key: SETTINGS_KEY, value: String(checked) }],
      });
      toast.success(checked ? 'New wizard enabled' : 'Classic wizard enabled');
    } catch {
      toast.error('Failed to save setting');
    }
  };

  if (isLoading) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>Wizard Version</CardTitle>
        </CardHeader>
        <CardContent>
          <Skeleton className="h-6 w-10" />
        </CardContent>
      </Card>
    );
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>Wizard Version</CardTitle>
        <CardDescription>
          When enabled, the &ldquo;Create New&rdquo; button on the Learnings list opens the new
          wizard instead of the classic version. Existing drafts continue with the wizard that
          created them — toggling this does not migrate in-progress work.
        </CardDescription>
        <CardDescription>
          Only enable this for tenants where the new wizard&apos;s edit workflow has been verified
          sufficient for production use. See CLAUDE.md Note 29 and BACKLOG §24 for the gate
          criteria.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <Switch
          checked={enabled}
          onCheckedChange={handleToggle}
          disabled={updateMutation.isPending}
          aria-label="Use new wizard for learning creation"
        />
      </CardContent>
    </Card>
  );
}
