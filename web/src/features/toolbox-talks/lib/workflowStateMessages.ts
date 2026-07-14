import type { TranslationWorkflowState } from '@/types/workflows';

export interface WorkflowStateIneligibilityMessage {
  title: string;
  suggestion: string;
}

/**
 * Per-state guidance for why a language cannot be sent for external review right now, and what
 * would resolve it. Single source of truth — consumed by both SendExternalReviewDialog (single
 * language, wizard-triggered) and SendForReviewDialog (bulk, low-score-triggered) so the two entry
 * points never drift into inconsistent wording for the same state.
 */
const INELIGIBILITY_MESSAGES: Partial<Record<TranslationWorkflowState, WorkflowStateIneligibilityMessage>> = {
  Initial: {
    title: 'Not yet validated',
    suggestion: "This language hasn't been validated yet. Validate it before requesting external review.",
  },
  AIGenerated: {
    title: 'Not yet validated',
    suggestion: "This language hasn't been validated yet. Validate it before requesting external review.",
  },
  Translating: {
    title: 'Translation in progress',
    suggestion: 'Translation is in progress. Wait for it to complete before requesting external review.',
  },
  Validating: {
    title: 'Validation in progress',
    suggestion: 'Validation is in progress. Wait for it to complete before requesting external review.',
  },
  AwaitingThirdParty: {
    title: 'Review already in progress',
    suggestion:
      'An external review is already in progress for this language. Wait for it to complete, or cancel the invitation from the Content Translations panel first.',
  },
  Accepted: {
    title: 'Already finalised',
    suggestion: 'This language is already finalised. No further review is needed.',
  },
  Stale: {
    title: 'Out of date',
    suggestion: 'This language is out of date and needs retranslation before review.',
  },
};

const FALLBACK_MESSAGE: WorkflowStateIneligibilityMessage = {
  title: 'Not ready for review',
  suggestion: 'This language cannot be sent for review from its current state.',
};

/** States from which InitiateExternalReview is accepted — mirrors TranslationWorkflowService.InitiateExternalReview. */
const EXTERNAL_REVIEW_ELIGIBLE_STATES: ReadonlySet<TranslationWorkflowState> = new Set([
  'Validated',
  'ReviewerAccepted',
  'ThirdPartyReviewed',
]);

export function isWorkflowStateEligibleForExternalReview(state: TranslationWorkflowState): boolean {
  return EXTERNAL_REVIEW_ELIGIBLE_STATES.has(state);
}

export function getWorkflowStateIneligibilityMessage(
  state: TranslationWorkflowState
): WorkflowStateIneligibilityMessage {
  return INELIGIBILITY_MESSAGES[state] ?? FALLBACK_MESSAGE;
}
