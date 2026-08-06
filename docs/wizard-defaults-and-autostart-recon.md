# Recon: New Wizard Toggle Defaults + Parse/Quiz Auto-Start

**Scope:** `web/src/app/(authenticated)/admin/toolbox-talks/learnings/**` (the new,
URL-per-step wizard — component logic in
`web/src/features/toolbox-talks/components/learning-wizard/`). The legacy wizard
(`/admin/toolbox-talks/create`, backed by
`web/src/features/toolbox-talks/components/create-wizard/` +
`ContentCreationController` / `ContentCreationSession`) is a **different code path**
and is mentioned only for contrast. Read-only recon — no code changed.

**Note on research process:** initial parallel research misidentified the legacy
wizard's `ContentCreationSessionService.ParseContentAsync` /
`GenerateQuizAsync` (session-based, `POST /api/toolbox-talks/create/session/{id}/parse`)
as belonging to the new wizard. That flow is real but belongs to `create-wizard`,
not `learning-wizard`. All Part 3/6 findings below were re-derived directly from
the actual new-wizard files (`ParseStep.tsx`, `QuizStep.tsx`,
`ParseToolboxTalkContentCommandHandler.cs`, `GenerateToolboxTalkQuizCommandHandler.cs`)
and are independently verified against source, not carried over from that
misdirected pass.

---

## Part 1 — Toggle inventory

All toggles below are set on **Step 1 (Input & Config)** or **Step 4 (Settings)**,
plus quiz-specific ones on **Step 3 (Quiz)**. There is no toggle on Step 2 (Parse),
Step 5 (Translate), Step 6 (Validate), or Step 7 (Publish) — those steps are
content/review screens, not configuration screens.

| Toggle | Step / File | Frontend static default | What actually applies at creation | Backend field (entity default) |
|---|---|---|---|---|
| `includeQuiz` | Step 1 — `InputConfigStep.tsx` (Switch) | `true` | Sent as `InitialiseToolboxTalkCommand.IncludeQuiz` → `ToolboxTalk.RequiresQuiz` | Command default `true`; entity default `false` (`ToolboxTalk.cs:61`) |
| `preserveSourceWording` | Step 1 — `InputConfigStep.tsx` (Switch) | `false` | `InitialiseToolboxTalkCommand.PreserveSourceWording` | Command default `false`; entity default `false` (`ToolboxTalk.cs:306`) |
| `videoRightsConfirmed` | Step 1 — `InputConfigStep.tsx` (Checkbox) | `false` | Client-only gate — **not sent to the backend at all** | n/a |
| `isActiveOnPublish` | Step 4 — `SettingsStep.tsx:84` (Switch) | `true` (static) | Actual value at creation comes from **`ToolboxTalkSettings.DefaultIsActive`**, tenant-configurable, itself defaulting to `true` (`ToolboxTalkSettings.cs:111`) | Entity default `true` (`ToolboxTalk.cs:72`) |
| `generateCertificate` | Step 4 — `SettingsStep.tsx:85` (Switch) | `false` (static — but overwritten before render, see below) | Actual value at creation comes from **`ToolboxTalkSettings.DefaultGenerateCertificate`**, tenant-configurable, itself defaulting to `true` (`ToolboxTalkSettings.cs:97`) | Entity default `true` (`ToolboxTalk.cs:188`, set by migration `MakeGenerateCertificateDefaultTrue`) |
| `autoAssign` (`AutoAssignToNewEmployees`) | Step 4 — `SettingsStep.tsx:87` (Switch) | `false` | No tenant-settings override field exists for this — always `false` at creation unless the admin flips it in Step 4 | Entity default `false` (`ToolboxTalk.cs:207`) |
| `generateSlideshow` (`GenerateSlidesFromPdf`) | Step 4 — `SettingsStep.tsx:89` (Switch, only rendered when a PDF is attached and input mode isn't Docx) | `false` | No tenant-settings override field exists for this — always `false` at creation unless the admin flips it | Entity default `false` (`ToolboxTalk.cs:219`) |
| `requiresQuiz` | Step 3 — `QuizSettingsPanel.tsx` (no dedicated Switch shown; driven by Step 1's `includeQuiz`) | seeded from `talk.requiresQuiz` | `UpdateToolboxTalkQuizSettingsCommand.RequiresQuiz` | Entity default `false` |
| `shuffleQuestions` | Step 3 — `QuizSettingsPanel.tsx` (Switch) | `false` | `UpdateToolboxTalkQuizSettingsCommand.ShuffleQuestions` | Entity default `false` (`ToolboxTalk.cs:163`) |
| `shuffleOptions` | Step 3 — `QuizSettingsPanel.tsx` (Switch) | `false` | `UpdateToolboxTalkQuizSettingsCommand.ShuffleOptions` | Entity default `false` (`ToolboxTalk.cs:168`) |
| `useQuestionPool` | Step 3 — `QuizSettingsPanel.tsx` (Switch) | `false` | `UpdateToolboxTalkQuizSettingsCommand.UseQuestionPool` | Entity default `false` (`ToolboxTalk.cs:175`) |
| `allowRetry` | Step 3 — `QuizSettingsPanel.tsx` (Switch) | `true` | `UpdateToolboxTalkQuizSettingsCommand.AllowRetry` | Entity default `true` (`ToolboxTalk.cs:181`) |

**Important correction to make when reading the frontend "static defaults" column:**
every Step 4 field is immediately overwritten by `form.reset(...)` from live server
data the moment the talk loads (`SettingsStep.tsx:94-112`), so the `useForm`
`defaultValues` are only a placeholder shown for a single render before data
arrives — they are **not** what the admin actually sees or what gets persisted.
The value that matters is the entity/tenant default applied at talk creation
(`InitialiseToolboxTalkCommandHandler`), which is what the table's third column
reflects.

**Autosave note:** every Step 4 toggle calls `saveField(...)` directly from its
`onCheckedChange` handler (`SettingsStep.tsx:392,418,447,513`) — there is no
batched "Save" button. Flipping a toggle in the UI persists immediately.

### Grouped by category

**Publish/completion-behaviour (side-effect toggles):**
`isActiveOnPublish`, `generateCertificate`, `autoAssign`, `generateSlideshow`,
`requiresRefresher`/`refresherFrequency` (not a boolean but same category)

**Content-generation:** `includeQuiz`, `preserveSourceWording`, `generateSlideshow`
(overlaps with publish-behaviour — see Part 2)

**Quiz-mechanics (no side effects, pure quiz UX):** `shuffleQuestions`,
`shuffleOptions`, `useQuestionPool`, `allowRetry`

**Client-only gate (never reaches backend):** `videoRightsConfirmed`

There are no purely cosmetic "display/UI" toggles in this wizard — every boolean
found maps to a persisted backend field.

---

## Part 2 — Implications of flipping defaults to `true`

### Already effectively `true` today (tenant-configurable, already defaulted on)

- **`isActiveOnPublish` → `DefaultIsActive`** — already `true` by default
  (`ToolboxTalkSettings.cs:111`). The entity comment is explicit: *"IsActive is
  not a learner-visibility gate — assignment records control visibility."*
  Flipping the UI default here changes nothing meaningful; it's already on and
  low-risk even where it isn't (`IsActive=false` just hides the talk from being
  assignable, it doesn't cascade anywhere).
- **`generateCertificate` → `DefaultGenerateCertificate`** — already `true` by
  default (`ToolboxTalkSettings.cs:97`), and has been the entity-level default
  since migration `MakeGenerateCertificateDefaultTrue` (`20260331194830`) — a
  prior, deliberate product decision, not an oversight. **Verified safe and
  complete infrastructure**: `CertificateGenerationService.GenerateTalkCertificateAsync`
  checks `talk.GenerateCertificate` at *completion* time only
  (`CertificateGenerationService.cs:46-50`), generates a QuestPDF certificate,
  uploads to R2, and fails soft (`return null`, logged) if anything goes wrong —
  it never blocks the employee's completion flow. No further risk from this
  toggle; it is already the practical default and the code path is mature.

### Real behaviour changes if flipped

- **`autoAssign` (`AutoAssignToNewEmployees`)** — **Verified: does NOT retroactively
  assign to the existing workforce.** The only consumer of this flag is
  `AutoAssignmentService.AssignNewEmployeeTrainingAsync`
  (`AutoAssignmentService.cs:27-148`), which is invoked exclusively from
  `EmployeeService.CreateAsync` (`EmployeeService.cs:347-364`) — i.e. **only
  when a brand-new employee record is created**, using that employee's
  `StartDate` as the anchor for `DueDate` calculation. Publishing a talk with
  `AutoAssign=true` does **not** walk existing employees and does **not** fire
  assignment emails at publish time. The real behavioural change is
  forward-looking: every employee hired *after* this default flip will
  automatically get every published `AutoAssign=true` talk/course, which is a
  meaningful step-change in assignment volume and (per
  `EmployeeService.CreateAsync`'s later language-handling block) potentially
  translation-queue volume too. There is no tenant-settings escape hatch for
  this default (no `DefaultAutoAssignToNewEmployees` field exists) — flipping
  the *entity* default to `true` would apply uniformly to every tenant with no
  per-tenant opt-out, unlike `IsActive`/`GenerateCertificate` which already
  route through `ToolboxTalkSettings`.
- **`generateSlideshow` (`GenerateSlidesFromPdf`) — real, automatic, costed
  side effect confirmed.** This is the one toggle in the set with a genuine
  "silently burn API budget" risk. It is **not** checked on every save — it's
  read exactly once, at **Publish**:
  `ToolboxTalksController.PublishByTalkId` (lines 683-694) fires
  `BackgroundJob.Enqueue<ContentGenerationJob>(job => job.GenerateSlideshowOnlyAsync(...))`
  fire-and-forget, explicitly *"mirroring the legacy wizard's ...
  PublishAsync... a failure here must never fail the publish"* — i.e. publish
  always succeeds regardless of slideshow outcome, but a Claude-backed
  slideshow generation job is unconditionally enqueued whenever
  `result.Data!.GenerateSlidesFromPdf` is true. Defaulting this to `true`
  means **every** PDF-sourced talk that gets published triggers an automatic
  AI slideshow generation job with real Anthropic API cost, with no admin
  confirmation step at publish time. Given `Note 32`/`Note 33`-level caution
  already present elsewhere in this codebase about silent AI-call side
  effects, this one warrants explicit sign-off before flipping, not a
  reflexive "safe to default on."

### Quiz-mechanics toggles

`shuffleQuestions`, `shuffleOptions`, `useQuestionPool`, `allowRetry` have no
downstream side effects beyond how the employee-facing quiz renders/behaves.
`allowRetry` already defaults `true`. The other three are inert UX
preferences — flipping their defaults changes only how the quiz looks/behaves
for employees, with no jobs, emails, or cross-entity effects triggered. These
are the toggles closest to genuinely "safe to flip freely."

### Content-generation toggles

`includeQuiz` already defaults `true` and gates whether Step 3 is meaningful
(it doesn't gate an AI call by itself — that's the manual "Generate Quiz"
button in Step 3, see Part 3). `preserveSourceWording` changes parse-prompt
behaviour only, no side effects beyond parse output quality — safe to flip
either way, purely a content-fidelity preference.

---

## Part 3 — Auto-start mechanics: Parse & Quiz steps (verified against actual new-wizard files)

### Current state: both steps require an explicit click today

Neither Parse nor Quiz auto-starts currently. `QuizStep.tsx:320` even carries an
explicit code comment: *"Manual trigger by design — explicit user confirmation
before firing an AI call. Consistent with ParseStep."* This is a deliberate
existing design decision, not an oversight.

### Parse step

- **File:** `web/src/features/toolbox-talks/components/learning-wizard/steps/ParseStep.tsx`
- **Button:** "Parse Content" (line ~257-264) → `handleParse` → `useParseTalk(talkId)` hook
  (`hooks/useParseTalk.ts:11`) → `parseTalk(id)` in
  `web/src/lib/api/toolbox-talks/toolbox-talks.ts:141-144` →
  **`POST /toolbox-talks/{id}/parse`**
- **Backend:** `ToolboxTalksController.ParseContent` (`ToolboxTalksController.cs:421-453`)
  → `ParseToolboxTalkContentCommand` → `ParseToolboxTalkContentCommandHandler`
  (`ParseToolboxTalkContentCommandHandler.cs`)
- **Guard (verified, line 46-49):**
  ```csharp
  if (talk.Status != ToolboxTalkStatus.Draft)
      return Result.Fail<ToolboxTalkDto>(
          "Learning must be in Draft status to parse content.",
          FailureCode.WorkflowInvalidState);   // → HTTP 409
  ```
- **Critical asymmetry by input mode:**
  - **Video mode** (`HandleVideoAsync`, lines 140-158): sets `talk.Status = Processing`
    and saves **before** enqueueing `VideoTranscriptionJob`, so a second
    concurrent parse call within that window correctly hits the `Draft` guard
    and gets a 409. Good idempotency.
  - **Text / PDF / Docx modes** (`HandleTextAsync` / `HandlePdfAsync` /
    `HandleDocxAsync`): the Claude call (`_contentParserService.ParseContentAsync`)
    runs **synchronously inline**, and `talk.Status` is **never changed away
    from `Draft`** during that call — it stays `Draft` throughout, only
    `LastEditedStep` is touched, at the very end. **This means the guard does
    not protect against two overlapping parse calls for the majority of
    talks** (anything not video-sourced): both would pass the `Draft` check,
    both would call Claude, and both would race in
    `MaterialiseSectionsAsync` (soft-delete-existing + insert-new,
    lines 160-194) — last-write-wins, or a partial duplicate-section state
    depending on interleaving.
- **Frontend single-tab protection:** `parseMutation.isPending` (TanStack Query)
  hides the "Parse Content" button and shows a spinner the instant the click
  fires (`ParseStep.tsx:174-177,193-216`), which is enough to stop a single
  user from double-clicking in one tab, but is pure client-side UI state — a
  second tab, a fast refresh, or (relevantly for auto-start) a remount would
  not be blocked by it.
- **Re-entry / remount behavior:** the "Parse Content" button only renders
  when `sections.length === 0` (`ParseStep.tsx:245`). Once sections exist
  (parse succeeded), remounting the step shows the section editor, not the
  button — so a *naive* mount-effect auto-start gated the same way ("only
  fire if no sections yet") would not re-trigger on a normal back/forward
  navigation once parse has completed. It would, however, re-fire on **every
  remount that happens before sections exist** — e.g. if the admin backs out
  mid-parse-error, or if Draft status is reached again via re-parse or a
  future "reset" action.
- **Timing:** `ContentParserService`'s `HttpClient.Timeout = 3 minutes`,
  chained through `ResiliencePolicies.GetClaudePolicy` (3 retries,
  exponential 2s/4s/8s ±jitter) — a single parse Claude call is typically
  single-digit-seconds to under a minute, with up to ~14s added on transient
  retries.

### Quiz step

- **File:** `web/src/features/toolbox-talks/components/learning-wizard/steps/QuizStep.tsx`
- **Button:** "Generate Quiz" (line ~321-328) → `handleGenerateQuiz` →
  `useGenerateQuiz(talkId)` hook (`hooks/useGenerateQuiz.ts`) →
  `generateQuiz(id)` in `toolbox-talks.ts:172-175` →
  **`POST /toolbox-talks/{id}/quiz/generate`**
- **Backend:** `ToolboxTalksController.GenerateQuiz` (`ToolboxTalksController.cs:494-519+`)
  → `GenerateToolboxTalkQuizCommand` → `GenerateToolboxTalkQuizCommandHandler`
- **Guard (verified, line 41-44) — stronger than Parse's:**
  ```csharp
  if (talk.Status != ToolboxTalkStatus.Draft)
      return Result.Fail<ToolboxTalkDto>(
          "Learning must be in Draft status to generate a quiz.",
          FailureCode.WorkflowInvalidState);   // → HTTP 409
  ```
  Unlike Parse, the handler **does** flip `talk.Status = Processing` and save
  *before* calling the AI service (line 59-60), then flips back to `Draft`
  after (success or failure — `talk.Status = ToolboxTalkStatus.Draft` on both
  the failure branch, line 77, and the success branch, line 89). This means a
  second concurrent quiz-generate call, for the duration of generation,
  correctly hits the `Draft` guard and gets a 409 — genuine (if narrow-window)
  idempotency protection, better than Parse's for non-video content.
- **"Regenerate All" fully replaces questions:** `MaterialiseQuestionsAsync`
  hard-deletes all existing `ToolboxTalkQuestion` rows for the talk
  (`ExecuteDeleteAsync`, line 110-112) before inserting fresh ones — every
  generate/regenerate call is a full-cost, full-replace Claude operation.
  There is no per-section or incremental regeneration on the backend.
- **Timing:** one Claude call per generate request (not per-section — this
  differs from the legacy `create-wizard`'s per-section loop), same 3-minute
  timeout / Claude retry policy as Parse.
- **Frontend re-entry:** the "Generate Quiz" card only renders when
  `questions.length === 0` (`QuizStep.tsx:307`), same pattern as Parse — a
  naive auto-start gated the same way would not re-fire once questions exist.

### What auto-start would change, concretely

1. **Parse:** removing the manual click removes the one place where an admin
   currently confirms "yes, spend an AI call on this content" before it
   happens — relevant given `preserveSourceWording` and source text may still
   need editing first in some flows. For the common (non-video) path, the
   Draft-status guard gives **no real protection** against a double-fire if
   auto-start logic has any bug (e.g. fires twice on a fast double-remount,
   or a `StrictMode`-style double-effect in dev) — this needs its own
   auto-start-specific dedup, not reliance on the existing backend guard.
2. **Quiz:** the backend guard is closer to safe (Processing-gates the
   window), so an accidental double-fire would produce one success + one 409
   rather than two successful concurrent Claude calls — better, but still
   costs one wasted round-trip on the loser, and the loser's error needs
   graceful handling in the UI rather than a raw toast.
3. **Both:** navigating away mid-request does not abort the underlying HTTP
   call (no `AbortController` wiring found in either `useParseTalk` or
   `useGenerateQuiz` / the underlying `apiClient.post` calls) — the backend
   keeps running and will still persist its result. This is *good* for
   auto-start (work isn't wasted by navigation) but means a user who
   auto-triggers, immediately backs out, and comes back before the request
   resolves will see a step that looks "not started" while a request is
   silently in flight server-side, then have the UI jump to "done" a moment
   later without the user having asked for it a second time. This is a
   plausible confusing-UX edge case worth explicit design attention before
   shipping auto-start, not a blocker in itself.

---

## Part 4 — Bulk SOP import context

**Confirmed: does not exist in any form.** No controller, service, Hangfire
job, or admin UI for importing multiple Toolbox Talks/SOPs at once.
`BACKLOG.md` lists it as a High-priority, not-started item. The only "bulk"
infrastructure in the codebase is `BulkImportSession`
(`src/Core/QuantumBuild.Core.Domain/Entities/BulkImportSession.cs`), which is
CSV-based and exclusively for **employee** import (`BulkEmployeeImportJob`) —
unrelated to talk/content creation. `ContentCreationSession` and
`IContentCreationSessionService` (the legacy wizard's session model) are
strictly single-document — no `BatchId`, no batch-creation method.

**Implication for this recon's premise:** the argument "these defaults suit
bulk import" is entirely forward-looking — there is no existing bulk-import
usage pattern to validate the defaults against today. That's a legitimate
way to plan ahead, but it means the defaults are currently being tuned for a
consumer that doesn't exist yet, while the wizard's only real, present-day
consumer is an interactive human admin. If bulk import's actual shape
(one-session-per-file, staged Hangfire batch, etc.) turns out to need
different defaults than what's chosen now, there is no usage data yet to
have prevented that guess from being wrong.

---

## Part 5 — Existing tenant impact

- **`isActiveOnPublish` / `generateCertificate`:** already effectively `true`
  today via `ToolboxTalkSettings.DefaultIsActive` / `DefaultGenerateCertificate`
  (both default `true` at the entity level, per Part 2). Any tenant using the
  new wizard today is **already** seeing these as on-by-default. Flipping the
  frontend static default doesn't change tenant-observed behaviour at all —
  it only changes the pre-load placeholder that's visible for a single
  render before `form.reset` fires.
- **`autoAssign` / `generateSlideshow`:** both currently default `false` with
  **no tenant-level override field** — every tenant sees `false` today.
  Flipping the hardcoded entity default to `true` (absent adding a
  `ToolboxTalkSettings` field first) would change behaviour identically for
  every tenant with no way to opt out per-tenant — this is a bigger blast
  radius than the other two toggles, which already have a tenant-settings
  escape hatch tenants could theoretically use to opt out today (though no
  admin UI currently surfaces `DefaultAutoAssignToNewEmployees` /
  `DefaultGenerateSlidesFromPdf` either, since those fields don't exist).
- **Does the default change affect existing talks?** No — confirmed. Defaults
  in `InitialiseToolboxTalkCommandHandler` and the Step 4 form only apply at
  **creation time** (`InitialiseToolboxTalkCommand`, consumed once when the
  draft talk is first created in Step 1). Existing `ToolboxTalk` rows already
  have their boolean fields persisted; nothing in the default-flip touches
  already-created talks. Confirmed only new talks are affected.
- **Actual count of talks created via the new wizard so far:** not determined
  by this recon — would require a DB query
  (`SELECT COUNT(*) FROM "ToolboxTalks" WHERE "LastEditedStep" IS NOT NULL`
  per the `lastEditedStep` discriminator documented in CLAUDE.md Note 29).
  Worth running before flipping defaults if there's meaningful production
  usage already, to gauge how many admins have been working with the
  current (mostly-off) defaults and might notice the change.

---

## Part 6 — Auto-start safety mechanism inventory

| Mechanism | Parse | Quiz | Notes |
|---|---|---|---|
| Status-based re-entry guard | Partial — only effective for **video** mode (`Processing` set before enqueue); no-op for text/PDF/Docx (status stays `Draft` throughout the synchronous call) | Yes — `Processing` set before the AI call, reverted to `Draft` after, for the full duration | Neither has a true row lock or concurrency token; both have a narrow read-then-write TOCTOU window even where the guard exists |
| Client-side "already running" disable | Yes — `parseMutation.isPending` hides the button (`ParseStep.tsx:174-177`) | Yes — `generateMutation.isPending \|\| isProcessing` (`QuizStep.tsx:244,261`) | Single-tab only; does not survive a second tab or a fast remount |
| Backend job-level dedup (Hangfire) | N/A — parse for text/PDF/Docx is **synchronous in-request**, not a Hangfire job at all; only the video-mode transcription sub-path uses Hangfire (`VideoTranscriptionJob`, `[AutomaticRetry(Attempts=2)]`, enqueued via concrete class per Note 21) | N/A — quiz generation is also synchronous in-request, no Hangfire job involved | Because these are request-scoped, not job-scoped, "don't enqueue twice" style Hangfire dedup patterns don't apply here — the relevant unit to protect is the HTTP request itself |
| Rate limiting / refresh-spam protection | **None found** — no debounce, no cooldown, no server-side per-talk rate limit on the parse endpoint | **None found** — same | A user refreshing the Parse or Quiz page 10 times while `Draft` status still shows "no sections/questions yet" could fire 10 real requests today (manual-click) and would do so automatically under naive auto-start |
| Abort-on-navigate | **None** — no `AbortController` wiring in `useParseTalk`/`useGenerateQuiz` or the underlying `apiClient` calls | **None** — same | In-flight requests are not cancelled by navigation; see Part 3 discussion of the resulting UX ambiguity |

**Net assessment:** the strongest existing protection (Quiz's `Processing`
status-gate) is good enough to make double-fire *survivable* (one success,
one clean 409) but not free of waste. The weakest point (Parse's no-op
status-gate for non-video content, which is the common case) provides **no
real protection today** — an auto-start implementation cannot rely on the
existing backend guard and would need its own explicit "has this already
been attempted this page-load" client-side lock at minimum, and ideally a
proper status-gate fix on the Parse handler (setting a transient in-progress
status before the synchronous call, mirroring what Quiz already does) before
auto-start should be considered safe to ship for the majority (non-video)
case.

---

## Recommended defaults (Part 7)

| Toggle | Recommendation | Rationale |
|---|---|---|
| `isActiveOnPublish` | Leave as-is (already effectively `true`) | No-op change; already the practical default via `ToolboxTalkSettings.DefaultIsActive` |
| `generateCertificate` | Leave as-is (already effectively `true`) | No-op change; mature, side-effect-safe infrastructure, already the practical default |
| `includeQuiz` | Leave as-is (already `true`) | No-op |
| `preserveSourceWording` | Safe to flip either way | No side effects beyond parse-prompt behaviour |
| `shuffleQuestions` / `shuffleOptions` / `useQuestionPool` | Safe to default `true` | Pure quiz-UX preferences, no side effects |
| `allowRetry` | Leave as-is (already `true`) | No-op |
| `autoAssign` | **Do not flip without product sign-off** | Real, permanent behavioural change for every future new-hire across every tenant, with no tenant-level opt-out mechanism today. Not "immediately assigns everyone" (verified false), but is a standing default that changes onboarding assignment volume tenant-wide the moment it ships |
| `generateSlideshow` | **Do not flip without product sign-off** | Confirmed to trigger a real, automatic, fire-and-forget Claude-backed Hangfire job on every publish of a PDF-sourced talk, with real API cost and no per-publish confirmation. Of all toggles examined, this one most directly matches the "silently burn AI budget" risk named in this recon's brief |

If `autoAssign` or `generateSlideshow` defaults are wanted specifically for a
future bulk-import path (Part 4), consider scoping the default to that
import path only (e.g. a parameter on the eventual bulk-import job) rather
than changing the interactive wizard's default — the two consumers plausibly
want different answers, and bulk import doesn't exist yet to validate the
choice either way.

## Recommended auto-start approach (Part 8)

Do not auto-start Parse for non-video content until the Parse handler gets a
real in-progress status-gate (mirroring Quiz's existing `Processing` set
before / `Draft` restored after pattern) — today's `Draft`-only guard
provides no real double-fire protection for the common case, and auto-start
by definition removes the one safeguard that currently exists (the admin's
own hesitation/attention before clicking).

Quiz is closer to auto-start-ready given its existing `Processing` gate, but
still needs:
- A client-side "already attempted this session" flag so a remount doesn't
  silently retrigger a second Claude call for a request that's still
  in-flight server-side (Part 3, point 3's UX-ambiguity scenario).
- An explicit answer to "does auto-start defeat the purpose of the Regenerate
  button" — if quiz auto-generates on arrival, the manual button's only
  remaining role is *regeneration after edits*, which should probably keep
  its existing confirmation dialog (`showRegenerateConfirm`) exactly as-is.

Recommend, if auto-start proceeds, gating it explicitly on
`talk.status === 'Draft' && sections.length === 0` /
`questions.length === 0` (mirroring the existing "show button" condition, so
auto-start literally replaces the click rather than introducing new
trigger conditions) and adding a same-session `useRef` guard so React
remounts within one page load can't re-fire it — the same primitive already
used for `initializedRef` in both step components today.
