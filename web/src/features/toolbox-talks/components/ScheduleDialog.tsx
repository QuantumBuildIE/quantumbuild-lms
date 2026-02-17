'use client';

import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { format } from 'date-fns';
import { CalendarIcon } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Calendar } from '@/components/ui/calendar';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
  FormDescription,
} from '@/components/ui/form';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from '@/components/ui/popover';
import { useToolboxTalks, useCreateToolboxTalkSchedule, useUpdateToolboxTalkSchedule, useToolboxTalkSchedule } from '@/lib/api/toolbox-talks';
import { useAllEmployees } from '@/lib/api/admin/use-employees';
import type {
  ToolboxTalkFrequency,
  ToolboxTalkSchedule,
  ToolboxTalkListItem,
} from '@/types/toolbox-talks';
import { toast } from 'sonner';
import { cn } from '@/lib/utils';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { AlertCircleIcon } from 'lucide-react';

// ============================================
// Schema
// ============================================

const scheduleFormSchema = z.object({
  toolboxTalkId: z.string().min(1, 'Please select a learning'),
  scheduledDate: z.date({ message: 'Scheduled date is required' }),
  endDate: z.date().optional().nullable(),
  frequency: z.enum(['Once', 'Weekly', 'Monthly', 'Annually'] as const),
  assignToAllEmployees: z.boolean(),
  employeeIds: z.array(z.string()).optional(),
  notes: z.string().max(500).optional().nullable(),
});

type ScheduleFormValues = z.infer<typeof scheduleFormSchema>;

// ============================================
// Component
// ============================================

const FREQUENCY_OPTIONS: { value: ToolboxTalkFrequency; label: string }[] = [
  { value: 'Once', label: 'Once' },
  { value: 'Weekly', label: 'Weekly' },
  { value: 'Monthly', label: 'Monthly' },
  { value: 'Annually', label: 'Annually' },
];

interface ScheduleDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Full schedule object (if available) */
  schedule?: ToolboxTalkSchedule;
  /** Schedule ID to fetch (alternative to passing full schedule) */
  scheduleId?: string;
  preselectedTalk?: ToolboxTalkListItem;
  toolboxTalkId?: string;
  onSuccess?: () => void;
  /** When true, renders without Dialog wrapper for embedded use */
  embedded?: boolean;
}

export function ScheduleDialog({
  open,
  onOpenChange,
  schedule: scheduleProp,
  scheduleId,
  preselectedTalk,
  toolboxTalkId,
  onSuccess,
  embedded = false,
}: ScheduleDialogProps) {
  const [selectedEmployees, setSelectedEmployees] = useState<string[]>([]);
  const [employeeSearch, setEmployeeSearch] = useState('');

  // Fetch schedule by ID if not passed directly
  const { data: fetchedSchedule } = useToolboxTalkSchedule(scheduleId ?? '');

  // Use passed schedule or fetched schedule
  const schedule = scheduleProp ?? fetchedSchedule;
  const isEditing = !!schedule;

  // Use toolboxTalkId prop or preselectedTalk id
  const preselectedTalkId = toolboxTalkId ?? preselectedTalk?.id;

  // Check if preselected talk is inactive (defensive check)
  const isPreselectedTalkInactive = preselectedTalk && !preselectedTalk.isActive;

  // Fetch talks for selection
  const { data: talksData } = useToolboxTalks({ isActive: true, pageSize: 100 });
  const talks = talksData?.items || [];

  // Fetch employees for selection
  const { data: employees, isLoading: employeesLoading } = useAllEmployees();

  const createMutation = useCreateToolboxTalkSchedule();
  const updateMutation = useUpdateToolboxTalkSchedule();

  const form = useForm<ScheduleFormValues>({
    resolver: zodResolver(scheduleFormSchema) as any,
    defaultValues: {
      toolboxTalkId: schedule?.toolboxTalkId ?? preselectedTalkId ?? '',
      scheduledDate: schedule ? new Date(schedule.scheduledDate) : new Date(),
      endDate: schedule?.endDate ? new Date(schedule.endDate) : null,
      frequency: schedule?.frequency ?? 'Once',
      assignToAllEmployees: schedule?.assignToAllEmployees ?? true,
      employeeIds: schedule?.assignments?.map(a => a.employeeId) ?? [],
      notes: schedule?.notes ?? '',
    },
  });

  const watchAssignToAll = form.watch('assignToAllEmployees');
  const watchFrequency = form.watch('frequency');

  // Reset form when dialog opens/closes
  useEffect(() => {
    if (open) {
      form.reset({
        toolboxTalkId: schedule?.toolboxTalkId ?? preselectedTalkId ?? '',
        scheduledDate: schedule ? new Date(schedule.scheduledDate) : new Date(),
        endDate: schedule?.endDate ? new Date(schedule.endDate) : null,
        frequency: schedule?.frequency ?? 'Once',
        assignToAllEmployees: schedule?.assignToAllEmployees ?? true,
        employeeIds: schedule?.assignments?.map(a => a.employeeId) ?? [],
        notes: schedule?.notes ?? '',
      });
      setSelectedEmployees(schedule?.assignments?.map(a => a.employeeId) ?? []);
    }
  }, [open, schedule, preselectedTalkId, form]);

  // Sync selected employees with form
  useEffect(() => {
    form.setValue('employeeIds', selectedEmployees);
  }, [selectedEmployees, form]);

  const isSubmitting = createMutation.isPending || updateMutation.isPending;

  async function onSubmit(values: ScheduleFormValues) {
    // Custom validation
    if (!values.assignToAllEmployees && (!values.employeeIds || values.employeeIds.length === 0)) {
      form.setError('employeeIds', { message: 'Please select at least one employee' });
      return;
    }
    if (values.endDate && values.scheduledDate && values.endDate < values.scheduledDate) {
      form.setError('endDate', { message: 'End date must be after the scheduled date' });
      return;
    }

    try {
      const payload = {
        toolboxTalkId: values.toolboxTalkId,
        scheduledDate: values.scheduledDate.toISOString(),
        endDate: values.endDate?.toISOString(),
        frequency: values.frequency,
        assignToAllEmployees: values.assignToAllEmployees,
        employeeIds: values.assignToAllEmployees ? undefined : values.employeeIds,
        notes: values.notes || undefined,
      };

      console.log('Creating schedule with payload:', JSON.stringify(payload, null, 2));

      if (isEditing && schedule) {
        await updateMutation.mutateAsync({
          id: schedule.id,
          data: { id: schedule.id, ...payload },
        });
        toast.success('Schedule updated successfully');
      } else {
        await createMutation.mutateAsync(payload);
        toast.success('Schedule created successfully');
      }

      onOpenChange(false);
      onSuccess?.();
    } catch (error: unknown) {
      console.error('Schedule creation error:', error);
      // Try to extract the error response
      const axiosError = error as { response?: { data?: { message?: string; errors?: unknown } } };
      if (axiosError.response?.data) {
        console.error('Error response data:', JSON.stringify(axiosError.response.data, null, 2));
      }
      const message = axiosError.response?.data?.message || (error instanceof Error ? error.message : 'An error occurred');
      toast.error(isEditing ? 'Failed to update schedule' : 'Failed to create schedule', {
        description: message,
      });
    }
  }

  const handleEmployeeToggle = (employeeId: string, checked: boolean) => {
    if (checked) {
      setSelectedEmployees(prev => [...prev, employeeId]);
    } else {
      setSelectedEmployees(prev => prev.filter(id => id !== employeeId));
    }
  };

  const filteredEmployees = (employees ?? []).filter((employee) => {
    if (!employeeSearch.trim()) return true;
    const search = employeeSearch.toLowerCase();
    return (
      employee.firstName?.toLowerCase().includes(search) ||
      employee.lastName?.toLowerCase().includes(search) ||
      employee.employeeCode?.toLowerCase().includes(search) ||
      `${employee.firstName} ${employee.lastName}`.toLowerCase().includes(search)
    );
  });

  const handleSelectAllEmployees = () => {
    const ids = filteredEmployees.map(e => e.id);
    setSelectedEmployees(prev => [...new Set([...prev, ...ids])]);
  };

  const handleClearEmployees = () => {
    if (employeeSearch.trim()) {
      const filteredIds = new Set(filteredEmployees.map(e => e.id));
      setSelectedEmployees(prev => prev.filter(id => !filteredIds.has(id)));
    } else {
      setSelectedEmployees([]);
    }
  };

  const formContent = (
    <Form {...form}>
          <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
            {/* Warning if preselected talk is inactive */}
            {isPreselectedTalkInactive && (
              <Alert variant="destructive">
                <AlertCircleIcon className="h-4 w-4" />
                <AlertDescription>
                  This learning is inactive and cannot be scheduled. Please activate it first or select a different talk.
                </AlertDescription>
              </Alert>
            )}

            {/* Talk selector (disabled if preselected or editing) */}
            <FormField
              control={form.control}
              name="toolboxTalkId"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Learning *</FormLabel>
                  <Select
                    value={field.value}
                    onValueChange={field.onChange}
                    disabled={!!preselectedTalkId || isEditing}
                  >
                    <FormControl>
                      <SelectTrigger>
                        <SelectValue placeholder="Select a learning" />
                      </SelectTrigger>
                    </FormControl>
                    <SelectContent>
                      {talks.map((talk) => (
                        <SelectItem key={talk.id} value={talk.id}>
                          {talk.title}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  <FormMessage />
                </FormItem>
              )}
            />

            <div className="grid gap-4 sm:grid-cols-2">
              {/* Scheduled date */}
              <FormField
                control={form.control}
                name="scheduledDate"
                render={({ field }) => (
                  <FormItem className="flex flex-col">
                    <FormLabel>Start Date *</FormLabel>
                    <Popover>
                      <PopoverTrigger asChild>
                        <FormControl>
                          <Button
                            variant="outline"
                            className={cn(
                              'w-full pl-3 text-left font-normal',
                              !field.value && 'text-muted-foreground'
                            )}
                          >
                            {field.value ? (
                              format(field.value, 'PPP')
                            ) : (
                              <span>Pick a date</span>
                            )}
                            <CalendarIcon className="ml-auto h-4 w-4 opacity-50" />
                          </Button>
                        </FormControl>
                      </PopoverTrigger>
                      <PopoverContent className="w-auto p-0" align="start">
                        <Calendar
                          mode="single"
                          selected={field.value}
                          onSelect={field.onChange}
                        />
                      </PopoverContent>
                    </Popover>
                    <FormMessage />
                  </FormItem>
                )}
              />

              {/* End date (optional) */}
              <FormField
                control={form.control}
                name="endDate"
                render={({ field }) => (
                  <FormItem className="flex flex-col">
                    <FormLabel>End Date</FormLabel>
                    <Popover>
                      <PopoverTrigger asChild>
                        <FormControl>
                          <Button
                            variant="outline"
                            className={cn(
                              'w-full pl-3 text-left font-normal',
                              !field.value && 'text-muted-foreground'
                            )}
                          >
                            {field.value ? (
                              format(field.value, 'PPP')
                            ) : (
                              <span>Optional</span>
                            )}
                            <CalendarIcon className="ml-auto h-4 w-4 opacity-50" />
                          </Button>
                        </FormControl>
                      </PopoverTrigger>
                      <PopoverContent className="w-auto p-0" align="start">
                        <Calendar
                          mode="single"
                          selected={field.value ?? undefined}
                          onSelect={field.onChange}
                        />
                      </PopoverContent>
                    </Popover>
                    <FormDescription>
                      For recurring schedules
                    </FormDescription>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </div>

            {/* Frequency */}
            <FormField
              control={form.control}
              name="frequency"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Frequency *</FormLabel>
                  <Select value={field.value} onValueChange={field.onChange}>
                    <FormControl>
                      <SelectTrigger>
                        <SelectValue placeholder="Select frequency" />
                      </SelectTrigger>
                    </FormControl>
                    <SelectContent>
                      {FREQUENCY_OPTIONS.map((option) => (
                        <SelectItem key={option.value} value={option.value}>
                          {option.label}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  <FormDescription>
                    {watchFrequency === 'Once'
                      ? 'Employees complete this talk once'
                      : `Employees will be re-assigned ${watchFrequency.toLowerCase()}`}
                  </FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />

            {/* Assign to all employees checkbox */}
            <FormField
              control={form.control}
              name="assignToAllEmployees"
              render={({ field }) => (
                <FormItem className="flex flex-row items-start space-x-3 space-y-0 rounded-md border p-4">
                  <FormControl>
                    <Checkbox
                      checked={field.value}
                      onCheckedChange={field.onChange}
                    />
                  </FormControl>
                  <div className="space-y-1 leading-none">
                    <FormLabel>Assign to all employees</FormLabel>
                    <FormDescription>
                      Automatically assign to all current and future employees.
                    </FormDescription>
                  </div>
                </FormItem>
              )}
            />

            {/* Employee selection (conditional) */}
            {!watchAssignToAll && (
              <FormField
                control={form.control}
                name="employeeIds"
                render={() => (
                  <FormItem>
                    <div className="flex items-center justify-between">
                      <FormLabel>Select Employees *</FormLabel>
                      <div className="flex gap-2">
                        <Button
                          type="button"
                          variant="ghost"
                          size="sm"
                          onClick={handleSelectAllEmployees}
                        >
                          Select All
                        </Button>
                        <Button
                          type="button"
                          variant="ghost"
                          size="sm"
                          onClick={handleClearEmployees}
                        >
                          Clear
                        </Button>
                      </div>
                    </div>
                    <Input
                      placeholder="Search employees..."
                      value={employeeSearch}
                      onChange={(e) => setEmployeeSearch(e.target.value)}
                    />
                    <div className="rounded-md border max-h-[200px] overflow-y-auto">
                      {employeesLoading ? (
                        <div className="p-4 text-center text-muted-foreground">
                          Loading employees...
                        </div>
                      ) : filteredEmployees.length > 0 ? (
                        <div className="divide-y">
                          {filteredEmployees.map((employee) => (
                            <label
                              key={employee.id}
                              className="flex items-center gap-3 p-3 hover:bg-muted/50 cursor-pointer"
                            >
                              <Checkbox
                                checked={selectedEmployees.includes(employee.id)}
                                onCheckedChange={(checked) =>
                                  handleEmployeeToggle(employee.id, !!checked)
                                }
                              />
                              <div className="flex-1">
                                <p className="text-sm font-medium">
                                  {employee.firstName} {employee.lastName}
                                </p>
                                {employee.email && (
                                  <p className="text-xs text-muted-foreground">
                                    {employee.email}
                                  </p>
                                )}
                              </div>
                            </label>
                          ))}
                        </div>
                      ) : employeeSearch.trim() ? (
                        <div className="p-4 text-center text-muted-foreground">
                          No employees match &ldquo;{employeeSearch}&rdquo;
                        </div>
                      ) : (
                        <div className="p-4 text-center text-muted-foreground">
                          No employees found
                        </div>
                      )}
                    </div>
                    <FormDescription>
                      {selectedEmployees.length} employee{selectedEmployees.length !== 1 ? 's' : ''} selected
                      {employeeSearch.trim() && employees && filteredEmployees.length !== employees.length && (
                        <> &middot; showing {filteredEmployees.length} of {employees.length}</>
                      )}
                    </FormDescription>
                    <FormMessage />
                  </FormItem>
                )}
              />
            )}

            {/* Notes */}
            <FormField
              control={form.control}
              name="notes"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Notes</FormLabel>
                  <FormControl>
                    <Textarea
                      placeholder="Optional notes about this schedule..."
                      className="min-h-[80px]"
                      {...field}
                      value={field.value ?? ''}
                    />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <div className={embedded ? 'flex justify-end gap-2 mt-6' : ''}>
              {embedded ? (
                <>
                  <Button
                    type="button"
                    variant="outline"
                    onClick={() => onOpenChange(false)}
                  >
                    Cancel
                  </Button>
                  <Button type="submit" disabled={isSubmitting || isPreselectedTalkInactive}>
                    {isSubmitting ? (
                      <>
                        <LoadingSpinner className="mr-2 h-4 w-4" />
                        {isEditing ? 'Updating...' : 'Creating...'}
                      </>
                    ) : isEditing ? (
                      'Update Schedule'
                    ) : (
                      'Create Schedule'
                    )}
                  </Button>
                </>
              ) : (
                <DialogFooter>
                  <Button
                    type="button"
                    variant="outline"
                    onClick={() => onOpenChange(false)}
                  >
                    Cancel
                  </Button>
                  <Button type="submit" disabled={isSubmitting || isPreselectedTalkInactive}>
                    {isSubmitting ? (
                      <>
                        <LoadingSpinner className="mr-2 h-4 w-4" />
                        {isEditing ? 'Updating...' : 'Creating...'}
                      </>
                    ) : isEditing ? (
                      'Update Schedule'
                    ) : (
                      'Create Schedule'
                    )}
                  </Button>
                </DialogFooter>
              )}
            </div>
          </form>
        </Form>
  );

  // If embedded, just render the form directly
  if (embedded) {
    return (
      <div className="rounded-lg border bg-card p-6">
        {formContent}
      </div>
    );
  }

  // Normal dialog mode
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[500px] max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{isEditing ? 'Edit Schedule' : 'Schedule Learning'}</DialogTitle>
          <DialogDescription>
            {isEditing
              ? 'Update the schedule details for this learning.'
              : 'Create a new schedule to assign this learning to employees.'}
          </DialogDescription>
        </DialogHeader>
        {formContent}
      </DialogContent>
    </Dialog>
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

