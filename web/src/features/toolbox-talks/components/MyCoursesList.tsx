'use client';

import { useRouter } from 'next/navigation';
import { format } from 'date-fns';
import {
  BookOpenIcon,
  CalendarIcon,
  ArrowRightIcon,
  PlayIcon,
  CheckCircle2Icon,
  AlertTriangleIcon,
  Inbox,
  RefreshCwIcon,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardFooter, CardHeader, CardTitle } from '@/components/ui/card';
import { Progress } from '@/components/ui/progress';
import { Skeleton } from '@/components/ui/skeleton';
import { useMyCourseAssignments } from '@/lib/api/toolbox-talks/use-course-assignments';
import type { ToolboxTalkCourseAssignmentDto } from '@/lib/api/toolbox-talks/course-assignments';
import { cn } from '@/lib/utils';

const getEffectiveStatus = (assignment: ToolboxTalkCourseAssignmentDto) => {
  // Derive effective status from progress data as a fallback
  // (handles existing assignments that were completed before the backend fix)
  if (assignment.status === 'Completed' || (assignment.totalTalks > 0 && assignment.completedTalks >= assignment.totalTalks)) {
    return 'Completed';
  }
  if (assignment.status === 'Overdue') return 'Overdue';
  if (assignment.status === 'InProgress' || assignment.completedTalks > 0) {
    return 'InProgress';
  }
  return 'Assigned';
};

const getStatusBadge = (assignment: ToolboxTalkCourseAssignmentDto) => {
  const status = getEffectiveStatus(assignment);
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

const getActionButton = (assignment: ToolboxTalkCourseAssignmentDto) => {
  const status = getEffectiveStatus(assignment);
  switch (status) {
    case 'Completed':
      return { text: 'Review', icon: CheckCircle2Icon, variant: 'outline' as const };
    case 'InProgress':
      return { text: 'Continue', icon: ArrowRightIcon, variant: 'default' as const };
    default:
      return { text: 'Start', icon: PlayIcon, variant: 'default' as const };
  }
};

interface CourseCardProps {
  assignment: ToolboxTalkCourseAssignmentDto;
  onAction: (id: string) => void;
}

function CourseCard({ assignment, onAction }: CourseCardProps) {
  const effectiveStatus = getEffectiveStatus(assignment);
  const isOverdue = effectiveStatus === 'Overdue';
  const isCompleted = effectiveStatus === 'Completed';
  const action = getActionButton(assignment);
  const ActionIcon = action.icon;

  return (
    <Card className={cn(
      'transition-shadow hover:shadow-md',
      isOverdue && 'border-destructive/50'
    )}>
      <CardHeader className="pb-2">
        <div className="flex items-start justify-between gap-2">
          <div className="flex items-center gap-2">
            <BookOpenIcon className="h-4 w-4 text-muted-foreground shrink-0" />
            <CardTitle className="text-base font-medium line-clamp-2">
              {assignment.courseTitle}
            </CardTitle>
          </div>
          <div className="flex items-center gap-1 shrink-0">
            {assignment.isRefresher && (
              <Badge variant="outline" className="bg-orange-100 text-orange-800 border-orange-200 dark:bg-orange-900/20 dark:text-orange-400 dark:border-orange-800">
                <RefreshCwIcon className="h-3 w-3 mr-1" />
                Refresher
              </Badge>
            )}
            {getStatusBadge(assignment)}
          </div>
        </div>
      </CardHeader>
      <CardContent className="space-y-3">
        {assignment.courseDescription && (
          <p className="text-sm text-muted-foreground line-clamp-2">
            {assignment.courseDescription}
          </p>
        )}

        {/* Due date */}
        {assignment.dueDate && (
          <div className={cn(
            'flex items-center gap-2 text-sm',
            isOverdue ? 'text-destructive font-medium' : 'text-muted-foreground'
          )}>
            {isOverdue ? (
              <AlertTriangleIcon className="h-4 w-4" />
            ) : (
              <CalendarIcon className="h-4 w-4" />
            )}
            <span>Due {format(new Date(assignment.dueDate), 'MMM d, yyyy')}</span>
          </div>
        )}

        {/* Progress */}
        {!isCompleted ? (
          <div className="space-y-1">
            <div className="flex items-center justify-between text-xs text-muted-foreground">
              <span>Progress</span>
              <span>{assignment.completedTalks} of {assignment.totalTalks} talks</span>
            </div>
            <Progress value={assignment.progressPercent} className="h-2" />
          </div>
        ) : (
          <div className="flex items-center gap-2 text-sm text-muted-foreground">
            <CheckCircle2Icon className="h-4 w-4 text-green-600" />
            <span>All {assignment.totalTalks} talks completed</span>
          </div>
        )}
      </CardContent>
      <CardFooter className="pt-2">
        <Button
          onClick={() => onAction(assignment.id)}
          className="w-full"
          variant={action.variant}
        >
          <ActionIcon className="h-4 w-4 mr-2" />
          {action.text}
        </Button>
      </CardFooter>
    </Card>
  );
}

function CourseCardSkeleton() {
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
        <div className="h-4 w-1/2 bg-muted rounded" />
        <div className="space-y-1">
          <div className="flex justify-between">
            <div className="h-3 w-16 bg-muted rounded" />
            <div className="h-3 w-24 bg-muted rounded" />
          </div>
          <div className="h-2 w-full bg-muted rounded" />
        </div>
      </CardContent>
      <CardFooter className="pt-2">
        <div className="h-9 w-full bg-muted rounded" />
      </CardFooter>
    </Card>
  );
}

export function MyCoursesList() {
  const router = useRouter();
  const { data: assignments, isLoading, error } = useMyCourseAssignments();

  const handleAction = (assignmentId: string) => {
    router.push(`/toolbox-talks/courses/${assignmentId}`);
  };

  if (error) {
    return (
      <div className="rounded-lg border border-destructive/50 bg-destructive/10 p-4">
        <p className="text-destructive">
          Error loading courses: {error instanceof Error ? error.message : 'Unknown error'}
        </p>
      </div>
    );
  }

  if (isLoading) {
    return (
      <div className="space-y-4">
        <div className="flex items-center gap-2">
          <BookOpenIcon className="h-5 w-5 text-muted-foreground" />
          <h2 className="text-lg font-semibold">My Courses</h2>
        </div>
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {Array.from({ length: 3 }).map((_, i) => (
            <CourseCardSkeleton key={i} />
          ))}
        </div>
      </div>
    );
  }

  if (!assignments || assignments.length === 0) {
    return null; // Don't show section if no courses assigned
  }

  // Sort: active assignments first (InProgress, Overdue, Assigned), then completed
  const sortedAssignments = [...assignments].sort((a, b) => {
    const statusOrder: Record<string, number> = {
      Overdue: 0,
      InProgress: 1,
      Assigned: 2,
      Completed: 3,
    };
    const aOrder = statusOrder[getEffectiveStatus(a)] ?? 4;
    const bOrder = statusOrder[getEffectiveStatus(b)] ?? 4;
    if (aOrder !== bOrder) return aOrder - bOrder;
    // Within same status, sort by due date
    if (a.dueDate && b.dueDate) {
      return new Date(a.dueDate).getTime() - new Date(b.dueDate).getTime();
    }
    return 0;
  });

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <BookOpenIcon className="h-5 w-5 text-muted-foreground" />
          <h2 className="text-lg font-semibold">My Courses</h2>
        </div>
        <span className="text-sm text-muted-foreground">
          {assignments.length} course{assignments.length !== 1 ? 's' : ''}
        </span>
      </div>
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {sortedAssignments.map((assignment) => (
          <CourseCard
            key={assignment.id}
            assignment={assignment}
            onAction={handleAction}
          />
        ))}
      </div>
    </div>
  );
}
