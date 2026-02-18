'use client';

import { useState, useEffect, useCallback } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { format, formatDistanceToNow } from 'date-fns';
import {
  BellIcon,
  UserIcon,
  CalendarIcon,
  ClockIcon,
  CheckCircle2Icon,
  AlertTriangleIcon,
  PlayCircleIcon,
  SearchIcon,
  XCircleIcon,
} from 'lucide-react';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Progress } from '@/components/ui/progress';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/tooltip';
import { DataTable, type Column } from '@/components/shared/data-table';
import { DeleteConfirmationDialog } from '@/components/shared/delete-confirmation-dialog';
import { useScheduledTalks, useSendReminder, useCancelScheduledTalk } from '@/lib/api/toolbox-talks';
import { useToolboxTalks } from '@/lib/api/toolbox-talks';
import { useAllEmployees } from '@/lib/api/admin/use-employees';
import type { ScheduledTalkListItem, ScheduledTalkStatus } from '@/types/toolbox-talks';
import { usePermission } from '@/lib/auth/use-auth';
import { toast } from 'sonner';
import { cn } from '@/lib/utils';

interface AssignmentsListProps {
  toolboxTalkId?: string;
  employeeId?: string;
  scheduleId?: string;
}

const STATUS_OPTIONS: { value: ScheduledTalkStatus | 'all'; label: string }[] = [
  { value: 'all', label: 'All Status' },
  { value: 'Pending', label: 'Pending' },
  { value: 'InProgress', label: 'In Progress' },
  { value: 'Completed', label: 'Completed' },
  { value: 'Overdue', label: 'Overdue' },
  { value: 'Cancelled', label: 'Cancelled' },
];

const getStatusBadgeVariant = (status: ScheduledTalkStatus) => {
  switch (status) {
    case 'Completed':
      return 'bg-green-100 text-green-800 hover:bg-green-100 dark:bg-green-900/20 dark:text-green-400';
    case 'Pending':
      return 'bg-blue-100 text-blue-800 hover:bg-blue-100 dark:bg-blue-900/20 dark:text-blue-400';
    case 'InProgress':
      return 'bg-yellow-100 text-yellow-800 hover:bg-yellow-100 dark:bg-yellow-900/20 dark:text-yellow-400';
    case 'Overdue':
      return 'bg-red-100 text-red-800 hover:bg-red-100 dark:bg-red-900/20 dark:text-red-400';
    case 'Cancelled':
      return 'bg-gray-100 text-gray-800 hover:bg-gray-100 dark:bg-gray-900/20 dark:text-gray-400';
    default:
      return '';
  }
};

const getStatusIcon = (status: ScheduledTalkStatus) => {
  switch (status) {
    case 'Completed':
      return <CheckCircle2Icon className="h-4 w-4 text-green-600" />;
    case 'Pending':
      return <ClockIcon className="h-4 w-4 text-blue-600" />;
    case 'InProgress':
      return <PlayCircleIcon className="h-4 w-4 text-yellow-600" />;
    case 'Overdue':
      return <AlertTriangleIcon className="h-4 w-4 text-red-600" />;
    default:
      return null;
  }
};

export function AssignmentsList({
  toolboxTalkId,
  employeeId,
  scheduleId,
}: AssignmentsListProps) {
  const router = useRouter();
  const canSchedule = usePermission('Learnings.Schedule');
  const searchParams = useSearchParams();

  // URL params state
  const page = Number(searchParams.get('assignPage')) || 1;
  const pageSize = Number(searchParams.get('assignSize')) || 20;
  const statusFilter = (searchParams.get('assignStatus') as ScheduledTalkStatus) || undefined;
  const talkFilter = searchParams.get('talkId') || toolboxTalkId;
  const empFilter = searchParams.get('empId') || employeeId;

  // Fetch data
  const { data, isLoading, error } = useScheduledTalks({
    toolboxTalkId: talkFilter,
    employeeId: empFilter,
    scheduleId,
    status: statusFilter,
    pageNumber: page,
    pageSize,
  });

  // Fetch talks and employees for filters
  const { data: talksData } = useToolboxTalks({ pageSize: 100 });
  const { data: employees } = useAllEmployees();

  const reminderMutation = useSendReminder();
  const cancelMutation = useCancelScheduledTalk();

  // State for cancel dialog
  const [cancelDialogOpen, setCancelDialogOpen] = useState(false);
  const [assignmentToCancel, setAssignmentToCancel] = useState<ScheduledTalkListItem | null>(null);

  // Update URL params
  const updateParams = useCallback(
    (updates: Record<string, string | null>) => {
      const params = new URLSearchParams(searchParams.toString());
      Object.entries(updates).forEach(([key, value]) => {
        if (value === null || value === '' || value === 'all') {
          params.delete(key);
        } else {
          params.set(key, value);
        }
      });
      // Reset to page 1 on filter change (except for page changes)
      if (!updates.hasOwnProperty('assignPage')) {
        params.set('assignPage', '1');
      }
      router.push(`?${params.toString()}`);
    },
    [router, searchParams]
  );

  // Handle send reminder
  const handleSendReminder = async (assignment: ScheduledTalkListItem) => {
    try {
      await reminderMutation.mutateAsync(assignment.id);
      toast.success('Reminder sent', {
        description: `Reminder sent to ${assignment.employeeName}`,
      });
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Failed to send reminder';
      toast.error('Error', { description: message });
    }
  };

  // Handle cancel assignment
  const handleCancel = async () => {
    if (!assignmentToCancel) return;

    try {
      await cancelMutation.mutateAsync(assignmentToCancel.id);
      toast.success('Assignment cancelled', {
        description: `Assignment for ${assignmentToCancel.employeeName} has been cancelled`,
      });
      setCancelDialogOpen(false);
      setAssignmentToCancel(null);
    } catch (error: unknown) {
      let message = 'Failed to cancel assignment';
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

  const columns: Column<ScheduledTalkListItem>[] = [
    {
      key: 'employeeName',
      header: 'Employee',
      sortable: true,
      render: (item) => (
        <div className="flex items-center gap-2">
          <div className="h-8 w-8 rounded-full bg-muted flex items-center justify-center">
            <UserIcon className="h-4 w-4 text-muted-foreground" />
          </div>
          <div className="flex flex-col">
            <span className="font-medium">{item.employeeName}</span>
            {item.employeeEmail && (
              <span className="text-xs text-muted-foreground">{item.employeeEmail}</span>
            )}
          </div>
        </div>
      ),
    },
    {
      key: 'toolboxTalkTitle',
      header: 'Talk',
      sortable: true,
      render: (item) => (
        <span className="font-medium">{item.toolboxTalkTitle}</span>
      ),
    },
    {
      key: 'dueDate',
      header: 'Due Date',
      sortable: true,
      render: (item) => {
        const dueDate = new Date(item.dueDate);
        const isOverdue = item.status === 'Overdue';

        return (
          <div className="flex flex-col">
            <div className="flex items-center gap-2">
              <CalendarIcon className={cn('h-4 w-4', isOverdue ? 'text-destructive' : 'text-muted-foreground')} />
              <span className={cn(isOverdue && 'text-destructive font-medium')}>
                {format(dueDate, 'dd MMM yyyy')}
              </span>
            </div>
            {isOverdue && (
              <span className="text-xs text-destructive">
                {formatDistanceToNow(dueDate, { addSuffix: true })}
              </span>
            )}
          </div>
        );
      },
    },
    {
      key: 'status',
      header: 'Status',
      render: (item) => (
        <div className="flex items-center gap-2">
          {getStatusIcon(item.status)}
          <Badge className={cn('font-medium', getStatusBadgeVariant(item.status))}>
            {item.statusDisplay}
          </Badge>
        </div>
      ),
    },
    {
      key: 'progress',
      header: 'Progress',
      render: (item) => (
        <TooltipProvider>
          <Tooltip>
            <TooltipTrigger asChild>
              <div className="w-[100px]">
                <div className="flex items-center justify-between text-xs mb-1">
                  <span>{item.completedSections}/{item.totalSections}</span>
                  <span>{item.progressPercent}%</span>
                </div>
                <Progress value={item.progressPercent} className="h-2" />
              </div>
            </TooltipTrigger>
            <TooltipContent>
              <p>{item.completedSections} of {item.totalSections} sections completed</p>
            </TooltipContent>
          </Tooltip>
        </TooltipProvider>
      ),
    },
    {
      key: 'remindersSent',
      header: 'Reminders',
      className: 'text-center',
      headerClassName: 'text-center',
      render: (item) => (
        <TooltipProvider>
          <Tooltip>
            <TooltipTrigger asChild>
              <div className="flex items-center justify-center gap-1">
                <BellIcon className="h-4 w-4 text-muted-foreground" />
                <span>{item.remindersSent}</span>
              </div>
            </TooltipTrigger>
            <TooltipContent>
              <p>{item.remindersSent} reminder(s) sent</p>
            </TooltipContent>
          </Tooltip>
        </TooltipProvider>
      ),
    },
    {
      key: 'actions',
      header: '',
      className: 'w-[80px]',
      render: (item) => {
        if (!canSchedule) return null;
        const isActionable = item.status === 'Pending' || item.status === 'InProgress' || item.status === 'Overdue';

        return (
          <div className="flex items-center gap-1">
            <TooltipProvider>
              <Tooltip>
                <TooltipTrigger asChild>
                  <button
                    type="button"
                    disabled={!isActionable || reminderMutation.isPending}
                    onClick={() => isActionable && handleSendReminder(item)}
                    className={cn(
                      'inline-flex h-8 w-8 items-center justify-center rounded-md transition-colors',
                      isActionable
                        ? 'text-muted-foreground hover:text-foreground hover:bg-muted cursor-pointer'
                        : 'text-muted-foreground/40 cursor-not-allowed'
                    )}
                  >
                    <BellIcon className="h-4 w-4" />
                  </button>
                </TooltipTrigger>
                <TooltipContent>
                  <p>Send Reminder</p>
                </TooltipContent>
              </Tooltip>
            </TooltipProvider>

            <TooltipProvider>
              <Tooltip>
                <TooltipTrigger asChild>
                  <button
                    type="button"
                    disabled={!isActionable || cancelMutation.isPending}
                    onClick={() => {
                      if (isActionable) {
                        setAssignmentToCancel(item);
                        setCancelDialogOpen(true);
                      }
                    }}
                    className={cn(
                      'inline-flex h-8 w-8 items-center justify-center rounded-md transition-colors',
                      isActionable
                        ? 'text-muted-foreground hover:text-destructive hover:bg-destructive/10 cursor-pointer'
                        : 'text-muted-foreground/40 cursor-not-allowed'
                    )}
                  >
                    <XCircleIcon className="h-4 w-4" />
                  </button>
                </TooltipTrigger>
                <TooltipContent>
                  <p>Cancel Assignment</p>
                </TooltipContent>
              </Tooltip>
            </TooltipProvider>
          </div>
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
      {/* Filters */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:flex-wrap">
        {/* Status filter */}
        <Select
          value={statusFilter || 'all'}
          onValueChange={(value) =>
            updateParams({ assignStatus: value === 'all' ? null : value })
          }
        >
          <SelectTrigger className="w-[160px]">
            <SelectValue placeholder="Filter by status" />
          </SelectTrigger>
          <SelectContent>
            {STATUS_OPTIONS.map((option) => (
              <SelectItem key={option.value} value={option.value}>
                {option.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        {/* Talk filter (if not already filtered by prop) */}
        {!toolboxTalkId && (
          <Select
            value={talkFilter || 'all'}
            onValueChange={(value) =>
              updateParams({ talkId: value === 'all' ? null : value })
            }
          >
            <SelectTrigger className="w-[200px]">
              <SelectValue placeholder="Filter by learning" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All Learnings</SelectItem>
              {talksData?.items.map((talk) => (
                <SelectItem key={talk.id} value={talk.id}>
                  {talk.title}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        )}

        {/* Employee filter (if not already filtered by prop) */}
        {!employeeId && (
          <Select
            value={empFilter || 'all'}
            onValueChange={(value) =>
              updateParams({ empId: value === 'all' ? null : value })
            }
          >
            <SelectTrigger className="w-[200px]">
              <SelectValue placeholder="Filter by employee" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All Employees</SelectItem>
              {employees?.map((emp) => (
                <SelectItem key={emp.id} value={emp.id}>
                  {emp.firstName} {emp.lastName}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        )}
      </div>

      {/* Data table */}
      <DataTable
        columns={columns}
        data={data?.items || []}
        isLoading={isLoading}
        emptyMessage="No assignments found"
        keyExtractor={(item) => item.id}
        pagination={
          data
            ? {
                pageNumber: data.pageNumber,
                pageSize: data.pageSize,
                totalCount: data.totalCount,
                totalPages: data.totalPages,
              }
            : undefined
        }
        onPageChange={(newPage) => updateParams({ assignPage: String(newPage) })}
        onPageSizeChange={(newSize) =>
          updateParams({ assignSize: String(newSize), assignPage: '1' })
        }
      />

      {/* Cancel confirmation dialog */}
      <DeleteConfirmationDialog
        open={cancelDialogOpen}
        onOpenChange={setCancelDialogOpen}
        title="Cancel Assignment"
        description={`Are you sure you want to cancel the assignment for ${assignmentToCancel?.employeeName}? This will mark the assignment as cancelled.`}
        onConfirm={handleCancel}
        isLoading={cancelMutation.isPending}
      />
    </div>
  );
}
