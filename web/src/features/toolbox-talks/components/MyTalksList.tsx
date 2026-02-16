'use client';

import * as React from 'react';
import { useRouter } from 'next/navigation';
import { ClipboardList, Inbox } from 'lucide-react';

import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import {
  useMyToolboxTalks,
  useMyPendingToolboxTalks,
  useMyInProgressToolboxTalks,
  useMyCompletedToolboxTalks,
  useMyOverdueToolboxTalks,
} from '@/lib/api/toolbox-talks/use-my-toolbox-talks';
import type { MyToolboxTalkListItem, ScheduledTalkStatus } from '@/types/toolbox-talks';

import { TalkCard, TalkCardSkeleton } from './TalkCard';

type TabValue = 'all' | 'pending' | 'in-progress' | 'overdue' | 'completed';

interface TabConfig {
  value: TabValue;
  label: string;
  emptyTitle: string;
  emptyDescription: string;
}

const tabConfigs: TabConfig[] = [
  {
    value: 'pending',
    label: 'Pending',
    emptyTitle: 'No pending talks',
    emptyDescription: 'You have no pending toolbox talks to complete.',
  },
  {
    value: 'in-progress',
    label: 'In Progress',
    emptyTitle: 'No talks in progress',
    emptyDescription: 'You have no toolbox talks currently in progress.',
  },
  {
    value: 'overdue',
    label: 'Overdue',
    emptyTitle: 'No overdue talks',
    emptyDescription: 'Great job! You have no overdue toolbox talks.',
  },
  {
    value: 'completed',
    label: 'Completed',
    emptyTitle: 'No completed talks',
    emptyDescription: 'You haven\'t completed any toolbox talks yet.',
  },
  {
    value: 'all',
    label: 'All',
    emptyTitle: 'No toolbox talks',
    emptyDescription: 'You have no assigned toolbox talks.',
  },
];

interface EmptyStateProps {
  title: string;
  description: string;
}

function EmptyState({ title, description }: EmptyStateProps) {
  return (
    <div className="flex flex-col items-center justify-center py-12 text-center">
      <div className="rounded-full bg-muted p-4 mb-4">
        <Inbox className="h-8 w-8 text-muted-foreground" />
      </div>
      <h3 className="text-lg font-medium mb-1">{title}</h3>
      <p className="text-sm text-muted-foreground max-w-sm">{description}</p>
    </div>
  );
}

function TalksGrid({
  talks,
  onAction,
  isLoading,
}: {
  talks: MyToolboxTalkListItem[];
  onAction: (id: string) => void;
  isLoading: boolean;
}) {
  if (isLoading) {
    return (
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {Array.from({ length: 6 }).map((_, i) => (
          <TalkCardSkeleton key={i} />
        ))}
      </div>
    );
  }

  // Sort by due date (overdue first, then by closest due date)
  const sortedTalks = [...talks].sort((a, b) => {
    // Overdue items first
    if (a.isOverdue && !b.isOverdue) return -1;
    if (!a.isOverdue && b.isOverdue) return 1;

    // Then by due date (closest first)
    return new Date(a.dueDate).getTime() - new Date(b.dueDate).getTime();
  });

  return (
    <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
      {sortedTalks.map((talk) => (
        <TalkCard key={talk.scheduledTalkId} talk={talk} onAction={onAction} />
      ))}
    </div>
  );
}

interface TabContentProps {
  tab: TabConfig;
  data: MyToolboxTalkListItem[] | undefined;
  isLoading: boolean;
  error: Error | null;
  onAction: (id: string) => void;
}

function ErrorState({ message }: { message: string }) {
  return (
    <div className="flex flex-col items-center justify-center py-12 text-center">
      <div className="rounded-full bg-destructive/10 p-4 mb-4">
        <Inbox className="h-8 w-8 text-destructive" />
      </div>
      <h3 className="text-lg font-medium mb-1">Something went wrong</h3>
      <p className="text-sm text-muted-foreground max-w-sm">{message}</p>
    </div>
  );
}

function TabContent({ tab, data, isLoading, error, onAction }: TabContentProps) {
  if (error) {
    return <ErrorState message="Failed to load toolbox talks. Please try again." />;
  }

  if (!isLoading && (!data || data.length === 0)) {
    return <EmptyState title={tab.emptyTitle} description={tab.emptyDescription} />;
  }

  return <TalksGrid talks={data || []} onAction={onAction} isLoading={isLoading} />;
}

export function MyTalksList() {
  const router = useRouter();
  const [activeTab, setActiveTab] = React.useState<TabValue>('pending');

  // Fetch data for each tab
  const pendingQuery = useMyPendingToolboxTalks();
  const inProgressQuery = useMyInProgressToolboxTalks();
  const overdueQuery = useMyOverdueToolboxTalks();
  const completedQuery = useMyCompletedToolboxTalks();
  const allQuery = useMyToolboxTalks();

  const handleAction = (scheduledTalkId: string) => {
    router.push(`/toolbox-talks/${scheduledTalkId}`);
  };

  // Get query for current tab
  const getTabQuery = (tab: TabValue) => {
    switch (tab) {
      case 'pending':
        return pendingQuery;
      case 'in-progress':
        return inProgressQuery;
      case 'overdue':
        return overdueQuery;
      case 'completed':
        return completedQuery;
      case 'all':
      default:
        return allQuery;
    }
  };

  // Get count for tab badges
  const getTabCount = (tab: TabValue): number | undefined => {
    const query = getTabQuery(tab);
    if (query.isLoading || !query.data) return undefined;
    return query.data.totalCount;
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">My Toolbox Talks</h1>
          <p className="text-muted-foreground">
            Complete your assigned safety training talks
          </p>
        </div>
        <div className="flex items-center gap-2">
          <ClipboardList className="h-5 w-5 text-muted-foreground" />
          <span className="text-sm text-muted-foreground">
            {allQuery.data?.totalCount ?? 0} total assignments
          </span>
        </div>
      </div>

      {/* Tabs */}
      <Tabs value={activeTab} onValueChange={(v) => setActiveTab(v as TabValue)}>
        <TabsList className="flex-wrap h-auto gap-1">
          {tabConfigs.map((tab) => {
            const count = getTabCount(tab.value);
            return (
              <TabsTrigger key={tab.value} value={tab.value} className="gap-2">
                {tab.label}
                {count !== undefined && count > 0 && (
                  <span className="text-xs bg-primary/10 text-primary px-1.5 py-0.5 rounded-full">
                    {count}
                  </span>
                )}
              </TabsTrigger>
            );
          })}
        </TabsList>

        {tabConfigs.map((tab) => {
          const query = getTabQuery(tab.value);
          return (
            <TabsContent key={tab.value} value={tab.value} className="mt-6">
              <TabContent
                tab={tab}
                data={query.data?.items}
                isLoading={query.isLoading}
                error={query.error}
                onAction={handleAction}
              />
            </TabsContent>
          );
        })}
      </Tabs>
    </div>
  );
}
