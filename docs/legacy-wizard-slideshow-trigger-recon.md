# Legacy Wizard Slideshow Trigger Timing — Recon

**Date:** 2026-07-08
**Branch:** transval
**Status:** Read-only recon — no application files modified
**Context:** Follow-up to `docs/new-wizard-slideshow-recon.md`, which recommended Shape C (fire
generation on toggle-flip in the new wizard's `SettingsStep`). Before committing to Shape C, this
recon verifies how the legacy create-wizard (`/admin/toolbox-talks/create`, `CreateWizard.tsx`)
actually handles its own slideshow toggle, to check whether a "trigger on Continue" pattern
(hypothesized as "Shape D") is what legacy already does — and if so, whether it's preferable.

---

## 1. Headline

**Legacy wizard uses a different pattern than either Shape C or the hypothesized Shape D — it
defers slideshow generation all the way to the Publish step, one step later than "Settings
Continue," and fires it as a fire-and-forget background job from backend code, not from a
frontend endpoint call at all.**

Neither the toggle flip (`SlideshowPanel.tsx`) nor the Settings step's "Continue" button
(`SettingsStep.tsx` in `create-wizard/`) ever triggers slideshow generation. Both only persist the
`generateSlideshow`/`slideshowSource` fields into the session's `SettingsJson` blob (via a debounced
auto-save). The actual trigger is `ContentCreationSessionService.PublishAsync`
(`ContentCreationSessionService.cs:884-938`) — **after** the draft talk/course has been finalized
and the session marked `Completed`, in a `try/catch` block that reads `session.SettingsJson` back
out, checks `GenerateSlideshow == true`, and fire-and-forget enqueues
`BackgroundJob.Enqueue<ContentGenerationJob>(job => job.GenerateSlideshowOnlyAsync(...))`. A failure
in this block is logged but does **not** fail the publish (`catch (Exception slideshowEx)` at line
933, "Publish succeeded" in the log message).

This is a genuinely distinct shape from both candidates evaluated previously — call it **Shape D**
(superseding the hypothesized "fire on Settings Continue," which legacy does not do). It resolves
the toggle-reversal race and the repeated-toggle cost risk identified against Shape C, but at a
real cost: **the user gets zero in-wizard feedback on slideshow success/failure.** The Publish
step's summary just shows a static "Generating in background" badge (`PublishStep.tsx:481-492`)
that never updates to "Ready" — confirmed by grep, there is no polling, SignalR subscription, or
query invalidation tied to slideshow status anywhere in the legacy wizard's Publish/success views.

---

## 2. Detailed walk of the legacy toggle/Continue/Publish chain

### 2.1 Toggle flip — `SlideshowPanel.tsx:32-42`

```tsx
const handleToggle = (checked: boolean) => {
  onChange({
    ...settings,
    generateSlideshow: checked,
    ...(checked && { slideshowSource: sourceOptions[0].value }),
  });
};
```

`onChange` is `SettingsStep`'s `handleChange` (or `handleChangeWithTitleClear` depending on caller —
for `SlideshowPanel` it's plain `handleChange`, `SettingsStep.tsx:274-279`). This only calls
`setSettings(newSettings)` — **pure local React state, no network call at all on flip.**

### 2.2 Debounced auto-save — `SettingsStep.tsx:91-102`

```tsx
const handleChange = useCallback((newSettings) => {
  setSettings(newSettings);
  if (saveRef.current) clearTimeout(saveRef.current);
  saveRef.current = setTimeout(() => {
    if (sessionId) updateSettingsRef.current.mutate({ sessionId, settings: newSettings });
  }, 500);
}, [sessionId]);
```

500ms after the *last* change to any Settings-step field (title, category, refresher, behaviour,
slideshow — they all funnel through this one debounced save), a `PUT` fires to
`updateSessionSettings(sessionId, settings)` (`use-content-creation.ts:580-591`), which persists the
whole settings object as JSON on the `ContentCreationSession` row. **No generation call is made
here or anywhere in this save path.** This confirms flipping the toggle on then off within 500ms
never even reaches the server — it's coalesced into a single "off" save.

### 2.3 Settings step "Continue" — `SettingsStep.tsx:172-213`

```tsx
const handleContinue = useCallback(async () => {
  ...
  setIsStartingValidation(true);
  try {
    if (saveRef.current) { clearTimeout(saveRef.current); saveRef.current = null; }
    await updateSettings.mutateAsync({ sessionId, settings });          // flush pending save
    await startValidation.mutateAsync({ sessionId, request: { targetLanguageCodes: ... } });
    onNext();
  } finally { setIsStartingValidation(false); }
}, [...]);
```

This flushes the debounced settings save, then calls `useStartValidation` →
`startTranslateValidate(sessionId, request)` → backend
`ContentCreationSessionService.StartTranslateValidateAsync` (`ContentCreationSessionService.cs:391-766`,
full method read). That method: cancels stale translation jobs, creates/updates the draft
`ToolboxTalk` (title, description, category, quiz settings, sections), optionally kicks off subtitle
processing for video-mode, and returns. **Grepped the entire method body for
`ContentGenerationJob`, `Slideshow`, `PdfUrl` — none present.** Settings-step Continue does **not**
trigger slideshow generation, contrary to the hypothesis in the design-question preamble.

Two steps (Translate, Validate) happen between Settings and Publish — the AI translation pipeline
and the human reviewer-acceptance workflow — both potentially slow and both requiring user
interaction. Slideshow generation is not gated behind either of them; it is simply not mentioned in
either step's code (grepped `TranslateStep.tsx` and `ValidateStep.tsx`, no hits for
`slideshow`/`Slideshow`).

### 2.4 Publish step "Publish" button — `PublishStep.tsx:160-194` (frontend) → `PublishAsync` (backend)

Frontend `handlePublish` calls `publish.mutate({ sessionId, request: { title, description,
category, sourceLanguageCode: 'en' } })` → `usePublish` → `POST` to the publish endpoint → backend
`ContentCreationSessionService.PublishAsync` (`ContentCreationSessionService.cs:806-971`, full method
read).

Sequence inside `PublishAsync`:

1. Validates session status/output type/sections/title (lines 823-850).
2. Sets `session.Status = Publishing`, saves (lines 852-853).
3. Calls `PublishAsLessonAsync` or `PublishAsCourseAsync` — this is where the actual `ToolboxTalk`
   (or Course) row transitions to its published form (lines 860-871). **This is also where
   `PdfUrl` gets set** — `PublishAsLessonAsync`/`PublishAsCourseAsync` construct or reuse the draft
   talk with `PdfUrl = session.InputMode == InputMode.Pdf ? session.SourceFileUrl : null` (lines
   1439, 1721) — so, unlike the new wizard's `InitialiseToolboxTalkCommandHandler`, **the legacy
   flow does not have the Fix-0 bug**: `PdfUrl` is correctly populated before slideshow generation
   is ever attempted.
4. Sets `session.Status = Completed`, saves (lines 873-874).
5. **Only now**, inside a dedicated `try/catch` block (lines 884-938), re-reads
   `session.SettingsJson`, checks `GenerateSlideshow == true && SlideshowSource != "none"`, and:
   - **Lesson output:** `BackgroundJob.Enqueue<ContentGenerationJob>(job =>
     job.GenerateSlideshowOnlyAsync(session.OutputTalkId.Value, tenantId,
     slideshowSettings.SlideshowSource, CancellationToken.None))` (lines 898-900).
   - **Course output:** loops every section-talk in the course (skipping the Full-Video item at
     `OrderIndex 0` for video mode) and enqueues one `GenerateSlideshowOnlyAsync` job per
     section-talk, always with `source: "sections"` regardless of the user's chosen
     `SlideshowSource` (lines 919-923) — a course-specific hardcoding, not user-configurable.
   - Any exception here is caught, logged, and **swallowed** — `PublishResult` still returns
     `success: true` (line 940). Slideshow failure is invisible to the publishing user.
6. Returns `PublishResult(true, effectiveOutputId, session.OutputType)` immediately — **the HTTP
   response does not wait for the Hangfire job to run.** This is fire-and-forget: `BackgroundJob.
   Enqueue` only schedules the job; `PublishAsync` returns right after the enqueue call.

### 2.5 What the user sees

- **Before publishing** (`PublishStep.tsx:481-492`): if `settings.generateSlideshow` is true, a
  static badge reading "Generating in background" is shown in the Content summary card — this
  reflects the user's *setting*, not any live job state (there is no job yet — Publish hasn't been
  clicked).
- **After publishing succeeds** (lines 200-236): the wizard shows a generic "Talk Published"
  success screen with "View Toolbox Talk" / "Create Another" buttons. **No reference to slideshow
  status at all on this screen** — confirmed by reading the full success-state JSX. The user must
  navigate to the talk's edit/detail page later to discover whether the slideshow actually
  generated.
- **Confirmed:** this is a synchronous-publish / asynchronous-slideshow split. `publish.isPending`
  only reflects the `PublishAsync` HTTP round-trip (fast — synchronous DB work only), not the
  background job.

---

## 3. Comparison against the new wizard's SettingsStep

(Per `docs/new-wizard-slideshow-recon.md` §2.1, re-confirmed inline in this session's `ide_selection`
context — new wizard's `SettingsStep.tsx`.)

| | Legacy (`create-wizard`) | New (`learning-wizard`) |
|---|---|---|
| Toggle flip | Local state only, no network call | Local state only via `saveField`, but debounce is per-field on `onCheckedChange`/`onBlur`, not a single wizard-wide debounce |
| Settings "Continue" | Flushes settings save, then calls `startValidation` (translate/validate kickoff) — **no slideshow logic** | Calls `saveField` (persists via `UpdateToolboxTalkSettingsCommandHandler`) then navigates — **no slideshow logic**, same absence as legacy |
| Where slideshow *actually* fires | **Publish step**, backend `PublishAsync`, fire-and-forget Hangfire job | **Nowhere** — `PublishToolboxTalkCommandHandler` has no slideshow-related code at all (confirmed in prior recon §2.6) |
| `PdfUrl` populated before generation is attempted? | **Yes** — `PublishAsLessonAsync`/`PublishAsCourseAsync` set it from `session.SourceFileUrl` at lines 1439/1721 | **No** — this is exactly Fix 0 from the prior recon; `InitialiseToolboxTalkCommandHandler` never sets `PdfUrl` |
| Feedback to user on job success/failure | None — static "Generating in background" badge, never resolved in-wizard | N/A — no job is ever fired, so the "Slideshow — Ready" badge in the new wizard's `PublishStep.tsx:265` is always false for PDF talks today |

**Key correction to the design-question preamble:** the legacy wizard does *not* fire on Settings
Continue. It fires one step later, on Publish — and does so from backend orchestration code, not a
frontend-triggered endpoint call. The hypothesized Shape D ("trigger on Settings-step Continue")
does not exist in the current codebase in that exact form; the closest real analog is "trigger on
the terminal wizard action" (Publish), which is a stronger, later deferral point than Settings
Continue would have been.

---

## 4. Shape D specification (revised to match what legacy actually does)

To mirror the legacy pattern in the new wizard:

**Backend:**
- Modify `PublishToolboxTalkCommandHandler` to add a block structurally identical to
  `ContentCreationSessionService.PublishAsync` lines 884-938: after the handler flips
  `Status → Published` and saves, in a `try/catch` that never fails the publish, check the talk's
  persisted slideshow-intent flag (`talk.GenerateSlidesFromPdf`, the same field
  `UpdateToolboxTalkSettingsCommandHandler` already writes — no new field needed) and, if true,
  `BackgroundJob.Enqueue<ContentGenerationJob>(job => job.GenerateSlideshowOnlyAsync(talk.Id,
  tenantId, source, CancellationToken.None))`. `source` would default to `"pdf"` since the new
  wizard's `SettingsStep` toggle only ever renders for PDF-mode talks and exposes no source picker
  (unlike legacy's three-way `RadioGroup`).
- Reuses the existing Hangfire job (`ContentGenerationJob.GenerateSlideshowOnlyAsync`, already
  reads `_slideshowGenerationService.GenerateSlideshowAsync` internally) — no new service code.
- Still requires Fix 0 (the `PdfUrl` backfill in `InitialiseToolboxTalkCommandHandler`) as a hard
  prerequisite, exactly as it was for Shape C — `GenerateSlideshowOnlyAsync` → `GenerateSlideshowAsync`
  → `GenerateFromPdfAsync` still reads `talk.PdfUrl`, not `SourceFileUrl`.
- No changes needed to `POST /{id}/generate-slides` at all — that endpoint is irrelevant to this
  shape; the job is enqueued directly from backend code, never called from the frontend.

**Frontend:**
- **None**, beyond Fix 0's effect on the toggle's visibility guard. The toggle itself needs no new
  `onCheckedChange` logic, no busy/error state, no debounce concerns — it already just persists a
  flag via the existing settings-save mutation, exactly like every other Settings-step field. This
  is a smaller frontend diff than Shape C, which required adding a dedicated call + loading/error
  UI to the toggle handler.
- Optional (to close the "user never finds out" gap identified in §2.5): add a "Generating in
  background" badge to the new wizard's own Publish step summary, matching
  `PublishStep.tsx:481-492`'s pattern — purely cosmetic, reflects the persisted flag, not live job
  state.

**Test impact:** would need coverage on `PublishToolboxTalkCommandHandlerTests` for the new
enqueue-on-publish branch (mocked `IBackgroundJobClient`/Hangfire), not `SettingsStep` or its
mutation hook at all — the opposite of Shape C's test surface.

---

## 5. Shape C vs Shape D comparison table

| | **Shape C** — fire on toggle flip (prior recon's recommendation) | **Shape D** — fire on Publish, mirroring legacy exactly |
|---|---|---|
| **Backend changes** | None beyond Fix 0 — reuse `POST /{id}/generate-slides?source=pdf` directly | Add a fire-and-forget enqueue block to `PublishToolboxTalkCommandHandler`, structurally copying `ContentCreationSessionService.cs:884-938`. Small, isolated, but touches a different, already-guarded handler (translation-validation completeness gate, reviewer-decision gate — see prior recon §2.6) |
| **Frontend changes** | Toggle's `onCheckedChange` gains a `generateSlides(talkId, 'pdf')` call plus its own busy/error state, mirroring `ToolboxTalkForm.tsx:220-236`'s `handleRegenerateSlides` | None required. Optionally a cosmetic "Generating in background" badge on the new wizard's Publish summary |
| **UX implications** | Fastest to a "Ready" badge for the field the user just touched; immediate visible feedback (loading spinner, then success/error) | Zero in-wizard feedback at all — user must leave the wizard and check the talk's edit page later to discover slideshow success/failure (this is legacy's actual, shipped behaviour today, not a hypothetical downside) |
| **Toggle-reversal race (§6.1 of prior recon)** | **Present and unresolved** — flipping on then off before the in-flight call settles leaves an inconsistent state | **Does not exist** — toggling before Publish is pure local/debounced-save state, no network call fires until the terminal Publish action, which the user cannot "undo" mid-flight the same way |
| **Repeated-toggle AI cost (§6.2 of prior recon)** | **Real risk** — every toggle-on fires a billed Claude-adjacent call (via `SlideshowGenerationService`), no debounce on the trigger itself | **Effectively zero** — the flag can be flipped on/off/on freely during editing at no cost; generation fires exactly once, at Publish, which itself only happens once per talk (one-way `Draft → Processing → ReadyForReview → Published` pipeline per CLAUDE.md) |
| **Retroactively fixes already-published new-wizard talks?** | No (forward-only), but the reused `/generate-slides` endpoint doubles as a manual backfill mechanism for ops | No — the enqueue only fires inside the publish transaction; a talk already published before this ships still needs a separate manual backfill call (same caveat as Shape C, just via a different mechanism — direct Hangfire enqueue script instead of hitting the REST endpoint) |
| **Failure visibility** | Toggle's own error state surfaces failures immediately to the person who can act on them (still mid-wizard, can retry) | Failure is logged server-side only and silently swallowed (matches legacy's own `catch (Exception slideshowEx)` — "Publish succeeded" even when slideshow enqueue itself throws, which is a narrower failure mode than the job itself failing, but still: nothing reaches the user) |
| **Blast radius / testing** | Contained to `SettingsStep` + its mutation hook | Contained to `PublishToolboxTalkCommandHandler`, which already carries two other gates — one more conditional branch in an already-guarded handler, needs care not to couple slideshow failure to publish failure (legacy's `try/catch` pattern already demonstrates how to do this safely) |

---

## 6. Recommendation

**Shape D (fire on Publish, matching legacy) is preferable to Shape C, on the strength of one
argument: it is the pattern the business has already shipped and operated for however long the
legacy wizard has existed, and it structurally eliminates both open product questions from the
prior recon (§6.1 toggle-reversal race, §6.2 repeated-toggle cost) rather than requiring a
debounce/cancellation decision to be designed and built from scratch.** There is no evidence in the
codebase that the legacy wizard's "silent, in-background, no completion feedback" trade-off has
caused a support burden — but there is also no evidence it hasn't; this recon found no error
tracking, logging dashboard, or user complaint references tied to this gap (out of scope to search
further here).

The honest trade-off: Shape D inherits legacy's actual, shipped weakness — **a talk can publish
successfully with the slideshow flag on and the generation can silently fail, with no signal to
anyone except the server log.** Shape C's toggle-time feedback loop, despite its own unresolved
race, at least surfaces failures to a user who is still in a position to retry. If the business
would tolerate closing that gap with a modest addition — e.g., surfacing `slidesGenerated` state on
the talk's edit page (which the new wizard's own `PublishStep.tsx:265` badge logic already halfway
does, just currently un-firing) — Shape D plus that addition is a stronger overall design than
either shape alone. That addition is not part of either shape as scoped and would need its own
follow-up decision.

**This recon does not overturn the prior recon's Fix 0 requirement** — both shapes need it
identically, since both ultimately call `SlideshowGenerationService.GenerateFromPdfAsync`, which
reads `talk.PdfUrl`.

No code changes were made. No fix prompt was written, per scope.
