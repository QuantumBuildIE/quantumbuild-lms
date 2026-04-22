'use client';

import { Switch } from '@/components/ui/switch';
import { Label } from '@/components/ui/label';
import { Button } from '@/components/ui/button';
import { Minus, Plus } from 'lucide-react';
import { cn } from '@/lib/utils';
import type { ContentCreationSettings } from '@/types/content-creation';

interface BehaviourPanelProps {
  settings: ContentCreationSettings;
  onChange: (settings: ContentCreationSettings) => void;
  isSaving?: boolean;
}

const WATCH_PERCENTAGES = [50, 60, 70, 80, 90, 100] as const;

export function BehaviourPanel({ settings, onChange, isSaving }: BehaviourPanelProps) {
  const adjustDays = (delta: number) => {
    const newDays = Math.min(90, Math.max(1, settings.autoAssignDueDays + delta));
    onChange({ ...settings, autoAssignDueDays: newDays });
  };

  return (
    <div className="rounded-lg border bg-muted/30 p-4 space-y-5">
      {/* Active on Publish */}
      <div className="flex items-center justify-between gap-3">
        <Label htmlFor="active-on-publish" className="text-sm">
          Active on Publish
        </Label>
        <Switch
          id="active-on-publish"
          checked={settings.isActiveOnPublish}
          onCheckedChange={(v) => onChange({ ...settings, isActiveOnPublish: v })}
          disabled={isSaving}
        />
      </div>

      {/* Generate Certificate */}
      <div className="flex items-center justify-between gap-3">
        <Label htmlFor="generate-certificate" className="text-sm">
          Generate Certificate
        </Label>
        <Switch
          id="generate-certificate"
          checked={settings.generateCertificate}
          onCheckedChange={(v) => onChange({ ...settings, generateCertificate: v })}
          disabled={isSaving}
        />
      </div>

      {/* Minimum Watch Percentage */}
      <div className="space-y-2">
        <Label className="text-sm">Minimum Watch Percentage</Label>
        <div className="flex flex-wrap gap-1.5">
          {WATCH_PERCENTAGES.map((pct) => (
            <Button
              key={pct}
              type="button"
              variant={settings.minimumWatchPercent === pct ? 'default' : 'outline'}
              size="sm"
              className={cn(
                'h-8 min-w-[3.5rem] text-xs tabular-nums',
                settings.minimumWatchPercent === pct && 'pointer-events-none'
              )}
              onClick={() => onChange({ ...settings, minimumWatchPercent: pct })}
              disabled={isSaving}
            >
              {pct}%
            </Button>
          ))}
        </div>
      </div>

      {/* Auto-assign */}
      <div className="space-y-3">
        <div className="flex items-center justify-between gap-3">
          <Label htmlFor="auto-assign" className="text-sm">
            Auto-assign to New Employees
          </Label>
          <Switch
            id="auto-assign"
            checked={settings.autoAssign}
            onCheckedChange={(v) => onChange({ ...settings, autoAssign: v })}
            disabled={isSaving}
          />
        </div>

        {settings.autoAssign && (
          <div className="flex items-center gap-3 pl-1">
            <Label className="text-sm text-muted-foreground">Due in</Label>
            <div className="flex items-center gap-1">
              <Button
                variant="outline"
                size="icon"
                className="h-7 w-7"
                onClick={() => adjustDays(-1)}
                disabled={settings.autoAssignDueDays <= 1 || isSaving}
              >
                <Minus className="h-3 w-3" />
              </Button>
              <span className="w-10 text-center text-sm font-medium tabular-nums">
                {settings.autoAssignDueDays}
              </span>
              <Button
                variant="outline"
                size="icon"
                className="h-7 w-7"
                onClick={() => adjustDays(1)}
                disabled={settings.autoAssignDueDays >= 90 || isSaving}
              >
                <Plus className="h-3 w-3" />
              </Button>
            </div>
            <span className="text-sm text-muted-foreground">days</span>
          </div>
        )}
      </div>
    </div>
  );
}
