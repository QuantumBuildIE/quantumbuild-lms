'use client';

import { useRouter, useSearchParams } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { ChevronLeft } from 'lucide-react';
import { ScheduleDialog } from '@/features/toolbox-talks/components/ScheduleDialog';

export default function ScheduleCreatePage() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const toolboxTalkId = searchParams.get('talkId') || undefined;

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-4">
        <Button variant="ghost" size="icon" onClick={() => router.back()}>
          <ChevronLeft className="h-4 w-4" />
        </Button>
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Create Schedule</h1>
          <p className="text-muted-foreground">
            Schedule a toolbox talk for employees
          </p>
        </div>
      </div>

      <ScheduleDialog
        open={true}
        onOpenChange={(open) => {
          if (!open) {
            router.push('/toolbox-talks/schedules');
          }
        }}
        toolboxTalkId={toolboxTalkId}
        embedded
      />
    </div>
  );
}
