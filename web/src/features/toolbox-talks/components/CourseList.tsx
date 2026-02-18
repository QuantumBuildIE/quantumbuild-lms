'use client';

import { useState, useEffect, useCallback } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { format } from 'date-fns';
import { PlusIcon, PencilIcon, TrashIcon, SearchIcon } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
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
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { DataTable, type Column } from '@/components/shared/data-table';
import { DeleteConfirmationDialog } from '@/components/shared/delete-confirmation-dialog';
import { useToolboxTalkCourses, useDeleteToolboxTalkCourse } from '@/lib/api/toolbox-talks/use-courses';
import { usePermission } from '@/lib/auth/use-auth';
import type { ToolboxTalkCourseListDto } from '@/lib/api/toolbox-talks/courses';
import { toast } from 'sonner';
import { cn } from '@/lib/utils';

const STATUS_OPTIONS = [
  { value: 'all', label: 'All Status' },
  { value: 'true', label: 'Active' },
  { value: 'false', label: 'Inactive' },
];

export function CourseList() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const canManage = usePermission('Learnings.Manage');

  const searchTerm = searchParams.get('search') || '';
  const activeFilter = searchParams.get('active');

  const [localSearch, setLocalSearch] = useState(searchTerm);
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [courseToDelete, setCourseToDelete] = useState<ToolboxTalkCourseListDto | null>(null);

  const isActiveFilter = activeFilter === 'true' ? true : activeFilter === 'false' ? false : undefined;

  const { data, isLoading, error } = useToolboxTalkCourses({
    searchTerm: searchTerm || undefined,
    isActive: isActiveFilter,
  });

  const deleteMutation = useDeleteToolboxTalkCourse();

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
      router.push(`?${params.toString()}`);
    },
    [router, searchParams]
  );

  useEffect(() => {
    const timer = setTimeout(() => {
      if (localSearch !== searchTerm) {
        updateParams({ search: localSearch || null });
      }
    }, 300);
    return () => clearTimeout(timer);
  }, [localSearch, searchTerm, updateParams]);

  const handleDelete = async () => {
    if (!courseToDelete) return;

    try {
      await deleteMutation.mutateAsync(courseToDelete.id);
      toast.success('Course deleted successfully');
      setDeleteDialogOpen(false);
      setCourseToDelete(null);
    } catch (error: unknown) {
      let message = 'Failed to delete course';
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

  const columns: Column<ToolboxTalkCourseListDto>[] = [
    {
      key: 'title',
      header: 'Title',
      sortable: true,
      className: 'max-w-[300px]',
      headerClassName: 'w-[300px]',
      render: (item) => (
        <div className="flex flex-col min-w-0">
          <span className="font-medium truncate" title={item.title}>
            {item.title}
          </span>
          {item.description && (
            <span className="text-sm text-muted-foreground line-clamp-1" title={item.description}>
              {item.description}
            </span>
          )}
        </div>
      ),
    },
    {
      key: 'talkCount',
      header: 'Learnings',
      className: 'text-center',
      headerClassName: 'text-center',
      render: (item) => (
        <span className="text-muted-foreground">{item.talkCount}</span>
      ),
    },
    {
      key: 'requireSequentialCompletion',
      header: 'Sequential',
      className: 'text-center',
      headerClassName: 'text-center',
      render: (item) => (
        <Badge variant="outline" className="text-xs">
          {item.requireSequentialCompletion ? 'Yes' : 'No'}
        </Badge>
      ),
    },
    {
      key: 'translationCount',
      header: 'Translations',
      className: 'text-center',
      headerClassName: 'text-center',
      render: (item) => (
        <span className="text-muted-foreground">{item.translationCount}</span>
      ),
    },
    {
      key: 'isActive',
      header: 'Status',
      render: (item) => (
        <Badge
          variant={item.isActive ? 'default' : 'secondary'}
          className={cn(
            item.isActive
              ? 'bg-green-100 text-green-800 hover:bg-green-100 dark:bg-green-900/20 dark:text-green-400'
              : 'bg-gray-100 text-gray-800 hover:bg-gray-100 dark:bg-gray-900/20 dark:text-gray-400'
          )}
        >
          {item.isActive ? 'Active' : 'Inactive'}
        </Badge>
      ),
    },
    {
      key: 'createdAt',
      header: 'Created',
      sortable: true,
      render: (item) => (
        <span className="text-muted-foreground text-sm">
          {format(new Date(item.createdAt), 'dd MMM yyyy')}
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
            {canManage && (
              <DropdownMenuItem onClick={() => router.push(`/admin/toolbox-talks/courses/${item.id}/edit`)}>
                <PencilIcon className="mr-2 h-4 w-4" />
                Edit
              </DropdownMenuItem>
            )}
            {canManage && (
              <DropdownMenuItem
                className="text-destructive focus:text-destructive"
                onClick={() => {
                  setCourseToDelete(item);
                  setDeleteDialogOpen(true);
                }}
              >
                <TrashIcon className="mr-2 h-4 w-4" />
                Delete
              </DropdownMenuItem>
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
          Error loading courses: {error instanceof Error ? error.message : 'Unknown error'}
        </p>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex flex-1 flex-col gap-4 sm:flex-row sm:items-center">
          <div className="relative flex-1 sm:max-w-xs">
            <SearchIcon className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              placeholder="Search courses..."
              value={localSearch}
              onChange={(e) => setLocalSearch(e.target.value)}
              className="pl-9"
            />
          </div>

          <Select
            value={activeFilter || 'all'}
            onValueChange={(value) => updateParams({ active: value === 'all' ? null : value })}
          >
            <SelectTrigger className="w-[140px]">
              <SelectValue placeholder="Status" />
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

        {canManage && (
          <Button onClick={() => router.push('/admin/toolbox-talks/courses/new')}>
            <PlusIcon className="mr-2 h-4 w-4" />
            Create Course
          </Button>
        )}
      </div>

      <DataTable
        columns={columns}
        data={data || []}
        isLoading={isLoading}
        emptyMessage="No courses found. Create your first course to group learnings together."
        keyExtractor={(item) => item.id}
      />

      <DeleteConfirmationDialog
        open={deleteDialogOpen}
        onOpenChange={setDeleteDialogOpen}
        title="Delete Course"
        description={`Are you sure you want to delete "${courseToDelete?.title}"? This action cannot be undone.`}
        onConfirm={handleDelete}
        isLoading={deleteMutation.isPending}
      />
    </div>
  );
}
