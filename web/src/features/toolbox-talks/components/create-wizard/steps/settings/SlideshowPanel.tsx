'use client';

import { Switch } from '@/components/ui/switch';
import { Label } from '@/components/ui/label';
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group';
import type { ContentCreationSettings, InputMode } from '@/types/content-creation';

interface SlideshowPanelProps {
  settings: ContentCreationSettings;
  onChange: (settings: ContentCreationSettings) => void;
  inputMode: InputMode;
  isSaving?: boolean;
}

function getSourceOptions(inputMode: InputMode) {
  switch (inputMode) {
    case 'Pdf':
      return [
        { value: 'pdf', label: 'From PDF pages' },
        { value: 'sections', label: 'From sections' },
      ];
    case 'Video':
      return [
        { value: 'video', label: 'From transcript' },
        { value: 'sections', label: 'From sections' },
      ];
    default:
      return [{ value: 'sections', label: 'From sections' }];
  }
}

export function SlideshowPanel({ settings, onChange, inputMode, isSaving }: SlideshowPanelProps) {
  const sourceOptions = getSourceOptions(inputMode);

  const handleToggle = (checked: boolean) => {
    onChange({
      ...settings,
      generateSlideshow: checked,
      // Default to first available source when toggling on
      ...(checked && { slideshowSource: sourceOptions[0].value }),
    });
  };

  return (
    <div className="rounded-lg border bg-muted/30 p-4 space-y-5">
      <h3 className="text-sm font-semibold">Slideshow</h3>

      <div className="flex items-center justify-between gap-3">
        <Label htmlFor="generate-slideshow" className="text-sm">
          Generate slideshow
        </Label>
        <Switch
          id="generate-slideshow"
          checked={settings.generateSlideshow}
          onCheckedChange={handleToggle}
          disabled={isSaving}
        />
      </div>

      {settings.generateSlideshow && (
        <RadioGroup
          value={settings.slideshowSource}
          onValueChange={(v) => onChange({ ...settings, slideshowSource: v })}
          disabled={isSaving}
          className="space-y-2 pl-1"
        >
          {sourceOptions.map((opt) => (
            <div key={opt.value} className="flex items-center gap-2">
              <RadioGroupItem value={opt.value} id={`slideshow-src-${opt.value}`} />
              <Label htmlFor={`slideshow-src-${opt.value}`} className="text-sm font-normal">
                {opt.label}
              </Label>
            </div>
          ))}
        </RadioGroup>
      )}
    </div>
  );
}
