# New Wizard Slideshow Generation — Recon (re-verified at HEAD)

**Date:** 2026-07-08
**Branch:** transval
**Status:** Read-only recon — no application files modified
**Supersedes:** the prior version of this file (dated 2026-06-26), whose premise about *how* content
generation is triggered in the new wizard no longer matches HEAD. That doc is preserved in spirit
below where it was still correct, and explicitly corrected where the architecture has moved on.

---

## 1. Headline

**Bug confirmed still present at HEAD — and it is a different, more fundamental bug than the
2-week-old investigation described, because the wizard's content-generation architecture has been
rewritten in the interim.**

The prior investigation's premise — "`ContentGenerationJob` is enqueued at the end of ParseStep,
before SettingsStep sets the slideshow flag" — **is no longer true**. At current HEAD, the new
wizard (`learning-wizard/`) does not use `ContentGenerationJob` at all for Text/PDF/Docx input
modes. Parsing and quiz generation are now **synchronous MediatR command handlers** invoked
directly from the Parse and Quiz steps (§2.1–2.2). `ContentGenerationJob` (and its
`AutoGenerateSlidesAsync` helper) is used exclusively by the **legacy** create-wizard, the old
`ContentCreationSessionService` flow, and the `/generate` / `/smart-generate` endpoints — none of
which the new wizard calls (§2.5).

This has replaced the old "ordering race" bug with a **data-mapping gap**:

1. **The Slideshow toggle never renders for new-wizard talks in the first place.** Its visibility
   guard in `SettingsStep.tsx:494` is `talk?.pdfUrl && talk?.inputMode !== 'Docx'`. But
   `InitialiseToolboxTalkCommandHandler` (used by the new wizard's InputConfigStep) and
   `ParseToolboxTalkContentCommandHandler` (used by ParseStep) **never populate `ToolboxTalk.PdfUrl`**
   for any input mode — the new wizard stores the uploaded file exclusively under
   `SourceFileUrl`/`SourceFileName` (§2.3). `PdfUrl` is only ever written by the legacy
   `ContentCreationSessionService` (lines 1439, 1721) and `ContentDeduplicationService` (line 450)
   — neither of which the new wizard touches. **Net effect: for every talk created via the new
   wizard, `talk.pdfUrl` is `null`, so the entire "Slideshow" section (4e) is invisible — even for
   PDF-mode talks with a PDF actually attached.**
2. **Even if the toggle were made visible, nothing in the new wizard's step chain ever calls
   slideshow generation.** `UpdateToolboxTalkSettingsCommandHandler.Handle` (called by
   `SettingsStep`'s `saveField`/`handleContinue`) only persists the flag (line 74); it never calls
   `ISlideshowGenerationService` or enqueues a job. `PublishToolboxTalkCommandHandler` (§2.6) has no
   slideshow logic either.
3. **Even a direct fix that only wires up a trigger call would still fail today**, because
   `SlideshowGenerationService.GenerateFromPdfAsync` (the method actually invoked by
   `POST /{id}/generate-slides?source=pdf`) also reads `talk.PdfUrl` (line 133) — the same null
   field. And the one existing manual remedy for this — the legacy edit page's "Regenerate
   Slideshow" panel — disables its "Generate from PDF" radio option whenever `!talk.pdfUrl &&
   !talk.pdfFileName` (`ToolboxTalkForm.tsx:931`), which is always true for a new-wizard talk. **There
   is currently no UI path — wizard or post-publish edit page — that can produce a PDF-sourced
   slideshow for a talk created via the new wizard.** The only way around it today is the
   `source=sections` mode, which is implemented in the backend
   (`SlideshowGenerationService.GenerateFromSectionsAsync`) but is **not exposed in any current
   frontend** (not the new wizard's SettingsStep, not the edit page's radio group — only the old
   create-wizard's `SlideshowPanel.tsx` exposed a three-way source choice, and that component is
   not reachable for new-wizard talks).

Any of the three candidate shapes (A/B/C) requires this `PdfUrl`/`SourceFileUrl` reconciliation as a
**shared prerequisite** before step-ordering changes matter at all. **Recommendation: Shape C**,
paired with the mandatory field fix — see §5.

---

## 2. Part 1 — Verified findings at HEAD

### 2.1 Step-by-step side effects (`web/src/features/toolbox-talks/components/learning-wizard/steps/`)

| Step | File | On entry | On "Continue" / primary action | Slideshow refs |
|---|---|---|---|---|
| 1. Input & Config | `InputConfigStep.tsx` | Pre-populates reviewer/target-language/sector defaults (lines 179–215) | `onSubmit` (line 298): uploads file via `useUploadSourceFile` if not already uploaded, then calls `initialiseMutation.mutateAsync` → `POST /toolbox-talks/initialise` → `InitialiseToolboxTalkCommandHandler`. Routes to step 2 (line 339). | None |
| 2. Parse | `ParseStep.tsx` | Polls talk via `useTalkStatusPolling` (line 59) | "Parse Content" button (line 259) → `useParseTalk` → `parseTalk(talkId)` → **synchronous** `POST /toolbox-talks/{id}/parse` → `ParseToolboxTalkContentCommandHandler`. "Save & Continue" (line 336) → `useUpdateTalkSections` → routes to step 3. | None |
| 3. Quiz | `QuizStep.tsx` | Loads existing questions from polled talk (line 103) | "Generate Quiz" button (line 323) → `useGenerateQuiz` → `generateQuiz(talkId)` → **synchronous** `POST` → `GenerateToolboxTalkQuizCommandHandler`. "Save & Continue" (line 428) saves questions, routes to step 4. | None |
| 4. Settings | `SettingsStep.tsx` | Populates form from `useTalk` once (lines 94–112) | Each field's `onBlur`/`onCheckedChange` calls `saveField` (line 114) → `updateTalkSettings` → `UpdateToolboxTalkSettingsCommandHandler`. "Continue" (line 149) does the same save then navigates. | **Yes — lines 493–522.** Toggle only rendered when `talk?.pdfUrl && talk?.inputMode !== 'Docx'` (line 494; always false for new-wizard talks — see §2.3). `onCheckedChange` (line 511) calls `saveField({ ...values, generateSlideshow: checked })`, which maps to `generateSlidesFromPdf` on the `UpdateTalkSettingsRequest` payload (line 128/168). No generation call is made. |
| 5. Translate | `TranslateStep.tsx` | Kicks off translation workflow | — | None |
| 6. Validate | `ValidateStep.tsx` | Shows validation run results | — | None |
| 7. Publish | `PublishStep.tsx` | Displays summary via `useTalk` | Parent page wrapper calls `usePublishTalk` → `publishTalk(talkId)` → `PublishToolboxTalkCommandHandler` (no slideshow logic — see §2.6) | **Display only — line 265.** Shows "Slideshow — Ready" badge iff `talk.slidesGenerated === true`. For a new-wizard PDF talk this is always `false`, so the badge never appears regardless of the toggle state. |

### 2.2 Content generation is no longer a single Hangfire job for the new wizard

- `ParseToolboxTalkContentCommandHandler.cs:36-58` dispatches synchronously by `InputMode`:
  `HandlePdfAsync` (line 82), `HandleVideoAsync` (line 140), `HandleDocxAsync` (line 113),
  `HandleTextAsync` (line 60). Only **Video mode** enqueues a background job — line 155:
  `_parseJobScheduler.EnqueueVideoTranscriptionJob(talk.Id, talk.TenantId)`, which
  (`ParseJobScheduler.cs:15`) enqueues `VideoTranscriptionJobForTalk`, which chains into
  `ContentCreationParseJobForTalk` on completion. **Neither of these video-mode jobs calls
  `ContentGenerationJob` or any slideshow generation** — confirmed by grep across
  `src/Modules/ToolboxTalks` for `BackgroundJob.Enqueue<ContentGenerationJob>`; the only three call
  sites are `ContentCreationSessionService.cs:898,921` and `ToolboxTalksController.cs:1252,1421`
  (the `/generate` and `/smart-generate` endpoints), all legacy-wizard-only (§2.5).
- Quiz generation (`GenerateToolboxTalkQuizCommandHandler.cs:31-96`) is likewise a synchronous
  in-request AI call (sets `talk.Status = Processing` only as a UI-polling signal, line 59–60, not
  a background job).

### 2.3 `PdfUrl` vs `SourceFileUrl` — the actual root cause

Grep for every write site of `ToolboxTalk.PdfUrl` across `src/Modules/ToolboxTalks`:

| File:line | Context |
|---|---|
| `ContentCreationSessionService.cs:1439` | `draftTalk.PdfUrl = session.InputMode == InputMode.Pdf ? session.SourceFileUrl : null;` — legacy content-creation-session flow only |
| `ContentCreationSessionService.cs:1721` | Same mapping, different draft-materialisation path |
| `ContentDeduplicationService.cs:450` | `target.PdfUrl = source.PdfUrl;` — content-reuse/dedup copy, not wizard-path |
| `InitialiseToolboxTalkCommandHandler.cs` | **Never sets `PdfUrl`** — only `SourceFileUrl`/`SourceFileName`/`SourceFileType` (lines 89–92) |
| `ParseToolboxTalkContentCommandHandler.cs` (`HandlePdfAsync`, lines 82–111) | **Never sets `PdfUrl`** — extracts text from `talk.SourceFileUrl` (line 88) and only sets `talk.GeneratedFromPdf = true` (line 106) |

`GetToolboxTalkByIdQueryHandler.cs:72` (the query behind `useTalk`, which `SettingsStep` reads) maps
`PdfUrl = talk.PdfUrl` straight from the entity — so it faithfully reports `null`. The DTO *does*
separately carry `SourceFileUrl`/`SourceFileName` (lines 140–141), which **is** populated and
**is** a durable public R2 URL (`useUploadSourceFile.ts:53`, presigned-PUT pattern, not a
short-lived signed URL) — so the file is retrievable at any later point in the wizard or after
publish. The gap is purely that `SettingsStep.tsx:494` and `SlideshowGenerationService.
GenerateFromPdfAsync` (below) both key off the wrong/legacy field name.

The `web/.../lib/__tests__/stepOrder.test.ts:26` test-talk factory independently corroborates this
— its default fixture sets `pdfUrl: null`, consistent with the new architecture never populating it.

### 2.4 Backend generation service still requires `PdfUrl`

`SlideshowGenerationService.cs`:
- `GenerateSlideshowAsync` (line 36) dispatches on an explicit `source` string param: `"sections"`
  (line 53, `GenerateFromSectionsAsync`), `"video"` (line 57, `GenerateFromVideoTranscriptAsync`),
  else `"pdf"` (default, line 63, `GenerateFromPdfAsync`).
- `GenerateFromPdfAsync` (line 127): `if (string.IsNullOrEmpty(talk.PdfUrl)) return Result.Fail<string>("No PDF attached to this talk");` (line 133) — reads `PdfUrl`, not `SourceFileUrl`. **This means even a naively-wired "call `/generate-slides` when the toggle flips" fix would still fail for new-wizard PDF talks** unless this method (or its caller) is updated to fall back to `SourceFileUrl`, or `PdfUrl` is backfilled somewhere upstream.
- `GenerateFromSectionsAsync` (line 100) has no such dependency — it reads live `ToolboxTalkSections` rows, which the new wizard's Parse step does populate correctly. This is a viable **workaround path** that sidesteps the `PdfUrl` gap entirely, at the cost of a different (text-based, not page-image-based) slideshow.

`AutoGenerateSlidesAsync` (`ContentGenerationJob.cs:378-471`) still exists, unchanged in spirit from
the prior investigation, and still falls back to `talk.GenerateSlidesFromPdf`/`talk.PdfUrl` (lines
393-411) when no explicit `slideshowSource` option is passed — but this method is **only reachable
from `ContentGenerationJob.ExecuteAsync`, which the new wizard never enqueues**.

`ContentGenerationJob.GenerateSlideshowOnlyAsync` (line 277) is the job used by the legacy edit
page's "slideshow-only" regeneration path documented in note 32 territory (§9 of CLAUDE.md
background section on ContentGenerationJob) — also PDF/section/video source-driven, also not
called by the new wizard.

### 2.5 Confirming `ContentGenerationJob` is legacy-wizard-only

- `web/.../create-wizard/steps/ParseStep.tsx` is the only frontend file calling
  `generateToolboxTalkContent()` (→ `POST /toolbox-talks/{id}/generate` → `ContentGenerationJob`, via
  `ToolboxTalksController.cs:1252`).
- The new wizard's `ParseStep.tsx` (learning-wizard) calls `parseTalk()` exclusively — a completely
  different endpoint/handler pair (§2.1–2.2). No file under `learning-wizard/` references
  `generateToolboxTalkContent`, `useGenerateContent`, or `ContentCreationSession`.

### 2.6 Publish step has no slideshow awareness

`PublishToolboxTalkCommandHandler.cs` (full file read, lines 1-103): validates section count,
translation-validation-run completeness gate, and reviewer-decision gate, then flips
`Status → Published`. **No reference to `PdfUrl`, `GenerateSlidesFromPdf`, `SlidesGenerated`, or
`ISlideshowGenerationService` anywhere in this handler.** Publishing a talk with the flag set to
`true` and no slides generated does not fail, warn, or retroactively trigger generation — it simply
publishes silently incomplete.

---

## 3. Part 2 — Shape comparison

All three shapes share a **mandatory shared prerequisite** (call it "Fix 0"): resolve the
`PdfUrl`/`SourceFileUrl` mismatch, either by (a) backfilling `talk.PdfUrl` = `talk.SourceFileUrl`
when `InputMode == Pdf` inside `InitialiseToolboxTalkCommandHandler` (cheapest, mirrors the pattern
already used by `ContentCreationSessionService.cs:1439`), or (b) updating
`SlideshowGenerationService.GenerateFromPdfAsync` and the `SettingsStep.tsx:494` guard to read
`SourceFileUrl` instead of/in addition to `PdfUrl`. Option (a) is less invasive — it makes the new
wizard's data shape converge with the field the rest of the slideshow pipeline already expects,
with no behaviour change to `SlideshowGenerationService` itself.

| | **Shape A** — toggle moved to Step 1/2, triggered right after upload/parse | **Shape B** — trigger deferred to end of Settings (reinterpreted; no monolithic job exists to "defer" anymore) | **Shape C** — fire on toggle flip, mirror edit page's Regenerate button |
|---|---|---|---|
| **Backend changes** | None beyond Fix 0, if the trigger reuses the existing `POST /{id}/generate-slides` endpoint. If instead folded into `ParseToolboxTalkContentCommandHandler.HandlePdfAsync`, that handler needs a new "and also generate slides" branch — more invasive, couples an already-multi-purpose handler to slideshow AI cost/latency. | Add a call to `ISlideshowGenerationService.GenerateSlideshowAsync` inside (or immediately after) `UpdateToolboxTalkSettingsCommandHandler.Handle`, gated on `request.GenerateSlidesFromPdf && !talk.SlidesGenerated` (or a false→true transition check to avoid re-firing on every settings save). Plus Fix 0. | None beyond Fix 0 — reuse the existing, already-tested `POST /{id}/generate-slides?source=pdf` endpoint directly from `SettingsStep`'s toggle handler, exactly as `ToolboxTalkForm.tsx:982` already does for the edit page. |
| **Frontend changes** | Move the "Generate slideshow from PDF" `FormField` (currently `SettingsStep.tsx:493-522`) into `InputConfigStep.tsx` (after upload succeeds, guard on local `sourceFileUrl`/`inputMode==='Pdf'` state rather than a round-tripped `talk` field) or into `ParseStep.tsx` (after sections exist). Needs its own loading/error UI matching the existing Parse/Quiz "Wand2 button + spinner" pattern already established at `ParseStep.tsx:257-266` and `QuizStep.tsx:320-328`. | No step-order change — toggle stays in `SettingsStep.tsx`, only its visibility guard changes (Fix 0). `handleContinue`/`saveField` need a "Saving settings and generating slideshow…" loading state since the settings-save request now blocks on an AI call. | Toggle stays in `SettingsStep.tsx`; its guard changes (Fix 0); `onCheckedChange` (line 511) additionally calls `generateSlides(talkId, 'pdf')` and needs its own busy/error state, closely mirroring `ToolboxTalkForm.tsx:220-236`'s `handleRegenerateSlides`. |
| **Tests likely needing updates** | `InputConfigStep`/`ParseStep` component tests (if any exist) gain a new toggle+call path; `stepOrder.test.ts` fixture defaults are unaffected since `pdfUrl`/`sourceFileUrl` aren't part of reachability logic today, but a new "slideshow pending" wizard state might need a reachability rule if slideshow generation becomes a gate. | `UpdateToolboxTalkSettingsCommandHandlerTests` (exists: `tests/.../UpdateToolboxTalkSettingsCommandHandlerTests.cs`) needs new cases for the gated generation call, including a mocked `ISlideshowGenerationService`. | New test coverage on `SettingsStep`'s toggle handler (frontend) exercising the fire-and-await call; no backend handler test changes needed since the existing `/generate-slides` endpoint is reused unmodified. |
| **UX implications** | Front-loads AI latency into Step 1 (delays "Continue" for every user, even those who don't want a slideshow, if made mandatory to wait for) or Step 2 (couples to an already-slow Parse call). Slideshow status is knowable earliest in the flow. | Couples the *last* settings save (or every settings save, if not carefully gated) to slideshow-generation latency — a user tweaking an unrelated toggle (e.g. certificate) after the slideshow one was already set could unintentionally re-trigger generation unless the handler diffs old vs. new flag value. | Fastest to "Ready" badge for the specific field the user just touched; matches an interaction pattern the codebase and its users (via the edit page) already know. Isolated blast radius — only the slideshow toggle's own save is affected, not the whole Settings-step save. |
| **Risks** | Bigger diff — touches a step (Input/Config) that isn't naturally about slideshow at all; conceptually odd to ask "generate slides?" before sections/quiz even exist. Race risk is essentially eliminated (PDF is freshly uploaded). | Silent re-generation on unrelated settings edits if not carefully gated (cost risk — each AI slideshow call is a billed Claude call per the AI Usage Logging system). Blocks the whole settings-save request on AI latency, which today is a fast, cheap DB write. | **Toggle-reversal race (see §6)** — turning the toggle on then off again before the in-flight generation call resolves leaves `SlidesGenerated=true`/HTML populated while the flag reads `false`, an inconsistent state with no defined resolution today. Repeated on/off toggling during testing/demoing re-fires a full billed AI call each time (no debounce). |
| **Retroactively fixes already-published new-wizard talks with the flag set but no slides?** | **No** — forward-only; a talk published before this ships still has `SlidesGenerated=false` and no code path revisits it. | **No** — same caveat. | **No** — same caveat, **but** Shape C's trigger mechanism (the `/generate-slides` endpoint) is exactly the mechanism a manual backfill script or an admin-facing "regenerate" affordance would need anyway, so shipping Shape C also unblocks a manual fix for pre-existing talks (once Fix 0 lands) — Shapes A/B don't provide that side benefit as directly. |

---

## 4. Part 3 — Cross-cutting findings

### 4.1 DOCX and Video modes — structurally unreachable, confirmed by more than one guard

- **DOCX:** `SettingsStep.tsx:494`'s explicit `talk?.inputMode !== 'Docx'` clause is actually
  redundant defence-in-depth — DOCX uploads go through `SourceFileUrl` exactly like PDF (never
  `PdfUrl`), so the `talk?.pdfUrl` half of the guard alone already excludes DOCX. No error path is
  reachable; the toggle is just invisible, same as PDF mode.
- **Video:** the new wizard's video-mode talks have `SourceFileUrl`/`VideoUrl` set but never
  `PdfUrl`, so the Slideshow section is likewise invisible for video-mode wizard runs — it is not a
  separate bug, it's the same `PdfUrl`-guard gap. Video-mode slideshow generation
  (`source=video`, `GenerateFromVideoTranscriptAsync`) exists in the backend and is exposed in the
  legacy edit page's radio group (`ToolboxTalkForm.tsx:946-963`, gated on `talk?.videoUrl` which
  *is* correctly populated for video-mode talks) — so video-mode talks, unlike PDF-mode ones, *do*
  have a working manual remedy today via the edit page, once published. The new wizard itself still
  has no path to trigger it.
- Neither DOCX nor Video mode's own background-job chain
  (`VideoTranscriptionJobForTalk` → `ContentCreationParseJobForTalk`, `ParseJobScheduler.cs:15`) calls
  `AutoGenerateSlidesAsync` or any slideshow service — confirmed by grep; these jobs only handle
  transcription/parsing.

### 4.2 PublishStep "Slideshow — Ready" badge — works automatically once generation is wired up

The badge (`PublishStep.tsx:265`) is purely a read of `talk.slidesGenerated`, refreshed via
`useTalk`'s TanStack Query cache. No changes are needed to the badge itself for any of Shapes A/B/C
— as soon as any shape successfully calls `ISlideshowGenerationService.GenerateSlideshowAsync` (which
sets `talk.SlidesGenerated = true` at `SlideshowGenerationService.cs:74`) and the relevant mutation's
`onSuccess` invalidates/repopulates the `['learnings', talkId]` query key, the badge will render
correctly the next time PublishStep mounts. This is a non-issue in all three shapes.

### 4.3 The manual "Regenerate Slideshow" edit-page remedy is currently a dead end for new-wizard PDF talks

Documented in §1 point 3 and confirmed by reading `ToolboxTalkForm.tsx:900-1022`: the "Generate from
PDF" radio option is `disabled` whenever `!talk?.pdfUrl && !talk?.pdfFileName` (line 931) — always
true for new-wizard PDF talks. The "Generate from video transcript" option is likewise disabled
without a `videoUrl` (irrelevant for a PDF talk). There is no "Generate from sections" option in
this UI at all (only the legacy create-wizard's separate `SlideshowPanel.tsx` component ever exposed
that third radio choice, and that component isn't rendered for new-wizard talks). **This means Fix
0 is required not just for the new wizard's forward-going flow, but to unblock the existing manual
remedy for any new-wizard PDF talk already published without a slideshow.**

---

## 5. Recommendation

**Ship Fix 0 (the `PdfUrl`/`SourceFileUrl` reconciliation) first, independent of which shape is
chosen — it is a bug in its own right (it also silently breaks the edit-page manual remedy) and
blocks all three shapes equally.** Recommended concrete form: in
`InitialiseToolboxTalkCommandHandler.cs`, when `request.InputMode == InputMode.Pdf`, also set
`talk.PdfUrl = request.SourceFileUrl` and `talk.PdfFileName = request.SourceFileName` at entity
creation (mirrors the existing pattern at `ContentCreationSessionService.cs:1439`). This is a
one-property-set change with no migration, no new dependency, and it makes both the new wizard's
`SettingsStep.tsx:494` guard and the legacy edit page's `ToolboxTalkForm.tsx:931` guard start
working correctly with zero further changes to either of those files.

**Then, for the trigger-timing question: Shape C.** Reasoning, with the trade-off stated honestly:

- Shape C is not free — it carries a real, unresolved design gap (the toggle-reversal race, §6)
  and a real cost risk (no debounce on a billed AI call if a user flips the toggle repeatedly while
  exploring the UI). Both must be addressed before shipping, not glossed over.
- But Shape A is architecturally the odd one out — it asks the user to decide about slideshow
  generation in Step 1, before sections or quiz exist, which doesn't match how every other
  AI-driven decision in this wizard is sequenced (Parse, then Quiz, then Settings — each step's
  AI action is scoped to what that step is "about"). It would also be the most invasive to
  implement cleanly (new UI real estate in a step that currently has none of this).
- Shape B silently couples an unrelated, currently-cheap mutation (settings save) to an expensive,
  slow one (AI slideshow generation), and only remains safe if implemented with careful
  false→true transition detection — a class of bug (re-firing paid AI calls on every unrelated
  settings tweak) that is easy to introduce and easy to miss in review.
- Shape C's chosen trigger point (the toggle itself) is the most legible to a user and reuses
  code and UX patterns that already exist and are already trusted in this codebase (the edit
  page's Regenerate button, and the Parse/Quiz steps' own "click a Wand2 button, wait, see a
  result" pattern) — it is the smallest actual diff of the three once Fix 0 is in place, and it is
  the only shape that also hands operations a working "regenerate for an existing talk" mechanism
  as a side effect, which is otherwise needed anyway as a backfill remedy for talks published before
  this ships.

No shape is a full fix on its own. **None of A/B/C retroactively repairs a talk already published
with the flag set and no slideshow** — that requires a separate one-off backfill (a script or an
admin action that calls `POST /{id}/generate-slides?source=pdf` for every affected talk, which only
works once Fix 0 has landed).

---

## 6. Open product questions

1. **Toggle-reversal race (Shape C, explicitly unresolved):** if a user flips the slideshow toggle
   on (firing an AI generation call that takes several seconds) and then flips it off again before
   the call resolves, what should happen?
   - Option (i): disable the toggle entirely while a generation is in-flight, so "off" can't be
     clicked until the in-flight call settles.
   - Option (ii): let the toggle be flipped freely, but treat "off" purely as "don't show the
     slideshow to employees" (a display-time gate) without discarding already-generated HTML —
     i.e. decouple "has a slideshow been generated" from "is it currently enabled."
   - Option (iii): actually cancel the in-flight request (client aborts the HTTP call, which — per
     `ToolboxTalksController.cs:1311`'s use of `HttpContext.RequestAborted` — the server does
     observe and can stop on) and discard any partial result.
   This is a genuine UX/product decision, not something the code can resolve unilaterally — it
   should be decided before Shape C ships, not discovered by a QA tester flipping the switch twice.
2. **Repeated-toggle AI cost:** should the toggle be debounced, or should re-generation be
   short-circuited if `talk.SlidesGenerated` is already `true` and the source content (PDF /
   sections) hasn't changed since the last generation — to avoid a user idly flipping the switch
   a few times racking up billed Claude calls each time?
3. **Backfill scope:** does the business want a one-off migration/admin action to regenerate
   slideshows for talks already published via the new wizard with the flag set but no slides, or is
   "fixed going forward only" acceptable? If backfill is wanted, it needs Fix 0 first, then can
   reuse the same `/generate-slides` endpoint Shape C wires up.
4. **Source parity:** should the new wizard eventually expose the "Generate from sections" option
   (already implemented backend-side, never surfaced in any current frontend) as a fallback for
   talks where the original PDF is no longer ideal to reprocess (e.g. content was heavily edited
   after parsing), the way the old create-wizard's `SlideshowPanel.tsx` did? This is out of scope
   for fixing the present bug but is a natural follow-on now that the gap in the current UI has been
   identified.
