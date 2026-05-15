'use client';

import { useState } from 'react';
import { useParams, useRouter, useSearchParams } from 'next/navigation';
import { AlertTriangle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { ChevronLeft } from 'lucide-react';
import { ToolboxTalkDetail } from '@/features/toolbox-talks/components/ToolboxTalkDetail';
import { ScheduleDialog } from '@/features/toolbox-talks/components/ScheduleDialog';
import type { ToolboxTalk } from '@/types/toolbox-talks';

export default function AdminToolboxTalkDetailPage() {
  const params = useParams();
  const router = useRouter();
  const searchParams = useSearchParams();
  const talkId = params.id as string;
  const previewMode = searchParams.get('preview') === 'true';

  const [scheduleDialogOpen, setScheduleDialogOpen] = useState(false);
  const [selectedTalk, setSelectedTalk] = useState<ToolboxTalk | null>(null);

  const handleSchedule = (talk: ToolboxTalk) => {
    setSelectedTalk(talk);
    setScheduleDialogOpen(true);
  };

  return (
    <div className="space-y-6">
      {previewMode && (
        <Alert className="border-amber-300 bg-amber-50 dark:border-amber-700 dark:bg-amber-950/30">
          <AlertTriangle className="h-4 w-4 text-amber-600" />
          <AlertDescription className="text-amber-700 dark:text-amber-400">
            Preview mode — this is how learners see this content. No changes can be made in preview mode.
          </AlertDescription>
        </Alert>
      )}

      <div className="flex items-center gap-4">
        <Button variant="ghost" size="icon" onClick={() => router.push('/admin/toolbox-talks/talks')}>
          <ChevronLeft className="h-4 w-4" />
        </Button>
        <span className="text-muted-foreground">Back to Learnings</span>
      </div>

      <ToolboxTalkDetail
        talkId={talkId}
        onSchedule={handleSchedule}
        previewMode={previewMode}
      />

      {!previewMode && (
        <ScheduleDialog
          open={scheduleDialogOpen}
          onOpenChange={setScheduleDialogOpen}
          toolboxTalkId={selectedTalk?.id}
        />
      )}
    </div>
  );
}
