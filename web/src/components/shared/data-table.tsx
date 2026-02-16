"use client";

import * as React from "react";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Button } from "@/components/ui/button";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { cn } from "@/lib/utils";

export interface Column<T> {
  key: string;
  header: string;
  sortable?: boolean;
  className?: string;
  headerClassName?: string;
  render?: (item: T) => React.ReactNode;
}

export interface PaginationInfo {
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export type SortDirection = "asc" | "desc";

interface DataTableProps<T> {
  columns: Column<T>[];
  data: T[];
  isLoading?: boolean;
  emptyMessage?: string;
  keyExtractor: (item: T) => string;
  onRowClick?: (item: T) => void;
  skeletonRows?: number;
  // Pagination props
  pagination?: PaginationInfo;
  onPageChange?: (page: number) => void;
  onPageSizeChange?: (size: number) => void;
  pageSizeOptions?: number[];
  // Server-side sorting props
  sortColumn?: string | null;
  sortDirection?: SortDirection | null;
  onSort?: (column: string, direction: SortDirection) => void;
}

const PAGE_SIZE_OPTIONS = [10, 20, 50, 100];

export function DataTable<T>({
  columns,
  data,
  isLoading = false,
  emptyMessage = "No data found",
  keyExtractor,
  onRowClick,
  skeletonRows = 5,
  pagination,
  onPageChange,
  onPageSizeChange,
  pageSizeOptions = PAGE_SIZE_OPTIONS,
  sortColumn,
  sortDirection,
  onSort,
}: DataTableProps<T>) {
  // Local sorting state (for client-side sorting when onSort is not provided)
  const [localSort, setLocalSort] = React.useState<{
    column: string | null;
    direction: SortDirection | null;
  }>({
    column: null,
    direction: null,
  });

  const isServerSideSorting = !!onSort;
  const currentSortColumn = isServerSideSorting ? sortColumn : localSort.column;
  const currentSortDirection = isServerSideSorting
    ? sortDirection
    : localSort.direction;

  const handleSort = (columnKey: string, sortable?: boolean) => {
    if (!sortable) return;

    if (isServerSideSorting && onSort) {
      // Server-side sorting
      if (sortColumn !== columnKey) {
        onSort(columnKey, "asc");
      } else if (sortDirection === "asc") {
        onSort(columnKey, "desc");
      } else {
        // Reset to default (no sort) - call with first column asc as default
        onSort(columnKey, "asc");
      }
    } else {
      // Client-side sorting
      setLocalSort((prev) => {
        if (prev.column !== columnKey) {
          return { column: columnKey, direction: "asc" };
        }
        if (prev.direction === "asc") {
          return { column: columnKey, direction: "desc" };
        }
        return { column: null, direction: null };
      });
    }
  };

  // Client-side sorted data (only used when not server-side sorting)
  const sortedData = React.useMemo(() => {
    if (isServerSideSorting || !localSort.column || !localSort.direction) {
      return data;
    }

    const sortCol = localSort.column;

    return [...data].sort((a, b) => {
      const column = columns.find((c) => c.key === sortCol);
      if (!column) return 0;

      const aValue = (a as Record<string, unknown>)[sortCol];
      const bValue = (b as Record<string, unknown>)[sortCol];

      if (aValue === null || aValue === undefined) return 1;
      if (bValue === null || bValue === undefined) return -1;

      let comparison = 0;
      if (typeof aValue === "string" && typeof bValue === "string") {
        comparison = aValue.localeCompare(bValue);
      } else if (typeof aValue === "number" && typeof bValue === "number") {
        comparison = aValue - bValue;
      } else {
        comparison = String(aValue).localeCompare(String(bValue));
      }

      return localSort.direction === "desc" ? -comparison : comparison;
    });
  }, [data, localSort, columns, isServerSideSorting]);

  const getSortIcon = (columnKey: string) => {
    if (currentSortColumn !== columnKey) {
      return (
        <svg
          className="ml-1 h-4 w-4 text-muted-foreground/50"
          fill="none"
          stroke="currentColor"
          viewBox="0 0 24 24"
        >
          <path
            strokeLinecap="round"
            strokeLinejoin="round"
            strokeWidth={2}
            d="M7 16V4m0 0L3 8m4-4l4 4m6 0v12m0 0l4-4m-4 4l-4-4"
          />
        </svg>
      );
    }
    if (currentSortDirection === "asc") {
      return (
        <svg
          className="ml-1 h-4 w-4"
          fill="none"
          stroke="currentColor"
          viewBox="0 0 24 24"
        >
          <path
            strokeLinecap="round"
            strokeLinejoin="round"
            strokeWidth={2}
            d="M5 15l7-7 7 7"
          />
        </svg>
      );
    }
    return (
      <svg
        className="ml-1 h-4 w-4"
        fill="none"
        stroke="currentColor"
        viewBox="0 0 24 24"
      >
        <path
          strokeLinecap="round"
          strokeLinejoin="round"
          strokeWidth={2}
          d="M19 9l-7 7-7-7"
        />
      </svg>
    );
  };

  const renderPagination = () => {
    if (!pagination) return null;

    // Ensure all values are valid numbers with defaults
    const pageNumber = pagination.pageNumber || 1;
    const pageSize = pagination.pageSize || 20;
    const totalCount = pagination.totalCount || 0;
    const totalPages = pagination.totalPages || 0;
    const startItem = totalCount > 0 ? (pageNumber - 1) * pageSize + 1 : 0;
    const endItem = Math.min(pageNumber * pageSize, totalCount);

    // Generate page numbers to display
    const getPageNumbers = () => {
      const pages: (number | "ellipsis")[] = [];
      const maxVisible = 5;

      if (totalPages <= maxVisible) {
        for (let i = 1; i <= totalPages; i++) {
          pages.push(i);
        }
      } else {
        // Always show first page
        pages.push(1);

        if (pageNumber > 3) {
          pages.push("ellipsis");
        }

        // Show pages around current page
        const start = Math.max(2, pageNumber - 1);
        const end = Math.min(totalPages - 1, pageNumber + 1);

        for (let i = start; i <= end; i++) {
          pages.push(i);
        }

        if (pageNumber < totalPages - 2) {
          pages.push("ellipsis");
        }

        // Always show last page
        if (totalPages > 1) {
          pages.push(totalPages);
        }
      }

      return pages;
    };

    return (
      <div className="flex flex-col gap-3 border-t px-4 py-3 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex flex-col items-start gap-3 text-sm text-muted-foreground sm:flex-row sm:items-center sm:gap-4">
          <span className="whitespace-nowrap">
            Showing {totalCount > 0 ? startItem : 0} to {endItem} of {totalCount}{" "}
            results
          </span>
          {onPageSizeChange && (
            <div className="flex items-center gap-2">
              <span className="whitespace-nowrap">Rows per page:</span>
              <Select
                value={String(pageSize)}
                onValueChange={(value) => onPageSizeChange(Number(value))}
              >
                <SelectTrigger className="h-8 w-[70px]">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {pageSizeOptions.map((size) => (
                    <SelectItem key={size} value={String(size)}>
                      {size}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          )}
        </div>

        {totalPages > 1 && onPageChange && (
          <div className="flex items-center justify-between gap-1 sm:justify-end">
            <Button
              variant="outline"
              size="sm"
              onClick={() => onPageChange(pageNumber - 1)}
              disabled={pageNumber <= 1 || isLoading}
              className="min-h-[44px] min-w-[80px] sm:min-h-0 sm:min-w-0"
            >
              Previous
            </Button>

            <div className="hidden items-center gap-1 md:flex">
              {getPageNumbers().map((page, idx) =>
                page === "ellipsis" ? (
                  <span key={`ellipsis-${idx}`} className="px-2 text-muted-foreground">
                    ...
                  </span>
                ) : (
                  <Button
                    key={page}
                    variant={page === pageNumber ? "default" : "outline"}
                    size="sm"
                    onClick={() => onPageChange(page)}
                    disabled={isLoading}
                    className="min-w-[32px]"
                  >
                    {page}
                  </Button>
                )
              )}
            </div>

            {/* Mobile page indicator */}
            <div className="flex items-center px-3 text-sm text-muted-foreground md:hidden">
              Page {pageNumber} of {totalPages}
            </div>

            <Button
              variant="outline"
              size="sm"
              onClick={() => onPageChange(pageNumber + 1)}
              disabled={pageNumber >= totalPages || isLoading}
              className="min-h-[44px] min-w-[80px] sm:min-h-0 sm:min-w-0"
            >
              Next
            </Button>
          </div>
        )}
      </div>
    );
  };

  const renderTableHeader = () => (
    <TableHeader>
      <TableRow className="hover:bg-transparent">
        {columns.map((column) => (
          <TableHead
            key={column.key}
            className={cn(
              "bg-muted/50",
              column.sortable && "cursor-pointer select-none",
              column.headerClassName
            )}
            onClick={() => handleSort(column.key, column.sortable)}
          >
            <div className="flex items-center">
              {column.header}
              {column.sortable && getSortIcon(column.key)}
            </div>
          </TableHead>
        ))}
      </TableRow>
    </TableHeader>
  );

  if (isLoading) {
    return (
      <div className="rounded-lg border bg-card">
        <div className="overflow-x-auto">
          <Table>
            {renderTableHeader()}
            <TableBody>
              {Array.from({ length: skeletonRows }).map((_, rowIndex) => (
                <TableRow key={rowIndex} className="hover:bg-transparent">
                  {columns.map((column) => (
                    <TableCell key={column.key} className={column.className}>
                      <Skeleton className="h-5 w-full max-w-[200px]" />
                    </TableCell>
                  ))}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
        {renderPagination()}
      </div>
    );
  }

  if (data.length === 0) {
    return (
      <div className="rounded-lg border bg-card">
        <div className="overflow-x-auto">
          <Table>
            {renderTableHeader()}
            <TableBody>
              <TableRow className="hover:bg-transparent">
                <TableCell
                  colSpan={columns.length}
                  className="h-32 text-center text-muted-foreground"
                >
                  {emptyMessage}
                </TableCell>
              </TableRow>
            </TableBody>
          </Table>
        </div>
        {renderPagination()}
      </div>
    );
  }

  return (
    <div className="rounded-lg border bg-card">
      <div className="overflow-x-auto">
        <Table>
          {renderTableHeader()}
          <TableBody>
            {sortedData.map((item) => (
              <TableRow
                key={keyExtractor(item)}
                className={cn(onRowClick && "cursor-pointer")}
                onClick={() => onRowClick?.(item)}
              >
                {columns.map((column) => (
                  <TableCell key={column.key} className={column.className}>
                    {column.render
                      ? column.render(item)
                      : String(
                          (item as Record<string, unknown>)[column.key] ?? ""
                        )}
                  </TableCell>
                ))}
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
      {renderPagination()}
    </div>
  );
}
