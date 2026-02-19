'use client';

import * as React from 'react';
import { useSearchParams, useRouter, usePathname } from 'next/navigation';
import { format } from 'date-fns';
import { CheckCircle, Download, ChevronLeft, ChevronRight } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { useMyOperators } from '@/lib/api/admin/use-supervisor-assignments';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Badge } from '@/components/ui/badge';
import { useCompletionsReport, useExportCompletionsReport } from '@/lib/api/toolbox-talks';
import { useSites } from '@/lib/api/admin/use-sites';
import { useToolboxTalks } from '@/lib/api/toolbox-talks';
import { toast } from 'sonner';

export default function CompletionsReportPage() {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();

  const dateFrom = searchParams.get('dateFrom') || undefined;
  const dateTo = searchParams.get('dateTo') || undefined;
  const siteId = searchParams.get('siteId') || undefined;
  const toolboxTalkId = searchParams.get('toolboxTalkId') || undefined;
  const pageNumber = parseInt(searchParams.get('page') || '1', 10);
  const pageSize = 20;

  const { data: report, isLoading, error } = useCompletionsReport({
    dateFrom,
    dateTo,
    siteId,
    toolboxTalkId,
    pageNumber,
    pageSize,
  });
  const { data: sitesData } = useSites();
  const { data: talksData } = useToolboxTalks({ isActive: true });
  const exportReport = useExportCompletionsReport();
  const { data: operators } = useMyOperators();
  const operatorCount = operators?.length ?? 0;

  const updateUrlParams = (updates: Record<string, string | null | undefined>) => {
    const params = new URLSearchParams(searchParams.toString());
    Object.entries(updates).forEach(([key, value]) => {
      if (value === null || value === undefined || value === 'all') {
        params.delete(key);
      } else {
        params.set(key, String(value));
      }
    });
    const queryString = params.toString();
    router.push(queryString ? `${pathname}?${queryString}` : pathname);
  };

  const handleExport = async () => {
    try {
      await exportReport.mutateAsync({ dateFrom, dateTo, siteId, toolboxTalkId });
      toast.success('Report exported successfully');
    } catch {
      toast.error('Export functionality is not yet available');
    }
  };

  const handlePageChange = (newPage: number) => {
    updateUrlParams({ page: newPage.toString() });
  };

  if (error) {
    return (
      <div className="space-y-6">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Team Completions Report</h1>
          <p className="text-muted-foreground">Error loading report</p>
        </div>
        <Card className="p-8 text-center">
          <p className="text-destructive">Failed to load completions report. Please try again.</p>
        </Card>
      </div>
    );
  }

  const totalPages = report?.totalPages ?? 1;
  const hasNextPage = report?.hasNextPage ?? false;
  const hasPreviousPage = report?.hasPreviousPage ?? false;

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight flex items-center gap-2">
            <CheckCircle className="h-6 w-6 text-green-600" />
            Team Completions Report
          </h1>
          <p className="text-muted-foreground">
            {report?.totalCount
              ? `${report.totalCount} completion${report.totalCount !== 1 ? 's' : ''} across your ${operatorCount} assigned operator${operatorCount !== 1 ? 's' : ''}`
              : `Completion records for your ${operatorCount} assigned operator${operatorCount !== 1 ? 's' : ''}`}
          </p>
        </div>
        <Button variant="outline" onClick={handleExport} disabled={exportReport.isPending || isLoading}>
          <Download className="h-4 w-4 mr-2" />
          Export Excel
        </Button>
      </div>

      {/* Filters */}
      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base">Filters</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="flex flex-wrap gap-4">
            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-medium">Date From</label>
              <input
                type="date"
                value={dateFrom || ''}
                onChange={(e) => updateUrlParams({ dateFrom: e.target.value || null, page: '1' })}
                className="h-9 rounded-md border border-input bg-background px-3 py-1 text-sm"
              />
            </div>
            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-medium">Date To</label>
              <input
                type="date"
                value={dateTo || ''}
                onChange={(e) => updateUrlParams({ dateTo: e.target.value || null, page: '1' })}
                className="h-9 rounded-md border border-input bg-background px-3 py-1 text-sm"
              />
            </div>
            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-medium">Site</label>
              <Select
                value={siteId || 'all'}
                onValueChange={(value) => updateUrlParams({ siteId: value === 'all' ? null : value, page: '1' })}
              >
                <SelectTrigger className="w-[200px]">
                  <SelectValue placeholder="All Sites" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All Sites</SelectItem>
                  {sitesData?.items?.map((site) => (
                    <SelectItem key={site.id} value={site.id}>
                      {site.siteName}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-medium">Learning</label>
              <Select
                value={toolboxTalkId || 'all'}
                onValueChange={(value) => updateUrlParams({ toolboxTalkId: value === 'all' ? null : value, page: '1' })}
              >
                <SelectTrigger className="w-[250px]">
                  <SelectValue placeholder="All Learnings" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All Learnings</SelectItem>
                  {talksData?.items?.map((talk) => (
                    <SelectItem key={talk.id} value={talk.id}>
                      {talk.title}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Completions Table */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Completion Details</CardTitle>
          <CardDescription>
            Individual completion records with quiz scores and timing information
          </CardDescription>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Employee</TableHead>
                <TableHead>Site</TableHead>
                <TableHead>Learning</TableHead>
                <TableHead className="text-right">Completed</TableHead>
                <TableHead className="text-right">Time Spent</TableHead>
                <TableHead className="text-center">Quiz Score</TableHead>
                <TableHead className="text-center">On Time</TableHead>
                <TableHead>Signed By</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {isLoading ? (
                Array.from({ length: 10 }).map((_, i) => (
                  <TableRow key={i}>
                    <TableCell><Skeleton className="h-4 w-32" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-24" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-40" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-24 ml-auto" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-16 ml-auto" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-20 mx-auto" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-16 mx-auto" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-24" /></TableCell>
                  </TableRow>
                ))
              ) : (report?.items ?? []).length === 0 ? (
                <TableRow>
                  <TableCell colSpan={8} className="text-center py-12">
                    <div className="flex flex-col items-center gap-2">
                      <CheckCircle className="h-8 w-8 text-muted-foreground" />
                      <p className="text-muted-foreground">No completions found</p>
                    </div>
                  </TableCell>
                </TableRow>
              ) : (
                report?.items?.map((item) => (
                  <TableRow key={item.completionId}>
                    <TableCell>
                      <div>
                        <div className="font-medium">{item.employeeName}</div>
                        {item.email && (
                          <div className="text-sm text-muted-foreground">{item.email}</div>
                        )}
                      </div>
                    </TableCell>
                    <TableCell>{item.siteName || '-'}</TableCell>
                    <TableCell className="max-w-[200px] truncate">{item.talkTitle}</TableCell>
                    <TableCell className="text-right">
                      <div>
                        <div>{format(new Date(item.completedAt), 'MMM dd, yyyy')}</div>
                        <div className="text-sm text-muted-foreground">
                          {format(new Date(item.completedAt), 'HH:mm')}
                        </div>
                      </div>
                    </TableCell>
                    <TableCell className="text-right">
                      {item.timeSpentMinutes} min
                    </TableCell>
                    <TableCell className="text-center">
                      {item.quizScore !== null ? (
                        <div className="flex flex-col items-center">
                          <Badge variant={item.quizPassed ? 'default' : 'destructive'}>
                            {item.quizScorePercentage?.toFixed(0)}%
                          </Badge>
                          <span className="text-xs text-muted-foreground">
                            {item.quizScore}/{item.quizMaxScore}
                          </span>
                        </div>
                      ) : (
                        <span className="text-muted-foreground">-</span>
                      )}
                    </TableCell>
                    <TableCell className="text-center">
                      {item.completedOnTime ? (
                        <Badge variant="outline" className="bg-green-50 text-green-700 border-green-200">
                          Yes
                        </Badge>
                      ) : (
                        <Badge variant="outline" className="bg-red-50 text-red-700 border-red-200">
                          No
                        </Badge>
                      )}
                    </TableCell>
                    <TableCell>
                      <div>
                        <div className="text-sm">{item.signedByName}</div>
                        <div className="text-xs text-muted-foreground">
                          {format(new Date(item.signedAt), 'MMM dd, HH:mm')}
                        </div>
                      </div>
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>

          {/* Pagination */}
          {totalPages > 1 && (
            <div className="flex items-center justify-between mt-4 pt-4 border-t">
              <div className="text-sm text-muted-foreground">
                Page {pageNumber} of {totalPages} ({report?.totalCount} total)
              </div>
              <div className="flex items-center gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => handlePageChange(pageNumber - 1)}
                  disabled={!hasPreviousPage}
                >
                  <ChevronLeft className="h-4 w-4" />
                  Previous
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => handlePageChange(pageNumber + 1)}
                  disabled={!hasNextPage}
                >
                  Next
                  <ChevronRight className="h-4 w-4" />
                </Button>
              </div>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
