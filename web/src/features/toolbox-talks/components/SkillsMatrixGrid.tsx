'use client';

import { useMemo, useState } from 'react';
import { format } from 'date-fns';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/tooltip';
import { Skeleton } from '@/components/ui/skeleton';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { ChevronLeft, ChevronRight, LayoutGrid, Table2 } from 'lucide-react';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  MultiSelectCombobox,
  type MultiSelectOption,
  type MultiSelectGroup,
} from '@/components/ui/multi-select-combobox';
import type {
  SkillsMatrix,
  SkillsMatrixCell,
  SkillsMatrixCellStatus,
  SkillsMatrixLearning,
} from '@/types/toolbox-talks';

const PAGE_SIZE = 25;

const STATUS_CONFIG: Record<
  SkillsMatrixCellStatus,
  { bg: string; text: string; label: string; hover: string; compactChar: string }
> = {
  Completed: { bg: 'bg-emerald-200', text: 'text-emerald-800', label: 'Completed', hover: 'hover:bg-emerald-300', compactChar: '✓' },
  InProgress: { bg: 'bg-blue-200', text: 'text-blue-800', label: 'In Progress', hover: 'hover:bg-blue-300', compactChar: '●' },
  Overdue: { bg: 'bg-red-200', text: 'text-red-800', label: 'Overdue', hover: 'hover:bg-red-300', compactChar: '⚠' },
  Assigned: { bg: 'bg-amber-200', text: 'text-amber-800', label: 'Assigned', hover: 'hover:bg-amber-300', compactChar: 'P' },
  NotAssigned: { bg: 'bg-gray-100', text: 'text-gray-400', label: 'Not Assigned', hover: '', compactChar: '—' },
};

function CellContent({ cell, compact }: { cell: SkillsMatrixCell | undefined; compact: boolean }) {
  if (!cell || cell.status === 'NotAssigned') {
    if (compact) {
      return <span className="text-gray-400 text-xs">&mdash;</span>;
    }
    return <span className="text-gray-400 text-sm">&mdash;</span>;
  }

  const config = STATUS_CONFIG[cell.status as SkillsMatrixCellStatus] ?? STATUS_CONFIG.NotAssigned;

  if (compact) {
    let tooltipText = config.label;
    if (cell.status === 'Completed') {
      if (cell.score != null) tooltipText += ` — ${cell.score}%`;
      if (cell.completedAt) tooltipText += ` on ${format(new Date(cell.completedAt), 'PP')}`;
    } else if (cell.dueDate) {
      tooltipText += ` — Due ${format(new Date(cell.dueDate), 'PP')}`;
    }
    if (cell.status === 'Overdue' && cell.daysOverdue != null) {
      tooltipText += ` (${cell.daysOverdue}d overdue)`;
    }

    return (
      <Tooltip>
        <TooltipTrigger asChild>
          <div
            className={`flex items-center justify-center h-full w-full rounded text-xs font-bold cursor-pointer transition-colors ${config.bg} ${config.text} ${config.hover}`}
          >
            {config.compactChar}
          </div>
        </TooltipTrigger>
        <TooltipContent>
          <p>{tooltipText}</p>
        </TooltipContent>
      </Tooltip>
    );
  }

  let displayText: React.ReactNode = '';
  if (cell.status === 'Completed') {
    displayText = cell.score != null
      ? <><span className="font-bold">&#10003;</span> {cell.score}%</>
      : <span className="font-bold">&#10003;</span>;
  } else if (cell.status === 'InProgress') {
    displayText = 'In Progress';
  } else if (cell.status === 'Overdue') {
    displayText = cell.daysOverdue != null
      ? <span className="font-bold">&#9888; {cell.daysOverdue}d</span>
      : <span className="font-bold">Overdue</span>;
  } else if (cell.status === 'Assigned') {
    displayText = 'Pending';
  }

  let tooltipText = config.label;
  if (cell.status === 'Completed' && cell.completedAt) {
    tooltipText += ` on ${format(new Date(cell.completedAt), 'PP')}`;
  } else if (cell.dueDate) {
    tooltipText += ` — Due ${format(new Date(cell.dueDate), 'PP')}`;
  }

  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <div
          className={`flex items-center justify-center h-full w-full rounded px-3 py-2 text-sm font-medium cursor-pointer transition-colors ${config.bg} ${config.text} ${config.hover}`}
        >
          {displayText}
        </div>
      </TooltipTrigger>
      <TooltipContent>
        <p>{tooltipText}</p>
      </TooltipContent>
    </Tooltip>
  );
}

function Legend() {
  return (
    <div className="flex flex-wrap gap-4 text-sm">
      {Object.entries(STATUS_CONFIG).map(([status, config]) => (
        <div key={status} className="flex items-center gap-2">
          <div className={`h-4 w-4 rounded ${config.bg} border border-gray-200`} />
          <span className="text-muted-foreground">{config.label}</span>
        </div>
      ))}
    </div>
  );
}

export interface CategoryFilterProps {
  categories: { id: string; name: string; isActive: boolean }[];
  selectedCategory?: string;
  onCategoryChange: (category: string | undefined) => void;
  isLoading: boolean;
}

interface SkillsMatrixGridProps {
  data: SkillsMatrix | undefined;
  isLoading: boolean;
  categoryFilter?: CategoryFilterProps;
}

export function SkillsMatrixGrid({ data, isLoading, categoryFilter }: SkillsMatrixGridProps) {
  const [page, setPage] = useState(0);
  const [compactOverride, setCompactOverride] = useState<boolean | null>(null);
  const [selectedLearningIds, setSelectedLearningIds] = useState<string[]>([]);

  // Default compact mode: compact if > 6 learnings
  const defaultCompact = (data?.learnings?.length ?? 0) > 6;
  const compact = compactOverride ?? defaultCompact;

  // Build a cell lookup map for O(1) access
  const cellMap = useMemo(() => {
    if (!data?.cells) return new Map<string, SkillsMatrixCell>();
    const map = new Map<string, SkillsMatrixCell>();
    for (const cell of data.cells) {
      map.set(`${cell.employeeId}:${cell.learningId}`, cell);
    }
    return map;
  }, [data?.cells]);

  // Filter learnings by selected IDs (empty = show all)
  const filteredLearnings = useMemo(() => {
    if (!data?.learnings) return [];
    if (selectedLearningIds.length === 0) return data.learnings;
    const selectedSet = new Set(selectedLearningIds);
    const filtered = data.learnings.filter((l) => selectedSet.has(l.id));
    // If none match (e.g. category filter changed), show all
    return filtered.length > 0 ? filtered : data.learnings;
  }, [data?.learnings, selectedLearningIds]);

  // Group filtered learnings by category
  const groupedLearnings = useMemo(() => {
    const groups = new Map<string, SkillsMatrixLearning[]>();
    for (const learning of filteredLearnings) {
      const cat = learning.category || 'Uncategorized';
      if (!groups.has(cat)) groups.set(cat, []);
      groups.get(cat)!.push(learning);
    }
    return Array.from(groups.entries()).map(([category, learnings]) => ({
      category,
      learnings,
    }));
  }, [filteredLearnings]);

  // Build learning selector options grouped by category
  const { learningOptions, learningGroups } = useMemo(() => {
    if (!data?.learnings) return { learningOptions: [] as MultiSelectOption[], learningGroups: [] as MultiSelectGroup[] };
    const groups = new Map<string, MultiSelectOption[]>();
    const allOpts: MultiSelectOption[] = [];
    for (const l of data.learnings) {
      const cat = l.category || 'Uncategorized';
      if (!groups.has(cat)) groups.set(cat, []);
      const opt: MultiSelectOption = {
        value: l.id,
        label: `${l.code} — ${l.title}`,
      };
      groups.get(cat)!.push(opt);
      allOpts.push(opt);
    }
    return {
      learningOptions: allOpts,
      learningGroups: Array.from(groups.entries()).map(([label, options]) => ({
        label,
        options,
      })),
    };
  }, [data?.learnings]);

  const hasMultipleCategories = groupedLearnings.length > 1;
  const totalEmployees = data?.employees.length ?? 0;
  const totalPages = Math.ceil(totalEmployees / PAGE_SIZE);
  const paginatedEmployees = data?.employees.slice(
    page * PAGE_SIZE,
    (page + 1) * PAGE_SIZE
  ) ?? [];

  // Reset page if data changes and page is out of bounds
  if (page > 0 && page >= totalPages) {
    setPage(0);
  }

  if (isLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-6 w-48" />
        <Skeleton className="h-[400px] w-full" />
      </div>
    );
  }

  if (!data || data.employees.length === 0 || data.learnings.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center py-12 text-center">
        <p className="text-muted-foreground">No learning assignments found</p>
        <p className="text-sm text-muted-foreground mt-1">
          Assignments will appear here once learnings are scheduled to employees.
        </p>
      </div>
    );
  }

  const allLearnings = groupedLearnings.flatMap((g) => g.learnings);
  const totalLearnings = data.learnings.length;
  const isFilteringLearnings = selectedLearningIds.length > 0 && selectedLearningIds.length < totalLearnings;

  return (
    <TooltipProvider delayDuration={200}>
      <div className="space-y-4">
        {/* Controls row: Legend on left, controls on right */}
        <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
          <Legend />
          <div className="flex flex-wrap items-center gap-2">
            {/* Compact / Full toggle */}
            <Tooltip>
              <TooltipTrigger asChild>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => setCompactOverride(!compact)}
                  className="h-9 w-9 p-0"
                >
                  {compact ? <LayoutGrid className="h-4 w-4" /> : <Table2 className="h-4 w-4" />}
                </Button>
              </TooltipTrigger>
              <TooltipContent>
                {compact ? 'Switch to full view' : 'Switch to compact view'}
              </TooltipContent>
            </Tooltip>

            {/* Category filter (passed from parent) */}
            {categoryFilter && (
              <Select
                value={categoryFilter.selectedCategory || 'all'}
                onValueChange={(value) =>
                  categoryFilter.onCategoryChange(value === 'all' ? undefined : value)
                }
                disabled={categoryFilter.isLoading}
              >
                <SelectTrigger className="w-[180px] h-9">
                  <SelectValue placeholder="All Categories" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All Categories</SelectItem>
                  {categoryFilter.categories
                    .filter((c) => c.isActive)
                    .map((cat) => (
                      <SelectItem key={cat.id} value={cat.name}>
                        {cat.name}
                      </SelectItem>
                    ))}
                </SelectContent>
              </Select>
            )}

            {/* Learning selector */}
            <MultiSelectCombobox
              options={learningOptions}
              groups={learningGroups.length > 1 ? learningGroups : undefined}
              selectedValues={selectedLearningIds}
              onValuesChange={(values) => setSelectedLearningIds(values)}
              placeholder="All Learnings"
              searchPlaceholder="Search learnings..."
              emptyText="No learnings found."
              maxDisplayItems={0}
              showSelectAll
              className="w-[200px] h-9"
              renderTriggerContent={
                isFilteringLearnings
                  ? () => (
                      <span className="flex items-center gap-1.5 text-sm">
                        Learnings
                        <Badge variant="secondary" className="px-1.5 py-0 text-xs">
                          {selectedLearningIds.length} of {totalLearnings}
                        </Badge>
                      </span>
                    )
                  : undefined
              }
            />
          </div>
        </div>

        <div className="relative overflow-x-auto rounded-lg shadow-sm border border-gray-200">
          <table className="w-full text-sm border-collapse">
            <thead>
              {/* Category header row */}
              {hasMultipleCategories && (
                <tr className="border-b border-gray-200 bg-gray-100">
                  <th className="sticky left-0 z-20 bg-gray-100 border-r-2 border-gray-300 min-w-[220px]" />
                  {groupedLearnings.map((group) => (
                    <th
                      key={group.category}
                      colSpan={group.learnings.length}
                      className="px-3 py-2 text-center text-sm font-semibold text-gray-700 border-r border-gray-200 last:border-r-0"
                    >
                      {group.category}
                    </th>
                  ))}
                </tr>
              )}
              {/* Learning code header row */}
              <tr className="border-b border-gray-200 bg-white">
                <th className="sticky left-0 z-20 bg-white border-r-2 border-gray-300 px-3 py-2.5 text-left text-sm font-semibold min-w-[220px]">
                  Employee
                </th>
                {allLearnings.map((learning) => (
                  <Tooltip key={learning.id}>
                    <TooltipTrigger asChild>
                      <th className={`px-2 py-2.5 text-center cursor-help border-r border-gray-200 last:border-r-0 ${
                        compact ? 'min-w-[40px] max-w-[64px]' : 'min-w-[100px] max-w-[120px]'
                      }`}>
                        <span className={`font-mono font-semibold ${compact ? 'text-xs truncate block overflow-hidden' : 'text-sm'}`}>
                          {learning.code}
                        </span>
                      </th>
                    </TooltipTrigger>
                    <TooltipContent side="bottom">
                      <p className="font-semibold">{learning.code}</p>
                      <p>{learning.title}</p>
                      {learning.category && (
                        <p className="text-xs opacity-80">{learning.category}</p>
                      )}
                    </TooltipContent>
                  </Tooltip>
                ))}
              </tr>
            </thead>
            <tbody>
              {paginatedEmployees.map((employee, idx) => (
                <tr
                  key={employee.id}
                  className={`border-b border-gray-200 last:border-b-0 ${idx % 2 === 0 ? 'bg-white' : 'bg-gray-50/50'}`}
                >
                  <td className={`sticky left-0 z-10 border-r-2 border-gray-300 px-3 py-2 min-w-[220px] ${idx % 2 === 0 ? 'bg-white' : 'bg-gray-50/50'}`}>
                    <div className="flex flex-col">
                      <span className="font-medium text-sm truncate max-w-[200px]">
                        {employee.fullName}
                      </span>
                      <span className="text-xs text-muted-foreground font-mono">
                        {employee.employeeCode}
                      </span>
                    </div>
                  </td>
                  {allLearnings.map((learning) => (
                    <td
                      key={learning.id}
                      className={`px-1 py-1 text-center border-r border-gray-200 last:border-r-0 ${
                        compact ? 'h-10' : 'h-12'
                      }`}
                    >
                      <CellContent
                        cell={cellMap.get(`${employee.id}:${learning.id}`)}
                        compact={compact}
                      />
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {/* Pagination */}
        <div className="flex items-center justify-between text-sm text-muted-foreground">
          <span>
            Showing {page * PAGE_SIZE + 1}–
            {Math.min((page + 1) * PAGE_SIZE, totalEmployees)} of{' '}
            {totalEmployees} employees
          </span>
          {totalPages > 1 && (
            <div className="flex items-center gap-2">
              <Button
                variant="outline"
                size="sm"
                onClick={() => setPage((p) => Math.max(0, p - 1))}
                disabled={page === 0}
              >
                <ChevronLeft className="h-4 w-4" />
              </Button>
              <span>
                Page {page + 1} of {totalPages}
              </span>
              <Button
                variant="outline"
                size="sm"
                onClick={() => setPage((p) => Math.min(totalPages - 1, p + 1))}
                disabled={page >= totalPages - 1}
              >
                <ChevronRight className="h-4 w-4" />
              </Button>
            </div>
          )}
        </div>
      </div>
    </TooltipProvider>
  );
}
