# Playwright E2E Recon — Learning Creation Wizard (PDF input mode)

Read-only recon. No code changes made. Goal: establish what exists today so a
new Playwright E2E test covering the new wizard (`/admin/toolbox-talks/learnings/**`)
can be written cleanly, and flag any UX signal gaps along the way.

**Correction to the task's premise:** the wizard is **7 steps, not 6**.
`web/src/features/toolbox-talks/components/learning-wizard/lib/stepOrder.ts:16-24`
defines: 1 Input & Config → 2 Parse → 3 Quiz → 4 Settings → 5 Translate → 6 Validate
→ 7 Publish. CLAUDE.md's "Translate & Validate" as one step is stale — they are
separate URL-per-step pages (`TranslateStep.tsx` / `ValidateStep.tsx`).

---

## Part 1 — Existing Playwright infrastructure

### Config — `web/playwright.config.ts`

- `testDir: "./e2e"`, `fullyParallel: false`, `workers: 1`, `retries: 0`.
- Reporters: `list` + `html` (`open: "never"`; opened manually via `npm run e2e:report`).
- Browser: **Chromium only** (`devices["Desktop Chrome"]`). No Firefox/WebKit/mobile projects.
- Three projects, in dependency order:
  - `setup` — matches `auth.setup.ts` only.
  - `unauthenticated` — root-level `e2e/*.spec.ts` files, no storageState.
  - `authenticated` — `testDir: "./e2e/authenticated"`, `storageState: "e2e/.auth/superuser.json"`, `dependencies: ["setup"]`.
- `webServer`: two entries spawned in parallel —
  1. `npm run dev`, polls `http://localhost:3000`, reused if already running.
  2. `dotnet run --project ../src/QuantumBuild.API --launch-profile http`, polls `http://localhost:5222/health`, injects `Cors__AllowedOrigins__0=http://localhost:3000` (portable fix since `appsettings.Development.json` is gitignored).
- **Workers locked to 1** — per CLAUDE.md Note 30, the Dev DB is shared and parallel runs are unsafe until per-test data isolation exists. A wizard test that creates a talk end-to-end is a heavier, longer-running test than existing ones — this constraint means it will serialize behind (or ahead of) every other E2E test, so keep it as fast as the flow allows.

### Existing tests (4 files total, all read in full)

| File | Covers | Auth | Cleanup | Async pattern |
|---|---|---|---|---|
| `e2e/login-page.spec.ts` | `/login` renders | none | none | auto-retrying `expect(...).toBeVisible()` only |
| `e2e/auth.setup.ts` | Logs in as SuperUser, saves `storageState` | credentials from `SEED_SUPERUSER_EMAIL` env or hardcoded fallback | n/a | `page.waitForURL("**/admin/tenants")` as the login-success signal |
| `e2e/authenticated/login-flow.spec.ts` | Verifies saved session is really SuperUser | inherited storageState | none | reads JWT from `localStorage`, calls `/api/auth/me` directly via `page.request` with manual `Authorization` header |
| `e2e/authenticated/tenant-creation.spec.ts` | SuperUser creates a tenant (3 cases) | inherited storageState | **none — uses `Date.now()`-suffixed unique names**, no delete path exists for tenants | `page.waitForResponse(...)` set up *before* the triggering click, to capture the POST response body (e.g. new tenant ID) without a race |

Key reusable patterns:
- **JWT lives in `localStorage`, not cookies.** Any direct API call from a test (via `page.request`) must manually extract the token (`page.evaluate(() => localStorage.getItem("accessToken"))`) and set `Authorization: Bearer`.
- **`waitForResponse` set up before the click** is the established way to capture a mutation's response body reliably.
- **Toasts are not asserted for success** — `toast.success()` and `router.push()` fire in the same tick and the page may unmount before Playwright observes the toast; `waitForURL` is used instead. Toasts are only asserted for error cases where no navigation happens.
- **shadcn/Radix gotchas** (documented in code comments): checkboxes render as `<button role="checkbox">`, not `<input type="checkbox">` — use `getByRole("checkbox")`. Zod errors surface as `aria-invalid="true"` via `FormControl`'s `Slot` spread.
- **No cleanup pattern exists anywhere** — every test relies on generating unique data (timestamp-suffixed names), not deleting after itself. There is no DELETE endpoint for tenants to clean up with, forcing this approach for tenant-creation; other domains may differ.
- **The project has a recon-doc-before-test convention** — `docs/playwright-tenant-creation-recon.md` already exists and follows this same investigate-then-recommend structure. This doc follows that precedent.

### What does NOT exist (gaps for this test to fill)

- **No shared helpers, fixtures, or page objects** — every test file imports directly from `@playwright/test` with zero abstraction layer. A multi-step wizard test will either inline everything (matching precedent) or be the first to introduce a helper/page-object — worth deciding deliberately rather than drifting into it.
- **No fixture files at all** — no `web/e2e/fixtures/` directory, no sample PDF/video/DOCX/CSV anywhere in the repo's E2E tree. A sample PDF must be created from scratch.
- **No precedent for waiting on long-running background jobs or SignalR events.** The only async-wait pattern in the suite (`tenant-creation.spec.ts`) covers a single synchronous POST. Nothing in the existing suite waits on a Hangfire job, a polling loop, or a SignalR push — this wizard test will be establishing that pattern for the first time in this codebase.

### `package.json` scripts (`web/package.json`)

`e2e`, `e2e:debug`, `e2e:headed`, `e2e:report`, `e2e:ui` — standard Playwright CLI wrappers, nothing custom. `test`/`test:watch` are Vitest (unit tests), unrelated.

### `.gitignore`

`web/.gitignore` excludes `/playwright-report/`, `/test-results/`, `/playwright/.cache/`, `/e2e/.auth/`. No Playwright-specific ignores at the repo root.

---

## Part 2 — Wizard step signals

Base dirs: pages under `web/src/app/(authenticated)/admin/toolbox-talks/learnings/`,
components under `web/src/features/toolbox-talks/components/learning-wizard/steps/`.

**No `data-testid` attributes exist anywhere in the new wizard** (grepped both directories,
zero matches). This is the single biggest signal gap for test-writing: every selector must
be role/label/text/aria-based. The good news is the codebase already uses `role="status"`,
`role="alert"`, `aria-live="polite"`, and `aria-busy` fairly consistently for loading/error
states, so role+accessible-name selectors are viable — just less robust to copy-tweaks than
`data-testid` would be.

| Step | In-progress signal | Complete/advance signal | Failed signal | Job triggered | Wait mechanism |
|---|---|---|---|---|---|
| **1. Input & Config** (`InputConfigStep.tsx`) | Submit button `Loader2` spin + "Creating…"/"Uploading… {n}%" text (`:1044-1059`); file-upload progress bar `role="status" aria-label="Uploading: {n}%"` (`:638-648`) | `router.push` to Parse step on success (`:339`) — no in-place "done" state | Inline field error via `form.setError('title', ...)` + `FormMessage role="alert"` (`:349-351`, `:488`); other failures via `toast.error` | `POST /toolbox-talks/initialise` — synchronous DB write, **not** a Hangfire job | Single request/response, then client-side navigation |
| **2. Parse** (`ParseStep.tsx`) | `role="status" aria-label="Parsing content"` + `Loader2` (`:193-216`) | `SectionList` appears once `sections.length > 0`; "Save & Continue" enabled on `sections.length > 0 && !isSaving` (`:334-349`) | `Alert variant="destructive" role="alert"` + "Retry parsing" button on `parseMutation.isError` (`:220-241`) | `POST /toolbox-talks/{id}/parse` | **PDF mode is synchronous** — confirmed at `ParseStep.tsx:97` ("After a sync parse (Text / PDF), initialize sections from mutation result"). Only **video mode** sets `talk.status = 'Processing'` and polls via `useTalkStatusPolling` (2s `refetchInterval`, `:72-88`). No SignalR on this step regardless of mode. |
| **3. Quiz** (`QuizStep.tsx`) | `role="status" aria-label="Generating quiz"` + `Loader2` (`:261-278`) | `SectionQuestionGroup` appears once `questions.length > 0`; Continue enabled on `!isSaving && hasQuestions` (`:426-441`) | `Alert variant="destructive" role="alert"` + "Retry generation" (`:282-303`) | `POST /toolbox-talks/{id}/quiz/generate` | Same `useTalkStatusPolling` mechanism, gated on `talk?.status === 'Processing'` — **but quiz generation is Claude-driven and typically synchronous in practice for short content**; confirm empirically whether it ever sets `Processing` for PDF-sourced talks or resolves in one round-trip. |
| **4. Settings** (`SettingsStep.tsx`) | No full-step spinner — per-field "Saving…" next to Continue while `isPending` (`:526-531`); save-on-blur per field | Continue `disabled={isSaving}` only — no data-readiness gate | 409 `TitleNotUnique` sets inline field error (`:133-144`); other failures via `toast.error` | Synchronous settings PUT, no job | Plain request/response |
| **5. Translate** (`TranslateStep.tsx` + `WizardTranslationPanel.tsx`) | Per-language row spinner while state is `Translating`/`Validating` (`WizardTranslationPanel.tsx:88-96`); "Start"/"Start All" buttons show "Starting…" during kickoff | `isComplete()` states (`Validated`, `ReviewerAccepted`, etc.) → green `CheckCircle2` + badge (`:52-54`, `:88-89`); no single step-level Continue button — advancement is reachability-gated (see below) | `Stale` state → amber `AlertCircle` + `Badge variant="destructive"` "Stale — needs retranslation" (`:31,39,92-93,106-108`); start-mutation errors → `toast.error` only | `POST /toolbox-talks/{talkId}/translations/{languageCode}/start-translation` → Hangfire `TranslationValidationJob` (multi-round back-translation consensus) | **SignalR push**, not polling — `useWorkflowSubscription` + one `<WorkflowSubscriber runId=.../>` per active run, subscribing to `ValidationProgress`/`SectionCompleted`/`ValidationComplete` and invalidating the workflow-state query on receipt |
| **6. Validate** (`ValidateStep.tsx` + `ValidationSectionCard.tsx`) | Per-section `Badge` "Running" + pulsing `Loader2` while `status` is `Running`/`Pending` and no result yet (`:384-388`); aggregate `Progress` bar + "Running (n)" chip (`ValidationProgressPanel.tsx:169-176`) — **but `percentComplete` is hardcoded to `100` and `isConnected={false}`** on this page (`ValidateStep.tsx:266,274`), i.e. the panel is NOT live-wired to the hub here | Outcome pill (Pass/Review/Fail) once `result` exists per section; "Ready to publish" text once `allLanguagesReady` (`:336-340`) | `Fail` outcome → red pill/border; reviewer-action failures → `toast.error`, with a specific 409 message for concurrent revalidation | Reviewer `Accept`/`Edit`/`Retry` → re-triggers `TranslationValidationJob` scoped to one section | **SignalR push** via same `WorkflowSubscriber`/`useWorkflowSubscription` pattern for run completion, but **post-reviewer-action refresh uses a hardcoded `setTimeout(() => refetchRun(), 1500)`** (`:91`) rather than a poll loop or a hub event — a genuinely timing-dependent implicit signal, not an explicit one |
| **7. Publish** (`PublishStep.tsx` + `page.tsx`) | `Loader2` + "Publishing…" text, button `disabled={isPublishing}` (`page.tsx:57-76`; duplicated inside `PublishStep.tsx:120-125`) | Success → `toast.success` + `router.push` to talk detail page, **leaving the wizard** — no in-wizard "published" screen renders (`PublishSuccessState` component exists but is dead code, never referenced by the page) | `PublishErrorAlert` (`:134-148`) + `toast.error` duplicate at page level | `POST /toolbox-talks/{talkId}/publish` — synchronous state transition | Single request/response |

### `useValidationHub` / `useWorkflowSubscription`

- `useValidationHub` (`web/src/features/toolbox-talks/hooks/use-validation-hub.ts`) connects to
  `/api/hubs/translation-validation`, subscribes to `ValidationProgress`/`SectionCompleted`/`ValidationComplete`
  (confirmed PascalCase), and exposes `isConnected`, `progress`, `completedSections`, `isComplete`, `error`.
- **None of this state is rendered anywhere in the new wizard's DOM.** It's wrapped by `WorkflowSubscriber.tsx`,
  which renders `null` and only exists to invalidate TanStack Query caches on hub events. A Playwright test
  **cannot** read SignalR connection/progress state directly from the page — it can only wait for the
  resulting DOM change (badge text, score number, outcome pill) after the query invalidation triggers a refetch.

### Continue-button gating summary

- Steps 2 & 3 (Parse, Quiz) disable Continue based on actual data presence (`sections.length === 0` / `!hasQuestions`) — a genuine, testable gate.
- Steps 1, 4, 7 (Input, Settings, Publish) disable Continue only on in-flight loading; validation errors block via in-page focus/error, not via disabled state.
- Steps 5 & 6 (Translate, Validate) have **no local Continue button at all** — advancement is entirely mediated by `WizardLayout`'s `canGoNext`/`isStepReachable()` logic in `stepOrder.ts`, driven by the `StepIndicator`'s clickable/disabled state, not a per-step button a test can target directly on the page itself.

---

## Part 3 — Gap analysis

### Gap 1 — Validate step's post-action refresh is a hardcoded 1.5s timeout, not an explicit signal

`ValidateStep.tsx:91` does `setTimeout(() => refetchRun(), 1500)` after any reviewer Accept/Edit/Retry action, rather than waiting for a SignalR confirmation or a query that resolves once the backend write is actually done. **This is also a real UX gap**, not just a test problem — if the backend save (or a subsequent re-validation trigger) takes longer than 1.5s under load, the reviewer sees stale data with no indication a refresh is still pending. A human reviewer clicking "Accept" has no way to know whether the 1.5s timeout already fired and the failure is real, or whether it just hasn't refreshed yet.
**Fix size: small.** Replace the fixed timeout with either (a) awaiting the mutation's response and refetching immediately in the `onSuccess` callback, or (b) a genuine `role="status"` "Saving…" indicator that clears only once the refetch resolves. Either change also gives a Playwright test an explicit, non-arbitrary wait target instead of "sleep past the known timeout."

### Gap 2 — Validate step's `ValidationProgressPanel` is not live-wired despite receiving SignalR-shaped props

`percentComplete={100}` and `isConnected={false}` are hardcoded at `ValidateStep.tsx:266,274` — the component *looks* like it should show live progress (it accepts those props and is built to render a progress bar + connection state) but on this page it's fed dead values. **This is a UX inconsistency, not just a missing test signal**: on the Translate step the same underlying job's progress is meaningfully pushed via `WorkflowSubscriber`, but the Validate step's own progress panel silently no-ops that wiring. A reviewer sitting on the Validate step during an active multi-section validation run gets no live indication of overall progress — only per-section badges as they individually resolve via query invalidation.
**Fix size: medium** (a chunk) — would need threading real hub state into `ValidateStep`, similar to how `TranslateStep` already does it via `WorkflowSubscriber`'s callbacks.

### Gap 3 — No in-wizard "published" success state (dead code exists but is unused)

`PublishSuccessState` component is exported from `PublishStep.tsx:471-481` but never rendered by the actual page — the page immediately navigates away via `router.push` on success. This isn't necessarily a UX bug (leaving the wizard to view the published talk is a reasonable flow), but it is a **trap for future test-writers**: anyone grepping for "success state" component names will find this and assume it renders, then write a test that waits for it and hangs/times out. Flag it as dead code; either wire it up or delete it.
**Fix size: small** if deleting; **small-medium** if the intent is to actually show a brief success screen before navigating.

### Gap 4 — Quiz generation's "Processing" status path is unconfirmed for PDF-sourced content

The polling mechanism (`useTalkStatusPolling`) exists on the Quiz step exactly as it does on Parse, but Part 2's research didn't confirm empirically whether PDF-sourced quiz generation ever actually sets `status = 'Processing'` (making it async) or always resolves synchronously like PDF parsing does. This isn't a UX gap per se, but it is a **test-design unknown**: if quiz generation is sometimes-async for PDF content (e.g., for longer documents where Claude takes several seconds), the test needs a polling-aware wait; if it's always synchronous for PDF, a simple `waitForResponse` suffices. **Recommend confirming this empirically by running the wizard manually once with the target PDF fixture before finalizing the test's wait strategy for this step**, rather than guessing from code alone.

### Gap 5 — No explicit "translation is fully idle/settled" signal on the Translate step

The Translate step's only advancement gate is the page-level `isStepReachable()` check (requires sections + target languages), not a "some/all languages have reached a terminal state" gate visible in the DOM. A test wanting to assert "all languages finished translating" before moving to Validate has to infer this from per-row badges rather than a single aggregate readiness signal. **This is a mild UX gap too** — an admin translating into 4 languages has to visually scan 4 rows rather than see one "3 of 4 complete" summary. **Fix size: small** — a simple aggregate counter/badge above the per-language list.

---

## Part 4 — Fixture and test-data requirements

### Fixture files

- **PDF fixture (required for the first test):** none exists in the repo (`web/e2e/fixtures/` doesn't exist yet). Needs to be created — small, predictable, plain text-heavy PDF that reliably produces **at least 2 distinct sections** when parsed. Max size is 50MB per `InputConfigStep.tsx:246-268` (PDF mode), but the fixture should be tiny (well under 1MB) purely for fast, deterministic test runs — content richness for section-splitting matters more than realism.
- **Other input-mode fixtures (later expansion, not needed for the first test):** DOCX (also supported per `InputConfigStep.tsx:67`, max 50MB), MP4/video (max 500MB — per CLAUDE.md file-management table), plain text mode has no file at all (pasted text).
- Recommend a new `web/e2e/fixtures/` directory as the first instance of this pattern in the repo — no existing convention to follow, so name it clearly (e.g. `web/e2e/fixtures/sample-toolbox-talk.pdf`).

### Test tenant setup

- **Reset-existing vs fresh-per-run:** existing precedent (`tenant-creation.spec.ts`) avoids the question entirely by generating uniquely-named data every run rather than resetting state — there's no tenant DELETE endpoint to reset with. The wizard test should follow the same approach: create a uniquely-titled talk each run (the ToolboxTalk `Code` auto-generation and duplicate-title 400 check at `InputConfigStep.tsx:349-351` mean titles must be unique per tenant anyway).
- **Tenant settings required:**
  - `TenantSettingKeys.UseNewWizard = "true"` (default `"false"` per `TenantSettingKeys.cs:13` — the Learnings list's "Create New" button won't route to the new wizard otherwise). CLAUDE.md Note 29 documents a `?wizard=new` URL override as a one-shot alternative to flipping the tenant setting — **this is likely the simpler test-setup path**: navigate directly to `/admin/toolbox-talks/learnings/new?wizard=new` rather than mutating tenant settings, avoiding any tenant-settings API dependency in test setup at all.
  - Target languages are **not** a standalone tenant setting for this flow — `InputConfigStep.tsx:190-198` auto-derives `targetLanguageCodes` from the tenant's **employee language distribution** (any language with `employeeCount > 0` other than English gets auto-checked), with a manual checkbox override available at `:725`. **This means the test tenant needs at least one employee with a non-English preferred language already seeded**, or the test must manually check a language checkbox in the UI, to meaningfully exercise the Translate/Validate steps at all. Relying on the SuperUser's existing seeded tenant data is risky (may or may not have such an employee) — safest to explicitly select a target language checkbox in the test itself rather than depend on auto-derivation.
- **No published talks/courses need to pre-exist** — correct, this is a creation-only flow.

### Auth credentials

- **Reuse the existing SuperUser storageState** (`e2e/.auth/superuser.json`, produced by `auth.setup.ts`). SuperUser bypasses per-permission checks entirely (per CLAUDE.md's Roles table), so no new test admin user or role setup is needed — this is the simplest path and matches 100% of existing precedent (all 3 current specs use the same SuperUser session).
- If tenant-scoping ever becomes a concern (SuperUser operates across all tenants, which normally requires an `X-Tenant-Id` header on API calls per the bulk-import endpoint convention — but the wizard UI itself presumably resolves tenant context from the logged-in session/URL, not a header), verify this doesn't cause the wizard to target the wrong tenant. Not flagged as a blocking risk, just worth a sanity check on first manual run.

---

## Part 5 — Test shape recommendation

### First test

- **File:** `web/e2e/authenticated/learning-wizard-pdf.spec.ts`
- **Test name:** `"admin can create, translate, validate, and publish a PDF-based learning via the new wizard"`
- **High-level flow:**
  1. Navigate to `/admin/toolbox-talks/learnings/new?wizard=new` (SuperUser session inherited automatically).
  2. **Step 1 (Input & Config):** fill title (timestamp-suffixed for uniqueness), select "Document" input mode, attach the PDF fixture via the file input, manually check at least one target-language checkbox (don't rely on employee-derived auto-selection), submit. Wait via `page.waitForURL` to the Parse step URL (matching the `tenant-creation.spec.ts` precedent of trusting navigation over toast).
  3. **Step 2 (Parse):** wait for the `role="status" aria-label="Parsing content"` element to disappear (or for the `SectionList` card to appear — prefer asserting the positive "sections rendered" state over the negative "spinner gone" state, since PDF parse is synchronous and the window may be too narrow to reliably catch the spinner at all). Assert section count ≥ 2. Click "Save & Continue".
  4. **Step 3 (Quiz):** wait for `SectionQuestionGroup` to appear (confirm empirically first whether this needs a polling wait per Gap 4). Assert question count > 0. Click "Save & Continue".
  5. **Step 4 (Settings):** accept defaults, click Continue (only gated on `isSaving`).
  6. **Step 5 (Translate):** click "Start All" (or per-language "Start"), then poll/wait (`expect(...).toBeVisible({ timeout: N })` on the `CheckCircle2`/"Validated" badge per language, not a fixed sleep) until the selected language(s) reach a complete state. This is a **multi-minute wait in real usage** (multi-round back-translation consensus) — needs a generous Playwright `timeout` override on this assertion, well beyond the default 30s.
  7. **Step 6 (Validate):** wait for section outcome pills to render (Pass/Review/Fail), accept each section (or assert "Ready to publish" text appears once `allLanguagesReady`).
  8. **Step 7 (Publish):** click Publish, `waitForURL` to the talk detail page (`/admin/toolbox-talks/talks/{id}`) as the success signal, per the established toast-avoidance convention.
- **Selectors:** role/label/text-based throughout (`getByRole("status", {name: ...})`, `getByRole("alert")`, `getByRole("button", {name: ...})`, `getByText(...)`) — no `data-testid` exists to use.
- **Async waits it depends on:** `waitForURL` (steps 1, 7), `expect(...).toBeVisible()` auto-retry with extended timeouts (steps 2, 3, 5, 6), no fixed `setTimeout`/`sleep` in the test itself (though the app's own Gap 1 timeout means step 6 might need a `waitForTimeout(1500)` companion if asserting immediately after a reviewer action, until Gap 1 is fixed).

### Pre-work needed before this test can be written cleanly

1. **Create the PDF fixture** (`web/e2e/fixtures/sample-toolbox-talk.pdf`) — required, blocking.
2. **Confirm empirically whether Quiz generation is ever async for PDF content** (Gap 4) — run the wizard manually once with the fixture and watch network/status before deciding the Step 3 wait strategy. Not blocking, but avoids writing a wait strategy that's wrong on day one.
3. **Decide on cleanup-or-unique-data strategy** — recommend following existing precedent (unique-per-run, no cleanup) rather than introducing tenant/talk deletion machinery that doesn't otherwise exist in this suite.
4. **Optional but recommended: fix Gap 1** (replace the Validate step's fixed 1.5s timeout with a real completion signal) before writing Step 6's assertions — otherwise the test either needs its own matching magic-number wait (fragile, breaks silently if the app's timeout value ever changes) or must poll past it defensively.
5. **Not blocking, but worth deciding up front:** whether this is the first test to introduce a shared helper/page-object for a 7-step flow, given how much repetitive step-navigation logic this test will otherwise inline. Given the wizard is likely to grow more E2E coverage (other input modes per the task's stated future expansion), a small `WizardHelper`-style module (even just exported step-navigation functions, not a full page-object framework) would pay for itself on the second test. Recommend introducing one now rather than copy-pasting the full flow into a second spec file later.

### Estimated hours

- **Pre-work:** ~2-3 hours — PDF fixture creation (~30 min), one manual full run of the new wizard to confirm Gap 4 and observe real timing for the Translate/Validate steps (~1 hour, mostly wall-clock waiting on the back-translation consensus job), deciding/documenting the unique-data convention (~15 min). Fixing Gap 1 is a separate, optional chunk (small, ~1-2 hours) not counted here since it's not strictly blocking.
- **First test writing:** ~4-6 hours — the flow itself is long (7 steps) but each step's signals are already well understood from this recon; the main time cost is tuning realistic timeouts for the Translate/Validate steps (multi-minute real job) and iterating on selector reliability in the absence of `data-testid`. If a shared wizard-navigation helper is introduced alongside (recommended, see above), add ~1-2 hours to this estimate; that cost is recovered on the second wizard test.
- **Total to first green run:** roughly **6-9 hours**, dominated by real-time waiting on the translation/validation job during iteration rather than by writing effort.

---

## Skeptical notes carried through this recon

- **PDF parse is confirmed synchronous** (`ParseStep.tsx:97` comment) — this is a genuinely reliable signal, not timing-dependent, and is a good reason to start with PDF over video: video mode requires the polling-based `Processing` status dance and real transcription latency, while PDF's parse step reduces to a single request/response like the Input step.
- **Quiz generation's synchronicity for PDF content is NOT confirmed** — the code supports both paths (sync mutation result vs. polled `Processing` status) but nothing in this recon proves which one PDF content actually takes. Flagged as Gap 4 — verify before finalizing the test.
- **Validate step's "live progress" is fake on that specific page** (hardcoded `percentComplete={100}`, `isConnected={false}`) — a naive reading of the component in isolation would suggest a Playwright test could watch a real progress bar tick up; it can't, on this page. Section-by-section outcome pills are the only genuine signal there.
- **The Translate/Validate steps have no page-level Continue button at all** — a test cannot simply look for "the Continue button became enabled" as a universal per-step pattern the way it can on steps 2-4; advancement here is mediated by the `StepIndicator`'s clickable state, which is a materially different thing to assert against.
- **Video mode was deliberately not chosen as the first target** per the task's own instinct — confirmed correct: video adds real transcription latency via an external service (ElevenLabs, per CLAUDE.md) on top of everything PDF already requires, making it a worse first target for a fast, reliable regression test.
