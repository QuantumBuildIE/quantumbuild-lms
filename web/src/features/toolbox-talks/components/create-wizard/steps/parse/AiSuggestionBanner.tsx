'use client';

import { Alert, AlertDescription } from '@/components/ui/alert';
import { Sparkles, AlertTriangle } from 'lucide-react';
import type { OutputType } from '@/types/content-creation';

interface AiSuggestionBannerProps {
  sectionCount: number;
  suggestedType: OutputType | null;
  selectedType: OutputType | null;
}

export function AiSuggestionBanner({
  sectionCount,
  suggestedType,
  selectedType,
}: AiSuggestionBannerProps) {
  if (!suggestedType) return null;

  const isOverridden = selectedType !== null && selectedType !== suggestedType;

  if (isOverridden) {
    return (
      <Alert className="border-amber-200 bg-amber-50 dark:border-amber-900 dark:bg-amber-950/30">
        <AlertTriangle className="h-4 w-4 text-amber-600" />
        <AlertDescription className="text-amber-800 dark:text-amber-200">
          <span className="font-medium">Override active.</span> AI suggested{' '}
          <span className="font-semibold">{suggestedType}</span> based on{' '}
          {sectionCount} sections, but you selected{' '}
          <span className="font-semibold">{selectedType}</span>.
          {selectedType === 'Lesson' && sectionCount >= 3 && (
            <> Combining {sectionCount} sections into a single lesson may result in lengthy content.</>
          )}
        </AlertDescription>
      </Alert>
    );
  }

  return (
    <Alert className="border-blue-200 bg-blue-50 dark:border-blue-900 dark:bg-blue-950/30">
      <Sparkles className="h-4 w-4 text-blue-600" />
      <AlertDescription className="text-blue-800 dark:text-blue-200">
        <span className="font-medium">AI suggests:</span>{' '}
        <span className="font-semibold">{suggestedType}</span>
        {suggestedType === 'Course' ? (
          <> — {sectionCount} sections detected, ideal for a multi-lesson course with sequential completion.</>
        ) : (
          <> — {sectionCount} section{sectionCount !== 1 ? 's' : ''} detected, suitable for a single lesson.</>
        )}
      </AlertDescription>
    </Alert>
  );
}
