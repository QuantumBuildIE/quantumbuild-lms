'use client';

import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import type { ContentCreationSettings } from '@/types/content-creation';

interface RefresherPanelProps {
  settings: ContentCreationSettings;
  onChange: (settings: ContentCreationSettings) => void;
  isSaving?: boolean;
}

const FREQUENCY_OPTIONS = [
  { value: 'Once', label: 'Once (no refresher)' },
  { value: 'Monthly', label: 'Monthly' },
  { value: 'Quarterly', label: 'Quarterly' },
  { value: 'Annually', label: 'Annually' },
] as const;

export function RefresherPanel({ settings, onChange, isSaving }: RefresherPanelProps) {
  return (
    <div className="rounded-lg border bg-muted/30 p-4 space-y-4">
      <h3 className="text-sm font-semibold">Refresher Frequency</h3>

      <div className="space-y-1.5">
        <Label htmlFor="settings-frequency" className="text-sm">
          How often should this be refreshed?
        </Label>
        <Select
          value={settings.refresherFrequency}
          onValueChange={(v) =>
            onChange({
              ...settings,
              refresherFrequency: v as ContentCreationSettings['refresherFrequency'],
            })
          }
          disabled={isSaving}
        >
          <SelectTrigger id="settings-frequency">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {FREQUENCY_OPTIONS.map((opt) => (
              <SelectItem key={opt.value} value={opt.value}>
                {opt.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>
    </div>
  );
}
