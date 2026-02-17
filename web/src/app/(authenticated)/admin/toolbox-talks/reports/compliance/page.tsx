'use client';

import * as React from 'react';
import { useSearchParams, useRouter, usePathname } from 'next/navigation';
import { format } from 'date-fns';
import { Download, Building2, FileText, Users, CheckCircle, AlertTriangle } from 'lucide-react';
import {
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
  PieChart,
  Pie,
  Cell,
  Legend,
} from 'recharts';

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
import { Progress } from '@/components/ui/progress';
import { Badge } from '@/components/ui/badge';
import { useComplianceReport, useExportComplianceReport } from '@/lib/api/toolbox-talks';
import { useSites } from '@/lib/api/admin/use-sites';
import { toast } from 'sonner';

export default function AdminComplianceReportPage() {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();

  const dateFrom = searchParams.get('dateFrom') || undefined;
  const dateTo = searchParams.get('dateTo') || undefined;
  const siteId = searchParams.get('siteId') || undefined;

  const { data: report, isLoading, error } = useComplianceReport({ dateFrom, dateTo, siteId });
  const { data: sitesData } = useSites();
  const exportReport = useExportComplianceReport();

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
      await exportReport.mutateAsync({ dateFrom, dateTo, siteId });
      toast.success('Report exported successfully');
    } catch {
      toast.error('Export functionality is not yet available');
    }
  };

  const statusPieData = React.useMemo(() => {
    if (!report) return [];
    return [
      { name: 'Completed', value: report.completedCount, color: '#10B981' },
      { name: 'Pending', value: report.pendingCount, color: '#F59E0B' },
      { name: 'In Progress', value: report.inProgressCount, color: '#3B82F6' },
      { name: 'Overdue', value: report.overdueCount, color: '#EF4444' },
    ].filter((item) => item.value > 0);
  }, [report]);

  if (error) {
    return (
      <div className="space-y-6">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Compliance Report</h1>
          <p className="text-muted-foreground">Error loading report</p>
        </div>
        <Card className="p-8 text-center">
          <p className="text-destructive">Failed to load compliance report. Please try again.</p>
        </Card>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Compliance Report</h1>
          <p className="text-muted-foreground">
            Overall learning compliance metrics
            {report && ` - Generated ${format(new Date(report.generatedAt), 'PPp')}`}
          </p>
        </div>
        <Button variant="outline" onClick={handleExport} disabled={exportReport.isPending || isLoading}>
          <Download className="h-4 w-4 mr-2" />
          Export PDF
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
                onChange={(e) => updateUrlParams({ dateFrom: e.target.value || null })}
                className="h-9 rounded-md border border-input bg-background px-3 py-1 text-sm"
              />
            </div>
            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-medium">Date To</label>
              <input
                type="date"
                value={dateTo || ''}
                onChange={(e) => updateUrlParams({ dateTo: e.target.value || null })}
                className="h-9 rounded-md border border-input bg-background px-3 py-1 text-sm"
              />
            </div>
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
          </div>
        </CardContent>
      </Card>

      {/* KPI Cards */}
      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Total Employees</CardTitle>
            <Users className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            {isLoading ? (
              <Skeleton className="h-8 w-16" />
            ) : (
              <div className="text-2xl font-bold">{report?.totalEmployees ?? 0}</div>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Assigned</CardTitle>
            <FileText className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            {isLoading ? (
              <Skeleton className="h-8 w-16" />
            ) : (
              <div className="text-2xl font-bold">{report?.assignedCount ?? 0}</div>
            )}
          </CardContent>
        </Card>

        <Card className="bg-green-50 border-green-200">
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Compliance Rate</CardTitle>
            <CheckCircle className="h-4 w-4 text-green-600" />
          </CardHeader>
          <CardContent>
            {isLoading ? (
              <Skeleton className="h-8 w-20" />
            ) : (
              <>
                <div className="text-2xl font-bold text-green-700">
                  {report?.compliancePercentage?.toFixed(1) ?? 0}%
                </div>
                <Progress value={report?.compliancePercentage ?? 0} className="mt-2 h-2" />
              </>
            )}
          </CardContent>
        </Card>

        <Card className={(report?.overdueCount ?? 0) > 0 ? 'bg-red-50 border-red-200' : ''}>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Overdue</CardTitle>
            <AlertTriangle className={`h-4 w-4 ${(report?.overdueCount ?? 0) > 0 ? 'text-red-600' : 'text-muted-foreground'}`} />
          </CardHeader>
          <CardContent>
            {isLoading ? (
              <Skeleton className="h-8 w-16" />
            ) : (
              <div className={`text-2xl font-bold ${(report?.overdueCount ?? 0) > 0 ? 'text-red-700' : ''}`}>
                {report?.overdueCount ?? 0}
              </div>
            )}
          </CardContent>
        </Card>
      </div>

      {/* Charts */}
      <div className="grid gap-4 md:grid-cols-2">
        {/* Status Distribution */}
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Assignment Status Distribution</CardTitle>
            <CardDescription>Current status breakdown</CardDescription>
          </CardHeader>
          <CardContent>
            {isLoading ? (
              <Skeleton className="h-[300px] w-full" />
            ) : statusPieData.length > 0 ? (
              <ResponsiveContainer width="100%" height={300}>
                <PieChart>
                  <Pie
                    data={statusPieData}
                    cx="50%"
                    cy="50%"
                    innerRadius={60}
                    outerRadius={100}
                    paddingAngle={2}
                    dataKey="value"
                    label={({ name, percent }) => `${name} ${((percent ?? 0) * 100).toFixed(0)}%`}
                    labelLine={false}
                  >
                    {statusPieData.map((entry, index) => (
                      <Cell key={`cell-${index}`} fill={entry.color} />
                    ))}
                  </Pie>
                  <Tooltip />
                  <Legend />
                </PieChart>
              </ResponsiveContainer>
            ) : (
              <div className="h-[300px] flex items-center justify-center text-muted-foreground">
                No data available
              </div>
            )}
          </CardContent>
        </Card>

        {/* Department Compliance Chart */}
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Compliance by Department</CardTitle>
            <CardDescription>Completion rates by site</CardDescription>
          </CardHeader>
          <CardContent>
            {isLoading ? (
              <Skeleton className="h-[300px] w-full" />
            ) : (report?.byDepartment ?? []).length > 0 ? (
              <ResponsiveContainer width="100%" height={300}>
                <BarChart data={report?.byDepartment} layout="vertical">
                  <CartesianGrid strokeDasharray="3 3" className="stroke-muted" />
                  <XAxis type="number" domain={[0, 100]} unit="%" />
                  <YAxis dataKey="departmentName" type="category" width={120} tick={{ fontSize: 12 }} />
                  <Tooltip formatter={(value) => value !== undefined ? `${Number(value).toFixed(1)}%` : '-'} />
                  <Bar dataKey="compliancePercentage" fill="#3B82F6" name="Compliance %" radius={[0, 4, 4, 0]} />
                </BarChart>
              </ResponsiveContainer>
            ) : (
              <div className="h-[300px] flex items-center justify-center text-muted-foreground">
                No department data available
              </div>
            )}
          </CardContent>
        </Card>
      </div>

      {/* Department Breakdown Table */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base flex items-center gap-2">
            <Building2 className="h-5 w-5" />
            Compliance by Department
          </CardTitle>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Department</TableHead>
                <TableHead className="text-right">Employees</TableHead>
                <TableHead className="text-right">Assigned</TableHead>
                <TableHead className="text-right">Completed</TableHead>
                <TableHead className="text-right">Overdue</TableHead>
                <TableHead className="text-right">Compliance</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {isLoading ? (
                Array.from({ length: 5 }).map((_, i) => (
                  <TableRow key={i}>
                    <TableCell><Skeleton className="h-4 w-32" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-12 ml-auto" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-12 ml-auto" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-12 ml-auto" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-12 ml-auto" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-16 ml-auto" /></TableCell>
                  </TableRow>
                ))
              ) : (report?.byDepartment ?? []).length === 0 ? (
                <TableRow>
                  <TableCell colSpan={6} className="text-center text-muted-foreground py-8">
                    No department data available
                  </TableCell>
                </TableRow>
              ) : (
                report?.byDepartment.map((dept) => (
                  <TableRow key={dept.siteId || dept.departmentName}>
                    <TableCell className="font-medium">{dept.departmentName}</TableCell>
                    <TableCell className="text-right">{dept.totalEmployees}</TableCell>
                    <TableCell className="text-right">{dept.assignedCount}</TableCell>
                    <TableCell className="text-right">{dept.completedCount}</TableCell>
                    <TableCell className="text-right">
                      {dept.overdueCount > 0 ? (
                        <Badge variant="destructive">{dept.overdueCount}</Badge>
                      ) : (
                        dept.overdueCount
                      )}
                    </TableCell>
                    <TableCell className="text-right">
                      <Badge variant={dept.compliancePercentage >= 80 ? 'default' : dept.compliancePercentage >= 50 ? 'secondary' : 'destructive'}>
                        {dept.compliancePercentage.toFixed(1)}%
                      </Badge>
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      {/* Talk Breakdown Table */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base flex items-center gap-2">
            <FileText className="h-5 w-5" />
            Compliance by Learning
          </CardTitle>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Talk</TableHead>
                <TableHead className="text-right">Assigned</TableHead>
                <TableHead className="text-right">Completed</TableHead>
                <TableHead className="text-right">Overdue</TableHead>
                <TableHead className="text-right">Compliance</TableHead>
                <TableHead className="text-right">Avg Quiz Score</TableHead>
                <TableHead className="text-right">Quiz Pass Rate</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {isLoading ? (
                Array.from({ length: 5 }).map((_, i) => (
                  <TableRow key={i}>
                    <TableCell><Skeleton className="h-4 w-48" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-12 ml-auto" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-12 ml-auto" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-12 ml-auto" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-16 ml-auto" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-16 ml-auto" /></TableCell>
                    <TableCell><Skeleton className="h-4 w-16 ml-auto" /></TableCell>
                  </TableRow>
                ))
              ) : (report?.byTalk ?? []).length === 0 ? (
                <TableRow>
                  <TableCell colSpan={7} className="text-center text-muted-foreground py-8">
                    No talk data available
                  </TableCell>
                </TableRow>
              ) : (
                report?.byTalk.map((talk) => (
                  <TableRow key={talk.toolboxTalkId}>
                    <TableCell className="font-medium">{talk.title}</TableCell>
                    <TableCell className="text-right">{talk.assignedCount}</TableCell>
                    <TableCell className="text-right">{talk.completedCount}</TableCell>
                    <TableCell className="text-right">
                      {talk.overdueCount > 0 ? (
                        <Badge variant="destructive">{talk.overdueCount}</Badge>
                      ) : (
                        talk.overdueCount
                      )}
                    </TableCell>
                    <TableCell className="text-right">
                      <Badge variant={talk.compliancePercentage >= 80 ? 'default' : talk.compliancePercentage >= 50 ? 'secondary' : 'destructive'}>
                        {talk.compliancePercentage.toFixed(1)}%
                      </Badge>
                    </TableCell>
                    <TableCell className="text-right">
                      {talk.averageQuizScore !== null ? `${talk.averageQuizScore.toFixed(0)}%` : '-'}
                    </TableCell>
                    <TableCell className="text-right">
                      {talk.quizPassRate !== null ? `${talk.quizPassRate.toFixed(0)}%` : '-'}
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
