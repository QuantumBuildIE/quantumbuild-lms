'use client';

import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { useLookupValues } from '@/hooks/use-lookups';
import type { ContentCreationSettings } from '@/types/content-creation';

interface CategoryPanelProps {
  settings: ContentCreationSettings;
  onChange: (settings: ContentCreationSettings) => void;
  isSaving?: boolean;
}

export function CategoryPanel({ settings, onChange, isSaving }: CategoryPanelProps) {
  const { data: categories = [], isLoading: categoriesLoading } =
    useLookupValues('TrainingCategory');

  return (
    <div className="rounded-lg border bg-muted/30 p-4 space-y-4">
      <h3 className="text-sm font-semibold">Category</h3>

      <div className="space-y-1.5">
        <Label htmlFor="settings-category" className="text-sm">
          Training Category
        </Label>
        <Select
          value={settings.category ?? ''}
          onValueChange={(v) => onChange({ ...settings, category: v || null })}
          disabled={categoriesLoading || isSaving}
        >
          <SelectTrigger id="settings-category">
            <SelectValue
              placeholder={categoriesLoading ? 'Loading...' : 'Select a category'}
            />
          </SelectTrigger>
          <SelectContent>
            {categories.length === 0 && !categoriesLoading ? (
              <div className="px-2 py-4 text-sm text-muted-foreground text-center">
                No categories configured
              </div>
            ) : (
              categories.map((cat) => (
                <SelectItem key={cat.id} value={cat.name}>
                  {cat.name}
                </SelectItem>
              ))
            )}
          </SelectContent>
        </Select>
      </div>
    </div>
  );
}
