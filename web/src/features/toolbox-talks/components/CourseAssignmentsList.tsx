'use client';

import { useState } from 'react';
import { format } from 'date-fns';
import {
  UserIcon,
  CalendarIcon,
  CheckCircle2Icon,
  ClockIcon,
  PlayCircleIcon,
  AlertTriangleIcon,
  TrashIcon,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Progress } from '@/components/ui/progress';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/tooltip';
import { DataTable, type Column } from '@/components/shared/data-table';
import { DeleteConfirmationDialog } from '@/components/shared/delete-confirmation-dialog';
import {
  useCourseAssignments,
  useDeleteCourseAssignment,
} from '@/lib/api/toolbox-talks/use-course-assignments';
import type { CourseAssignmentListDto } from '@/lib/api/toolbox-talks/course-assignments';
import { toast } from 'sonner';
import { cn } from '@/lib/utils';

type CourseAssignmentStatus = 'Assigned' | 'InProgress' | 'Completed' | 'Overdue';

const getStatusBadgeVariant = (status: string) => {
  switch (status) {
    case 'Completed':
      return 'bg-green-100 text-green-800 hover:bg-green-100 dark:bg-green-900/20 dark:text-green-400';
    case 'Assigned':
      return 'bg-blue-100 text-blue-800 hover:bg-blue-100 dark:bg-blue-900/20 dark:text-blue-400';
    case 'InProgress':
      return 'bg-yellow-100 text-yellow-800 hover:bg-yellow-100 dark:bg-yellow-900/20 dark:text-yellow-400';
    case 'Overdue':
      return 'bg-red-100 text-red-800 hover:bg-red-100 dark:bg-red-900/20 dark:text-red-400';
    default:
      return '';
  }
};

const getStatusIcon = (status: string) => {
  switch (status) {
    case 'Completed':
      return <CheckCircle2Icon className="h-4 w-4 text-green-600" />;
    case 'Assigned':
      return <ClockIcon className="h-4 w-4 text-blue-600" />;
    case 'InProgress':
      return <PlayCircleIcon className="h-4 w-4 text-yellow-600" />;
    case 'Overdue':
      return <AlertTriangleIcon className="h-4 w-4 text-red-600" />;
    default:
      return null;
  }
};

const getStatusLabel = (status: string) => {
  switch (status) {
    case 'InProgress':
      return 'In Progress';
    default:
      return status;
  }
};

interface CourseAssignmentsListProps {
  courseId: string;
}

export function CourseAssignmentsList({ courseId }: CourseAssignmentsListProps) {
  const { data: assignments, isLoading, error } = useCourseAssignments(courseId);
  const deleteMutation = useDeleteCourseAssignment();

  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [assignmentToDelete, setAssignmentToDelete] = useState<CourseAssignmentListDto | null>(null);

  const handleDelete = async () => {
    if (!assignmentToDelete) return;

    try {
      await deleteMutation.mutateAsync(assignmentToDelete.id);
      toast.success('Assignment removed', {
        description: `Removed assignment for ${assignmentToDelete.employeeName}`,
      });
      setDeleteDialogOpen(false);
      setAssignmentToDelete(null);
    } catch (error: unknown) {
      let message = 'Failed to remove assignment';
      if (error && typeof error === 'object' && 'response' in error) {
        const axiosError = error as { response?: { data?: { message?: string } } };
        if (axiosError.response?.data?.message) {
          message = axiosError.response.data.message;
        }
      } else if (error instanceof Error) {
        message = error.message;
      }
      toast.error('Error', { description: message });
    }
  };

  const columns: Column<CourseAssignmentListDto>[] = [
    {
      key: 'employeeName',
      header: 'Employee',
      sortable: true,
      render: (item) => (
        <div className="flex items-center gap-2">
          <div className="h-8 w-8 rounded-full bg-muted flex items-center justify-center">
            <UserIcon className="h-4 w-4 text-muted-foreground" />
          </div>
          <span className="font-medium">{item.employeeName}</span>
        </div>
      ),
    },
    {
      key: 'status',
      header: 'Status',
      render: (item) => (
        <div className="flex items-center gap-2">
          {getStatusIcon(item.status)}
          <Badge className={cn('font-medium', getStatusBadgeVariant(item.status))}>
            {getStatusLabel(item.status)}
          </Badge>
        </div>
      ),
    },
    {
      key: 'progress',
      header: 'Progress',
      render: (item) => {
        const percent = item.totalTalks > 0
          ? Math.round((item.completedTalks * 100) / item.totalTalks)
          : 0;
        return (
          <TooltipProvider>
            <Tooltip>
              <TooltipTrigger asChild>
                <div className="w-[120px]">
                  <div className="flex items-center justify-between text-xs mb-1">
                    <span>{item.completedTalks}/{item.totalTalks} talks</span>
                    <span>{percent}%</span>
                  </div>
                  <Progress value={percent} className="h-2" />
                </div>
              </TooltipTrigger>
              <TooltipContent>
                <p>{item.completedTalks} of {item.totalTalks} talks completed</p>
              </TooltipContent>
            </Tooltip>
          </TooltipProvider>
        );
      },
    },
    {
      key: 'dueDate',
      header: 'Due Date',
      sortable: true,
      render: (item) => {
        if (!item.dueDate) return <span className="text-muted-foreground">-</span>;
        const isOverdue = item.status === 'Overdue';
        return (
          <div className="flex items-center gap-2">
            <CalendarIcon className={cn('h-4 w-4', isOverdue ? 'text-destructive' : 'text-muted-foreground')} />
            <span className={cn(isOverdue && 'text-destructive font-medium')}>
              {format(new Date(item.dueDate), 'dd MMM yyyy')}
            </span>
          </div>
        );
      },
    },
    {
      key: 'assignedAt',
      header: 'Assigned',
      sortable: true,
      render: (item) => (
        <span className="text-sm text-muted-foreground">
          {format(new Date(item.assignedAt), 'dd MMM yyyy')}
        </span>
      ),
    },
    {
      key: 'actions',
      header: '',
      className: 'w-[50px]',
      render: (item) => {
        const canDelete = item.status !== 'Completed';
        return (
          <TooltipProvider>
            <Tooltip>
              <TooltipTrigger asChild>
                <button
                  type="button"
                  disabled={!canDelete || deleteMutation.isPending}
                  onClick={() => {
                    if (canDelete) {
                      setAssignmentToDelete(item);
                      setDeleteDialogOpen(true);
                    }
                  }}
                  className={cn(
                    'inline-flex h-8 w-8 items-center justify-center rounded-md transition-colors',
                    canDelete
                      ? 'text-muted-foreground hover:text-destructive hover:bg-destructive/10 cursor-pointer'
                      : 'text-muted-foreground/40 cursor-not-allowed'
                  )}
                >
                  <TrashIcon className="h-4 w-4" />
                </button>
              </TooltipTrigger>
              <TooltipContent>
                <p>{canDelete ? 'Remove Assignment' : 'Cannot remove completed assignment'}</p>
              </TooltipContent>
            </Tooltip>
          </TooltipProvider>
        );
      },
    },
  ];

  if (error) {
    return (
      <div className="rounded-lg border border-destructive/50 bg-destructive/10 p-4">
        <p className="text-destructive">
          Error loading assignments: {error instanceof Error ? error.message : 'Unknown error'}
        </p>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <DataTable
        columns={columns}
        data={assignments ?? []}
        isLoading={isLoading}
        emptyMessage="No employees assigned to this course yet"
        keyExtractor={(item) => item.id}
      />

      <DeleteConfirmationDialog
        open={deleteDialogOpen}
        onOpenChange={setDeleteDialogOpen}
        title="Remove Assignment"
        description={`Are you sure you want to remove the assignment for ${assignmentToDelete?.employeeName}? This will cancel their progress on this course.`}
        onConfirm={handleDelete}
        isLoading={deleteMutation.isPending}
      />
    </div>
  );
}
