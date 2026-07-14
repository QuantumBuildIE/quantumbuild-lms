# Playwright Wizard Manual Run — Observations

Manual (Playwright-driven, since there's no human at a browser) confirmation run
through the new Learning Creation Wizard (`/admin/toolbox-talks/learnings/**`).
Goal: resolve the recon doc's open gaps empirically before the real E2E test is
written. This is a report of what was observed, not a permanent test — the
throwaway spec used to drive it (`web/e2e/authenticated/manual-run-temp.spec.ts`)
was deleted at the end of this session.

Tenant used: **RASCOR** (seeded dev tenant). Talk source: RAMS (Risk Assessment
and Method Statement) content, extracted verbatim from
`web/e2e/fixtures/sample-toolbox-talk.pdf` (see "Fixture adequacy" below for why
Text input mode was substituted for Pdf mode).

---

## 0. Pre-requisite discovered — not in the recon doc

Before the wizard is reachable at all, a SuperUser session must have an **active
tenant selected** via the `TenantSwitcher` in the top nav (`combobox` showing "All
Tenants" / a tenant name). `AdminLayout` (`web/src/app/(authenticated)/admin/layout.tsx:52-59`)
redirects any tenant-scoped page — including every wizard URL — back to
`/admin/tenants` if `isSuperUser && !activeTenantId`. The recon doc's Part 4 does
not mention this; the eventual E2E test must select a tenant (stored in
`localStorage.activeTenantId`) before navigating to the wizard.

Also discovered: **RASCOR itself has zero sectors configured** ("No sectors
configured for this tenant" alert on Step 1). Sector is optional in
`inputConfigSchema.ts`, so this didn't block anything, but it's a surprising gap
for what is presumably the primary dev/demo tenant.

---

## 1. Per-step observations

### Step 1 — Input & Config

- **In-progress signal:** submit button shows `Loader2` + "Creating…" while
  `initialiseMutation` is pending; no separate spinner for a Text-mode submission
  (no file upload involved).
- **Duration:** 357ms – 2211ms across 4 runs (`POST /toolbox-talks/initialise`,
  201 Created every time). Fast and consistent.
- **Completion signal:** clear — `router.push` to the Parse step URL fires
  immediately after the 201 response; `page.waitForURL` is a reliable, non-fuzzy
  signal.
- **Pre-checked target language, confirmed:** the `MultiSelectCombobox` trigger
  showed **"Portuguese"** already selected on 3 of 4 runs (one run mis-timed the
  read and caught the "Loading languages…" placeholder instead — fixed after).
  This confirms `InputConfigStep.tsx:190-200`'s employee-language auto-derivation
  is live and working for RASCOR (it has at least one Portuguese-speaking
  employee seeded). A second language ("Afrikaans", the first non-selected option
  in the list) was manually added on top, per the task's instruction to always
  touch the field regardless of auto-state.
- **Surprise (accessibility gap, not in recon):** the `MultiSelectCombobox`
  trigger button has **no accessible name / label association** —
  `getByLabel(/target languages/i)` fails to find it, even though a `FormLabel`
  with matching text sits right next to it in the DOM. The label's `htmlFor`
  does not reach the nested `Popover`/`Button`. It is the first `role="combobox"`
  element within the `<form>` (Pass threshold / Audience / Sector / Client /
  Audit purpose Selects all have real accessible names of their own), so a
  positional selector is the only reliable workaround today.
- **Error states hit:** none (no duplicate-title collisions; RAMS-sourced titles
  were timestamp-suffixed).

### Step 2 — Parse

- **In-progress signal:** `role="status" aria-label="Parsing content"` +
  `Loader2`, exactly as documented; window to observe it is short since parsing
  is genuinely synchronous.
- **Duration:** 12.3s – 20.6s across 4 runs (`POST /toolbox-talks/{id}/parse`,
  200 every time — single request/response, confirmed synchronous exactly as
  `ParseStep.tsx:97`'s code comment claims, for **Text mode** — no PDF-specific
  divergence expected per the code, but see the fixture-adequacy caveat below).
- **Section counts:** 6, 7, 5, 5 across the 4 runs from the *identical* source
  text — genuine AI non-determinism in section-splitting, all comfortably ≥ 2.
- **Completion signal:** clear — `SectionList` card renders once
  `sections.length > 0`; no ambiguity.
- **Error states hit:** none; no retries needed.

### Step 3 — Quiz (Gap 4)

See the dedicated Gap 4 section below — full evidence there. Summary: clearly
**synchronous**, 22.0s – 26.2s across 4 runs, always 10 questions, `status:
"Draft"` never `"Processing"`.

### Step 4 — Settings

- **In-progress signal:** none full-step; only the per-field "Saving…" text next
  to Continue.
- **Duration:** 1.5s to accept defaults and advance (1 sample — this step was
  only reached on the full run and one direct-API-assisted flow).
- **Completion signal:** clear — plain request/response, `router.push` to
  `/translate`.
- **Error states hit:** none.

### Step 5 — Translate

- **In-progress signal:** per-language row spinner (`Loader2`) while state is
  `Translating…`/`Validating…`; confirmed present.
- **Initial state, single language:** row shows "Portuguese PT Start".
- **Completion signal:** the row transitions to a `Validated`/`Review`-adjacent
  outcome badge once the run finishes — genuine, not timing-dependent (mirrors
  the real validation-run `status` field).
- **Surprise:** clicking **"Start All"** with 2 target languages (Portuguese +
  manually-added Afrikaans) did **not** reach a complete state within the
  entire 25-minute test budget (see Gap 2 / timing sections — this consumed
  most of this session's wall-clock time and is the single most important
  timing finding below).
- **Selector gotcha (own test bug, not app):** a bare `page.getByRole('listitem')`
  also matches `WizardLayout`'s step-nav `<li>` items (Input & Config, Parse,
  Quiz, …), not just the language rows — must scope via
  `page.getByRole('list', { name: 'Target languages' })` first
  (`TranslateStep.tsx:165`'s `role="list" aria-label="Target languages"`).

### Step 6 — Validate (Gap 1 & Gap 2)

See the dedicated sections below. Summary: **both fixes confirmed working as
designed** with real, non-fabricated live data (actual score, actual
per-outcome counts, actual hub connection state, and a genuine
poll-until-terminal-status completion signal for Retry — not a fixed timeout).

### Step 7 — Publish

- **In-progress signal:** `Loader2` + "Publishing…", exactly as documented.
- **Completion signal:** confirmed **clear and non-ambiguous when it succeeds**
  — `POST /publish`, single request/response, `router.push` to the talk detail
  page on success.
- **Error state hit (real, not simulated):** attempting to publish a talk with
  an **untranslated target language** (Afrikaans — "No run", since only
  Portuguese was ever started in these runs) returned **HTTP 409** with a clean
  inline toast: *"Publish failed — Request failed with status code 409"*. No
  navigation occurred, exactly as `PublishErrorAlert`/`page.tsx`'s error path is
  coded to do.
- **Real gap found (see §6 below):** the frontend's `isStepReachable(7)` check
  does **not** verify every target language has at least one validation run —
  it only checks that existing *completed* runs have no pending decisions. A
  language that was never started passes that check trivially, so the wizard
  lets the user reach the Publish step and click a button that is guaranteed to
  fail with a 409.
- I did **not** observe a successful publish→navigate in this session (every
  attempt in the time available had at least one unresolved target language).
  The button/request/error mechanics are otherwise fully characterized.

---

## 2. Gap 4 resolution — Quiz generation is SYNCHRONOUS

**Confirmed synchronous, not async/polling**, with concrete evidence from 4
independent runs:

- `POST /toolbox-talks/{id}/quiz/generate` is a single long-running request
  (22.0s – 26.2s) that blocks until Claude returns the questions.
- The **response body of that single POST already contains the full
  `ToolboxTalk` DTO**, including a populated `questions` array (always **10**
  questions across all 4 runs) and `status: "Draft"` — never `"Processing"`.
- Network log captured around the click showed **only one GET** to
  `/toolbox-talks/{id}` before the POST (the page's initial `useTalk` load) and
  **zero additional GETs after** the POST resolved, in 3 of 4 runs (the 4th run
  had exactly one extra GET arriving ~100ms after the POST resolved — a normal
  TanStack Query cache-invalidation refetch, not `Processing`-status polling).

Sample evidence (partial run 1):
```
POST /toolbox-talks/e4215550.../quiz/generate → 200, tMs=26124, status:"Draft", questions: [10 items]
GET  /toolbox-talks/e4215550...                → 200, tMs=62   (only the initial page-load GET)
```

`useTalkStatusPolling`'s `Processing`-gated polling exists in the code but is
never actually exercised for text/PDF-sourced quiz generation — it appears to
be dead-in-practice for this path (reserved for video mode, per the same
pattern as Parse).

---

## 3. Gap 1 and Gap 2 fix confirmation — BOTH CONFIRMED WORKING

Both fixes (`d0996c4`, `cc55b71`) were verified two ways: (a) direct reading of
the current `ValidateStep.tsx` source, which matches the commits' descriptions
exactly with zero trace of the old `setTimeout(1500)` / hardcoded
`percentComplete={100}` / `isConnected={false}`, and (b) live runtime evidence
from two clean Playwright runs against a real, backend-completed validation run
(talk `9dc1f756-de94-40ba-89a8-e52e18036966`, Portuguese, score 76→75%, 1
Pass/4 Review sections).

### Gap 1 — real completion signal (not a fixed 1.5s timeout)

- **Accept (synchronous path):** 558ms – 565ms across 3 samples, **no
  "Revalidating…" badge ever appeared** — correct, Accept is a synchronous DB
  write with an immediate `onSuccess` refetch.
- **Retry (asynchronous path — real `TranslationValidationJob`):** the
  "Revalidating…" badge appeared **279ms – 301ms** after the click (fast
  optimistic UI update from the mutation's `onSuccess`), then **stayed visible
  until the job genuinely finished** — total round trip **16.4s – 18.5s** across
  2 clean runs. This is real backend work (Claude Haiku + DeepL back-translation
  for one section), not a fixed wait — confirmed by the fact the two samples
  differ by ~2 seconds, consistent with real API-call variance, and by the fact
  the badge only disappeared once the run's `status` field genuinely reached a
  terminal state (poll every 2s with the `hasSeenRunning` guard, per the fix
  commit).
- **No STOP condition triggered** — the fix behaves exactly as designed.

### Gap 2 — live-wired ValidationProgressPanel (not hardcoded)

- Snapshotting the Validate page against the completed run showed a **real
  score** (76%, then 75% after Retry re-scored a section — the number visibly
  changed between runs, proving it isn't a static value), a **real
  connection indicator** ("Live" — the hub genuinely connects even for an
  already-completed run, exactly as the fix commit's fallback-chain design
  intends), and **real per-outcome counts** ("1 passed · 4 for review · 0
  failed") that match the backend API's `passedSections`/`reviewSections`/
  `failedSections` fields exactly (verified via a direct authenticated API call
  to `GET /toolbox-talks/{id}/validation/runs/{runId}`).
- **No STOP condition triggered** — grep-level and runtime evidence both agree
  the old hardcoded values are gone.

---

## 4. Timing table

| Step | Observed range | Samples | Notes |
|---|---|---|---|
| Input & Config submit | 0.36s – 2.2s | 4 | `POST /initialise`, 201 |
| Parse | 12.3s – 20.6s | 4 | `POST /parse`, 200, synchronous |
| Quiz generate | 22.0s – 26.2s | 4 | `POST /quiz/generate`, 200, synchronous |
| Settings submit | 1.5s | 1 | trivial |
| Translate + Validate, **1 language**, 5 sections | **3m 12s** | 1 (verified server-side via direct API: 14:24:51 → 14:28:02 UTC) | full pipeline: translate all sections/quiz/title + multi-round back-translation consensus + safety classification + glossary check |
| Translate + Validate, **2 languages** ("Start All"), 5 sections | **did not complete within 23+ min** | 1 (client-side; server-side final outcome unconfirmed — dev servers were torn down before a follow-up API check could be made) | started Portuguese (auto) + Afrikaans (manual) together |
| Validate → Accept (sync) | 0.56s – 0.57s | 3 | no badge, immediate refetch |
| Validate → Retry (async) | badge at ~0.28–0.30s, total 16.4s – 18.5s | 2 | real per-section revalidation job |
| Publish (blocked, 409) | button click → error toast, no navigation | 3 | correctly blocked, untranslated language present |
| Publish (success → navigate) | not observed this session | 0 | see §1 Step 7 |

**Section/question count variability** (same RAMS source text, 4 runs):
sections **5, 5, 6, 7**; quiz questions **10, 10, 10** (max of the documented
5–10 AI-determined range, consistent with the source content's length).

---

## 5. Recommendations for the eventual Playwright test

1. **Select a tenant first.** Before navigating to the wizard, set
   `localStorage.activeTenantId` (or drive the `TenantSwitcher` UI) — otherwise
   every tenant-scoped page silently redirects to `/admin/tenants`.
2. **Target-language selector:** use a positional selector — `page.locator('form').getByRole('combobox').first()` —
   until `MultiSelectCombobox`'s missing accessible name is fixed. `getByLabel`
   will not work.
3. **Scope list/button locators precisely** to avoid strict-mode collisions with
   the wizard's own step-nav chrome: use
   `page.getByRole('list', { name: 'Target languages' })` before `.getByRole('listitem')`,
   and `page.getByRole('button', { name: 'Publish', exact: true })` on the
   Publish page (a plain `{ name: 'Publish' }` also matches the step-nav "Step 7
   of 7: Publish" button).
4. **Use `expect(locator).toBeVisible({ timeout })`, never
   `locator.isVisible({ timeout })`**, for anything you need to actually wait
   for — `isVisible({ timeout })` accepts a timeout parameter but performs a
   single immediate check and does **not** poll/retry. This cost significant
   time in this session (a Retry-badge check silently always returned `false`
   until this was caught).
5. **Timeouts, with buffer over observed values:**
   - Input & Config submit: 15s
   - Parse: 60s
   - Quiz generate: 90s
   - Settings: 15s
   - Translate + Validate, **one language**: budget at least **10 minutes**
     (observed 3m12s; add generous buffer for provider variance/retries)
   - Translate + Validate, **multiple languages started together**: **do not
     assume linear scaling.** The observed 2-language run did not complete in
     23+ minutes vs. ~3m12s for one language — over 7x, not 2x. Recommend the
     **first** E2E test use exactly **one target language** to keep the suite
     fast and deterministic; treat multi-language timing as a separate,
     generously-budgeted (30–45 min) test or a manual/monitoring concern rather
     than a CI-blocking assertion.
   - Accept: 5s
   - Retry: 30s (observed 16.4–18.5s; some buffer for slower provider responses)
   - Publish: 15s
6. **Verify all target languages have a run before asserting Publish succeeds**,
   or deliberately assert the 409 error path as its own test case (it is a
   real, well-behaved error surface worth covering).
7. **New UX gap worth a backlog item:** `isStepReachable(7)` in `stepOrder.ts`
   should also verify every code in `targetLanguageCodes` has at least one
   validation run, not just that existing completed runs are free of pending
   decisions — otherwise the wizard lets a user reach Publish and click a
   button that is guaranteed to 409.
8. **New UX/perf gap worth escalating:** starting 2 target languages together
   via "Start All" appears to cost far more than 2x a single language's time
   (see §4). Worth the dev team checking the Hangfire dashboard/logs for talk
   `0385399c-b5c0-4928-b809-1b6aa4f4598c` (the run that never completed in this
   session) to determine whether this is provider rate-limiting, Hangfire
   worker concurrency, or something else.

---

## 6. Bugs / unexpected behaviour (flag only — not fixed)

1. **SuperUser tenant-selection prerequisite** for the wizard — undocumented in
   the recon; not a bug, but a real gap in test-setup understanding until this
   session.
2. **`MultiSelectCombobox` (target languages) has no accessible name** — a real
   a11y gap (`FormLabel`'s `htmlFor` doesn't reach the nested `Popover`/`Button`).
3. **RASCOR (the reference dev tenant) has zero sectors configured** — didn't
   block this session's work (sector is optional), but is a surprising
   data-completeness gap for the most-used dev/demo tenant.
4. **Publish reachability gate doesn't check per-language run coverage** — see
   §5 item 7. Real, previously-undocumented UX gap; a user can reach Publish
   and be guaranteed a 409 if any target language was never started.
5. **2-language "Start All" did not complete within 23+ minutes**, vs. ~3m12s
   for 1 language server-side-confirmed — a >7x, not 2x, slowdown. This is the
   most operationally significant finding in this session. Not confirmed
   whether it eventually finished (unconfirmed after this session ended);
   flagged for direct backend investigation.
6. **Test-tooling pitfalls worth remembering for the real E2E test** (own
   mistakes during this session, documented so the next author doesn't repeat
   them): `Locator.isVisible({ timeout })` does not poll; a bare
   `getByRole('listitem')` also matches the step-nav; a bare
   `getByRole('button', { name: 'Publish' })` collides with the step-nav
   button.

---

## Fixture adequacy — `web/e2e/fixtures/sample-toolbox-talk.pdf`

**Content is adequate; no changes recommended.** It's a realistic 3-page RAMS
(Risk Assessment and Method Statement) document (~7.9KB) covering 7 major
sections: Scope of Works, Hazard Identification/Risk table, a 6-step Method
Statement, PPE requirements, Emergency Procedures (with named contacts — real
safety-critical content that should exercise the safety-classification/glossary
pipeline), Document Approval sign-off, and a Toolbox Talk briefing record. This
is genuinely rich enough to reliably produce ≥2 sections — empirically it
produced **5–7 sections and consistently 10 quiz questions** across 4 runs when
its exact text content was driven through the wizard.

**One important caveat:** this session could **not** exercise the fixture via
the actual **Pdf** input mode / file-upload path. `InputConfigStep`'s upload
flow does a direct browser `PUT` to a presigned Cloudflare R2 URL
(`useUploadSourceFile.ts`), and this sandboxed execution environment has **no
outbound network route to `r2.cloudflarestorage.com`** (confirmed via `curl` —
requests to `cloudflare.com` fail to connect while requests to `google.com`
succeed). This is an environment limitation of this recon session, not an app
or fixture defect. I extracted the fixture's exact text and drove the
identical pipeline via **Text input mode** instead — Parse's synchronous
behavior is documented as input-mode-independent except for Video
(`ParseStep.tsx:97`), so this substitution should faithfully represent PDF-mode
behavior for everything from Parse onward. **Recommend the eventual real E2E
test (run on a machine/CI runner with real network access) do one confirmatory
run through actual Pdf mode** before finalizing — this was recon's own
recommendation and remains unconfirmed by this session.
