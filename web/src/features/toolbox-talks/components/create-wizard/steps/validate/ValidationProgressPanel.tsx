'use client';

import { useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Progress } from '@/components/ui/progress';
import {
  ShieldAlert,
  ShieldCheck,
  ShieldX,
  Globe,
  Pencil,
  Check,
  X,
  Wifi,
  WifiOff,
} from 'lucide-react';
import { cn } from '@/lib/utils';
import type { ValidationOutcome } from '@/types/content-creation';

// ============================================
// Types
// ============================================

interface StatusCounts {
  pass: number;
  review: number;
  fail: number;
  running: number;
  pending: number;
}

interface ValidationProgressPanelProps {
  overallScore: number;
  percentComplete: number;
  sectionsComplete: number;
  totalSections: number;
  statusCounts: StatusCounts;
  safetyVerdict: ValidationOutcome | null;
  sourceDialect: string | null;
  progressMessage: string;
  isConnected: boolean;
}

// ============================================
// Component
// ============================================

export function ValidationProgressPanel({
  overallScore,
  percentComplete,
  sectionsComplete,
  totalSections,
  statusCounts,
  safetyVerdict,
  sourceDialect,
  progressMessage,
  isConnected,
}: ValidationProgressPanelProps) {
  const [isEditingDialect, setIsEditingDialect] = useState(false);
  const [dialectOverride, setDialectOverride] = useState('');

  const isRunning = statusCounts.running > 0 || statusCounts.pending > 0;

  return (
    <div className="space-y-4 rounded-lg border bg-card p-4">
      {/* Top row: Score + Progress + Connection */}
      <div className="flex items-center gap-4">
        {/* Overall score */}
        <div className="flex flex-col items-center justify-center rounded-lg border bg-muted/30 px-4 py-2 min-w-[80px]">
          <span
            className={cn(
              'text-2xl font-bold tabular-nums',
              overallScore >= 75
                ? 'text-green-600'
                : overallScore >= 60
                  ? 'text-amber-600'
                  : sectionsComplete > 0
                    ? 'text-red-600'
                    : 'text-muted-foreground'
            )}
          >
            {sectionsComplete > 0 ? overallScore : '--'}
          </span>
          <span className="text-xs text-muted-foreground">Score</span>
        </div>

        {/* Progress bar + message */}
        <div className="flex-1 space-y-1">
          <div className="flex items-center justify-between text-sm">
            <span className="text-muted-foreground">
              {progressMessage || `${sectionsComplete} / ${totalSections} sections`}
            </span>
            <span className="tabular-nums text-muted-foreground">
              {Math.round(percentComplete)}%
            </span>
          </div>
          <Progress value={percentComplete} className="h-2" />
        </div>

        {/* Connection indicator */}
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
          <span className="hidden sm:inline">
            {isConnected ? 'Live' : 'Offline'}
          </span>
        </div>
      </div>

      {/* Status counts row */}
      <div className="flex flex-wrap items-center gap-3 text-sm">
        {statusCounts.pass > 0 && (
          <span className="flex items-center gap-1">
            <span className="h-2 w-2 rounded-full bg-green-500" />
            <span className="text-green-700">Pass ({statusCounts.pass})</span>
          </span>
        )}
        {statusCounts.review > 0 && (
          <span className="flex items-center gap-1">
            <span className="h-2 w-2 rounded-full bg-amber-500" />
            <span className="text-amber-700">
              Review ({statusCounts.review})
            </span>
          </span>
        )}
        {statusCounts.fail > 0 && (
          <span className="flex items-center gap-1">
            <span className="h-2 w-2 rounded-full bg-red-500" />
            <span className="text-red-700">Fail ({statusCounts.fail})</span>
          </span>
        )}
        {statusCounts.running > 0 && (
          <span className="flex items-center gap-1">
            <span className="h-2 w-2 animate-pulse rounded-full bg-blue-500" />
            <span className="text-blue-700">
              Running ({statusCounts.running})
            </span>
          </span>
        )}
        {statusCounts.pending > 0 && (
          <span className="flex items-center gap-1">
            <span className="h-2 w-2 rounded-full bg-gray-300" />
            <span className="text-muted-foreground">
              Pending ({statusCounts.pending})
            </span>
          </span>
        )}

        {/* Spacer */}
        <span className="flex-1" />

        {/* Safety verdict badge */}
        {safetyVerdict && (
          <Badge
            variant={
              safetyVerdict === 'Pass'
                ? 'default'
                : safetyVerdict === 'Review'
                  ? 'secondary'
                  : 'destructive'
            }
            className={cn(
              'gap-1',
              safetyVerdict === 'Pass' &&
                'bg-green-100 text-green-800 hover:bg-green-100',
              safetyVerdict === 'Review' &&
                'bg-amber-100 text-amber-800 hover:bg-amber-100'
            )}
          >
            {safetyVerdict === 'Pass' ? (
              <ShieldCheck className="h-3 w-3" />
            ) : safetyVerdict === 'Fail' ? (
              <ShieldX className="h-3 w-3" />
            ) : (
              <ShieldAlert className="h-3 w-3" />
            )}
            Safety: {safetyVerdict}
          </Badge>
        )}

        {/* Dialect display */}
        {sourceDialect && !isEditingDialect && (
          <span className="flex items-center gap-1 text-muted-foreground">
            <Globe className="h-3 w-3" />
            Dialect: {sourceDialect}
            <Button
              variant="ghost"
              size="icon"
              className="h-5 w-5"
              onClick={() => {
                setDialectOverride(sourceDialect);
                setIsEditingDialect(true);
              }}
            >
              <Pencil className="h-3 w-3" />
            </Button>
          </span>
        )}
        {isEditingDialect && (
          <span className="flex items-center gap-1">
            <Globe className="h-3 w-3 text-muted-foreground" />
            <Input
              value={dialectOverride}
              onChange={(e) => setDialectOverride(e.target.value)}
              className="h-6 w-32 text-xs"
              autoFocus
            />
            <Button
              variant="ghost"
              size="icon"
              className="h-5 w-5 text-green-600"
              onClick={() => setIsEditingDialect(false)}
            >
              <Check className="h-3 w-3" />
            </Button>
            <Button
              variant="ghost"
              size="icon"
              className="h-5 w-5 text-muted-foreground"
              onClick={() => setIsEditingDialect(false)}
            >
              <X className="h-3 w-3" />
            </Button>
          </span>
        )}
      </div>
    </div>
  );
}
