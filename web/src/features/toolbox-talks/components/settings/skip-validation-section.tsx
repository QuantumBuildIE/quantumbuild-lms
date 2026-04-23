'use client';

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Switch } from '@/components/ui/switch';
import { Skeleton } from '@/components/ui/skeleton';
import { toast } from 'sonner';
import {
  useTenantSettings,
  useUpdateTenantSettings,
} from '@/lib/api/admin/use-tenant-settings';

const SETTINGS_KEY = 'SkipValidationStep';

export function SkipValidationSection() {
  const { data: settings, isLoading } = useTenantSettings();
  const updateMutation = useUpdateTenantSettings();

  const enabled = settings?.[SETTINGS_KEY] === 'true';

  const handleToggle = async (checked: boolean) => {
    try {
      await updateMutation.mutateAsync({
        settings: [{ key: SETTINGS_KEY, value: String(checked) }],
      });
      toast.success(checked ? 'Validation step disabled' : 'Validation step enabled');
    } catch {
      toast.error('Failed to save setting');
    }
  };

  if (isLoading) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>Skip Validation Step</CardTitle>
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
        <CardTitle>Skip Validation Step</CardTitle>
        <CardDescription>
          When enabled, the translation validation review step is skipped in the content
          creation wizard. Translations will be published without manual review.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <Switch
          checked={enabled}
          onCheckedChange={handleToggle}
          disabled={updateMutation.isPending}
          aria-label="Skip validation step"
        />
      </CardContent>
    </Card>
  );
}
