'use client';

import * as React from 'react';
import { useSearchParams, useRouter, usePathname } from 'next/navigation';
import { format } from 'date-fns';
import { AlertTriangle, Download, Bell, Mail } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
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
import { Progress } from '@/components/ui/progress';
import { useOverdueReport, useExportOverdueReport } from '@/lib/api/toolbox-talks';
import { useSites } from '@/lib/api/admin/use-sites';
import { useToolboxTalks } from '@/lib/api/toolbox-talks';
import { useSendReminder } from '@/lib/api/toolbox-talks';
import { toast } from 'sonner';

export default function AdminOverdueReportPage() {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();

  const siteId = searchParams.get('siteId') || undefined;
  const toolboxTalkId = searchParams.get('toolboxTalkId') || undefined;

  const { data: overdueItems, isLoading, error, refetch } = useOverdueReport({ siteId, toolboxTalkId });
  const { data: sitesData } = useSites();
  const { data: talksData } = useToolboxTalks({ isActive: true });
  const exportReport = useExportOverdueReport();
  const sendReminder = useSendReminder();

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
      await exportReport.mutateAsync({ siteId, toolboxTalkId });
      toast.success('Report exported successfully');
    } catch {
      toast.error('Export functionality is not yet available');
    }
  };

  const handleSendReminder = async (id: string, employeeName: string) => {
    try {
      await sendReminder.mutateAsync(id);
      toast.success(`Reminder sent to ${employeeName}`);
      refetch();
    } catch {
      toast.error('Failed to send reminder');
    }
  };

  if (error) {
    return (
      <div className="space-y-6">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Overdue Report</h1>
          <p className="text-muted-foreground">Error loading report</p>
        </div>
        <Card className="p-8 text-center">
          <p className="text-destructive">Failed to load overdue report. Please try again.</p>
        </Card>
      </div>
    );
  }

  const overdueCount = overdueItems?.length ?? 0;

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight flex items-center gap-2">
            <AlertTriangle className={`h-6 w-6 ${overdueCount > 0 ? 'text-red-600' : 'text-muted-foreground'}`} />
            Overdue Report
          </h1>
          <p className="text-muted-foreground">
            {overdueCount > 0
              ? `${overdueCount} overdue assignment${overdueCount !== 1 ? 's' : ''} requiring attention`
              : 'No overdue assignments'}
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
              <label className="text-sm font-medium">Site</label>
              <Select
                value={siteId || 'all'}
                onValueChange={(value) => updateUrlParams({ siteId: value === 'all' ? null : value })}
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
                onValueChange={(value) => updateUrlParams({ toolboxTalkId: value === 'all' ? null : value })}
              >
                <SelectTrigger className="w-[250px]">
                  <SelectValue placeholder="All Learning" />
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

      {/* Overdue Table */}
      <Card className={overdueCount > 0 ? 'border-red-200' : ''}>
        <CardHeader>
          <CardTitle className="text-base">Overdue Assignments</CardTitle>
          <CardDescription>
            Assignments past their due date that have not been completed
          </CardDescription>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Employee</TableHead>
                <TableHead>Site</TableHead>
                <TableHead>Learning</TableHead>
                <TableHead className="text-right">Due Date</TableHead>
                <TableHead className="text-right">Days Overdue</TableHead>
                <TableHead className="text-center">Progress</TableHead>
                <TableHead className="text-right">Reminders</TableHead>
                <TableHead></TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {isLoading ? (
                Array.from({ length: 5 }).map((_, i) => (
                  <TableRow key={i}>
                    <TableCell><Skeleton className="h-4 w-32" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-24" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-40" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-24 ml-auto" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-16 ml-auto" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-20 mx-auto" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-12 ml-auto" /></TableCell>
                    <TableCell><Skeleton className="h-8 w-20" /></TableCell>
                  </TableRow>
                ))
              ) : overdueCount === 0 ? (
                <TableRow>
                  <TableCell colSpan={8} className="text-center py-12">
                    <div className="flex flex-col items-center gap-2">
                      <AlertTriangle className="h-8 w-8 text-green-500" />
                      <p className="text-muted-foreground">No overdue assignments found</p>
                    </div>
                  </TableCell>
                </TableRow>
              ) : (
                overdueItems?.map((item) => (
                  <TableRow key={item.scheduledTalkId}>
                    <TableCell>
                      <div>
                        <div className="font-medium">{item.employeeName}</div>
                        {item.email && (
                          <div className="text-sm text-muted-foreground flex items-center gap-1">
                            <Mail className="h-3 w-3" />
                            {item.email}
                          </div>
                        )}
                      </div>
                    </TableCell>
                    <TableCell>{item.siteName || '-'}</TableCell>
                    <TableCell className="max-w-[200px] truncate">{item.talkTitle}</TableCell>
                    <TableCell className="text-right">
                      {format(new Date(item.dueDate), 'MMM dd, yyyy')}
                    </TableCell>
                    <TableCell className="text-right">
                      <Badge variant="destructive">{item.daysOverdue} days</Badge>
                    </TableCell>
                    <TableCell>
                      <div className="flex flex-col items-center gap-1">
                        {item.isInProgress ? (
                          <>
                            <Badge variant="secondary">In Progress</Badge>
                            <Progress value={item.videoWatchPercent} className="w-16 h-1" />
                            <span className="text-xs text-muted-foreground">{item.videoWatchPercent}%</span>
                          </>
                        ) : (
                          <Badge variant="outline">Not Started</Badge>
                        )}
                      </div>
                    </TableCell>
                    <TableCell className="text-right">
                      <div className="flex flex-col items-end">
                        <span>{item.remindersSent}</span>
                        {item.lastReminderAt && (
                          <span className="text-xs text-muted-foreground">
                            Last: {format(new Date(item.lastReminderAt), 'MMM dd')}
                          </span>
                        )}
                      </div>
                    </TableCell>
                    <TableCell>
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={() => handleSendReminder(item.scheduledTalkId, item.employeeName)}
                        disabled={sendReminder.isPending}
                      >
                        <Bell className="h-3 w-3 mr-1" />
                        Remind
                      </Button>
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </CardContent>
      </Card>
    </div>
  );
}
