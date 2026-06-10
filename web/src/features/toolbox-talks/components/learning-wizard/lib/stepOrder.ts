import type { ToolboxTalk } from '@/types/toolbox-talks';

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

export function isStepReachable(step: number, talk: ToolboxTalk | null): boolean {
  // Step 1 is the pre-talk step — reachable only for new talks (no talk yet)
  if (step === 1) return true;

  // All post-Step-1 steps require the talk row to exist
  if (!talk) return false;

  switch (step) {
    case 2:
      // Parse: reachable as soon as a talk exists
      return true;
    case 3:
    case 4:
      // Quiz + Settings: reachable once talk has sections
      return talk.sections.length > 0;
    case 5:
    case 6:
      // Translate + Validate: placeholder — 5.4 owns the real rule
      return false;
    case 7:
      // Publish: placeholder — 5.5 owns the real rule
      return false;
    default:
      return false;
  }
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
