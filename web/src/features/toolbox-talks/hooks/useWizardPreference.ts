'use client';

import { useSearchParams } from 'next/navigation';
import { useTenantSettings } from '@/lib/api/admin/use-tenant-settings';

export type WizardPreference = 'new' | 'old';

/**
 * Resolves which wizard the Create New button should route to.
 *
 * Resolution order:
 *  1. URL parameter ?wizard=new or ?wizard=old (one-shot, for testing only)
 *  2. TenantSettings 'UseNewWizard' (persisted, toggled in Settings → General)
 *  3. Default: 'old'
 *
 * The URL override is not persisted — it survives refreshes while the param
 * stays in the URL, but disappears on the next navigation. Intended for
 * operator smoke-testing only; not exposed to end users.
 *
 * See BACKLOG §5.27 and CLAUDE.md Note 29 for cutover operator notes.
 */
export function useWizardPreference(): WizardPreference {
  const searchParams = useSearchParams();
  const { data: settings } = useTenantSettings();

  const urlOverride = searchParams.get('wizard');
  if (urlOverride === 'new') return 'new';
  if (urlOverride === 'old') return 'old';

  return settings?.['UseNewWizard'] === 'true' ? 'new' : 'old';
}
