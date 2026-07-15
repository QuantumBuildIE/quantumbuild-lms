# Recon: Translations UI stuck at "Translating"/"Running" despite backend completion

**Date:** 2026-07-15
**Type:** Read-only recon — no code changes made
**Trigger:** User reports of status indicators staying on "Translating…" / "Running" / "Validating…" long after the backend has actually finished, self-resolving only on manual page refresh.

---

## 0. Headline finding

There are **three structurally distinct "translating/running" UI surfaces** in this codebase, and only **one** of the three families has been hardened against the stuck-indicator bug class (via commits `d0996c4`, `cc55b71`, `3ca943f`). The other two are still running the exact pre-fix pattern those commits eliminated:

| # | Surface | Where | Live-update mechanism | Status |
|---|---|---|---|---|
| 1 | **`TranslationWorkflowPanel`** | Edit Talk page (`/admin/toolbox-talks/talks/[id]/edit`) | **None** — plain `useQuery`, no `refetchInterval`, no SignalR | ❌ Never fixed |
| 2 | **`create-wizard/steps/ValidateStep.tsx`** (Edit/Retry only) | Legacy (default) creation wizard, step 6 "Validation Results" | One-shot `setTimeout(refetchRun, 1500)`, hardcoded `isConnected={false}` | ❌ Never fixed |
| 3 | **`learning-wizard/steps/TranslateStep.tsx` + `ValidateStep.tsx`** | New wizard (`UseNewWizard=true`) *and* the Talk Detail page's Translations/Validation tabs (used by **all** talks regardless of which wizard created them) | SignalR (`WorkflowSubscriber`/`useValidationHub`) + 5s REST poll fallback + `revalidating` status-poll for Edit/Retry | ✅ Fixed across three commits |

Because `TenantSettings.UseNewWizard` defaults to `"false"` (CLAUDE.md Note 29), the **legacy wizard is the default creation experience for every tenant that hasn't explicitly flipped the toggle**, and the Edit Talk page is a permanent, wizard-agnostic surface used for all ongoing translation management. This means the two unfixed surfaces are very plausibly the majority of what users are actually looking at when they report the bug — the fixes shipped so far may have addressed a minority code path (the new wizard) while the default/legacy paths kept the original bug.

---

## Part 1 — Inventory of "translating"/"running" indicators

| Indicator | Component | State source | Refresh mechanism |
|---|---|---|---|
| Per-language badge "Translating…"/"Validating…" (spinner) | `WizardTranslationPanel.tsx` (rendered by `learning-wizard/steps/TranslateStep.tsx`) | `useWorkflowSubscription(talkId)` → REST `workflow-state` query | SignalR invalidation (`WorkflowSubscriber`) + 5s poll while any language `ACTIVE_STATES` (`Translating`/`Validating`) or pending-start |
| Per-language badge (same states) elsewhere (History modal, general badge) | `WorkflowStateBadge.tsx` | Prop-driven, whoever renders it supplies `state` | Depends entirely on the caller |
| Per-language badge in **Edit Talk page** ("Translate"/"Validate" buttons, state text) | `TranslationWorkflowPanel.tsx` | `useWorkflowStates(toolboxTalkId)` (`web/src/lib/api/toolbox-talks/use-toolbox-talks.ts:229`) | **None** — plain `useQuery`, no `refetchInterval`; only refreshed by the query-cache invalidation each mutation's own `onSuccess` fires (i.e. only the action *you* just took, never a background job's completion) |
| Overall validation progress bar / "Running (N)" section-status pill | `ValidationProgressPanel.tsx` (shared by legacy `create-wizard` and `learning-wizard`) | Props: `percentComplete`, `statusCounts.running`, `isConnected` | Purely presentational — correctness depends entirely on what the parent step passes in (see Parts 2–3) |
| Per-section "Running…" / "Revalidating…" badge on a section card | `ValidationSectionCard.tsx` (shared file, `create-wizard/steps/validate/ValidationSectionCard.tsx`, imported by **both** wizards) | Props: `isRunning`, `isRevalidating` (optional, default `false`) | Purely presentational — see Parts 2–3 for who actually sets these props |
| "Running translation validation…" full-panel splash (legacy wizard step 5 while `session.status === 'TranslatingValidating'`) | `create-wizard/steps/TranslateStep.tsx` | `useCreationSession` REST query | 5s `setInterval(refetchSession)` while status is `TranslatingValidating`, **plus** `useValidationHub` SignalR — this path is well covered |
| "Starting translation validation…" splash | `create-wizard/steps/TranslateValidateStep.tsx` | — | **Dead code** — no importers anywhere in `web/src`; superseded by the split `TranslateStep.tsx`/`ValidateStep.tsx`. Not a live bug source, ignore. |
| Continue button disabled state (Translate/Validate steps, all three step families) | Each step's own `canContinue`/`canStart` logic | Derived from the same session/workflow-state/run data as above | Inherits whatever staleness affects its data source |
| Stale-translation banner on Talk Detail overview | `ToolboxTalkDetail.tsx` (`hasStaleTranslation`) | `useWorkflowSubscription` | Same poll/SignalR wiring as #1 — fine |
| Validation run history table (Date/Score/Outcome columns) | `ValidationHistoryTab.tsx` | `useValidationRuns`/`useCourseValidationRuns` | No live-status column rendered (only completed runs are listed with static Pass/Review/Fail outcome pills) — **not a "stuck" indicator**, it's an append-only history table, no bug here |
| Subtitle processing progress | `SubtitleProcessingPanel.tsx` / `SubtitleProgressPanel.tsx` | Own SignalR hub, separate subsystem | Out of scope — this is video transcription, a different pipeline, not translation validation |

**Classification:**
- **Per-language, per-run indicators** (most of the above) — these are what "stuck at Translating" almost certainly refers to.
- **Overall run status** (`ValidationProgressPanel`) — a presentational component; its correctness is 100% inherited from whichever step wires it up.
- **Talk-level / list-level** — no live "Translating" text found on the Learnings list, Drafts list, or Schedule list (`ScheduleList.tsx` renders `ToolboxTalkScheduleStatus`, an unrelated enum — Draft/Active/Completed/Cancelled, not translation state).

---

## Part 2 — Translate step subscription lifecycle

### Fixed path: `learning-wizard/hooks/useWorkflowSubscription.ts` (post `3ca943f`)

- **Mount**: hook runs whenever `TranslateStep`/`ValidateStep`/`ToolboxTalkDetail` mounts; the REST `workflow-state` query is `enabled: !!talkId`.
- **`refetchInterval`** is a callback re-evaluated every tick: `hasActive || pendingStartRef.current.size > 0 ? 5000 : false`. `hasActive` = any language currently in `Translating`/`Validating`.
- **`markStarted(languageCode, previousState)`** — called synchronously in the `Start`/`Start All` click handlers, **before** the mutation fires. Stores the pre-click state in a ref keyed by language code. This closes the specific race described in `3ca943f`: the instant after clicking Start, a refetch can still observe the pre-click state (backend hasn't flipped it to `Translating` yet); without the baseline, `activeRunIds` would stay empty and `WorkflowSubscriber` would never mount. The `useEffect` at lines 48-59 reconciles this: once fresh data shows the language moved off its recorded baseline, the pending entry is cleared.
- **SignalR companion**: `activeRunIds` (languages currently `Translating`/`Validating` **with** a `lastValidationRunId`) each get a `<WorkflowSubscriber runId=... onComplete={onValidationComplete}>`. On `ValidationComplete`, `onValidationComplete` invalidates the workflow-state query — an immediate push-driven refresh on top of the 5s poll floor.
- **Unmount**: `WorkflowSubscriber`'s `useValidationHub` cleans up its SignalR connection in its own `useEffect` return (stop connection, clear reconnect timer). No custom unmount logic in `useWorkflowSubscription` itself — it's just a `useQuery`, TanStack Query handles GC normally.

### Scenario coverage (per the recon brief's checklist)

| Scenario | Covered? | Why |
|---|---|---|
| Tab loses focus during work, events missed | **Yes** | The 5s REST poll is independent of SignalR and of window focus for *duration of interval firing*; TanStack Query's default `refetchOnWindowFocus: true` also forces an immediate refetch the moment the tab regains focus, so even a fully-missed SignalR event self-heals within one focus event or one 5s tick. |
| Network disconnect/reconnect | **Yes, for the SignalR channel** | `use-validation-hub.ts` has automatic reconnect (10 attempts, up to ~2 min) plus a manual-reconnect fallback on `onclose` (re-checks every 10s) as long as `isCompleteRef.current` is false. Even if SignalR never recovers, the REST poll is a fully independent channel that doesn't depend on the socket at all. |
| Component unmount/remount mid-transition | **Yes** | Remounting re-runs the `useQuery` with the same `queryKey`, and if cached data is stale it refetches; the 5s poll resumes as long as workflow-state still reports an active language. |
| Race between initial fetch and subscription mount | **Yes** | This is exactly what `markStarted`/`pendingStartRef` was built to close (see above). |

**Conclusion for Part 2**: the *learning-wizard* Translate step's subscription lifecycle is solid and appears to genuinely close the gaps the brief asked about. **This is not where the residual bug reports are likely coming from**, assuming users are on the new wizard or using the Talk Detail Translations tab.

### Unfixed path: `create-wizard/steps/TranslateStep.tsx` (legacy wizard, step "5a")

This is the wizard's **own** reference precedent (explicitly cited in the `cc55b71` and `3ca943f` commit messages as "the actual reusable precedent") and it is itself in reasonably good shape:

- 5s `setInterval(() => refetchSession(), 5000)` while `session.status === 'TranslatingValidating'`.
- `useValidationHub(activeEntry?.runId)` — SignalR for the *currently active tab's* language only (single-run, not per-language like the new wizard's array of subscribers).
- On `hub.isComplete`, refetches both `runDetail` and `session`.

One structural difference worth flagging: only the **active tab's** language gets a live hub subscription (`activeEntry?.runId`). If a user starts multiple languages via a hypothetical "start all" (not present in this step — languages are queued server-side per session, not user-triggered individually here) and then switches tabs, the newly-active tab's hub only connects once `activeIndex` changes — there's no gap here in practice because `useCreationSession`'s 5s poll of `session.status` is talk/session-wide, not per-language, so it still catches every language's completion. This step looks fine.

---

## Part 3 — Validate step subscription lifecycle

### Fixed path: `learning-wizard/steps/ValidateStep.tsx` (post `d0996c4` + `cc55b71`)

- `useWorkflowSubscription` supplies `activeRunIds` → `WorkflowSubscriber` per active run, same as Translate step.
- `useValidationHub(activeRunId)` is called directly at the top level (added in `cc55b71`) and feeds `ValidationProgressPanel` with a real `percentComplete`/`isConnected` — replacing what was previously a hardcoded `percentComplete={100}, isConnected={false}`.
- **Edit/Retry-specific fix (`d0996c4`)**: the commit message is explicit about *why* the SignalR/`ACTIVE_STATES` mechanism above doesn't help here — a single-section re-validation job does **not** flip the language's overall workflow state to `Validating` (`TranslationValidationJob.cs:184` only advances state when the whole language is currently `Translating`), so `activeRunIds` stays empty and no `WorkflowSubscriber` ever mounts for that job. The fix adds a **second, independent** polling mechanism scoped to the run's own `Status` field:
  - `revalidating` state: `{ sectionIndex, hasSeenRunning }`, set in `handleSectionAction`'s `onSuccess` for `edit`/`retry` (never for `accept`, which is synchronous).
  - `useValidationRun(..., { refetchInterval: revalidating ? 2000 : false })` — 2s poll while tracking.
  - `hasSeenRunning` guard dodges the race where `runDetail.status` still reads the *previous* job's `Completed` value in the brief window before Hangfire actually picks up the new job.
  - `ValidationSectionCard` receives `isRevalidating={revalidating?.sectionIndex === section.index}` → renders a `role="status" aria-live="polite"` "Revalidating…" badge and disables Accept/Edit/Retry on that card, preventing duplicate-click 409s.

This is a genuinely careful, two-layer fix (workflow-level SignalR + section-level REST poll) and per-code-reading looks complete for its own file.

### Unfixed path: `create-wizard/steps/ValidateStep.tsx` (legacy/default wizard, step "6a")

This file is the pre-fix pattern, essentially unchanged since before `d0996c4`/`cc55b71` were written **against a sibling file that shares the same underlying components**:

```tsx
// create-wizard/steps/ValidateStep.tsx
onSuccess: () => {
  ...
  setTimeout(() => refetchRun(), 1500);   // <-- exactly the pattern d0996c4 replaced
},
```

```tsx
<ValidationProgressPanel
  ...
  percentComplete={100}      // <-- hardcoded, exactly what cc55b71 removed from the sibling file
  isConnected={false}        // <-- hardcoded
/>
```

```tsx
<ValidationSectionCard
  ...
  isRunning={false}          // <-- no `isRevalidating` prop passed at all — defaults to undefined/false
/>
```

Consequences, directly from the shared `ValidationSectionCard.tsx` render logic (`isRevalidating` gates the "Revalidating…" badge and the button-disable-on-busy behavior; `isRunning` gates the "Running…" pill):

1. Click **Edit** or **Retry** on a section here → job is enqueued via Hangfire, `refetchRun()` fires once after a flat 1.5s delay, then **nothing ever polls again**. Multi-round back-translation consensus (Haiku + DeepL, escalating to Gemini/Sonnet) routinely takes well past 1.5s. Whatever `runDetail` looked like at the 1.5s mark is what stays on screen — stale score, stale outcome, no "Revalidating…" indicator, Accept/Edit/Retry buttons remain clickable (risking a 409 "already in progress" on a second click) — until the user navigates away and back, or hits F5.
2. The overall `ValidationProgressPanel` **never shows live progress or a "Live"/"Offline" connection indicator** on this step at all — it's `isConnected={false}` unconditionally, `percentComplete={100}` unconditionally. This is cosmetically "not stuck" (it always shows a full bar) but it's lying, exactly as `cc55b71`'s commit message describes for the sibling file before that fix.
3. This is the **default wizard's Validate step** — `UseNewWizard` defaults to `"false"` — so this is the experience most tenants get when creating new content and reviewing translations during creation, unless they've explicitly opted into the new wizard.

Note the legacy `create-wizard/steps/TranslateStep.tsx` (Part 2) is fine — it's specifically the **split-out Validate step** that lost the hub/poll access the old combined `TranslateValidateStep.tsx` (now dead code) used to have via `hub.reset()` after edit/retry. The split (referenced in the file's own `@deprecated` header) evidently didn't carry the re-validation-tracking behavior over to the new `ValidateStep.tsx`.

---

## Part 4 — Talk-level status indicators & Continue button

### `TranslationWorkflowPanel.tsx` (Edit Talk page — the worst-case surface)

Rendered on `/admin/toolbox-talks/talks/[id]/edit` (`ToolboxTalkForm.tsx:1061`), this is a **third, entirely separate** implementation of per-language translation state, used for translating/validating languages on an **existing published talk** outside either wizard. Its data source:

```ts
// web/src/lib/api/toolbox-talks/use-toolbox-talks.ts:229
export function useWorkflowStates(toolboxTalkId: string) {
  return useQuery({
    queryKey: [...TOOLBOX_TALKS_KEY, toolboxTalkId, 'workflow-state'],
    queryFn: () => getWorkflowStates(toolboxTalkId),
    enabled: !!toolboxTalkId,
  });
}
```

No `refetchInterval`, no SignalR subscription anywhere in `TranslationWorkflowPanel.tsx`, no `WorkflowSubscriber`. The only refresh path is `queryClient.invalidateQueries(...)` fired from each mutation's own `onSuccess` (`useValidateTranslation`, `useGenerateContentTranslations`, etc.) — which only fires for the action the **current user's browser tab** just performed, and fires **immediately on the 202-Accepted response**, not on job completion. Once a background Hangfire job (translate or validate) is dispatched, this panel has **zero mechanism** to ever learn it finished short of a manual refresh, remount, or the user triggering some unrelated mutation that happens to invalidate the same query key.

This matches the reported symptom ("stuck… a page refresh reveals the completed state") more precisely than either wizard path, because there is no fallback at all here — not even a slow one.

Cross-reference: a prior recon (`docs/add-language-to-existing-talk-recon.md`, `docs/generate-translations-data-contract-recon.md`, fixed in `58bb1a5`) already investigated a related-but-distinct bug in this exact component ("adding a language showed stuck at Initial despite completion") and explicitly notes: *"`TranslationWorkflowPanel` is a standalone card component visible elsewhere... via the Edit page."* That fix addressed a **data** bug (missing `TargetLanguageCodes` entry) — it did not touch the **live-update** mechanism, which remains exactly as absent as it always was. The two bugs are easy to conflate but are independent: one is "the language never appears," the other is "the language appears but its badge freezes."

### `WorkflowStateBadge.tsx` (Translating/Validating literal labels)

`stateConfig.Translating.label = 'Translating'`, `stateConfig.Validating.label = 'Validating'` — this is the exact literal string from the bug report. It's a pure presentational component; whichever of the three families above feeds it a stale `state` prop is responsible, not the badge itself.

### Continue button, all three families

| Wizard | Gate | Data source | Risk |
|---|---|---|---|
| New wizard `TranslateStep` | No explicit Continue button in this step (badges only; navigation is via step indicator elsewhere) | `useWorkflowSubscription` | N/A — see Part 2, well covered |
| New wizard `ValidateStep` | "Ready to publish" text, not a disabled button, gated on `allLanguagesReady` (`validationRuns` status `Completed` + no `hasPendingDecisions`) | `useValidationRuns` (no polling) + `useWorkflowSubscription` | Low — `validationRuns` is invalidated by `handleComplete`/section-decision `onSuccess`, and the Edit/Retry `refetchRun` covers the active run; a genuinely stuck badge here is unlikely given Part 3's fix, but `allLanguagesReady` reads `validationRuns`, a **separate, unpolled** query — if a *second* language's run completes while the user is looking at a *different* active tab, `validationRuns` won't refresh until something invalidates it. Minor gap, not the primary complaint. |
| Legacy `create-wizard/TranslateStep` (5a) | `canContinue = session.status === 'Validated' \|\| 'Failed'` | `useCreationSession`, 5s-polled while `TranslatingValidating` | Low — covered, see Part 2 |
| Legacy `create-wizard/ValidateStep` (6a) | `canContinue = allSectionsDecided \|\| session.status === 'Validated'`, derived from `mergedSections` → `runDetail.results[].reviewerDecision` | `useSessionValidationRun` — **no `refetchInterval` parameter exists on this hook at all** (confirmed: `useSessionValidationRun` in `use-content-creation.ts` has no `options` parameter, unlike `useValidationRun` which gained one in `d0996c4`) | **Real risk**: if `reviewerDecision` is only set to a terminal value by the backend once the re-validation job actually completes (not synchronously on Edit submission), the Continue button can appear to "never enable" after an Edit/Retry, for the same reason the section card itself goes stale — there is no way for this hook to ever observe the transition without a manual refetch. This needs a backend read (not done in this recon) to confirm whether `reviewerDecision` flips synchronously or only post-job; either way, the *frontend* has no polling safety net here regardless of the answer. |

---

## Part 5 — Reproduction observability hints

If a user can reproduce this live, capture:

- **Network tab, filtered to `workflow-state`**: on a fixed-path page (new wizard, or Talk Detail → Translations/Validation tabs), confirm a request fires every ~5s while any language shows Translating/Validating. **On the Edit Talk page, this request should fire once on load and never again** — that absence, by itself, would confirm the `TranslationWorkflowPanel` diagnosis without needing anything else.
- **Network tab, filtered to `validation-runs` or the specific `runs/{runId}` endpoint**: on the legacy wizard's Validate step (step 6, "6a Validation Results"), click Edit or Retry on a section and watch for exactly **one** request ~1.5s later, then silence — confirms the `create-wizard/ValidateStep.tsx` diagnosis.
- **WS frames tab (or `chrome://net-export`)**: connection to `/api/hubs/translation-validation` — check whether it's present at all on the page being observed. It will be **absent** on `TranslationWorkflowPanel` (Edit page) and on the legacy `ValidateStep` (step 6a never calls `useValidationHub`) by design, not by bug — that's expected given the current code, not evidence of a *new* problem, but it does confirm those two surfaces have no live channel whatsoever.
- **Console**: `[ValidationHub] Connection closed while run is still active — scheduling manual reconnect` / `Manual reconnect failed` — would indicate the SignalR-specific failure mode on a *fixed* surface (new wizard / Talk Detail tabs); if these appear and the REST poll still doesn't visibly update the UI, that would point to something new (e.g. a rendering bug) rather than any of the three diagnoses above, and would warrant fresh investigation.
- **Which exact URL/page the user is on** is the single highest-value piece of information to gather — `/admin/toolbox-talks/talks/[id]/edit` vs. `/admin/toolbox-talks/talks/[id]` (Translations/Validation tab) vs. `/admin/toolbox-talks/learnings/[talkId]/translate|validate` (new wizard) vs. `/admin/toolbox-talks/create` (legacy wizard, in-progress creation) resolve to three structurally different bugs with three different fixes.

Conditions more likely to reproduce:
- **Edit/Retry actions specifically** (not the initial Translate/Start) — both unfixed surfaces' worst gaps are on the *re-validation* path, not initial translation.
- **Long-running jobs** (>1.5s, i.e. essentially always for a real multi-round consensus run) — trivially reproduces the legacy `ValidateStep` one-shot-timeout gap.
- **Any elapsed time at all** after a background job starts on the Edit Talk page — that surface has no time-based recovery whatsoever, so it doesn't even need to be "long-running."
- Tab-switching/network-hiccup conditions are **not** expected to worsen the fixed surfaces (Part 2/3 conclusion) but would still fully break the two unfixed ones, since they have nothing to recover with regardless.

---

## Part 6 — Recent history

Chronological, most relevant commits (full repo, not filtered to one directory):

| Commit | Date | Scope | Touched |
|---|---|---|---|
| `128c187` | — | `feat(phase-5): Step 5 Translate + Step 6 Validate (5.4) with SignalR` | Original new-wizard Translate/Validate steps built |
| `58bb1a5` | 2026-06-24 | `fix(translations): old-wizard add-language flow stays in sync` | **Data** bug in `TranslationWorkflowPanel`'s add-language flow (missing `TargetLanguageCodes` entry) — unrelated to the live-update mechanism, which this commit did not touch |
| `d0996c4` | 2026-07-13 | `fix(validate): replace hardcoded refresh timeout with real completion signal` | `learning-wizard/steps/ValidateStep.tsx`, `ValidationSectionCard.tsx`, `use-content-creation.ts` (`useValidationRun` gains `refetchInterval`) — **new wizard only** |
| `cc55b71` | 2026-07-13 | `fix(validate): live-wire ValidationProgressPanel via hub subscription` | `learning-wizard/steps/ValidateStep.tsx` only — **new wizard only** |
| `3ca943f` | 2026-07-13 | `fix(wizard): three UX signal-integrity fixes` (includes the multi-language Translate-step slowdown fix) | `learning-wizard/hooks/useWorkflowSubscription.ts`, `learning-wizard/steps/TranslateStep.tsx`, `learning-wizard/lib/stepOrder.ts` — **new wizard only** |
| `aa27513`, `35eac44`, `59739ed` | after `3ca943f` | External-review UX fixes | Also scoped to `learning-wizard`/`SendExternalReviewDialog` — no further change to the polling/SignalR mechanism itself |
| `bc6da84` (HEAD) | — | `fix(slideshow): surface slideshow status on view page and during pending generation` | Unrelated subsystem (slideshow generation, not translation validation) |

**No commit in the visible history touches `TranslationWorkflowPanel.tsx`'s live-update mechanism, `create-wizard/steps/ValidateStep.tsx`, or `use-content-creation.ts`'s `useSessionValidationRun`.** All three stuck-status fixes to date are scoped exclusively to the `learning-wizard` directory. This is fully consistent with (and explains) continued user reports after those fixes shipped: the fixes are real and correct for the surface they touched, but that surface is not the one most tenants hit by default.

---

## Part 7 — Diagnosis, ranked

### Candidate 1 (highest confidence): `TranslationWorkflowPanel.tsx` on the Edit Talk page has no live-update mechanism at all
- **Evidence**: `useWorkflowStates` is a bare `useQuery` with no `refetchInterval`; no `WorkflowSubscriber`/`useValidationHub` anywhere in the file; `useValidationRun` is called there only for a review-dialog score display, also without `refetchInterval`.
- **Why highest confidence**: this is not a subtle race or edge case — it is the total absence of any completion signal. Any background job dispatched from this page (Translate or Validate) will show a frozen badge for its entire runtime, every time, 100% reproducible, no special conditions needed.
- **Why plausibly the dominant source of reports**: the Edit Talk page is wizard-version-agnostic — it's used for translating/validating languages on *any* existing published talk, which is a routine, frequent admin operation independent of the `UseNewWizard` toggle.

### Candidate 2 (high confidence): `create-wizard/steps/ValidateStep.tsx` Edit/Retry actions, on the default (legacy) wizard
- **Evidence**: literal pre-fix code (`setTimeout(refetchRun, 1500)`, hardcoded `isConnected={false}`/`percentComplete={100}`, no `isRevalidating` prop passed) sitting alongside a sibling file (`learning-wizard/steps/ValidateStep.tsx`) that received exactly this fix three commits ago for exactly this reason.
- **Why high confidence**: `UseNewWizard` defaults to `false`, so this is the default in-creation-flow experience. Any tenant that hasn't explicitly opted into the new wizard hits this exact pattern every time they Edit or Retry a section during initial content creation.
- **Caveat**: the *initial* Translate step (5a) on this same legacy wizard is fine — only the split-out Validate step (6a) regressed relative to the pre-split combined step.

### Candidate 3 (lower confidence, worth a quick backend check): Continue button on legacy `ValidateStep` (6a) may appear permanently disabled after Edit/Retry
- **Evidence**: `canContinue` depends on `reviewerDecision` read from an unpolled `useSessionValidationRun` query — same staleness source as Candidate 2, but manifesting as a blocked workflow rather than just a stale badge.
- **Why lower confidence**: depends on backend timing (whether `ReviewerDecision` flips to `Edited`/non-Pending synchronously in the mutation response, vs. only after the re-validation job completes) — not verified in this read-only recon. Worth a quick check of `TranslationValidationController`/`SectionDecisionCommandHandler` before scoping a fix, since if the decision is already synchronous this may be a non-issue in practice even though the frontend has no safety net either way.

### Candidate 4 (ruled unlikely): regression in the already-fixed `learning-wizard` paths
- Tab-visibility, network-hiccup, and mount/unmount races were all explicitly checked against the current code (Part 2/3) and appear closed by the combination of SignalR + independent 5s/2s REST polls + `refetchOnWindowFocus`. If a user's exact reproduction is confirmed to be on the new wizard or the Talk Detail tabs specifically, this recon did not find an obvious remaining gap and a fresh, more targeted investigation (possibly involving a HAR capture) would be warranted rather than assuming this doc's diagnosis applies.

### Candidate 5 (minor, not primary): `allLanguagesReady` on new-wizard `ValidateStep` reads an unpolled `validationRuns` query
- Only matters in a specific multi-language scenario (a background language completes while a different tab is active) and only affects the "Ready to publish" summary text, not a "stuck" badge per se. Low priority.

---

## Part 8 — Chunk breakdown for fixes (recon only — not fix prompts)

Rough sizing for a future implementation pass, in priority order matching Part 7:

1. **`TranslationWorkflowPanel` live-update mechanism** — add the same class of fix as `3ca943f` applied to `useWorkflowSubscription`: give `useWorkflowStates` a `refetchInterval` (active-state-gated, same 5s cadence) and/or mount `WorkflowSubscriber` for any `Translating`/`Validating` language on this page. Likely the single highest-impact, most self-contained chunk — one file (`use-toolbox-talks.ts`) plus wiring in `TranslationWorkflowPanel.tsx`.
2. **`create-wizard/steps/ValidateStep.tsx` parity with its already-fixed sibling** — port the `revalidating`/`hasSeenRunning` polling pattern from `learning-wizard/steps/ValidateStep.tsx` (this is now a proven, working pattern — largely a copy-adapt job since `ValidationSectionCard.tsx` and `useValidationRun`'s `refetchInterval` option are already shared/available). Also replace the hardcoded `isConnected={false}`/`percentComplete={100}` — optionally by wiring `useValidationHub(activeEntry?.runId)` here too, mirroring `cc55b71`, though this step's SignalR value is more limited (per `d0996c4`'s own finding that section-level revalidation doesn't set workflow state to `Validating`).
3. **Backend verification of `ReviewerDecision` timing** — a quick read of the Edit/Retry command handler (not covered in this recon) to determine whether Candidate 3 is real; folds into chunk 2's testing if so.
4. **(Optional/low priority)** `allLanguagesReady` polling gap on new-wizard `ValidateStep` — small, isolated addition of `refetchInterval` or an invalidation hook to `useValidationRuns` while any run is non-terminal.

Not recommending a chunk for Candidate 4 — no evidence of an actual defect there; flag for a targeted follow-up only if a concrete new-wizard reproduction surfaces.
