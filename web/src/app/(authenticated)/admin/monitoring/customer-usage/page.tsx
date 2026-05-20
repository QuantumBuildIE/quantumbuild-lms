"use client";

import { useState, useMemo } from "react";
import { format, parseISO } from "date-fns";
import {
  AlertTriangle,
  ArrowDown,
  ArrowUp,
  ArrowUpDown,
  CheckCircle2,
  Clock,
} from "lucide-react";
import { toast } from "sonner";
import { useIsSuperUser } from "@/lib/auth/use-auth";
import {
  useCustomerUsageReport,
  useMarkReviewed,
} from "@/lib/api/admin/use-monitoring";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { cn } from "@/lib/utils";
import type { TenantUsageRowDto } from "@/lib/api/admin/monitoring";

type SortKey = keyof Omit<TenantUsageRowDto, "tenantId" | "isAtRisk">;
type SortDir = "asc" | "desc";

const COLUMNS: { key: SortKey; label: string }[] = [
  { key: "tenantName", label: "Customer" },
  { key: "signUpDate", label: "Sign-up Date" },
  { key: "activeEmployeeCount", label: "Employees" },
  { key: "totalLearnings", label: "Total Learnings" },
  { key: "newLearnings", label: "New Learnings" },
  { key: "completions", label: "Completions" },
  { key: "lastLoginAt", label: "Last Login" },
];

function formatDate(value: string | null, fallback = "Never"): string {
  if (!value) return fallback;
  return format(parseISO(value), "dd MMM yyyy");
}

function toDateInputValue(iso: string | null): string {
  if (!iso) return "";
  return format(parseISO(iso), "yyyy-MM-dd");
}

function SortIcon({
  column,
  sortKey,
  sortDir,
}: {
  column: SortKey;
  sortKey: SortKey;
  sortDir: SortDir;
}) {
  if (column !== sortKey) return <ArrowUpDown className="ml-1 h-3 w-3 opacity-40" />;
  return sortDir === "asc" ? (
    <ArrowUp className="ml-1 h-3 w-3" />
  ) : (
    <ArrowDown className="ml-1 h-3 w-3" />
  );
}

export default function CustomerUsageReportPage() {
  const isSuperUser = useIsSuperUser();
  const [userSelectedDate, setUserSelectedDate] = useState<string | null>(null);
  const [sortKey, setSortKey] = useState<SortKey>("tenantName");
  const [sortDir, setSortDir] = useState<SortDir>("asc");

  const { data, isLoading } = useCustomerUsageReport(userSelectedDate ?? undefined);
  const markReviewed = useMarkReviewed();

  const sortedRows = useMemo(() => {
    if (!data?.rows) return [];
    return [...data.rows].sort((a, b) => {
      const aVal = a[sortKey];
      const bVal = b[sortKey];
      if (aVal === null || aVal === undefined) return 1;
      if (bVal === null || bVal === undefined) return -1;
      let cmp = 0;
      if (typeof aVal === "number") {
        cmp = aVal - (bVal as number);
      } else {
        cmp = String(aVal).localeCompare(String(bVal));
      }
      return sortDir === "asc" ? cmp : -cmp;
    });
  }, [data?.rows, sortKey, sortDir]);

  function handleSort(key: SortKey) {
    if (key === sortKey) {
      setSortDir((d) => (d === "asc" ? "desc" : "asc"));
    } else {
      setSortKey(key);
      setSortDir("asc");
    }
  }

  function handleDateChange(e: React.ChangeEvent<HTMLInputElement>) {
    const val = e.target.value;
    setUserSelectedDate(val || null);
  }

  function handleMarkReviewed() {
    markReviewed.mutate(undefined, {
      onSuccess: () => {
        toast.success("Marked as reviewed");
        setUserSelectedDate(null);
      },
      onError: () => {
        toast.error("Failed to mark as reviewed");
      },
    });
  }

  if (!isSuperUser) {
    return (
      <div className="text-muted-foreground">
        You do not have permission to access Customer Usage Report.
      </div>
    );
  }

  const atRiskCount = data?.rows.filter((r) => r.isAtRisk).length ?? 0;

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <h2 className="text-xl font-semibold tracking-tight">
            Customer Usage Report
          </h2>
          <p className="text-sm text-muted-foreground">
            Per-tenant activity since the comparison date
          </p>
        </div>
        <Button
          onClick={handleMarkReviewed}
          disabled={markReviewed.isPending || isLoading}
          size="sm"
        >
          <CheckCircle2 className="mr-2 h-4 w-4" />
          {markReviewed.isPending ? "Saving..." : "Mark as Reviewed"}
        </Button>
      </div>

      {/* Controls + last-reviewed banner */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-end">
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="comparison-date" className="text-sm">
            Comparison date
          </Label>
          <Input
            id="comparison-date"
            type="date"
            value={userSelectedDate ?? toDateInputValue(data?.comparisonDate ?? null)}
            onChange={handleDateChange}
            className="w-44"
            disabled={isLoading}
          />
        </div>
        <div className="flex items-center gap-1.5 text-sm text-muted-foreground pb-0.5">
          <Clock className="h-3.5 w-3.5 shrink-0" />
          {isLoading ? (
            <Skeleton className="h-4 w-48" />
          ) : data?.lastReviewedAt ? (
            <span>
              Last reviewed:{" "}
              <span className="font-medium text-foreground">
                {format(parseISO(data.lastReviewedAt), "dd MMM yyyy, HH:mm")}
              </span>
            </span>
          ) : (
            <span>Never reviewed</span>
          )}
        </div>
      </div>

      {/* At-risk summary badge */}
      {!isLoading && atRiskCount > 0 && (
        <Card className="border-amber-300 bg-amber-50 dark:bg-amber-950/20">
          <CardContent className="flex items-center gap-2 py-3">
            <AlertTriangle className="h-4 w-4 text-amber-600" />
            <span className="text-sm font-medium text-amber-800 dark:text-amber-400">
              {atRiskCount} at-risk customer{atRiskCount !== 1 ? "s" : ""} — no recent login
              activity
            </span>
          </CardContent>
        </Card>
      )}

      {/* Table */}
      <div className="rounded-md border">
        <Table>
          <TableHeader>
            <TableRow>
              {COLUMNS.map((col) => (
                <TableHead
                  key={col.key}
                  className="cursor-pointer select-none whitespace-nowrap"
                  onClick={() => handleSort(col.key)}
                >
                  <span className="inline-flex items-center">
                    {col.label}
                    <SortIcon column={col.key} sortKey={sortKey} sortDir={sortDir} />
                  </span>
                </TableHead>
              ))}
            </TableRow>
          </TableHeader>
          <TableBody>
            {isLoading ? (
              Array.from({ length: 6 }).map((_, i) => (
                <TableRow key={i}>
                  {COLUMNS.map((col) => (
                    <TableCell key={col.key}>
                      <Skeleton className="h-4 w-full" />
                    </TableCell>
                  ))}
                </TableRow>
              ))
            ) : sortedRows.length === 0 ? (
              <TableRow>
                <TableCell
                  colSpan={COLUMNS.length}
                  className="py-10 text-center text-sm text-muted-foreground"
                >
                  No customers found.
                </TableCell>
              </TableRow>
            ) : (
              sortedRows.map((row) => (
                <TableRow
                  key={row.tenantId}
                  className={cn(
                    row.isAtRisk &&
                      "bg-amber-50 hover:bg-amber-100 dark:bg-amber-950/20 dark:hover:bg-amber-950/30"
                  )}
                >
                  <TableCell className="font-medium">
                    <span className="inline-flex items-center gap-1.5">
                      {row.isAtRisk && (
                        <AlertTriangle
                          className="h-4 w-4 shrink-0 text-amber-500"
                          aria-label="At risk"
                        />
                      )}
                      {row.tenantName}
                    </span>
                  </TableCell>
                  <TableCell className="text-muted-foreground">
                    {formatDate(row.signUpDate, "—")}
                  </TableCell>
                  <TableCell>{row.activeEmployeeCount}</TableCell>
                  <TableCell>{row.totalLearnings}</TableCell>
                  <TableCell>{row.newLearnings}</TableCell>
                  <TableCell>{row.completions}</TableCell>
                  <TableCell className="text-muted-foreground">
                    {row.lastLoginAt ? formatDate(row.lastLoginAt) : "Never"}
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </div>

      {!isLoading && sortedRows.length > 0 && (
        <p className="text-xs text-muted-foreground">
          {sortedRows.length} customer{sortedRows.length !== 1 ? "s" : ""} &mdash;{" "}
          New learnings and completions are counted since{" "}
          <strong>{data?.comparisonDate ? toDateInputValue(data.comparisonDate) : "—"}</strong>
        </p>
      )}
    </div>
  );
}
