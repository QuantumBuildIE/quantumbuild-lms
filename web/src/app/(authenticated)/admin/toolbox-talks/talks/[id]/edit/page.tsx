'use client';

import { useParams, useRouter } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { ChevronLeft } from 'lucide-react';
import { Skeleton } from '@/components/ui/skeleton';
import { Card, CardContent, CardHeader } from '@/components/ui/card';
import { ToolboxTalkForm } from '@/features/toolbox-talks/components/ToolboxTalkForm';
import { useToolboxTalk } from '@/lib/api/toolbox-talks';
import type { ToolboxTalk } from '@/types/toolbox-talks';

export default function EditToolboxTalkPage() {
  const params = useParams();
  const router = useRouter();
  const talkId = params.id as string;
  
  const { data: talk, isLoading, error } = useToolboxTalk(talkId);

  const handleSuccess = (updatedTalk: ToolboxTalk) => {
    router.push(`/admin/toolbox-talks/talks/${updatedTalk.id}`);
  };

  const handleCancel = () => {
    router.push(`/admin/toolbox-talks/talks/${talkId}`);
  };

  if (isLoading) {
    return (
      <div className="space-y-6">
        <div className="flex items-center gap-4">
          <Skeleton className="h-10 w-10" />
          <div>
            <Skeleton className="h-8 w-64 mb-2" />
            <Skeleton className="h-4 w-48" />
          </div>
        </div>
        <Card>
          <CardHeader>
            <Skeleton className="h-6 w-40" />
          </CardHeader>
          <CardContent className="space-y-4">
            <Skeleton className="h-10 w-full" />
            <Skeleton className="h-24 w-full" />
            <Skeleton className="h-10 w-1/2" />
          </CardContent>
        </Card>
      </div>
    );
  }

  if (error || !talk) {
    return (
      <div className="space-y-6">
        <div className="flex items-center gap-4">
          <Button variant="ghost" size="icon" onClick={() => router.back()}>
            <ChevronLeft className="h-4 w-4" />
          </Button>
          <span className="text-muted-foreground">Back</span>
        </div>
        <Card className="p-8 text-center">
          <p className="text-destructive">
            {error instanceof Error ? error.message : 'Toolbox talk not found'}
          </p>
          <Button className="mt-4" onClick={() => router.push('/admin/toolbox-talks/talks')}>
            Back to Talks
          </Button>
        </Card>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-4">
        <Button variant="ghost" size="icon" onClick={() => router.push('/admin/toolbox-talks/talks')}>
          <ChevronLeft className="h-4 w-4" />
        </Button>
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Edit Learning</h1>
          <p className="text-muted-foreground">
            Update "{talk.title}"
          </p>
        </div>
      </div>

      <ToolboxTalkForm talk={talk} onSuccess={handleSuccess} onCancel={handleCancel} />
    </div>
  );
}
