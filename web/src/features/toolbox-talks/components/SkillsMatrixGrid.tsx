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
  { bg: string; text: string; label: string; hover: string }
> = {
  Completed: { bg: 'bg-emerald-200', text: 'text-emerald-800', label: 'Completed', hover: 'hover:bg-emerald-300' },
  InProgress: { bg: 'bg-blue-200', text: 'text-blue-800', label: 'In Progress', hover: 'hover:bg-blue-300' },
  Overdue: { bg: 'bg-red-200', text: 'text-red-800', label: 'Overdue', hover: 'hover:bg-red-300' },
  Assigned: { bg: 'bg-amber-200', text: 'text-amber-800', label: 'Assigned', hover: 'hover:bg-amber-300' },
  NotAssigned: { bg: 'bg-gray-100', text: 'text-gray-400', label: 'Not Assigned', hover: '' },
};

function CellContent({ cell }: { cell: SkillsMatrixCell | undefined }) {
  if (!cell || cell.status === 'NotAssigned') {
    return (
      <span className="text-gray-400 text-sm">&mdash;</span>
    );
  }

  const config = STATUS_CONFIG[cell.status as SkillsMatrixCellStatus] ?? STATUS_CONFIG.NotAssigned;

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
                      <th className="px-2 py-2.5 text-center min-w-[100px] max-w-[120px] cursor-help border-r border-gray-200 last:border-r-0">
                        <span className="font-mono font-semibold text-sm">{learning.code}</span>
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
                      className="px-1 py-1 text-center h-12 border-r border-gray-200 last:border-r-0"
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
