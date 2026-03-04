'use client';

import { BookOpen, Library, Sparkles } from 'lucide-react';
import { cn } from '@/lib/utils';
import { Label } from '@/components/ui/label';
import { Badge } from '@/components/ui/badge';
import type { OutputType } from '@/types/content-creation';

interface OutputTypeSelectorProps {
  suggestedType: OutputType | null;
  selectedType: OutputType | null;
  onSelect: (type: OutputType) => void;
}

const OUTPUT_OPTIONS: {
  type: OutputType;
  label: string;
  icon: React.ElementType;
  description: string;
}[] = [
  {
    type: 'Lesson',
    label: 'Single Lesson',
    icon: BookOpen,
    description: 'All sections combined into one toolbox talk',
  },
  {
    type: 'Course',
    label: 'Course',
    icon: Library,
    description: 'Each section becomes a separate lesson in an ordered course',
  },
];

export function OutputTypeSelector({
  suggestedType,
  selectedType,
  onSelect,
}: OutputTypeSelectorProps) {
  return (
    <div>
      <Label className="mb-3 block text-sm font-medium">Output Type</Label>
      <div className="grid grid-cols-2 gap-3">
        {OUTPUT_OPTIONS.map(({ type, label, icon: Icon, description }) => {
          const isSelected = selectedType === type;
          const isSuggested = suggestedType === type;

          return (
            <button
              key={type}
              type="button"
              onClick={() => onSelect(type)}
              className={cn(
                'relative flex flex-col items-center gap-2 rounded-lg border-2 p-4 text-center transition-all hover:border-primary/50',
                isSelected
                  ? 'border-primary bg-primary/5'
                  : 'border-muted'
              )}
            >
              {isSuggested && (
                <Badge
                  variant="secondary"
                  className="absolute -top-2.5 right-2 gap-1 text-[10px]"
                >
                  <Sparkles className="h-3 w-3" />
                  AI Suggested
                </Badge>
              )}
              <Icon
                className={cn(
                  'h-6 w-6',
                  isSelected ? 'text-primary' : 'text-muted-foreground'
                )}
              />
              <span className="text-sm font-medium">{label}</span>
              <span className="text-xs text-muted-foreground">{description}</span>
            </button>
          );
        })}
      </div>
    </div>
  );
}
