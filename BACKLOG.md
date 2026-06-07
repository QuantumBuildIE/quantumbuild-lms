# CertifiedIQ — Backlog (Source of Truth)

**Last updated:** 3 June 2026
**Purpose:** Comprehensive record of every known item — bug, feature, refactor, product decision — across the CertifiedIQ LMS. This is the long reference. For the active prioritised list, see `SPRINT.md`.

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

  **Known limitation:** the propagation flattens HTML structure (lists, headings) to plain paragraphs because the reviewer edits a plain-text projection of the HTML source. To prevent stored XSS, the propagated text is HTML-encoded and wrapped in `<p>` elements (one paragraph per non-empty line). The amber warning discloses this: *"Editing the source affects all translations of this section, and may simplify the formatting of structured content (lists, headings) into plain paragraphs."* Long-term fix tracked as **1.1.18** (rich-text editor).

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
- **Status:** Open
- **Description:** Click Next before choosing target languages → Continue stays disabled even after languages filled. Only a hard refresh recovers it.
- **Cause:** `canContinue` doesn't require `targetLanguageCodes.length > 0`. `createSession.mutateAsync` runs without languages; mutation's pending state + missing languages combo wedges button.
- **Files:** `InputConfigStep.tsx:263-271, 273-340, 922-930`
- **Fix direction:** Add languages check to `canContinue`. Wrap `handleContinue` body in try/finally. Optionally include `targetLanguageCodes` in `createSession` payload so backend rejects empty.
- **Acceptance:** Filling a missing required field re-enables Continue with no refresh.

#### 1.1.7 Section content not editable after parse
- **Priority:** P1
- **Origin:** `[UAT]`
- **Status:** Open
- **Description:** Sections can be renamed and reordered, but body is read-only. To fix wording the user must delete and start over.
- **Cause:** Body rendered via `dangerouslySetInnerHTML`. `onChange` only fires for title/delete/reorder.
- **Files:** `SectionList.tsx:289-306, 320-328, 41`
- **Fix direction:** Inline body editor (textarea or contenteditable). Save on blur via existing `onChange`. Mark each edited section with manual-override flag so re-parse doesn't stomp it.
- **Acceptance:** Body editable; edit survives going back and re-entering.

#### 1.1.8 Back-nav from Parse loses deletions, forces full re-parse
- **Priority:** P1
- **Origin:** `[UAT]`
- **Status:** Open
- **Description:** Delete section → Back → return → re-parse forced including the wait. Quiz settings, by contrast, preserved.
- **Cause:** `hydrateFromSession()` overwrites client `parsedSections` with stale backend `parsedSectionsJson`. InputConfig step explicitly clears `parsedSections: []` on return Continue ("reset to Draft for re-parsing").
- **Files:** `ParseStep.tsx:131-136, 182-202`, `InputConfigStep.tsx:303-313`
- **Fix direction:** Persist Parse edits to backend (extend `parsedSectionsJson` or add `editedSectionsJson`). Skip hydrate when client state non-empty. Only clear on InputConfig return if source content actually changed.
- **Acceptance:** Parse state persists across back-nav like Quiz state does.

#### 1.1.9 Preview as Employee — no slideshow after navigating back and forth
- **Priority:** P1
- **Origin:** `[UAT]`
- **Status:** Open
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
- **Status:** Open
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
  - **Translation-source structural mismatch** — if a reviewer changes the *structure* of the source (adds a bullet, removes a heading), the translation's structure may no longer match. Use the existing `NeedsRevalidation` flag (added in 1.1.3) to mark translations as stale; surface this to the reviewer in a follow-up UI prompt.
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
- **Fix direction:** Add edit dialog. `CodeToken` immutable (preserves printed codes). Display name, ContentMode, assigned talk/course editable. Optionally clarify in UI that changing the assigned content updates what people see *without* requiring a reprint.

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

A new tenant-scoped entity `Asset` representing a piece of equipment, vehicle, or fixed object that training can be associated with — analogous to an Employee but for *things*. Used to deliver and track training tied to specific equipment rather than specific people.

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
- No `PreferredLanguage` (single) — assets *support* multiple languages but don't have a personal preference
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
- **Asset → schedule.** Can a learning be *scheduled* on an asset (analogous to `ScheduledTalk` on employees)? Or are assets only ever scanned-into-via-QR? v1: scan-only. Scheduling can come later.
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
- **Status:** Open
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

#### **3.11 (new)** — Tenant creation: 400 "already exists" returned on first submit. Submitting the Create Tenant form once produces an HTTP 400 with "A tenant with this name already exists", but the tenant row is actually created. Possible double-submit (form fires POST twice; first creates, second hits uniqueness check) or misleading error from post-commit exception during admin-user creation. Predates 3.1. Repro: submit Create Tenant form with a fresh unique name, observe 400 in browser network tab, query DB to confirm row exists.

#### **3.12 (new)** — New user activation timing question. New users are created with `IsActive = true` despite not being able to log in until they complete the invitation flow (set password). Product question: should `IsActive` reflect "account fully set up" or "admin has approved this account"? Needs explicit decision.

#### **3.13 (new)** — Testing discipline: admin should be tested in a separate browser profile from end-user verification flows. Same-origin localStorage means token state from a user-login can clobber an admin's token state silently. Caused a 403 cascade during 3.1 verification on 5 June. Documentation/discipline note rather than code change.

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
- **Status:** Open
- **Description:** Three production bugs from the same root cause have surfaced (bulk import, subtitle processing job, subtitle content fetch from QR). All silent — query returns nothing because `ICurrentUserService.TenantId` returns `Guid.Empty` in Hangfire jobs and `[AllowAnonymous]` endpoints. Each instance found by user-observed symptom, not by testing. There are very likely more.
- **Fix direction:** Systematic audit: every query against a tenant-filtered entity that runs in a Hangfire job OR an unauthenticated endpoint must either use `IgnoreQueryFilters()` with explicit predicates, OR take an explicit `bypassTenantFilter` parameter. Grep all `[AllowAnonymous]` controllers and all Hangfire job classes; cross-reference every DB query inside them.
- **Closes:** Prevents the fourth instance.

#### 5.2 Migration creation process & Designer.cs guard
- **Priority:** P1 (escalated from P2 — 2 Jun 2026)
- **Origin:** `[Production-incident]` `[Engineering]`
- **Status:** Open
- **Description:** **Four documented instances** of missing Designer.cs files causing silent migration skips this session alone (one production bug `AddGlossaryCorrectionsToTranslationValidationResult`, plus three drift cases discovered while investigating, plus one new migration `AddAudienceRoleToContentCreationSession` that broke Development on push).
  Root cause confirmed: migrations have been routinely **hand-written** rather than generated via `dotnet ef migrations add`. The CLI generates both the `.cs` AND `.Designer.cs` files together; hand-writing the `.cs` alone results in EF silently skipping the migration on every startup because the `[Migration]` attribute lives on the Designer.cs partial class declaration.
- **Fix direction (two parts):**
  - **Process change:** all migrations must be created via `dotnet ef migrations add <Name>`, never hand-written. Add explicit directive to CLAUDE.md so Claude Code follows it on every future schema change prompt.
  - **Build-time guard:** check that every migration `.cs` file has a matching `.Designer.cs`. Fail build if mismatched. Catches both accidental hand-writing and future deletions.
- **Closes:** Prevents recurrence of the silent-skip pattern that's cost ~3 hours of debugging this week.

#### 5.3 Migration forensic audit — find any remaining missing Designer.cs files
- **Priority:** P2
- **Origin:** `[Engineering]`
- **Status:** Open (new — 2 Jun 2026)
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

*Phase 0 — Preparation:*
1. Create `demo` branch off `main`, immediately merge `transval` (main is behind by Designer.cs restoration commits)
2. Collect env var values from Development Railway service
3. Generate fresh JWT secret for Demo
4. Provision Demo PostgreSQL database on Railway

*Phase 1 — Railway Reconnect:*
5. Reconnect Demo API service to GitHub (branch: `demo`)
6. Reconnect Demo web service to GitHub
7. Set all env vars on Demo API
8. Set `NEXT_PUBLIC_API_URL` on Demo web service
9. Verify CORS origin is exact match (no trailing slash)

*Phase 2 — Database Migration:*
10. Run migrations locally against Demo DB via `dotnet ef database update`
11. Confirm migration applied cleanly (top entry should be `AddPreserveSourceWordingToContentCreationSession`)

*Phase 3 — First Deploy:*
12. Trigger first manual deploy of Demo API; watch Railway logs for: migrations (0 pending, already applied), seeder (creates System tenant + SuperUser + roles + permissions), Hangfire init
13. Deploy Demo web service

*Phase 4 — Demo Tenant and Test Data:*
14. Create Demo tenant ("CertifiedIQ Demo"); note UUID (R2 folder prefix)
15. Create Demo Admin user under Demo tenant
16. Assign at least one sector to Demo tenant
17. Create one demo toolbox talk for smoke testing

*Phase 5 — Smoke Test and Handoff:*
18. Execute smoke test checklist (15 items: login, token refresh, user creation, talk creation, employee completion, CORS, TransVal, QR setup, QR scan, Skills Matrix, bulk import, certificates, Help Assistant, R2 isolation)
19. Re-test User Creation page
20. Update CLAUDE.md deployment section with three-tier workflow

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

# 7. Design review: Auditor audience role on ContentCreationSession.

Added during Ryan's UAT review as an AudienceRole value alongside Operator and Supervisor on quiz generation. Implementation generates different questions per audience string.

Problem: Auditor is not an identity role in the system (production has SuperUser/Admin/Supervisor/Operator). There is no mechanism to assign a quiz to an Auditor user because no such user can exist.
The "generate questions for Auditor audience" code path has no consumer — the questions cannot be taken.

Two separable intents are conflated in the current design:
(a) Learner-facing quiz tone varies by identity role (Operator vs Supervisor — both are real users who take quizzes).
(b) Auditor-facing demonstration content — showing an external auditor "this is the kind of question we can generate" as evidence of training rigour, not as a quiz to be taken.

These are different features with different storage, different UI, and different access patterns. Forcing them into a single AudienceRole string was a misread of the UAT feedback.

Action: design review before more code is built on top of AudienceRole. Decide whether (a) and (b) are both in scope, drop the other, or rescope. Until then, no new features should branch on AudienceRole == "Auditor".

Related: see ContentCreationSession.cs (AudienceRole property), QuizGenerationPrompts.cs (audience text variations), any frontend dropdown surfacing AudienceRole.

---

# ==================================================================
# 7. Recently Closed
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

*End of BACKLOG.md. For active prioritised work, see `SPRINT.md`.*