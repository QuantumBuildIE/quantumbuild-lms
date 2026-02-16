'use client';

import { useState } from 'react';
import { ToolboxTalkList } from '@/features/toolbox-talks/components/ToolboxTalkList';
import { ScheduleDialog } from '@/features/toolbox-talks/components/ScheduleDialog';
import type { ToolboxTalkListItem } from '@/types/toolbox-talks';

export default function AdminToolboxTalksListPage() {
  const [scheduleDialogOpen, setScheduleDialogOpen] = useState(false);
  const [selectedTalk, setSelectedTalk] = useState<ToolboxTalkListItem | null>(null);

  const handleSchedule = (talk: ToolboxTalkListItem) => {
    setSelectedTalk(talk);
    setScheduleDialogOpen(true);
  };

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Toolbox Talks</h1>
        <p className="text-muted-foreground">
          Manage and schedule safety toolbox talks
        </p>
      </div>

      <ToolboxTalkList onSchedule={handleSchedule} />

      <ScheduleDialog
        open={scheduleDialogOpen}
        onOpenChange={setScheduleDialogOpen}
        toolboxTalkId={selectedTalk?.id}
      />
    </div>
  );
}
