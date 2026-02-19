import {
  CheckCircle2Icon,
  ClockIcon,
  PlayCircleIcon,
  AlertTriangleIcon,
} from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import type {
  ScheduledTalkStatus,
  ToolboxTalkScheduleStatus,
  CourseAssignmentStatus,
} from '@/types/toolbox-talks';

// ============================================
// ScheduledTalkStatus
// ============================================

export const SCHEDULED_TALK_STATUS_LABELS: Record<ScheduledTalkStatus, string> = {
  Pending: 'Pending',
  InProgress: 'In Progress',
  Completed: 'Completed',
  Overdue: 'Overdue',
  Cancelled: 'Cancelled',
};

export const SCHEDULED_TALK_STATUS_BADGE_CLASSES: Record<ScheduledTalkStatus, string> = {
  Completed: 'bg-green-100 text-green-800 hover:bg-green-100 dark:bg-green-900/20 dark:text-green-400',
  Pending: 'bg-blue-100 text-blue-800 hover:bg-blue-100 dark:bg-blue-900/20 dark:text-blue-400',
  InProgress: 'bg-yellow-100 text-yellow-800 hover:bg-yellow-100 dark:bg-yellow-900/20 dark:text-yellow-400',
  Overdue: 'bg-red-100 text-red-800 hover:bg-red-100 dark:bg-red-900/20 dark:text-red-400',
  Cancelled: 'bg-gray-100 text-gray-800 hover:bg-gray-100 dark:bg-gray-900/20 dark:text-gray-400',
};

export const SCHEDULED_TALK_STATUS_VARIANTS: Record<ScheduledTalkStatus, 'default' | 'secondary' | 'destructive' | 'outline'> = {
  Pending: 'secondary',
  InProgress: 'default',
  Completed: 'outline',
  Overdue: 'destructive',
  Cancelled: 'outline',
};

export const SCHEDULED_TALK_STATUS_CHART_COLORS: Record<ScheduledTalkStatus, string> = {
  Pending: '#F59E0B',
  InProgress: '#3B82F6',
  Completed: '#10B981',
  Overdue: '#EF4444',
  Cancelled: '#6B7280',
};

export const SCHEDULED_TALK_STATUS_OPTIONS: { value: ScheduledTalkStatus | 'all'; label: string }[] = [
  { value: 'all', label: 'All Status' },
  { value: 'Pending', label: 'Pending' },
  { value: 'InProgress', label: 'In Progress' },
  { value: 'Completed', label: 'Completed' },
  { value: 'Overdue', label: 'Overdue' },
  { value: 'Cancelled', label: 'Cancelled' },
];

export function getScheduledTalkStatusIcon(status: ScheduledTalkStatus) {
  switch (status) {
    case 'Completed':
      return <CheckCircle2Icon className="h-4 w-4 text-green-600" />;
    case 'Pending':
      return <ClockIcon className="h-4 w-4 text-blue-600" />;
    case 'InProgress':
      return <PlayCircleIcon className="h-4 w-4 text-yellow-600" />;
    case 'Overdue':
      return <AlertTriangleIcon className="h-4 w-4 text-red-600" />;
    default:
      return null;
  }
}

// ============================================
// ToolboxTalkScheduleStatus
// ============================================

export const SCHEDULE_STATUS_BADGE_CLASSES: Record<ToolboxTalkScheduleStatus, string> = {
  Active: 'bg-green-100 text-green-800 hover:bg-green-100 dark:bg-green-900/20 dark:text-green-400',
  Draft: 'bg-blue-100 text-blue-800 hover:bg-blue-100 dark:bg-blue-900/20 dark:text-blue-400',
  Completed: 'bg-gray-100 text-gray-800 hover:bg-gray-100 dark:bg-gray-900/20 dark:text-gray-400',
  Cancelled: 'bg-red-100 text-red-800 hover:bg-red-100 dark:bg-red-900/20 dark:text-red-400',
};

export const SCHEDULE_STATUS_OPTIONS: { value: ToolboxTalkScheduleStatus | 'all'; label: string }[] = [
  { value: 'all', label: 'All Status' },
  { value: 'Draft', label: 'Draft' },
  { value: 'Active', label: 'Active' },
  { value: 'Completed', label: 'Completed' },
  { value: 'Cancelled', label: 'Cancelled' },
];

// ============================================
// CourseAssignmentStatus
// ============================================

export const COURSE_ASSIGNMENT_STATUS_BADGE_CLASSES: Record<CourseAssignmentStatus, string> = {
  Completed: 'bg-green-100 text-green-800 hover:bg-green-100 dark:bg-green-900/20 dark:text-green-400',
  Assigned: 'bg-blue-100 text-blue-800 hover:bg-blue-100 dark:bg-blue-900/20 dark:text-blue-400',
  InProgress: 'bg-yellow-100 text-yellow-800 hover:bg-yellow-100 dark:bg-yellow-900/20 dark:text-yellow-400',
  Overdue: 'bg-red-100 text-red-800 hover:bg-red-100 dark:bg-red-900/20 dark:text-red-400',
};

export const COURSE_ASSIGNMENT_STATUS_LABELS: Record<CourseAssignmentStatus, string> = {
  Assigned: 'Assigned',
  InProgress: 'In Progress',
  Completed: 'Completed',
  Overdue: 'Overdue',
};

export const COURSE_ASSIGNMENT_STATUS_VARIANTS: Record<CourseAssignmentStatus, 'default' | 'secondary' | 'destructive' | 'outline'> = {
  Assigned: 'secondary',
  InProgress: 'default',
  Completed: 'outline',
  Overdue: 'destructive',
};

export function getCourseAssignmentStatusBadge(status: string) {
  switch (status) {
    case 'Completed':
      return <Badge variant="outline" className="bg-green-100 text-green-800 border-green-200 dark:bg-green-900/20 dark:text-green-400 dark:border-green-800">Completed</Badge>;
    case 'InProgress':
      return <Badge variant="default">In Progress</Badge>;
    case 'Overdue':
      return <Badge variant="destructive">Overdue</Badge>;
    case 'Assigned':
    default:
      return <Badge variant="secondary">Assigned</Badge>;
  }
}

export function getCourseAssignmentStatusIcon(status: string) {
  switch (status) {
    case 'Completed':
      return <CheckCircle2Icon className="h-4 w-4 text-green-600" />;
    case 'Assigned':
      return <ClockIcon className="h-4 w-4 text-blue-600" />;
    case 'InProgress':
      return <PlayCircleIcon className="h-4 w-4 text-yellow-600" />;
    case 'Overdue':
      return <AlertTriangleIcon className="h-4 w-4 text-red-600" />;
    default:
      return null;
  }
}
