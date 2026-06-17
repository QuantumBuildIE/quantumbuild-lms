'use client';

import { useState } from 'react';
import { Loader2 } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { LoadingState } from '../components/LoadingState';
import { WizardTranslationPanel } from '../components/WizardTranslationPanel';
import { WorkflowSubscriber } from '../hooks/WorkflowSubscriber';
import { useTalk } from '../hooks/useTalk';
import { useWorkflowSubscription } from '../hooks/useWorkflowSubscription';
import { useStartTalkTranslation } from '@/lib/api/toolbox-talks/use-toolbox-talks';
import { parseLanguageCodes } from '@/features/toolbox-talks/utils/parseLanguageCodes';
import type { TranslationWorkflowState } from '@/types/workflows';

export interface TranslateStepProps {
  talkId: string;
}

function canStart(state: TranslationWorkflowState | undefined): boolean {
  if (!state) return true;
  return state === 'AIGenerated' || state === 'Initial' || state === 'Stale';
}

export function TranslateStep({ talkId }: TranslateStepProps) {
  const { talk, isLoading } = useTalk(talkId);
  const {
    data: workflowStates,
    activeRunIds,
    onValidationComplete,
    onSectionCompleted,
  } = useWorkflowSubscription(talkId);
  const { mutate: startTranslation, isPending, variables } = useStartTalkTranslation();
  const [isStartingAll, setIsStartingAll] = useState(false);

  const languages = parseLanguageCodes(talk?.targetLanguageCodes ?? null);

  if (isLoading) return <LoadingState label="Loading…" />;

  if (languages.length === 0) {
    return (
      <div className="rounded-lg border border-dashed p-8 text-center text-muted-foreground">
        <p className="text-sm font-medium">No target languages configured</p>
        <p className="text-xs mt-1">
          Go back to Input &amp; Config to add target languages.
        </p>
      </div>
    );
  }

  const stateByCode = Object.fromEntries(
    (workflowStates ?? []).map((s) => [s.languageCode, s])
  );

  const handleStart = (languageCode: string) => {
    const current = stateByCode[languageCode]?.state;
    const confirmOverwrite = current === 'Stale';
    startTranslation(
      { talkId, languageCode, confirmOverwrite },
      {
        onError: (err) => {
          toast.error(err.message || 'Failed to start translation');
        },
      }
    );
  };

  const handleStartAll = async () => {
    setIsStartingAll(true);
    try {
      const startable = languages.filter((code) => canStart(stateByCode[code]?.state));
      for (const code of startable) {
        const current = stateByCode[code]?.state;
        const confirmOverwrite = current === 'Stale';
        startTranslation({ talkId, languageCode: code, confirmOverwrite });
        // Stagger initiation to reduce API rate pressure
        await new Promise((resolve) => setTimeout(resolve, 1000));
      }
    } finally {
      setIsStartingAll(false);
    }
  };

  const hasStartable = languages.some((code) => canStart(stateByCode[code]?.state));

  return (
    <>
      {/* One SignalR subscriber per actively translating/validating run — invalidates
          workflow-state query on completion so state badge updates immediately */}
      {activeRunIds.map((runId) => (
        <WorkflowSubscriber
          key={runId}
          runId={runId}
          onComplete={onValidationComplete}
          onSectionCompleted={onSectionCompleted}
        />
      ))}

      <div className="space-y-6">
        <div>
          <h2 className="text-base font-semibold">Translate</h2>
          <p className="text-sm text-muted-foreground mt-1">
            Generate translations for each target language. The system will translate all
            sections, quiz questions, and titles, then back-translate to validate accuracy.
          </p>
        </div>

        <div className="flex justify-end mb-3">
          <Button
            onClick={handleStartAll}
            disabled={isStartingAll || !hasStartable}
          >
            {isStartingAll && (
              <Loader2 className="h-4 w-4 animate-spin mr-2" aria-hidden="true" />
            )}
            Start All
          </Button>
        </div>

        <div className="space-y-3" role="list" aria-label="Target languages">
          {languages.map((code) => (
            <div key={code} role="listitem">
              <WizardTranslationPanel
                languageCode={code}
                workflowState={stateByCode[code] ?? null}
                onStart={() => handleStart(code)}
                isStarting={isPending && variables?.languageCode === code}
              />
            </div>
          ))}
        </div>
      </div>
    </>
  );
}
