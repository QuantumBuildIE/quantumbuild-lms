'use client';

import { useState, useMemo, useCallback } from 'react';
import { format } from 'date-fns';
import { CalendarIcon, SearchIcon, UsersIcon, CheckCircle2Icon, ArrowLeftIcon, Loader2Icon } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Checkbox } from '@/components/ui/checkbox';
import { Calendar } from '@/components/ui/calendar';
import { Label } from '@/components/ui/label';
import { Badge } from '@/components/ui/badge';
import {
  Accordion,
  AccordionContent,
  AccordionItem,
  AccordionTrigger,
} from '@/components/ui/accordion';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from '@/components/ui/popover';
import { useAllEmployees } from '@/lib/api/admin/use-employees';
import { useAssignCourse, useCourseAssignmentPreview } from '@/lib/api/toolbox-talks/use-course-assignments';
import type { CourseAssignmentPreviewDto, EmployeeCourseAssignment } from '@/lib/api/toolbox-talks/course-assignments';
import { toast } from 'sonner';
import { cn } from '@/lib/utils';

interface AssignCourseDialogProps {
  course: { id: string; title: string };
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

type Step = 'select' | 'preview';

export function AssignCourseDialog({ course, open, onOpenChange }: AssignCourseDialogProps) {
  const { data: employees, isLoading: employeesLoading } = useAllEmployees();
  const assignMutation = useAssignCourse();
  const previewMutation = useCourseAssignmentPreview();

  const [step, setStep] = useState<Step>('select');
  const [selectedEmployees, setSelectedEmployees] = useState<string[]>([]);
  const [employeeSearch, setEmployeeSearch] = useState('');
  const [dueDate, setDueDate] = useState<Date | undefined>(undefined);
  const [preview, setPreview] = useState<CourseAssignmentPreviewDto | null>(null);
  // Map<employeeId, Set<includedTalkIds>>
  const [assignments, setAssignments] = useState<Map<string, Set<string>>>(new Map());

  const filteredEmployees = useMemo(() => {
    return (employees ?? []).filter((employee) => {
      if (!employeeSearch.trim()) return true;
      const search = employeeSearch.toLowerCase();
      return (
        employee.firstName?.toLowerCase().includes(search) ||
        employee.lastName?.toLowerCase().includes(search) ||
        `${employee.firstName} ${employee.lastName}`.toLowerCase().includes(search) ||
        employee.email?.toLowerCase().includes(search)
      );
    });
  }, [employees, employeeSearch]);

  const handleEmployeeToggle = (employeeId: string, checked: boolean) => {
    setSelectedEmployees((prev) =>
      checked ? [...prev, employeeId] : prev.filter((id) => id !== employeeId)
    );
  };

  const handleSelectAll = () => {
    const allFilteredIds = filteredEmployees.map((e) => e.id);
    setSelectedEmployees((prev) => {
      const newSet = new Set(prev);
      allFilteredIds.forEach((id) => newSet.add(id));
      return Array.from(newSet);
    });
  };

  const handleClearAll = () => {
    setSelectedEmployees([]);
  };

  const handleNext = async () => {
    if (selectedEmployees.length === 0) {
      toast.error('Please select at least one employee');
      return;
    }

    try {
      const previewData = await previewMutation.mutateAsync({
        courseId: course.id,
        employeeIds: selectedEmployees,
      });
      setPreview(previewData);

      // Initialize assignments: non-completed talks are pre-selected
      const initialAssignments = new Map<string, Set<string>>();
      for (const emp of previewData.employees) {
        const included = new Set<string>();
        for (const talk of emp.talks) {
          if (!talk.alreadyCompleted) {
            included.add(talk.toolboxTalkId);
          }
        }
        initialAssignments.set(emp.employeeId, included);
      }
      setAssignments(initialAssignments);
      setStep('preview');
    } catch (error: unknown) {
      let message = 'Failed to load assignment preview';
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

  const handleBack = () => {
    setStep('select');
  };

  const handleTalkToggle = useCallback((employeeId: string, talkId: string, checked: boolean) => {
    setAssignments((prev) => {
      const next = new Map(prev);
      const empSet = new Set(next.get(employeeId) ?? []);
      if (checked) {
        empSet.add(talkId);
      } else {
        empSet.delete(talkId);
      }
      next.set(employeeId, empSet);
      return next;
    });
  }, []);

  const handleIncludeAll = useCallback((employeeId: string) => {
    if (!preview) return;
    const emp = preview.employees.find((e) => e.employeeId === employeeId);
    if (!emp) return;
    setAssignments((prev) => {
      const next = new Map(prev);
      const empSet = new Set(emp.talks.map((t) => t.toolboxTalkId));
      next.set(employeeId, empSet);
      return next;
    });
  }, [preview]);

  const handleSkipCompleted = useCallback((employeeId: string) => {
    if (!preview) return;
    const emp = preview.employees.find((e) => e.employeeId === employeeId);
    if (!emp) return;
    setAssignments((prev) => {
      const next = new Map(prev);
      const empSet = new Set<string>();
      for (const talk of emp.talks) {
        if (!talk.alreadyCompleted) {
          empSet.add(talk.toolboxTalkId);
        }
      }
      next.set(employeeId, empSet);
      return next;
    });
  }, [preview]);

  const handleAssign = async () => {
    const assignmentData: EmployeeCourseAssignment[] = [];

    for (const [employeeId, talkIds] of assignments) {
      if (talkIds.size === 0) continue;
      assignmentData.push({
        employeeId,
        includedTalkIds: Array.from(talkIds),
      });
    }

    if (assignmentData.length === 0) {
      toast.error('No talks selected for any employee');
      return;
    }

    try {
      await assignMutation.mutateAsync({
        courseId: course.id,
        assignments: assignmentData,
        dueDate: dueDate ? dueDate.toISOString() : undefined,
      });
      toast.success('Course assigned', {
        description: `Assigned "${course.title}" to ${assignmentData.length} employee${assignmentData.length !== 1 ? 's' : ''}`,
      });
      handleClose();
    } catch (error: unknown) {
      let message = 'Failed to assign course';
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

  const handleClose = () => {
    setSelectedEmployees([]);
    setEmployeeSearch('');
    setDueDate(undefined);
    setStep('select');
    setPreview(null);
    setAssignments(new Map());
    onOpenChange(false);
  };

  const totalSelectedTalks = useMemo(() => {
    let count = 0;
    for (const talkIds of assignments.values()) {
      count += talkIds.size;
    }
    return count;
  }, [assignments]);

  const employeesWithTalks = useMemo(() => {
    return Array.from(assignments.values()).filter((s) => s.size > 0).length;
  }, [assignments]);

  return (
    <Dialog open={open} onOpenChange={handleClose}>
      <DialogContent className={cn("sm:max-w-[500px]", step === 'preview' && "sm:max-w-[600px]")}>
        <DialogHeader>
          <DialogTitle>
            {step === 'select' ? 'Assign Course' : 'Review Assignment'}
          </DialogTitle>
          <DialogDescription>
            {step === 'select'
              ? `Assign "${course.title}" to employees.`
              : 'Review and customize which talks each employee needs to complete.'}
          </DialogDescription>
        </DialogHeader>

        {step === 'select' ? (
          <div className="space-y-4 py-4">
            {/* Due Date */}
            <div className="space-y-2">
              <Label>Due Date (optional)</Label>
              <Popover>
                <PopoverTrigger asChild>
                  <Button
                    variant="outline"
                    className={cn(
                      'w-full justify-start text-left font-normal',
                      !dueDate && 'text-muted-foreground'
                    )}
                  >
                    <CalendarIcon className="mr-2 h-4 w-4" />
                    {dueDate ? format(dueDate, 'PPP') : 'Select a due date'}
                  </Button>
                </PopoverTrigger>
                <PopoverContent className="w-auto p-0" align="start">
                  <Calendar
                    mode="single"
                    selected={dueDate}
                    onSelect={setDueDate}
                    disabled={(date) => date < new Date()}
                    initialFocus
                  />
                </PopoverContent>
              </Popover>
            </div>

            {/* Employee Selection */}
            <div className="space-y-2">
              <div className="flex items-center justify-between">
                <Label>Select Employees</Label>
                <div className="flex items-center gap-2">
                  <Button type="button" variant="ghost" size="sm" onClick={handleSelectAll}>
                    Select All
                  </Button>
                  <Button type="button" variant="ghost" size="sm" onClick={handleClearAll}>
                    Clear
                  </Button>
                </div>
              </div>

              {/* Search */}
              <div className="relative">
                <SearchIcon className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                <Input
                  placeholder="Search employees..."
                  value={employeeSearch}
                  onChange={(e) => setEmployeeSearch(e.target.value)}
                  className="pl-9"
                />
              </div>

              {/* Employee List */}
              <div className="rounded-md border max-h-[200px] overflow-y-auto">
                {employeesLoading ? (
                  <div className="p-4 text-center text-sm text-muted-foreground">
                    Loading employees...
                  </div>
                ) : filteredEmployees.length === 0 ? (
                  <div className="p-4 text-center text-sm text-muted-foreground">
                    No employees found
                  </div>
                ) : (
                  filteredEmployees.map((employee) => (
                    <label
                      key={employee.id}
                      className="flex items-center gap-3 p-3 hover:bg-muted/50 cursor-pointer border-b last:border-b-0"
                    >
                      <Checkbox
                        checked={selectedEmployees.includes(employee.id)}
                        onCheckedChange={(checked) =>
                          handleEmployeeToggle(employee.id, !!checked)
                        }
                      />
                      <div className="flex-1 min-w-0">
                        <p className="text-sm font-medium">
                          {employee.firstName} {employee.lastName}
                        </p>
                        {employee.email && (
                          <p className="text-xs text-muted-foreground truncate">
                            {employee.email}
                          </p>
                        )}
                      </div>
                    </label>
                  ))
                )}
              </div>

              {/* Selected count */}
              {selectedEmployees.length > 0 && (
                <div className="flex items-center gap-2 text-sm text-muted-foreground">
                  <UsersIcon className="h-4 w-4" />
                  <span>{selectedEmployees.length} employee{selectedEmployees.length !== 1 ? 's' : ''} selected</span>
                </div>
              )}
            </div>
          </div>
        ) : (
          /* Preview Step */
          <div className="py-4">
            <div className="rounded-md border max-h-[400px] overflow-y-auto">
              {preview && preview.employees.length > 0 ? (
                <Accordion type="multiple" defaultValue={preview.employees.map((e) => e.employeeId)}>
                  {preview.employees.map((emp) => {
                    const empTalkIds = assignments.get(emp.employeeId) ?? new Set<string>();
                    const selectedCount = empTalkIds.size;

                    return (
                      <AccordionItem key={emp.employeeId} value={emp.employeeId}>
                        <AccordionTrigger className="px-4 hover:no-underline">
                          <div className="flex items-center gap-3 flex-1 min-w-0">
                            <div className="flex-1 min-w-0 text-left">
                              <span className="font-medium">{emp.employeeName}</span>
                              {emp.employeeCode && (
                                <span className="text-muted-foreground ml-2 text-xs">
                                  ({emp.employeeCode})
                                </span>
                              )}
                            </div>
                            <div className="flex items-center gap-2 shrink-0">
                              {emp.completedCount > 0 && (
                                <Badge variant="secondary" className="text-xs">
                                  {emp.completedCount}/{emp.totalCount} completed
                                </Badge>
                              )}
                              <Badge variant="outline" className="text-xs">
                                {selectedCount} selected
                              </Badge>
                            </div>
                          </div>
                        </AccordionTrigger>
                        <AccordionContent className="px-4">
                          <div className="space-y-1">
                            {emp.talks.map((talk) => {
                              const isSelected = empTalkIds.has(talk.toolboxTalkId);
                              return (
                                <label
                                  key={talk.toolboxTalkId}
                                  className="flex items-center gap-3 p-2 rounded-md hover:bg-muted/50 cursor-pointer"
                                >
                                  <Checkbox
                                    checked={isSelected}
                                    onCheckedChange={(checked) =>
                                      handleTalkToggle(emp.employeeId, talk.toolboxTalkId, !!checked)
                                    }
                                  />
                                  <div className="flex-1 min-w-0">
                                    <span className="text-sm">{talk.title}</span>
                                  </div>
                                  {talk.alreadyCompleted && (
                                    <div className="flex items-center gap-1 text-xs text-green-600 shrink-0">
                                      <CheckCircle2Icon className="h-3.5 w-3.5" />
                                      <span>
                                        Completed{talk.completedAt ? ` ${format(new Date(talk.completedAt), 'MMM d')}` : ''}
                                      </span>
                                    </div>
                                  )}
                                </label>
                              );
                            })}
                          </div>
                          <div className="flex items-center gap-2 mt-2 pt-2 border-t">
                            <Button
                              type="button"
                              variant="ghost"
                              size="sm"
                              onClick={() => handleIncludeAll(emp.employeeId)}
                            >
                              Include All
                            </Button>
                            <Button
                              type="button"
                              variant="ghost"
                              size="sm"
                              onClick={() => handleSkipCompleted(emp.employeeId)}
                            >
                              Skip Completed
                            </Button>
                          </div>
                        </AccordionContent>
                      </AccordionItem>
                    );
                  })}
                </Accordion>
              ) : (
                <div className="p-4 text-center text-sm text-muted-foreground">
                  No preview data available
                </div>
              )}
            </div>
            {totalSelectedTalks > 0 && (
              <p className="text-xs text-muted-foreground mt-2">
                {totalSelectedTalks} talk{totalSelectedTalks !== 1 ? 's' : ''} will be assigned across {employeesWithTalks} employee{employeesWithTalks !== 1 ? 's' : ''}
              </p>
            )}
          </div>
        )}

        <DialogFooter>
          {step === 'select' ? (
            <>
              <Button variant="outline" onClick={handleClose}>
                Cancel
              </Button>
              <Button
                onClick={handleNext}
                disabled={selectedEmployees.length === 0 || previewMutation.isPending}
              >
                {previewMutation.isPending ? (
                  <>
                    <Loader2Icon className="mr-2 h-4 w-4 animate-spin" />
                    Loading...
                  </>
                ) : (
                  'Next'
                )}
              </Button>
            </>
          ) : (
            <>
              <Button variant="outline" onClick={handleBack}>
                <ArrowLeftIcon className="mr-2 h-4 w-4" />
                Back
              </Button>
              <Button
                onClick={handleAssign}
                disabled={totalSelectedTalks === 0 || assignMutation.isPending}
              >
                {assignMutation.isPending ? (
                  <>
                    <Loader2Icon className="mr-2 h-4 w-4 animate-spin" />
                    Assigning...
                  </>
                ) : (
                  `Assign ${totalSelectedTalks} talk${totalSelectedTalks !== 1 ? 's' : ''}`
                )}
              </Button>
            </>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
