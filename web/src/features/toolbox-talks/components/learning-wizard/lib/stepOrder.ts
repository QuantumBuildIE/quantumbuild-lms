import type { ToolboxTalk } from '@/types/toolbox-talks';
import type { ValidationRunSummary } from '@/types/content-creation';

// ============================================
// Step definitions
// ============================================

export interface WizardStepDef {
  number: number;
  label: string;
  slug: string;
}

export const WIZARD_STEPS: WizardStepDef[] = [
  { number: 1, label: 'Input & Config',  slug: 'new' },
  { number: 2, label: 'Parse',           slug: 'parse' },
  { number: 3, label: 'Quiz',            slug: 'quiz' },
  { number: 4, label: 'Settings',        slug: 'settings' },
  { number: 5, label: 'Translate',       slug: 'translate' },
  { number: 6, label: 'Validate',        slug: 'validate' },
  { number: 7, label: 'Publish',         slug: 'publish' },
];

export const TOTAL_STEPS = WIZARD_STEPS.length;

export function getStepDef(step: number): WizardStepDef | undefined {
  return WIZARD_STEPS.find((s) => s.number === step);
}

// ============================================
// Reachability rules (scaffold placeholders)
// Real logic for steps 5-7 lands in 5.4/5.5 when workflow state is wired.
// ============================================

function parseTargetCodes(json: string | null): string[] {
  if (!json) return [];
  try {
    const parsed = JSON.parse(json);
    return Array.isArray(parsed) ? parsed : [];
  } catch { return []; }
}

export function isStepReachable(
  step: number,
  talk: ToolboxTalk | null,
  validationRuns?: ValidationRunSummary[] | null
): boolean {
  // Step 1 is the pre-talk step — reachable only for new talks (no talk yet)
  if (step === 1) return true;

  // All post-Step-1 steps require the talk row to exist
  if (!talk) return false;

  switch (step) {
    case 2:
      // Parse: reachable as soon as a talk exists
      return true;
    case 3:
      // Quiz: reachable once talk has sections AND requiresQuiz is true
      // When requiresQuiz is false, the step is skipped (not unreachable — it was intentionally disabled)
      return talk.sections.length > 0 && talk.requiresQuiz;
    case 4:
      // Settings: reachable once talk has sections
      return talk.sections.length > 0;
    case 5:
      // Translate: reachable once sections exist AND target languages are declared
      return talk.sections.length > 0 && parseTargetCodes(talk.targetLanguageCodes ?? null).length > 0;
    case 6:
      // Validate: reachable once sections exist AND target languages are declared
      return talk.sections.length > 0 && parseTargetCodes(talk.targetLanguageCodes ?? null).length > 0;
    case 7: {
      // Sections required
      if (talk.sections.length === 0) return false;
      // Already published — no re-publish
      if (talk.status === 'Published') return false;

      const codes = parseTargetCodes(talk.targetLanguageCodes ?? null);

      // No target languages declared — English-only path, no translation gate
      if (codes.length === 0) return true;

      // Target languages declared — explicit handling of validation runs state
      const runs = validationRuns ?? [];
      if (runs.length === 0) return false; // none exist (not fetched yet, or no runs created)

      const completedRuns = runs.filter((r) => r.status === 'Completed');
      if (completedRuns.length === 0) return false;

      // Strict review gate: every completed run must have no pending non-Pass decisions.
      // Pass sections are auto-accepted by the backend on validation completion.
      return !completedRuns.some((r) => r.hasPendingDecisions);
    }
    default:
      return false;
  }
}

/**
 * Returns true if the step exists but has been intentionally bypassed by a user config choice
 * (e.g. requiresQuiz=false skips step 3). Distinct from "unreachable" — a skipped step is
 * shown with strikethrough in the StepIndicator, not greyed out as "not yet available".
 */
export function isStepSkipped(step: number, talk: ToolboxTalk | null): boolean {
  if (!talk) return false;
  if (step === 3) return talk.sections.length > 0 && !talk.requiresQuiz;
  if (step === 5 || step === 6) {
    return talk.sections.length > 0 && parseTargetCodes(talk.targetLanguageCodes ?? null).length === 0;
  }
  return false;
}

/**
 * Walks forward from currentStep + 1 and returns the first step where isStepReachable is true.
 * Returns null if no forward step is reachable (i.e. already at or past the last step).
 */
export function findNextReachableStep(
  currentStep: number,
  talk: ToolboxTalk | null,
  validationRuns?: ValidationRunSummary[] | null,
): number | null {
  for (let s = currentStep + 1; s <= TOTAL_STEPS; s++) {
    if (isStepReachable(s, talk, validationRuns)) return s;
  }
  return null;
}

export function firstReachableStep(talk: ToolboxTalk | null): number {
  for (const step of WIZARD_STEPS) {
    if (isStepReachable(step.number, talk)) return step.number;
  }
  return 1;
}

export function resumeStep(talk: ToolboxTalk): number {
  const lastEdited = talk.lastEditedStep ?? 2;
  // If stored step is no longer reachable (e.g. after cascade-reset), fall back
  if (isStepReachable(lastEdited, talk)) return lastEdited;
  return firstReachableStep(talk);
}
