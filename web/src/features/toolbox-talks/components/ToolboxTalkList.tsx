'use client';

import { useState, useEffect, useCallback } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { format } from 'date-fns';
import { PlusIcon, EyeIcon, PencilIcon, TrashIcon, CalendarClockIcon, SearchIcon } from 'lucide-react';
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
import { useToolboxTalks, useDeleteToolboxTalk } from '@/lib/api/toolbox-talks';
import type {
  ToolboxTalkListItem,
  ToolboxTalkFrequency,
} from '@/types/toolbox-talks';
import { toast } from 'sonner';
import { cn } from '@/lib/utils';

interface ToolboxTalkListProps {
  onSchedule?: (talk: ToolboxTalkListItem) => void;
  /** Base path for navigation (default: /admin/toolbox-talks) */
  basePath?: string;
}

const FREQUENCY_OPTIONS: { value: ToolboxTalkFrequency | 'all'; label: string }[] = [
  { value: 'all', label: 'All Frequencies' },
  { value: 'Once', label: 'Once' },
  { value: 'Weekly', label: 'Weekly' },
  { value: 'Monthly', label: 'Monthly' },
  { value: 'Annually', label: 'Annually' },
];

const STATUS_OPTIONS = [
  { value: 'all', label: 'All Status' },
  { value: 'true', label: 'Active' },
  { value: 'false', label: 'Inactive' },
];

export function ToolboxTalkList({ onSchedule, basePath = '/admin/toolbox-talks' }: ToolboxTalkListProps) {
  const router = useRouter();
  const searchParams = useSearchParams();

  // URL params state
  const page = Number(searchParams.get('page')) || 1;
  const pageSize = Number(searchParams.get('size')) || 20;
  const searchTerm = searchParams.get('search') || '';
  const frequencyFilter = (searchParams.get('frequency') as ToolboxTalkFrequency) || undefined;
  const activeFilter = searchParams.get('active');

  // Local state for debounced search
  const [localSearch, setLocalSearch] = useState(searchTerm);
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [talkToDelete, setTalkToDelete] = useState<ToolboxTalkListItem | null>(null);

  // Parse active filter
  const isActiveFilter = activeFilter === 'true' ? true : activeFilter === 'false' ? false : undefined;

  // Fetch data
  const { data, isLoading, error } = useToolboxTalks({
    searchTerm: searchTerm || undefined,
    frequency: frequencyFilter,
    isActive: isActiveFilter,
    pageNumber: page,
    pageSize,
  });

  const deleteMutation = useDeleteToolboxTalk();

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
      if (!updates.hasOwnProperty('page')) {
        params.set('page', '1');
      }
      router.push(`?${params.toString()}`);
    },
    [router, searchParams]
  );

  // Debounced search
  useEffect(() => {
    const timer = setTimeout(() => {
      if (localSearch !== searchTerm) {
        updateParams({ search: localSearch || null });
      }
    }, 300);
    return () => clearTimeout(timer);
  }, [localSearch, searchTerm, updateParams]);

  // Handle delete
  const handleDelete = async () => {
    if (!talkToDelete) return;

    try {
      await deleteMutation.mutateAsync(talkToDelete.id);
      toast.success('Learning deleted successfully');
      setDeleteDialogOpen(false);
      setTalkToDelete(null);
    } catch (error: unknown) {
      let message = 'Failed to delete learning';
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

  const columns: Column<ToolboxTalkListItem>[] = [
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
      key: 'frequency',
      header: 'Frequency',
      sortable: true,
      render: (item) => (
        <Badge variant="outline">{item.frequencyDisplay}</Badge>
      ),
    },
    {
      key: 'isActive',
      header: 'Active',
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
      key: 'sectionCount',
      header: 'Sections',
      className: 'text-center',
      headerClassName: 'text-center',
      render: (item) => (
        <span className="text-muted-foreground">{item.sectionCount}</span>
      ),
    },
    {
      key: 'questionCount',
      header: 'Questions',
      className: 'text-center',
      headerClassName: 'text-center',
      render: (item) => (
        <span className="text-muted-foreground">
          {item.requiresQuiz ? item.questionCount : '-'}
        </span>
      ),
    },
    {
      key: 'completionStats',
      header: 'Assignments',
      render: (item) => {
        if (!item.completionStats) {
          return <span className="text-muted-foreground">-</span>;
        }
        const stats = item.completionStats;
        return (
          <div className="flex items-center gap-2">
            <span className="text-sm">
              {stats.completedCount}/{stats.totalAssignments}
            </span>
            {stats.overdueCount > 0 && (
              <Badge variant="destructive" className="text-xs">
                {stats.overdueCount} overdue
              </Badge>
            )}
          </div>
        );
      },
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
            <DropdownMenuItem onClick={() => router.push(`${basePath}/talks/${item.id}`)}>
              <EyeIcon className="mr-2 h-4 w-4" />
              View
            </DropdownMenuItem>
            <DropdownMenuItem onClick={() => router.push(`${basePath}/talks/${item.id}/edit`)}>
              <PencilIcon className="mr-2 h-4 w-4" />
              Edit
            </DropdownMenuItem>
            <DropdownMenuItem
              onClick={() => onSchedule?.(item)}
              disabled={!item.isActive}
              title={!item.isActive ? 'Only active talks can be scheduled' : undefined}
            >
              <CalendarClockIcon className="mr-2 h-4 w-4" />
              Schedule
              {!item.isActive && (
                <span className="ml-1 text-xs text-muted-foreground">(inactive)</span>
              )}
            </DropdownMenuItem>
            <DropdownMenuItem
              className="text-destructive focus:text-destructive"
              onClick={() => {
                setTalkToDelete(item);
                setDeleteDialogOpen(true);
              }}
            >
              <TrashIcon className="mr-2 h-4 w-4" />
              Delete
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      ),
    },
  ];

  if (error) {
    return (
      <div className="rounded-lg border border-destructive/50 bg-destructive/10 p-4">
        <p className="text-destructive">
          Error loading learnings: {error instanceof Error ? error.message : 'Unknown error'}
        </p>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {/* Header with filters */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex flex-1 flex-col gap-4 sm:flex-row sm:items-center">
          {/* Search */}
          <div className="relative flex-1 sm:max-w-xs">
            <SearchIcon className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              placeholder="Search talks..."
              value={localSearch}
              onChange={(e) => setLocalSearch(e.target.value)}
              className="pl-9"
            />
          </div>

          {/* Frequency filter */}
          <Select
            value={frequencyFilter || 'all'}
            onValueChange={(value) => updateParams({ frequency: value === 'all' ? null : value })}
          >
            <SelectTrigger className="w-[160px]">
              <SelectValue placeholder="Frequency" />
            </SelectTrigger>
            <SelectContent>
              {FREQUENCY_OPTIONS.map((option) => (
                <SelectItem key={option.value} value={option.value}>
                  {option.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>

          {/* Active filter */}
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

        {/* Create button */}
        <Button onClick={() => router.push(`${basePath}/talks/new`)}>
          <PlusIcon className="mr-2 h-4 w-4" />
          Create New
        </Button>
      </div>

      {/* Data table */}
      <DataTable
        columns={columns}
        data={data?.items || []}
        isLoading={isLoading}
        emptyMessage="No learnings found"
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
        onPageChange={(newPage) => updateParams({ page: String(newPage) })}
        onPageSizeChange={(newSize) => updateParams({ size: String(newSize), page: '1' })}
      />

      {/* Delete confirmation dialog */}
      <DeleteConfirmationDialog
        open={deleteDialogOpen}
        onOpenChange={setDeleteDialogOpen}
        title="Delete Learning"
        description={`Are you sure you want to delete "${talkToDelete?.title}"? This action cannot be undone.`}
        onConfirm={handleDelete}
        isLoading={deleteMutation.isPending}
      />
    </div>
  );
}
