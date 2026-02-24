'use client';

import { Badge } from '@/components/ui/badge';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/tooltip';
import { Languages, AlertTriangle, XCircle, Minus, CheckCircle2 } from 'lucide-react';
import type { TranslationQueueStatus } from '@/types/lesson-parser';

interface TranslationStatusBadgeProps {
  status: TranslationQueueStatus;
  translationsQueued: number;
  translationLanguages: string | null;
  translationFailures: string | null;
}

interface TranslationFailure {
  TalkId: string;
  Language: string;
  Reason: string;
}

function parseFailures(json: string | null): TranslationFailure[] {
  if (!json) return [];
  try {
    return JSON.parse(json) as TranslationFailure[];
  } catch {
    return [];
  }
}

export function TranslationStatusBadge({
  status,
  translationsQueued,
  translationLanguages,
  translationFailures,
}: TranslationStatusBadgeProps) {
  switch (status) {
    case 'NotRequired':
      return (
        <Badge variant="secondary" className="gap-1 text-muted-foreground">
          <Minus className="h-3 w-3" />
          Not Required
        </Badge>
      );

    case 'Queued':
      return (
        <TooltipProvider>
          <Tooltip>
            <TooltipTrigger asChild>
              <Badge variant="secondary" className="gap-1 bg-blue-100 text-blue-700 hover:bg-blue-100">
                <Languages className="h-3 w-3" />
                Queued ({translationsQueued})
              </Badge>
            </TooltipTrigger>
            <TooltipContent>
              <p>Languages: {translationLanguages?.split(',').join(', ').toUpperCase() ?? 'N/A'}</p>
            </TooltipContent>
          </Tooltip>
        </TooltipProvider>
      );

    case 'PartialFailure': {
      const failures = parseFailures(translationFailures);
      return (
        <TooltipProvider>
          <Tooltip>
            <TooltipTrigger asChild>
              <Badge variant="secondary" className="gap-1 bg-amber-100 text-amber-700 hover:bg-amber-100">
                <AlertTriangle className="h-3 w-3" />
                Partial Failure
              </Badge>
            </TooltipTrigger>
            <TooltipContent className="max-w-sm">
              <div className="space-y-1">
                <p className="font-medium">Some translations failed:</p>
                {failures.length > 0 ? (
                  <ul className="text-xs space-y-0.5">
                    {failures.slice(0, 5).map((f, i) => (
                      <li key={i}>
                        {f.Language.toUpperCase()}: {f.Reason}
                      </li>
                    ))}
                    {failures.length > 5 && (
                      <li>...and {failures.length - 5} more</li>
                    )}
                  </ul>
                ) : (
                  <p className="text-xs">Check individual talks for details.</p>
                )}
                <p className="text-xs text-muted-foreground mt-1">
                  Retry via Learnings module
                </p>
              </div>
            </TooltipContent>
          </Tooltip>
        </TooltipProvider>
      );
    }

    case 'Failed': {
      const failures = parseFailures(translationFailures);
      return (
        <TooltipProvider>
          <Tooltip>
            <TooltipTrigger asChild>
              <Badge variant="secondary" className="gap-1 bg-red-100 text-red-700 hover:bg-red-100">
                <XCircle className="h-3 w-3" />
                Failed
              </Badge>
            </TooltipTrigger>
            <TooltipContent className="max-w-sm">
              <div className="space-y-1">
                <p className="font-medium">All translations failed</p>
                {failures.length > 0 && (
                  <ul className="text-xs space-y-0.5">
                    {failures.slice(0, 3).map((f, i) => (
                      <li key={i}>
                        {f.Language.toUpperCase()}: {f.Reason}
                      </li>
                    ))}
                    {failures.length > 3 && (
                      <li>...and {failures.length - 3} more</li>
                    )}
                  </ul>
                )}
                <p className="text-xs text-muted-foreground mt-1">
                  Retry via Learnings module
                </p>
              </div>
            </TooltipContent>
          </Tooltip>
        </TooltipProvider>
      );
    }

    case 'Completed':
      return (
        <TooltipProvider>
          <Tooltip>
            <TooltipTrigger asChild>
              <Badge variant="secondary" className="gap-1 bg-green-100 text-green-700 hover:bg-green-100">
                <CheckCircle2 className="h-3 w-3" />
                Translated
              </Badge>
            </TooltipTrigger>
            <TooltipContent>
              <p>All {translationsQueued} translation(s) completed</p>
              <p className="text-xs text-muted-foreground">
                Languages: {translationLanguages?.split(',').join(', ').toUpperCase() ?? 'N/A'}
              </p>
            </TooltipContent>
          </Tooltip>
        </TooltipProvider>
      );

    default:
      return <Badge variant="secondary">{status}</Badge>;
  }
}
