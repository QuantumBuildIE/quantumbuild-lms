'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import {
  DndContext,
  closestCenter,
  KeyboardSensor,
  PointerSensor,
  useSensor,
  useSensors,
  type DragEndEvent,
} from '@dnd-kit/core';
import {
  arrayMove,
  SortableContext,
  sortableKeyboardCoordinates,
  useSortable,
  verticalListSortingStrategy,
} from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';
import { restrictToVerticalAxis } from '@dnd-kit/modifiers';
import {
  GripVerticalIcon,
  PlusIcon,
  XIcon,
  VideoIcon,
  FileTextIcon,
  HelpCircleIcon,
  ArrowLeftIcon,
  UsersIcon,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Switch } from '@/components/ui/switch';
import { Label } from '@/components/ui/label';
import { Badge } from '@/components/ui/badge';
import {
  Form,
  FormControl,
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from '@/components/ui/form';
import {
  useCreateToolboxTalkCourse,
  useUpdateToolboxTalkCourse,
  useUpdateCourseItems,
} from '@/lib/api/toolbox-talks/use-courses';
import type {
  ToolboxTalkCourseDto,
  ToolboxTalkCourseItemDto,
} from '@/lib/api/toolbox-talks/courses';
import type { ToolboxTalkListItem } from '@/types/toolbox-talks';
import { AddTalksDialog } from './AddTalksDialog';
import { AssignCourseDialog } from './AssignCourseDialog';
import { CourseAssignmentsList } from './CourseAssignmentsList';
import { toast } from 'sonner';

// ============================================
// Form Schema
// ============================================

const courseFormSchema = z.object({
  title: z.string().min(1, 'Title is required').max(200, 'Title must be 200 characters or less'),
  description: z.string().max(2000, 'Description must be 2000 characters or less').optional().nullable(),
  isActive: z.boolean(),
  requireSequentialCompletion: z.boolean(),
  requiresRefresher: z.boolean(),
  refresherIntervalMonths: z.number().min(1).max(120),
  generateCertificate: z.boolean(),
  autoAssignToNewEmployees: z.boolean(),
  autoAssignDueDays: z.number().min(1).max(365),
});

type CourseFormValues = z.infer<typeof courseFormSchema>;

// ============================================
// Course Item Type (local state)
// ============================================

interface CourseItem {
  toolboxTalkId: string;
  orderIndex: number;
  isRequired: boolean;
  talkCode: string;
  talkTitle: string;
  talkDescription?: string;
  talkHasVideo: boolean;
  talkSectionCount: number;
  talkQuestionCount: number;
}

// ============================================
// Sortable Item Component
// ============================================

interface SortableItemProps {
  item: CourseItem;
  index: number;
  onToggleRequired: (id: string) => void;
  onRemove: (id: string) => void;
}

function SortableItem({ item, index, onToggleRequired, onRemove }: SortableItemProps) {
  const {
    attributes,
    listeners,
    setNodeRef,
    transform,
    transition,
    isDragging,
  } = useSortable({ id: item.toolboxTalkId });

  const style = {
    transform: CSS.Transform.toString(transform),
    transition,
    opacity: isDragging ? 0.5 : 1,
  };

  return (
    <div
      ref={setNodeRef}
      style={style}
      className="flex items-center gap-3 rounded-md border bg-card p-3"
    >
      <button
        className="cursor-grab touch-none text-muted-foreground hover:text-foreground"
        {...attributes}
        {...listeners}
      >
        <GripVerticalIcon className="h-4 w-4" />
      </button>

      <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-muted text-xs font-medium">
        {index + 1}
      </span>

      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2">
          <span className="font-medium text-sm truncate">
            {item.talkCode && (
              <span className="text-muted-foreground font-mono mr-1.5">{item.talkCode}</span>
            )}
            {item.talkTitle}
          </span>
          {item.talkHasVideo && (
            <VideoIcon className="h-3.5 w-3.5 shrink-0 text-muted-foreground" />
          )}
        </div>
        <div className="flex items-center gap-3 mt-0.5 text-xs text-muted-foreground">
          {item.talkSectionCount > 0 && (
            <span className="flex items-center gap-1">
              <FileTextIcon className="h-3 w-3" />
              {item.talkSectionCount} sections
            </span>
          )}
          {item.talkQuestionCount > 0 && (
            <span className="flex items-center gap-1">
              <HelpCircleIcon className="h-3 w-3" />
              {item.talkQuestionCount} questions
            </span>
          )}
        </div>
      </div>

      <div className="flex items-center gap-2 shrink-0">
        <label className="flex items-center gap-1.5 cursor-pointer">
          <Switch
            checked={item.isRequired}
            onCheckedChange={() => onToggleRequired(item.toolboxTalkId)}
            className="scale-75"
          />
          <span className="text-xs text-muted-foreground">Required</span>
        </label>
        <Button
          variant="ghost"
          size="sm"
          className="h-7 w-7 p-0 text-muted-foreground hover:text-destructive"
          onClick={() => onRemove(item.toolboxTalkId)}
        >
          <XIcon className="h-4 w-4" />
        </Button>
      </div>
    </div>
  );
}

// ============================================
// Course Form Component
// ============================================

interface CourseFormProps {
  course?: ToolboxTalkCourseDto;
}

export function CourseForm({ course }: CourseFormProps) {
  const router = useRouter();
  const isEditing = !!course;

  const createMutation = useCreateToolboxTalkCourse();
  const updateMutation = useUpdateToolboxTalkCourse();
  const updateItemsMutation = useUpdateCourseItems();

  const [addTalksOpen, setAddTalksOpen] = useState(false);
  const [assignDialogOpen, setAssignDialogOpen] = useState(false);
  const [courseItems, setCourseItems] = useState<CourseItem[]>(() => {
    if (course?.items) {
      return course.items
        .sort((a, b) => a.orderIndex - b.orderIndex)
        .map((item) => ({
          toolboxTalkId: item.toolboxTalkId,
          orderIndex: item.orderIndex,
          isRequired: item.isRequired,
          talkCode: item.talkCode,
          talkTitle: item.talkTitle,
          talkDescription: item.talkDescription ?? undefined,
          talkHasVideo: item.talkHasVideo,
          talkSectionCount: item.talkSectionCount,
          talkQuestionCount: item.talkQuestionCount,
        }));
    }
    return [];
  });

  const form = useForm<CourseFormValues>({
    resolver: zodResolver(courseFormSchema),
    defaultValues: {
      title: course?.title ?? '',
      description: course?.description ?? '',
      isActive: course?.isActive ?? true,
      requireSequentialCompletion: course?.requireSequentialCompletion ?? true,
      requiresRefresher: course?.requiresRefresher ?? false,
      refresherIntervalMonths: course?.refresherIntervalMonths ?? 12,
      generateCertificate: course?.generateCertificate ?? false,
      autoAssignToNewEmployees: course?.autoAssignToNewEmployees ?? false,
      autoAssignDueDays: course?.autoAssignDueDays ?? 14,
    },
  });

  const requiresRefresher = form.watch('requiresRefresher');
  const autoAssignToNewEmployees = form.watch('autoAssignToNewEmployees');

  const sensors = useSensors(
    useSensor(PointerSensor),
    useSensor(KeyboardSensor, {
      coordinateGetter: sortableKeyboardCoordinates,
    })
  );

  const handleDragEnd = (event: DragEndEvent) => {
    const { active, over } = event;

    if (over && active.id !== over.id) {
      setCourseItems((items) => {
        const oldIndex = items.findIndex((i) => i.toolboxTalkId === active.id);
        const newIndex = items.findIndex((i) => i.toolboxTalkId === over.id);
        const newItems = arrayMove(items, oldIndex, newIndex);
        return newItems.map((item, idx) => ({ ...item, orderIndex: idx }));
      });
    }
  };

  const handleAddTalks = (talks: ToolboxTalkListItem[]) => {
    const startIndex = courseItems.length;
    const newItems: CourseItem[] = talks.map((talk, idx) => ({
      toolboxTalkId: talk.id,
      orderIndex: startIndex + idx,
      isRequired: true,
      talkCode: talk.code,
      talkTitle: talk.title,
      talkDescription: talk.description ?? undefined,
      talkHasVideo: false,
      talkSectionCount: talk.sectionCount,
      talkQuestionCount: talk.questionCount,
    }));
    setCourseItems((prev) => [...prev, ...newItems]);
  };

  const handleToggleRequired = (talkId: string) => {
    setCourseItems((items) =>
      items.map((item) =>
        item.toolboxTalkId === talkId ? { ...item, isRequired: !item.isRequired } : item
      )
    );
  };

  const handleRemoveItem = (talkId: string) => {
    setCourseItems((items) =>
      items
        .filter((item) => item.toolboxTalkId !== talkId)
        .map((item, idx) => ({ ...item, orderIndex: idx }))
    );
  };

  const onSubmit = async (values: CourseFormValues) => {
    try {
      if (isEditing && course) {
        await updateMutation.mutateAsync({
          id: course.id,
          data: {
            title: values.title,
            description: values.description || undefined,
            isActive: values.isActive,
            requireSequentialCompletion: values.requireSequentialCompletion,
            requiresRefresher: values.requiresRefresher,
            refresherIntervalMonths: values.refresherIntervalMonths,
            generateCertificate: values.generateCertificate,
            autoAssignToNewEmployees: values.autoAssignToNewEmployees,
            autoAssignDueDays: values.autoAssignDueDays,
          },
        });

        await updateItemsMutation.mutateAsync({
          courseId: course.id,
          data: {
            items: courseItems.map((item) => ({
              toolboxTalkId: item.toolboxTalkId,
              orderIndex: item.orderIndex,
              isRequired: item.isRequired,
            })),
          },
        });

        toast.success('Course updated successfully');
      } else {
        await createMutation.mutateAsync({
          title: values.title,
          description: values.description || undefined,
          isActive: values.isActive,
          requireSequentialCompletion: values.requireSequentialCompletion,
          requiresRefresher: values.requiresRefresher,
          refresherIntervalMonths: values.refresherIntervalMonths,
          generateCertificate: values.generateCertificate,
          autoAssignToNewEmployees: values.autoAssignToNewEmployees,
          autoAssignDueDays: values.autoAssignDueDays,
          items: courseItems.map((item) => ({
            toolboxTalkId: item.toolboxTalkId,
            orderIndex: item.orderIndex,
            isRequired: item.isRequired,
          })),
        });

        toast.success('Course created successfully');
      }

      router.push('/admin/toolbox-talks/courses');
    } catch (error: unknown) {
      let message = isEditing ? 'Failed to update course' : 'Failed to create course';
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

  const isSaving = createMutation.isPending || updateMutation.isPending || updateItemsMutation.isPending;

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-4">
        <Button variant="ghost" size="sm" onClick={() => router.push('/admin/toolbox-talks/courses')}>
          <ArrowLeftIcon className="mr-2 h-4 w-4" />
          Back to Courses
        </Button>
      </div>

      <div>
        <h1 className="text-2xl font-semibold tracking-tight">
          {isEditing ? 'Edit Course' : 'Create Course'}
        </h1>
        <p className="text-muted-foreground">
          {isEditing
            ? 'Update course details and manage the talks included in this course.'
            : 'Create a new course by defining its settings and adding learnings.'}
        </p>
      </div>

      <Form {...form}>
        <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-8">
          {/* Course Details */}
          <div className="space-y-4 rounded-lg border p-6">
            <h2 className="text-lg font-medium">Course Details</h2>

            <FormField
              control={form.control}
              name="title"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Title</FormLabel>
                  <FormControl>
                    <Input placeholder="e.g. Construction Safety Fundamentals" {...field} />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="description"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>Description</FormLabel>
                  <FormControl>
                    <Textarea
                      placeholder="Describe the purpose and contents of this course..."
                      rows={3}
                      {...field}
                      value={field.value ?? ''}
                    />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
          </div>

          {/* Course Settings */}
          <div className="space-y-4 rounded-lg border p-6">
            <h2 className="text-lg font-medium">Settings</h2>

            <FormField
              control={form.control}
              name="isActive"
              render={({ field }) => (
                <FormItem className="flex items-center justify-between rounded-lg border p-3">
                  <div className="space-y-0.5">
                    <FormLabel>Active</FormLabel>
                    <FormDescription>
                      Active courses can be assigned to employees.
                    </FormDescription>
                  </div>
                  <FormControl>
                    <Switch checked={field.value} onCheckedChange={field.onChange} />
                  </FormControl>
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="requireSequentialCompletion"
              render={({ field }) => (
                <FormItem className="flex items-center justify-between rounded-lg border p-3">
                  <div className="space-y-0.5">
                    <FormLabel>Sequential Completion</FormLabel>
                    <FormDescription>
                      Employees must complete talks in order. They cannot skip ahead.
                    </FormDescription>
                  </div>
                  <FormControl>
                    <Switch checked={field.value} onCheckedChange={field.onChange} />
                  </FormControl>
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="generateCertificate"
              render={({ field }) => (
                <FormItem className="flex items-center justify-between rounded-lg border p-3">
                  <div className="space-y-0.5">
                    <FormLabel>Generate Certificate</FormLabel>
                    <FormDescription>
                      Issue a completion certificate when an employee finishes all talks.
                    </FormDescription>
                  </div>
                  <FormControl>
                    <Switch checked={field.value} onCheckedChange={field.onChange} />
                  </FormControl>
                </FormItem>
              )}
            />

            <FormField
              control={form.control}
              name="requiresRefresher"
              render={({ field }) => (
                <FormItem className="flex items-center justify-between rounded-lg border p-3">
                  <div className="space-y-0.5">
                    <FormLabel>Requires Refresher</FormLabel>
                    <FormDescription>
                      Employees must retake this course after a set period.
                    </FormDescription>
                  </div>
                  <FormControl>
                    <Switch checked={field.value} onCheckedChange={field.onChange} />
                  </FormControl>
                </FormItem>
              )}
            />

            {requiresRefresher && (
              <FormField
                control={form.control}
                name="refresherIntervalMonths"
                render={({ field }) => (
                  <FormItem className="ml-4">
                    <FormLabel>Refresher Interval (months)</FormLabel>
                    <FormControl>
                      <Input
                        type="number"
                        min={1}
                        max={120}
                        className="w-32"
                        {...field}
                        onChange={(e) => field.onChange(Number(e.target.value))}
                      />
                    </FormControl>
                    <FormDescription>
                      How many months before the course must be retaken.
                    </FormDescription>
                    <FormMessage />
                  </FormItem>
                )}
              />
            )}

            <FormField
              control={form.control}
              name="autoAssignToNewEmployees"
              render={({ field }) => (
                <FormItem className="flex items-center justify-between rounded-lg border p-3">
                  <div className="space-y-0.5">
                    <FormLabel>Auto-Assign to New Employees</FormLabel>
                    <FormDescription>
                      Automatically assign this course when a new employee is created.
                    </FormDescription>
                  </div>
                  <FormControl>
                    <Switch checked={field.value} onCheckedChange={field.onChange} />
                  </FormControl>
                </FormItem>
              )}
            />

            {autoAssignToNewEmployees && (
              <FormField
                control={form.control}
                name="autoAssignDueDays"
                render={({ field }) => (
                  <FormItem className="ml-4">
                    <FormLabel>Due Days</FormLabel>
                    <FormControl>
                      <Input
                        type="number"
                        min={1}
                        max={365}
                        className="w-32"
                        {...field}
                        onChange={(e) => field.onChange(Number(e.target.value))}
                      />
                    </FormControl>
                    <FormDescription>
                      Number of days after employee start date for the course to be due.
                    </FormDescription>
                    <FormMessage />
                  </FormItem>
                )}
              />
            )}
          </div>

          {/* Course Talks */}
          <div className="space-y-4 rounded-lg border p-6">
            <div className="flex items-center justify-between">
              <div>
                <h2 className="text-lg font-medium">Learnings</h2>
                <p className="text-sm text-muted-foreground">
                  {courseItems.length === 0
                    ? 'Add learnings to this course.'
                    : `${courseItems.length} learning${courseItems.length !== 1 ? 's' : ''} in this course. Drag to reorder.`}
                </p>
              </div>
              <Button type="button" variant="outline" onClick={() => setAddTalksOpen(true)}>
                <PlusIcon className="mr-2 h-4 w-4" />
                Add Learnings
              </Button>
            </div>

            {courseItems.length > 0 ? (
              <DndContext
                sensors={sensors}
                collisionDetection={closestCenter}
                modifiers={[restrictToVerticalAxis]}
                onDragEnd={handleDragEnd}
              >
                <SortableContext
                  items={courseItems.map((i) => i.toolboxTalkId)}
                  strategy={verticalListSortingStrategy}
                >
                  <div className="space-y-2">
                    {courseItems.map((item, index) => (
                      <SortableItem
                        key={item.toolboxTalkId}
                        item={item}
                        index={index}
                        onToggleRequired={handleToggleRequired}
                        onRemove={handleRemoveItem}
                      />
                    ))}
                  </div>
                </SortableContext>
              </DndContext>
            ) : (
              <div className="rounded-md border border-dashed p-8 text-center">
                <p className="text-sm text-muted-foreground">
                  No talks added yet. Click &quot;Add Learnings&quot; to select learnings for this course.
                </p>
              </div>
            )}
          </div>

          {/* Course Assignments (only when editing) */}
          {isEditing && course && (
            <div className="space-y-4 rounded-lg border p-6">
              <div className="flex items-center justify-between">
                <div>
                  <h2 className="text-lg font-medium">Assignments</h2>
                  <p className="text-sm text-muted-foreground">
                    Manage employee assignments for this course.
                  </p>
                </div>
                <Button type="button" variant="outline" onClick={() => setAssignDialogOpen(true)}>
                  <UsersIcon className="mr-2 h-4 w-4" />
                  Assign Employees
                </Button>
              </div>

              <CourseAssignmentsList courseId={course.id} />
            </div>
          )}

          {/* Form Actions */}
          <div className="flex items-center gap-4">
            <Button type="submit" disabled={isSaving}>
              {isSaving ? (
                <>
                  <LoadingSpinner className="mr-2 h-4 w-4" />
                  {isEditing ? 'Saving...' : 'Creating...'}
                </>
              ) : (
                isEditing ? 'Save Changes' : 'Create Course'
              )}
            </Button>
            <Button
              type="button"
              variant="outline"
              onClick={() => router.push('/admin/toolbox-talks/courses')}
            >
              Cancel
            </Button>
          </div>
        </form>
      </Form>

      <AddTalksDialog
        open={addTalksOpen}
        onOpenChange={setAddTalksOpen}
        excludeTalkIds={courseItems.map((i) => i.toolboxTalkId)}
        onAdd={handleAddTalks}
      />

      {isEditing && course && (
        <AssignCourseDialog
          course={{ id: course.id, title: course.title }}
          open={assignDialogOpen}
          onOpenChange={setAssignDialogOpen}
        />
      )}
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
