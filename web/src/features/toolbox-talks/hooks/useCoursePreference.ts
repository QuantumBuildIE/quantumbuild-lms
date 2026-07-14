'use client';

import { useSearchParams } from 'next/navigation';
import { useTenantSettings } from '@/lib/api/admin/use-tenant-settings';

/**
 * Resolves whether course creation should use the new compose-existing
 * form (CourseForm) or redirect to the legacy split-and-author wizard.
 *
 * Resolution order:
 *  1. URL parameter ?coursemode=new or ?coursemode=old (one-shot, for testing only)
 *  2. TenantSettings 'UseNewCourseCreation' (persisted, defaults to "true" at cutover)
 *  3. Default: true
 *
 * Mirrors useWizardPreference's resolution order and URL-override behaviour.
 * Polarity is deliberately flipped from useWizardPreference: UseNewCourseCreation
 * defaults to "true" (courses ship new-primary), whereas UseNewWizard defaults to
 * "false" (legacy-first, opt-in) — so unset/loading resolves to true here, not false.
 *
 * The URL override is not persisted — it survives refreshes while the param
 * stays in the URL, but disappears on the next navigation. Intended for
 * operator smoke-testing only; not exposed to end users.
 */
export function useCoursePreference(): boolean {
  const searchParams = useSearchParams();
  const { data: settings } = useTenantSettings();

  const urlOverride = searchParams.get('coursemode');
  if (urlOverride === 'new') return true;
  if (urlOverride === 'old') return false;

  return settings?.['UseNewCourseCreation'] !== 'false';
}
