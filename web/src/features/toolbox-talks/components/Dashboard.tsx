'use client';

import * as React from 'react';
import { useState } from 'react';
import Link from 'next/link';
import { format, formatDistanceToNow } from 'date-fns';
import {
  FileText,
  Clock,
  CheckCircle,
  AlertTriangle,
  TrendingUp,
  RefreshCw,
  Bell,
  Calendar,
  Users,
} from 'lucide-react';
import {
  LineChart,
  Line,
  BarChart,
  Bar,
  PieChart,
  Pie,
  Cell,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  Legend,
  ResponsiveContainer,
} from 'recharts';

import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Progress } from '@/components/ui/progress';
import { DataTable, type Column } from '@/components/shared/data-table';
import { Skeleton } from '@/components/ui/skeleton';
import {
  useToolboxTalkDashboard,
  useSendReminder,
  TOOLBOX_TALKS_DASHBOARD_KEY,
} from '@/lib/api/toolbox-talks';
import { useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { cn } from '@/lib/utils';
import type {
  RecentCompletion,
  OverdueAssignment,
  UpcomingSchedule,
  ScheduledTalkStatus,
} from '@/types/toolbox-talks';

// ============================================
// Constants
// ============================================

const STATUS_COLORS: Record<ScheduledTalkStatus, string> = {
  Pending: '#F59E0B',
  InProgress: '#3B82F6',
  Completed: '#10B981',
  Overdue: '#EF4444',
  Cancelled: '#6B7280',
};

const PIE_COLORS = ['#F59E0B', '#3B82F6', '#10B981', '#EF4444'];

// ============================================
// KPI Card Component
// ============================================

interface KpiCardProps {
  title: string;
  value: string | number;
  subtitle?: string;
  icon: React.ElementType;
  trend?: 'up' | 'down' | 'neutral';
  variant?: 'default' | 'success' | 'warning' | 'danger';
  progress?: number;
  isLoading?: boolean;
}

function KpiCard({
  title,
  value,
  subtitle,
  icon: Icon,
  variant = 'default',
  progress,
  isLoading,
}: KpiCardProps) {
  const bgVariants = {
    default: '',
    success: 'bg-green-50 border-green-200',
    warning: 'bg-yellow-50 border-yellow-200',
    danger: 'bg-red-50 border-red-200',
  };

  const iconVariants = {
    default: 'text-muted-foreground',
    success: 'text-green-600',
    warning: 'text-yellow-600',
    danger: 'text-red-600',
  };

  const valueVariants = {
    default: '',
    success: 'text-green-700',
    warning: 'text-yellow-700',
    danger: 'text-red-700',
  };

  if (isLoading) {
    return (
      <Card className={cn(bgVariants[variant])}>
        <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
          <Skeleton className="h-4 w-24" />
          <Skeleton className="h-4 w-4" />
        </CardHeader>
        <CardContent>
          <Skeleton className="h-8 w-16 mb-2" />
          {progress !== undefined && <Skeleton className="h-2 w-full mb-2" />}
          <Skeleton className="h-3 w-32" />
        </CardContent>
      </Card>
    );
  }

  return (
    <Card className={cn(bgVariants[variant])}>
      <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
        <CardTitle className="text-sm font-medium">{title}</CardTitle>
        <Icon className={cn('h-4 w-4', iconVariants[variant])} />
      </CardHeader>
      <CardContent>
        <div className={cn('text-2xl font-bold', valueVariants[variant])}>{value}</div>
        {progress !== undefined && <Progress value={progress} className="mt-2 h-2" />}
        {subtitle && <p className="text-xs text-muted-foreground mt-2">{subtitle}</p>}
      </CardContent>
    </Card>
  );
}

// ============================================
// Progress Ring Component
// ============================================

interface ProgressRingProps {
  value: number;
  size?: number;
  strokeWidth?: number;
  className?: string;
}

function ProgressRing({ value, size = 60, strokeWidth = 6, className }: ProgressRingProps) {
  const radius = (size - strokeWidth) / 2;
  const circumference = radius * 2 * Math.PI;
  const offset = circumference - (value / 100) * circumference;

  const getColor = (percent: number) => {
    if (percent >= 80) return 'text-green-500';
    if (percent >= 60) return 'text-yellow-500';
    return 'text-red-500';
  };

  return (
    <div className={cn('relative inline-flex items-center justify-center', className)}>
      <svg width={size} height={size} className="transform -rotate-90">
        <circle
          cx={size / 2}
          cy={size / 2}
          r={radius}
          fill="none"
          stroke="currentColor"
          strokeWidth={strokeWidth}
          className="text-muted/20"
        />
        <circle
          cx={size / 2}
          cy={size / 2}
          r={radius}
          fill="none"
          stroke="currentColor"
          strokeWidth={strokeWidth}
          strokeDasharray={circumference}
          strokeDashoffset={offset}
          strokeLinecap="round"
          className={cn('transition-all duration-500', getColor(value))}
        />
      </svg>
      <span className="absolute text-sm font-semibold">{Math.round(value)}%</span>
    </div>
  );
}

// ============================================
// Chart Skeleton Components
// ============================================

function ChartSkeleton() {
  return (
    <div className="h-[300px] w-full animate-pulse bg-muted rounded-md flex items-center justify-center">
      <span className="text-muted-foreground">Loading chart...</span>
    </div>
  );
}

// ============================================
// Dashboard Component
// ============================================

export function Dashboard() {
  const queryClient = useQueryClient();
  const [isRefreshing, setIsRefreshing] = useState(false);

  const { data: dashboard, isLoading, error } = useToolboxTalkDashboard();
  const sendReminder = useSendReminder();

  const handleRefresh = async () => {
    setIsRefreshing(true);
    await queryClient.invalidateQueries({ queryKey: TOOLBOX_TALKS_DASHBOARD_KEY });
    setIsRefreshing(false);
  };

  const handleSendReminder = async (id: string, employeeName: string) => {
    try {
      await sendReminder.mutateAsync(id);
      toast.success(`Reminder sent to ${employeeName}`);
    } catch {
      toast.error('Failed to send reminder');
    }
  };

  // Transform data for charts
  const statusPieData = React.useMemo(() => {
    if (!dashboard?.talksByStatus) return [];
    return [
      { name: 'Pending', value: dashboard.talksByStatus.Pending || 0, color: STATUS_COLORS.Pending },
      { name: 'In Progress', value: dashboard.talksByStatus.InProgress || 0, color: STATUS_COLORS.InProgress },
      { name: 'Completed', value: dashboard.talksByStatus.Completed || 0, color: STATUS_COLORS.Completed },
      { name: 'Overdue', value: dashboard.talksByStatus.Overdue || 0, color: STATUS_COLORS.Overdue },
    ].filter((item) => item.value > 0);
  }, [dashboard?.talksByStatus]);

  // Generate completion trend data (last 30 days)
  const completionTrendData = React.useMemo(() => {
    if (!dashboard?.recentCompletions) return [];

    const last30Days: Record<string, number> = {};
    const today = new Date();

    // Initialize last 30 days with 0
    for (let i = 29; i >= 0; i--) {
      const date = new Date(today);
      date.setDate(date.getDate() - i);
      const key = format(date, 'MMM dd');
      last30Days[key] = 0;
    }

    // Count completions per day
    dashboard.recentCompletions.forEach((completion) => {
      const date = new Date(completion.completedAt);
      const key = format(date, 'MMM dd');
      if (last30Days[key] !== undefined) {
        last30Days[key]++;
      }
    });

    return Object.entries(last30Days).map(([date, count]) => ({
      date,
      completions: count,
    }));
  }, [dashboard?.recentCompletions]);

  // Top completed talks data (horizontal bar)
  const topTalksData = React.useMemo(() => {
    if (!dashboard?.recentCompletions) return [];

    const talkCounts: Record<string, number> = {};
    dashboard.recentCompletions.forEach((completion) => {
      const title = completion.toolboxTalkTitle;
      talkCounts[title] = (talkCounts[title] || 0) + 1;
    });

    return Object.entries(talkCounts)
      .sort((a, b) => b[1] - a[1])
      .slice(0, 5)
      .map(([name, count]) => ({ name, count }));
  }, [dashboard?.recentCompletions]);

  // Recent completions table columns
  const recentCompletionsColumns: Column<RecentCompletion>[] = [
    {
      key: 'employeeName',
      header: 'Employee',
      render: (item) => <span className="font-medium">{item.employeeName}</span>,
    },
    {
      key: 'toolboxTalkTitle',
      header: 'Talk',
      render: (item) => (
        <span className="text-sm text-muted-foreground truncate max-w-[200px] block">
          {item.toolboxTalkTitle}
        </span>
      ),
    },
    {
      key: 'completedAt',
      header: 'Completed',
      render: (item) => (
        <span className="text-sm">
          {formatDistanceToNow(new Date(item.completedAt), { addSuffix: true })}
        </span>
      ),
    },
    {
      key: 'quizScore',
      header: 'Score',
      render: (item) =>
        item.quizScore !== null ? (
          <Badge variant={item.quizPassed ? 'default' : 'destructive'}>
            {item.quizScore}%
          </Badge>
        ) : (
          <span className="text-muted-foreground">-</span>
        ),
    },
  ];

  // Overdue assignments table columns
  const overdueColumns: Column<OverdueAssignment>[] = [
    {
      key: 'employeeName',
      header: 'Employee',
      render: (item) => <span className="font-medium">{item.employeeName}</span>,
    },
    {
      key: 'toolboxTalkTitle',
      header: 'Talk',
      render: (item) => (
        <span className="text-sm text-muted-foreground truncate max-w-[200px] block">
          {item.toolboxTalkTitle}
        </span>
      ),
    },
    {
      key: 'dueDate',
      header: 'Due Date',
      render: (item) => (
        <span className="text-sm">{format(new Date(item.dueDate), 'MMM dd, yyyy')}</span>
      ),
    },
    {
      key: 'daysOverdue',
      header: 'Days Overdue',
      render: (item) => (
        <Badge variant="destructive">{item.daysOverdue} days</Badge>
      ),
    },
    {
      key: 'remindersSent',
      header: 'Reminders',
      render: (item) => <span className="text-sm">{item.remindersSent}</span>,
    },
    {
      key: 'actions',
      header: '',
      render: (item) => (
        <Button
          size="sm"
          variant="outline"
          onClick={(e) => {
            e.stopPropagation();
            handleSendReminder(item.scheduledTalkId, item.employeeName);
          }}
          disabled={sendReminder.isPending}
        >
          <Bell className="h-3 w-3 mr-1" />
          Remind
        </Button>
      ),
    },
  ];

  // Upcoming schedules table columns
  const upcomingColumns: Column<UpcomingSchedule>[] = [
    {
      key: 'toolboxTalkTitle',
      header: 'Talk',
      render: (item) => <span className="font-medium">{item.toolboxTalkTitle}</span>,
    },
    {
      key: 'scheduledDate',
      header: 'Date',
      render: (item) => (
        <span className="text-sm">{format(new Date(item.scheduledDate), 'MMM dd, yyyy')}</span>
      ),
    },
    {
      key: 'assignmentCount',
      header: 'Employees',
      render: (item) => (
        <div className="flex items-center gap-1">
          <Users className="h-3 w-3 text-muted-foreground" />
          <span className="text-sm">
            {item.assignToAllEmployees ? 'All' : item.assignmentCount}
          </span>
        </div>
      ),
    },
    {
      key: 'frequencyDisplay',
      header: 'Frequency',
      render: (item) => (
        <Badge variant="outline" className="text-xs">
          {item.frequencyDisplay}
        </Badge>
      ),
    },
  ];

  if (error) {
    return (
      <div className="space-y-6">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">Learnings Dashboard</h1>
            <p className="text-muted-foreground">Overview of learnings and completions</p>
          </div>
        </div>
        <Card className="p-8 text-center">
          <p className="text-destructive">Failed to load dashboard. Please try again.</p>
          <Button variant="outline" className="mt-4" onClick={handleRefresh}>
            <RefreshCw className="h-4 w-4 mr-2" />
            Retry
          </Button>
        </Card>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Learnings Dashboard</h1>
          <p className="text-muted-foreground">Overview of learnings and completions</p>
        </div>
        <Button variant="outline" onClick={handleRefresh} disabled={isRefreshing || isLoading}>
          <RefreshCw className={cn('h-4 w-4 mr-2', (isRefreshing || isLoading) && 'animate-spin')} />
          Refresh
        </Button>
      </div>

      {/* KPI Cards */}
      <div className="grid gap-4 grid-cols-2 lg:grid-cols-5">
        <KpiCard
          title="Active Talks"
          value={dashboard?.activeTalks ?? 0}
          subtitle={`${dashboard?.totalTalks ?? 0} total talks`}
          icon={FileText}
          isLoading={isLoading}
        />
        <KpiCard
          title="Pending Assignments"
          value={dashboard?.pendingCount ?? 0}
          subtitle={`${dashboard?.inProgressCount ?? 0} in progress`}
          icon={Clock}
          variant="warning"
          isLoading={isLoading}
        />
        <KpiCard
          title="Completed This Month"
          value={dashboard?.completedCount ?? 0}
          subtitle={`${dashboard?.totalAssignments ?? 0} total assignments`}
          icon={CheckCircle}
          variant="success"
          isLoading={isLoading}
        />
        <Card className={cn(isLoading ? '' : '')}>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium">Completion Rate</CardTitle>
            <TrendingUp className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            {isLoading ? (
              <div className="flex items-center justify-center py-2">
                <Skeleton className="h-[60px] w-[60px] rounded-full" />
              </div>
            ) : (
              <div className="flex items-center gap-4">
                <ProgressRing value={dashboard?.completionRate ?? 0} />
                <div className="text-xs text-muted-foreground">
                  <p>Quiz Pass: {dashboard?.quizPassRate?.toFixed(0) ?? 0}%</p>
                  <p>Avg Score: {dashboard?.averageQuizScore?.toFixed(0) ?? 0}%</p>
                </div>
              </div>
            )}
          </CardContent>
        </Card>
        <KpiCard
          title="Overdue"
          value={dashboard?.overdueCount ?? 0}
          subtitle={`${dashboard?.overdueRate?.toFixed(1) ?? 0}% overdue rate`}
          icon={AlertTriangle}
          variant={(dashboard?.overdueCount ?? 0) > 0 ? 'danger' : 'default'}
          isLoading={isLoading}
        />
      </div>

      {/* Charts Section */}
      <div className="grid gap-4 md:grid-cols-2">
        {/* Completion Trend Chart */}
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Completion Trend</CardTitle>
            <CardDescription>Last 30 days</CardDescription>
          </CardHeader>
          <CardContent>
            {isLoading ? (
              <ChartSkeleton />
            ) : completionTrendData.length > 0 ? (
              <ResponsiveContainer width="100%" height={300}>
                <LineChart data={completionTrendData}>
                  <CartesianGrid strokeDasharray="3 3" className="stroke-muted" />
                  <XAxis
                    dataKey="date"
                    tick={{ fontSize: 10 }}
                    interval="preserveStartEnd"
                    className="text-muted-foreground"
                  />
                  <YAxis tick={{ fontSize: 12 }} className="text-muted-foreground" />
                  <Tooltip
                    contentStyle={{
                      backgroundColor: 'hsl(var(--popover))',
                      border: '1px solid hsl(var(--border))',
                      borderRadius: 'var(--radius)',
                    }}
                  />
                  <Line
                    type="monotone"
                    dataKey="completions"
                    stroke="#10B981"
                    strokeWidth={2}
                    dot={false}
                    name="Completions"
                  />
                </LineChart>
              </ResponsiveContainer>
            ) : (
              <div className="h-[300px] flex items-center justify-center text-muted-foreground">
                No completion data available
              </div>
            )}
          </CardContent>
        </Card>

        {/* Assignments by Status Pie Chart */}
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Assignments by Status</CardTitle>
            <CardDescription>Current distribution</CardDescription>
          </CardHeader>
          <CardContent>
            {isLoading ? (
              <ChartSkeleton />
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
                      <Cell key={`cell-${index}`} fill={PIE_COLORS[index % PIE_COLORS.length]} />
                    ))}
                  </Pie>
                  <Tooltip
                    contentStyle={{
                      backgroundColor: 'hsl(var(--popover))',
                      border: '1px solid hsl(var(--border))',
                      borderRadius: 'var(--radius)',
                    }}
                  />
                  <Legend />
                </PieChart>
              </ResponsiveContainer>
            ) : (
              <div className="h-[300px] flex items-center justify-center text-muted-foreground">
                No status data available
              </div>
            )}
          </CardContent>
        </Card>

        {/* Top Talks by Completion */}
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Top Talks by Completion</CardTitle>
            <CardDescription>Most completed talks</CardDescription>
          </CardHeader>
          <CardContent>
            {isLoading ? (
              <ChartSkeleton />
            ) : topTalksData.length > 0 ? (
              <ResponsiveContainer width="100%" height={300}>
                <BarChart data={topTalksData} layout="vertical">
                  <CartesianGrid strokeDasharray="3 3" className="stroke-muted" />
                  <XAxis type="number" tick={{ fontSize: 12 }} className="text-muted-foreground" />
                  <YAxis
                    dataKey="name"
                    type="category"
                    tick={{ fontSize: 10 }}
                    width={150}
                    className="text-muted-foreground"
                  />
                  <Tooltip
                    contentStyle={{
                      backgroundColor: 'hsl(var(--popover))',
                      border: '1px solid hsl(var(--border))',
                      borderRadius: 'var(--radius)',
                    }}
                  />
                  <Bar dataKey="count" fill="#3B82F6" name="Completions" radius={[0, 4, 4, 0]} />
                </BarChart>
              </ResponsiveContainer>
            ) : (
              <div className="h-[300px] flex items-center justify-center text-muted-foreground">
                No completion data available
              </div>
            )}
          </CardContent>
        </Card>

        {/* Average Completion Time */}
        <Card>
          <CardHeader>
            <CardTitle className="text-base">Performance Metrics</CardTitle>
            <CardDescription>Quiz and completion statistics</CardDescription>
          </CardHeader>
          <CardContent>
            {isLoading ? (
              <div className="space-y-4">
                <Skeleton className="h-16 w-full" />
                <Skeleton className="h-16 w-full" />
                <Skeleton className="h-16 w-full" />
              </div>
            ) : (
              <div className="space-y-4">
                <div className="flex items-center justify-between p-4 bg-muted/50 rounded-lg">
                  <div>
                    <p className="text-sm text-muted-foreground">Avg. Completion Time</p>
                    <p className="text-2xl font-bold">
                      {(dashboard?.averageCompletionTimeHours ?? 0).toFixed(1)}h
                    </p>
                  </div>
                  <Clock className="h-8 w-8 text-muted-foreground" />
                </div>
                <div className="flex items-center justify-between p-4 bg-muted/50 rounded-lg">
                  <div>
                    <p className="text-sm text-muted-foreground">Quiz Pass Rate</p>
                    <p className="text-2xl font-bold">
                      {(dashboard?.quizPassRate ?? 0).toFixed(0)}%
                    </p>
                  </div>
                  <CheckCircle className="h-8 w-8 text-green-500" />
                </div>
                <div className="flex items-center justify-between p-4 bg-muted/50 rounded-lg">
                  <div>
                    <p className="text-sm text-muted-foreground">Average Quiz Score</p>
                    <p className="text-2xl font-bold">
                      {(dashboard?.averageQuizScore ?? 0).toFixed(0)}%
                    </p>
                  </div>
                  <TrendingUp className="h-8 w-8 text-blue-500" />
                </div>
              </div>
            )}
          </CardContent>
        </Card>
      </div>

      {/* Recent Completions */}
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Recent Completions</CardTitle>
          <CardDescription>Last 10 completed assignments</CardDescription>
        </CardHeader>
        <CardContent>
          <DataTable
            columns={recentCompletionsColumns}
            data={(dashboard?.recentCompletions ?? []).slice(0, 10)}
            isLoading={isLoading}
            keyExtractor={(item) => item.scheduledTalkId}
            emptyMessage="No recent completions"
          />
        </CardContent>
      </Card>

      {/* Overdue Alerts */}
      <Card className={cn((dashboard?.overdueCount ?? 0) > 0 && 'border-destructive')}>
        <CardHeader>
          <div className="flex items-center gap-2">
            <AlertTriangle
              className={cn(
                'h-5 w-5',
                (dashboard?.overdueCount ?? 0) > 0 ? 'text-destructive' : 'text-muted-foreground'
              )}
            />
            <CardTitle className="text-base">Overdue Assignments</CardTitle>
          </div>
          <CardDescription>
            {(dashboard?.overdueCount ?? 0) > 0
              ? `${dashboard?.overdueCount} assignments require attention`
              : 'No overdue assignments'}
          </CardDescription>
        </CardHeader>
        <CardContent>
          {(dashboard?.overdueCount ?? 0) > 0 ? (
            <DataTable
              columns={overdueColumns}
              data={dashboard?.overdueAssignments ?? []}
              isLoading={isLoading}
              keyExtractor={(item) => item.scheduledTalkId}
              emptyMessage="No overdue assignments"
            />
          ) : (
            <div className="flex items-center justify-center py-8 text-muted-foreground">
              <CheckCircle className="h-8 w-8 mr-2 text-green-500" />
              <span>All assignments are on track!</span>
            </div>
          )}
        </CardContent>
      </Card>

      {/* Upcoming Schedules */}
      <Card>
        <CardHeader className="flex flex-row items-center justify-between">
          <div>
            <CardTitle className="text-base">Upcoming Schedules</CardTitle>
            <CardDescription>Next 7 days of scheduled releases</CardDescription>
          </div>
          <Button variant="outline" size="sm" asChild>
            <Link href="/toolbox-talks/schedules">
              <Calendar className="h-4 w-4 mr-2" />
              View All
            </Link>
          </Button>
        </CardHeader>
        <CardContent>
          {(dashboard?.upcomingSchedules ?? []).length > 0 ? (
            <DataTable
              columns={upcomingColumns}
              data={dashboard?.upcomingSchedules ?? []}
              isLoading={isLoading}
              keyExtractor={(item) => item.scheduleId}
              emptyMessage="No upcoming schedules"
              onRowClick={(item) => {
                window.location.href = `/toolbox-talks/schedules/${item.scheduleId}`;
              }}
            />
          ) : (
            <div className="flex items-center justify-center py-8 text-muted-foreground">
              <Calendar className="h-8 w-8 mr-2" />
              <span>No upcoming schedules in the next 7 days</span>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

// ============================================
// Dashboard Skeleton
// ============================================

export function DashboardSkeleton() {
  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <Skeleton className="h-8 w-64 mb-2" />
          <Skeleton className="h-4 w-48" />
        </div>
        <Skeleton className="h-10 w-24" />
      </div>

      <div className="grid gap-4 grid-cols-2 lg:grid-cols-5">
        {Array.from({ length: 5 }).map((_, i) => (
          <Card key={i}>
            <CardHeader className="pb-2">
              <Skeleton className="h-4 w-24" />
            </CardHeader>
            <CardContent>
              <Skeleton className="h-8 w-16 mb-2" />
              <Skeleton className="h-3 w-32" />
            </CardContent>
          </Card>
        ))}
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        {Array.from({ length: 4 }).map((_, i) => (
          <Card key={i}>
            <CardHeader>
              <Skeleton className="h-5 w-40 mb-1" />
              <Skeleton className="h-4 w-24" />
            </CardHeader>
            <CardContent>
              <Skeleton className="h-[300px] w-full" />
            </CardContent>
          </Card>
        ))}
      </div>
    </div>
  );
}
