# CertifiedIQ — Backlog (Source of Truth)

**Last updated:** 17 June 2026
**Purpose:** Comprehensive record of every known item — bug, feature, refactor, product decision — across the CertifiedIQ LMS. This is the long reference. Active prioritised work is captured directly in this document. A separate sprint file is no longer maintained — see §5.26 for the rationale.

## Conventions

**Priority:**

- **P0 — Critical.** Blocks release, undermines product trust, real customer impact, or production-broken.
- **P1 — High.** Real workflow friction, real customer or quality impact, near-term blocker.
- **P2 — Medium.** Polish, completeness, rough edges in working flows.
- **P3 — Low.** Nice-to-have, tooling, internal quality, future capability.
- **PD — Product Decision Required.** Not buildable until a product/business decision is made.

**Origin tags:**

- `[UAT]` — real customer feedback (Jessie Ryan / Ryan's Bakery)
- `[Boss]` — product owner / business request
- `[Internal-QA]` — discovered by team during testing
- `[Production-incident]` — surfaced as a real bug in production
- `[Engineering]` — quality / process / refactor identified by the team
- `[Roadmap]` — forward-looking product capability

**Status:**

- `Open` — not started
- `In Progress`
- `Blocked` — waiting on decision, dependency, or external party
- `Deferred` — deliberately parked
- `Done` — closed (kept here for trail until pruned)

---

## Phase status

**Phase 5: ✅ Closed — 2026-06-17.** All in-scope work shipped or descoped; deferred work moved to Post-Phase-5 Cleanup (Section 7). Cutover toggle infrastructure (§5.27) shipped with default position "Classic wizard" — **§24 engineering gate cleared 2026-06-18; toggle-flip is now an operational decision.** Production deployment of §5.28 (Anthropic model deprecation fix) is operational and tracked separately.

See `docs/phase-5/closure-notes.md` for the closure summary.

---

# 1. Content Creation & Translation

## 1.1 Translator UAT — Jessie Ryan / Ryan's Bakery

Source: `CertifiedIQ_Translator_UAT_Brief_Ryans_Bakery_v3.pdf` (27 May 2026). Real-world feedback from a trial admin running EN→RU on two SOPs (Silos, High-Speed Mixer). Each item below maps to a specific finding with file references in the original brief.

### P0 — Critical (Translator trust)

#### 1.1.1 Validation summary contradicts itself ("0/4 passed, score 100%")

- **Priority:** P0
- **Origin:** `[UAT]`
- **Status:** ✅ Done (3 Jun 2026)
- **Resolution:** Backend completion message reduced to "Validation complete" — no more concatenated counters. Frontend `ValidationProgressPanel.tsx` renders two distinct labelled metrics on completion: `Score: X%` and `Sections passed: Y / Z`. Review badge tinting now conditional: slate (neutral) when overall score ≥ threshold, amber when within 10 of threshold, red when significantly below. Closes 1.1.12 as a side effect (same badge tinting code). Verified on Development.

#### 1.1.2 Final score 100% EN / 80% RU contradicts upstream 100%

- **Priority:** P0
- **Origin:** `[UAT]`
- **Status:** ✅ Done (3 Jun 2026)
- **Resolution:** Backend filters source language out of target list when creating validation runs — if source is EN and targets include EN, only non-EN runs are created. Frontend `PublishStep.tsx` section heading changed to "Back-translation scores" with description "Consensus back-translation score per target language". Each per-language score is clearly labelled. Frontend index-misalignment bug also caught and fixed (RU run was being rendered with EN label after the backend filter dropped EN — fixed by sourcing language mapping from session storage rather than wizard state). Verified on Development.

#### 1.1.3 No path to edit English source and re-validate

- **Priority:** P0
- **Origin:** `[UAT]`
- **Status:** ✅ Done (3 Jun 2026) — with known limitation (see 1.1.18)
- **Resolution:** Reviewers can now edit both Original (English source) and Translation textareas in Edit mode on the Validate step. An inline amber warning above the source textarea discloses the consequences. New `EditedSource` field on `TranslationValidationResult` stages the edit; on Accept, the edit propagates to `ToolboxTalkSection.Content` and sets `NeedsRevalidation = true` on all other-language translations of the same talk (per-talk granularity — `ToolboxTalkTranslation` is per-talk, not per-section). Single-section re-validation reuses the existing `ConsensusEngine` pipeline by overriding `OriginalText` with `EditedSource` when present — no validation logic duplication. Concurrency guarded with 409 response if a re-validation is already running.

  Migration `20260602142845_AddEditedSourceAndRevalidationFlag` adds both new fields (CLI-generated, both `.cs` and `.Designer.cs` present).

  **Known limitation:** the propagation flattens HTML structure (lists, headings) to plain paragraphs because the reviewer edits a plain-text projection of the HTML source. To prevent stored XSS, the propagated text is HTML-encoded and wrapped in `<p>` elements (one paragraph per non-empty line). The amber warning discloses this: _"Editing the source affects all translations of this section, and may simplify the formatting of structured content (lists, headings) into plain paragraphs."_ Long-term fix tracked as **1.1.18** (rich-text editor).

### P1 — High (workflow breakers)

#### 1.1.4 Slideshow counter mismatch ("Slide 1 of 6" when on slide 4)

- **Priority:** P1
- **Origin:** `[UAT]`
- **Status:** ✅ Done (2 Jun 2026)
- **Resolution:** Two-part fix. Prompt change in `SlideshowGenerationPrompts.cs` told AI-generated slideshows to natively post `slideChanged` messages on every navigation. Then a second fix removed the legacy `InjectPostMessageBridge` script entirely from `AiSlideshowGenerationService.cs` — it was racing the AI's own messages and overwriting them with `current: 0` because its `currentSlide()` fallback couldn't read the AI's slide-index variable. Verified on Development: counter syncs and stays correct, Back button activates after slide 1.

#### 1.1.5 Slideshow Back button doesn't navigate

- **Priority:** P1
- **Origin:** `[UAT]`
- **Status:** ✅ Done (2 Jun 2026)
- **Resolution:** Same root cause as 1.1.4 (currentSlide stuck at 0 due to bridge reset); resolved by the same fix.

#### 1.1.6 "Continue" wedged inactive after early Next on Input step

- **Priority:** P1
- **Origin:** `[UAT]`
- **Status:** ✅ Done (4 Jun 2026) — Continue button unwedged: language presence check added to `canContinue`, `videoRightsConfirmed` persisted across InputConfigStep remount.
- **Description:** Click Next before choosing target languages → Continue stays disabled even after languages filled. Only a hard refresh recovers it.
- **Cause:** `canContinue` doesn't require `targetLanguageCodes.length > 0`. `createSession.mutateAsync` runs without languages; mutation's pending state + missing languages combo wedges button.
- **Files:** `InputConfigStep.tsx:263-271, 273-340, 922-930`
- **Fix direction:** Add languages check to `canContinue`. Wrap `handleContinue` body in try/finally. Optionally include `targetLanguageCodes` in `createSession` payload so backend rejects empty.
- **Acceptance:** Filling a missing required field re-enables Continue with no refresh.

#### 1.1.7 Section content not editable after parse

- **Priority:** P1
- **Origin:** `[UAT]`
- **Status:** ✅ Done (4 Jun 2026) — Inline section body editor shipped at parse step; backend allowlist widened to Parsed/QuizGenerated/Validated with cascade-reset of downstream artefacts on edit.
- **Description:** Sections can be renamed and reordered, but body is read-only. To fix wording the user must delete and start over.
- **Cause:** Body rendered via `dangerouslySetInnerHTML`. `onChange` only fires for title/delete/reorder.
- **Files:** `SectionList.tsx:289-306, 320-328, 41`
- **Fix direction:** Inline body editor (textarea or contenteditable). Save on blur via existing `onChange`. Mark each edited section with manual-override flag so re-parse doesn't stomp it.
- **Acceptance:** Body editable; edit survives going back and re-entering.

#### 1.1.8 Back-nav from Parse loses deletions, forces full re-parse

- **Priority:** P1
- **Origin:** `[UAT]`
- **Status:** ✅ Done (4 Jun 2026) — Parse-step edits preserved across back-nav via sessionSourceSnapshot pattern; InputConfig return only clears parse state when source content actually changed.
- **Description:** Delete section → Back → return → re-parse forced including the wait. Quiz settings, by contrast, preserved.
- **Cause:** `hydrateFromSession()` overwrites client `parsedSections` with stale backend `parsedSectionsJson`. InputConfig step explicitly clears `parsedSections: []` on return Continue ("reset to Draft for re-parsing").
- **Files:** `ParseStep.tsx:131-136, 182-202`, `InputConfigStep.tsx:303-313`
- **Fix direction:** Persist Parse edits to backend (extend `parsedSectionsJson` or add `editedSectionsJson`). Skip hydrate when client state non-empty. Only clear on InputConfig return if source content actually changed.
- **Acceptance:** Parse state persists across back-nav like Quiz state does.

#### 1.1.9 Preview as Employee — no slideshow after navigating back and forth

- **Priority:** P1
- **Origin:** `[UAT]`
- **Status:** ✅ Done (4 Jun 2026) — PreviewModal renders a pending-state placeholder when `talk.hasSlideshow` is true but slideshow data is not yet generated.
- **Description:** Preview renders without slideshow card while generation still in flight. Sections present.
- **Cause:** `PreviewModal` only renders slideshow card if `slideshowHtmlData?.html` truthy OR image-based slides present. Both empty during async generation.
- **Files:** `PreviewModal.tsx:69-73, 160-197`
- **Fix direction:** Third branch — if `talk.hasSlideshow` true but data empty, render "Slideshow generating — refresh to see it" placeholder. Optional: poll status while modal open.
- **Acceptance:** Preview always shows slideshow card if one exists; pending shows explicit pending state.

### P2 — Medium (friction & polish)

#### 1.1.10 AI rewrites SOP wording; no "verbatim" mode

- **Priority:** P2
- **Origin:** `[UAT]`
- **Status:** ✅ Done (2 Jun 2026)
- **Resolution:** Prompt change in `SectionGenerationPrompts.cs` plus full plumbing chain (DTO, entity, EF config, migration `AddPreserveSourceWordingToContentCreationSession`, service methods including the Hangfire video-path job, frontend wizard state, Step 1 toggle). When `preserveSourceWording` is on, the AI is instructed to identify section breaks but preserve source wording exactly — no rewriting, no rephrasing, no added safety advice. Verified end-to-end on Development: off-mode paraphrases as before, on-mode reproduces source content verbatim.

#### 1.1.11 Word document (.docx) input not supported

- **Priority:** P2
- **Origin:** `[UAT]`
- **Status:** Open
- **Description:** Customers maintain SOPs in Word; converting to PDF first is friction. Frontend rejects non-PDF in two places. Working `ExtractFromDocxAsync` already exists in LessonParser module — only wizard wiring missing.
- **Files:** `InputConfigStep.tsx:220, 459`, `InputMode.cs:6-11`, `DocumentExtractorService.cs:99-130`
- **Fix direction:** Add `Docx = 4` to InputMode enum. Allow `.docx` MIME in frontend allowedTypes. Branch in `ContentParserService` to call `ExtractFromDocxAsync`.

#### 1.1.12 Orange "Review (n)" badge reads as error when it isn't

- **Priority:** P2
- **Origin:** `[UAT]`
- **Status:** ✅ Done (3 Jun 2026) — Badge tint made conditional as a side effect of the 1.1.1 fix: slate when score ≥ threshold, amber at-threshold, red below.
- **Description:** Orange tint + filled dot looks like a problem to fix, not a neutral counter.
- **Cause:** Review badge unconditionally amber when `statusCounts.review > 0`.
- **Files:** `ValidationProgressPanel.tsx:128-135`
- **Fix direction:** Make tint conditional. Slate (neutral) when `overallScore >= passThreshold`. Amber at-threshold, red below. Pairs with 1.1.1.

#### 1.1.13 No diff view between source and back-translation

- **Priority:** P2
- **Origin:** `[UAT]`
- **Status:** Open
- **Description:** Score 100% but reviewer can't see why (no differences) or what would have caused a lower score. Feels like rubber-stamping in the dark.
- **Cause:** Back-translations rendered as plain text blocks.
- **Files:** `ValidationSectionCard.tsx:437-475`
- **Fix direction:** Add `diff-words` package (~3KB). Render back-translation with `<ins>` / `<del>` markup against original. "No differences detected" when diff empty.

#### 1.1.14 Slide colour contrast / accessibility

- **Priority:** P2
- **Origin:** `[UAT]`
- **Status:** ✅ Done (2 Jun 2026)
- **Resolution:** Prompt change in `SlideshowGenerationPrompts.cs` (all three prompt blocks). Body text opacity lifted from 0.75 to 0.95; secondary from 0.5 to 0.75. WCAG AA 4.5:1 contrast constraint and no-colour-only-signalling rule added. Verified on Development.

#### 1.1.15 Quiz questions too hard for floor audience

- **Priority:** P2
- **Origin:** `[UAT]`
- **Status:** ✅ Done (2 Jun 2026)
- **Resolution:** Prompt change in `QuizGenerationPrompts.cs` plus full plumbing chain (parameter, DTO, entity, EF config, migration `AddAudienceRoleToContentCreationSession`, service methods, frontend wizard state, Step 1 dropdown with three values: Operator / Supervisor / Auditor). Operator quizzes explicitly forbid identifier-recall questions; distractor diversity rule added; plain-language requirement added. Verified end-to-end on Development with a healthcare CMS access policy document — Operator quiz is procedural (no identifier recall), Auditor quiz has appropriately formal compliance framing.

#### 1.1.16 Quiz: deleted questions cannot be restored after back-nav

- **Priority:** P2
- **Origin:** `[UAT]`
- **Status:** Open
- **Description:** Deleted questions can't be restored. Immediate save, no soft-delete, no Undo.
- **Cause:** `handleDeleteQuestion` filters out and immediately saves.
- **Files:** `QuizStep.tsx:184-193`
- **Fix direction:** Toast Undo via sonner (`toast.success(..., { action: { label: 'Undo', onClick: restore } })`) or persist deletions only on Continue (matches 1.1.8).

#### 1.1.17 Request: per-tenant Translator defaults

- **Priority:** P2
- **Origin:** `[UAT]`
- **Status:** Open
- **Description:** Customer wants to globally set target languages and content options rather than picking per-SOP.
- **Cause:** Not a bug — feature request. Wizard initialises empty; no tenant-defaults lookup.
- **Files:** Infrastructure exists — `TenantSetting.cs`, `useTenantSettings()` hook
- **Fix direction:** New tenant-settings keys under `Module = ToolboxTalks.Defaults`: target languages, sector, include-quiz, pass threshold, reviewer role, audience role, verbatim mode. Read in `InputConfigStep` on mount; allow per-import override. Settings UI in `/admin/toolbox-talks/settings`.

#### 1.1.18 Rich-text editor for section source/translation editing

- **Priority:** P2
- **Origin:** `[Engineering]` `[UAT-followup]`
- **Status:** Open (new — 3 Jun 2026)
- **Description:** Replace the plain-text textareas on the Validate step's source/translation edit panes with a rich-text editor that natively handles structured HTML (paragraphs, headings h2/h3, bullet and numbered lists, bold/italic, links).

  **Why this matters:** `ToolboxTalkSection.Content` stores HTML (the AI generates structured content with bullets, headings, etc). The current "edit plain text, write HTML" round-trip is destructive — accepting a source edit flattens all structural formatting to plain paragraphs (mitigated short-term in 1.1.3 by wrapping in `<p>` elements per line and HTML-encoding for XSS prevention, but the formatting loss remains).

  Replacing the textareas with a structured editor (the same representation the reviewer sees as what's stored) is the long-term correct fix. WYSIWYG editing eliminates the strip/reconstruct round-trip and preserves structure end-to-end.

  **Library candidates:**
  - **ProseMirror** (likely best fit) — MIT-licensed, no commercial pressure, very stable long-term API, smaller bundle, used by NYT/Atlassian/Asana directly. Requires more boilerplate than higher-level wrappers.
  - **TipTap** — Wrapper around ProseMirror. MIT core; some advanced features in paid Pro tier (collaboration, AI extensions, cloud sync). Faster initial setup, less flexible long-term.
  - **Lexical (Meta)** — MIT, newer, smaller community.

  Recommendation: ProseMirror directly. The editor scope is narrow (~6 schema nodes — paragraph, h2, h3, bullet list, ordered list, bold, italic) and ProseMirror's directness pays off over the project's lifetime more than TipTap's initial-velocity benefit.

  **Files affected:**
  - `web/src/features/toolbox-talks/components/create-wizard/steps/validate/ValidationSectionCard.tsx` — replace textareas with editor component
  - Probably `web/src/features/toolbox-talks/components/create-wizard/steps/parse/SectionList.tsx` — Parse step currently has read-only display via `dangerouslySetInnerHTML`; if reviewers can edit on Validate, they should edit on Parse too. Same editor reused.
  - Backend: `TranslationValidationController.cs` `PropagateEditedSourceAsync` — remove the line-splitting-and-wrapping logic; editor outputs valid HTML directly.
  - Backend: review whether `TranslationValidationJob.StripHtml()` for AI prompting still needs to happen on the fly during re-validation, or can be reused from a stored plain-text projection.
  - PDF export / certificate rendering — verify the editor's HTML output renders correctly through QuestPDF (the existing PDF generator).

  **Considerations:**
  - **Paste sanitisation** — reviewers will paste from Word, web pages, etc. Configure schema to strip disallowed elements (Microsoft Office paste in particular injects MSO classes and inline styles that need scrubbing).
  - **Schema enforcement** — only allow the elements the AI generator actually produces. Confirm by sampling existing `ToolboxTalkSection.Content` values from the DB before defining the schema.
  - **Translation-source structural mismatch** — if a reviewer changes the _structure_ of the source (adds a bullet, removes a heading), the translation's structure may no longer match. Use the existing `NeedsRevalidation` flag (added in 1.1.3) to mark translations as stale; surface this to the reviewer in a follow-up UI prompt.
  - **Accessibility** — keyboard navigation, screen reader compatibility. ProseMirror has good defaults but worth verifying.
  - **Bundle size** — ProseMirror core is small (~50KB gzipped) but adds up with extensions; budget around 100-200KB additional.

  **Effort estimate:** 4-5 working days for a developer familiar with React (Claude Code competent at both libraries):
  - Library install + basic editor in `ValidationSectionCard.tsx`: 0.5 day
  - Schema configuration + toolbar: 0.5 day
  - Both source and translation panes: 0.25 day
  - Backend pipeline adjustments (remove `<p>`-wrapping, audit StripHtml usage): 0.5 day
  - Parse step editor: 0.5 day
  - Structure-changed → translation-stale UI surfacing: 0.5 day
  - PDF export verification, paste sanitisation tuning: 0.5 day
  - Testing across browsers and round-trip scenarios: 1 day
  - Cleanup, removing now-obsolete `<p>`-wrapping logic from `PropagateEditedSourceAsync`: 0.5 day

  **Dependencies:**
  - The 1.1.3 fix (source-edit feature itself) — must be in place. It is.
  - Schema design decision (which elements to allow) — quick pre-build investigation needed.

  **Closes:** the formatting-loss limitation disclosed in 1.1.3's amber warning. Once shipped, that warning can be removed.

---

## 1.2 Translation Behaviour & Regulatory

#### 1.2.1 Translation validation conditional on regulatory applicability

- **Priority:** PD (Product Decision Required)
- **Origin:** `[Internal-QA]`
- **Status:** Open / Blocked on decision
- **Description:** Currently translation validation runs unconditionally on every translation. For non-regulated tenants this incurs AI cost (consensus back-translation across multiple models) for limited benefit.
- **Decision needed:** (a) always-on as headline "regulatory-grade translation" value prop, (b) opt-in per translation, (c) tenant-level toggle / pricing tier, (d) smart default based on tenant having regulatory sectors.
- **Related:** `SectorKey` is user-supplied with no cross-check against tenant's assigned `TenantSector` rows — user can request validation/scoring against any sector regardless of their tenant's actual assignments. Worth tightening in same work.

#### 1.2.2 Surface regulatory applicability to user during translation

- **Priority:** P2
- **Origin:** `[Internal-QA]`
- **Status:** Open
- **Description:** Today the system silently processes whatever sector key is supplied. Users have no visibility into whether their content will actually be validated against regulatory documents.
- **Fix direction:** (a) When tenant sector has regulatory profile but no Approved requirements loaded, show warning at translation time — "regulatory validation will be skipped" — let user proceed or cancel. (b) When tenant sector has no regulatory profile, run validation as today (language/quality/glossary), no warning. (c) On results screen, distinguish "regulatory validation passed" from "regulatory validation not attempted".

#### 1.2.3 Translation performance — section vs subtitle pipeline disparity

- **Priority:** P3
- **Origin:** `[Internal-QA]`
- **Status:** Open
- **Description:** Section translation (consensus validation pipeline) meaningfully slower than subtitle translation (lighter pipeline). E.g. 0/5 sections complete while subtitles reach 30/84. Not a bug — but unmeasured. Worth instrumenting per-stage timings (consensus back-translation rounds, lexical scoring, safety classification, glossary loading, persistence) to know where time goes.

#### 1.2.4 Source language detection audit

- **Priority:** P3
- **Origin:** `[Engineering]`
- **Status:** Open
- **Description:** Audit how source language is currently detected/inferred across the pipeline. Related to UAT 1.1.2 (hardcoded "en" causing EN→EN runs).

#### 1.2.5 Translation pass threshold — plain language

- **Priority:** P3
- **Origin:** `[Internal-QA]`
- **Status:** Open
- **Description:** The "pass threshold" concept needs clearer in-product explanation. Users don't know what 80% means.

#### 1.2.6 Translation quality phase 2 — glossary maintenance UI

- **Priority:** P2
- **Origin:** `[Roadmap]`
- **Status:** Open
- **Description:** Tenant admin UI for managing tenant-level glossary terms. Currently glossaries are managed at the DB / system seed level. Tenants can't add their own corrections.

#### 1.2.7 YouTube caption integration

- **Priority:** P3
- **Origin:** `[Roadmap]`
- **Status:** Open
- **Description:** Pull existing YouTube captions instead of regenerating transcripts for YouTube-sourced videos.

#### 1.2.8 Mixed voice in parse log

- **Priority:** P3
- **Origin:** `[Internal-QA]`
- **Status:** Open
- **Description:** Parse log alternates between system/AI/user voice inconsistently. Polish.

#### 1.2.9 Cross-section remediation

- **Priority:** P2
- **Origin:** `[Roadmap]`
- **Status:** Open
- **Description:** Apply a glossary correction or wording change across multiple sections at once instead of per-section.

#### 1.2.10 Iteration guard

- **Priority:** P2
- **Origin:** `[Engineering]`
- **Status:** Open
- **Description:** Guard against infinite or runaway iteration in the translation/validation pipeline. Specifics to be reviewed.

#### 1.2.11 AI quiz gen on edit

- **Priority:** P3
- **Origin:** `[Roadmap]`
- **Status:** Open
- **Description:** When admin edits a section after parse, regenerate the quiz questions tied to that section. Currently quiz is one-shot at creation.

#### 1.2.12 Translation generate API: accept language codes instead of names

- **Priority:** P3
- **Origin:** `[Engineering]`
- **Status:** Open (new — surfaced during Phase 3c.3, 9 Jun 2026)
- **Description:** The `POST /toolbox-talks/{id}/translations/generate` endpoint accepts a `languages: string[]` payload where the strings are language _names_ ("Spanish", "Latvian") rather than codes ("es", "lv"). The new Phase 3c per-language panel inherited this convention from the deleted `ContentTranslationPanel` to preserve production behaviour, but names are fragile: any localisation, capitalisation, or labelling change to the language list would break the resolver silently.
- **Fix direction:** Three layers, in this order:
  - Backend: locate the controller action handling `/translations/generate` (likely in `ToolboxTalksController.cs` since the search for handlers under `Application/Commands/` came up empty). Switch language resolution from name lookup to code lookup against the language reference table.
  - Frontend: change the panel and any other caller of `generateContentTranslations` to pass language codes instead of names. The new panel currently passes `row.languageName`; after the refactor it would pass `row.languageCode`.
  - Tests: backend integration tests for generate-translations currently seed name-based requests; rewrite to use codes.
- **Risk:** Identify every caller of the generate endpoint before changing the contract. If it's only the panel (and the wizard's equivalent step), straightforward. If external callers exist, breaking change.
- **Estimate:** Half a day if scope is panel + wizard. Up to a day if other callers exist.

#### 1.2.13 Workflow event history — render structured payloads

- **Priority:** P3
- **Origin:** `[Engineering]`
- **Status:** Open (new — surfaced during Phase 3c.5, 9 Jun 2026)
- **Description:** The Phase 3c.5 history modal renders event type, triggered-by source (User/System), and timestamp for each `WorkflowEventDto`. The DTO also carries a `PayloadJson` string with event-specific data — for example, `ExternalReviewInitiated` carries the invited email and expiry, and per-section events likely carry the section index. 3c.5 deferred payload rendering because no documented inventory of payload shapes per event type exists.
- **Fix direction:**
  - Recon: catalogue every event type the backend writes, and document the `PayloadJson` shape for each. Likely lives in the workflow service implementation.
  - Define per-event-type render functions on the frontend, similar to the WorkflowStateBadge mapping table.
  - Update WorkflowHistoryModal to render structured payload under each event entry when a renderer exists. Events with no renderer continue to show only the high-level summary.
- **Estimate:** Half a day if event types are well-documented; day if recon is needed.

#### 1.2.14 Workflow event history — show triggered-by user name

- **Priority:** P3
- **Origin:** `[Engineering]`
- **Status:** Open (new — surfaced during Phase 3c.5, 9 Jun 2026)
- **Description:** `WorkflowEventDto` carries `TriggeredByUserId` (nullable Guid) but no user name. The Phase 3c.5 history modal shows "by User" or "by System" without resolving the user's display name. For a reviewer auditing who accepted a translation last Tuesday, "by User" is not actionable.
- **Fix direction:**
  - Backend: extend `WorkflowEventDto` with `TriggeredByUserName` (nullable string), populated in the `GetHistory` implementation via a join to the user table on `TriggeredByUserId`. For System events the field stays null.
  - Frontend: update the modal's triggered-by line to "by {userName}" when present, falling back to "by User" when the type is User but the name is missing (defensive).
- **Estimate:** Half a day, mostly backend.

#### 1.2.15 Design review: Auditor audience role on ContentCreationSession

- **Priority:** PD (Product Decision Required)
- **Origin:** `[Internal-QA]`
- **Status:** Open — design review required before further AudienceRole development

Added during Ryan's UAT review as an AudienceRole value alongside Operator and Supervisor on quiz generation. Implementation generates different questions per audience string.

Problem: Auditor is not an identity role in the system (production has SuperUser/Admin/Supervisor/Operator). There is no mechanism to assign a quiz to an Auditor user because no such user can exist. The "generate questions for Auditor audience" code path has no consumer — the questions cannot be taken.

Two separable intents are conflated in the current design:
(a) Learner-facing quiz tone varies by identity role (Operator vs Supervisor — both are real users who take quizzes).
(b) Auditor-facing demonstration content — showing an external auditor "this is the kind of question we can generate" as evidence of training rigour, not as a quiz to be taken.

These are different features with different storage, different UI, and different access patterns. Forcing them into a single AudienceRole string was a misread of the UAT feedback.

Action: design review before more code is built on top of AudienceRole. Decide whether (a) and (b) are both in scope, drop the other, or rescope. Until then, no new features should branch on AudienceRole == "Auditor".

Related: see `ContentCreationSession.cs` (AudienceRole property), `QuizGenerationPrompts.cs` (audience text variations), any frontend dropdown surfacing AudienceRole.

---

## 1.3 Wizard / Create Content UX

#### 1.3.1 Clickable wizard progress

- **Priority:** P2
- **Origin:** `[Internal-QA]`
- **Status:** Open
- **Description:** Make wizard step indicators clickable for navigation between completed steps.

#### 1.3.2 Wizard step descriptions hover

- **Priority:** P3
- **Origin:** `[Internal-QA]`
- **Status:** Open
- **Description:** Tooltip / hover descriptions on wizard step names so users know what each step does.

#### 1.3.3 Drag-to-reorder discoverability

- **Priority:** P3
- **Origin:** `[Internal-QA]`
- **Status:** Open
- **Description:** Section reordering is drag-and-drop but not visually signalled. Users don't discover it.

#### 1.3.4 Passing score slider

- **Priority:** P3
- **Origin:** `[Roadmap]`
- **Status:** Open
- **Description:** Visual slider for passing-score selection instead of numeric input.

#### 1.3.5 Long-running job UX — fire-and-notify

- **Priority:** P2
- **Origin:** `[Engineering]`
- **Status:** Open
- **Description:** Replace polling-based progress UI for bulk import, content generation, validation, corpus runs with a fire-and-notify pattern (e.g. SignalR + notification). Improves UX (less network chatter) and lets users navigate away during long jobs.

  **Phase 3c.3 instance (9 Jun 2026):** the per-language panel's Validate action fires `POST /validation/validate` and toasts "Validation started for {language}", but the panel does not subscribe to SignalR `ValidationComplete` events. The state badge does not update until React Query refetches `workflow-state` on the next user action that invalidates that key. From the user's perspective, validation appears to do nothing until they reload. The proper fix is to subscribe to the existing validation hub (`useValidationHub` pattern in the wizard) from the panel and invalidate `workflow-state` on `ValidationComplete`. Wire-up effort: small. Deferred from 3c.3 to keep scope tight per the scope-drift learning in LEARNING_LIFECYCLE.md §8.12.

#### 1.3.6 Translations as background task

- **Priority:** P3
- **Origin:** `[Engineering]`
- **Status:** Open (may be partially done — verify)
- **Description:** Confirm translation step in create wizard runs as Hangfire background task with proper SignalR updates. Already partially in place; verify completeness.

#### 1.3.7 Content creation end-to-end tests

- **Priority:** P2
- **Origin:** `[Engineering]`
- **Status:** Open
- **Description:** No automated E2E tests covering the full content creation wizard. Every change risks regression that only surfaces in manual testing.

#### 1.3.8 File upload size display rounds small files to "0.0 MB"

- **Priority:** P3
- **Origin:** `[Internal-QA]`
- **Status:** Open (new — 2 Jun 2026)
- **Description:** File upload component displays size in MB to one decimal. Files smaller than ~50KB show as "0.0 MB" which reads as "no file uploaded".
- **Fix direction:** Switch to KB for files < 1MB, MB for files ≥ 1MB. Simple formatting fix in the upload component.

---

# 2. QR Workstations & PINs

## 2.1 QR Code Management

#### 2.1.1 QR codes have no edit action

- **Priority:** P2
- **Origin:** `[Internal-QA]`
- **Status:** Open
- **Description:** QR codes can only be toggled active or deleted — no edit. Any change requires delete + recreate, which generates a new `CodeToken` and invalidates any printed code already deployed. Particularly painful for tenants who've printed and stuck codes at workstations.
- **Fix direction:** Add edit dialog. `CodeToken` immutable (preserves printed codes). Display name, ContentMode, assigned talk/course editable. Optionally clarify in UI that changing the assigned content updates what people see _without_ requiring a reprint.

#### 2.1.2 Language list consolidation

- **Priority:** P3
- **Origin:** `[Engineering]`
- **Status:** Open
- **Description:** Codebase has three divergent language lists: DB lookup (33 languages, authoritative), glossary section constant (9 hardcoded), and previously a QR page constant (10, now removed). Glossary section should consolidate to DB lookup for single source of truth.

## 2.2 Assets (NEW FEATURE)

#### 2.2.1 Asset Management — workstation training for equipment / vehicles / fixed assets

- **Priority:** P1
- **Origin:** `[Boss]` `[Roadmap]`
- **Status:** Open — design partially complete
- **Description:**

A new tenant-scoped entity `Asset` representing a piece of equipment, vehicle, or fixed object that training can be associated with — analogous to an Employee but for _things_. Used to deliver and track training tied to specific equipment rather than specific people.

**Use case:** A hire company has a digger (`DIG-042`). They create learnings for operating the digger and changing the bucket. They print a QR code that points to a "Digger Operator Induction" course and assign asset PIN `DIG-042` (their existing internal ID). A new hirer scans, enters `DIG-042`, completes training in their preferred language, and the completion is recorded against the asset with a timestamp and (where possible) geo-location. The hire company knows from their own records who was hiring the digger at that time.

**Entity shape — mirrors Employee with deletions:**

Kept from Employee:

- Tenant-scoped, soft-delete with audit (`IsDeleted`, `DeletedBy`, audit trail)
- Identifier / Code (auto-generated, like employee code)
- Name, Description
- PIN (hashed for verification, plaintext for admin view per the existing PIN-display decision)
- Multi-language support (translated content delivered via the asset's QR)
- Active / inactive status
- Notes
- Optional category / taxonomy (e.g. Vehicle / Tool / Machine / Workstation)
- Optional Site (physical location)
- Standard audit fields

Removed from Employee:

- No `Email` — no inbox
- No invitation email / PIN-reset email
- No `PreferredLanguage` (single) — assets _support_ multiple languages but don't have a personal preference
- No `Roles`, no `User` linkage, no login
- No `JobTitle`, no `Department` (or repurposed — see open question below)
- No `Mobile`, no `Phone`, no `StartDate`/`EndDate` in employment sense (replaced with acquisition / decommission dates if needed)

**PIN behaviour:**

- Tenant admin can either auto-generate (like employee PIN) OR set manually (e.g. to match the asset's internal ID `DIG-042`).
- PIN unique within tenant; can duplicate across tenants (same as employee PIN scope).
- Hashed for verification; plaintext stored for admin view (same security trade-off as employee PIN, per CLAUDE.md decision).
- Lockout policy: same as employee (5 failed → 15-min lockout).

**QR code targeting:**

- QR codes can target an asset (talk or course) in addition to the existing employee-targeted QR codes.
- New ContentMode or new target enum (`TargetType: Employee | Asset`).
- Backend: `QrCode` entity gets an optional `AssetId` alongside the existing `ToolboxTalkId`/`CourseId`.

**Tracking model (configurable per asset):**

- **Default — anonymous to asset:** Completion recorded against the asset only. "Someone completed training on DIG-042 at 10:30am on 2026-05-28 at [geo-location if available]." The tenant knows from their hire records who was hiring DIG-042 at that time.
- **Optional — asset + employee:** Asset PIN identifies the asset; then the scanner enters an employee PIN to identify themselves. Both recorded: "Employee E completed training on DIG-042." Heavier flow; provides individual accountability.
- Per-asset toggle: `RequireEmployeeIdentification: bool`.

**Geo-tagging:**

- On QR scan, capture browser geolocation (with user permission) and store on the session.
- Surfaces in completion reports as "completed at [lat, lng]" or reverse-geocoded as a place name.
- Useful for hire/field-equipment use cases — the asset moves around, so location matters.
- Requires HTTPS (already in place); browser permission UX; fallback when permission denied.

**Tenant-level toggle:**

- New tenant setting `AssetManagementEnabled` (mirrors `QrLocationTrainingEnabled`).
- When off: Asset menu items, asset QR target option, and asset-related UI all hidden.
- Default off — tenants opt in.

**UI surface:**

- Main admin nav: new "Assets" section (visible when toggle on), structured like Employees.
- Asset list / detail / create / edit pages.
- QR code creation gets a new target type selector ("Employee" or "Asset"); existing flow unchanged when Asset Management disabled.
- Completion reporting filterable by asset.

**Open questions before build:**

- **Taxonomy.** How rich should asset categorisation be? Free-text category + free-text site? Or a lookup-based taxonomy (Vehicle/Tool/Machine/Workstation)? Probably configurable like employee Department.
- **Asset → multiple tenants?** Could a tenant share assets across sub-tenants in a multi-tenant hierarchy? Out of scope for v1; flag for future.
- **Asset → schedule.** Can a learning be _scheduled_ on an asset (analogous to `ScheduledTalk` on employees)? Or are assets only ever scanned-into-via-QR? v1: scan-only. Scheduling can come later.
- **Reporting surface.** What does "training history for DIG-042" look like? List of completion sessions with timestamp, location, employee (if identified), score. Probably a copy of employee training history view.
- **Asset bulk import.** Same shape as employee bulk import? Likely yes, but separate CSV format and validation.

**Build effort estimate:** Substantial. Near-total copy of Employee + Site + PIN infrastructure. Realistically a multi-week feature done properly. Worth approaching as its own dedicated sprint.

**Dependencies:**

- The existing PIN infrastructure (hash + plaintext storage)
- Existing QR code / scan flow
- Tenant settings infrastructure
- Site / Location entities (existing)

#### 2.2.2 Auto-assign visual grouping

- **Priority:** P3
- **Origin:** `[Internal-QA]`
- **Status:** Open
- **Description:** When auto-assigning learnings to employees (or, in future, assets), visually group by criteria. UI improvement, not buildable until auto-assign matures.

---

# 3. Employee & User Management

#### 3.1 Unify user creation on throwaway-password + invitation-email flow

- **Priority:** P1
- **Origin:** `[Engineering]` `[Production-incident]`
- **Status:** ✅ Done (5 Jun 2026) — Three user-creation paths unified on bulk-import pattern; password fields removed from UI user-create form; all paths now issue throwaway password + invitation email. Commit `bb2709e` → merged to main via `5d12808`.
- **Description:** Currently three paths create users with inconsistent behaviour:
  - **UI user-create** (`POST /api/users`): admin sets password directly; until this session, never sent any email. Now sends a "account created" notification email but still admin-sets-password.
  - **Tenant-create**: Contact Name + Email becomes the Admin user, `EmailConfirmed=true`. Inconsistent with how regular users are created.
  - **Bulk import** (reference correct): throwaway-password + invitation-email pattern. User clicks link in email, sets their own password.
- **Fix direction:** Unify on the bulk import model. Remove password fields from UI user-create form. Generate password-reset token on creation, email setup link, set `EmailConfirmed=false` until completion. Same for tenant-create's Admin user. Estimated 2-3 hours done properly.
- **Closes:** The "welcome email" gap fixed temporarily in this session; the bulk import partial-row recovery issue.

#### 3.2 Bulk import partial-row recovery

- **Priority:** P2
- **Origin:** `[Engineering]`
- **Status:** Open
- **Description:** When bulk import is interrupted, an employee can be created without a linked user account. On re-run, the row classifies as `AlreadyExisted` but the user account never gets created. Should reuse `EmployeeService`'s linked-user creation properly.

#### 3.3 SuperUser lockdown — Admin role assignment

- **Priority:** P3
- **Origin:** `[Engineering]`
- **Status:** Open
- **Description:** SuperUser lockdown (rejecting any role list containing SuperUser) may be too strict on Admin role assignment. Currently requires a DB workaround in some cases. Review the exact rule.

#### 3.4 Employee training audit & reporting

- **Priority:** P2
- **Origin:** `[Roadmap]`
- **Status:** Open
- **Description:** Comprehensive reporting view per employee: all training, completion history, scores, certificates, gaps. Currently partially available; needs consolidation.

#### 3.5 Training Evidence Pack — extend to remaining sectors

- **Priority:** P2
- **Origin:** `[Roadmap]`
- **Status:** Open
- **Description:** Training Evidence Pack feature currently covers a subset of sectors. Extend to remaining sectors.

#### 3.6 My Learnings no-op fix

- **Priority:** P3
- **Origin:** `[Internal-QA]`
- **Status:** Open
- **Description:** Something in "My Learnings" view is a no-op or broken. Needs investigation to specify.

#### 3.7 Active Learnings count clickable

- **Priority:** P3
- **Origin:** `[Internal-QA]`
- **Status:** Open
- **Description:** Active Learnings count on dashboard should be clickable to drill into the list.

#### 3.8 Auto reschedule testing

- **Priority:** P2
- **Origin:** `[Engineering]`
- **Status:** Open
- **Description:** Test coverage for auto-reschedule of recurring talks is thin. Behaviour around DST, missed cycles, and employee status changes (active/inactive) needs verification.

#### 3.9 2FA

- **Priority:** P2
- **Origin:** `[Roadmap]`
- **Status:** Open
- **Description:** Two-factor authentication for users. Standard feature for an enterprise product.

#### 3.10 Email send logging fix

- **Priority:** P3
- **Origin:** `[Production-incident]`
- **Status:** Open
- **Description:** `UserService` logs "Sent account creation email" even when MailerSend returned 422 (trial limit). The success log line shouldn't fire if the provider call failed. Caused real-world confusion this session — logs showed "Sent" while delivery was failing.

#### **3.11 (new)** — Tenant creation: 400 "already exists" returned on first submit

- **Priority:** P1
- **Origin:** `[Internal-QA]`
- **Status:** ✅ Done — 2026-06-17 — Frontend race window closed via `form.formState.isSubmitting` (React Hook Form's synchronous internal-ref guard), preventing the double-submit pattern that caused first-submit 400s. Trimming added to user-input string fields on both frontend payload construction and backend duplicate-check / entity creation. Backend defensive catch added for EF unique-constraint violations (concurrent-tab scenarios where two browser sessions race to create the same tenant name): returns the clean "already exists" message instead of a generic EF exception. Recon: `docs/phase-5/reports/3.11-tenant-creation-400-recon.md`. Fix: `docs/phase-5/reports/3.11-tenant-creation-400-fix.md`. Test coverage: deferred — Playwright will provide canonical coverage for UI-level race conditions.
- **Description:** Submitting the Create Tenant form once produces an HTTP 400 with "A tenant with this name already exists", but the tenant row is actually created. Possible double-submit (form fires POST twice; first creates, second hits uniqueness check) or misleading error from post-commit exception during admin-user creation. Predates 3.1. Repro: submit Create Tenant form with a fresh unique name, observe 400 in browser network tab, query DB to confirm row exists.

#### **3.12 (new)** — New user activation timing question

- **Priority:** PD (Product Decision Required)
- **Origin:** `[Internal-QA]`
- **Status:** Open
- **Description:** New users are created with `IsActive = true` despite not being able to log in until they complete the invitation flow (set password). Product question: should `IsActive` reflect "account fully set up" or "admin has approved this account"? Needs explicit decision.

#### **3.13 (new)** — Testing discipline: use separate browser profiles for admin and end-user flows

- **Priority:** P3
- **Origin:** `[Engineering]`
- **Status:** Open
- **Description:** Admin should be tested in a separate browser profile from end-user verification flows. Same-origin localStorage means token state from a user-login can clobber an admin's token state silently. Caused a 403 cascade during 3.1 verification on 5 June. Documentation/discipline note rather than code change — not operationalized as a hook or checklist yet.

---

# 4. Tenant Management & Regulatory

#### 4.1 Tenant `IsActive` / sector remove for tenant admins

- **Priority:** P3
- **Origin:** `[Engineering]`
- **Status:** Open
- **Description:** Currently My Sectors is add-only for tenant admins. Removing a sector requires SuperUser. Add the ability for tenant admins to remove sectors from their own tenant.

#### 4.2 Sector preset quick-add

- **Priority:** P3
- **Origin:** `[Roadmap]`
- **Status:** Open
- **Description:** Pre-configured sector bundles (e.g. "Food & Hospitality" auto-adds typical sub-sectors) for faster tenant setup.

#### 4.3 Dialect detection UI

- **Priority:** P3
- **Origin:** `[Roadmap]`
- **Status:** Open
- **Description:** UI for detecting and selecting dialects within a language (e.g. European vs Latin American Portuguese). Relevant for content translation accuracy.

---

# 5. Infrastructure & Tooling

#### 5.1 Tenant-filter sweep audit

- **Priority:** P1
- **Origin:** `[Production-incident]` `[Engineering]`
- **Status:** ✅ Done (3 Jun 2026) — Forensic audit completed: 6 anonymous endpoints + 18 Hangfire jobs + ~120 queries inspected; 2 issues found (AuthService tenant-filter bypass, ContentExtractionService interface-enqueue) and fixed.
- **Description:** Three production bugs from the same root cause have surfaced (bulk import, subtitle processing job, subtitle content fetch from QR). All silent — query returns nothing because `ICurrentUserService.TenantId` returns `Guid.Empty` in Hangfire jobs and `[AllowAnonymous]` endpoints. Each instance found by user-observed symptom, not by testing. There are very likely more.
- **Fix direction:** Systematic audit: every query against a tenant-filtered entity that runs in a Hangfire job OR an unauthenticated endpoint must either use `IgnoreQueryFilters()` with explicit predicates, OR take an explicit `bypassTenantFilter` parameter. Grep all `[AllowAnonymous]` controllers and all Hangfire job classes; cross-reference every DB query inside them.
- **Closes:** Prevents the fourth instance.

#### 5.2 Migration creation process & Designer.cs guard

- **Priority:** P1 (escalated from P2 — 2 Jun 2026)
- **Origin:** `[Production-incident]` `[Engineering]`
- **Status:** ✅ Done (3 Jun 2026) — Build-time guard implemented: two `[Fact]` tests in `MigrationStructureTests.cs` verify every migration has a matching Designer.cs with a valid `[Migration]` attribute; CLAUDE.md Note 28 added enforcing CLI-only migration creation.
- **Description:** **Four documented instances** of missing Designer.cs files causing silent migration skips this session alone (one production bug `AddGlossaryCorrectionsToTranslationValidationResult`, plus three drift cases discovered while investigating, plus one new migration `AddAudienceRoleToContentCreationSession` that broke Development on push).
  Root cause confirmed: migrations have been routinely **hand-written** rather than generated via `dotnet ef migrations add`. The CLI generates both the `.cs` AND `.Designer.cs` files together; hand-writing the `.cs` alone results in EF silently skipping the migration on every startup because the `[Migration]` attribute lives on the Designer.cs partial class declaration.
- **Fix direction (two parts):**
  - **Process change:** all migrations must be created via `dotnet ef migrations add <Name>`, never hand-written. Add explicit directive to CLAUDE.md so Claude Code follows it on every future schema change prompt.
  - **Build-time guard:** check that every migration `.cs` file has a matching `.Designer.cs`. Fail build if mismatched. Catches both accidental hand-writing and future deletions.
- **Closes:** Prevents recurrence of the silent-skip pattern that's cost ~3 hours of debugging this week.

#### 5.3 Migration forensic audit — find any remaining missing Designer.cs files

- **Priority:** P2
- **Origin:** `[Engineering]`
- **Status:** ✅ Done (3 Jun 2026) — All 90 migrations confirmed structurally sound; 5 synthetic-timestamp migrations identified but functional; no additional silent-skip risks found.
- **Description:** Given the discovery that the codebase routinely hand-writes migrations, there may be other Designer.cs files missing besides the four already found and fixed. Some may have happened to work for unrelated reasons (`[Migration]` attribute on the wrong file, applied long ago with history intact, etc) but represent latent drift.
- **Fix direction:** One-pass forensic scan of `src/Core/QuantumBuild.Core.Infrastructure/Migrations/` reporting:
  - Every migration `.cs` without a matching `.Designer.cs`
  - Every migration `.cs` with inline `[Migration(...)]` attribute (suggests hand-writing)
  - Every migration with a synthetic timestamp (e.g. `*_000001` pattern) vs real timestamps
  - Whether any of the above will cause issues on a fresh-environment migration replay
- **Acceptance:** Confidence that no more silent-skip surprises lurk.

#### 5.4 Development DB drift sweep

- **Priority:** P2
- **Origin:** `[Production-incident]`
- **Status:** Open
- **Description:** Multiple drift cases found this session — schema present without history rows (`GlossaryCorrectionsJson`, the three QR-related migrations on both Dev and Prod). Other drift may exist.
- **Fix direction:** One-pass comparison of every migration's expected schema vs actual schema on all environments (Development, Production, and Demo once it's brought up). Identify drift, decide fix per case. Consider a startup verification routine that warns on detected drift (informational, not blocking).

#### 5.5 R2 orphan file cleanup nightly job

- **Priority:** P2
- **Origin:** `[Engineering]`
- **Status:** Open
- **Description:** Files uploaded to R2 that are no longer referenced by any DB record accumulate over time. Need a nightly Hangfire job to identify and delete orphans.

#### 5.6 MailerSendEmailProvider 429 handling

- **Priority:** P2
- **Origin:** `[Engineering]`
- **Status:** Open
- **Description:** Currently silently drops on 429 (rate limit). Should retry with backoff, log clearly, alert if persistent.

#### 5.7 Demo environment refresh and three-tier promotion workflow

- **Priority:** P1
- **Origin:** `[Engineering]` `[Boss]`
- **Status:** Open (new — 2 Jun 2026)
- **Description:** Bring the Demo Railway environment from its disconnected/frozen state to a fully working, auto-deploying instance on a dedicated `demo` branch. Establish ongoing three-tier workflow: Development for build/test, Demo for business sign-off and prospect demos, Production for customer release.

**Pre-requisite decision required before starting:**

- R2 bucket sharing — Option A: shared with Dev/Prod (zero setup, demo media available immediately, tenant-folder isolation only partially mitigates pollution risk). Option B: separate Demo bucket (clean isolation, but QR images / subtitles / certificates all missing until regenerated). Recommendation: **Option A** for initial bring-up, promote to isolated buckets before external customer demo sessions.
- 5.31 must ship before Demo is brought up, or Demo will be created with the seeded literal credentials.

**Missing environment variables (organised by block, see detailed spec in session notes):**

- Block 1: Database (1 demo-specific var)
- Block 2: JWT (1 demo-specific, 2 copied from Dev)
- Block 3: CORS (1-2 demo-specific)
- Block 4: TranslationValidation (12 keys, all known values copied from Dev)
- Block 5: Email (Provider=Stub initially to prevent real emails)
- Block 6: R2 / Cloudflare Storage (7 keys copied from Dev)
- Block 7: External APIs (4 keys copied from Dev)
- Block 8: App Settings (2 demo-specific)
- Block 9: Needs research (`BulkImport__InvitationEmailDelayMs`, `SubtitleProcessing__ElevenLabs__Model`)

**Ordered task list — 20 tasks across 6 phases:**

_Phase 0 — Preparation:_

1. Create `demo` branch off `main`, immediately merge `transval` (main is behind by Designer.cs restoration commits)
2. Collect env var values from Development Railway service
3. Generate fresh JWT secret for Demo
4. Provision Demo PostgreSQL database on Railway

_Phase 1 — Railway Reconnect:_ 5. Reconnect Demo API service to GitHub (branch: `demo`) 6. Reconnect Demo web service to GitHub 7. Set all env vars on Demo API 8. Set `NEXT_PUBLIC_API_URL` on Demo web service 9. Verify CORS origin is exact match (no trailing slash)

_Phase 2 — Database Migration:_ 10. Run migrations locally against Demo DB via `dotnet ef database update` 11. Confirm migration applied cleanly (top entry should be `AddPreserveSourceWordingToContentCreationSession`)

_Phase 3 — First Deploy:_ 12. Trigger first manual deploy of Demo API; watch Railway logs for: migrations (0 pending, already applied), seeder (creates System tenant + SuperUser + roles + permissions), Hangfire init 13. Deploy Demo web service

_Phase 4 — Demo Tenant and Test Data:_ 14. Create Demo tenant ("CertifiedIQ Demo"); note UUID (R2 folder prefix) 15. Create Demo Admin user under Demo tenant 16. Assign at least one sector to Demo tenant 17. Create one demo toolbox talk for smoke testing

_Phase 5 — Smoke Test and Handoff:_ 18. Execute smoke test checklist (15 items: login, token refresh, user creation, talk creation, employee completion, CORS, TransVal, QR setup, QR scan, Skills Matrix, bulk import, certificates, Help Assistant, R2 isolation) 19. Re-test User Creation page 20. Update CLAUDE.md deployment section with three-tier workflow

**Migration risk:** All migrations apply fresh on empty DB — lowest-risk scenario possible. The four Designer.cs restorations are now in place on `transval`/`main`, so a `demo` branch off `main` (with `transval` merged in) will have them.

**Ongoing workflow post-setup:**

- Daily development: `transval` → Railway auto-deploys Development
- Promote to Production: `git checkout main && git merge transval && git push company main && git push origin main`
- Promote to Demo: `git checkout demo && git merge transval && git push company demo && git push origin demo` — run this before any scheduled customer demo session
- Never commit directly to `demo`
- Keep `origin` and `company` in sync on all three branches

#### 5.8 Next.js 15+ params shape

- **Priority:** P3
- **Origin:** `[Engineering]`
- **Status:** Open
- **Description:** Synchronous `params` access in redirect stubs. Not breaking yet but Next.js is moving toward async params. Will need updating before a future Next.js upgrade.

#### 5.9 AI Chat Assistant — UI Help / Data Q&A

- **Priority:** P3
- **Origin:** `[Roadmap]`
- **Status:** Open
- **Description:** An AI-powered help assistant for admins — "how do I create a course", "show me employees in site X" — embedded in the product. Forward-looking.

#### 5.13 English-only learning creation blocked — Step 1 rejects empty target languages (§5.19)

- **Priority:** P1
- **Origin:** `[Internal-QA]`
- **Status:** ✅ Done — 2026-06-14 — fixed in wizard-skip-regression chunk; smoke verified post-deploy (see `docs/phase-5/reports/wizard-skip-regression-fix.md` Scenarios A and B).
- **Description:** The new wizard's Step 1 (Input & Config) rejects submissions where `targetLanguageCodes` is empty, blocking English-only learning creation. Additionally Steps 5 (Translate) and 6 (Validate) do not skip when no target languages are configured — they remain reachable even when there is nothing to translate or validate. Root cause: the backend `InitialiseToolboxTalkCommandValidator` enforces "At least one target language is required"; the frontend `stepOrder.ts` reachability rules for steps 5 and 6 gate on `talk.sections.length > 0` rather than on `targetLanguageCodes.length > 0`. Fix direction: (1) Remove the target-language validator rule. (2) Update step 5 and 6 reachability rules to return false when `targetLanguageCodes` is empty. (3) Confirm Continue-button navigation skips unreachable steps correctly. Recon: `docs/phase-5/reports/wizard-skip-regression-recon.md`.

#### 5.14 Quiz-skipped declared but Continue lands on Quiz step (§23)

- **Priority:** P1
- **Origin:** `[Internal-QA]`
- **Status:** ✅ Done — 2026-06-14 — fixed in wizard-skip-regression chunk; smoke verified post-deploy (see `docs/phase-5/reports/wizard-skip-regression-fix.md` Scenarios A and B).
- **Description:** When "include quiz" is deselected at Step 1, the wizard's step indicator correctly renders "3 Quiz — Skipped" on the Parse step, but clicking Continue on Step 2 (Parse) navigates to Step 3 (Quiz) instead of jumping past it to Step 4 (Settings). The display logic and the navigation logic are inconsistent — `isStepReachable` and the Continue-button next-step computation appear to use different signals. Root cause hypothesis: the step indicator reads from `isStepReachable(3, talk)` which correctly reflects the quiz-disabled flag; the Continue navigation calls `goToStep(currentStep + 1)` (integer increment) rather than `findNextReachable(currentStep)`. Fix direction: Make Continue use the same reachability logic the step indicator uses. If §5.13 and §5.14 share the same navigation root cause, a single fix chunk closes both. Recon: `docs/phase-5/reports/wizard-skip-regression-recon.md`.

#### 5.15 Integration test suite — comprehensive review post-Phase 5

- **Priority:** P2
- **Origin:** `[Engineering]`
- **Status:** Open — deferred to post-Phase 5

The deprecated test user cleanup that the previous version of this entry scoped was completed in two commits before Phase 5.2:

- `<insert hash 1>` — test(cleanup): remove deprecated test users and migrate to Operator
- `<insert hash 2>` — test(cleanup): delete misleading and orphaned tests

End state: 397 integration tests passing, zero role-not-found warnings, deprecated `TestUserType` values / `IntegrationTestBase` client properties / `TestTenantConstants` entries / orphaned playwright fixtures all removed. Three misleading tests (`AllAuthenticatedUsers_CanAccessEmployeesList`, `OnlyManagePermission_CanModifyEmployees`, `AllUsersInTenant_SeesSameEmployeeList`) deleted rather than rewritten.

The remaining drift in the test suite is deferred to a dedicated post-Phase-5 review task. The test suite is too important to be repaired piecemeal between Phase 5 feature chunks. Phase 5 will not add tests to it beyond what strictly verifies non-obvious new behaviour (per `PHASE_5_STANDARDS` §11). The comprehensive review happens once Phase 5 closes and gets the time and attention it warrants.

Scope when picked up:

- Per-test triage of every integration test: still meaningful as written, misleading and rewriteable, or delete as obsolete
- Playwright fixture audit (which fixtures are still active, which describe blocks should be unskipped or deleted)
- Frontend test coverage decision: extend, leave sparse, or deliberately scope out
- The seeder/JWT reconciliation (§12)
- The `login.spec.ts` skipped block (3 tests remain in `test.describe.skip` after the pre-Phase-5 cleanup; review whether to delete the block or rewrite the tests)
- E2E suite breadth — what's covered, what's missing, what's stale

#### 5.16 SignalR client timeout defaults missing in four hooks

- **Priority:** P1
- **Origin:** `[Engineering]`
- **Status:** ✅ Done — 2026-06-15 — `serverTimeoutInMilliseconds = 120_000` and `keepAliveIntervalInMilliseconds = 10_000` applied to all four hooks: `use-subtitle-hub.ts`, `use-corpus-run-hub.ts`, `use-lesson-parser-hub.ts`, and `use-subtitle-processing.ts` — six build sites total (initial connection and manual reconnect paths where applicable). TypeScript compile clean; vitest 9/9 passing. Fix: `docs/phase-5/reports/5.16-signalr-timeouts.md`. Recon: `docs/phase-5/reports/5.16-signalr-timeouts-recon.md`.
- **Description:** `use-subtitle-hub.ts`, `use-corpus-run-hub.ts`, `use-subtitle-processing.ts`, and `use-lesson-parser-hub.ts` all build `HubConnection` instances without setting `serverTimeoutInMilliseconds` / `keepAliveIntervalInMilliseconds`. Exposed to the same Railway proxy idle-timeout drop (1006 close) fixed in the validation hub (chunk 5.4-signalr-timeout-fix). Fix is a two-line patch per hook (`serverTimeoutInMilliseconds = 120_000`, `keepAliveIntervalInMilliseconds = 10_000` after `.build()`). Recommend a single dedicated chunk covering all four.

#### 5.17 First-language row state lag in Step 5 Translate under Start All

- **Priority:** P1
- **Origin:** `[Internal-QA]`
- **Status:** ✅ Done — 2026-06-15 — fixed in §5.17 chunk. `TranslationValidationJob` now calls `StartValidation` immediately after Phase A (TotalSections save), before Phase B (section validation loop), keeping the language in `Validating` state (which is in `ACTIVE_STATES`) for Phase B's duration. `WorkflowSubscriber` stays mounted; `ValidationComplete` events are received and trigger correct UI refetch. Root-cause recon in `docs/phase-5/reports/5.17-row-state-lag-recon.md`. Smoke verification needed post-deploy.
- **Reference:** `docs/phase-5/reports/5.4-signalr-timeout-fix.md` smoke evidence section.

#### 5.18 Frontend test framework not installed

- **Priority:** P1
- **Origin:** `[Engineering]`
- **Status:** ✅ Done — vitest + @testing-library/react + jsdom installed (confirmed via package.json). Test suite runs via `npm run test`. Framework smoke test and stepOrder unit tests both passing. §5.19 (Step 7 reachability tests, previously blocked on §5.18) can now be actioned.
- **Surfaced:** 2026-06-14 during Phase 5.5b implementation.

No jest/vitest/@testing-library/react in package.json. The Phase
5.5b prompt specified unit tests for `isStepReachable` step 7
cases (5 scenarios: zero sections, sections + no target languages,
sections + target languages + no completed runs, sections + target
languages + one completed run, already published). These were
deferred because there is nothing to run them in.

Fix direction: install vitest + @testing-library/react + @vitejs/plugin-react
as dev dependencies; configure vitest.config.ts with the Next.js
alias set; add the five test cases in
`web/src/features/toolbox-talks/components/learning-wizard/lib/__tests__/stepOrder.test.ts`.

The five cases to cover:

1. `sections.length === 0` → false
2. `sections.length > 0`, no target languages → true
3. `sections.length > 0`, target languages set, no completed runs → false
4. `sections.length > 0`, target languages set, one completed run → true
5. `talk.status === 'Published'` → false

#### 5.19 Unit tests for Step 7 reachability rule (depends on §5.18)

- **Priority:** P2
- **Origin:** `[Engineering]`
- **Status:** ✅ Done — 2026-06-15 — 7 unit tests added in `stepOrder.test.ts` covering all behavioural states for the Step 7 reachability rule: zero sections, no target languages (English-only path), targets declared with no completed runs, targets declared with a completed run (with and without pending reviewer decisions), and already-published status. Defensive `validationRuns = undefined` case also covered via the `?? []` guard. 9 of 9 tests passing (2 from §5.18 smoke + 7 new). Fix: `docs/phase-5/reports/5.19-stepOrder-tests.md`.
- **Surfaced:** 2026-06-14 during structural robustness refactor of `stepOrder.ts`.

The Step 7 reachability rule in
`web/src/features/toolbox-talks/components/learning-wizard/lib/stepOrder.ts`
has four meaningful behavioral states, now made explicit by the 2026-06-14
structural refactor:

1. `talk.sections.length === 0` → `false`
2. `talk.status === 'Published'` → `false`
3. Sections exist, no target languages declared (English-only path) → `true`
4. Sections exist, target languages declared, no completed validation runs → `false`
5. Sections exist, target languages declared, at least one run with `status === 'Completed'` → `true`

After §5.18 is closed (vitest + @testing-library/react installed), add unit
tests covering each state in:
`web/src/features/toolbox-talks/components/learning-wizard/lib/__tests__/stepOrder.test.ts`

Also cover the defensive default: passing `validationRuns = undefined` with
target languages declared must behave identically to passing `[]` (both
return `false` — the `?? []` guard is the enforcement point).

#### 5.20 Refresh Amendment

- **Priority:** P2
- **Origin:** `[Engineering]`
- **Status:** ✅ Done — 2026-06-15 — Closed by §5.22 (SuperUser slice) and recon (regular-admin slice was a phantom). The Phase 5.3b smoke observation was SuperUser-only; the §5.22 fix (removing the TenantQueryInvalidator hydration redirect) eliminated the cause. Regular admins were never affected — the `(authenticated)/layout.tsx:30-36` spinner-gate prevents downstream layouts from mounting during auth hydration, so no stale-state redirect could fire. SU + All Tenants mode (`activeTenantId === null`) bouncing to `/admin/tenants` is by-design per `admin/layout.tsx:52-58`. Recon: `docs/phase-5/reports/5.20-refresh-amendment-recon.md`.
- **Surfaced:** 2026-06-11 during Phase 5.3b smoke.

Phase 5.3b smoke (2026-06-11) confirmed the side effect: refresh
on any /learnings/{talkId}/{step} route lands on /drafts rather
than the step the user was on. Functional (user clicks Resume to
return) but not refresh-recovery as PHASE_5_STANDARDS §5.4
prescribes.

**SuperUser slice (CLOSED):** The redirect was caused by
`TenantQueryInvalidator` firing on the `null → storedUUID`
hydration transition and redirecting to the parent path, which
then redirected to /drafts. The §5.22 fix (2026-06-15) removed
the redirect block from `TenantQueryInvalidator` entirely.
SuperUsers refreshing on any wizard step now stay on that step.

**Regular-admin slice (OPEN):** Regular admins' `activeTenantId`
is always null and never transitions, so the invalidator was
never the cause for them. If regular admins experience
refresh-position loss, the cause lies elsewhere (auth layout
guards or session restoration). Needs investigation if confirmed
as a real issue.

#### 5.21 Learning wizard page header inherits wrong context

- **Priority:** P3
- **Origin:** `[Engineering]`
- **Status:** ✅ Done — 2026-06-15 — `web/src/app/(authenticated)/admin/toolbox-talks/layout.tsx` extended with a `leafLabels` map covering all 8 learnings routes and a UUID-skip guard for the dynamic `[talkId]` segment. Breadcrumb for learnings routes now renders three levels: "Administration / [Learnings] / Leaf" where "Learnings" is a link back to the drafts list. Non-learnings toolbox-talks routes are unchanged — "Learnings" remains plain text, no third segment. The subtitle header sub-issue originally described in §5.21 was already resolved before this work — `admin/layout.tsx` correctly emits "Manage learnings and training" for all `/admin/toolbox-talks/*` paths. Fix: `docs/phase-5/reports/5.21-wizard-breadcrumb-leaf-fix.md`. Recon: `docs/phase-5/reports/5.21-wizard-header-context-recon.md`.
- **Surfaced:** 2026-06-10 during 5.2 smoke.

The new learning-wizard routes
(/admin/toolbox-talks/learnings/...) render with the page header
"Administration / Manage employees and users" — inherited from a
parent layout that thinks the page is in the employees subsection.

Affects all 8 new routes scaffolded in Phase 5.2.

Fix direction: either the routes need their own layout override
that sets the correct page header, or the shared admin layout
needs to derive its header from the active route segment rather
than from a default. Look at how the existing toolbox-talks
pages (talks/, courses/, schedules/) handle this — they don't
show the "Manage employees" header, so the pattern exists.

Also: breadcrumbs render as "Administration / Learnings" with
no leaf segment ("Drafts", "New", etc.). Worth adding.

#### 5.22 TenantQueryInvalidator parent-path redirect doesn't know which paths are routable

- **Priority:** P2
- **Origin:** `[Engineering]`
- **Status:** Open
- **Surfaced:** 2026-06-11 during Phase 5.3b smoke testing.

web/src/lib/providers.tsx — TenantQueryInvalidator strips the
UUID segment from the URL on tenant change and uses
router.replace() with the resulting parent path. The logic
doesn't validate that a page.tsx exists at the target — it
just assumes "parent of any UUID route is itself a valid
page."

This held for existing admin detail pages (talks/{id},
courses/{id}, etc.) because their parent paths happen to
have list pages. Phase 5's learning-wizard routes broke
the assumption: /learnings/{talkId}/quiz has no
/learnings/ landing page, so the redirect hit 404.

Phase 5.3b smoke fix added a /learnings/ index page that
redirects to drafts, resolving the 404 for the wizard's
routes. The underlying logic in TenantQueryInvalidator is
still fragile — any future route tree that adds UUID
segments without a parent landing page will hit the same
bug.

Fix direction:

- Either: have TenantQueryInvalidator redirect to a
  known-safe fallback (e.g. tenant dashboard) when the
  parent path can't be confirmed routable.
- Or: add a manifest of valid parent paths and check
  against it before redirecting.
- Or: stop stripping UUID segments altogether and just
  invalidate React Query — the redirect was a defensive
  guard against showing stale tenant data, but query
  invalidation alone may be sufficient.

Only reproducible on accounts that transition activeTenantId
from null to non-null (SuperUser with stored tenant ID).
Regular admins don't trigger it. Deferred — the index-page fix
in Phase 5.3b unblocks the new wizard's routes; the structural
fix can wait for a cross-cutting cleanup.

#### 5.23 Wizard Step 4 Settings — tenant defaults (deferred from Phase 5.3d)

- **Priority:** P3
- **Origin:** `[Engineering]`
- **Status:** ✅ Done — 2026-06-16 — Five default fields added to `ToolboxTalkSettings`; tenant-level `UpdateToolboxTalkTenantDefaultsCommand` implemented; General tab in settings admin UI built out with RHF + Zod form; `InitialiseToolboxTalkCommandHandler` now reads tenant defaults at talk creation. Hardcoded `IsActive = false` removed (per §5.23 follow-up recon Q1.4 — `IsActive` is not a learner-visibility gate). `DefaultPassingScore` also wired at creation for consistency. Fix: `docs/phase-5/reports/5.23-step4-tenant-defaults-fix.md`.
- **Source:** Phase 5.3d spec item I1

#### 5.24 ToolboxTalk.Frequency vs RequiresRefresher/RefresherIntervalMonths conflict (Phase 5.3d)

- **Priority:** P2
- **Origin:** `[Engineering]`
- **Status:** ✅ Done — 2026-06-16 — Bidirectional mirror added between `ToolboxTalk.Frequency` and `RequiresRefresher`/`RefresherIntervalMonths` via `RefresherFrequencyMapper` shared helper (Domain/Helpers). All write paths now keep both representations in sync: `InitialiseToolboxTalkCommandHandler` (new wizard create), `UpdateToolboxTalkSettingsCommandHandler` (Step 4 save), `UpdateToolboxTalkCommandHandler` (old wizard edit — the critical overwrite fix), and both publish paths in `ContentCreationSessionService`. The old edit form's silent overwrite of `RequiresRefresher = false` is closed — `UpdateToolboxTalkCommandHandler` now derives canonical fields from `request.Frequency` rather than trusting absent DTO defaults. Hardcoded `FrequencyDisplay = "Once"` in `InitialiseToolboxTalkCommandHandler.MapToDto` removed. `Weekly` removed from new-selection options in old form; existing Weekly talks display correctly. Full removal of `Frequency` deferred to §7.1. Fix: `docs/phase-5/reports/5.24-frequency-conflict-fix.md`.
- **Source:** Phase 5.3d spec item I2

#### 5.25 Mobile audit at Phase 5 closure

- **Priority:** P1
- **Origin:** `[Engineering]`
- **Status:** Descoped — 2026-06-17 — Mobile UX rescoped from Phase 5 closeout to product-wide concern. The "mandatory audit at closure" framing was artificially narrow (the wizard is one of many surfaces; mobile correctness matters for all of them). Broader audit lives in §7.2 (Post-Phase-5 Cleanup). PHASE_5_STANDARDS §10 amended to remove the closure-gate framing. Mobile-breaking bugs noticed during Phase 5 work get logged as normal BACKLOG entries, not held against Phase 5 sign-off.
- **Surfaced:** 2026-06-10 during 5.2 smoke.

PHASE_5_STANDARDS §10 requires the wizard to be seamless on mobile.
Verifying after every chunk is overkill; doing it never is wrong.
A dedicated mobile pass at Phase 5 closure (or sooner if something
obviously breaks earlier) covers:

- 375px / 768px / 1280px verification of every step + drafts list
- Touch target sizes
- Drag-to-reorder works with touch (per §10.2 + BACKLOG §1.3.3)
- Modal full-screen vs centred behaviour
- No horizontal scroll at any width ≥ 320px

#### 5.26 SPRINT.md stale — needs Phase 5 state rewrite

- **Priority:** P2
- **Origin:** `[Engineering]`
- **Status:** ✅ Done — 2026-06-17 — SPRINT.md deleted rather than rewritten. Read-through confirmed every item in SPRINT.md was already tracked in BACKLOG (either as an open entry or in §7 Recently Closed). The duplicated content invited drift (SPRINT.md was 10+ days stale at the point §5.26 was opened). Active prioritised work is now captured directly in BACKLOG. A new sprint document may be created when a future large phase begins; for now the surface is BACKLOG only.

#### 5.27 Phase 5.6 cutover toggle — parallel-period mechanism

- **Priority:** P1
- **Origin:** `[Engineering]` `[Boss]`
- **Status:** ✅ Done — 2026-06-16 — Cutover toggle infrastructure shipped. Tenant-level `UseNewWizard` setting defaults to `"false"` (legacy wizard). When `"true"`, the "Create New" button on the Learnings list and the legacy wizard's "Create Another" both route to the new wizard. URL parameter `?wizard=new|old` overrides per-navigation for smoke-testing. `useWizardPreference` hook resolves preference order (URL override → TenantSettings → default old). Legacy wizard drafts show a "Legacy" badge in the drafts list with corrected Resume routing (live bug fix — without this fix, Resume would have navigated legacy-draft IDs to new-wizard step URLs). Toggle exposed via Settings → General tab. CLAUDE.md Note 29 added. Unit tests: 6 cases in `hooks/__tests__/useWizardPreference.test.ts`. Fix report: `docs/phase-5/reports/5.27-cutover-toggle-fix.md`. **Flipping any tenant to `"true"` remains gated on §24 (Edit workflow design).**
- **Surfaced:** 2026-06-15 stocktaking discussion (renumbering 2026-06-15).

The original Phase 5.6 was framed as "move the Create New button to the new wizard." The 2026-06-15 stocktaking refined this to: a toggle that lets old and new wizards run in parallel, with the user's preferred wizard chosen per-tenant (and per-URL for testing), until business sign-off triggers manual cutover.

### Design decisions (locked 2026-06-15)

1. **Toggle level:** tenant-level toggle plus URL parameter for testing.
2. **In-flight drafts:** stay with the wizard that created them. Two separate data paths.
3. **Toggle removal:** manual decision, no automatic threshold.

### Adjacent dependencies before any tenant is toggled to new-as-default

- **§24** (Edit workflow design): ✅ **engineering gate cleared 2026-06-18** (Chunks 1, 2, 3, 6 shipped — Demo cut and toggle-flip cut complete). Flipping any tenant to `"true"` is now an operational decision; no further engineering work blocks it.
- **§5.24** (Frequency conflict): ✅ resolved 2026-06-16 — bidirectional mirror prevents old form from overwriting Step 4 refresher config.
- **§5.20** (Refresh Amendment): ✅ resolved 2026-06-15 — closed by §5.22 + phantom recon.

§24 engineering gate cleared. Toggle infrastructure is ready. Any production tenant can now be toggled to `"true"` as an operational decision.

### Implementation scope

Recon-first. Likely chunks:

- Tenant-level toggle (entity + settings UI).
- URL parameter handling for testing.
- "Create New" button router — read toggle, navigate to appropriate wizard entry.
- Drafts list display — distinguish old-wizard drafts from new-wizard drafts.
- Documentation / migration notes for users.

### Out of scope for this chunk

- Migration of in-flight drafts between wizard models (rule 2 says they stay).
- Removal of old wizard code paths (separate chunk after toggle is removed).

#### 5.28 P0 — Anthropic model deprecation incident (claude-sonnet-4-20250514 retired)

- **Priority:** P0
- **Origin:** `[Engineering]`
- **Status:** ✅ Done — 2026-06-16 — Unified `AIProviders` configuration section introduced with `IValidateOptions<T>` fail-fast startup validation. Six hardcoded Anthropic model identifier sites converted to inject `IOptions<AIProviderOptions>`. `claude-sonnet-4-20250514` (retired by Anthropic 2026-06-15) replaced with `claude-sonnet-4-5` across all sites. Orphaned `Round3Provider` config key removed. Code shipped to Development and smoke-verified against affected surfaces (lesson parse, Help Chat, TransVal Round 3D, Haiku-side conversions, ElevenLabs transcription, DeepL Round 1B). Production deployment is an operational concern — tracked via commit history and Railway deployment logs, not in BACKLOG. Recon: `docs/phase-5/reports/anthropic-model-deprecation-recon.md` and `docs/phase-5/reports/multi-provider-config-recon.md`. Fix report: `docs/phase-5/reports/multi-provider-config-fix.md`.

**Incident:** Anthropic retired `claude-sonnet-4-20250514` (also known as `claude-sonnet-4-0`) effective 2026-06-15 after a 60-day deprecation notice. Six production code paths broke: help chat, subtitle translation (`SubtitleProcessing:Claude:Model`), regulatory requirement ingestion, regulatory requirement mapping, regulatory scoring, and the Round 3 back-translation provider in TransVal.

**Fix:** Multi-provider config unification patch. See `docs/phase-5/reports/multi-provider-config-fix.md` for full details.

- Introduced `AIProviderOptions` (`AIProviders` config section) as a single canonical model-identifier registry for Anthropic, Gemini, and ElevenLabs
- Added `IValidateOptions<AIProviderOptions>` with `.ValidateOnStart()` — missing config now causes immediate startup failure instead of silent null-reference at call time
- Converted 6 hardcoded `claude-sonnet-4-20250514` / `claude-haiku-4-5-20251001` sites to inject `IOptions<AIProviderOptions>`
- Updated `appsettings.json` and `appsettings.Development.json`: added `AIProviders` section, set `Sonnet = "claude-sonnet-4-5"`, removed orphaned `Round3Provider` key
- Removed C# default model values from `ClaudeSettings`, `TranslationValidationSettings`, `SubtitleProcessingSettings` — no silent fallback to retired models possible

#### 5.29 Follow-up items from P0 incident (§5.28)

- **Priority:** P2
- **Origin:** `[Engineering]`
- **Status:** Open — non-blocking, do not hold §5.28 deploy

**Follow-up items identified during the §5.28 patch and subsequent work:**

1. **API key security:** API keys (Anthropic, Gemini, ElevenLabs, DeepL) remain in `appsettings.json`. They should be moved to Railway environment variables only. This is a known gap; the §5.28 patch explicitly excluded key migration from scope.
2. **CostEstimationService rate table:** Rates for `claude-sonnet-4-5` (`SonnetInputPer1K`, `SonnetOutputPer1K`) were inherited from the deprecated `claude-sonnet-4-0` rate table (April 2026 EUR). Verify against current Anthropic pricing and update if needed.
3. **DI registration pattern for new services:** Any new service that calls a Claude, Gemini, or ElevenLabs model must (a) inject `IOptions<AIProviderOptions>` instead of hardcoding a model string, and (b) chain `.AddPolicyHandler(ResiliencePolicies.Get*Policy(logger))` at registration. Both rules to be added to CLAUDE.md coding conventions.
4. **Railway env vars:** Ensure `AIProviders__Anthropic__Models__Sonnet` and `AIProviders__Anthropic__Models__Haiku` (plus Gemini Flash and ElevenLabs Transcription) are set in Railway Production and Development services before deploy. The fail-fast validator will catch missing values at startup.

**Duplicate source of truth: AIProviders vs operational config keys:** The §5.28
patch introduced `AIProviders` as the canonical model identifier registry, but
six operational config keys still hold the same identifiers separately:
`SubtitleProcessing:Claude:Model`, `TranslationValidation:Round1AModel`,
`TranslationValidation:Round3DModel`, `TranslationValidation:Gemini:Model`,
`SubtitleProcessing:ElevenLabs:Model`. These hold model identifiers alongside
operational config (max tokens, batch sizes) and are read directly by the
services. After §5.28, both sources must be kept in sync manually — drift
between them is a future incident shape. Proper fix: refactor `ClaudeSettings`,
`TranslationValidationSettings.GeminiSettings`, and
`SubtitleProcessingSettings.ElevenLabsSettings` so the `Model` properties
derive from `AIProviderOptions` at bind time rather than being independently
configured. Also extend `AIProviderOptionsValidator` to cover the operational
keys until the refactor lands.

5. **`PublishSuccessState` dead code in `PublishStep.tsx`:** `PublishSuccessState` is exported at `web/src/features/toolbox-talks/components/learning-wizard/steps/PublishStep.tsx` (bottom of file) but the publish page uses `router.push` on success rather than rendering this component — it was presumably an inline success state replaced by navigation. Safe to delete if no other consumer exists (surfaced 2026-06-17 during §30 implementation).

#### 5.30 ToolboxTalk.IsActive is functionally decorative

- **Priority:** P3
- **Origin:** `[Engineering]`
- **Status:** Open
- **Surfaced:** 2026-06-16 during §5.23 follow-up recon.

`ToolboxTalk.IsActive` does not gate learner visibility. Every employee-facing
query (`GetMyToolboxTalksQueryHandler`, `GetMyToolboxTalkByIdQueryHandler`,
the `/pending`, `/in-progress`, `/overdue`, `/completed` status endpoints)
filters only on the presence of a `ScheduledTalk` assignment record;
`IsActive` and `Status` are ignored. The schedule-processing job
(`ProcessToolboxTalkSchedulesJob`) and assignment-creation handler
(`ProcessToolboxTalkScheduleCommandHandler`) likewise do not check
`IsActive` or `Status` before creating assignments. The field is currently
used in only two places:

1. `GetToolboxTalkDashboardQueryHandler` counts `activeTalks = talks.Count(t => t.IsActive)` for the admin dashboard's active-talks tile.
2. `GetToolboxTalksQueryHandler` (admin list) accepts an optional `IsActive` filter — only applied when the admin explicitly filters on it.

Operationally, "deactivating" a published talk via the wizard toggle has
no effect on already-assigned learners — they continue to see and can
still complete the talk. To genuinely deactivate a talk, an admin must
cancel or delete every existing `ScheduledTalk` assignment manually.

This is either a missing feature (deactivation should cascade to
assignments or block new assignments) or a UI labeling issue (the
toggle implies more than it does). Product decision needed before
sizing a fix. Adjacent: §5.23 introduced `DefaultIsActive` as a tenant
default — a tenant setting it to `true` is safe today precisely because
the field is decorative.

Files:
- `src/Modules/ToolboxTalks/.../Queries/GetMyToolboxTalks/GetMyToolboxTalksQueryHandler.cs:23-28`
- `src/Modules/ToolboxTalks/.../Queries/GetMyToolboxTalkById/GetMyToolboxTalkByIdQueryHandler.cs:29-44`
- `src/Modules/ToolboxTalks/.../Jobs/ProcessToolboxTalkSchedulesJob.cs:57-63`
- `src/Modules/ToolboxTalks/.../Queries/GetToolboxTalkDashboard/GetToolboxTalkDashboardQueryHandler.cs:23-30`

---

## 5.31 — DataSeeder seeds credentialled accounts ungated in Production

Priority: P1
Origin: [Engineering]
Status: ✅ Done — 2026-06-17 (see docs/5.31-dataseeder-gating-fix.md)
**Amendment 2026-06-18:** Gate widened to include Demo environment per §5.7 requirement. See docs/5.31-amendment-fix.md.
Surfaced: 2026-06-17 during Playwright Step 2 recon (see docs/dataseeder-production-recon.md).

DataSeeder.SeedAsync at Program.cs:332 runs unconditionally on every startup in every environment. It seeds two accounts with hardcoded literal passwords:

superuser@certifiediq.ai / SuperUser123! (DataSeeder.cs:255–256) — SuperUser role
admin@quantumbuild.ai / Admin123! (DataSeeder.cs:309–310) — Admin role

Skip-if-exists guards (DataSeeder.cs:258, :315) prevent password overwrite on rolling restarts. Fresh-DB scenarios (new deploy on a clean instance, DB reset, Demo restore per §5.7) recreate the accounts with the literal passwords. Repo is private; credentials are known to two people; exposure is bounded but the pattern is wrong and should be fixed before §5.7's Demo refresh brings up another environment that would inherit them.
Fix direction:

Gate credentialled user-creation blocks in DataSeeder.cs behind Environment.IsDevelopment(). System data (roles, permissions, sectors, regulatory bodies) continues to seed in all environments.
Replace literal passwords with config-driven values for the Development seeding path — sourced from environment variables or appsettings.Development.json per implementation recon's recommendation. No insecure fallback defaults — fail-fast on missing config.
Add a regression test asserting DataSeeder does not create credentialled accounts when Environment != Development.
Rotation of Production credentials can happen at the same time as the code fix or after — operational decision, not code-blocking.

Adjacent: §5.7 (Demo refresh) must not bring up Demo without this fix in place, or Demo gets the seeded accounts with the literal passwords. Worth cross-referencing in §5.7.
Out of scope for this entry: API keys in appsettings.json (separate finding — docs/dataseeder-production-recon.md Finding A).

--- 

# 7. Post-Phase-5 Cleanup

Items deferred until specific Phase 5 work decommissions allow them to land cleanly. Do not action these until the stated blocker is resolved.

#### 7.1 Remove `ToolboxTalk.Frequency` after old-wizard decommission

- **Priority:** P2
- **Origin:** `[Engineering]`
- **Status:** Open — blocked on old-wizard decommission (§5.27 cutover toggle + subsequent removal of the old edit form and `UpdateToolboxTalkCommandHandler` legacy paths).
- **Surfaced:** 2026-06-16 during §5.24 fix.

`ToolboxTalk.Frequency` is a legacy enum column (`Once / Weekly / Monthly / Annually`) kept in sync with the canonical `RequiresRefresher` + `RefresherIntervalMonths` fields via `RefresherFrequencyMapper` (added in §5.24). The dual representation is a workaround — the new wizard treats the canonical fields as authoritative; the legacy `Frequency` is mirrored so the old edit form, dashboard frequency breakdown, admin list filter, and legacy DTO `frequencyDisplay` field continue to work.

Once the old wizard is decommissioned and `UpdateToolboxTalkCommandHandler` no longer needs to accept `Frequency` as input, the `Frequency` column can be removed entirely. Work needed:

- Migrate every read site listed in `docs/phase-5/reports/5.24-frequency-conflict-recon.md` §2 (Step 1) to read `RequiresRefresher` + `RefresherIntervalMonths` instead (~14 backend read sites).
- Rework the admin dashboard `talksByFrequency` KPI breakdown — compute from canonical fields or replace with a "has refresher" / "interval bucket" grouping.
- Rework the admin list frequency filter similarly.
- Remove `RefresherFrequencyMapper.ToCanonicalFields` (no longer needed once `UpdateToolboxTalkCommandHandler` is gone) and `ToLegacyFrequency` / `FromWizardFrequencyString` (all write paths removed).
- Update the frontend `ToolboxTalkFrequency` type and `frequency` / `frequencyDisplay` properties on TypeScript interfaces (~4 sites per recon).
- Drop the `Frequency` column via CLI-generated EF migration with Designer.cs (CLAUDE.md Note 28).
- Decide on existing-data handling for rows still carrying `Weekly` — round to no-refresher (current behaviour) or monthly.

Adjacent: `Weekly` refreshers have been non-functional for refresher scheduling since before Phase 5 (`RefresherSchedulingService` uses `RefresherIntervalMonths` — integer months only). Removing `Frequency` is the natural moment to make that state honest.

References: `docs/phase-5/reports/5.24-frequency-conflict-recon.md` (§2 read-site inventory, §6 fix candidates, §10 enum mismatch mapping).

#### 7.2 Product-wide mobile UX audit

- **Priority:** P2
- **Origin:** `[Engineering]`
- **Status:** Open
- **Surfaced:** 2026-06-17 during §5.25 descope discussion.

Mobile UX correctness applies across the entire product, not just the new wizard. The original Phase 5 framing (§5.25 — wizard mobile audit at closure) was artificially narrow. This entry replaces it with a product-wide audit.

**Scope:** every authenticated route the product exposes, including:

- The new learning wizard (eight routes: drafts, new, parse, quiz, settings, translate, validate, publish)
- The talk detail page and its sub-pages (validation runs, translation review, edit)
- The course builder
- Settings admin pages (all five tabs: General, Notifications, Quiz, Validation, QR Training)
- The admin dashboard
- Employee portal (My Learnings, in-progress, completed, certificates)
- QR scan flow (PIN entry, language selection, talk playback)
- Certificate views (admin list, individual certificate)
- Schedule management
- Asset Management (when shipped per §2.2.1)
- Any other authenticated route surfaced by the codebase

External-reviewer-facing pages (third-party review links) — separate audit; not in this scope.

**Breakpoints:** 320px (minimum supported), 375px (small phone), 768px (tablet), 1280px (desktop). The 320px check is the floor — no horizontal scroll at or above this width.

**Method:** code-driven sweep first to identify systemic patterns (fixed widths, missing responsive utilities, hardcoded padding that doesn't scale, modals without full-screen mobile behaviour, hover-only affordances, drag-and-drop lacking touch handlers). Visual confirmation pass at each breakpoint. Categorise findings as blocking / significant friction / polish / touch interaction.

**Findings disposition:** blocking and significant-friction issues land as individual BACKLOG entries with severity. Polish and per-breakpoint issues group into a single follow-up entry. Touch interaction issues that fail PHASE_5_STANDARDS §10.2 (drag-to-reorder with touch) are blockers.

**Sizing:** substantial work — probably multi-day. Should be approached as its own focused effort, not interleaved with feature work. Worth doing once the product surface is more settled (post §24 edit workflow, post §5.27 cutover toggle).

References: PHASE_5_STANDARDS §10 (the standard that motivates this), §5.25 (the original wizard-scoped entry that was descoped here).

#### 7.3 Investigate per-section external review escalation

- **Priority:** P3
- **Origin:** `[Engineering]`
- **Status:** Open — investigation deferred until §30 per-language fix has shipped.
- **Surfaced:** 2026-06-17 during §30 product discussion.

The §30 fix ships per-language external review escalation — the reviewer escalates an entire language to external review at once. The original product intent included per-section escalation: the reviewer escalates specific sections (e.g., "this regulatory section is too important for me to sign off on") while accepting or editing others internally.

Per-language was scoped for §30 because:
- Existing `InitiateExternalReview` backend operates per-language.
- Smaller change, faster to ship.
- Per-section may not be needed if tenants don't request it.

This entry tracks the investigation if per-section becomes a real ask. Investigation scope:

- What changes to the workflow state machine would be needed? Currently the state lives on `ToolboxTalkTranslation` (per-language). Per-section would need state on `TranslationValidationResult` (per-section per-language) — possibly a new field or a separate per-section escalation entity.
- How does the external reviewer's UI surface "you're reviewing only sections X, Y, Z of this translation"?
- How are decisions on non-escalated sections preserved when escalated sections come back?
- What happens if the reviewer wants to escalate after some sections are already accepted?
- Does the validation run's overall pass/fail logic need adjustment to account for "some sections externally reviewed, others internally"?
- UI for selecting which sections to escalate.
- State display: a language that has some externally reviewed sections is no longer a simple workflow state — it's a mix.

Investigation produces a doc with findings and a recommendation: implement, defer indefinitely, or close as not-needed. The doc lives at `docs/per-section-external-review-investigation.md`. The investigation itself is read-only; any actual implementation work would be a separate entry.

#### 7.4 User-ID propagation gap in translation pipeline events

- **Priority:** P3
- **Origin:** `[Engineering]`
- **Status:** Open — surfaced during §31 implementation (2026-06-17).

`GenerateContentTranslationsCommand` carries `TriggeredByType` (enum: `User` / `System`) but not a user ID. `TranslationValidationJob` always uses `TriggeredByType.System` regardless of who initiated the run. This means notification recipients for §31 default to **all Admins** — there is no way to target the specific user who triggered the job.

If per-user targeted notifications become a product requirement:

- Add `TriggeredByUserId? Guid?` to `GenerateContentTranslationsCommand`. Controller sets it from `ICurrentUserService.UserIdGuid`. Background jobs (`ContentGenerationJob`, `MissingTranslationsJob`, `EmployeeLanguageChangeHandler`) leave it null.
- Thread it through `GenerateContentTranslationsCommandHandler` to the notification dispatch.
- For validation: `TranslationValidationController` sets `TriggeredByUserId` on the job parameter (requires adding the field to `TranslationValidationJob`). All other validation trigger sites leave it null.
- Notification dispatch: send to triggering user if non-null; fall back to all Admins when null.
- The `ContentGenerationJob` path deserves care — it sets `TriggeredBy = TriggeredByType.User` by default but has no user ID. The misleading default should be corrected to `System` before this work lands.

**Caveat:** At typical tenant sizes (1-3 Admins), the broadcast approach in §31 is fine. Only action this if a multi-Admin tenant reports notification noise or a specific workflow requires targeted delivery.

#### 7.5 Email body templates — extract to shared pattern

- **Priority:** P3
- **Origin:** `[Engineering]`
- **Status:** Open — surfaced during §31 implementation (2026-06-17).

`ToolboxTalkNotificationService` and `EmailService` both build HTML email bodies as large inline `$"..."` string literals. As the number of notification types grows, this becomes a maintenance burden — visual inconsistency, no previewing, no localisation path.

Options (in order of complexity):

1. **Shared `EmailBodyBuilder` static helper** — a set of C# methods (`BuildHeader`, `BuildSection`, `BuildCtaButton`, `BuildTable`) that compose HTML from typed parameters. Zero new dependencies. Bodies stay in C# but are assembled from reusable blocks.
2. **Razor template engine (`.cshtml` files)** — compile-time type-checked, previewable in a browser, familiar to .NET devs. Requires `Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation` or standalone Razor renderer. Non-trivial setup.
3. **Fluid / Scriban template engine** — lightweight Liquid-syntax templates, loadable from files or strings. Easier to hand-edit than raw HTML. Adds a NuGet dependency.

**Not urgent while there are only ~8 email types.** Revisit when the count exceeds ~15 or when a designer needs to touch the templates without touching C# code.

#### 7.7 Company list Select truncated at pagination limit

- **Priority:** P3
- **Origin:** `[Engineering]` `[§25 Chunk 1 recon discovery 2026-06-18]`
- **Status:** Open
- **Surfaced:** During §25 Chunk 1 implementation. The `useAllCompanies` hook used by InputConfigStep's Client Name Select calls `GET /companies/all` and returns the full array in one request. However, if the backend ever pages this endpoint or a separate paginated call is substituted, tenants with more than the page-size limit will have a truncated Select dropdown — companies beyond the first page are silently invisible. The legacy wizard has the same pattern. Does not block §25.

**Fix direction:**
- The current `/companies/all` endpoint returns an unbounded flat array — no pagination issue today.
- If this endpoint is ever replaced with a paginated call, either: pass a high `pageSize` parameter to the Select call, or implement a Combobox with server-side search instead of a flat Select.

Defer until a real tenant hits any limit or the endpoint shape changes. Track sightings; if more than one Select consumer surfaces the same gap, prioritise.

#### 7.6 `IX_Tenants_Name` does not align with application soft-delete semantics

- **Priority:** P3
- **Origin:** `[Engineering]`
- **Status:** Open — latent edge-case bug surfaced during §3.11 recon (2026-06-17).

The unique index `IX_Tenants_Name` ([TenantConfiguration.cs](src/Core/QuantumBuild.Core.Infrastructure/Data/Configurations/TenantConfiguration.cs)) is declared without a filter clause:

```csharp
builder.HasIndex(e => e.Name)
    .IsUnique()
    .HasDatabaseName("IX_Tenants_Name");
```

This enforces uniqueness across ALL tenant rows, including soft-deleted ones. The application-level duplicate check in `TenantService.CreateAsync` explicitly excludes soft-deleted tenants (`&& !t.IsDeleted`).

Result: a tenant named "Acme" that has been soft-deleted does not appear as a duplicate at the application layer (the check passes), but the DB index blocks the insert. After §3.11's fix, the `DbUpdateException` is caught and translated to "A tenant with this name already exists" — but the message is misleading because, semantically, the application doesn't consider a soft-deleted tenant to exist.

The fix is to add a filter to the index:

```csharp
builder.HasIndex(e => e.Name)
    .IsUnique()
    .HasFilter("\"IsDeleted\" = false")
    .HasDatabaseName("IX_Tenants_Name");
```

This requires a CLI-generated migration (per CLAUDE.md Note 28). The same misalignment may apply to `IX_Tenants_Code` — that index already filters for non-null codes but does not exclude soft-deleted rows. Worth reviewing both in the same chunk.

**Risk if not addressed:** rare. Requires a tenant to be soft-deleted, then an attempt to create a new tenant with the exact same name. Post-§3.11 the user sees a clean error message, but the system is rejecting a legitimate request. Workaround: use a different name.

#### 7.8 SettingsEditPanel missing fields: video watch %, active status, auto-assign

- **Priority:** P3
- **Origin:** `[Engineering]` `[§25 Chunk 5 recon discovery 2026-06-19]`
- **Status:** Open
- **Surfaced:** During §25 Chunk 5 recon, while assessing the §24 detail panels for visual polish. `settingsEditSchema` in `web/src/features/toolbox-talks/components/detail/SettingsEditPanel.tsx` does not include `minimumVideoWatchPercent`, `isActive`, or `autoAssignToNewEmployees` — these fields exist on the underlying talk model and are settable via the wizard but cannot be edited from the talk detail page once a talk is created. Users who want to change watch %, active status, or auto-assignment after publish must either use the wizard's re-edit flow or modify the data directly.

**Fix direction:**
- Add `minimumVideoWatchPercent`, `isActive`, and `autoAssignToNewEmployees` to `settingsEditSchema` with the same Zod validation rules as the wizard's `settingsSchema` (`minimumVideoWatchPercent: z.number().int().min(50).max(100)`, booleans for `isActive` and `autoAssignToNewEmployees`)
- Add corresponding form fields in edit mode: Switch + bordered tile via `ToggleRow` (for `isActive` and `autoAssignToNewEmployees`), and a `w-24` number input with label-unit suffix (for `minimumVideoWatchPercent`, matching the existing `w-24` + label-with-unit convention already in the panel)
- Add corresponding `ViewRow` entries to the view-mode rendering
- Wire the values into `onSubmit` — currently these three fields are passed through from `talk.*` unchanged; they need to come from `values.*` like the other edited fields

Estimated 0.5 day. Not blocking §25 closure; moderate user impact (real edit workflow gap) but workaround exists (re-run wizard).

---

# 6. Security Notes (Product Decisions)

These are not backlog items — they're explicit product decisions with known trade-offs, captured here for reference and to inform future revisits.

#### 6.1 Plaintext PIN storage for SuperUser + Admin visibility

- **Decision:** `Employees.QrPinPlain` stores QR PINs in plaintext, visible on the employee detail view to SuperUsers and tenant Admins (Admins see only their own tenant's employees via existing tenant scoping).
- **Date:** 28 May 2026
- **Origin:** `[Boss]`
- **Trade-off:** Plaintext PINs are recoverable from any DB access or backup. Visible to every tenant's admins, not just internal staff.
- **Rationale:** Operational need to read PINs to employees who haven't received or lost the email. Existing Reset PIN flow alone was considered insufficient.
- **Revisit if:** A more secure approach becomes viable — e.g. one-time reveal at PIN reset (modal showing the PIN once before destroying the raw value); or encryption with a key held outside the DB (env var / key vault). Either approach would preserve the operational capability while removing the bulk-recoverability risk.

#### 6.2 Asset PINs will follow the same model

- **Decision:** When Asset Management (2.2.1) ships, asset PINs will use the same plaintext storage model as employee PINs.
- **Trade-off:** Same as 6.1 — recoverable from DB access. Slightly different risk profile because asset PINs are often deliberately the tenant's own internal IDs (e.g. `DIG-042`) which are not secret.
- **Revisit:** Alongside 6.1 if/when the PIN storage model is reviewed.

---

## 9. Hardcoded English assumptions in translation pipeline

The `ToolboxTalk.SourceLanguageCode` field is structurally open to
non-English values (entity comment names "en", "af", "es" as
examples), but 13 distinct code paths hardcode English at runtime,
silently overriding any non-English source language a user might
set.

Surfaced during Phase 2b sentence-detection recon on 2026-06-08.
The recon was satisfied that Phase 2b can ship with English-only
sentence detection because no non-English source talks exist
today. But the pipeline-wide hardcoding is a separate concern —
the day someone creates a talk with `SourceLanguageCode = "es"`,
the validation run and downstream jobs will silently treat it as
English without error.

Affected sites (file:line — nature):

- ToolboxTalk.cs:231 — Entity default = "en"
- CreateToolboxTalkCommand.cs:37 — Command default = "en"
- ContentCreationSessionService.cs:553 — Draft talk hardcoded
  SourceLanguageCode = "en"
- ContentCreationSessionService.cs:674 — ValidationRun hardcoded
  SourceLanguage = "en" (ignores the talk's own SourceLanguageCode)
- ContentCreationSessionService.cs:631 — Target-language exclusion
  hardcoded !c.Equals("en", ...)
- MissingTranslationsJob.cs:81 — `?? "en"` fallback
- DailyTranslationScanJob.cs:95 — `?? "en"` fallback
- ContentGenerationJob.cs:495 — `?? "en"` fallback
- TranslationValidationJob.cs:746 — `?? "English"` fallback
- ContentTranslationService.cs:51,198 — `?? "English"` fallback (2 sites)
- SubtitleProcessingOrchestrator.cs:122-123 — Master subtitle track
  hardcoded Language = "English" / LanguageCode = "en"
- ContentExtractionService.cs:518-519 — Same hardcoding in
  extraction path
- TranscriptService.cs:34 — GetSrtContentAsync(id, "en", ...) —
  always fetches English
- EmployeeLanguageChangeHandler.cs:38 — Early-exits on "en"
  preferred language

Investigation needed: which of these are legitimate (e.g., a
validation pipeline that intentionally only supports English source
for now) and which are bugs (e.g., the ValidationRun.SourceLanguage
hardcoding that ignores the talk's actual SourceLanguageCode).

This is not a Phase 2b dependency. It is a tracking entry so the
inconsistency does not get rediscovered every time someone touches
the translation pipeline.

---

## 10. ValidationStarted → Initial state mapping gap (deferred from Phase 3b.1.2)

**Status:** ✅ Done — 2026-06-15 — silently closed by earlier work that added `Validating` to `TranslationWorkflowState` and mapped `ValidationStarted → Validating` in `EventTypeToState`. The `StartValidation` method and `RecordValidationCompleted` method on `ITranslationWorkflowService` were added at the same time (approach (a) chosen). Now exercised in production code by the §5.17 fix — `TranslationValidationJob.ExecuteAsync` calls `StartValidation` before Phase B begins and `RecordValidationCompleted` at Phase B end. The full lifecycle (`Translating → AIGenerated → Validating → Validated`) is now observably written end-to-end for new-wizard runs.

**Surfaced:** 2026-06-08, during Phase 3b.1.2 recon.

---

## 11. Cancel external review — end-to-end

**Status:** ✅ Done — 2026-06-17 (side-effect of §30) — `AwaitingThirdParty` state is now reachable end-to-end, which was the only remaining blocker. The "Cancel invitation" button in `TranslationWorkflowPanel` renders for `state === 'AwaitingThirdParty'` (unchanged). The `CancelExternalReviewDialog` is wired. Backend `cancel-external-review` endpoint is confirmed working (as of 2026-06-15). No code changes needed for §11 itself — §30's state gate fix was the missing piece.

**Update (2026-06-15):** The backend implementation now exists — `POST /api/toolbox-talks/{id}/translations/{languageCode}/cancel-external-review` is implemented and working (confirmed in §21 / 5.5a gap-check). The `InvitationStatus.Revoked` path is wired. The frontend UI to trigger cancellation has not been built; the remaining gap is a "Cancel external review" button on the per-language panel of the talk detail/edit page.

The original scope of Phase 4 work described below is partially superseded. The backend items are complete; only the frontend trigger remains.

Phase 4 must build, end-to-end:

- `CancelExternalReview` service method on `ITranslationWorkflowService`
  (transitions from `AwaitingThirdParty` back to `ReviewerAccepted`,
  marks the invitation `Revoked`, emits `ExternalReviewCancelled` event)
- Controller endpoint exposing it
- Frontend trigger on the per-language panel (Phase 4 work alongside
  the Send for external review button)

Surfaced during Phase 3c recon (commit 38ba9c8). Scope was deferred
rather than built in 3c because the Send for external review action
itself is a Phase 4 concern — Cancel without Send is meaningless.

---

## 12. Seeder/JWT user representation divergence

`TestTenantSeeder.SeedUsersWithUserManagerAsync` creates users via
ASP.NET Identity (`UserManager`), looking up roles by string and
assigning them. `CustomWebApplicationFactory.GenerateTestToken`
forges JWTs with hardcoded role and permission claims, bypassing
Identity entirely.

These two paths can silently produce different user representations
for the same email. A test that authenticates via JWT-forge sees
hardcoded permission claims; the same user looked up via the
Identity-backed path sees whatever roles the seeder actually
assigned (or, historically, none — see §5.15 history).

This was masked during the pre-Phase-5 cleanup because all the
affected deprecated-role users have been removed. The divergence
remains a structural risk: any future test that mixes the two
authentication paths for the same user (e.g., login-via-API then
JWT-forge for the same employee) could exhibit role/permission
inconsistencies that are hard to debug.

Fix direction:

- Reconcile so both paths produce the same user state for the same
  email — most likely by having JWT-forge derive its claims from
  the Identity-backed user rather than carrying its own hardcoded
  set.
- Or, deliberately use only one path per test and document the
  constraint in `IntegrationTestBase`.

Surfaced during the pre-Phase-5 test suite cleanup recon
(2026-06-10). Deferred to the post-Phase-5 comprehensive test
review (§5.15) because fixing it well requires touching test
infrastructure that the review will be reshaping anyway.

---

## 15. InputMode column added in wrong migration

The InputMode column on ToolboxTalk was added in
20260611072549_AddTranscriptWordsJsonToToolboxTalk (Phase 5.3b's
migration) instead of 20260610202622_AddLearningWizardFieldsToToolboxTalk
(Phase 5.3a's migration, where the other Step 1 wizard fields live).

Both migrations apply cleanly in sequence on fresh and existing
databases; the ordering is enforced by EF's migration history.
This is cosmetic untidiness, not a functional bug.

Surfaced 2026-06-11 during Phase 5.3b implementation review.
Deferred because the fix would require rewriting pushed migration
history. Acceptable to live with; flagged so future schema reviews
don't waste time wondering.

---

## 18. Post-publish translation editing gap — AwaitingThirdParty languages

After a talk is published, languages that are `state === 'AwaitingThirdParty'`
have no UI to cancel the external review and re-translate.

The backend escape hatch exists (InvitationStatus.Revoked is defined,
BACKLOG item 11 tracks the full Cancel External Review flow). The
publish step surface the warning banner (amber, non-blocking) per
5.5b spec but cannot provide a cancel action because the backend
endpoint does not yet exist.

Fix direction: implement BACKLOG item 11 (Cancel External Review
end-to-end) then wire a "Cancel review" button in the external
review warning banner so users can act on it from the publish
step or the talk edit page.

Surfaced 2026-06-14 during Phase 5.5b implementation (noted in
5.5a gap-check as a known limitation).

---

## 19. Learning list page shows "Inactive" for draft talks instead of "Draft"

**Priority:** P2
**Origin:** [Internal-QA]
**Status:** Open
**Surfaced:** 2026-06-14, during 5.5b post-deploy smoke (incidental
observation, not part of any smoke scenario).

The learning list page (admin/toolbox-talks/learnings or similar)
renders an "Active/Inactive" column for every talk regardless of
lifecycle status. Talks in `Draft` status (created via the new
wizard but not yet published) display as "Inactive", which is
misleading — a draft isn't an inactive published talk, it's a
work-in-progress that doesn't exist as an assignable artifact yet.

Two distinct concepts are conflated:

- Lifecycle status (Draft, Published) — on the talk entity
- Active/Inactive — visibility/assignability concept that only
  meaningfully applies to published talks

Suggested fix direction: when `Status == Draft`, the column renders
"Draft"; otherwise it renders the existing Active/Inactive value.
Do not universally rename "Inactive" to "Draft" — that would break
display for legitimately deactivated published talks.

Adjacent questions worth checking during recon:

- What does the "Actions" menu on a draft row currently expose? If
  it offers Activate/Deactivate, those are nonsensical for drafts
  and may be a coupled bug.
- Are P2 status filters on this page (if any) doing the right thing
  for drafts?

Not blocking any current work. Suggested P2 — visible UX confusion
but no data corruption, no workflow blocker.

---

## 20. Per-section accept/reject actions missing on Validate step [PRIORITY]

**Priority:** P1
**Origin:** [Internal-QA]
**Status:** ✅ Done — 2026-06-15 — Strict review workflow ported from old wizard with backend enforcement, auto-accept Pass, and no-bypass design. Cache invalidation fix shipped as follow-up. Smoke verified post-deploy (see `docs/phase-5/reports/strict-review-workflow-port.md` Scenarios 1, 2, 3).
**Surfaced:** 2026-06-14, during wizard-skip-fix smoke (Scenario C).

The new wizard's Validate step ports the per-section validation
outcome display from the old wizard — scores, back-translations,
consensus calculations, safety threshold, all visible per section.
But the action UI (accept/reject decisions per section that would
resolve a Review-state outcome) is not present. The user can see
that a section needs review, can read the evidence, but has no
path to act on it within the wizard.

Old wizard reference: `create-wizard/steps/ValidateStep.tsx:186` —
`canContinue = allSectionsDecided || session?.status === 'Validated'`.
The old wizard required per-section decisions before Continue. The
new wizard's Step 7 reachability requires only "at least one
completed validation run" regardless of per-section outcomes,
which means after today's wiring fix, users can reach Publish and
ship a talk while sections remain in unresolved Review state.

This is feature parity loss with a real quality implication — the
old workflow's strictness was the mechanism by which a reviewer
explicitly attested to each Review-state outcome.

### Product decision required before implementation

Three positions are defensible; the chosen position determines
the chunk's shape.

- **Strict** — Each Review-state section must be explicitly
  accepted or rejected before Step 7 becomes reachable. Matches
  old wizard behavior. Largest chunk: per-section action UI,
  decision persistence (likely a new field on
  `TranslationValidationResult` or a separate decisions entity),
  Step 6 → Step 7 gate logic on both frontend and backend.

- **Permissive** — Review-state is informational. Users may
  publish past it. The current new-wizard behavior since today's
  wiring fix. Smallest chunk: maybe a UX message acknowledging
  the state, otherwise close as "intentional new behavior."

- **Strict-with-override** — A bulk acknowledgment ("I have
  reviewed these issues, publishing anyway") gates Step 7,
  without per-section decisions. Medium chunk: single
  acknowledgment widget, simpler gate logic, no per-section
  data model changes.

### Reference

Smoke screenshot 2026-06-14: section "Content Recognition and
Handling" with Score 81 / Review outcome, full back-translation
evidence rendered, A vs Original 80, B vs Original 76, A+B
agreement 96, consensus 81 (Marginal), 3/3 rounds, Safety
threshold 85, critical term "PPE" flagged. No decision UI exposed
to act on this state.

### Next step

Make the Strict / Permissive / Strict-with-override decision.
Once decided, status updates to Open with chosen direction
documented, and implementation chunk gets scoped from there.

---

## 30. External review user journey not characterized (P1)

**Priority:** P1
**Origin:** [Internal-QA]
**Status:** ✅ Done — 2026-06-17 — External review is now reachable from the new wizard's Validate step. Backend: state gate on `InitiateExternalReview` relaxed to accept `Validated` in addition to `ReviewerAccepted` — external review is part of the internal reviewer's workflow (escalation when a translation is too important or too uncertain to sign off internally), not a step after it. Frontend: per-language "Send for external review" button added to the Validate step's per-language row (above the section cards, visible when `workflowState === 'Validated' || 'ReviewerAccepted'`); `canSendForExternalReview` in `TranslationWorkflowPanel` updated to accept `Validated`; `SendExternalReviewDialog` reused from its existing location (not lifted — both callers use the component directly from `components/SendExternalReviewDialog.tsx`). `ExternalReviewWarningBanner` on the Publish step is now reachable for the first time and verified correct (copy, pluralization, non-blocking). Side effects: §11 closed (see entry), §21 partially addressed via Edit page path (detail page path blocked on §24). Recon: `docs/phase-5/reports/30-external-review-discoverability-recon.md`. Fix: `docs/phase-5/reports/30-external-review-per-language-fix.md`.
**Surfaced:** 2026-06-15 stocktaking discussion.

Backend infrastructure for external (third-party) review exists and is tested:

- POST .../translations/{lang}/initiate-external-review (transitions Validated/ReviewerAccepted → AwaitingThirdParty)
- POST .../translations/{lang}/cancel-external-review (reverts)
- ExternalReviewController handles reviewer-token submissions
- New wizard's Publish step renders AwaitingThirdParty warning banner

But during 2026-06-15 smoke of the new wizard, no UI path to initiate external review was found.

Open investigation:

- Does the talk detail page (TranslationWorkflowPanel) expose initiate-external-review? Per yesterday's strict-review recon, this is where the action was expected to live. Confirm or refute.
- If UI exists on talk detail page, is it discoverable from the new wizard's flow? Currently the wizard's Publish step navigates to the talk detail page on success, but does the user know external review lives there?
- If UI doesn't exist anywhere, this is a feature parity gap with whatever workflow the backend was built for.

Once investigation completes, this entry either closes (UI exists and is reachable) or becomes a sized implementation chunk (build the UI surface).

Related: §23 (Strict review workflow port, now closed) explicitly punted external review actions to a separate effort. This is that effort.

Investigation 2026-06-15: UI exists on the talk detail page via TranslationWorkflowPanel.tsx. Backend, API client function, and React hook are all in place. The gap is journey/discoverability, not missing UI. A user who creates a talk through the new wizard reaches the Publish step and then the talk detail page on publish success — but the new wizard makes no reference to external review as an option. The user has to know external review exists and find their way to the panel. Narrows the entry from "build the UI" to "make external review discoverable from the new wizard's flow."

---

## 31. Translation completion notification gap (P1)

**Priority:** P1
**Origin:** `[Internal-QA]`
**Status:** Done — 2026-06-17. Email-only implementation shipped: `IToolboxTalkNotificationService` + 4 toggle settings in `ToolboxTalkSettings` + frontend Notifications tab in Settings. See `docs/phase-5/reports/31-translation-completion-notifications-fix.md`. Two follow-on items logged: §7.4 (user-ID propagation gap) and §7.5 (email template extraction).
**Surfaced:** 2026-06-15 stocktaking discussion.

Phase 5 standards committed to "translations run in the background." The implementation records translation completion via `ITranslationWorkflowService.RecordTranslationCompleted` (called from `GenerateContentTranslationsCommandHandler.cs:167`) and validation completion via `RecordValidationCompleted` (called from `TranslationValidationJob.cs:362`). Both maintain the internal workflow state machine.

**Confirmed gap (investigation 2026-06-15):** No notification path — email, in-app, or otherwise — is invoked on either event. Confirmed by grep across `src/`: zero hits for translation-completion-tied notification patterns.

This means:

- User on the wizard's Translate step receives live SignalR updates: works.
- User who navigated away (other admin page, dashboard) is not notified.
- User who closed the browser is not notified.
- User who started a long-running multi-language translation has no way to know when it finishes without polling the system manually.

For typical translation runs (5-30 minutes for multi-language with validation), this is a significant workflow friction.

### Design needed

- In-app notification surface: notification center? Toast on next page load? Badge on the talk in the list? Combination?
- Out-of-band email: triggered on translation completion? Validation completion? Both? Per-language or once-all-complete?
- Per-tenant or per-user notification preferences?
- Failure notifications too — if a translation fails, the user should also be notified.

### Implementation shape (estimated)

- Backend hook: `RecordTranslationCompleted` and `RecordValidationCompleted` (and presumably failure paths) get a notification dispatch added.
- Notification dispatcher: routes to email + in-app channels per user/tenant preference.
- Email templates: translation completion, validation completion, failures.
- In-app UI: notification center or badge surface (depends on whether one already exists or needs building).
- Tenant settings: notification preferences.

Likely 3-5 day chunk if a notification framework needs building from scratch; 1-2 days if MailerSend + existing infrastructure is enough.

### Related

- `MailerSendEmailProvider` (§5.6 — 429 handling) is the email infrastructure. Notifications would use it.
- §1.3.5 (Long-running job UX — fire-and-notify pattern) is the broader version of this — it covers bulk import, content generation, validation, corpus runs. This entry is the translation-specific instance of the same gap.

---

## 24. Edit workflow for new-wizard talks (P0 — design specified, implementation chunks to scope)

**Priority:** P0
**Origin:** [Internal-QA]
**Status:** Open — design rules locked 2026-06-15; sub-tasks to scope as separate chunks.
**Note (2026-06-16):** §5.27 toggle infrastructure is now deployed (defaulted to legacy wizard). The `UseNewWizard` TenantSetting is ready to flip per-tenant as soon as this §24 work reaches a defensible production state. No tenant should be toggled to `"true"` before Chunk 1 (talk detail edit UI) and Chunk 3 (validate step on talk detail) are in place.
**Surfaced:** 2026-06-15 stocktaking discussion.

**Note:** Earlier draft of this entry (previously §29) merged into this entry on 2026-06-15 — the locked design rules subsume the open-questions framing of the earlier draft.

### Design rules (locked)

1. **Edit UI lives on the talk detail page.** Not in the wizard. Uses new UI elements. Calls into wizard backend (commands, services) where applicable.
2. **Source content edit cascades to translations.** All translations of the edited section across all languages are marked `NeedsRevalidation`. User is prompted to re-translate.
3. **Quiz element edit cascades to translations.** Same as source content: re-translate the edited element across all languages.
4. **Reviewer-accepted translations are terminal unless source is edited.** When source is edited, translations become invalidated and re-translation is required.
5. **Adding new target languages post-publish is supported.** Source unchanged: translate the single new language only. Source changed: same flow as full re-translation.
6. **Removing target languages post-publish:** TBD — design rule not yet stated.
7. **Post-publish translation edits go through the validate-step accept-edit-retry UI** (the §23-ported component), accessible from the talk detail page. Creates a fresh validation run; same reviewer-decision flow as during initial creation.
8. **Publishing with stale translations:** TBD — blocked, warned, or allowed? Suggested: warned with non-blocking banner (same shape as AwaitingThirdParty), since users may intentionally publish source-only updates before retranslation.

### Open design sub-questions

- Rule 6: removal of target languages — UI exists or doesn't?
- Rule 8: stale translation publish gate — block, warn, or allow?
- Recovery flow when re-translation fails mid-process post-publish (in-flight failure on a published talk).
- Concurrency: what if a third-party review is in flight (AwaitingThirdParty) when source edit happens? Cancel the review automatically? Block the edit? Warn?
- Audit trail: edits to published talks — what gets logged, where, who sees it?
- In-progress draft vs published talk: what does "Edit" mean differently for a talk row that exists and has had no validation started (draft mode post-Step 4, pre-Publish) versus one that has been published? Determines whether the edit UI surfaces any "resume wizard" affordances for drafts.

### Implementation shape (estimated)

Substantial work, decomposable into chunks:

- **Chunk 1:** Talk detail page edit UI — section content editing surface, dirty-state, save-and-cascade flow.
- **Chunk 2:** Translation re-run UI — surface the wizard's translation step UI on the talk detail page, scoped to specific languages.
- **Chunk 3:** Validate step UI on talk detail page — same component as wizard, reachable post-publish.
- **Chunk 4:** Quiz editing UI — element edit with cascade to translations.
- **Chunk 5:** Add new language post-publish flow. ✅ Shipped 2026-06-18.
- **Chunk 6:** Stale translation handling (banner, gate policy from rule 8).

Each chunk is recon-first.

### Why P0

Without edit, the new wizard is creation-only. Tenants toggled to the new wizard (Phase 5.6) lose the ability to fix published mistakes — including ones the system itself produces (a translation that reviewers later flag as wrong, a typo discovered post-publish). That's not a defensible release state.

Phase 5.6 (cutover toggle) should not enable any tenant for production rollout until at least the highest-priority sub-chunks of this work are shipped. Recommended minimum before any rollout: chunks 1, 2, 3, and the Rule-8 stale gate (chunk 6).

**All 6 chunks shipped 2026-06-18. Demo cut and toggle-flip cut both engineering-complete. §24 fully closed.** §5.27 (cutover toggle) is now engineering-unblocked — operational decision on tenant flip-timing is separate.

---

# ==================================================================

# Recently Closed

# ==================================================================

Kept here for trail; prune periodically.

## 5 June 2026 — BACKLOG 3.1: Unify user creation (shipped to Production)

- **3.1** — Three user-creation paths unified on the bulk-import pattern. PATH A (UI user-create, `UserService.CreateAsync`) and PATH B (tenant onboarding, `TenantOnboardingService.CreateAdminUserAsync`) now generate throwaway passwords internally, set `EmailConfirmed = false`, generate a password-reset token, and send `SendPasswordSetupEmailAsync` — same as PATH C (bulk import). Admin no longer supplies passwords; new users go through set-password flow on first login. Password field removed from admin user-create form (`CreateUserDto`, `CreateUserValidator`, frontend `user-form.tsx`). PATH A and PATH B verified end-to-end on Development; PATH C unchanged. Commit `bb2709e` (transval) → merged to main via `5d12808`.

## 4 June 2026 — Wizard cascade-reset hardening + UAT 1.1.6/1.1.7/1.1.8/1.1.9 (shipped to Production)

### UAT batch

- **1.1.6** — Continue button wedged after empty languages; `videoRightsConfirmed` persisted in wizard state to survive InputConfigStep remount
- **1.1.7** — Inline section body editor at parse step; user-facing consent for section re-edits (inline notice + confirmation dialog when editing past Parsed); snapshot-based change detection; backend allowlist widened to Parsed/QuizGenerated/Validated with cascade reset of downstream artefacts; in-flight statuses rejected; step-indicator hard-block on validationRunIds null
- **1.1.8** — Parse-step edits preserved across back-nav (sessionSourceSnapshot pattern); ParseStep render guard restoring hasParsed on remount when wizard state populated
- **1.1.9** — Pending state in PreviewModal when slideshow expected but not yet generated

### Lifecycle hardening (CONTENT-LIFECYCLE §6.x — all closed)

- **§6.4** — Orphaned `TranslationValidationJob` cancellation: `TranslationJobIds` column added to session (migration `20260604111150_AddTranslationJobIdsToContentCreationSession`); `IBackgroundJobClient.Delete()` called for stale jobs on re-enqueue; two-layer defence with in-job relevance guard at top of `GenerateTranslationForSectionsAsync`
- **§6.5** — Session no longer silently stuck in `TranslatingValidating`: `GenerateTranslationForSectionsAsync` catch block now marks `TranslationValidationRun.Status = Failed` before returning null
- **§6.10** — `UpdateQuestionsAsync` no longer silently demotes Validated → QuizGenerated: mirrored cascade-reset pattern from `UpdateSectionsAsync` (Validated → Parsed with full downstream invalidation, consent dialog on QuizStep, in-flight disable)
- **§6.11** — `ConfirmUploadAsync` no longer resets Draft from any status: Draft-status guard added matching `UploadFileAsync` pattern
- **§6.2 (partial closure)** — Eight `.Remove()` sites converted to `ExecuteDeleteAsync` to bypass the `SetAuditFields` interceptor that was silently soft-deleting them: ToolboxTalkSlideshowTranslation (1 site), ToolboxTalkQuestion (3 sites), ToolboxTalkTranslation (2 sites), ToolboxTalkScheduleAssignment (2 sites — with `Detach` to suppress EF orphan-removal phantom soft-delete). Structural close — filtering the unfiltered unique indexes — remains BACKLOG (Path-B candidate)

### Lifecycle document

- `docs/CONTENT_CREATION_LIFECYCLE.md` introduced as source-of-truth state map (commit `45ca98d`); verified end-to-end; consolidated update post-batch (commit `25c73e9`) closing the four sharp edges above, adding §6.12 SetAuditFields interceptor as system-wide sharp edge, §4.5 ToolboxTalkSlideshowTranslation entry, §4.6 TranslationValidationRun field-by-field, Option B annotations on §4.3/§4.4 mechanism corrections, and Rule 8 in §9 (Remove() vs ExecuteDeleteAsync discipline)

## 3 June 2026 — UAT P0s + tenant-filter sweep batch (shipped to Production — pending)

- **1.1.1** Validation summary "Score / Sections passed" twin-metric display + conditional badge tinting (closes 1.1.12 as side effect)
- **1.1.2** EN-from-targets filter + labelled "Back-translation scores" display + frontend index-misalignment fix
- **1.1.3** Edit English source and re-validate from Validate step (with disclosed formatting-loss limitation tracked as 1.1.18)
- **AuthService tenant-filter bypass** — silent bug closed: `GenerateAuthResponseAsync` was returning empty `enabledModules` array for every login/refresh because the global tenant filter conflicted with the explicit user.TenantId predicate in an unauthenticated context
- **Note 21 interface-enqueue fix** — `ContentExtractionService` subtitle retry path now enqueues via concrete class
- **Tenant-filter forensic audit (BACKLOG 5.1)** — completed; 6 anonymous endpoints + 18 Hangfire jobs + ~120 queries inspected, 2 issues found (above), both fixed
- **Migration forensic audit (BACKLOG 5.3)** — completed; all 90 migrations confirmed structurally sound, 5 synthetic-timestamp migrations identified but functional
- **CLAUDE.md hygiene** — Note 28 (CLI-only migrations) added, 12 resolved notes archived to CLAUDE-archive.md (~80 lines saved), Pipeline Audit entity invariants captured inline
- **Build-time migration guard (BACKLOG 5.2)** — two `[Fact]` tests in `MigrationStructureTests.cs` verify every migration has a matching Designer.cs with valid `[Migration]` attribute. Deliberate-failure verification proved checks would have caught all four real instances this week.

## 2 June 2026 — "Translator polish" batch (shipped to Production)

- **1.1.4** Slideshow counter mismatch — prompt change + bridge removal
- **1.1.5** Slideshow Back button — same fix as 1.1.4
- **1.1.10** Verbatim parse mode — full feature shipped end-to-end
- **1.1.14** Slide WCAG contrast — prompt change applied to all three blocks
- **1.1.15** Audience-aware quiz generation — full feature shipped (Operator / Supervisor / Auditor)
- Section divider styling — wizard sections darker/more legible across all steps
- Designer.cs restoration for `AddEmployeePinFields`, `AddQrLocationAndQrCode`, `AddQrSession` migrations
- `AddAudienceRoleToContentCreationSession` migration (with Designer.cs from CLI)
- `AddPreserveSourceWordingToContentCreationSession` migration (with Designer.cs from CLI)

## 28 May 2026 — Pre-demo fixes (shipped to Production)

- Migration Designer.cs missing for `AddGlossaryCorrectionsToTranslationValidationResult`
- QR sessions summary 500 (EF Core 9 LINQ translation failure)
- QR scan language selector disconnected from backend — removed entirely
- QR location edit dialog not pre-filling form
- Welcome email not sent on UI user creation (targeted patch — proper unified flow remains as 3.1)
- Subtitle processing job stuck at 0% (tenant filter trap)
- QR video display (single-talk only; course branch deferred)
- QR video subtitles — backend tenant filter + frontend CORS + endpoint addition
- PIN visible to SuperUser + Admin on employee detail (with plaintext storage decision recorded as 6.1)

---

## 21. Post-publish translation management UI — AwaitingThirdParty languages (Medium)

**Status:** Partially addressed as side-effect of §30 (2026-06-17). The Edit page path now works end-to-end: `TranslationWorkflowPanel` shows "Cancel invitation" for `AwaitingThirdParty` languages (existing button, now reachable since `AwaitingThirdParty` is reachable). Admin can navigate Talk detail → Edit → scroll to Content Translations card → cancel. The detail page path (no `TranslationWorkflowPanel` on the detail page itself) remains blocked on §24 (Chunk 2 — detail page translation surface).

**Surfaced by:** 5.5a gap-check, 2026-06-14.

When a talk is published while one or more translations are in `AwaitingThirdParty` state (external review in flight), no UI path exists to manage those translations post-publication.

**Backend is complete:** The cancel-external-review endpoint (`POST /api/toolbox-talks/{id}/translations/{languageCode}/cancel-external-review`) exists and works — it reverts `AwaitingThirdParty` → `ReviewerAccepted`. After cancellation, `POST /{id}/translations/generate` proceeds normally. The `PublishToolboxTalkCommandHandler` imposes no lock on translations.

Note: BACKLOG §11 ("Cancel external review — end-to-end") has been updated — the backend is confirmed complete as of 2026-06-15.

**Gap:** The talk detail page (being built in 5.5b) does not surface a "Cancel external review" or "Re-translate" action for languages in `AwaitingThirdParty` state on a published talk. Admins who publish and later want to replace or fix an in-flight external review have no UI path.

**Fix direction (when in scope):**

- On the per-language panel of the talk detail/edit page, show a "Cancel external review" button when `workflowState === 'AwaitingThirdParty'`
- On success, transition the language chip to `ReviewerAccepted` and expose a "Re-translate" button
- No new backend endpoints needed

---

## 23. Reviewer-action UI missing on Validate step (consolidated)

**Priority:** P1
**Origin:** [Internal-QA]
**Status:** ✅ Done — 2026-06-15 — Strict review workflow ported with backend enforcement, auto-accept Pass, no-bypass design. Cache invalidation follow-up fix shipped 2026-06-15. Smoke verified across Scenarios 1, 2, 3 (see `docs/phase-5/reports/strict-review-workflow-port.md`).
**Surfaced:** 2026-06-14, during wizard-skip-fix smoke (Scenario C) and 5.5b smoke Scenario 3 setup.
**Supersedes:** §20 (Per-section accept/reject UI gap, 2026-06-14).
**Related:** §21 (Post-publish translation management UI — AwaitingThirdParty languages, talk detail page).

The new wizard's Validate step (and its detail pages) ports the full display of validation outcomes — scores, back-translations, consensus calculations, safety threshold, critical terms, regulatory scoring panel — but ports none of the reviewer-action UI from the old wizard. The user can read everything; they can act on nothing.

Three actions are documented as missing in the validate-step UI:

1. **Initiate external review.** No UI to send a Validated/Verified translation to a third-party reviewer. Backend endpoint exists: `POST /api/toolbox-talks/{id}/translations/{lang}/initiate-external-review`. Surfaced 2026-06-14 while attempting smoke Scenario 3 — could not drive the talk into AwaitingThirdParty state through the wizard.

2. **Accept/reject per section.** No UI to resolve a Review-state section outcome. Surfaced 2026-06-14 during Scenario C smoke. Old wizard reference: `create-wizard/steps/ValidateStep.tsx:186` gates continuation on `allSectionsDecided`.

3. **Accept translation as final.** No UI for the `POST .../translations/{lang}/accept` endpoint that transitions ThirdPartyReviewed → Accepted. Related to action 1 above.

A fourth action — cancel external review for languages already in AwaitingThirdParty — is tracked separately as §21 (talk detail page, post-publish path). Same root cause: backend endpoint exists, no UI exposes it.

### Product decisions required before implementation

These actions need product positions before scoping. Some positions interact — e.g., if external review is genuinely optional/vestigial, action 1 staying missing is fine; if it's a real workflow, all three are required.

- **Per-section review resolution** (action 2): Strict (must resolve all Review-state before publish), Permissive (informational, can publish past), or Strict-with-override (single ack covers all).
- **External review workflow** (actions 1 and 3): if the workflow is meant to be used, both need UI. If it's Phase 4 work that hasn't shipped yet, both can stay missing for now, with a small notice or placeholder added so users know it's unavailable rather than broken.

### Implementation scope (depends on decisions)

- If reviewer actions are intended as workflow features: substantial chunk — per-section action buttons, decision persistence (likely new fields on TranslationValidationResult or a separate decisions entity), external-review initiate/accept UI on the validate detail page, plus the cancel UI from §21. Estimated 3-4 days.
- If Permissive on action 2 and external review is Phase 4 work: small chunk — placeholder notices, close the rest as "intentional behavior for now." Estimated half a day.

### Reference

Screenshots 2026-06-14: validation run detail page for "Validation Run — NL" with full read-side detail (Run Details panel, Score 89%, section L01 with Review outcome and full back-translation evidence, L02 with Pass outcome and Verified consensus, Regulatory Score panel). No actions other than regulatory-scoring buttons. No path to initiate external review or to resolve section review state.

5.5b smoke Scenario 3 (AwaitingThirdParty warning banner runtime smoke) is blocked on this entry — Scenario 3 cannot be driven through the UI until the initiate-external-review action exists. Banner code itself is verified at `PublishStep.tsx:434-446` (correct copy, correct pluralization, includes language-list interpolation).

---

## 25 — New wizard visual polish to parity with legacy

Priority: P0 — blocks §5.7 (Demo refresh) closure and blocks the wizard cutover toggle being flipped for paying tenants.
Origin: [Engineering] [Demo refresh discovery 2026-06-18]
Status: ✅ Done — 2026-06-19 — Closed via six chunks. Outer step container shell (Chunk 6) closed the final visible gap. New wizard now at visual parity with the legacy across all seven steps plus the §24 detail panels. Demo cut (Chunks 1-4) unblocked §5.7 partially; full closure on §5.7 now also requires §26 (closed same day). Chunks: 1 InputConfigStep, 2 ParseStep+QuizStep, 3 SettingsStep, 4 StepIndicator subtitles, 5 detail panels, 6 outer shell. Reports: docs/25/recon.md, docs/25/chunk-{1-6}-recon.md, docs/25/chunk-{1-6}-fix.md.
Surfaced: During §5.7 Demo refresh smoke verify. Side-by-side comparison of the new wizard against the legacy revealed the new wizard ships with significantly less visual polish — flat field stack vs the legacy's card-based layout, no subsection markers, tile selectors not styled, no helper text patterns, etc. Functional but visibly unfinished.

Scope: Bring the new wizard's visual layer to at minimum legacy parity. Cards, subsection markers, tile selectors, toggle patterns, helper text, step indicator subtitles. All wizard steps (Input & Config, Parse, Quiz, Settings, Translate, Validate, Publish) plus the §24 edit panels on the talk detail page (SectionEditPanel, QuizEditPanel, SettingsEditPanel, AddTargetLanguagePicker).
Blocks:

§5.7 closure (Demo can't be declared done with the wizard looking like a draft)
Wizard cutover toggle being flipped for paying tenants (cutover at this visual standard generates "why does the new one look worse" feedback)

Definition of done:

New wizard renders with visual polish at parity with legacy
Both wizards screenshotted side-by-side in the implementation report for verification
Phase 7.4–7.6 smoke verify on Demo passes end-to-end
§5.7 unblocked and closed
Cutover toggle ready to flip for real tenants

---

## §26 — Wizard navigation & cancellation parity

- **Priority:** P1 — surfaces real ghost-data bug; affects user trust in the new wizard
- **Status:** ✅ Done — 2026-06-19 — Closed via three chunks. Chunk 4 was reserved during scoping for items surfaced during implementation; nothing warranted it. Chunk 1: backend parse-job IsDeleted guards (zombie-revival bug fix). Chunk 2: Cancel affordances on Steps 1 (non-destructive, form-state discard) and 2 (destructive, soft-delete via useDeleteToolboxTalk). Chunk 3: Regenerate All confirmation dialog on Step 3 + isNavigating prop fix on settings/page.tsx. The Re-parse dialog originally listed in Chunk 3 was found already-shipped via §25 Chunk 2; BACKLOG scope corrected during Chunk 3. Reports: docs/26/chunk-{1-3}-recon.md, docs/26/chunk-{1-3}-fix.md.
- **Origin:** Post-§25 navigation recon (`docs/25/post-close-navigation-recon.md`) and parse-handler investigation (`docs/parse-handler-cancellation-investigation.md`), both 2026-06-19
- **Predecessors:** §24 (Edit workflow), §25 (Visual polish — closed 2026-06-19)

### Context

The new wizard ships at visual parity with the legacy (§25), but a comparison of navigation and action surfaces found five real gaps and one latent backend bug. The headline finding: video-mode parse jobs do not respect soft-deletion of their target talk row, producing zombie talks (deleted in DB, with sections still being written by the AI handler) — a bug that activates any time a video-mode talk is deleted mid-parse, including via the planned Cancel feature, drafts-list deletion, or admin actions.

The wizard's async philosophy (validated as intentional design during the navigation recon) is preserved throughout: no synchronous "must wait here" gates, async work continues server-side, user has agency to move freely. The gaps being addressed are the user-agency affordances missing from that model: the ability to explicitly abandon a creation attempt without leaving server-side debris.

### Scope (four chunks)

**Chunk 1 — Parse jobs tolerate soft-deleted talks (backend)**

- `VideoTranscriptionJobForTalk` and `ContentCreationParseJobForTalk` add an `IsDeleted` check after talk load, matching the convention in `TranslationValidationJob:990-992`.
- Preferred shape: extend the predicate to `&& !t.IsDeleted` rather than an explicit if-block — matches the existing pattern.
- ~4 lines net diff across two files.
- Prerequisite for Chunk 2's Cancel-mid-parse to work without zombie data.
- Out of scope: aborting in-flight AI API calls (transcription and Claude both run to completion at provider end; the fix prevents the write, not the spend). Tracked separately if cost optimisation becomes worthwhile.

**Chunk 2 — Cancel affordances (frontend)**

- Cancel button on Step 1: discards local form state, confirmation dialog, navigates to `/admin/toolbox-talks/talks`. No backend involvement (no talk row exists yet).
- Cancel button on Step 2 (and arguably any draft state from Step 2 onward): confirmation dialog, calls `DELETE /api/toolbox-talks/{id}`, navigates to talks list.
- Both use shadcn `AlertDialog`, matching the convention from §25 Chunk 2's Re-parse dialog and the existing `DeleteDraftDialog.tsx`.
- Depends on Chunk 1 landing first — without it, Cancel on Step 2 during a video parse produces zombie data.

**Chunk 3 — Regenerate All confirmation dialog + settings nav prop**

- _Scope correction (2026-06-19, post-§26-Chunk-3-recon):_ The Re-parse confirmation dialog originally listed in this chunk was found to already be shipped — it landed in §25 Chunk 2 alongside the Re-parse button itself. The post-§25 navigation recon misread the file. Confirmed by §26 Chunk 3 recon: `ParseStep.tsx` lines 62, 116-128, 314-330 contain the full implementation. Re-parse item removed from this chunk's scope.
- Add confirmation dialog on Step 3 Regenerate All in `QuizStep.tsx`. Currently the button fires the regenerate mutation directly with no warning; should match the pattern of the already-shipped Re-parse dialog. Non-destructive `AlertDialogAction` styling (recoverable action, not permanent loss). Trigger only when questions exist (the empty-state Generate Quiz and the error-state Retry buttons stay un-gated — nothing to lose in either case).
- Fold in: settings/page.tsx pass `isNavigating={isNavigating}` to `WizardLayout` (one-line fix sourced from the existing `useStepNavigation` hook). Prevents Back button double-action during step save.
- Independent of Chunks 1 and 2.

**Chunk 4 — (reserved if needed)**

- Originally planned as separate `isNavigating` fix; folded into Chunk 3.
- Reserved for any item surfaced during Chunks 1-3 implementation that warrants its own chunk.

### Out of scope

- Back from Step 2 (intentional design decision — talk row exists at end of Step 1, "going back" is structurally ambiguous; StepIndicator + Cancel cover the user intents).
- Translate Step 5 Continue gate (intentional async design — user can advance to Validate before translations complete; Validate handles progressive rendering correctly).
- "Preview as Learner" on Step 7 (preview accessible via talk detail page after publish; wizard-step preview is nice-to-have, not regression).
- Aborting in-flight AI API calls server-side (separate concern; would reduce cost on cancel but isn't required for correctness).
- Reaper for orphaned drafts created by browser-close / network-drop scenarios (separate concern from explicit Cancel).

### Definition of done

- All four chunks shipped, each with recon and fix reports
- §5.7 Demo refresh (which has been blocked / unblocked / blocked again through §25's cycles) can run against a wizard with full navigation parity
- Wizard cutover toggle (`UseNewWizard`) ready to flip for paying tenants without known UX dead-ends

### Risks

- Chunk 1's fix doesn't prevent the AI API cost — only the write. If this is unacceptable (e.g., a tenant runs many cancels and the cost adds up), the deeper "abort AI calls server-side" work surfaces as a separate chunk.
- The intentional new-wizard design decisions (Back from Step 2 absent, no Translate gate) may surface as confusion from users habituated to the legacy. Worth monitoring but not addressing pre-emptively.

---

_End of BACKLOG.md._
