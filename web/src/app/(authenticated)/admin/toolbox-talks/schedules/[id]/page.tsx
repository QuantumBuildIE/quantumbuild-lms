'use client';

import { use } from 'react';
import { useRouter } from 'next/navigation';
import { format } from 'date-fns';
import {
  ChevronLeft,
  CalendarIcon,
  UsersIcon,
  ClockIcon,
  PlayCircleIcon,
  XCircleIcon,
  PencilIcon,
  CheckCircle2,
  Circle,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '@/components/ui/alert-dialog';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import {
  useToolboxTalkSchedule,
  useCancelToolboxTalkSchedule,
  useProcessToolboxTalkSchedule,
} from '@/lib/api/toolbox-talks';
import { toast } from 'sonner';
import { cn } from '@/lib/utils';
import { useState } from 'react';
import { ScheduleDialog } from '@/features/toolbox-talks/components/ScheduleDialog';
import type { ToolboxTalkScheduleStatus } from '@/types/toolbox-talks';

interface PageProps {
  params: Promise<{ id: string }>;
}

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

export default function ScheduleDetailPage({ params }: PageProps) {
  const { id } = use(params);
  const router = useRouter();
  const [editDialogOpen, setEditDialogOpen] = useState(false);

  const { data: schedule, isLoading, error } = useToolboxTalkSchedule(id);
  const cancelMutation = useCancelToolboxTalkSchedule();
  const processMutation = useProcessToolboxTalkSchedule();

  const handleCancel = async () => {
    try {
      await cancelMutation.mutateAsync(id);
      toast.success('Schedule cancelled successfully');
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Failed to cancel schedule';
      toast.error('Error', { description: message });
    }
  };

  const handleProcess = async () => {
    try {
      const result = await processMutation.mutateAsync(id);
      toast.success('Schedule processed', {
        description: result.message || `Created ${result.talksCreated} assignment(s)`,
      });
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Failed to process schedule';
      toast.error('Error', { description: message });
    }
  };

  if (isLoading) {
    return (
      <div className="space-y-6">
        <div className="flex items-center gap-4">
          <Skeleton className="h-10 w-10" />
          <div className="space-y-2">
            <Skeleton className="h-6 w-48" />
            <Skeleton className="h-4 w-32" />
          </div>
        </div>
        <div className="grid gap-6 md:grid-cols-2">
          <Skeleton className="h-48" />
          <Skeleton className="h-48" />
        </div>
        <Skeleton className="h-64" />
      </div>
    );
  }

  if (error || !schedule) {
    return (
      <div className="space-y-6">
        <Button variant="ghost" onClick={() => router.back()}>
          <ChevronLeft className="mr-2 h-4 w-4" />
          Back
        </Button>
        <div className="rounded-lg border border-destructive/50 bg-destructive/10 p-6">
          <p className="text-destructive">
            {error instanceof Error ? error.message : 'Schedule not found'}
          </p>
        </div>
      </div>
    );
  }

  const canProcess = schedule.status === 'Draft' || schedule.status === 'Active';
  const canCancel = schedule.status !== 'Cancelled' && schedule.status !== 'Completed';
  const canEdit = schedule.status === 'Draft';

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-start justify-between">
        <div className="flex items-center gap-4">
          <Button variant="ghost" size="icon" onClick={() => router.back()}>
            <ChevronLeft className="h-4 w-4" />
          </Button>
          <div>
            <div className="flex items-center gap-3">
              <h1 className="text-2xl font-semibold tracking-tight">
                {schedule.toolboxTalkTitle}
              </h1>
              <Badge className={cn('font-medium', getStatusBadgeVariant(schedule.status))}>
                {schedule.statusDisplay}
              </Badge>
            </div>
            <p className="text-muted-foreground">
              {schedule.frequencyDisplay} schedule
            </p>
          </div>
        </div>

        {/* Actions */}
        <div className="flex items-center gap-2">
          {canEdit && (
            <Button variant="outline" onClick={() => setEditDialogOpen(true)}>
              <PencilIcon className="mr-2 h-4 w-4" />
              Edit
            </Button>
          )}
          {canProcess && (
            <AlertDialog>
              <AlertDialogTrigger asChild>
                <Button variant="outline">
                  <PlayCircleIcon className="mr-2 h-4 w-4" />
                  Process Now
                </Button>
              </AlertDialogTrigger>
              <AlertDialogContent>
                <AlertDialogHeader>
                  <AlertDialogTitle>Process Schedule Now</AlertDialogTitle>
                  <AlertDialogDescription>
                    This will immediately create assignments for all employees based on the
                    schedule settings. {schedule.assignmentCount} employee(s) will be assigned.
                  </AlertDialogDescription>
                </AlertDialogHeader>
                <AlertDialogFooter>
                  <AlertDialogCancel>Cancel</AlertDialogCancel>
                  <AlertDialogAction onClick={handleProcess} disabled={processMutation.isPending}>
                    {processMutation.isPending ? 'Processing...' : 'Process Now'}
                  </AlertDialogAction>
                </AlertDialogFooter>
              </AlertDialogContent>
            </AlertDialog>
          )}
          {canCancel && (
            <AlertDialog>
              <AlertDialogTrigger asChild>
                <Button variant="destructive">
                  <XCircleIcon className="mr-2 h-4 w-4" />
                  Cancel Schedule
                </Button>
              </AlertDialogTrigger>
              <AlertDialogContent>
                <AlertDialogHeader>
                  <AlertDialogTitle>Cancel Schedule</AlertDialogTitle>
                  <AlertDialogDescription>
                    Are you sure you want to cancel this schedule? This will stop any future
                    assignments from being created.
                  </AlertDialogDescription>
                </AlertDialogHeader>
                <AlertDialogFooter>
                  <AlertDialogCancel>Keep Schedule</AlertDialogCancel>
                  <AlertDialogAction
                    onClick={handleCancel}
                    className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
                    disabled={cancelMutation.isPending}
                  >
                    {cancelMutation.isPending ? 'Cancelling...' : 'Cancel Schedule'}
                  </AlertDialogAction>
                </AlertDialogFooter>
              </AlertDialogContent>
            </AlertDialog>
          )}
        </div>
      </div>

      {/* Info Cards */}
      <div className="grid gap-6 md:grid-cols-2 lg:grid-cols-4">
        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Start Date</CardTitle>
            <CalendarIcon className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">
              {format(new Date(schedule.scheduledDate), 'dd MMM yyyy')}
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">End Date</CardTitle>
            <CalendarIcon className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">
              {schedule.endDate
                ? format(new Date(schedule.endDate), 'dd MMM yyyy')
                : 'No end date'}
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Assignments</CardTitle>
            <UsersIcon className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">
              {schedule.processedCount} / {schedule.assignmentCount}
            </div>
            <p className="text-xs text-muted-foreground">
              {schedule.assignToAllEmployees ? 'All employees' : 'Selected employees'}
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Next Run</CardTitle>
            <ClockIcon className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">
              {schedule.nextRunDate
                ? format(new Date(schedule.nextRunDate), 'dd MMM yyyy')
                : 'N/A'}
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Notes */}
      {schedule.notes && (
        <Card>
          <CardHeader>
            <CardTitle>Notes</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-muted-foreground whitespace-pre-wrap">{schedule.notes}</p>
          </CardContent>
        </Card>
      )}

      {/* Assignments Table */}
      <Card>
        <CardHeader>
          <CardTitle>Employee Assignments</CardTitle>
          <CardDescription>
            List of employees assigned to this schedule
          </CardDescription>
        </CardHeader>
        <CardContent>
          {schedule.assignments && schedule.assignments.length > 0 ? (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Employee</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Processed At</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {schedule.assignments.map((assignment) => (
                  <TableRow key={assignment.id}>
                    <TableCell className="font-medium">
                      {assignment.employeeName || assignment.employeeId}
                    </TableCell>
                    <TableCell>
                      {assignment.isProcessed ? (
                        <div className="flex items-center gap-2 text-green-600">
                          <CheckCircle2 className="h-4 w-4" />
                          <span>Processed</span>
                        </div>
                      ) : (
                        <div className="flex items-center gap-2 text-muted-foreground">
                          <Circle className="h-4 w-4" />
                          <span>Pending</span>
                        </div>
                      )}
                    </TableCell>
                    <TableCell className="text-muted-foreground">
                      {assignment.processedAt
                        ? format(new Date(assignment.processedAt), 'dd MMM yyyy HH:mm')
                        : '-'}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          ) : (
            <div className="text-center py-8 text-muted-foreground">
              No assignments yet
            </div>
          )}
        </CardContent>
      </Card>

      {/* Edit Dialog */}
      <ScheduleDialog
        open={editDialogOpen}
        onOpenChange={setEditDialogOpen}
        scheduleId={id}
        onSuccess={() => setEditDialogOpen(false)}
      />
    </div>
  );
}
