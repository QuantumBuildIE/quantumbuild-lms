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
import { ChevronLeft, ChevronRight } from 'lucide-react';
import type {
  SkillsMatrix,
  SkillsMatrixCell,
  SkillsMatrixCellStatus,
  SkillsMatrixLearning,
} from '@/types/toolbox-talks';

const PAGE_SIZE = 25;

const STATUS_CONFIG: Record<
  SkillsMatrixCellStatus,
  { bg: string; text: string; label: string }
> = {
  Completed: { bg: 'bg-emerald-100', text: 'text-emerald-700', label: 'Completed' },
  InProgress: { bg: 'bg-blue-100', text: 'text-blue-700', label: 'In Progress' },
  Overdue: { bg: 'bg-red-100', text: 'text-red-700', label: 'Overdue' },
  Assigned: { bg: 'bg-sky-100', text: 'text-sky-700', label: 'Assigned' },
  NotAssigned: { bg: 'bg-gray-50', text: 'text-gray-400', label: 'Not Assigned' },
};

function CellContent({ cell }: { cell: SkillsMatrixCell | undefined }) {
  if (!cell || cell.status === 'NotAssigned') {
    return (
      <span className="text-gray-400">&mdash;</span>
    );
  }

  const config = STATUS_CONFIG[cell.status as SkillsMatrixCellStatus] ?? STATUS_CONFIG.NotAssigned;

  let displayText = '';
  if (cell.status === 'Completed' && cell.score != null) {
    displayText = `${cell.score}%`;
  } else if (cell.status === 'Overdue' && cell.daysOverdue != null) {
    displayText = `${cell.daysOverdue}d`;
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
          className={`flex items-center justify-center h-full w-full rounded px-1.5 py-1 text-xs font-medium ${config.bg} ${config.text}`}
        >
          {displayText || config.label.charAt(0)}
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
    <div className="flex flex-wrap gap-3 text-xs">
      {Object.entries(STATUS_CONFIG).map(([status, config]) => (
        <div key={status} className="flex items-center gap-1.5">
          <div className={`h-3 w-3 rounded ${config.bg} border border-gray-200`} />
          <span className="text-muted-foreground">{config.label}</span>
        </div>
      ))}
    </div>
  );
}

interface SkillsMatrixGridProps {
  data: SkillsMatrix | undefined;
  isLoading: boolean;
}

export function SkillsMatrixGrid({ data, isLoading }: SkillsMatrixGridProps) {
  const [page, setPage] = useState(0);

  // Build a cell lookup map for O(1) access
  const cellMap = useMemo(() => {
    if (!data?.cells) return new Map<string, SkillsMatrixCell>();
    const map = new Map<string, SkillsMatrixCell>();
    for (const cell of data.cells) {
      map.set(`${cell.employeeId}:${cell.learningId}`, cell);
    }
    return map;
  }, [data?.cells]);

  // Group learnings by category
  const groupedLearnings = useMemo(() => {
    if (!data?.learnings) return [];
    const groups = new Map<string, SkillsMatrixLearning[]>();
    for (const learning of data.learnings) {
      const cat = learning.category || 'Uncategorized';
      if (!groups.has(cat)) groups.set(cat, []);
      groups.get(cat)!.push(learning);
    }
    return Array.from(groups.entries()).map(([category, learnings]) => ({
      category,
      learnings,
    }));
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

  return (
    <TooltipProvider delayDuration={200}>
      <div className="space-y-4">
        <Legend />

        <div className="relative overflow-x-auto border rounded-lg">
          <table className="w-full text-sm">
            <thead>
              {/* Category header row */}
              {hasMultipleCategories && (
                <tr className="border-b bg-muted/30">
                  <th className="sticky left-0 z-20 bg-muted/30 border-r min-w-[200px]" />
                  {groupedLearnings.map((group) => (
                    <th
                      key={group.category}
                      colSpan={group.learnings.length}
                      className="px-2 py-1.5 text-center text-xs font-semibold text-muted-foreground border-r last:border-r-0"
                    >
                      {group.category}
                    </th>
                  ))}
                </tr>
              )}
              {/* Learning code header row */}
              <tr className="border-b bg-muted/50">
                <th className="sticky left-0 z-20 bg-muted/50 border-r px-3 py-2 text-left text-xs font-semibold min-w-[200px]">
                  Employee
                </th>
                {allLearnings.map((learning) => (
                  <Tooltip key={learning.id}>
                    <TooltipTrigger asChild>
                      <th className="px-1.5 py-2 text-center text-xs font-medium min-w-[70px] max-w-[90px] cursor-help">
                        <span className="font-mono">{learning.code}</span>
                      </th>
                    </TooltipTrigger>
                    <TooltipContent side="bottom">
                      <p className="font-semibold">{learning.title}</p>
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
                  className={idx % 2 === 0 ? 'bg-background' : 'bg-muted/20'}
                >
                  <td className={`sticky left-0 z-10 border-r px-3 py-1.5 ${idx % 2 === 0 ? 'bg-background' : 'bg-muted/20'}`}>
                    <div className="flex flex-col">
                      <span className="font-medium text-sm truncate max-w-[180px]">
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
                      className="px-1 py-1 text-center"
                    >
                      <CellContent
                        cell={cellMap.get(`${employee.id}:${learning.id}`)}
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
