import type { ValidationOutcome } from './content-creation';

export type { ValidationOutcome };

export type TranslationWorkflowState =
  | 'Initial'
  | 'AIGenerated'
  | 'Translating'
  | 'Validating'
  | 'Validated'
  | 'ReviewerAccepted'
  | 'AwaitingThirdParty'
  | 'ThirdPartyReviewed'
  | 'Accepted'
  | 'Stale';

export type TriggeredByType = 'User' | 'System';

export interface TranslationWorkflowStateDto {
  talkId: string;
  languageCode: string;
  state: TranslationWorkflowState;
  lastEventType: string | null;
  lastEventAt: string | null;
  translatedTitle: string | null;
  translatedAt: string | null;
  needsRevalidation: boolean;
  lastValidationOutcome: ValidationOutcome | null;
  lastValidationRunId: string | null;
  flaggedWordCount: number;
}

export interface WorkflowEventDto {
  eventType: string;
  triggeredByType: TriggeredByType;
  triggeredByUserId: string | null;
  payloadJson: string | null;
  occurredAt: string;
}
