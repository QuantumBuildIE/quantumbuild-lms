# Slideshow Behaviour Recon — Three Reported Issues

**Date:** 2026-07-15
**Branch:** transval
**Status:** Read-only recon — no application files modified
**Supersedes/extends:** `docs/new-wizard-slideshow-recon.md` (2026-07-08) and
`docs/legacy-wizard-slideshow-trigger-recon.md` (2026-07-08), both of which recommended "Fix 0 +
Shape D." That recommendation **was implemented** in commit `bb9d6c4` (2026-07-08, same day as the
recon). This document verifies the current HEAD state against that claim and investigates three
newly reported issues on top of it.

---

## 0. Headline

**The "already fixed" claim is correct for what it actually fixed — slideshow generation for
new-wizard PDF talks does now fire (Shape D, on Publish) and does now succeed (Fix 0, `PdfUrl` is
populated at Initialise) — but it fixed only the generation trigger, not the two things now being
reported:**

1. **Issue 1 (Preview as Operator doesn't show slideshows) — real bug, root-caused.** The new
   wizard has **no in-wizard preview at all** (confirmed by exhaustive grep — see §3). The
   "Preview as Employee" surface being discussed is the shared `ToolboxTalkDetail.tsx` page's
   modal, only reachable **after Publish** (the wizard's own Publish step redirects straight to
   `/admin/toolbox-talks/talks/{id}` — `publish/page.tsx:33`). Shape D's slideshow generation is
   **fire-and-forget background job that starts after the publish HTTP response returns** — so
   there is a real race: the admin lands on the detail page and can open "Preview as Employee"
   before the Hangfire job finishes. In that window, `PreviewModal.tsx`'s "pending" placeholder
   (added 2026-06-04, commit `9ac296b`, for a *different* race — translated-slideshow-not-ready)
   **never fires for the initial-generation race**, because its gating condition
   (`talk.hasSlideshow || preview?.hasSlideshow`) is defined as
   `!string.IsNullOrEmpty(SlideshowHtml)` (`ToolboxTalkDto.cs:38`) — i.e. "has already finished
   generating" — not "was requested/intended" (`talk.generateSlidesFromPdf`). Before the job
   completes, `SlideshowHtml` is still null, so `hasSlideshow` is `false`, so **none of the three
   branches in `PreviewModal.tsx` render — no slideshow, no pending spinner, nothing** — it looks
   exactly like slideshow generation silently failed or was never requested. See §4.
2. **Issue 2 (view page shows no slideshow indicator) — real bug, confirmed by direct inspection.**
   `ToolboxTalkDetail.tsx`'s "Talk Details" card and the newer `SettingsEditPanel.tsx` (the two
   places settings/config are rendered on the page admins land on after publish) enumerate Quiz,
   Refresher, Certificate, and Schedule settings exhaustively — but **never reference
   `generateSlidesFromPdf`, `slidesGenerated`, or any slideshow field at all**, in either view mode
   or edit mode. `generateSlidesFromPdf` is silently threaded through `SettingsEditPanel`'s submit
   payload (line 170) purely to avoid clobbering the DB value when other settings are saved — it is
   never displayed or editable there. See §5.
3. **Issue 3 (slideshows not actually generating) — reporter's skepticism is justified; generation
   is working as designed for the one case it was built for.** Once Fix 0 + Shape D (`bb9d6c4`)
   landed, PDF-mode new-wizard talks with the toggle on **do** get slideshow generation enqueued at
   Publish and **do** get `SlidesGenerated=true`/`SlideshowHtml` populated on success — this is
   verified by direct code inspection of the full chain (Controller → `PublishToolboxTalkCommand` →
   fire-and-forget enqueue → `ContentGenerationJob.GenerateSlideshowOnlyAsync` →
   `SlideshowGenerationService.GenerateFromPdfAsync`, which now reads a populated `PdfUrl`). **This
   recon could not empirically confirm success/failure rates against real data** — the local
   Development database (queried directly, see §6) currently contains **zero PDF-mode toolbox talks
   of any kind**, wizard or legacy (all new-wizard talks present are `InputMode = Text`, mostly from
   Playwright E2E fixtures). So Issue 3 is most likely **Issue 2 wearing a disguise** exactly as the
   reporter suspected — an admin enables the toggle, publishes, sees no slideshow indicator anywhere
   on the view page (Issue 2), and reasonably concludes generation "isn't happening," when the
   generation may in fact have succeeded silently in the background with no UI evidence either way.

**All three issues are correlated but not identical.** Issues 1 and 2 share the same underlying
defect class (no persistent, intent-aware UI signal of slideshow state), and Issue 3 is best
explained as a perception consequence of Issue 2, not an independent generation defect — though this
recon cannot rule out an independent generation-failure defect given the empty local dataset (§6).

---

## 1. Slideshow settings inventory (Part 1)

All fields live directly on `ToolboxTalk` (no separate slideshow config on `ToolboxTalkSettings`,
which only carries tenant-level *defaults* for quiz/refresher/certificate — no slideshow default was
added there).

`src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Entities/ToolboxTalk.cs`:

| Field | Type | Default | Line | Meaning |
|---|---|---|---|---|
| `GenerateSlidesFromPdf` | `bool` | `false` | 219 | **Intent** — user-selected setting: "generate a slideshow for this talk." Despite the name, it is the single flag driving generation for PDF-mode new-wizard talks (no separate flag per source type). |
| `SlidesGenerated` | `bool` | `false` | 224 | **Completion status** — flipped to `true` only inside `SlideshowGenerationService.GenerateSlideshowAsync` (`SlideshowGenerationService.cs:74`) once generation succeeds. |
| `SlideshowHtml` | `string?` | `null` | 230 | The generated HTML itself (source language). |
| `SlideshowGeneratedAt` | `DateTime?` | `null` | 235 | Timestamp of last successful generation. |
| `PdfUrl` | `string?` | `null` | 87 | **Load-bearing dependency** — `SlideshowGenerationService.GenerateFromPdfAsync` (line 133) fails immediately with `"No PDF attached to this talk"` if this is empty. Historically only set by the legacy `ContentCreationSessionService`; now also set by `InitialiseToolboxTalkCommandHandler` for the new wizard (Fix 0 — see §7). |
| `Slides` (nav collection) | `ICollection<ToolboxTalkSlide>` | empty | 339 | Legacy image-based slideshow (page-image extraction), a fallback rendering path distinct from the AI HTML slideshow. Populated by a different, older pipeline (`GenerateFromPdfAsync` via `AiSlideshowGenerationService`/PDF-to-image, not covered further here as neither reported issue implicates it directly). |
| `SlideshowTranslations` (nav collection) | `ICollection<ToolboxTalkSlideshowTranslation>` | empty | 344 | Per-language translated HTML slideshows; cleared and regenerated every time `GenerateSlideshowAsync` succeeds (`SlideshowGenerationService.cs:80-90`). |

Derived/computed field, not stored: `ToolboxTalkDto.HasSlideshow => !string.IsNullOrEmpty(SlideshowHtml)`
(`ToolboxTalkDto.cs:38`) — and identically defined on `ToolboxTalkPreviewDto.cs:24` and
`MyToolboxTalkDto` (via `GetMyToolboxTalkByIdQueryHandler.cs:152`). **This is the field root-causing
Issue 1** — see §4.

No slideshow-related fields exist on `ToolboxTalkSettings` (tenant defaults) — confirmed by reading
the entity; only quiz/refresher/certificate/notification defaults are tenant-configurable.

**New-wizard Settings step toggle** (`web/src/features/toolbox-talks/components/learning-wizard/steps/SettingsStep.tsx`):
- Line 89: local form default `generateSlideshow: false`
- Line 110: hydrated from `talk.generateSlidesFromPdf`
- Line 128/168: mapped back to `generateSlidesFromPdf` on save
- **Line 494 — visibility guard: `talk?.pdfUrl && talk?.inputMode !== 'Docx'`.** This is the exact
  guard the 2026-07-08 recon flagged as broken (`PdfUrl` was always null pre-fix). Verified still
  present at this line, now functioning correctly because `PdfUrl` is populated (§7).
- The toggle **only ever renders for `InputMode.Pdf` talks** — Video, Text, and DOCX modes never
  populate `PdfUrl` (confirmed — `InitialiseToolboxTalkCommandHandler.cs:97-98` sets it
  conditionally on `InputMode.Pdf` only), so the Slideshow section of Settings is invisible for
  those modes **by design**, not by accident. This is stated explicitly in the `bb9d6c4` commit
  message ("Video/DOCX/Text modes explicitly excluded — toggle only renders for PDF").

---

## 2. Generation trigger and flow (Part 2)

**Current mechanism ("Shape D", landed in `bb9d6c4`, 2026-07-08): fire-and-forget Hangfire job
enqueued from the Publish controller action, mirroring the legacy wizard's own publish-time
enqueue.**

`src/QuantumBuild.API/Controllers/ToolboxTalksController.cs`, `PublishByTalkId` action
(lines 660-704):

1. `PublishToolboxTalkCommand` is sent via MediatR (line 670) → `PublishToolboxTalkCommandHandler`
   (below) flips `Status → Published`, returns `PublishTalkResult` including
   `GenerateSlidesFromPdf` (added to the result record specifically so the controller doesn't need a
   second DB read — per commit message).
2. Line 682-683: unconditionally enqueues `RequirementMappingJob` (unrelated feature, pre-existing).
3. **Lines 685-701 — the Shape D block:** `if (result.Data!.GenerateSlidesFromPdf)` →
   `BackgroundJob.Enqueue<ContentGenerationJob>(job => job.GenerateSlideshowOnlyAsync(talkId,
   tenantId, "pdf", CancellationToken.None))`, wrapped in try/catch that only logs
   (`_logger.LogError`) and never fails the publish response — matches the legacy pattern's
   swallow-and-log behaviour exactly.
4. Enqueued via the **concrete class** `ContentGenerationJob` (not an interface) — correctly follows
   CLAUDE.md Note 21 (Hangfire attribute visibility).

`PublishToolboxTalkCommandHandler.cs` (full file, 103 lines) itself has no slideshow logic — it only
gates on section count and translation-validation completeness (lines 31-76), flips status (78-81),
and returns the flag it read off the entity (line 85). This division of responsibility (DB-state
flip in the handler, background-job enqueue in the controller) exists because, per the commit
message, **the Application project deliberately has no Hangfire dependency** — a design constraint,
not an oversight.

**Conditional gates confirmed:**
- Only fires when `GenerateSlidesFromPdf == true` (user opted in via the Settings-step toggle).
- The toggle itself is only visible/settable for `InputMode.Pdf` talks (§1) — so in practice, Video-
  and Text-mode new-wizard talks can never have this flag meaningfully set true through the UI
  (nothing stops the API accepting it, but no frontend surface exposes it for those modes).
- No explicit tenant-setting or feature-flag gate beyond the per-talk boolean.
- `GenerateSlideshowOnlyAsync` (`ContentGenerationJob.cs:277-353`) always calls
  `SlideshowGenerationService.GenerateSlideshowAsync(tenantId, talkId, "pdf", ct)` — hardcoded
  source `"pdf"` from the controller call site (line 693), consistent with the toggle only existing
  for PDF mode.

**Synchronous vs background:** Fully asynchronous. The publish HTTP response returns immediately
after the enqueue call (`BackgroundJob.Enqueue` only schedules; it does not await job completion).
Result persistence: `SlideshowGenerationService.GenerateSlideshowAsync` (`SlideshowGenerationService.cs:36-98`)
writes `talk.SlideshowHtml`, `talk.SlideshowGeneratedAt`, `talk.SlidesGenerated = true`, deletes stale
`ToolboxTalkSlideshowTranslation` rows, and saves — all in one `SaveChangesAsync` call (line 92). On
success it also enqueues `MissingTranslationsJob` (`ContentGenerationJob.cs:330-331`) to translate the
new slideshow into languages that already have translations.

**Backend dependency chain confirmed intact:**
`SlideshowGenerationService.GenerateFromPdfAsync` (`Services/Slideshow/SlideshowGenerationService.cs:127-159`)
still reads `talk.PdfUrl` (line 133, fails with `"No PDF attached to this talk"` if empty) and
downloads the PDF bytes via `HttpClient.GetAsync(talk.PdfUrl, ...)` before calling
`_aiService.GenerateSlideshowFromPdfAsync`. This now succeeds for new-wizard PDF talks because
`PdfUrl` is populated at Initialise time (§7, Fix 0) — confirmed by direct reading, not by trusting
the commit message.

---

## 3. Preview as Operator position and behaviour (Part 3)

**Finding: there is no "Preview as Operator" (or "Preview as Learner"/"Preview as Employee") feature
anywhere inside the new wizard's own step flow.** Exhaustive grep for `preview`/`Preview` (case
insensitive) across the entire
`web/src/features/toolbox-talks/components/learning-wizard/` tree returned only unrelated hits
(image-upload alt text, plain-text stripping helper names) — no component, hook, or route under that
tree references a talk-content preview at all. This was cross-checked with a full `Glob` of every
file under `learning-wizard/` (37 files) — none is named `Preview*` or contains preview logic.

The **only** two "preview as employee/learner" surfaces in the codebase are:

1. **`PreviewModal.tsx`** (`web/src/features/toolbox-talks/components/PreviewModal.tsx`) — a shared
   component, opened from a button labelled **"Preview as Employee"** on:
   - `ToolboxTalkDetail.tsx:147-150` — the generic talk detail page at
     `/admin/toolbox-talks/talks/[id]`, gated `!previewMode` (i.e. hidden only in the read-only
     preview-mode variant of that same page, not the modal itself).
   - `create-wizard/steps/PublishStep.tsx:302,326` — the **legacy** wizard's own Publish step,
     labelled "Preview as Learner" there (commit `2f788b5`, 2026-05-12). **The new wizard's
     `PublishStep.tsx` has no equivalent** — confirmed by direct read of the full 482-line file
     (`learning-wizard/steps/PublishStep.tsx`): no `PreviewModal` import, no `Eye`-icon preview
     button, nothing. Its only slideshow reference is a passive "Slideshow — Ready" badge (line 265)
     shown iff `talk.slidesGenerated === true` — display-only, no interaction.
2. A separate, unrelated `TalkPreviewPage.tsx` (`/my/toolbox-talks/[id]?preview=true`, also from
   `2f788b5`) is opened by the legacy wizard's own preview button in a new tab — again, legacy-only,
   not reachable from the new wizard.

**Where "Preview as Operator" actually becomes reachable for a new-wizard talk:** only *after*
Publish, via redirect. `web/src/app/(authenticated)/admin/toolbox-talks/learnings/[talkId]/publish/page.tsx:33`
— `router.push(`/admin/toolbox-talks/talks/${result.talkId}`)` — fires immediately once the publish
mutation resolves (i.e., as soon as the HTTP response in §2 step 3 returns, which is *before* the
Shape D background job has necessarily finished). The admin lands on `ToolboxTalkDetail.tsx`, which
does render the "Preview as Employee" button unconditionally (not gated on any slideshow-readiness
check) — so nothing stops an admin from clicking it within the race window.

**What the preview modal fetches:** `PreviewModal.tsx`:
- `useToolboxTalkPreview(talk.id, previewLanguage)` (line 59) → admin-facing preview endpoint (same
  family as `/{id}/preview` in CLAUDE.md's endpoint table) — returns `ToolboxTalkPreviewDto` whose
  `HasSlideshow` is `!string.IsNullOrEmpty(SlideshowHtml)` (`ToolboxTalkPreviewDto.cs:24`).
- `useToolboxTalkPreviewSlides(...)` (line 64) — only enabled when
  `open && talk.slidesGenerated && talk.slideCount > 0` (image-slide fallback path).
- `useAdminSlideshowHtml(...)` (line 70) — only enabled when
  `open && (talk.hasSlideshow || !!preview?.hasSlideshow)`.

**This is the crux of Issue 1** — every one of the three "does a slideshow exist" checks that gate
what's fetched and rendered (`slidesGenerated`, `hasSlideshow` from the cached list-page `talk`
object, and `preview?.hasSlideshow` from the freshly-fetched preview DTO) is a **completion-based**
signal (`SlideshowHtml` non-empty), not an **intent-based** one (`generateSlidesFromPdf`). None of
them will ever be true while the Shape D background job is still running. See §4 for the exact
render-branch consequence.

---

## 4. Why Issue 1 happens — the render-branch gap in detail

`PreviewModal.tsx` lines 161-217 define three mutually-exclusive branches for the "Presentation"
card:

```
1. slideshowHtmlData?.html                                            → render HTML slideshow
2. !html && slides?.length > 0                                        → render image slideshow
3. !html && !slides && (talk.hasSlideshow || preview?.hasSlideshow)   → render "generating…" spinner
```

Comment above them (line 161, present in the file today) literally reads: *"THREE BRANCHES FOR
SLIDESHOW, 1. HTML Slideshow 2. Image-based slideshow fallback 3. Pending: slideshow expected but
not yet generated"* — i.e., branch 3 was written with the explicit *intent* of covering exactly the
scenario reported in Issue 1. **But its guard condition inherits the same completion-based
`hasSlideshow` used by branches 1 and 2**, which is defined purely by `SlideshowHtml` being
non-empty (`ToolboxTalkDto.cs:38`). Since generation hasn't happened yet in the race window, none of
`talk.hasSlideshow`, `preview?.hasSlideshow`, or `talk.slidesGenerated` are true — **all three
branches evaluate false, and the entire "Presentation" card silently disappears** rather than
showing branch 3's spinner. The intent flag that *would* correctly gate branch 3
(`talk.generateSlidesFromPdf`) is available on the `ToolboxTalk` type (`web/src/types/toolbox-talks.ts:112`)
and is passed into `PreviewModal` as a prop already (`talk: ToolboxTalk`) but is never read by this
component at all (confirmed by grep — `generateSlidesFromPdf` does not appear anywhere in
`PreviewModal.tsx`).

**The commit that added branch 3 (`9ac296b`, 2026-06-04, "fix(UAT 1.1.9)") predates Fix 0/Shape D
(`bb9d6c4`, 2026-07-08) by over a month.** At the time branch 3 was written, new-wizard PDF talks
could never even reach a state where generation was triggered at all (per the 2026-07-08 recon), so
the scenario "generation is in flight, refresh will show it" most plausibly targeted was the
**translated-slideshow-lag** case (source-language slideshow already exists — `hasSlideshow=true` —
but the just-added target language's HTML hasn't been translated yet), not the **initial-generation**
race that Shape D's fire-and-forget enqueue introduced afterward. Branch 3 was never revisited when
Shape D landed, so it inherited a gap it wasn't originally designed to cover.

---

## 5. View page slideshow display or absence (Part 4)

Two components render "Talk Details"/"Settings" on `ToolboxTalkDetail.tsx`
(`/admin/toolbox-talks/talks/[id]`, the page every publish flow — new and legacy — redirects to):

1. **`ToolboxTalkDetail.tsx`'s own "Talk Details" card** (lines 246-325): enumerates Frequency,
   Sections count, Video (with source badge + external link), Minimum Watch %, Quiz Required,
   Passing Score, Questions count, Attachment. **No slideshow field of any kind** — confirmed by
   grep (`Slideshow|slideshow|slidesGenerated|generateSlidesFromPdf` → zero matches in this file).
2. **`SettingsEditPanel.tsx`** (`web/src/features/toolbox-talks/components/detail/SettingsEditPanel.tsx`),
   rendered directly below via `<SettingsEditPanel talk={talk} onRefetch={refetch} />` (line 358 of
   `ToolboxTalkDetail.tsx`) — this is the newer, inline-editable settings surface for new-wizard
   talks. Its zod schema (`settingsEditSchema`, lines 39-50) and both its view-mode (`ViewRow`
   entries, lines 501-583) and edit-mode (`FormField` entries, lines 278-497) render Quiz,
   Refresher, and Certificate settings exhaustively — **`generateSlidesFromPdf` is present exactly
   once in this file, at line 170, purely as a pass-through value in the submit payload** (`
   generateSlidesFromPdf: talk.generateSlidesFromPdf,` — carried unchanged so that saving other
   settings doesn't accidentally null it out). **It is absent from the schema, absent from both
   view-mode and edit-mode JSX, and there is no toggle, badge, or read-only row for it anywhere in
   either component.**

**Conclusion: Issue 2 is a straightforward, unambiguous gap — not a display bug in the sense of
"shows the wrong thing," but a total absence.** An admin who enables "Generate slideshow" in the
Settings step and later opens the published talk's detail page has zero way, from that page alone,
to confirm the setting was saved, is still enabled, or whether generation succeeded, is pending, or
failed. The only place any slideshow-state signal exists post-publish is the transient "Slideshow —
Ready" badge on the new wizard's own `PublishStep.tsx:265` — which the admin only sees during the
publish flow itself, before the redirect in §3 fires, and which (per §2) is very likely to read
`false` at that exact moment regardless of eventual success, since the job hasn't run yet.

---

## 6. Generation success rate findings (Part 5)

**Local Development database was reachable and queried directly** (psql, `127.0.0.1:5432`, database
`rascor_stock`, schema `toolbox_talks` — trust-auth, no password required locally). Query:

```sql
SELECT "InputMode", ("LastEditedStep" IS NOT NULL) AS new_wizard,
       "GenerateSlidesFromPdf", "SlidesGenerated", count(*)
FROM toolbox_talks."ToolboxTalks"
GROUP BY 1,2,3,4 ORDER BY 1,2;
```

Result — **the entire local dataset**:

| InputMode | new_wizard | GenerateSlidesFromPdf | SlidesGenerated | count |
|---|---|---|---|---|
| Text | false | false | false | 34 |
| Text | true | false | false | 5 |

**There are zero PDF-mode toolbox talks in the local Development database at all** — new wizard or
legacy. The five new-wizard rows present are all `InputMode = Text`, titled things like `"E2E Wizard
Manual Run …"` / `"E2E Wizard Partial Run …"` — clearly Playwright E2E test fixtures (per CLAUDE.md
Note 30), all still `Status = Draft` with `LastEditedStep` 3-5 (never reached Publish). **This local
database cannot answer "is slideshow generation actually succeeding" at all** — there is no PDF-mode
talk, published or otherwise, to inspect. This is very likely explained by the periodic Development
DB wipes referenced elsewhere in CLAUDE.md/BACKLOG, combined with the fact that PDF-mode talks
requiring slideshow generation are a manual, deliberate admin action rather than something E2E
fixtures currently exercise.

**No slideshow-specific logging/telemetry dashboard was found** — `ContentGenerationJob.GenerateSlideshowOnlyAsync`
logs generously to the standard app log (start/success/failure banners with duration and HTML size,
`ContentGenerationJob.cs:285-351`), but this recon found no aggregated view, alert, or metrics table
for slideshow failures — a failure is discoverable only by grepping raw logs for
`"SLIDESHOW-ONLY GENERATION JOB FAILED"` or `"...EXCEPTION"`.

**What to check manually, if empirical confirmation is wanted:**
1. In whichever environment (Development/Production) actually has PDF-mode talks published via the
   new wizard since 2026-07-08 (when `bb9d6c4` shipped), run the same query above scoped to
   `"CreatedAt" > '2026-07-08'` and `"InputMode" = 'Pdf'`.
2. For any row where `"GenerateSlidesFromPdf" = true` and `"SlidesGenerated" = false` more than a few
   minutes after `"PublishedAt"`, that is a genuine generation failure (not just an in-flight job) —
   cross-reference the Hangfire dashboard (`/hangfire` if exposed, or the `hangfire` schema tables
   directly — `hangfire.job`, `hangfire.state`) for a failed `GenerateSlideshowOnlyAsync` job around
   that talk's `PublishedAt` timestamp, or grep application logs for the failure banner above with
   that `TalkId`.
3. If the business wants a faster manual check going forward: query
   `SELECT "Id","Title","GenerateSlidesFromPdf","SlidesGenerated","SlideshowGeneratedAt","PublishedAt" FROM toolbox_talks."ToolboxTalks" WHERE "InputMode"='Pdf' AND "GenerateSlidesFromPdf" AND "PublishedAt" IS NOT NULL ORDER BY "PublishedAt" DESC;`
   and eyeball the gap between `PublishedAt` and `SlideshowGeneratedAt` (null = never completed).

---

## 7. Recent git history for slideshow-related changes (Part 6)

Full chronological list of slideshow-touching commits (`git log --all -i --grep=slideshow`, newest
first as found; table below re-ordered oldest→newest for narrative clarity):

| Date | Commit | Summary |
|---|---|---|
| 2026-02-19 | `fc3b6e7` | Add slideshow generation from video transcript |
| 2026-02-23 | `f3e629c` | Slideshow generation truncation fix — output limits, truncation detection |
| 2026-02-23 | `700b48a` | Move slideshow navigation to React wrapper — eliminate AI generation variance |
| 2026-03-12 | `42471ff` | Slideshow generation, video subtitles, standalone video learning (foundational feature commit) |
| 2026-03-12 | `0967b3b` | Refactor: tidy Settings step layout, improve Slideshow panel copy (**legacy** wizard) |
| 2026-03-26 | `e2972b2` | Generate slideshow per Learning in course output when slideshow option selected |
| 2026-03-27 | `07a99c4` | `MissingTranslationsJob` checks translation completeness, not just record existence |
| 2026-03-27 | `20e669e` | Trigger slideshow translation immediately after slideshow generation |
| 2026-03-29 | `2a0f701` | Per-tenant AI usage logging across all Claude API call sites (touches slideshow AI calls incidentally) |
| 2026-04-22 | `18cc4df` | Fix `WizardSectionDivider` spacing (legacy wizard, incidental) |
| 2026-05-12 | `2f788b5` | **"Preview as Learner" from publish step — legacy `create-wizard` only.** No new-wizard equivalent ever added (§3). |
| 2026-05-12 | `36d2c27` | Wizard defaults + CLAUDE.md archive (incidental) |
| 2026-05-17 | `fe33aad` | Security: harden job tenant scoping (incidental) |
| 2026-06-01 | `556068a` | Slideshow prompt — postMessage on slide change, WCAG AA contrast |
| 2026-06-02 | `d462523` | Remove slideshow postMessage bridge — was racing AI-generated slideshow's own messages |
| 2026-06-04 | `9ac296b` | **`PreviewModal.tsx` — add "pending" branch 3** (the branch central to §4's Issue 1 root cause) |
| 2026-06-05 | `1b97d18` | Docs rename/extend (incidental) |
| 2026-06-11 | `8360fb2`/`9fcc45c` | **Rebuild new-wizard Step 4 (Settings)** — "wizard 5.3d" — likely introduced/repositioned the current `SettingsStep.tsx` toggle and its (then-broken) `pdfUrl` guard |
| 2026-06-19 | `dd65dc7` | §25 Chunk 3 — SettingsStep visual polish (new wizard) |
| 2026-06-22 | `a2dce8b` | Option-B/1 service migration (incidental) |
| 2026-06-23 | `e7b59fb` | DOCX import via the wizard — explicitly excluded from the slideshow toggle guard |
| **2026-07-08** | **`bb9d6c4`** | **"Fix and wire new-wizard slideshow via Fix 0 + Shape D"** — the commit matching the "already fixed" session claim. Verified still intact at HEAD (§2, §7 below); nothing since has touched `InitialiseToolboxTalkCommandHandler.cs`, `PublishToolboxTalkCommand(Handler).cs`, or the Shape D block in `ToolboxTalksController.cs` (confirmed via `git log bb9d6c4..HEAD -- <those files>` → no results for the Application/Controller files; only `SettingsStep.tsx` has zero hits in that range either). |

**Post-`bb9d6c4` commits (7 total, through `3a4c894` at HEAD) do not touch any slideshow-relevant
file** — confirmed by `git log --oneline bb9d6c4..HEAD` (7 commits: `feat(learnings): Activate
default…`, `fix(review): external reviewer deserialization…`, `fix(wizard): show validation
scores…`, `fix(wizard): ValidateStep send-for-review gate…`, `fix(review): graceful UX…`,
`feat(reviewers): tenant reviewer configuration…`, `fix(ui): toast notifications position…`) — all
external-review/reviewer-config/UI-polish work, unrelated to slideshow. **No regression was
introduced after the Fix 0 + Shape D commit** — the generation-trigger fix genuinely still stands at
HEAD. What was never addressed, before or after `bb9d6c4`, is the display/preview gap described in
§4-§5, because that commit's stated scope (per its own message) was explicitly the trigger and the
`PdfUrl` data bug, not any UI surfacing of the resulting state.

**Verification detail for Fix 0** — `InitialiseToolboxTalkCommandHandler.cs:97-98` (read in full at
HEAD):
```csharp
PdfUrl = request.InputMode == InputMode.Pdf ? request.SourceFileUrl : null,
PdfFileName = request.InputMode == InputMode.Pdf ? request.SourceFileName : null,
```
Confirmed present, unchanged since `bb9d6c4`.

---

## 8. Per-issue diagnosis

### Issue 1 — "Slideshows don't appear on the Preview as Operator surface in the new wizard"

**Real bug, root-caused, not a step-ordering problem in the sense originally hypothesized.** There is
no in-wizard preview step to be mis-ordered relative to generation (§3) — the premise of "preview
sits before generation in the step sequence" does not apply because there is no preview step in the
sequence at all. The actual mechanism is a **race between an instant post-publish redirect
(`publish/page.tsx:33`) and an asynchronous, unbounded-latency background job (Shape D)**, compounded
by a **rendering gap**: `PreviewModal.tsx`'s existing "pending" branch (added 2026-06-04, before
Shape D existed) uses a completion-based signal (`hasSlideshow`) instead of an intent-based one
(`generateSlidesFromPdf`), so it never actually renders during the specific window this race
creates. Evidence: `PreviewModal.tsx:67,70,73,202` (gating conditions), `ToolboxTalkDto.cs:38`
(`HasSlideshow` definition), `ToolboxTalksController.cs:685-701` (fire-and-forget enqueue point),
`publish/page.tsx:33` (immediate redirect).

### Issue 2 — "View page doesn't display whether generate-slideshow was selected"

**Real bug, unambiguous absence, directly confirmed.** Neither `ToolboxTalkDetail.tsx`'s "Talk
Details" card nor `SettingsEditPanel.tsx` (view or edit mode) references `generateSlidesFromPdf` or
`slidesGenerated` anywhere except one silent pass-through value in a submit payload
(`SettingsEditPanel.tsx:170`) that exists purely to avoid overwriting the DB value, not to surface
it. Evidence: full-file greps returning zero display-related matches (§5), full read of both
components confirming the absence structurally (every other settings category — Quiz, Refresher,
Certificate, Schedule — has a matching `ViewRow`/`FormField` pair; Slideshow has neither).

### Issue 3 — "Slideshows aren't generating when selected"

**Reporter's skepticism appears justified — most likely Issue 2 masking as Issue 3 — but this recon
cannot fully confirm generation is succeeding in practice, only that the code path is intact and
should succeed.** The full trigger→generation→persistence chain was read end-to-end and is
internally consistent (§2, §6/§7 Fix 0 verification) — nothing in the code as it stands at HEAD would
prevent successful generation for a correctly-configured PDF-mode new-wizard talk. However, the
local Development database contains **zero PDF-mode talks of any kind** (§6), so this recon could
verify the code's correctness but not its real-world track record. Given that (a) the only UI
surface that would ever tell an admin "yes, this generated successfully" is the transient, easy-to-miss
`PublishStep.tsx:265` badge, and (b) the permanent view page has no such indicator at all (Issue 2),
an admin has no ordinary way to distinguish "generation succeeded but I can't see it" from
"generation silently failed" — which is sufficient to explain the report without assuming an actual
generation defect exists. **Recommend closing this out empirically per §6's suggested queries in
whichever environment has real PDF-mode publish activity before assuming any further code change is
needed here.**

---

## 9. Suggested chunk breakdown for fixes

Recon only — no fix prompts are written per scope. Rough sizing and touch-points only.

1. **[Small] Fix `PreviewModal.tsx`'s pending-branch gating to cover the initial-generation race.**
   Touches: `PreviewModal.tsx` only (add `talk.generateSlidesFromPdf` to branch 3's condition,
   alongside the existing `hasSlideshow` checks, so "intended but not yet generated" and "generated
   for another language but not this one" are both covered by the same spinner). No backend
   changes. Directly resolves Issue 1's most visible symptom (silent disappearance). Does not
   address the underlying race window's duration — that is a UX latency question, not a display
   bug, and probably out of scope for this fix.

2. **[Small] Add a persistent slideshow status field to `ToolboxTalkDetail.tsx`'s "Talk Details"
   card or `SettingsEditPanel.tsx`.** Touches: one of those two components (recommend
   `SettingsEditPanel.tsx` for symmetry with how Quiz/Refresher/Certificate already work — add a
   `ViewRow`/`ToggleRow` pair plus a status badge similar to the new wizard's own
   `PublishStep.tsx:265-275` "Ready" pattern, extended to also show "Generating…"/"Not requested").
   Directly resolves Issue 2. Should reuse the existing `generateSlidesFromPdf` (intent) +
   `slidesGenerated` (completion) pair rather than introducing new fields — no backend/migration
   work needed.

3. **[Medium, optional/product-decision-gated] Close the race window itself, not just its display
   gap.** Two independent sub-options, not mutually exclusive:
   - Make the Publish HTTP response await slideshow generation (turns an async, unbounded-latency UX
     into a synchronous, bounded-latency one) — touches `PublishToolboxTalkCommandHandler`/
     controller, reintroduces the "does Publish block on AI latency" trade-off the original Shape
     C/D comparison explicitly reasoned about and rejected (see `docs/legacy-wizard-slideshow-trigger-recon.md`
     §5) — likely not worth revisiting without a product conversation, given that trade-off was
     already made deliberately.
   - Add a lightweight polling/SignalR nudge on `ToolboxTalkDetail.tsx` (or specifically the
     Preview modal, while open) so that once `SlidesGenerated` flips true server-side, the preview
     auto-refreshes rather than requiring a manual re-open — touches `PreviewModal.tsx` +
     possibly a new or reused SignalR hub/subscription. Larger than chunk 1/2, genuinely optional
     if chunk 1's spinner + a manual "refresh" affordance is judged sufficient.

4. **[Small, verification-only, no code] Confirm Issue 3 empirically before writing any fix for
   it.** Not a code chunk — run the §6 queries against Development/Production data. If they show
   `SlidesGenerated = false` persisting long after `PublishedAt` for `GenerateSlidesFromPdf = true`
   rows, that is a genuine, separate generation-failure bug requiring its own investigation
   (starting point: grep app logs for the `"SLIDESHOW-ONLY GENERATION JOB FAILED"`/`"...EXCEPTION"`
   banners in `ContentGenerationJob.cs:335-351`, and check Hangfire's own retry/failure state for
   that job type). If they show `SlidesGenerated = true` reliably, Issue 3 closes as "resolved by
   chunk 2" with no further backend work.

**Suggested order:** 4 (verify first — costs nothing, changes nothing about chunks 1-2's necessity)
→ 1 and 2 in parallel (both small, independent files, both pure-frontend) → 3 only if product wants
to revisit the async trade-off after seeing chunks 1-2 in practice.
