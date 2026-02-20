'use client';

import * as React from 'react';
import { format, formatDistanceToNow, isPast } from 'date-fns';
import { Calendar, Clock, Play, Eye, ArrowRight, AlertTriangle, RefreshCw } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardFooter, CardHeader, CardTitle } from '@/components/ui/card';
import { Progress } from '@/components/ui/progress';
import { cn } from '@/lib/utils';
import type { MyToolboxTalkListItem } from '@/types/toolbox-talks';
import {
  SCHEDULED_TALK_STATUS_VARIANTS as statusVariants,
  SCHEDULED_TALK_STATUS_LABELS as statusLabels,
} from '@/lib/constants/status';

interface TalkCardProps {
  talk: MyToolboxTalkListItem;
  onAction: (id: string) => void;
  className?: string;
}

export function TalkCard({ talk, onAction, className }: TalkCardProps) {
  const dueDate = new Date(talk.dueDate);
  const isOverdue = talk.isOverdue || (isPast(dueDate) && talk.status !== 'Completed');
  const effectiveStatus = isOverdue && talk.status !== 'Completed' ? 'Overdue' : talk.status;

  // Determine action button text and icon
  const getActionButton = () => {
    switch (talk.status) {
      case 'Completed':
        return { text: 'View', icon: Eye };
      case 'InProgress':
        return { text: 'Continue', icon: ArrowRight };
      default:
        return { text: 'Start', icon: Play };
    }
  };

  const actionButton = getActionButton();
  const ActionIcon = actionButton.icon;

  // Format due date display
  const formatDueDate = () => {
    if (talk.status === 'Completed') {
      return `Completed ${format(dueDate, 'MMM d, yyyy')}`;
    }
    if (isOverdue) {
      return `Overdue by ${formatDistanceToNow(dueDate)}`;
    }
    if (talk.daysUntilDue <= 0) {
      return 'Due today';
    }
    if (talk.daysUntilDue === 1) {
      return 'Due tomorrow';
    }
    if (talk.daysUntilDue <= 7) {
      return `Due in ${talk.daysUntilDue} days`;
    }
    return `Due ${format(dueDate, 'MMM d, yyyy')}`;
  };

  return (
    <Card
      className={cn(
        'transition-shadow hover:shadow-md',
        isOverdue && talk.status !== 'Completed' && 'border-destructive/50',
        className
      )}
    >
      <CardHeader className="pb-2">
        <div className="flex items-start justify-between gap-2">
          <div className="flex items-center gap-2">
            <CardTitle className="text-base font-medium line-clamp-2">
              {talk.code && (
                <span className="text-muted-foreground font-mono text-sm mr-1.5">{talk.code}</span>
              )}
              {talk.title}
            </CardTitle>
          </div>
          <div className="flex items-center gap-1 shrink-0">
            {talk.isRefresher && (
              <Badge variant="outline" className="bg-orange-100 text-orange-800 border-orange-200 dark:bg-orange-900/20 dark:text-orange-400 dark:border-orange-800">
                <RefreshCw className="h-3 w-3 mr-1" />
                Refresher
              </Badge>
            )}
            <Badge variant={statusVariants[effectiveStatus]}>
              {statusLabels[effectiveStatus]}
            </Badge>
          </div>
        </div>
      </CardHeader>
      <CardContent className="space-y-3">
        {talk.description && (
          <p className="text-sm text-muted-foreground line-clamp-2">{talk.description}</p>
        )}

        {/* Due date */}
        <div
          className={cn(
            'flex items-center gap-2 text-sm',
            isOverdue && talk.status !== 'Completed'
              ? 'text-destructive font-medium'
              : 'text-muted-foreground'
          )}
        >
          {isOverdue && talk.status !== 'Completed' ? (
            <AlertTriangle className="h-4 w-4" />
          ) : (
            <Calendar className="h-4 w-4" />
          )}
          <span>{formatDueDate()}</span>
        </div>

        {/* Progress bar */}
        {talk.status !== 'Completed' && (
          <div className="space-y-1">
            <div className="flex items-center justify-between text-xs text-muted-foreground">
              <span>Progress</span>
              <span>
                {talk.completedSections} of {talk.totalSections} sections
              </span>
            </div>
            <Progress value={talk.progressPercent} className="h-2" />
          </div>
        )}

        {/* Completed progress - show as complete */}
        {talk.status === 'Completed' && (
          <div className="flex items-center gap-2 text-sm text-muted-foreground">
            <Clock className="h-4 w-4" />
            <span>All {talk.totalSections} sections completed</span>
          </div>
        )}

        {/* Info badges */}
        <div className="flex flex-wrap gap-2">
          {talk.hasVideo && (
            <Badge variant="outline" className="text-xs">
              Video
            </Badge>
          )}
          {talk.requiresQuiz && (
            <Badge variant="outline" className="text-xs">
              Quiz
            </Badge>
          )}
        </div>
      </CardContent>
      <CardFooter className="pt-2">
        <Button
          onClick={() => onAction(talk.scheduledTalkId)}
          className="w-full"
          variant={talk.status === 'Completed' ? 'outline' : 'default'}
        >
          <ActionIcon className="h-4 w-4 mr-2" />
          {actionButton.text}
        </Button>
      </CardFooter>
    </Card>
  );
}

// Skeleton component for loading state
export function TalkCardSkeleton() {
  return (
    <Card className="animate-pulse">
      <CardHeader className="pb-2">
        <div className="flex items-start justify-between gap-2">
          <div className="h-5 w-3/4 bg-muted rounded" />
          <div className="h-5 w-16 bg-muted rounded-full" />
        </div>
      </CardHeader>
      <CardContent className="space-y-3">
        <div className="h-4 w-full bg-muted rounded" />
        <div className="h-4 w-2/3 bg-muted rounded" />
        <div className="space-y-1">
          <div className="flex justify-between">
            <div className="h-3 w-16 bg-muted rounded" />
            <div className="h-3 w-24 bg-muted rounded" />
          </div>
          <div className="h-2 w-full bg-muted rounded" />
        </div>
        <div className="flex gap-2">
          <div className="h-5 w-12 bg-muted rounded-full" />
          <div className="h-5 w-12 bg-muted rounded-full" />
        </div>
      </CardContent>
      <CardFooter className="pt-2">
        <div className="h-9 w-full bg-muted rounded" />
      </CardFooter>
    </Card>
  );
}
