# Post-Close Navigation Recon — New Wizard vs Legacy Wizard

_Date: 2026-06-19_
_Branch: transval_
_Author: Claude Code (read-only investigation — no code changed)_
_Trigger: §25 closed; this recon is post-close and outside the chunk-N-recon naming convention._

---

## Scope and hypothesis under test

Compare every navigation and action surface across both wizards — legacy (`create-wizard`) and new (`learning-wizard`) — on all seven steps, in every state (idle, in-progress, ready, error). Produce a structured gap report.

**Hypothesis:** StepIndicator clicks substitute for Back/Cancel — they navigate back AND abort in-flight operations.

---

## 1. Per-Step Inventory

### Step 1 — Input & Config

| Affordance | Legacy (`create-wizard`) | New wizard (`learning-wizard`) |
|---|---|---|
| Back button | None (step 1) | None (step 1) |
| **Cancel** | **Yes** — inline bottom-left → `onCancel` → `window.confirm()` → `/admin/toolbox-talks/talks` | **Absent** |
| **"View drafts"** | Absent | **Yes** — `footer` slot → `router.push(getDraftsUrl())` → `/admin/toolbox-talks/learnings/drafts` |
| Continue | Yes — inline bottom-right — creates session, uploads file, calls `onNext()` | Yes — inline `type="submit"` — creates talk via API, uploads file |
| StepIndicator | Yes — in-memory `setCurrentStep()` only; no route change; no API call | Yes — `goToStep()` → `isStepReachable()` check → `router.push()`; only step 1 reachable before talk exists |

### Step 2 — Parse (idle / sections ready)

| Affordance | Legacy | New wizard |
|---|---|---|
| **Back button** | **Yes** — always visible → `onBack()` → `setCurrentStep(1)` (in-memory) | **Absent** — `canGoBack = currentStep > 2 = false`; WizardLayout does not render Back |
| Continue / "Save & Continue" | Yes — inline | Yes — inline |
| Re-parse button | Yes — with cascade-reset AlertDialog (warns sections/quiz will clear) | Yes — **no confirmation** (code comment: "No cascade-reset needed at Step 2 — intentionally not carried over") |
| StepIndicator | Yes — in-memory navigate | Yes — step 1 is reachable and clickable |

### Step 2 — Parse (in-progress / spinner)

| Affordance | Legacy | New wizard |
|---|---|---|
| **Back button** | **Yes** — visible during parse | **Absent** — `canGoBack = false` for step 2; `canGoNext={false}` hardcoded; navigation bar renders empty |
| **Cancel and start over** | **Yes** — AlertDialog → confirmed → `stopPolling()` + `abandonSession.mutateAsync(sessionId)` + `onReset()` | **Absent** |
| Continue | Disabled | Absent (`canGoNext={false}`) |
| Escape via StepIndicator | Yes — in-memory navigate to any reachable step | Yes — step 1 clickable → `router.push('/learnings/new')`; **backend parse continues running** |

### Step 3 — Quiz (idle / questions ready)

| Affordance | Legacy | New wizard |
|---|---|---|
| Back button | Yes | Yes — WizardLayout (`canGoBack = step 3 > 2 = true`) |
| Continue / "Save & Continue" | Yes — inline | Yes — inline |
| Regenerate All | Yes — with cascade-reset AlertDialog | Yes — **no confirmation** (code comment: "intentionally not carried over") |
| StepIndicator | Yes | Yes |

### Step 3 — Quiz (generation in-progress)

| Affordance | Legacy | New wizard |
|---|---|---|
| Back button | **Yes** — visible during generation | **Yes** — WizardLayout Back renders (`canGoBack=true`, `onBack={goBack}`) |
| Continue | Disabled | Absent (`canGoNext={false}`) |
| Abort generation | No | No |
| StepIndicator | Yes | Yes |

### Step 4 — Settings

| Affordance | Legacy | New wizard |
|---|---|---|
| Back button | Yes | Yes — WizardLayout |
| Continue | Yes — triggers `startValidation.mutateAsync()` | Yes — inline `type="submit"` saves form, then calls `onContinue` (= `goNext`) |
| `isNavigating` disables Back | N/A | Note: settings/page.tsx does not pass `isNavigating` to WizardLayout; Back not disabled during `updateStep.isPending` |
| StepIndicator | Yes | Yes |

### Step 5 — Translate

| Affordance | Legacy | New wizard |
|---|---|---|
| Back button | Yes | Yes — WizardLayout |
| Continue | Yes — **disabled until `session.status === 'Validated' \|\| 'Failed'`** | Yes — WizardLayout (`canGoNext={canGoNext}`) — **no translation completion gate** (`isStepReachable(6)` only checks sections + target languages exist) |
| StepIndicator | Yes | Yes |

### Step 6 — Validate

| Affordance | Legacy | New wizard |
|---|---|---|
| Back button | Yes | Yes — WizardLayout |
| Continue | Yes — disabled until `allSectionsDecided \|\| session.status === 'Validated'` | Yes — WizardLayout (`canGoNext={canGoNext}`) — gated via `isStepReachable(7)`: strict review gate checks completed runs with no `hasPendingDecisions`. Equivalent gate, different layer. |
| StepIndicator | Yes | Yes |

### Step 7 — Publish

| Affordance | Legacy | New wizard |
|---|---|---|
| Back button | Yes | Yes — WizardLayout |
| Publish | Yes — green button, `handlePublish()` | Yes — WizardLayout `footer` slot, green `bg-green-600` button, Rocket icon, `handlePublish()` |
| **"Preview as Learner"** | **Yes** — optional link button opens preview | **Absent** |
| StepIndicator | Yes | Yes |

---

## 2. Affordances Comparison Table

| # | Affordance | Legacy | New Wizard | Delta |
|---|---|---|---|---|
| 1 | Global Cancel (wizard toolbar level) | Yes — `window.confirm()` → talks list | **Absent** | Gap |
| 2 | Step 1 Cancel (inline) | Yes — `handleCancel()` → talks list | **Absent** | Gap |
| 3 | Step 1 "View drafts" | Absent | Yes — drafts list (not a cancel equivalent — different destination) | New (partial substitute) |
| 4 | Step 1 Continue | Yes — session creation + upload | Yes — talk creation + upload | Equivalent |
| 5 | Step 2 Back (idle) | Yes — always visible | **Absent** — `canGoBack = step 2 > 2 = false` | Design decision |
| 6 | Step 2 Back (in-progress) | Yes — visible during parse | **Absent** — same constraint | Design decision |
| 7 | Step 2 Continue / "Save & Continue" | Yes | Yes | Equivalent |
| 8 | Step 2 Re-parse | Yes — cascade-reset AlertDialog | Yes — no confirmation | Behaviour difference (intentional) |
| 9 | Step 2 "Cancel and start over" (in-progress) | Yes — `abandonSession.mutateAsync()` + `onReset()` | **Absent** | Gap |
| 10 | Step 2 navigation bar during parse in-progress | Back + Cancel-and-start-over visible | **Empty** — no buttons at all | Functional degradation |
| 11 | Step 3 Back (idle) | Yes | Yes — WizardLayout | Equivalent |
| 12 | Step 3 Back (during generation) | Yes — visible | Yes — WizardLayout renders (`canGoBack=true`) | Equivalent |
| 13 | Step 3 Continue / "Save & Continue" | Yes | Yes — inline | Equivalent |
| 14 | Step 3 Regenerate All | Yes — cascade-reset AlertDialog | Yes — no confirmation | Behaviour difference (intentional) |
| 15 | Step 4 Back | Yes | Yes — WizardLayout | Equivalent |
| 16 | Step 4 Continue | Yes | Yes — inline type="submit" | Equivalent |
| 17 | Step 5 Back | Yes | Yes — WizardLayout | Equivalent |
| 18 | Step 5 Continue gate | Disabled until translation complete | **No gate** — `canGoNext` based on step reachability only | Readiness gap |
| 19 | Step 6 Back | Yes | Yes — WizardLayout | Equivalent |
| 20 | Step 6 Continue gate | Disabled until all decisions made | Gated via `isStepReachable(7)` review gate | Equivalent (gate at reachability layer) |
| 21 | Step 7 Back | Yes | Yes — WizardLayout | Equivalent |
| 22 | Step 7 Publish | Yes — green button | Yes — WizardLayout footer, green, Rocket icon | Equivalent |
| 23 | Step 7 "Preview as Learner" | Yes | **Absent** | Gap |
| 24 | StepIndicator backward click | In-memory `setCurrentStep()` — no route change, no API | `router.push()` only — no API call for backward navigation | Behaviour difference (new is better: URL stays clean) |
| 25 | StepIndicator forward click | In-memory `setCurrentStep()` — no route change, no API | `router.push()` + `updateStep.mutateAsync()` — updates `lastEditedStep` | Behaviour difference (new persists progress) |
| 26 | StepIndicator click during parse in-progress | In-memory navigate (steps still clickable) | `router.push()` to step 1 — **backend parse continues** | Behaviour difference (new has no abort) |

26 rows. Under the stated stop threshold of 15 distinct affordances missing or different — all are distinct; count of **Gaps** is 5 (rows 1, 2, 9, 18, 23); count of **functional degradations** is 1 (row 10); count of design decisions is 2 (rows 5–6); count of intentional behaviour differences is 2 (rows 8, 14); count of equivalents is 13.

---

## 3. Behaviour of Each Affordance

**Cancel (legacy, global + step 1):** `CreateWizard.handleCancel()` calls `window.confirm('Are you sure you want to cancel? All progress will be lost.')`. On confirm, `router.push('/admin/toolbox-talks/talks')`. Step 1's `InputConfigStep` receives this as `onCancel` prop and renders an inline `<Button variant="outline">Cancel</Button>` at bottom-left. The wizard itself also holds `handleCancel` at the host level; per the code it is passed to each step that needs it. No server cleanup occurs — any partial `ContentCreationSession` created before cancel is left in the DB.

**"View drafts" (new wizard step 1):** Rendered in the WizardLayout `footer` slot as a plain `<button>` styled as a muted underlined link. Calls `router.push(getDraftsUrl())` which resolves to `/admin/toolbox-talks/learnings/drafts`. This is NOT a cancel: it navigates to the drafts list (where any in-progress talk will appear), not to the talks list. The user's draft is preserved.

**Step 2 Back absent (new wizard):** `useStepNavigation` computes `canGoBack = currentStep > 2`. For step 2, this is `false`. The WizardLayout conditionally renders the Back button only when `canGoBack && onBack` — both conditions must be true. With `canGoBack=false`, the Back button does not render. This is a deliberate architecture decision: the talk is already created at the end of step 1, so "going back to step 1" is structurally ambiguous (would it delete the talk? recreate a new session?). The step indicator's step 1 pill IS clickable as an escape route, but with different semantics (see row 26).

**"Cancel and start over" (legacy parse in-progress):** The most server-aware navigation action in either wizard. When `isParsing` is true, legacy `ParseStep` renders an additional `<Button variant="outline" className="text-destructive">Cancel and start over</Button>`. Clicking opens an `AlertDialog`. On confirm: `stopPolling()` halts the progress-polling interval, `abandonSession.mutateAsync(state.sessionId)` calls `DELETE /api/toolbox-talks/sessions/{id}` to clean up the `ContentCreationSession` entity on the server, then `onReset()` calls back to `CreateWizard.resetWizard()` which restores all in-memory state to initial values and sets `currentStep(1)`. The user ends up back on step 1 with a clean wizard. **New wizard has no equivalent.** The `ContentCreationSession` entity is different from the `ToolboxTalk` entity created in the new wizard — but the effect is the same: the new wizard creates a draft `ToolboxTalk` row that persists if the user navigates away.

**Parse in-progress — new wizard navigation bar:** `parse/page.tsx` renders `WizardLayout` with `canGoBack={canGoBack}` (false for step 2) and `canGoNext={false}` (hardcoded). WizardLayout's navigation bar `<div>` renders at the bottom of the page, but the `{canGoBack && onBack && <Button>Back</Button>}` and `{canGoNext && onNext && <Button>Continue</Button>}` guards mean neither button renders. The navigation bar is a visible but empty `div` with a `border-t`. The `StepIndicator` above it IS rendered and step 1 IS clickable (`isStepReachable(1) = true` always). Clicking step 1 calls `goToStep(1)` → `isStepReachable(1)` = true → step 1 < step 2 so no `updateStep` API call → `router.push('/admin/toolbox-talks/learnings/new')`. The user navigates away. The backend parse job continues running for the orphaned draft talk.

**Re-parse / Regenerate (both wizards — intentional difference):** Legacy `ParseStep` renders a cascade-reset `AlertDialog` warning that regenerating will clear section edits and quiz questions. New wizard `ParseStep` has a Re-parse button in the `CardHeader` with no confirmation dialog. Code comment in `ParseStep.tsx`: "No cascade-reset needed at Step 2 — intentionally not carried over." Same pattern for quiz `QuizStep` "Regenerate All": legacy had AlertDialog, new wizard omits it. These are deliberate product decisions, not oversights.

**Step 5 Translate Continue gate:** Legacy `TranslateStep` disabled the Continue button until `session.status === 'Validated' || session.status === 'Failed'`. New wizard Translate page passes `canGoNext={canGoNext}` from `useStepNavigation`, which computes `findNextReachableStep(5, talk, validationRuns)` → checks `isStepReachable(6, talk)` → returns `true` if `talk.sections.length > 0 && parseLanguageCodes(talk.targetLanguageCodes).length > 0`. Translation job completion is not checked. A user can click Continue immediately on reaching step 5, before any translation has run.

**Step 6 Validate Continue gate:** The new wizard's gate is equivalent to the legacy's but implemented at the reachability layer rather than the button's `disabled` prop. `isStepReachable(7)` in `stepOrder.ts` checks: sections exist, status is not Published, and every completed validation run has `!hasPendingDecisions`. This is the same semantic as "all sections decided." No gap here.

**"Preview as Learner" (legacy step 7):** Legacy `PublishStep` rendered an optional "Preview as Learner" link button in its navigation bar. New wizard `publish/page.tsx` passes only `publishFooter` (the green Publish button) in the `footer` slot. The `PublishStep` component itself contains no navigation buttons. Preview is still accessible via the talk detail page at `/admin/toolbox-talks/talks/{id}` which has its own Preview button, but the wizard offers no direct preview affordance on step 7.

**StepIndicator click — new wizard forward vs backward:** Confirmed in `useStepNavigation.ts`:
- Backward (step < currentStep): `isStepReachable()` check → if passes → **no API call** → `router.push(url)`. URL changes. No save.
- Forward (step > currentStep): `isStepReachable()` check → if passes → **`updateStep.mutateAsync(step)` fires** → `router.push(url)`. Advances `lastEditedStep` on the server.
- During in-flight operation (e.g. parse): same as above — pure route push, no abort.

---

## 4. StepIndicator-as-Navigation Verdict

**Verdict: Partial — navigates, does not abort.**

**Evidence for navigation:**
- StepIndicator buttons are `<button type="button">` elements calling `onStepClick(step.number)`.
- `onStepClick` wires to `goToStep` from `useStepNavigation`.
- `goToStep` calls `router.push(getStepUrl(talkId, step))` for any step that passes `isStepReachable()`.
- URL changes; the step page unmounts and the target step page mounts. This is real navigation.
- For step 1 (always reachable): clicking step 1 from step 2 during parse → page navigates to `/admin/toolbox-talks/learnings/new`.

**Evidence against "abort":**
- `goToStep` contains no `stopPolling()` call.
- `goToStep` contains no `abandonSession.mutateAsync()` call.
- `goToStep` contains no `window.confirm()` or `AlertDialog`.
- After StepIndicator click from step 2 during parse: the backend parse job continues running for the orphaned draft `ToolboxTalk`. Any sections generated while the user is on step 1 are written to the draft talk in the background. The user has no indication this is happening.

**Conclusion:** StepIndicator clicks substitute for Back in the navigational sense — they change the URL and move the user to a different step. They are NOT equivalent to "Cancel and start over" — they perform no cleanup and send no abort signal to the server. The hypothesis is **partially confirmed**: StepIndicator can navigate back, but it cannot abort.

**The practical consequence:** A user who starts a parse on step 2, decides they want to change the source file, and clicks step 1 in the StepIndicator will be on step 1's form while the original parse runs to completion in the background. If they then re-submit step 1 with new content, a second draft talk is created. The original draft talk (with the completed-but-discarded parse) lives in the drafts list indefinitely.

---

## 5. Gap Classification

| # | Gap | Classification | Severity |
|---|---|---|---|
| A | No Cancel button (step 1 inline; no global wizard Cancel either) | **Missing feature** | High — users cannot abort a new talk creation cleanly |
| B | No "Cancel and start over" during parse in-progress | **Missing feature** | High — backend parse job orphaned; draft talk accumulates in DB |
| C | No Back from step 2 | **Design decision** — `canGoBack = currentStep > 2` is intentional; step 1 StepIndicator provides partial escape | — |
| D | Empty navigation bar during parse in-progress (no buttons at all) | **UX gap** — step indicator is the only visible escape; not obvious to users | Medium |
| E | No cascade-reset dialog on re-parse | **Design decision** — code comment: "intentionally not carried over" | — |
| F | No cascade-reset dialog on quiz regeneration | **Design decision** — code comment: "intentionally not carried over" | — |
| G | Translate Continue not gated on translation completion | **Readiness gate gap** — user can advance to Validate before translations exist | Medium |
| H | No "Preview as Learner" on publish step | **Minor feature gap** — preview accessible via talk detail but not inline on step 7 | Low |
| I | "View drafts" ≠ Cancel (different destination; draft preserved) | **Destination mismatch** — "View drafts" is a legitimate affordance but not a cancel substitute | Low |
| J | StepIndicator forward click updates `lastEditedStep` (legacy didn't persist) | **Behavioural improvement** — new wizard is better here | — |
| K | settings/page.tsx omits `isNavigating` prop to WizardLayout | **Minor omission** — Back button not disabled during `updateStep.isPending` on step 4 | Low |

---

## 6. Recommended Next Step

**Implement a "Discard draft" mechanism in the new wizard.**

The two high-severity gaps (A and B) share a root cause: the new wizard creates a persistent `ToolboxTalk` draft row on step 1 completion, but provides no clean path to discard it. When a user walks away mid-parse, the draft accumulates. When they abandon a creation attempt, there is no equivalent of the legacy's `window.confirm() → router.push(talks-list)`.

The minimum viable fix is a single "Discard draft" action — a `footer` slot entry that:
1. Renders from step 2 onward (step 1 has no draft talk yet; "View drafts" is the appropriate step-1 escape).
2. Calls `DELETE /api/toolbox-talks/{talkId}` to soft-delete the draft talk.
3. On success, navigates to the talks list (`/admin/toolbox-talks/talks`), matching the legacy Cancel destination.

This does not require a new API endpoint — the delete endpoint exists. It does not require a `ContentCreationSession` concept — the new wizard's server state is just the `ToolboxTalk` row. It resolves gaps A, B, and D simultaneously: users have an explicit named action (Gap A), it cleans up the server (Gap B), and the step 2 in-progress navigation bar gets a visible button (Gap D).

A confirmation dialog (matching legacy's `window.confirm()`) is appropriate because deletion is irreversible.

Gap G (translate gate) is a separate and lower-urgency fix: add a readiness check in translate/page.tsx — either pass a computed `canGoNext` that also checks `translationsComplete`, or disable the Continue button in `TranslateStep` until at least one translation run exists.

Gap H (preview) can be addressed by adding a "Preview" link to the publish page footer, routing to the existing talk detail preview tab.

Do not address Gaps C, E, F, J as they are intentional design decisions.

---

## 7. Files Read

| File | Purpose |
|---|---|
| `web/src/features/toolbox-talks/components/create-wizard/CreateWizard.tsx` | Legacy wizard host — `handleCancel`, `resetWizard`, `goToStep` (in-memory), step rendering |
| `web/src/features/toolbox-talks/components/create-wizard/steps/InputConfigStep.tsx` | Legacy step 1 — Cancel + Continue bottom bar |
| `web/src/features/toolbox-talks/components/create-wizard/steps/ParseStep.tsx` | Legacy step 2 — Back, Cancel-during-parse AlertDialog, `abandonSession.mutateAsync()` |
| `web/src/features/toolbox-talks/components/create-wizard/steps/QuizStep.tsx` | Legacy step 3 — Back, Regenerate AlertDialog |
| `web/src/features/toolbox-talks/components/create-wizard/steps/SettingsStep.tsx` | Legacy step 4 — Back, Continue |
| `web/src/features/toolbox-talks/components/create-wizard/steps/TranslateStep.tsx` | Legacy step 5 — Back, Continue with translation gate |
| `web/src/features/toolbox-talks/components/create-wizard/steps/ValidateStep.tsx` | Legacy step 6 — Back, Continue with decision gate |
| `web/src/features/toolbox-talks/components/create-wizard/steps/PublishStep.tsx` | Legacy step 7 — Back, Publish, "Preview as Learner" |
| `web/src/features/toolbox-talks/components/learning-wizard/components/WizardLayout.tsx` | New wizard shared layout — Back/Continue conditional rendering |
| `web/src/features/toolbox-talks/components/learning-wizard/components/StepIndicator.tsx` | StepIndicator — button render, `isDisabled` logic, `onStepClick` handler |
| `web/src/features/toolbox-talks/components/learning-wizard/hooks/useStepNavigation.ts` | `canGoBack`, `goBack`, `goNext`, `goToStep` — full navigation logic |
| `web/src/features/toolbox-talks/components/learning-wizard/lib/stepOrder.ts` | `isStepReachable`, `isStepSkipped`, `findNextReachableStep` |
| `web/src/features/toolbox-talks/components/learning-wizard/lib/urlState.ts` | `getStepUrl`, `getDraftsUrl`, `getStepFromPathname` |
| `web/src/features/toolbox-talks/components/learning-wizard/steps/InputConfigStep.tsx` | New step 1 — Continue only (no Cancel); confirmed from session summary |
| `web/src/features/toolbox-talks/components/learning-wizard/steps/ParseStep.tsx` | New step 2 — in-progress early return (spinner as Card children), "Save & Continue" |
| `web/src/features/toolbox-talks/components/learning-wizard/steps/QuizStep.tsx` | New step 3 — in-progress early return, "Save & Continue" |
| `web/src/features/toolbox-talks/components/learning-wizard/steps/SettingsStep.tsx` | New step 4 — inline Continue type="submit" |
| `web/src/features/toolbox-talks/components/learning-wizard/steps/TranslateStep.tsx` | New step 5 — no inline navigation; relies on WizardLayout |
| `web/src/features/toolbox-talks/components/learning-wizard/steps/ValidateStep.tsx` | New step 6 — no inline navigation; relies on WizardLayout |
| `web/src/features/toolbox-talks/components/learning-wizard/steps/PublishStep.tsx` | New step 7 — no inline navigation; no preview button |
| `web/src/app/(authenticated)/admin/toolbox-talks/learnings/new/page.tsx` | Step 1 page — `canGoBack={false}`, `canGoNext={false}`, "View drafts" footer |
| `web/src/app/(authenticated)/admin/toolbox-talks/learnings/[talkId]/parse/page.tsx` | Step 2 page — `canGoBack={canGoBack}` (false), `canGoNext={false}` hardcoded |
| `web/src/app/(authenticated)/admin/toolbox-talks/learnings/[talkId]/quiz/page.tsx` | Step 3 page — `canGoBack={canGoBack}` (true), `canGoNext={false}` hardcoded |
| `web/src/app/(authenticated)/admin/toolbox-talks/learnings/[talkId]/settings/page.tsx` | Step 4 page — `canGoBack`, `onBack`, no `canGoNext`/`isNavigating` |
| `web/src/app/(authenticated)/admin/toolbox-talks/learnings/[talkId]/translate/page.tsx` | Step 5 page — `canGoNext={canGoNext}`, `onNext={goNext}` |
| `web/src/app/(authenticated)/admin/toolbox-talks/learnings/[talkId]/validate/page.tsx` | Step 6 page — `canGoNext={canGoNext}`, `onNext={goNext}` |
| `web/src/app/(authenticated)/admin/toolbox-talks/learnings/[talkId]/publish/page.tsx` | Step 7 page — `canGoNext={false}`, `footer={publishFooter}` (green Publish button) |
| `docs/25/recon.md` | §25 original recon — confirmed navigation not in scope for Chunks 1–6 |
| `docs/25/chunk-6-recon.md` | Chunk 6 recon — confirmed WizardLayout structure and navigation bar position |
| `docs/25/chunk-6-fix.md` | Chunk 6 fix — confirmed §25 closed; this recon is post-close |
| `docs/25/chunk-2-fix.md` | Chunk 2 fix — confirmed cascade-reset dialogs added to ParseStep (re-parse only, not cancel) |
