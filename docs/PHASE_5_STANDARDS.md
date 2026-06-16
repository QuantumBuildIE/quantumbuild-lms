# Phase 5 Standards

> **Purpose:** The rubric every Phase 5 chunk holds itself to. Written before any code so that "professional, robust, adaptable" has a concrete meaning we can hold each chunk against, rather than a feeling we discover we've drifted from at chunk 5.4.
>
> **Status:** Living document. Update in the same PR as any Phase 5 chunk that revises a decision here.
>
> **Read alongside:** `PHASE_5_KICKOFF.md` (framing and recon findings), `TRANSLATION_WORKFLOW_DESIGN.md` §7, §9, §10, §13 (workflow service contract and decisions), `LEARNING_LIFECYCLE.md` (current wizard's failure modes), `BACKLOG.md` (deferred items).

---

## 1. Purpose and Scope

### 1.1 What this document is

Phase 5 rebuilds the talk-creation wizard. The kickoff established the *what* and the *why*. This document is the *how, to a standard high enough that we don't ship Phase 5.7 still embarrassed about Phase 5.3*. It is not a feature spec — the design doc and kickoff cover that. It is the bar for code quality, UX consistency, error handling, accessibility, and state management that each chunk holds itself to.

### 1.2 What "professional, robust, adaptable" means here

Concretely, three things:

**Professional** — the wizard never surprises the user with what just happened, what's happening, or what to do next. Loading states are explicit. Errors are surfaced with what failed and what the user can do about it. Destructive actions are confirmed. Long-running work shows progress. There is no "click and pray" anywhere.

**Robust** — refresh recovery from any step, no data loss on a closed tab, no orphaned backend state, no silent failures, no states the UI can reach but the backend rejects. The defects catalogued in `LEARNING_LIFECYCLE.md` §6 are eliminated by *shape* (the new model can't produce them), not by additional guards retrofitted around the same shape.

**Adaptable** — clear separation between step logic, form schema, API integration, and rendering means a future change (a new step, a different validation pipeline, a different workflow service consumer) lands at one site, not seven. The per-language workflow panel is the model: state machine in the service, UI consumes via thin hooks, no parallel state systems.

### 1.3 What this document is not

- It is not an estimate. Estimates live in the kickoff and design doc §9.
- It is not a feature list. Steps and their behaviour live in the design doc §7.
- It is not a tutorial. It assumes familiarity with the codebase conventions, Zod, react-hook-form, React Query, and the Phase 3-4 workflow service.

---

## 2. Inherited Decisions

These were settled in the kickoff (§"Phase 5 design decisions"). Restated here as commitments because they constrain every section below.

1. **Workflow state lives in the service, not the session.** `ContentCreationSession` is no longer a source of truth for translation state. The canonical `ToolboxTalk` row plus `TranslationWorkflowService.GetState` per language is the source. The session table either goes away or becomes a thin wizard-bookkeeping helper (decided in 5.2).
2. **Wizard state lives in the URL.** Step is part of the URL; the talk ID is in the URL once the talk row exists. Refresh on any step lands on the same step with the same talk.
3. **Talk row is created at Step 1.** No pre-talk session window. Step 1 commits the user to a draft talk in the DB before they continue to Step 2.
4. **Audit metadata moves to the talk row.** Reviewer name, document ref, client name, audit purpose currently live on `ContentCreationSession`. They migrate to `ToolboxTalk` (or a per-talk audit-metadata table) so they survive session deprecation.
5. **Zod + react-hook-form throughout.** No `useState` for form values. Every form step has a Zod schema; every form is `useForm({ resolver: zodResolver(schema) })`.
6. **Each step is independently testable.** No monolithic `CreateWizard.tsx` holding all step state. Each step is its own route, its own component, its own form. Cross-step data flows via the talk row, not via a shared parent.
7. **Loading and error states are consistent.** Every API call has explicit loading UI and explicit error UI. No silent failures.
8. **Accessibility and mobile are in scope.** Desktop-first, but the wizard must be seamless on mobile — not "doesn't crash."
9. **Old wizard stays in place until cutover.** Bounded parallel period. The "Create New" button doesn't move until 5.6.

---

## 3. File and Component Structure

### 3.1 Directory layout

The new wizard lives at `web/src/features/toolbox-talks/components/create-wizard-v2/` (working name — final name settled at 5.2). The old wizard at `create-wizard/` is untouched until 5.6.

Suggested structure (concrete final shape to be confirmed in 5.2):

```
create-wizard-v2/
├── routes/                  # Next.js App Router pages
│   ├── new/                 # Step 1 — pre-talk: input mode + source upload
│   ├── [talkId]/            # All post-Step-1 steps live under the talk ID
│   │   ├── parse/           # Step 2
│   │   ├── quiz/            # Step 3
│   │   ├── settings/        # Step 4
│   │   ├── translate/       # Step 5
│   │   ├── validate/        # Step 6
│   │   └── publish/         # Step 7
│   └── drafts/              # Drafts list (§8)
├── steps/                   # Step components (rendered by routes)
│   ├── InputConfigStep.tsx
│   ├── ParseStep.tsx
│   ├── QuizStep.tsx
│   ├── SettingsStep.tsx
│   ├── TranslateStep.tsx
│   ├── ValidateStep.tsx
│   └── PublishStep.tsx
├── components/              # Shared wizard components
│   ├── StepIndicator.tsx
│   ├── WizardLayout.tsx
│   ├── LoadingState.tsx
│   ├── ErrorState.tsx
│   └── ConfirmationDialog.tsx
├── schemas/                 # Zod schemas per step (§4.1)
│   ├── inputConfigSchema.ts
│   ├── parseSchema.ts
│   └── ...
├── hooks/                   # API + state hooks per step
│   ├── useTalk.ts
│   ├── useDraftsList.ts
│   ├── useStepNavigation.ts
│   └── ...
└── lib/
    ├── stepOrder.ts         # Step sequence + conditional skip rules
    └── urlState.ts          # URL helpers for step + talk ID
```

### 3.2 One responsibility per file

Each step file is a component, not a god-object. It does three things and no more: read the talk + workflow state via hooks, render the form with RHF, and dispatch the step's save mutation. Anything beyond that — Zod schema, API client, complex sub-components — lives in its own file.

A step file longer than ~300 lines is a smell. Cut.

### 3.3 No cross-step shared state in memory

The only places step-to-step data flows are:

- The URL (current step, talk ID)
- The talk row in the DB (everything else)
- React Query cache (consequence of fetching the talk row)

No React Context holding wizard state. No Zustand store. No prop-drilling a "wizard state" object through every step. If a step needs data, it fetches the talk (cheap — React Query dedupes).

This is the single most important shape decision in Phase 5 and it eliminates `LEARNING_LIFECYCLE.md` §6.3 (cascade reset leaves stale fields), §6.4 (orphaned jobs from prior session state), and the entire "session ID lost on refresh" defect class by construction.

---

## 4. Form Handling

### 4.1 Zod + react-hook-form is the only form pattern

Every form step uses Zod + RHF. Conventions:

- Schema lives in `schemas/{step}Schema.ts` and exports both the schema and a `z.infer<>` type
- Component imports both: schema for the resolver, type for handler signatures
- `useForm` is configured with `resolver: zodResolver(schema)` and `mode: "onBlur"` (errors show after first blur, not on every keystroke)
- Default values come from the talk row (after fetch), not hardcoded

```ts
// schemas/settingsSchema.ts
import { z } from "zod";

export const settingsSchema = z.object({
  title: z.string().min(1, "Title is required").max(200),
  description: z.string().max(1000).optional(),
  category: z.string().min(1, "Category is required"),
  // ...
});

export type SettingsFormValues = z.infer<typeof settingsSchema>;
```

### 4.2 Validation rule location

Three layers, each owning a distinct concern:

| Layer | Concern | Example |
|---|---|---|
| Zod schema (frontend) | Shape, presence, format | "title is required, max 200 chars" |
| Frontend pre-submit | Cross-field UX | "at least one target language must be selected before Continue is enabled" |
| Backend command handler | Domain invariants | "title must be unique within tenant", "talk must be in Draft status to edit sections" |

Frontend Zod never duplicates backend domain rules — those are the backend's job and the backend is the source of truth. Frontend validation exists for fast feedback only.

### 4.3 Error display

- Inline under the field, red text, `role="alert"` for screen readers
- The field gets `aria-invalid="true"` and `aria-describedby` pointing at the error
- On submit attempt with errors, focus moves to the first invalid field
- Backend validation errors map onto the same fields where possible; unmappable backend errors render in a form-level error banner above the submit button

### 4.4 Save semantics per step

| Step | Save trigger | Why |
|---|---|---|
| Input Config | On Continue | Step 1 creates the talk; nothing to save until that commit |
| Parse | On Continue + manual edits saved on blur | Editing a section title shouldn't require a page-level submit |
| Quiz | On Continue + manual edits saved on blur | Same as Parse |
| Settings | Auto-save on blur per field | Settings are independent toggles; no semantic Continue commitment |
| Translate | Action-driven (per-language buttons) | Each language is its own workflow operation |
| Validate | Action-driven (per-section accept/edit/retry) | Same model as Phase 3c.4b's review screen |
| Publish | On Publish button | The terminal commitment |

In every case, the save mutation is wrapped per §6 — loading state, error state, retry where appropriate, optimistic update where it's safe.

---

## 5. State Management

### 5.1 URL is the source of truth for navigation state

The URL encodes:

- Current step (path segment: `/parse`, `/quiz`, etc.)
- Talk ID (path segment under `[talkId]/`)

Refreshing on any step reloads the same step with the same talk. Closing the tab and returning via the drafts list takes the user to the step they were last on (stored on the talk row as `LastEditedStep` or derived from talk state — decided in 5.2).

### 5.2 Pre-talk state (Step 1 only)

Step 1 is the one exception to "talk ID in URL." Before the talk row exists, the user is selecting input mode and uploading a source. There is no talk yet to refresh back into.

Two options for handling this (decided in 5.2):

- **Option A:** Step 1 state is in-memory only. A refresh on Step 1 returns the user to an empty Step 1. The user has not yet committed to anything; losing transient form state on refresh is acceptable.
- **Option B:** Step 1 state is persisted to a thin "pre-talk session" record keyed by user ID. A refresh on Step 1 restores the user's in-progress selections.

Option A is simpler. Option B is friendlier if Step 1 ever grows enough that re-entering it is annoying. The recon noted Step 1 today is light (input mode, source file, target languages) — Option A is likely fine.

### 5.3 React Query is the only server-state cache

All server data — the talk row, workflow state, draft list, validation runs — is fetched via React Query. Conventions:

- Query keys are tuples with stable shape: `["talk", talkId]`, `["workflow-state", talkId]`, `["drafts"]`
- Mutations invalidate the affected keys on success (`queryClient.invalidateQueries({ queryKey: ["talk", talkId] })`)
- No manual state mirrors of server data; if a component needs talk data, it calls `useTalk(talkId)`
- Stale-while-revalidate is the default; explicit `staleTime` only where there's a reason

### 5.4 Refresh recovery from any step

The combination of URL state and React Query rehydration means refresh recovery is automatic. The standards bar is: the user can refresh on any step, and within one second of the data fetching, the UI is back where it was. No "session expired" toasts. No blank Step 1.

The single edge case is in-flight work (translation running, validation running). On refresh, the wizard re-subscribes to the workflow service's state and the SignalR hub (§6.4) and the UI re-paints the live state without the user having to do anything.

---

## 6. API Integration

### 6.1 Loading state is mandatory

Every component that fetches data has an explicit loading state. Three shapes are acceptable:

- **Skeleton** — for content rendered into a known layout (talk header, step indicator, sections list). Renders the shape of the eventual content, greyed.
- **Spinner with label** — for full-screen or full-step loads. "Loading talk..." not just a spinner.
- **Inline label** — for button-bound mutations. "Saving..." in the button, button disabled.

No bare spinners with no context. No "nothing on screen for 800ms while the fetch resolves."

### 6.2 Error state is mandatory

Every fetch and every mutation has an explicit error state. Components render an error UI that includes:

- What failed ("Couldn't load this draft", "Couldn't save your changes")
- What the user can do (retry button, link back to drafts list, contact support if it's a 500-class)
- The error message from the backend if it's actionable (validation, conflict), or a friendly generic if it's a server error

Errors are never swallowed silently. A failed mutation that leaves the user looking at a form that didn't update is a bug.

### 6.3 Retry behaviour

| Failure type | Retry behaviour |
|---|---|
| Network failure on fetch | React Query default (3 retries with backoff) |
| Network failure on mutation | No automatic retry; user sees error with Retry button |
| 4xx on mutation | No retry; surface the message |
| 5xx on mutation | No retry; surface "something went wrong, try again" |
| Optimistic mutation rejected | Roll back, surface error, restore previous state |

The reason mutations don't auto-retry is that they're often not idempotent in the way the UI expects — a retried "publish" call against an already-published talk is a different operation than the first call. Surface and let the user decide.

### 6.4 SignalR for long-running operations (closes BACKLOG §1.3.5)

This is non-negotiable for Phase 5. The current wizard polls. The new wizard subscribes.

Translation, validation, transcription, parsing — all jobs that run on Hangfire and take measurable time — emit SignalR events when state changes. The wizard's relevant steps subscribe to those events and invalidate the appropriate React Query keys on receipt. State badges, progress, completion all update live.

Conventions:

- One hub per concern (`validationHub`, `translationHub`, etc.) — the validation hub is already established
- Hub subscription is a hook (`useWorkflowSubscription(talkId)`) that mounts on step entry and unmounts on step leave
- On receipt of a state-change event, the hook invalidates the matching React Query key; the component re-renders from the cache update
- Connection failures fall back to polling with a longer interval (every 30s) and surface a small "live updates unavailable" indicator

### 6.5 Optimistic UX

Optimistic updates are allowed for:

- Field-level form saves (settings auto-save) — the user sees their change reflected immediately; rollback on error
- Toggles and simple counters where rollback is trivial

Optimistic updates are forbidden for:

- Anything that changes workflow state (Start Translation, Accept, Send for external review) — these can fail in ways that are confusing if the UI lies about success
- Anything that affects other entities (publish, cascade-reset)

---

## 7. Workflow Service Consumption

### 7.1 The workflow service is the only source of truth for translation state

There is no `ContentCreationSessionStatus` enum being read by the wizard's translate or validate steps. There is no "draft talk Status field" being read either. There is `TranslationWorkflowService.GetState(talkId, languageCode)` returning a `TranslationWorkflowState` per language, and that is what the UI renders.

This is what the kickoff means by "collapse the parallel state systems." The session-status enum that overlapped with workflow state goes away.

### 7.2 The per-language panel from design §7.1 is the pattern

The Translate step renders `TranslationWorkflowPanel` (the component from Phase 3c.3). One row per language. Each row shows:

- Current workflow state (badge with colour + icon + label)
- Last validation outcome (when present)
- Per-language actions: Translate, Validate, Review, Accept, Send for external review, Cancel, View history

Conditional rendering and enabling are state-driven, not session-status-driven. See `TRANSLATION_WORKFLOW_DESIGN.md` §7.1 for the canonical action availability matrix.

The Validate step is the Review page from Phase 3c.4b — per-section accept/edit/retry. Already shared with the edit surface; the wizard reuses it without forking.

### 7.3 Automatic MarkStale (per design §10 decision 15)

When the wizard's Parse step or Quiz step commits a change that invalidates downstream translations, the backend's command handler calls `MarkStale` automatically on every language whose translation is affected. The UI doesn't ask the user to declare staleness. The Translate step's per-language panel re-renders with the Stale state on next fetch.

The user-visible affordance is the panel itself showing the Stale state with a Translate button. The user re-runs translation when they're ready. No modal, no banner, no "your translations are stale, click here to fix it" — the panel already says that, visually.

### 7.4 Guards before UI wiring (per design §10 decision 16)

Phase 3a completed the state-machine guards on `StartTranslation` and `StartValidation`. Phase 5's Translate and Validate steps consume guarded service methods. Any new mutation Phase 5 adds (e.g., a wizard-specific batch operation) MUST go through a guarded service method, never a direct controller-to-DbContext path that bypasses the workflow service.

If a chunk discovers a needed operation isn't in the service yet, the chunk stops and adds the service method (with its guard) before wiring the UI. This is the §10 decision 16 discipline applied to Phase 5.

### 7.5 Services callable from Hangfire must accept an explicit tenant ID (added 5.4 fix)

`ICurrentUserService.TenantId` reads from `HttpContext`, which is null in Hangfire jobs. Any service method that reads or writes `TenantEntity` rows and may be called from a Hangfire job MUST accept an optional `Guid? explicitTenantId = null` parameter. When provided (non-null), the implementation MUST:

1. **Reject `Guid.Empty`** at the top of the method via the `ValidateExplicitTenantId` guard (returns `WorkflowInvalidState` failure).
2. **Bypass the EF Core tenant query filter** on all reads via `IgnoreQueryFilters()`.
3. **Apply an explicit `WHERE TenantId == tenantId` predicate** on every filtered read — `IgnoreQueryFilters()` without this predicate is a cross-tenant data leak.
4. **Set `TenantId = tenantId` explicitly** on every entity added to the context — do not rely on the auto-stamp interceptor, which reads `ICurrentUserService.TenantId` and stamps `Guid.Empty` in Hangfire context.

HTTP callers pass no argument (default `null`) — zero changes required at existing call sites. Hangfire job callers pass `explicitTenantId: tenantId` where `tenantId` is already available as a job parameter.

**Reference implementation:** `TranslationWorkflowService` — `ResolveTenantId`, `ValidateExplicitTenantId`, `GetState`, `AddEvent`.  
**Pattern also applies to:** any future service added to `ToolboxTalks.Infrastructure/Services/` that touches `TenantEntity` rows and has a job invocation path.

### 7.6 SignalR live updates for workflow state (closes BACKLOG §1.3.5 Phase 3c.3 paragraph)

Phase 3c.3 shipped without SignalR subscription on the per-language panel. Phase 5 closes that for the wizard's Translate step. The step subscribes to a workflow-state hub for the current talk and invalidates the `["workflow-state", talkId]` query key on receipt of any state-change event for any language on that talk.

The edit-page panel can adopt the same hook (`useWorkflowSubscription`) in the same PR or a follow-up — Phase 5 doesn't gate on it, but the hook should be designed for reuse by both surfaces.

---

## 8. Drafts and Resume

### 8.1 What a draft is

A draft is any `ToolboxTalk` row created by the new wizard that has not yet reached `Status = Published`. Because Step 1 commits the talk row (per §2 decision 3), every "Create New" click that progresses past Step 1's submit produces a draft.

### 8.2 Visibility

Drafts are visible to all admins in the tenant. The drafts list shows the creator's name alongside each row so users can identify who started what. Any admin can resume any draft. This matches the "two admins, one is on leave, the other finishes the talk" use case.

### 8.3 Lifetime and auto-expiry

Drafts auto-expire only if they have **never advanced past Step 1**. The reasoning:

- Step-1-only drafts are false starts (clicked Create New, didn't follow through). They accumulate fast under per-tenant visibility — every admin's abandoned curiosity becomes everyone else's clutter.
- Drafts past Step 1 represent real work. Auto-expiring them would be obnoxious; users have the explicit delete affordance (§8.4) as their escape hatch.

Expiry threshold for Step-1-only drafts: 7 days untouched (configurable per tenant; default decided in 5.2 scaffolding).

"Touched" means **last meaningfully saved**, not last viewed. Opening the drafts list does not tick anyone's clock. The talk's `UpdatedAt` advances on real writes only.

### 8.4 Explicit delete

Any admin in the tenant can delete any draft from the list. Delete is:

- Triggered by an explicit Delete action per row in the drafts list
- Confirmed via a modal showing the talk's title and last-edited timestamp
- A hard delete (talk row + all child rows + R2 source file) — once gone, gone

There is no restore. Soft-delete with restore was considered and rejected: it adds list-state complexity (filter toggles, "show deleted") for a recovery case that's covered by the user not clicking Delete in the first place. The confirmation dialog is the safety net.

### 8.5 Resume from any step

Resuming a draft from the list navigates the user to the step they were last on. The talk row stores `LastEditedStep` (or the step is derived from talk state — decided in 5.2). If the step the user was on is no longer reachable (e.g., they were on Translate but a cascade-reset cleared their validation runs since), the wizard redirects them to the nearest reachable step with a small banner explaining why.

### 8.6 Future concern: R2 storage cost

Drafts past Step 1 have a source file in R2. A draft that sits for two years is two years of storage cost for content that may never publish. Not in scope for Phase 5; recorded here as a known future concern. A reasonable shape when the time comes: drafts untouched for N days get their R2 source file purged but the talk row stays, with a banner on resume saying "source file was purged due to inactivity — please re-upload to continue." Captured to BACKLOG at Phase 5 closure.

---

## 9. Accessibility

### 9.1 Baseline

WCAG 2.1 AA. The wizard does not have to clear AAA, but it does have to clear AA without exceptions for "the admin tool doesn't need this." Admins have disabilities too.

### 9.2 Keyboard

Every action reachable by mouse must be reachable by keyboard. Specifics:

- Tab order follows visual order, no exceptions
- Step indicator is a list of links; arrow keys move between, Enter activates (where the step is reachable)
- Drag-to-reorder (BACKLOG §1.3.3) has a keyboard fallback — the selected section can be moved with arrow keys + a modifier, or via an explicit "move up / move down" affordance
- Modals trap focus; Escape closes; focus returns to the trigger on close

### 9.3 Screen reader

- All interactive elements have accessible names (visible text, `aria-label`, or `aria-labelledby`)
- Form fields have associated `<label>` elements
- Validation errors are announced via `role="alert"` or `aria-live="polite"` on the error container
- Step transitions announce the new step via an `aria-live` region ("Now on step 3 of 7: Quiz")
- Workflow state badges have `aria-label` describing the state textually, not just colour + icon

### 9.4 Focus management

- On step navigation, focus moves to the step's main heading (`<h1>`)
- On modal open, focus moves to the first interactive element in the modal
- On form submit with errors, focus moves to the first invalid field
- On long-running operation completion, an `aria-live` region announces the outcome

### 9.5 Colour and contrast

- WCAG AA contrast minimums for all text (4.5:1 for body, 3:1 for large)
- No information conveyed by colour alone — workflow state badges have icon + label + colour, not just colour
- This standard already applies via BACKLOG 1.1.14's resolution for slideshows; Phase 5 extends it to all wizard UI

---

## 10. Mobile

Mobile UX is a product-wide concern, not a Phase 5 closure gate. Mobile-breaking bugs noticed during Phase 5 work are logged as normal BACKLOG entries and prioritised against other work — they do not block Phase 5 sign-off unless the wizard is unusably broken on mobile.

A comprehensive product-wide mobile audit lives in BACKLOG §7.2 (Post-Phase-5 Cleanup).

### 10.1 The bar: seamless, not functional

The wizard is desktop-first because it's an admin tool, but "doesn't crash on mobile" is not the standard. The bar is seamless: an admin who happens to open the wizard on a tablet or phone has a working, usable experience that doesn't feel like a desktop site jammed into a small viewport.

### 10.2 What seamless means concretely

- No horizontal scroll at any width ≥ 320px
- All text legible without zoom (16px minimum body)
- Touch targets meet 44×44px minimum (WCAG 2.5.5)
- No hover-only affordances — every hover-revealed action also has a touch-accessible equivalent
- Tables that don't fit (validation results, section lists with multiple columns) reshape to vertical cards on narrow widths, not horizontal scroll
- Drag-to-reorder works with touch (BACKLOG §1.3.3 resolution must consider this)
- Modals are full-screen on mobile, centred dialogs on desktop
- File upload works with mobile camera/file pickers, not just desktop drag-and-drop

### 10.3 What seamless does not require

- Mobile-specific layouts for things that work fine responsively
- Touch gestures beyond what the OS provides (no custom swipe-to-delete; tap a delete button)
- Offline support
- Native-feeling animations or transitions

### 10.4 Testing

Every step is verified on:

- Desktop (≥1280px)
- Tablet (768–1024px)
- Mobile (375px — iPhone SE width as the narrow target)

Chrome DevTools device emulator is acceptable for routine work; real-device verification happens at chunk close for any step touched.

---

## 11. Testing

### 11.1 Posture

Tests are added where they meaningfully verify behaviour. They are not
added as a per-step checkbox to satisfy this document. A step whose
correctness is obvious from its code and verified by manual walk-through
does not need integration tests written just to claim coverage.

The bar is: when a Phase 5 chunk's code does something non-obvious — a
state transition, a cascade, a permission boundary, a workflow service
call with multiple guarded preconditions — that behaviour gets a test.
When a chunk is mechanical wiring with no real branching, manual
verification and the existing surrounding coverage are enough.

This is a deliberate softening from earlier framing in this document.
The test suite has accumulated drift (BACKLOG §8) and the right time to
address it comprehensively is after Phase 5 ships, as its own focused
task (BACKLOG §14 — added when Phase 5 closes). Phase 5 chunks should
not add tests onto a suite that is itself due a proper review.

### 11.2 What chunks should do

- Test the non-obvious. Workflow service transitions, cascade effects,
  permission boundaries, anything the chunk is the first to introduce.
- Reuse existing test infrastructure (`IntegrationTestBase`,
  `CustomWebApplicationFactory`, the established Phase 1-4 patterns).
- If a chunk discovers existing tests are blocking, misleading, or
  broken, STOP and report — do not fold a test fix into a Phase 5
  feature commit. The post-Phase-5 review will pick it up.
- Manual verification before commit remains the standard: diff against
  code, walk the affected paths, confirm the change behaves as
  described in the report.

### 11.3 What chunks should not do

- Write tests purely to satisfy this document.
- Add Playwright E2E coverage as a Phase 5 deliverable. The existing
  E2E suite has known drift; adding to it now is premature.
- Rewrite, repair, or migrate existing tests beyond what the chunk's
  feature change strictly requires.

### 11.4 Pre-Phase-5 cleanup status

The deprecated test user cleanup (BACKLOG §8) was completed in two
commits before Phase 5.2 started. The remaining drift documented in
BACKLOG §8 is deferred to the post-Phase-5 review.

---

## 12. Defects from the Old Wizard, Explicitly Eliminated

Each row below names a defect the old wizard exhibits and the *shape* of the new wizard that prevents it. The shape change is what matters — guards retrofitted around the same shape are how the old wizard ended up with §6.1 through §6.12 in the first place.

| Defect | Old wizard root cause | New wizard shape that prevents it |
|---|---|---|
| Cold refresh loses session ID (kickoff §"Critical recon findings") | Session ID in `useState`, no URL persistence | Talk ID in URL from Step 1 onward; pre-talk state minimal (§5) |
| Cascade reset leaves stale fields (LL §6.3) | Session has many fields; cascade clears some, not others | No session in the data path; talk + workflow service own state; cascade-reset becomes a workflow event, not a session-field reshuffle |
| Orphaned Hangfire jobs (LL §6.4, resolved in Phase 4 via Delete+guard) | Wizard re-triggered translation without cancelling prior runs | Workflow service owns job lifecycle; wizard calls `StartTranslation` and the service handles cancellation |
| Session stuck in TranslatingValidating (LL §6.5, resolved) | Failed runs left in non-terminal state | Workflow service guarantees state transition on failure; no session-status to get stuck |
| Note 23 ChangeTracker contamination (LL §6.1) | Multi-step transaction in `PublishAsync` accumulating EF changes; caught exception left tracker dirty | No `PublishAsync` equivalent in the new wizard; publish is a single command per the standard backend pattern |
| SetAuditFields silently soft-deletes (LL §6.12) | `.Remove()` on `BaseEntity` descendants throughout the wizard service | Per §7.4, wizard never calls DbContext directly; all writes go through workflow service or command handlers that use `ExecuteDeleteAsync` |
| `UpdateQuestionsAsync` silently demotes (LL §6.10, resolved) | Cascade rules had a gap | New wizard doesn't have `UpdateQuestionsAsync` at all; quiz edits go through the talk-row command handler, which calls `MarkStale` automatically (per §7.3) |
| `ConfirmUploadAsync` resets from any status (LL §6.11, resolved) | No status guard on upload-confirm | Step 1 creates the talk in Draft; later steps can't upload a new source (would be a §2 source-change operation, a separate explicit flow) |
| Section edits silently stale translations (LL §10.9.1, resolved Phase 3) | Edit-page command didn't call `MarkStale` | Backend already fixed in Phase 3b; wizard's Parse step inherits the same command handler |
| Removed+re-added sections break translation ID mappings (LL §10.9.2, resolved Phase 3) | Same root cause as §10.9.1 | Same fix |
| Reviewer-accepted translations silently overwritten (LL §10.9.5, resolved Phase 3) | Generate-translations endpoint unconditionally overwrote | Phase 3b's `StartTranslation` guard rejects from Accepted without `confirmOverwrite=true`; wizard surfaces the confirmation per workflow service contract |
| Synchronous translate HTTP call, no progress (LL §10.9.7) | Phase 3 didn't address this for the edit page | Phase 5's Translate step subscribes to SignalR; long-running operations show progress live |
| Step indicator allows clicks to unreachable steps | `isStepReachable` only blocks 5/6 when `validationRunIds` null | Each step's reachability is derived from workflow state + talk state uniformly (§3.3); the step indicator queries that uniformly |
| No drafts list, no resume | Wizard state in component memory only | URL state + drafts list (§8) |

---

## 13. Out of Scope for Phase 5

These are real problems but not Phase 5's. Each is tracked in BACKLOG; Phase 5 chunks that encounter them should add to the relevant BACKLOG item rather than fold them in.

- **Slideshow operations** (LL §10.9.3, §10.9.4 slideshow half, §10.9.7) — talk-level, not per-language; awaits a slideshow-operations phase
- **Unfiltered unique index on `ToolboxTalkTranslation`** (LL §6.2 Path-B) — structural close requires a migration; not Phase 5's job
- **Hardcoded English assumptions** (BACKLOG §9) — 13 sites in the translation pipeline; orthogonal to wizard rebuild
- **`AudienceRole` Auditor design** (BACKLOG §7) — needs a product decision before any new feature branches on it
- **Integration test suite revamp** (BACKLOG §8) — flagged at §11.5 as a pre-Phase-5 dependency
- **`ValidationStarted → Validating` state mapping gap** (BACKLOG §10) — symmetric to the `TranslationStarted → Translating` gap closed in Phase 3b.1.2; not Phase 5
- **R2 orphan source file cleanup for old drafts** (§8.6) — future, captured at Phase 5 close
- **Demo environment refresh** (BACKLOG §5.7) — orthogonal infrastructure work
- **`MailerSend` resilience** (BACKLOG §5.6) — orthogonal infrastructure work
- **Phase 7 workflow notification triggers** (kickoff §"Open items inherited from Phase 4") — separate phase

---

## 14. Maintenance Discipline

1. **This document is the source of truth for the new wizard's standards.** When a chunk's code disagrees with this document, either the code changes or the document changes — in the same PR.
2. **Every Phase 5 chunk's report includes a checklist against the relevant sections of this document.** Did the new step have explicit loading and error states (§6.1, §6.2)? Did it use Zod + RHF (§4.1)? Did it subscribe to SignalR if it has long-running work (§6.4)? The chunk doesn't ship until the checklist is honestly green.
**Verification reports must include the literal test runner output for the pass/fail/skip line, not a paraphrase.** "All tests passing" without the actual `Passed: X, Failed: Y, Skipped: Z, Total: N` line is not acceptable. The 5.3b/5.3c verification gap (tests reported as passing while 7 were broken from the original commit) is the case study for why.
3. **Persisted reports.** Every Phase 5 chunk's implementation report is committed to `docs/phase-5/reports/<chunk-id>.md` alongside the code. The report includes the literal output of `dotnet test`, the file:line evidence for each PHASE_5_STANDARDS conformance claim, and the list of files changed. Reports must be verifiable against git history and current code — conformance claims without file:line evidence are treated as unverified.
4. **If a chunk discovers a standard here is wrong or impractical, the standard changes — in writing — before the chunk ships.** Silent drift away from the doc is exactly what produced the old wizard's §6 list.
5. **The "out of scope" list (§13) is binding for Phase 5 chunks, not aspirational.** A chunk that finds itself solving §13 work is scope-drifting per the lesson in `LEARNING_LIFECYCLE.md` §8.3. Stop, report, defer.
6. **This document gets reviewed at Phase 5 closure.** Some standards will have proved obvious and uncontroversial; some will have proved load-bearing in ways worth promoting into the design doc. Some will have proved wrong. The post-Phase-5 doc is the input to whatever phase comes next.

---

*Document created at Phase 5 kickoff. Update commit ref on every revision.*