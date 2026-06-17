'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { format } from 'date-fns';
import {
  PencilIcon,
  TrashIcon,
  CalendarClockIcon,
  VideoIcon,
  FileTextIcon,
  ClockIcon,
  AlertTriangleIcon,
  ListChecksIcon,
  ExternalLinkIcon,
  EyeIcon,
  CheckCircle2Icon,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { DeleteConfirmationDialog } from '@/components/shared/delete-confirmation-dialog';
import { PreviewModal } from './PreviewModal';
import { ValidationHistoryTab } from './ValidationHistoryTab';
import { SectionEditPanel } from './detail/SectionEditPanel';
import { QuizEditPanel } from './detail/QuizEditPanel';
import { useToolboxTalk, useDeleteToolboxTalk } from '@/lib/api/toolbox-talks';
import { usePermission } from '@/lib/auth/use-auth';
import { useWizardPreference } from '@/features/toolbox-talks/hooks/useWizardPreference';
import type { ToolboxTalk } from '@/types/toolbox-talks';
import { toast } from 'sonner';
import { cn } from '@/lib/utils';

interface ToolboxTalkDetailProps {
  talkId: string;
  onSchedule?: (talk: ToolboxTalk) => void;
  /** Base path for navigation (default: /admin/toolbox-talks/talks) */
  basePath?: string;
  previewMode?: boolean;
}

export function ToolboxTalkDetail({ talkId, onSchedule, basePath = '/admin/toolbox-talks/talks', previewMode = false }: ToolboxTalkDetailProps) {
  const router = useRouter();
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [previewOpen, setPreviewOpen] = useState(false);
  const canManage = usePermission('Learnings.Manage');
  const canSchedule = usePermission('Learnings.Schedule');

  const { data: talk, isLoading, error, refetch } = useToolboxTalk(talkId);
  const deleteMutation = useDeleteToolboxTalk();
  const wizardPreference = useWizardPreference();

  const handleDelete = async () => {
    if (!talk) return;

    try {
      await deleteMutation.mutateAsync(talk.id);
      toast.success('Learning deleted successfully');
      router.push(basePath);
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Failed to delete learning';
      toast.error('Error', { description: message });
    }
  };

  if (isLoading) {
    return <ToolboxTalkDetailSkeleton />;
  }

  if (error) {
    return (
      <div className="rounded-lg border border-destructive/50 bg-destructive/10 p-4">
        <p className="text-destructive">
          Error loading learning: {error instanceof Error ? error.message : 'Unknown error'}
        </p>
      </div>
    );
  }

  if (!talk) {
    return (
      <div className="rounded-lg border p-8 text-center">
        <p className="text-muted-foreground">Learning not found</p>
      </div>
    );
  }

  const stats = talk.completionStats;
  const isPartOfCourse = talk.isPartOfCourse;

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
        <div className="space-y-1">
          <div className="flex items-center gap-3">
            {talk.code && (
              <Badge variant="outline" className="text-sm font-mono">
                {talk.code}
              </Badge>
            )}
            <h1 className="text-2xl font-bold">{talk.title}</h1>
            <Badge
              variant={talk.isActive ? 'default' : 'secondary'}
              className={cn(
                talk.isActive
                  ? 'bg-green-100 text-green-800 hover:bg-green-100 dark:bg-green-900/20 dark:text-green-400'
                  : 'bg-gray-100 text-gray-800 hover:bg-gray-100 dark:bg-gray-900/20 dark:text-gray-400'
              )}
            >
              {talk.isActive ? 'Active' : 'Inactive'}
            </Badge>
          </div>
          {talk.description && (
            <p className="text-muted-foreground">{talk.description}</p>
          )}
          <div className="flex items-center gap-4 text-sm text-muted-foreground">
            {talk.category && (
              <Badge variant="secondary">{talk.category}</Badge>
            )}
            <span>Created {format(new Date(talk.createdAt), 'dd MMM yyyy')}</span>
            {talk.updatedAt && (
              <span>Updated {format(new Date(talk.updatedAt), 'dd MMM yyyy')}</span>
            )}
          </div>
        </div>

        {!previewMode && (
          <div className="flex flex-wrap gap-2">
            <Button variant="outline" onClick={() => setPreviewOpen(true)}>
              <EyeIcon className="mr-2 h-4 w-4" />
              Preview as Employee
            </Button>
            {canManage && wizardPreference === 'old' && (
              <Button variant="outline" onClick={() => router.push(`${basePath}/${talk.id}/edit`)}>
                <PencilIcon className="mr-2 h-4 w-4" />
                Edit
              </Button>
            )}
            {canSchedule && (
              <Button
                variant="outline"
                onClick={() => onSchedule?.(talk)}
                disabled={!talk.isActive}
                title={!talk.isActive ? 'Only active talks can be scheduled' : undefined}
              >
                <CalendarClockIcon className="mr-2 h-4 w-4" />
                Schedule
              </Button>
            )}
            {canManage && (
              <Button
                variant="outline"
                className="text-destructive hover:text-destructive"
                onClick={() => setDeleteDialogOpen(true)}
              >
                <TrashIcon className="mr-2 h-4 w-4" />
                Delete
              </Button>
            )}
          </div>
        )}
      </div>

      {/* Statistics */}
      {!previewMode && stats && (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <Card>
            <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
              <CardTitle className="text-sm font-medium">Total Assigned</CardTitle>
              <ListChecksIcon className="h-4 w-4 text-muted-foreground" />
            </CardHeader>
            <CardContent>
              <div className="text-2xl font-bold">{stats.totalAssignments}</div>
            </CardContent>
          </Card>
          <Card>
            <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
              <CardTitle className="text-sm font-medium">Completed</CardTitle>
              <CheckCircle2Icon className="h-4 w-4 text-green-500" />
            </CardHeader>
            <CardContent>
              <div className="text-2xl font-bold text-green-600">{stats.completedCount}</div>
              <p className="text-xs text-muted-foreground">
                {stats.completionRate.toFixed(1)}% completion rate
              </p>
            </CardContent>
          </Card>
          <Card>
            <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
              <CardTitle className="text-sm font-medium">Pending</CardTitle>
              <ClockIcon className="h-4 w-4 text-blue-500" />
            </CardHeader>
            <CardContent>
              <div className="text-2xl font-bold text-blue-600">
                {stats.pendingCount + stats.inProgressCount}
              </div>
              <p className="text-xs text-muted-foreground">
                {stats.inProgressCount} in progress
              </p>
            </CardContent>
          </Card>
          <Card>
            <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
              <CardTitle className="text-sm font-medium">Overdue</CardTitle>
              <AlertTriangleIcon className="h-4 w-4 text-destructive" />
            </CardHeader>
            <CardContent>
              <div className="text-2xl font-bold text-destructive">{stats.overdueCount}</div>
            </CardContent>
          </Card>
        </div>
      )}

      {/* Tabs: Overview / Validation */}
      <Tabs defaultValue="overview">
        <TabsList>
          <TabsTrigger value="overview">Overview</TabsTrigger>
          {!isPartOfCourse && !previewMode && (
            <TabsTrigger value="validation">Validation</TabsTrigger>
          )}
        </TabsList>

        <TabsContent value="overview" className="space-y-6 mt-4">
          <div className="grid gap-6 lg:grid-cols-3">
            {/* Talk Details */}
            <Card className="lg:col-span-2">
              <CardHeader>
                <CardTitle>Talk Details</CardTitle>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="grid gap-4 sm:grid-cols-2">
                  <div>
                    <label className="text-sm font-medium text-muted-foreground">Frequency</label>
                    <p className="mt-1">{talk.frequencyDisplay}</p>
                  </div>
                  <div>
                    <label className="text-sm font-medium text-muted-foreground">Sections</label>
                    <p className="mt-1">{talk.sections.length}</p>
                  </div>
                  <div>
                    <label className="text-sm font-medium text-muted-foreground">Video</label>
                    <div className="mt-1 flex items-center gap-2">
                      {talk.videoSource !== 'None' ? (
                        <>
                          <VideoIcon className="h-4 w-4 text-muted-foreground" />
                          <span>{talk.videoSourceDisplay}</span>
                          {talk.videoUrl && (
                            <a
                              href={talk.videoUrl}
                              target="_blank"
                              rel="noopener noreferrer"
                              className="text-primary hover:underline"
                            >
                              <ExternalLinkIcon className="h-4 w-4" />
                            </a>
                          )}
                        </>
                      ) : (
                        <span className="text-muted-foreground">No video</span>
                      )}
                    </div>
                  </div>
                  <div>
                    <label className="text-sm font-medium text-muted-foreground">Minimum Watch %</label>
                    <p className="mt-1">
                      {talk.videoSource !== 'None' ? `${talk.minimumVideoWatchPercent}%` : '-'}
                    </p>
                  </div>
                  <div>
                    <label className="text-sm font-medium text-muted-foreground">Quiz Required</label>
                    <p className="mt-1">{talk.requiresQuiz ? 'Yes' : 'No'}</p>
                  </div>
                  {talk.requiresQuiz && (
                    <>
                      <div>
                        <label className="text-sm font-medium text-muted-foreground">Passing Score</label>
                        <p className="mt-1">{talk.passingScore}%</p>
                      </div>
                      <div>
                        <label className="text-sm font-medium text-muted-foreground">Questions</label>
                        <p className="mt-1">{talk.questions.length}</p>
                      </div>
                    </>
                  )}
                  {talk.attachmentUrl && (
                    <div className="sm:col-span-2">
                      <label className="text-sm font-medium text-muted-foreground">Attachment</label>
                      <div className="mt-1">
                        <a
                          href={talk.attachmentUrl}
                          target="_blank"
                          rel="noopener noreferrer"
                          className="inline-flex items-center gap-2 text-primary hover:underline"
                        >
                          <FileTextIcon className="h-4 w-4" />
                          Download Attachment
                          <ExternalLinkIcon className="h-3 w-3" />
                        </a>
                      </div>
                    </div>
                  )}
                </div>
              </CardContent>
            </Card>

            {/* Recent Completions placeholder */}
            {!previewMode && (
              <Card>
                <CardHeader>
                  <CardTitle>Recent Completions</CardTitle>
                  <CardDescription>Latest employees to complete this talk</CardDescription>
                </CardHeader>
                <CardContent>
                  <p className="text-sm text-muted-foreground text-center py-8">
                    View completions in the Assignments tab
                  </p>
                </CardContent>
              </Card>
            )}
          </div>

          {/* Sections — inline editable for new-wizard talks */}
          <SectionEditPanel talk={talk} onRefetch={refetch} />

          {/* Quiz Questions — inline editable for new-wizard talks */}
          <QuizEditPanel talk={talk} onRefetch={refetch} />
        </TabsContent>

        {!isPartOfCourse && !previewMode && (
          <TabsContent value="validation" className="mt-4">
            <ValidationHistoryTab talkId={talkId} basePath={basePath} />
          </TabsContent>
        )}
      </Tabs>

      {/* Delete confirmation dialog */}
      {!previewMode && (
        <DeleteConfirmationDialog
          open={deleteDialogOpen}
          onOpenChange={setDeleteDialogOpen}
          title="Delete Learning"
          description={`Are you sure you want to delete "${talk.title}"? This action cannot be undone and will also delete all associated schedules and assignments.`}
          onConfirm={handleDelete}
          isLoading={deleteMutation.isPending}
        />
      )}

      {/* Preview as Employee modal */}
      {!previewMode && (
        <PreviewModal
          open={previewOpen}
          onOpenChange={setPreviewOpen}
          talk={talk}
        />
      )}
    </div>
  );
}

function ToolboxTalkDetailSkeleton() {
  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
        <div className="space-y-2">
          <Skeleton className="h-8 w-64" />
          <Skeleton className="h-4 w-96" />
          <Skeleton className="h-4 w-48" />
        </div>
        <div className="flex gap-2">
          <Skeleton className="h-10 w-24" />
          <Skeleton className="h-10 w-28" />
          <Skeleton className="h-10 w-24" />
        </div>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {[...Array(4)].map((_, i) => (
          <Card key={i}>
            <CardHeader className="pb-2">
              <Skeleton className="h-4 w-24" />
            </CardHeader>
            <CardContent>
              <Skeleton className="h-8 w-16" />
            </CardContent>
          </Card>
        ))}
      </div>

      <div className="grid gap-6 lg:grid-cols-3">
        <Card className="lg:col-span-2">
          <CardHeader>
            <Skeleton className="h-6 w-32" />
          </CardHeader>
          <CardContent>
            <div className="grid gap-4 sm:grid-cols-2">
              {[...Array(6)].map((_, i) => (
                <div key={i}>
                  <Skeleton className="h-4 w-20 mb-1" />
                  <Skeleton className="h-5 w-32" />
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <Skeleton className="h-6 w-40" />
          </CardHeader>
          <CardContent>
            <Skeleton className="h-32 w-full" />
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
