import { WIZARD_STEPS } from './stepOrder';

const BASE_PATH = '/admin/toolbox-talks/learnings';

// ============================================
// URL helpers
// ============================================

/** Build the URL for a given step + talkId.
 *  Step 1 has no talkId in the URL (pre-talk).
 */
export function getStepUrl(talkId: string | null, step: number): string {
  if (step === 1 || !talkId) {
    return `${BASE_PATH}/new`;
  }
  const def = WIZARD_STEPS.find((s) => s.number === step);
  if (!def) return `${BASE_PATH}/new`;
  return `${BASE_PATH}/${talkId}/${def.slug}`;
}

/** Drafts list URL. */
export function getDraftsUrl(): string {
  return `${BASE_PATH}/drafts`;
}

/** Infer the current step number from a Next.js pathname.
 *  Returns 1 if the path ends in /new, 8 if /drafts, or the matching step slug.
 */
export function getStepFromPathname(pathname: string): number {
  if (pathname.endsWith('/new')) return 1;
  if (pathname.endsWith('/drafts')) return 0; // not a wizard step

  for (const step of WIZARD_STEPS) {
    if (step.number === 1) continue; // 'new' already handled
    if (pathname.endsWith(`/${step.slug}`)) return step.number;
  }
  return 1;
}

/** Extract the talkId segment from a wizard pathname.
 *  e.g. /admin/toolbox-talks/learnings/abc-123/parse → 'abc-123'
 *  Returns null for /new and /drafts paths.
 */
export function getTalkIdFromPathname(pathname: string): string | null {
  const segments = pathname.split('/');
  // pattern: …/learnings/[talkId]/[stepSlug]
  const learningsIdx = segments.findIndex((s) => s === 'learnings');
  if (learningsIdx === -1) return null;
  const candidate = segments[learningsIdx + 1];
  if (!candidate || candidate === 'new' || candidate === 'drafts') return null;
  return candidate;
}
