'use client';

import { useEffect, useRef } from 'react';

interface UseAutoStartStepOptions {
  /** Scopes the "already attempted" guard to one talk + step, e.g. `learning-wizard:autostart:parse:${talkId}`. */
  storageKey: string;
  /** True once preconditions are met and no result exists yet — auto-start should fire. */
  shouldStart: boolean;
  /** Fires the generation mutation. Called at most once per mount. */
  onStart: () => void;
}

/**
 * Fires `onStart` once on mount when `shouldStart` is true.
 *
 * Two guards, for two different failure modes:
 * - A ref (not state) blocks React strict-mode's double effect invocation in dev —
 *   state updates lag a render behind, a ref doesn't.
 * - sessionStorage blocks re-firing on remount (step navigation, back button, page
 *   refresh) after a previous attempt this browser session. This matters because a
 *   failed generation reverts the talk to Draft with no result — the same server
 *   state as "never attempted" — so without this guard a failure would auto-retry
 *   forever on every remount instead of surfacing the error for a manual retry.
 */
export function useAutoStartStep({ storageKey, shouldStart, onStart }: UseAutoStartStepOptions) {
  const firedRef = useRef(false);

  useEffect(() => {
    if (firedRef.current || !shouldStart) return;
    if (typeof window !== 'undefined' && sessionStorage.getItem(storageKey)) return;

    firedRef.current = true;
    if (typeof window !== 'undefined') sessionStorage.setItem(storageKey, '1');
    onStart();
  }, [shouldStart, storageKey, onStart]);
}
