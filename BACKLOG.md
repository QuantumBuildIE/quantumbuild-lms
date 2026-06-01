# CertifiedIQ — Backlog (Source of Truth)

**Last updated:** 28 May 2026
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
- **Status:** Open
- **Description:** Translation Progress panel shows green "100" score with caption "Validation complete: 0/4 passed, score 100%". Orange Review (n) badge appears below. Reviewer reads orange as error but can't tell what's wrong since score says 100. Two unrelated counters concatenated into one sentence.
- **Cause:** `PassedSections` counts only `Outcome == Pass`; `OverallScore` is arithmetic mean. A section can be `Outcome=Review` with `FinalScore=100` (e.g. when DeepL unavailable and Round 2 with Gemini lands 100 but downstream rules still mark Review).
- **Files:** `TranslationValidationJob.cs:221-226, 252-254`, `ConsensusEngine.cs:78-85, 198-211`
- **Fix direction:** Replace concatenated sentence with two labelled metrics. Drop orange tint on Review badge when `OverallScore >= PassThreshold`. Optionally surface provider-fallback messages.
- **Acceptance:** A reviewer with no compliance background can state in one sentence what action (if any) is required.

#### 1.1.2 Final score 100% EN / 80% RU contradicts upstream 100%
- **Priority:** P0
- **Origin:** `[UAT]`
- **Status:** Open
- **Description:** Per-step scores show 100 across the board, then final Translate step shows English 100% / Russian 80% with no explanation for the 20% drop.
- **Cause:** `SourceLanguage` hardcoded to "en" when creating validation runs. If "en" also appears in target list, an EN→EN validation run is created — trivial to score 100 — and Russian back-translation drops to 80 separately. Publish page shows `runDetail.overallScore` with no baseline label.
- **Files:** `ContentCreationSessionService.cs:571-593`, `PublishStep.tsx:520-552`
- **Fix direction:** Filter source language out of target list. If keeping EN row for audit completeness, label "Source — no back-translation required". Label per-language scores with their baseline (e.g. "RU back-translation consensus").
- **Acceptance:** Any score on publish page traceable to its screen/calculation. EN never appears as a translation target.

#### 1.1.3 No path to edit English source and re-validate
- **Priority:** P0
- **Origin:** `[UAT]`
- **Status:** Open
- **Description:** Reviewer can Accept / Reject / Edit a translation section, but Edit textarea prefills with target only. No affordance to edit the EN source string. Reviewer cannot fix a phrase upstream and re-validate.
- **Cause:** `startEdit()` sets `editText` from `editedTranslation ?? translatedText`. `submitEdit()` sends as `editedTranslation`. `result.originalText` rendered read-only. No `editedOriginalText` field on the decision payload.
- **Files:** `ValidationSectionCard.tsx:217-232, 372-380, 385-410`, `ValidateStep.tsx:123-161`
- **Fix direction:** Add second textarea on Original card; new `editedOriginalText` field on the section-decision mutation. Backend writes to a new `EditedSource` field, requeues a single-section validation job, propagates `EditedTranslation → ToolboxTalkTranslation` on reviewer accept (not on validation completion).
- **Acceptance:** Reviewer edits EN, re-validates that section only, accepts, live translation reflects the edit.

### P1 — High (workflow breakers)

#### 1.1.4 Slideshow counter mismatch ("Slide 1 of 6" when on slide 4)
- **Priority:** P1
- **Origin:** `[UAT]`
- **Status:** Open
- **Description:** Slideshow iframe header reads 4/6; parent footer shows "Slide 1 of 6". Back button on footer disabled.
- **Cause:** Slideshow HTML never posts `slideChanged` outbound messages. Parent listens but never receives.
- **Files:** `HtmlSlideshow.tsx:36-48, 117`, `SlideshowGenerationPrompts.cs:187, 414, 556`
- **Fix direction:** **Prompt-only change** — tell AI to post `slideChanged` on every slide transition, respond to `getSlideCount` immediately. See Prompt 3 in original brief. No frontend change needed.
- **Acceptance:** Header and footer indices always match. Back active whenever index > 1. Fixes 1.1.5 too.

#### 1.1.5 Slideshow Back button doesn't navigate
- **Priority:** P1
- **Origin:** `[UAT]`
- **Status:** Open
- **Description:** Back inactive or returns to create flow instead of previous slide.
- **Cause:** Same as 1.1.4 — `currentSlide` never increments past 0, so `isFirstSlide` evaluates true.
- **Fix:** Resolved by 1.1.4 prompt fix.

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
- **Status:** Open
- **Description:** Pasted SOP comes back with rephrased headings and added flair. Customer wants option to keep wording exactly as written.
- **Cause:** Section prompt instructs "summarize the key points" / "clear, simple language suitable for all employees". No verbatim parameter.
- **Files:** `SectionGenerationPrompts.cs:11-64`, `InputConfigStep.tsx` (new toggle)
- **Fix direction:** **Prompt + small UI** — add `verbatim` toggle to Step 1, pipe `preserveSourceWording` flag to `BuildSectionPrompt`. See Prompt 1 in original brief.

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
- **Status:** Open
- **Description:** Section headers render as faded text on deep purple background — barely legible. Below WCAG AA 4.5:1 body threshold.
- **Cause:** Slideshow prompt uses `rgba(255,255,255,0.5)` for secondary text.
- **Files:** `SlideshowGenerationPrompts.cs:154-160, 381-387, 523-529`
- **Fix direction:** **Prompt-only** — lift body opacity to 0.95, secondary to 0.75. Add WCAG AA constraint, no-colour-only-signalling rule. See Prompt 3 in original brief.

#### 1.1.15 Quiz questions too hard for floor audience
- **Priority:** P2
- **Origin:** `[UAT]`
- **Status:** Open
- **Description:** Generated questions ask operators to recall RA numbers, use close distractors. Too hard for the actual audience.
- **Cause:** Quiz prompt has no audience-awareness, no rule against identifier recall, vague distractor guidance.
- **Files:** `QuizGenerationPrompts.cs:11-58`
- **Fix direction:** **Prompt-only** — add `audienceRole` parameter to `BuildQuizPrompt`. Explicit rules against identifier recall for Operators. Stronger distractor diversity. See Prompt 2 in original brief.

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
- **Fix direction:** New tenant-settings keys under `Module = ToolboxTalks.Defaults`: target languages, sector, include-quiz, pass threshold, reviewer role. Read in `InputConfigStep` on mount; allow per-import override. Settings UI in `/admin/toolbox-talks/settings`.

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

#### 5.2 Migration Designer.cs guard
- **Priority:** P2
- **Origin:** `[Production-incident]`
- **Status:** Open
- **Description:** Hand-written migrations can be missing their `.Designer.cs` companion file. EF Core discovers migrations via `[Migration]` attribute on Designer.cs — without it, EF silently skips the migration on every startup. One instance silently broken for over a month before discovery this session.
- **Fix direction:** Either mandate `dotnet ef migrations add` (which generates both files) via team practice, OR add a build-time / startup-time check that compares migration `.cs` files to their `.Designer.cs` counterparts and fails if mismatched. Either prevents recurrence.

#### 5.3 Development DB drift sweep
- **Priority:** P2
- **Origin:** `[Production-incident]`
- **Status:** Open
- **Description:** During the Designer.cs fix, Development DB was found to have `GlossaryCorrectionsJson` columns present without corresponding rows in `__EFMigrationsHistory`. Origin unknown — likely partial migration apply (DDL committed, history row failed) or manual schema change. Other drift may exist.
- **Fix direction:** One-pass comparison of every migration's expected schema vs actual schema on Development. Identify drift, decide fix per case. Consider a startup verification routine that warns on detected drift (informational, not blocking).

#### 5.4 R2 orphan file cleanup nightly job
- **Priority:** P2
- **Origin:** `[Engineering]`
- **Status:** Open
- **Description:** Files uploaded to R2 that are no longer referenced by any DB record accumulate over time. Need a nightly Hangfire job to identify and delete orphans.

#### 5.5 MailerSendEmailProvider 429 handling
- **Priority:** P2
- **Origin:** `[Engineering]`
- **Status:** Open
- **Description:** Currently silently drops on 429 (rate limit). Should retry with backoff, log clearly, alert if persistent.

#### 5.6 Demo deployment workflow
- **Priority:** P3
- **Origin:** `[Engineering]`
- **Status:** Open
- **Description:** Currently Demo is "frozen" by disconnecting GitHub. Each refresh requires reconnect → redeploy → disconnect. Cleaner pattern: a dedicated `demo` branch that Railway watches, with promotions done by `git merge transval → demo`. Lets Demo stay auto-deploy-from-its-own-branch while still being controlled (no accidental updates from Development churn).

#### 5.7 Next.js 15+ params shape
- **Priority:** P3
- **Origin:** `[Engineering]`
- **Status:** Open
- **Description:** Synchronous `params` access in redirect stubs. Not breaking yet but Next.js is moving toward async params. Will need updating before a future Next.js upgrade.

#### 5.8 AI Chat Assistant — UI Help / Data Q&A
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

# 7. Recently Closed (last sprint)

Kept here for trail; prune periodically.

- **Migration Designer.cs missing for `AddGlossaryCorrectionsToTranslationValidationResult`** — Production-incident. Fixed 28 May 2026.
- **QR sessions summary 500 (EF Core 9 LINQ translation failure)** — Production-incident. Fixed 28 May 2026.
- **QR scan language selector disconnected from backend** — Internal-QA. Removed 28 May 2026.
- **QR location edit dialog not pre-filling form** — Internal-QA. Fixed 28 May 2026.
- **Welcome email not sent on UI user creation** — Production-incident. Targeted patch 28 May 2026 (proper unified flow remains as 3.1).
- **Subtitle processing job stuck at 0% (tenant filter trap)** — Production-incident. Fixed 28 May 2026.
- **QR video display not implemented** — Boss / Demo-critical. Built 28 May 2026 (single-talk only; course branch deferred).
- **QR video subtitles — backend tenant filter + frontend CORS** — Boss / Demo-critical. Fixed 28 May 2026.
- **PIN visible to SuperUser + Admin on employee detail** — Boss. Built 28 May 2026.

---

*End of BACKLOG.md. For active prioritised work, see `SPRINT.md`.*