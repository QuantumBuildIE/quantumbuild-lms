'use client';

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { PlusIcon } from 'lucide-react';
import { ScheduleList } from '@/features/toolbox-talks/components/ScheduleList';
import { ScheduleDialog } from '@/features/toolbox-talks/components/ScheduleDialog';
import { usePermission } from '@/lib/auth/use-auth';
import type { ToolboxTalkScheduleListItem } from '@/types/toolbox-talks';

export default function AdminSchedulesListPage() {
  const canSchedule = usePermission('Learnings.Schedule');
  const [scheduleDialogOpen, setScheduleDialogOpen] = useState(false);
  const [selectedSchedule, setSelectedSchedule] = useState<ToolboxTalkScheduleListItem | null>(null);

  const handleEdit = (schedule: ToolboxTalkScheduleListItem) => {
    setSelectedSchedule(schedule);
    setScheduleDialogOpen(true);
  };

  const handleCreate = () => {
    setSelectedSchedule(null);
    setScheduleDialogOpen(true);
  };

  const handleDialogClose = (open: boolean) => {
    setScheduleDialogOpen(open);
    if (!open) {
      setSelectedSchedule(null);
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Schedules</h1>
          <p className="text-muted-foreground">
            Manage scheduled learning distributions
          </p>
        </div>
        {canSchedule && (
          <Button onClick={handleCreate}>
            <PlusIcon className="mr-2 h-4 w-4" />
            Create Schedule
          </Button>
        )}
      </div>

      <ScheduleList onEdit={handleEdit} />

      <ScheduleDialog
        open={scheduleDialogOpen}
        onOpenChange={handleDialogClose}
        scheduleId={selectedSchedule?.id}
      />
    </div>
  );
}
