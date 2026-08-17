# Hooks-Order Violation Audit (React error #310)

Read-only recon. No fixes applied by this audit.

## Summary

A React error #310 (Rules-of-Hooks violation) was found in
`web/src/app/(authenticated)/admin/toolbox-talks/schedules/[id]/page.tsx`: a call to
`usePermission('Learnings.Schedule')` (which internally calls `useContext` + `useMemo`)
was placed **below** two early returns (`if (isLoading) return ...`, `if (error ||
!schedule) return ...`). On the first render the component returns early during the
loading state, so `usePermission`'s internal hooks never run; on a later render, once
data has loaded, the same early returns are skipped and `usePermission` — and its
internal `useContext`/`useMemo` — run for the first time. The hook count/order differs
between renders, which is exactly what triggers error #310. This file has since been
fixed (verified below).

The bug was introduced by commit `ef1f75f` ("feat: Implement new roles & permissions
system"), which rewired the frontend from a hardcoded-role model to a
permission-based model and, in the process, added `usePermission` / `useAuth` /
`useHasAnyPermission` / `useHasAllPermissions` / `usePermissions` calls across roughly
30 frontend files (pages, layouts, and feature components). Because the same
mechanical edit — "add a permission hook near the top of the component" — was applied
repeatedly across many files, it was plausible that the same placement mistake
(inserting the hook after, rather than before, an existing early return) had been
made elsewhere too. This audit exists to find out whether it had.

## Verdict

- **0 additional WILL-THROW instances** found beyond the schedule-detail page, which
  is already fixed.
- **0 LATENT instances** found (hooks below a return that would violate the rule on
  paper but likely never manifest).
- **0 internal violations** found inside any custom hook (`usePermission`, `useAuth`,
  `useHasAnyPermission`, `useHasAllPermissions`, `usePermissions`, `useIsSuperUser`,
  or any other project hook under `web/src/hooks` / `web/src/lib/auth` /
  `web/src/features/toolbox-talks/hooks` / the learning-wizard `hooks/` folder).
- **Blast radius: contained.** The `ef1f75f` placement mistake was made in exactly one
  file. It was not replicated anywhere else in the codebase. No other page or
  component is expected to white-screen from this specific class of bug under normal
  loading/error/permission-state transitions.

This is a genuine negative result, not an incomplete search: ~275 candidate files
were read in full (not grepped-and-skimmed) across eight parallel passes, targeting
every file touched by `ef1f75f`, every call site of the five permission hooks, every
authenticated page and layout, and every component under `features/toolbox-talks` and
`components/`. Every custom hook implementation in the repo was also read for
internal conditional hook calls. See "Coverage" below for the exact file lists. The
value of this audit is the systematic confirmation itself — ruling out a whole class
of latent white-screens rather than waiting to discover them one at a time in
production, the way the schedule page was found.

## Already-fixed reference case (verified)

`web/src/app/(authenticated)/admin/toolbox-talks/schedules/[id]/page.tsx`

- `usePermission('Learnings.Schedule')` is now called at line 68, immediately after
  the other top-of-component hooks (`use(params)`, `useRouter()`, `useState`,
  `useToolboxTalkSchedule`, `useCancelToolboxTalkSchedule`,
  `useProcessToolboxTalkSchedule`) and **before** both early returns:
  - `if (isLoading) return ...` at line 92
  - `if (error || !schedule) return ...` at line 111
- Confirmed clean by direct read during this audit (2026-08-17).

## Coverage

Eight parallel research passes were run, each assigned a distinct, non-overlapping
slice of `web/src`. Every file in every slice was read in full and checked for:

1. A hook call (`useState`, `useEffect`, `useMemo`, `useCallback`, `useContext`,
   `useRef`, `useReducer`, `usePermission`, `useAuth`, `useHasAnyPermission`,
   `useHasAllPermissions`, `usePermissions`, `useIsSuperUser`, `useQuery`,
   `useMutation`, `useForm`, or any other `use[A-Z]…(` call) positioned textually
   **after** an early return in the same component/hook body, or inside a
   conditional block or loop.
2. For custom hooks specifically: any React/TanStack hook called conditionally
   *inside the hook's own implementation*, which would break every caller
   regardless of how the caller's component is structured.

| # | Slice | Scope | Files checked | Violations | Could not verify |
|---|---|---|---|---|---|
| 1 | Admin — Toolbox Talks pages | `app/(authenticated)/admin/toolbox-talks/**` (talks, courses, schedules, learnings wizard routes, reports, settings, pipeline, bulk-sop-import, qr-locations, validation runs — including the 2,535-line pipeline page and the already-fixed schedule detail page) | 37 | 0 | 0 |
| 2 | Admin — Core pages | `app/(authenticated)/admin/**` excluding toolbox-talks (employees, users, tenants, settings/departments/locations/lookups/languages, regulatory, monitoring, lesson-parser, admin layout/home) | 39 | 0 | 0 |
| 3 | Employee-facing + public pages | `app/(authenticated)/toolbox-talks/**`, `profile`, `dashboard`, `help`, the `(authenticated)` layout, plus public `login`, `auth/set-password`, `dpa-acceptance`, and the root `page.tsx` | 27 | 0 | 0 |
| 4 | Toolbox Talks feature components (top level) | `features/toolbox-talks/components/*.tsx` — includes the five files directly touched by `ef1f75f` (`ToolboxTalkDetail`, `ToolboxTalkList`, `ScheduleList`, `CourseList`, `AssignmentsList`) plus `ScheduleDialog`, `VideoPlayer`, `TalkViewer`, `Dashboard`, dialogs, editors, viewers, etc. | 42 | 0 | 0 |
| 5 | Toolbox Talks detail/settings/bulk-sop subcomponents | `features/toolbox-talks/components/detail/**`, `settings/**`, `bulk-sop-import/**` — includes `QuizEditPanel`, `SectionEditPanel`, `SettingsEditPanel`, `AddTargetLanguagePicker`, `external-reviewers-section` (all touched by `ef1f75f`) | 17 | 0 | 0 |
| 6 | Create-wizard step components | `features/toolbox-talks/components/create-wizard/**` (wizard shell + all `steps/`, `parse/`, `quiz/`, `settings/`, `validate/` subfolders) | 26 | 0 | 0 |
| 7 | Learning-wizard components + hooks | `features/toolbox-talks/components/learning-wizard/components/**`, `steps/**`, and every custom hook under `learning-wizard/hooks/**` (checked both as components and for internal conditional hook calls) | 38 | 0 (incl. 0 internal hook violations) | 0 |
| 8 | Shared components, layout, custom hooks | `components/admin/**`, `components/layout/**` (`top-nav.tsx`, `tenant-switcher.tsx`), `components/shared/**`, `components/profile/**`, `features/lesson-parser/**`, `features/help/components/HelpAssistant.tsx`, plus every standalone custom hook (`hooks/use-geolocation.ts`, `hooks/use-lookups.ts`, `features/toolbox-talks/hooks/use-corpus-run-hub.ts`, `use-subtitle-hub.ts`, `use-validation-hub.ts`, `useCoursePreference.ts`, `useWizardPreference.ts`, `lib/auth/get-home-route.ts`, `lib/providers.tsx`) | 49 | 0 (incl. 0 internal hook violations) | 0 |

**Total: ~275 files read in full across 8 non-overlapping slices. 0 violations. 0
files that could not be verified.**

Files flagged in the original task as specifically worth checking (because they were
either touched by `ef1f75f` or looked structurally similar to the schedule-detail
bug) were all individually confirmed clean, including: `top-nav.tsx`,
`assigned-operators-section.tsx`, `tenant-switcher.tsx`,
`pending-training-banner.tsx`, `HelpAssistant.tsx`,
`external-reviewers-section.tsx`, `ToolboxTalkDetail.tsx`, `ToolboxTalkList.tsx`,
`ScheduleList.tsx`, `CourseList.tsx`, `AssignmentsList.tsx`, `QuizEditPanel.tsx`,
`SectionEditPanel.tsx`, `SettingsEditPanel.tsx`, `AddTargetLanguagePicker.tsx`.

## Internal custom-hook check

Every custom hook exposed under `web/src/lib/auth`, `web/src/hooks`,
`web/src/features/toolbox-talks/hooks`, and
`web/src/features/toolbox-talks/components/learning-wizard/hooks` was read for
whether it calls other hooks conditionally inside its own body (which would break
every caller of that hook, independent of how the caller's component is written).

`web/src/lib/auth/use-auth.ts` — the source of the five permission hooks — was read
directly as part of this audit and confirmed clean: `useAuth()` calls `useContext`
unconditionally; `usePermission`, `usePermissions`, `useHasAnyPermission`, and
`useHasAllPermissions` each call `useAuth()` followed by an unconditional `useMemo()`
— no hook call sits behind an `if`/early-return inside the hook itself. The
conditional logic (`if (!user) return false`, `if (user.isSuperUser) return true`)
lives entirely *inside* the `useMemo` callback, which is not a hook-ordering issue.

All other custom hooks checked (`use-geolocation.ts`, `use-lookups.ts`,
`use-corpus-run-hub.ts`, `use-subtitle-hub.ts`, `use-validation-hub.ts`,
`useCoursePreference.ts`, `useWizardPreference.ts`, and the 17 hooks under
`learning-wizard/hooks/`) were confirmed clean — no conditional internal hook calls
found.

## Files that could not be fully verified

None. All 8 research passes reported full verification of every assigned file; no
file was skipped for size or ambiguous control flow.

## Notes on method

- "Hook call after an early return" was checked textually within the top-level
  component/hook function body only. Hooks referenced inside JSX render callbacks
  (e.g. a `render:` function passed to a `DataTable` column definition, or a
  `.map()` callback) are not hook calls on the enclosing component and were not
  counted as violations — those are ordinary function calls, not part of the
  component's own render.
- Early returns embedded in nested callbacks (not the outer component body) were
  likewise excluded, since they don't affect the outer component's hook count.
- Given the clean result, no minimal-fix locations are listed — there is nothing to
  fix beyond the already-corrected schedule-detail page.
