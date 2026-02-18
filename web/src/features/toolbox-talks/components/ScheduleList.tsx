'use client';

import { useState, useEffect, useCallback } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { format } from 'date-fns';
import {
  EyeIcon,
  PencilIcon,
  XCircleIcon,
  PlayCircleIcon,
  CalendarIcon,
  UsersIcon,
  RefreshCwIcon,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { DataTable, type Column } from '@/components/shared/data-table';
import {
  useToolboxTalkSchedules,
  useCancelToolboxTalkSchedule,
  useProcessToolboxTalkSchedule,
} from '@/lib/api/toolbox-talks';
import type {
  ToolboxTalkScheduleListItem,
  ToolboxTalkScheduleStatus,
} from '@/types/toolbox-talks';
import { usePermission } from '@/lib/auth/use-auth';
import { toast } from 'sonner';
import { cn } from '@/lib/utils';

interface ScheduleListProps {
  toolboxTalkId?: string;
  onEdit?: (schedule: ToolboxTalkScheduleListItem) => void;
  /** Base path for navigation (default: /admin/toolbox-talks) */
  basePath?: string;
}

const STATUS_OPTIONS: { value: ToolboxTalkScheduleStatus | 'all'; label: string }[] = [
  { value: 'all', label: 'All Status' },
  { value: 'Draft', label: 'Draft' },
  { value: 'Active', label: 'Active' },
  { value: 'Completed', label: 'Completed' },
  { value: 'Cancelled', label: 'Cancelled' },
];

const getStatusBadgeVariant = (status: ToolboxTalkScheduleStatus) => {
  switch (status) {
    case 'Active':
      return 'bg-green-100 text-green-800 hover:bg-green-100 dark:bg-green-900/20 dark:text-green-400';
    case 'Draft':
      return 'bg-blue-100 text-blue-800 hover:bg-blue-100 dark:bg-blue-900/20 dark:text-blue-400';
    case 'Completed':
      return 'bg-gray-100 text-gray-800 hover:bg-gray-100 dark:bg-gray-900/20 dark:text-gray-400';
    case 'Cancelled':
      return 'bg-red-100 text-red-800 hover:bg-red-100 dark:bg-red-900/20 dark:text-red-400';
    default:
      return '';
  }
};

export function ScheduleList({ toolboxTalkId, onEdit, basePath = '/admin/toolbox-talks' }: ScheduleListProps) {
  const router = useRouter();
  const searchParams = useSearchParams();
  const canSchedule = usePermission('Learnings.Schedule');

  // URL params state
  const page = Number(searchParams.get('schedulePage')) || 1;
  const pageSize = Number(searchParams.get('scheduleSize')) || 20;
  const statusFilter = (searchParams.get('scheduleStatus') as ToolboxTalkScheduleStatus) || undefined;

  // Dialog state
  const [cancelDialogOpen, setCancelDialogOpen] = useState(false);
  const [processDialogOpen, setProcessDialogOpen] = useState(false);
  const [selectedSchedule, setSelectedSchedule] = useState<ToolboxTalkScheduleListItem | null>(null);

  // Fetch data
  const { data, isLoading, error } = useToolboxTalkSchedules({
    toolboxTalkId,
    status: statusFilter,
    pageNumber: page,
    pageSize,
  });

  const cancelMutation = useCancelToolboxTalkSchedule();
  const processMutation = useProcessToolboxTalkSchedule();

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
      if (!updates.hasOwnProperty('schedulePage')) {
        params.set('schedulePage', '1');
      }
      router.push(`?${params.toString()}`);
    },
    [router, searchParams]
  );

  // Handle cancel schedule
  const handleCancel = async () => {
    if (!selectedSchedule) return;

    try {
      await cancelMutation.mutateAsync(selectedSchedule.id);
      toast.success('Schedule cancelled successfully');
      setCancelDialogOpen(false);
      setSelectedSchedule(null);
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Failed to cancel schedule';
      toast.error('Error', { description: message });
    }
  };

  // Handle process schedule
  const handleProcess = async () => {
    if (!selectedSchedule) return;

    try {
      const result = await processMutation.mutateAsync(selectedSchedule.id);
      toast.success('Schedule processed', {
        description: `Created ${result.talksCreated} assignment(s)`,
      });
      setProcessDialogOpen(false);
      setSelectedSchedule(null);
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Failed to process schedule';
      toast.error('Error', { description: message });
    }
  };

  const columns: Column<ToolboxTalkScheduleListItem>[] = [
    {
      key: 'toolboxTalkTitle',
      header: 'Talk',
      sortable: true,
      render: (item) => (
        <div className="flex flex-col">
          <span className="font-medium">{item.toolboxTalkTitle}</span>
          <span className="text-xs text-muted-foreground">
            {item.frequencyDisplay}
          </span>
        </div>
      ),
    },
    {
      key: 'scheduledDate',
      header: 'Start Date',
      sortable: true,
      render: (item) => (
        <div className="flex items-center gap-2">
          <CalendarIcon className="h-4 w-4 text-muted-foreground" />
          <span>{format(new Date(item.scheduledDate), 'dd MMM yyyy')}</span>
        </div>
      ),
    },
    {
      key: 'endDate',
      header: 'End Date',
      render: (item) => (
        <span className="text-muted-foreground">
          {item.endDate ? format(new Date(item.endDate), 'dd MMM yyyy') : '-'}
        </span>
      ),
    },
    {
      key: 'frequency',
      header: 'Frequency',
      render: (item) => (
        <Badge variant="outline">{item.frequencyDisplay}</Badge>
      ),
    },
    {
      key: 'status',
      header: 'Status',
      render: (item) => (
        <Badge className={cn('font-medium', getStatusBadgeVariant(item.status))}>
          {item.statusDisplay}
        </Badge>
      ),
    },
    {
      key: 'assignmentCount',
      header: 'Assignments',
      render: (item) => (
        <div className="flex items-center gap-2">
          <UsersIcon className="h-4 w-4 text-muted-foreground" />
          <span>
            {item.processedCount}/{item.assignmentCount}
            {item.assignToAllEmployees && (
              <span className="text-xs text-muted-foreground ml-1">(all)</span>
            )}
          </span>
        </div>
      ),
    },
    {
      key: 'nextRunDate',
      header: 'Next Run',
      render: (item) => (
        <span className="text-muted-foreground text-sm">
          {item.nextRunDate
            ? format(new Date(item.nextRunDate), 'dd MMM yyyy')
            : '-'}
        </span>
      ),
    },
    {
      key: 'actions',
      header: '',
      className: 'w-[100px]',
      render: (item) => (
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="ghost" size="sm">
              Actions
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            <DropdownMenuItem
              onClick={() => router.push(`${basePath}/schedules/${item.id}`)}
            >
              <EyeIcon className="mr-2 h-4 w-4" />
              View
            </DropdownMenuItem>
            {canSchedule && item.status === 'Draft' && (
              <DropdownMenuItem onClick={() => onEdit?.(item)}>
                <PencilIcon className="mr-2 h-4 w-4" />
                Edit
              </DropdownMenuItem>
            )}
            {canSchedule && (item.status === 'Draft' || item.status === 'Active') && (
              <>
                <DropdownMenuSeparator />
                <DropdownMenuItem
                  onClick={() => {
                    setSelectedSchedule(item);
                    setProcessDialogOpen(true);
                  }}
                >
                  <PlayCircleIcon className="mr-2 h-4 w-4" />
                  Process Now
                </DropdownMenuItem>
              </>
            )}
            {canSchedule && item.status !== 'Cancelled' && item.status !== 'Completed' && (
              <>
                <DropdownMenuSeparator />
                <DropdownMenuItem
                  className="text-destructive focus:text-destructive"
                  onClick={() => {
                    setSelectedSchedule(item);
                    setCancelDialogOpen(true);
                  }}
                >
                  <XCircleIcon className="mr-2 h-4 w-4" />
                  Cancel
                </DropdownMenuItem>
              </>
            )}
          </DropdownMenuContent>
        </DropdownMenu>
      ),
    },
  ];

  if (error) {
    return (
      <div className="rounded-lg border border-destructive/50 bg-destructive/10 p-4">
        <p className="text-destructive">
          Error loading schedules: {error instanceof Error ? error.message : 'Unknown error'}
        </p>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {/* Filters */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex items-center gap-4">
          <Select
            value={statusFilter || 'all'}
            onValueChange={(value) =>
              updateParams({ scheduleStatus: value === 'all' ? null : value })
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
        </div>
      </div>

      {/* Data table */}
      <DataTable
        columns={columns}
        data={data?.items || []}
        isLoading={isLoading}
        emptyMessage="No schedules found"
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
        onPageChange={(newPage) => updateParams({ schedulePage: String(newPage) })}
        onPageSizeChange={(newSize) =>
          updateParams({ scheduleSize: String(newSize), schedulePage: '1' })
        }
      />

      {/* Cancel confirmation dialog */}
      <AlertDialog open={cancelDialogOpen} onOpenChange={setCancelDialogOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Cancel Schedule</AlertDialogTitle>
            <AlertDialogDescription>
              Are you sure you want to cancel this schedule? This will stop any future
              assignments from being created.
              {selectedSchedule && (
                <span className="block mt-2 font-medium text-foreground">
                  {selectedSchedule.toolboxTalkTitle}
                </span>
              )}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Keep Schedule</AlertDialogCancel>
            <AlertDialogAction
              onClick={handleCancel}
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
            >
              {cancelMutation.isPending ? (
                <>
                  <LoadingSpinner className="mr-2 h-4 w-4" />
                  Cancelling...
                </>
              ) : (
                'Cancel Schedule'
              )}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* Process confirmation dialog */}
      <AlertDialog open={processDialogOpen} onOpenChange={setProcessDialogOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Process Schedule Now</AlertDialogTitle>
            <AlertDialogDescription>
              This will immediately create assignments for all employees based on the
              schedule settings.
              {selectedSchedule && (
                <span className="block mt-2 font-medium text-foreground">
                  {selectedSchedule.toolboxTalkTitle} - {selectedSchedule.assignmentCount}{' '}
                  employee(s)
                </span>
              )}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction onClick={handleProcess}>
              {processMutation.isPending ? (
                <>
                  <LoadingSpinner className="mr-2 h-4 w-4" />
                  Processing...
                </>
              ) : (
                <>
                  <RefreshCwIcon className="mr-2 h-4 w-4" />
                  Process Now
                </>
              )}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}

function LoadingSpinner({ className }: { className?: string }) {
  return (
    <svg
      className={`animate-spin ${className}`}
      xmlns="http://www.w3.org/2000/svg"
      fill="none"
      viewBox="0 0 24 24"
    >
      <circle
        className="opacity-25"
        cx="12"
        cy="12"
        r="10"
        stroke="currentColor"
        strokeWidth="4"
      />
      <path
        className="opacity-75"
        fill="currentColor"
        d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
      />
    </svg>
  );
}
