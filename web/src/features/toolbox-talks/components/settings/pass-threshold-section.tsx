'use client';

import { useState, useEffect } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Skeleton } from '@/components/ui/skeleton';
import { Plus, Trash2, Save } from 'lucide-react';
import { toast } from 'sonner';
import {
  useTenantSettings,
  useUpdateTenantSettings,
} from '@/lib/api/admin/use-tenant-settings';

const SETTINGS_KEY = 'ValidationPassThresholds';

export function PassThresholdSection() {
  const { data: settings, isLoading } = useTenantSettings();
  const updateMutation = useUpdateTenantSettings();
  const [thresholds, setThresholds] = useState<number[]>([]);
  const [newThreshold, setNewThreshold] = useState('');
  const [isDirty, setIsDirty] = useState(false);

  // Load thresholds from tenant settings
  useEffect(() => {
    if (settings) {
      const raw = settings[SETTINGS_KEY];
      if (raw) {
        try {
          const parsed = JSON.parse(raw);
          if (Array.isArray(parsed)) {
            setThresholds(parsed.sort((a: number, b: number) => a - b));
            return;
          }
        } catch {
          // invalid JSON, use defaults
        }
      }
      // Default thresholds if none configured
      setThresholds([70, 75, 80, 85, 90]);
    }
  }, [settings]);

  const handleAdd = () => {
    const value = parseInt(newThreshold, 10);
    if (isNaN(value) || value < 50 || value > 100) {
      toast.error('Threshold must be a number between 50 and 100');
      return;
    }
    if (thresholds.includes(value)) {
      toast.error('This threshold already exists');
      return;
    }
    setThresholds((prev) => [...prev, value].sort((a, b) => a - b));
    setNewThreshold('');
    setIsDirty(true);
  };

  const handleRemove = (value: number) => {
    if (thresholds.length <= 1) {
      toast.error('At least one threshold must remain');
      return;
    }
    setThresholds((prev) => prev.filter((t) => t !== value));
    setIsDirty(true);
  };

  const handleSave = async () => {
    try {
      await updateMutation.mutateAsync({
        settings: [
          { key: SETTINGS_KEY, value: JSON.stringify(thresholds) },
        ],
      });
      setIsDirty(false);
      toast.success('Pass thresholds saved');
    } catch {
      toast.error('Failed to save thresholds');
    }
  };

  if (isLoading) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>Pass Threshold Options</CardTitle>
        </CardHeader>
        <CardContent>
          <Skeleton className="h-10 w-full" />
        </CardContent>
      </Card>
    );
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>Pass Threshold Options</CardTitle>
        <CardDescription>
          Configure available pass threshold percentages for translation validation runs.
          These values appear as options when starting a validation.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {/* Current thresholds */}
        <div className="flex flex-wrap gap-2">
          {thresholds.map((t) => (
            <Badge key={t} variant="secondary" className="gap-1 text-sm py-1.5 px-3">
              {t}%
              <button
                onClick={() => handleRemove(t)}
                disabled={thresholds.length <= 1}
                className="ml-1 text-muted-foreground hover:text-destructive disabled:opacity-30 disabled:pointer-events-none"
                title={thresholds.length <= 1 ? 'At least one threshold must remain' : 'Remove threshold'}
              >
                <Trash2 className="h-3 w-3" />
              </button>
            </Badge>
          ))}
          {thresholds.length === 0 && (
            <p className="text-sm text-muted-foreground">No thresholds configured.</p>
          )}
        </div>

        {/* Add new threshold */}
        <div className="flex gap-2 items-center">
          <Input
            type="number"
            min={50}
            max={100}
            value={newThreshold}
            onChange={(e) => setNewThreshold(e.target.value)}
            placeholder="Enter value (50-100)"
            className="w-48"
            onKeyDown={(e) => {
              if (e.key === 'Enter') {
                e.preventDefault();
                handleAdd();
              }
            }}
          />
          <Button variant="outline" size="sm" onClick={handleAdd}>
            <Plus className="h-3.5 w-3.5 mr-1" />
            Add
          </Button>
        </div>

        {/* Save button */}
        {isDirty && (
          <div className="flex justify-end">
            <Button onClick={handleSave} disabled={updateMutation.isPending}>
              <Save className="h-4 w-4 mr-2" />
              {updateMutation.isPending ? 'Saving...' : 'Save Changes'}
            </Button>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
