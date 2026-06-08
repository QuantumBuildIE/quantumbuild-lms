'use client';

import { Fragment } from 'react';
import { FlagSeverity } from '@/types/content-creation';
import type { TranslationFlag } from '@/types/content-creation';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/tooltip';

// ============================================
// Types
// ============================================

interface Segment {
  start: number;
  end: number;
  flag: TranslationFlag | null;
}

interface FlaggedTextProps {
  text: string;
  flags: TranslationFlag[];
}

// ============================================
// Constants
// ============================================

const SEVERITY_CLASSES: Record<FlagSeverity, string> = {
  [FlagSeverity.Info]: 'bg-blue-100 text-blue-900',
  [FlagSeverity.Warning]: 'bg-amber-100 text-amber-900',
  [FlagSeverity.Error]: 'bg-red-100 text-red-900',
};

// ============================================
// Helpers
// ============================================

function buildSegments(text: string, flags: TranslationFlag[]): Segment[] {
  const sorted = [...flags].sort((a, b) => a.startOffset - b.startOffset);
  const segments: Segment[] = [];
  let cursor = 0;
  for (const flag of sorted) {
    const segStart = Math.max(cursor, flag.startOffset);
    if (segStart > cursor) {
      segments.push({ start: cursor, end: segStart, flag: null });
    }
    if (segStart < flag.endOffset) {
      segments.push({ start: segStart, end: flag.endOffset, flag });
      cursor = flag.endOffset;
    }
  }
  if (cursor < text.length) {
    segments.push({ start: cursor, end: text.length, flag: null });
  }
  return segments;
}

// ============================================
// Component
// ============================================

export function FlaggedText({ text, flags }: FlaggedTextProps) {
  if (flags.length === 0) {
    return <>{text}</>;
  }

  const segments = buildSegments(text, flags);

  return (
    <TooltipProvider>
      <>
        {segments.map((seg, i) => {
          const slice = text.slice(seg.start, seg.end);
          if (!seg.flag) {
            return <Fragment key={i}>{slice}</Fragment>;
          }
          return (
            <Tooltip key={i}>
              <TooltipTrigger asChild>
                <span className={SEVERITY_CLASSES[seg.flag.severity]}>
                  {slice}
                </span>
              </TooltipTrigger>
              <TooltipContent side="top">
                {seg.flag.reason}
              </TooltipContent>
            </Tooltip>
          );
        })}
      </>
    </TooltipProvider>
  );
}
