'use client';

import { use } from 'react';
import { useRouter } from 'next/navigation';
import { format } from 'date-fns';
import {
  ArrowLeftIcon,
  BookOpenIcon,
  CalendarIcon,
  CheckCircle2Icon,
  LockIcon,
  PlayIcon,
  ArrowRightIcon,
  EyeIcon,
  AlertTriangleIcon,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Progress } from '@/components/ui/progress';
import { Skeleton } from '@/components/ui/skeleton';
import { useMyCourseAssignment } from '@/lib/api/toolbox-talks/use-course-assignments';
import type { CourseScheduledTalkDto } from '@/lib/api/toolbox-talks/course-assignments';
import { cn } from '@/lib/utils';

const getStatusBadge = (status: string) => {
  switch (status) {
    case 'Completed':
      return <Badge variant="outline" className="bg-green-100 text-green-800 border-green-200 dark:bg-green-900/20 dark:text-green-400 dark:border-green-800">Completed</Badge>;
    case 'InProgress':
      return <Badge variant="default">In Progress</Badge>;
    case 'Overdue':
      return <Badge variant="destructive">Overdue</Badge>;
    case 'Assigned':
    default:
      return <Badge variant="secondary">Assigned</Badge>;
  }
};

interface TalkItemProps {
  talk: CourseScheduledTalkDto;
  onNavigate: (scheduledTalkId: string) => void;
}

function TalkItem({ talk, onNavigate }: TalkItemProps) {
  const getActionButton = () => {
    if (talk.isLocked) return null;
    switch (talk.status) {
      case 'Completed':
        return { text: 'Review', icon: EyeIcon, variant: 'outline' as const };
      case 'InProgress':
        return { text: 'Continue', icon: ArrowRightIcon, variant: 'default' as const };
      default:
        return { text: 'Start', icon: PlayIcon, variant: 'default' as const };
    }
  };

  const action = getActionButton();
  const isCompleted = talk.status === 'Completed';

  return (
    <div className={cn(
      'flex items-center gap-4 rounded-lg border p-4 transition-colors',
      talk.isLocked && 'opacity-60 bg-muted/30',
      isCompleted && 'bg-green-50/50 dark:bg-green-900/5 border-green-200 dark:border-green-900/30'
    )}>
      {/* Order number */}
      <div className={cn(
        'flex h-8 w-8 shrink-0 items-center justify-center rounded-full text-sm font-medium',
        isCompleted
          ? 'bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400'
          : talk.isLocked
            ? 'bg-muted text-muted-foreground'
            : 'bg-primary/10 text-primary'
      )}>
        {isCompleted ? (
          <CheckCircle2Icon className="h-4 w-4" />
        ) : (
          talk.orderIndex + 1
        )}
      </div>

      {/* Talk info */}
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2">
          <span className={cn(
            'font-medium text-sm',
            talk.isLocked && 'text-muted-foreground'
          )}>
            {talk.talkTitle}
          </span>
          {!talk.isRequired && (
            <Badge variant="outline" className="text-xs">Optional</Badge>
          )}
        </div>

        {/* Status / Lock info */}
        {talk.isLocked ? (
          <div className="flex items-center gap-1.5 mt-1 text-xs text-muted-foreground">
            <LockIcon className="h-3 w-3" />
            <span>{talk.lockedReason}</span>
          </div>
        ) : talk.completedAt ? (
          <p className="text-xs text-muted-foreground mt-1">
            Completed {format(new Date(talk.completedAt), 'MMM d, yyyy')}
          </p>
        ) : null}
      </div>

      {/* Action */}
      {action && (
        <Button
          variant={action.variant}
          size="sm"
          onClick={() => onNavigate(talk.scheduledTalkId)}
          className="shrink-0"
        >
          <action.icon className="h-4 w-4 mr-1.5" />
          {action.text}
        </Button>
      )}

      {talk.isLocked && (
        <LockIcon className="h-4 w-4 text-muted-foreground shrink-0" />
      )}
    </div>
  );
}

export default function CourseViewerPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const router = useRouter();
  const { data: assignment, isLoading, error } = useMyCourseAssignment(id);

  const handleNavigateToTalk = (scheduledTalkId: string) => {
    router.push(`/toolbox-talks/${scheduledTalkId}`);
  };

  if (isLoading) {
    return (
      <div className="space-y-6">
        <div className="flex items-center gap-4">
          <Skeleton className="h-9 w-32" />
        </div>
        <div className="space-y-2">
          <Skeleton className="h-8 w-64" />
          <Skeleton className="h-4 w-96" />
        </div>
        <Skeleton className="h-4 w-full" />
        <div className="space-y-3">
          {Array.from({ length: 4 }).map((_, i) => (
            <Skeleton key={i} className="h-20 w-full" />
          ))}
        </div>
      </div>
    );
  }

  if (error || !assignment) {
    return (
      <div className="space-y-4">
        <Button variant="ghost" size="sm" onClick={() => router.push('/toolbox-talks')}>
          <ArrowLeftIcon className="mr-2 h-4 w-4" />
          Back to My Learnings
        </Button>
        <div className="rounded-lg border border-destructive/50 bg-destructive/10 p-4">
          <p className="text-destructive">
            {error ? `Error loading course: ${error instanceof Error ? error.message : 'Unknown error'}` : 'Course assignment not found'}
          </p>
        </div>
      </div>
    );
  }

  const isOverdue = assignment.status === 'Overdue';
  const sortedTalks = [...assignment.scheduledTalks].sort((a, b) => a.orderIndex - b.orderIndex);

  return (
    <div className="space-y-6">
      {/* Back button */}
      <Button variant="ghost" size="sm" onClick={() => router.push('/toolbox-talks')}>
        <ArrowLeftIcon className="mr-2 h-4 w-4" />
        Back to My Learnings
      </Button>

      {/* Course header */}
      <div className="space-y-4">
        <div className="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
          <div className="space-y-1">
            <div className="flex items-center gap-2">
              <BookOpenIcon className="h-5 w-5 text-muted-foreground" />
              <h1 className="text-2xl font-bold tracking-tight">{assignment.courseTitle}</h1>
            </div>
            {assignment.courseDescription && (
              <p className="text-muted-foreground max-w-2xl">
                {assignment.courseDescription}
              </p>
            )}
          </div>
          {getStatusBadge(assignment.status)}
        </div>

        {/* Meta info */}
        <div className="flex flex-wrap items-center gap-4 text-sm text-muted-foreground">
          {assignment.dueDate && (
            <div className={cn(
              'flex items-center gap-1.5',
              isOverdue && 'text-destructive font-medium'
            )}>
              {isOverdue ? (
                <AlertTriangleIcon className="h-4 w-4" />
              ) : (
                <CalendarIcon className="h-4 w-4" />
              )}
              <span>Due {format(new Date(assignment.dueDate), 'MMM d, yyyy')}</span>
            </div>
          )}
          <span>{assignment.completedTalks} of {assignment.totalTalks} talks completed</span>
        </div>

        {/* Overall progress */}
        <Card>
          <CardContent className="pt-6">
            <div className="space-y-2">
              <div className="flex items-center justify-between text-sm">
                <span className="font-medium">Overall Progress</span>
                <span className="text-muted-foreground">{assignment.progressPercent}%</span>
              </div>
              <Progress value={assignment.progressPercent} className="h-3" />
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Talk list */}
      <div className="space-y-3">
        <h2 className="text-lg font-semibold">Course Learnings</h2>
        <div className="space-y-2">
          {sortedTalks.map((talk) => (
            <TalkItem
              key={talk.scheduledTalkId}
              talk={talk}
              onNavigate={handleNavigateToTalk}
            />
          ))}
        </div>
      </div>
    </div>
  );
}
