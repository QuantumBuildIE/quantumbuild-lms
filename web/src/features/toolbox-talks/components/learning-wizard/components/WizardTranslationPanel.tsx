'use client';

import { CheckCircle2, Loader2, AlertCircle, Clock } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import type { TranslationWorkflowStateDto, TranslationWorkflowState } from '@/types/workflows';

const LANG_NAMES: Record<string, string> = {
  fr: 'French',
  pl: 'Polish',
  ro: 'Romanian',
  uk: 'Ukrainian',
  pt: 'Portuguese',
  es: 'Spanish',
  lt: 'Lithuanian',
  de: 'German',
  lv: 'Latvian',
};

const STATE_LABELS: Record<TranslationWorkflowState, string> = {
  Initial: 'Not started',
  AIGenerated: 'Ready to translate',
  Translating: 'Translating…',
  Validating: 'Validating…',
  Validated: 'Validated',
  ReviewerAccepted: 'Accepted',
  AwaitingThirdParty: 'Awaiting review',
  ThirdPartyReviewed: 'Third-party reviewed',
  Accepted: 'Accepted',
  Stale: 'Stale — needs retranslation',
};

function stateVariant(
  state: TranslationWorkflowState
): 'default' | 'secondary' | 'destructive' | 'outline' {
  if (state === 'Validated' || state === 'ReviewerAccepted' || state === 'Accepted') return 'default';
  if (state === 'Translating' || state === 'Validating') return 'secondary';
  if (state === 'Stale') return 'destructive';
  return 'outline';
}

function isComplete(state: TranslationWorkflowState): boolean {
  return ['Validated', 'ReviewerAccepted', 'AwaitingThirdParty', 'ThirdPartyReviewed', 'Accepted'].includes(state);
}

function isActive(state: TranslationWorkflowState): boolean {
  return state === 'Translating' || state === 'Validating';
}

function canStart(state: TranslationWorkflowState | undefined): boolean {
  if (!state) return true;
  return state === 'AIGenerated' || state === 'Initial' || state === 'Stale';
}

export interface WizardTranslationPanelProps {
  languageCode: string;
  workflowState: TranslationWorkflowStateDto | null;
  onStart: () => void;
  isStarting: boolean;
}

export function WizardTranslationPanel({
  languageCode,
  workflowState,
  onStart,
  isStarting,
}: WizardTranslationPanelProps) {
  const langName = LANG_NAMES[languageCode] ?? languageCode.toUpperCase();
  const state = workflowState?.state;

  return (
    <div className="flex items-center justify-between gap-4 rounded-lg border p-4">
      <div className="flex items-center gap-3 min-w-0">
        {/* Status icon */}
        {state && isComplete(state) ? (
          <CheckCircle2 className="h-5 w-5 text-green-600 shrink-0" aria-hidden="true" />
        ) : state && isActive(state) ? (
          <Loader2 className="h-5 w-5 text-blue-500 animate-spin shrink-0" aria-hidden="true" />
        ) : state === 'Stale' ? (
          <AlertCircle className="h-5 w-5 text-amber-500 shrink-0" aria-hidden="true" />
        ) : (
          <Clock className="h-5 w-5 text-muted-foreground shrink-0" aria-hidden="true" />
        )}

        <div className="min-w-0">
          <p className="text-sm font-medium truncate">{langName}</p>
          <p className="text-xs text-muted-foreground">{languageCode.toUpperCase()}</p>
        </div>
      </div>

      <div className="flex items-center gap-3 shrink-0">
        {state && (
          <Badge variant={stateVariant(state)}>
            {STATE_LABELS[state] ?? state}
          </Badge>
        )}

        {(!state || canStart(state)) && (
          <Button
            size="sm"
            variant="outline"
            onClick={onStart}
            disabled={isStarting}
            aria-label={`Start translation for ${langName}`}
          >
            {isStarting ? (
              <>
                <Loader2 className="h-3.5 w-3.5 animate-spin mr-1.5" aria-hidden="true" />
                Starting…
              </>
            ) : state === 'Stale' ? (
              'Retranslate'
            ) : (
              'Start'
            )}
          </Button>
        )}
      </div>
    </div>
  );
}
