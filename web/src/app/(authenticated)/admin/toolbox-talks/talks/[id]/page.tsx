'use client';

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { ChevronLeft } from 'lucide-react';
import { ToolboxTalkDetail } from '@/features/toolbox-talks/components/ToolboxTalkDetail';
import { ScheduleDialog } from '@/features/toolbox-talks/components/ScheduleDialog';
import type { ToolboxTalk } from '@/types/toolbox-talks';

export default function AdminToolboxTalkDetailPage() {
  const params = useParams();
  const router = useRouter();
  const talkId = params.id as string;

  const [scheduleDialogOpen, setScheduleDialogOpen] = useState(false);
  const [selectedTalk, setSelectedTalk] = useState<ToolboxTalk | null>(null);

  const handleSchedule = (talk: ToolboxTalk) => {
    setSelectedTalk(talk);
    setScheduleDialogOpen(true);
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-4">
        <Button variant="ghost" size="icon" onClick={() => router.push('/admin/toolbox-talks/talks')}>
          <ChevronLeft className="h-4 w-4" />
        </Button>
        <span className="text-muted-foreground">Back to Learnings</span>
      </div>

      <ToolboxTalkDetail talkId={talkId} onSchedule={handleSchedule} />

      <ScheduleDialog
        open={scheduleDialogOpen}
        onOpenChange={setScheduleDialogOpen}
        toolboxTalkId={selectedTalk?.id}
      />
    </div>
  );
}
