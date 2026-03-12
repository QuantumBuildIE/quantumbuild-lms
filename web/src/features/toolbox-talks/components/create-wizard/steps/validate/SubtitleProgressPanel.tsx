'use client';

import { useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Progress } from '@/components/ui/progress';
import {
  ChevronDown,
  ChevronRight,
  Subtitles,
  Wifi,
  WifiOff,
  Check,
  Loader2,
  AlertTriangle,
  Clock,
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { useSubtitleHub } from '@/features/toolbox-talks/hooks/use-subtitle-hub';
import type {
  SubtitleProcessingStatus,
  SubtitleTranslationStatus,
  LanguageProgress,
} from '@/features/toolbox-talks/hooks/use-subtitle-hub';

// ============================================
// Props
// ============================================

interface SubtitleProgressPanelProps {
  jobId: string;
}

// ============================================
// Helpers
// ============================================

function getOverallStatusLabel(status: SubtitleProcessingStatus): string {
  switch (status) {
    case 'Pending': return 'Pending';
    case 'Transcribing': return 'Transcribing video...';
    case 'Translating': return 'Translating subtitles...';
    case 'Uploading': return 'Uploading files...';
    case 'Completed': return 'Subtitles ready';
    case 'Failed': return 'Subtitle processing failed';
    case 'Cancelled': return 'Cancelled';
    default: return status;
  }
}

function getLanguageStatusIcon(status: SubtitleTranslationStatus) {
  switch (status) {
    case 'Completed':
      return <Check className="h-3.5 w-3.5 text-green-600" />;
    case 'InProgress':
      return <Loader2 className="h-3.5 w-3.5 animate-spin text-blue-600" />;
    case 'Failed':
      return <AlertTriangle className="h-3.5 w-3.5 text-red-600" />;
    case 'Pending':
    default:
      return <Clock className="h-3.5 w-3.5 text-muted-foreground" />;
  }
}

function getOverallBadgeVariant(status: SubtitleProcessingStatus | null) {
  if (!status) return 'secondary' as const;
  if (status === 'Completed') return 'default' as const;
  if (status === 'Failed' || status === 'Cancelled') return 'destructive' as const;
  return 'secondary' as const;
}

// ============================================
// Component
// ============================================

export function SubtitleProgressPanel({ jobId }: SubtitleProgressPanelProps) {
  const [isExpanded, setIsExpanded] = useState(true);

  const {
    isConnected,
    overallStatus,
    percentComplete,
    currentStep,
    languageProgress,
    error,
    isComplete,
  } = useSubtitleHub(jobId);

  const isTerminal = overallStatus === 'Completed' || overallStatus === 'Failed' || overallStatus === 'Cancelled';

  return (
    <div className="rounded-lg border bg-card">
      {/* Collapsible header */}
      <button
        type="button"
        className="flex w-full items-center gap-3 p-4 text-left hover:bg-muted/50 transition-colors"
        onClick={() => setIsExpanded(!isExpanded)}
      >
        {isExpanded ? (
          <ChevronDown className="h-4 w-4 text-muted-foreground shrink-0" />
        ) : (
          <ChevronRight className="h-4 w-4 text-muted-foreground shrink-0" />
        )}

        <Subtitles className="h-4 w-4 text-muted-foreground shrink-0" />

        <span className="text-sm font-medium flex-1">
          Subtitle Processing
        </span>

        {/* Status badge */}
        {overallStatus && (
          <Badge
            variant={getOverallBadgeVariant(overallStatus)}
            className={cn(
              'text-xs',
              overallStatus === 'Completed' && 'bg-green-100 text-green-800 hover:bg-green-100'
            )}
          >
            {getOverallStatusLabel(overallStatus)}
          </Badge>
        )}

        {/* Connection indicator */}
        {!isTerminal && (
          <div
            className={cn(
              'flex items-center gap-1 text-xs',
              isConnected ? 'text-green-600' : 'text-muted-foreground'
            )}
          >
            {isConnected ? (
              <Wifi className="h-3 w-3" />
            ) : (
              <WifiOff className="h-3 w-3" />
            )}
          </div>
        )}
      </button>

      {/* Expandable content */}
      {isExpanded && (
        <div className="border-t px-4 pb-4 pt-3 space-y-3">
          {/* Overall progress bar */}
          <div className="space-y-1">
            <div className="flex items-center justify-between text-sm">
              <span className="text-muted-foreground">
                {currentStep || (overallStatus ? getOverallStatusLabel(overallStatus) : 'Waiting for updates...')}
              </span>
              <span className="tabular-nums text-muted-foreground">
                {Math.round(percentComplete)}%
              </span>
            </div>
            <Progress value={percentComplete} className="h-2" />
          </div>

          {/* Error message */}
          {error && (
            <div className="flex items-start gap-2 rounded-md bg-red-50 p-2 text-sm text-red-700">
              <AlertTriangle className="h-4 w-4 shrink-0 mt-0.5" />
              <span>{error}</span>
            </div>
          )}

          {/* Per-language progress rows */}
          {languageProgress.length > 0 && (
            <div className="space-y-2">
              <span className="text-xs font-medium text-muted-foreground uppercase tracking-wider">
                Languages
              </span>
              <div className="space-y-1.5">
                {languageProgress.map((lang) => (
                  <div
                    key={lang.languageCode}
                    className="flex items-center gap-3 rounded-md border px-3 py-2"
                  >
                    {getLanguageStatusIcon(lang.status)}

                    <span className="text-sm font-medium min-w-[100px]">
                      {lang.language}
                    </span>

                    <span className="text-xs text-muted-foreground uppercase">
                      {lang.languageCode}
                    </span>

                    <div className="flex-1">
                      <Progress value={lang.percentage} className="h-1.5" />
                    </div>

                    <span className="text-xs tabular-nums text-muted-foreground w-[36px] text-right">
                      {lang.percentage}%
                    </span>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Informational note */}
          {!isTerminal && (
            <p className="text-xs text-muted-foreground">
              Subtitle processing runs in the background and does not block your progress.
            </p>
          )}
        </div>
      )}
    </div>
  );
}
