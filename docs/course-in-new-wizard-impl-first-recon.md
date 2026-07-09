# Course-in-New-Wizard — First Implementation Chunk Recon

**Date:** 2026-07-09
**Scope:** Read-only investigation. No code changes.
**Builds on:** `docs/course-in-new-wizard-recon.md` (established zero course code exists in the new wizard) and `docs/course-in-new-wizard-scoping.md` (locked strategic decisions: split-and-author model, additive-parallel track, hold-until-unified-cutover for Production).

---

## 1. Executive summary

The legacy course flow works by treating course-splitting as an afterthought bolted onto Publish: one document is parsed and processed as a single draft talk all the way through Quiz/Settings/Translate/Validate, and only at the very last step does `PublishAsCourseAsync` retroactively explode it into N published talks. The new wizard's entire architecture is built on the opposite assumption — a single `ToolboxTalk` database row *is* the wizard's state, present from Step 1 onward, with every route, command, hook, and component hard-wired to one `talkId`. These two models are structurally incompatible at the root, not just missing a feature: the new wizard has nowhere to put a decision ("is this a Lesson or a Course?") that legacy code defers until after parsing, because the new wizard has already committed to one `ToolboxTalk` row by the time parsing happens. The first implementation chunk is therefore not "build a course step" — it is deciding, and then building, how the new wizard defers its early Lesson/Course commitment, which is an architecture decision, not a coding task.

---

## 2. Legacy-course flow trace (Part 1)

### 2.1 Entry point and where "Course" gets chosen

Route: `web/src/app/(authenticated)/admin/toolbox-talks/create/page.tsx` → `CreateWizard.tsx`, a 7-step client-state SPA. There is **no dedicated "create course" entry point or URL parameter** — the Lesson/Course choice happens *inside* the wizard, on the Parse step, **after** AI parsing completes, not before upload and not at Step 1.

`OutputTypeSelector.tsx` (`web/src/features/toolbox-talks/components/create-wizard/steps/parse/OutputTypeSelector.tsx`) is a two-card picker ("Single Lesson" / "Course: each section becomes a separate lesson in an ordered course") rendered under a "2a — Output Type" divider inside `ParseStep.tsx`, directly above the section list. The `ContentCreationSession.OutputType` field is null until the user clicks Continue on the Parse step; `UpdateSectionsAsync` (`ContentCreationSessionService.cs:362`) is the only write path for it prior to publish.

**Surprising finding — the "AI Suggested" badge is dead:** `ParseContentAsync` writes `session.OutputType = parseResult.SuggestedOutputType` immediately after parsing (line 292), and the frontend shows a "Sparkles / AI Suggested" badge on whichever card matches. But `ContentParserService.SuggestOutputType(int sectionCount)` (`ContentParserService.cs:177-181`) is:

```csharp
public OutputType SuggestOutputType(int sectionCount)
{
    // Always default to Lesson — user can manually switch to Course if desired
    return OutputType.Lesson;
}
```

It always returns `Lesson`, regardless of section count. A `CourseThreshold = 3` constant exists at the top of the file (line 25) and is **never referenced anywhere**. The AI never actually suggests Course — the badge only ever fires for Lesson. This looks like a half-finished feature that was deliberately neutered (the comment reads as an intentional decision, not an accident) but the dead threshold constant and the "AI Suggested" UI copy are misleading artifacts of it.

### 2.2 Input/upload step

Identical for both output types. `InputConfigStep.tsx` has zero references to `OutputType` — the mode selector, file upload, translation settings, quiz-inclusion toggle, sector, and audit-metadata fields are all output-type-agnostic. The concept of "course" doesn't exist yet at this point in the flow.

### 2.3 Parse step

Parsing is **identical regardless of eventual output type**. `ParseContentAsync` (`ContentCreationSessionService.cs:186-322`) calls `IContentParserService.ParseContentAsync(rawText, session.InputMode, tenantId, ...)` with no `OutputType` parameter — the AI has no awareness that its sections might become separate talks. `ContentParserService.ParseContentAsync` (`ContentParserService.cs:41-175`) is pure Claude-Sonnet judgement (via `SectionGenerationPrompts.BuildSectionPrompt`, minimum 2 sections, aim 3-7), not heading/page-break detection. There is no course-aware splitting logic anywhere in the parse path — 100% of course-structuring happens at Publish.

**Sections are admin-editable before continuing.** `SectionList.tsx` (`.../steps/parse/SectionList.tsx`) implements full `@dnd-kit` drag-reorder (`handleDragEnd`, lines 80-95), delete, and double-click rename/edit-in-place, with no grep hits for merge/split operations. Reordering here is the **only** way to control final course-item order — `handleSectionsChange` reassigns `suggestedOrder = index` on every drag, and this literal array order becomes `ToolboxTalkCourseItem.OrderIndex` at publish.

### 2.4 Quiz step

`GenerateQuizAsync` (`ContentCreationSessionService.cs:981-1095`) generates questions **per section** (a loop over `sections`, one AI call per section, "for natural grouping" per the code comment) and tags each question with `SectionIndex` (line 1035). All questions are appended into one flat list, `session.QuestionsJson` — there is no concept of "per course item" here.

**Verified defect — every course item gets every question, not just its own.** `SectionIndex` is written once (line 1035) and **never read anywhere else in the file** (confirmed by exhaustive grep). At publish, `PublishAsCourseAsync` deserializes the full `session.QuestionsJson` list once (lines 1787-1793) and, inside the per-section loop that creates each course-item talk, calls `SyncQuizQuestionsToTalk(talk.Id, quizQuestions, session.InputMode)` (line 1974) — passing the **entire unfiltered list**, not a slice for that section. `SyncQuizQuestionsToTalk` itself (lines 2447-2480) simply does `foreach (var q in quizQuestions)` with no filtering. Net effect: if a document has 5 sections with 3 questions each (15 total), publishing as a course produces 5 talks, **each carrying all 15 questions**, not 3. `SectionIndex` is captured metadata that is currently dead. This is a real, currently-shipping quirk in production code, not a hypothetical.

### 2.5 Settings step

`SettingsStep.tsx` is the same component for both output types — title, description, cover image, category, refresher frequency, `generateCertificate`, `minimumWatchPercent`, `autoAssign`/`autoAssignDueDays`, slideshow settings, saved into `session.SettingsJson`. **There is no course-specific settings UI anywhere** — no `RequireSequentialCompletion` toggle, no course-level certificate/refresher/auto-assign control distinct from the talk-level fields being edited.

**Verified defect — these settings are silently dropped for Course output.** `PublishAsLessonAsync` explicitly copies `GenerateCertificate`, `MinimumVideoWatchPercent`, `AutoAssignToNewEmployees`, `AutoAssignDueDays`, `RequiresRefresher`/`RefresherIntervalMonths` from `session.SettingsJson` onto the published talk (`ContentCreationSessionService.cs:1448-1459`). `PublishAsCourseAsync` has **no equivalent block** — the new `ToolboxTalkCourse` (lines 1846-1854) is created with only `Title`, `Description`, `IsActive = true`, `RequireSequentialCompletion = true` (hardcoded). `GenerateCertificate`, `RequiresRefresher`, `AutoAssignToNewEmployees` all remain at entity defaults (`false`/`false`/`false`) regardless of what the admin configured in Settings, and there is no warning anywhere in `PublishStep.tsx`'s summary UI. An admin who turns on "Generate Certificate" and sets a refresher interval, then picks Course output, silently gets a course with neither.

### 2.6 Translate & Validate steps

Course-blind (zero grep hits for `OutputType`/`Course` in either `TranslateStep.tsx` or `ValidateStep.tsx`). `StartTranslateValidateAsync` (`ContentCreationSessionService.cs:392-767`) creates one `TranslationValidationRun` per target language, each scoped to the single draft talk (`ToolboxTalkId = talkId`). The whole undivided document is translated and validated as one unit — **not per eventual course item**. `PublishAsCourseAsync` later reassociates each run (`run.CourseId = course.Id; run.ToolboxTalkId = null` — lines 2149-2151) as a unit, not per-item. This means today's "course-level validation" is neither genuinely per-item nor genuinely course-wide in a first-class sense — it's a single whole-document run retroactively relabeled to belong to the course as a whole.

### 2.7 Publish step — `PublishAsCourseAsync` in full

**File:** `ContentCreationSessionService.cs`, lines **1779-2175** (396 lines), private method, called only when `session.OutputType == Course`.

| Step | Action |
|---|---|
| 1 | Course title uniqueness check against `ToolboxTalkCourses` — throws if duplicate |
| 2 | Capture draft `Title`/`Description`, draft `ToolboxTalkSection` rows, draft `ToolboxTalkTranslation` rows, and ordered draft `ToolboxTalkQuestion` IDs **before any mutation** ("R1 mitigation" comment — implies a prior data-loss bug from clearing too early) |
| 3 | Create `ToolboxTalkCourse` — `Title`, `Description`, `IsActive=true`, `RequireSequentialCompletion=true` only (see §2.5 defect) |
| 4a | **(Video mode only, `InputMode==Video && OutputTalkId.HasValue`)** Repurpose the draft talk in place as "{Title} — Full Video" (`OrderIndex 0`): soft-delete its sections, insert a single synthetic "Video" section, hard-delete its quiz questions, suffix its translations, regenerate its `Code`, add as course item |
| 4b | For each parsed section (in current array order): create a new `Published`, `IsPartOfCourse=true` `ToolboxTalk` with that section as its sole content; sync quiz settings + **all** quiz questions (§2.4 defect); migrate matching translation slice from the draft via `ExtractTranslatedSectionForId`/`ExtractTranslatedQuestionForId` (GUID remapping, skips with a warning log if question counts mismatch); add as course item, `OrderIndex` incrementing, `IsRequired=true` always (no per-item optional flag exposed) |
| 5 | Create one `ToolboxTalkCourseTranslation` per draft-talk language; if title/description changed between Translate step and Publish step, re-translates them **synchronously, in-request** via `_contentTranslationService.TranslateTextAsync` — blocking the publish HTTP call on a live external API call, inconsistent with the rest of the module's job-queued translation architecture |
| 6 | `SaveWithCodeRetryAsync` — commits course + all talks in one batch (up to 10 attempts on `Code` unique-constraint collision) |
| 7 | Reassociate validation runs: `run.CourseId = course.Id; run.ToolboxTalkId = null` — second `SaveChangesAsync` |
| 8 | **(Non-Video only)** Soft-delete the orphaned draft talk (`DeleteDraftTalkAsync`); `session.OutputTalkId = null`. Video mode never deletes it — it survives as the repurposed Full Video item |
| 9 | Enqueue `RequirementMappingJob` fire-and-forget, with `courseId` populated / `talkId = null` (contrast: lesson path passes `talkId` populated / `courseId = null`) |

Back in `PublishAsync`, a **separate** pass afterward enqueues `ContentGenerationJob.GenerateSlideshowOnlyAsync` once per non-Full-Video course item, gated on `GenerateSlideshow` in settings.

**No transaction wrapper anywhere** in this method (confirmed by grep — zero `BeginTransaction`/`IExecutionStrategy` matches). It is a chain of independent `SaveChangesAsync` calls with no compensating rollback; a failure between the course+talks save and the validation-run-reassociation save leaves an inconsistent but partially-committed state.

**Verified defect — code-generation collision risk within one course-publish batch.** `GenerateCodeAsync(parsedSection.Title, ...)` is called once per section inside the loop (line 1938), and its "existing codes" query only sees already-*persisted* codes — it cannot see sibling talks generated earlier in the same in-memory, not-yet-saved batch. Two same-titled or same-initials sections in one document will independently compute the identical `PREFIX-001` code. `SaveWithCodeRetryAsync` catches the resulting Postgres unique-violation and regenerates codes for the **whole batch**, up to 10 attempts with jitter — but it re-runs the same stale query, so the collision can recur. Not flagged or handled as a distinct case anywhere; a genuine (if narrow) undocumented edge case.

### 2.8 Ordering — code-determined, not a separate admin decision

`ToolboxTalkCourseItem.OrderIndex` is set entirely by the literal array order of `sections` at publish time, which is whatever order was last saved from the Parse step's drag-reorder UI. There is no reordering control anywhere later in the flow (Settings/Translate/Validate/Publish have none) — this is **not** a genuine open product question; the code already answers it (order = section order, editable only pre-Quiz).

### 2.9 Per-item metadata

`Title` = the section heading verbatim (rename-able via `SectionList`'s double-click edit). `Description` is **not** derived from content — every item gets the identical templated string `"Part of course: {courseTitle}"`. `Category`/`SourceLanguageCode` are copied from the course-level `PublishRequest`, identical across all items — there is no per-item category.

### 2.10 Hangfire jobs hit during this flow

| Job | Enqueue site |
|---|---|
| `VideoTranscriptionJob` | `ParseContentAsync`, Video mode, no transcript yet |
| `ContentCreationParseJob` | `ParseContentAsync`, Video mode, transcript already present (retry path) |
| `TranslationValidationJob` | `StartTranslateValidateAsync`, once per target language |
| `RequirementMappingJob` | `PublishAsCourseAsync`, fire-and-forget, `courseId` populated |
| `ContentGenerationJob.GenerateSlideshowOnlyAsync` | Back in `PublishAsync`, once per non-Full-Video course item |

All enqueued via concrete class per CLAUDE.md Note 21.

---

## 3. New wizard step model (Part 2)

### 3.1 Overall architecture

**No `ContentCreationSession` involvement anywhere** — confirmed by an exhaustive grep across `web/src/features/toolbox-talks/components/learning-wizard/**` (zero hits). The **`ToolboxTalk` row itself is the wizard's state holder**, present from Step 1. All new-wizard routes live in the existing `ToolboxTalksController.cs` (`api/toolbox-talks`, class-gated `Learnings.View`, actions gated `Learnings.Manage`) — there is no dedicated controller:

- `POST initialise` — Step 1
- `POST learning-wizard/upload-source-url` — presigned R2 upload, pre-talk
- `POST {id}/parse` — Step 2
- `PUT {id}/sections` — Step 2 save
- `POST {id}/quiz/generate` — Step 3
- `PUT {id}/questions`, `PUT {id}/quiz-settings` — Step 3 save
- `PUT {id}/settings` — Step 4
- Steps 5/6 reuse pre-existing translation-workflow + `TranslationValidationController` endpoints, not new wizard-specific routes
- `POST {talkId}/publish` — Step 7

**State/resumability field:** `ToolboxTalk.LastEditedStep` (nullable int). Set to `1` at creation, unconditionally overwritten to `2` on every parse call, bumped to `3` only if currently lower, and explicitly written by the frontend on forward navigation via a dedicated `UpdateLastEditedStepCommand`. This is the discriminator behind CLAUDE.md Note 29 (`null` = legacy wizard, non-null = new wizard).

### 3.2 Step 1 — Input & Config

`InitialiseToolboxTalkCommandHandler` creates the `ToolboxTalk` row **directly** — no session, no draft table, no deferred decision. Title uniqueness + `Code` generation happen immediately. Tenant defaults (`DefaultIsActive`, `DefaultGenerateCertificate`, `DefaultMinimumVideoWatchPercent`, `DefaultAutoAssignDueDays`, `DefaultPassingScore`, `DefaultRefresherFrequency`) are pulled in and set on the talk at this point — `Status=Draft`, `LastEditedStep=1`. The source file is uploaded via a separate presigned-URL flow *before* this call — the file is already in R2 by the time the talk row exists.

### 3.3 Step 2 — Parse

`ParseToolboxTalkContentCommandHandler` dispatches on `InputMode` to `HandleTextAsync`/`HandlePdfAsync`/`HandleDocxAsync`/`HandleVideoAsync`. Text/PDF/Docx call the same `IContentParserService.ParseContentAsync` used by the legacy wizard (pure Claude-Sonnet judgement, same prompt builder). Video mode flips `Status=Processing`, enqueues a background transcription job, and the frontend polls.

Parsed output materializes as **real `ToolboxTalkSection` rows immediately** (`MaterialiseSectionsAsync`) — not a JSON blob like the legacy session's `ParsedSectionsJson`. Post-parse UI supports reorder/edit-in-place/delete/add-new (no merge/split), plus a "Re-parse" (discard-and-retry) button. Save via `PUT {id}/sections` only on explicit "Save & Continue."

### 3.4 Step 3 — Quiz

`GenerateToolboxTalkQuizCommandHandler` generates for the **whole document at once** — concatenates all sections into one `combinedContent` string and makes a single AI call with `minimumQuestions: 5` hardcoded (confirms CLAUDE.md's "min 5, up to 10, AI-determined" claim exactly — `QuizGenerationPrompts.BuildQuizPrompt` computes `maxQuestions = Max(10, minimumQuestions + 5) = 10`). This differs from the legacy wizard's per-section AI-call loop (§2.4) — the new wizard does not tag questions with a section index at all, since there is only one talk.

Old questions hard-deleted (`ExecuteDeleteAsync`) before new ones inserted — "replacing, not appending."

### 3.5 Step 4 — Settings

`UpdateToolboxTalkSettingsCommand` writes `Title`, `Description`, `Category`, `RefresherFrequency`, `IsActive`, `GenerateCertificate`, `MinimumVideoWatchPercent`, `AutoAssignToNewEmployees`, `AutoAssignDueDays`, `GenerateSlidesFromPdf` **directly onto the talk row** — there is no separate settings-JSON-then-promote-at-publish indirection like the legacy session has. Because the talk *is* the entity, Settings changes take effect immediately, not retroactively at Publish. Save-on-blur (not debounced) specifically to avoid triggering a translation-staleness cascade on every keystroke.

### 3.6 Step 5 — Translate

Per-language, not "translate everything" in one action. Each language row has an independent "Start" (or a "Start All" that staggers calls 1s apart). `StartTalkTranslationCommandHandler`: verifies the language is declared, advances a per-language `ITranslationWorkflowService` state machine, creates a `TranslationValidationRun` directly (`IsNewWizard=true` — a flag that "skips the old-wizard session-relevance guard"), and enqueues `TranslationValidationJob` — **the same job the legacy wizard and standalone TransVal flow use.**

### 3.7 Step 6 — Validate

Not a distinct orchestration phase — it is the review/results UI for the same runs Step 5 started. **It imports and reuses the legacy wizard's own `ValidationProgressPanel`/`ValidationSectionCard` components directly** — the new wizard did not fork these. Reachability logic for Steps 5 and 6 is identical (`sections.length > 0 && targetLanguageCodes.length > 0`); the split into two screens is a UI convenience, not a backend phase boundary.

### 3.8 Step 7 — Publish

`PublishToolboxTalkCommandHandler`: talk exists, not already Published, ≥1 section, and — if target languages declared — at least one completed validation run with no pending non-Pass reviewer decisions. On success: `Status=Published`, `PublishedAt` set. **That is the entire handler** — no certificate/requirement-mapping logic inside it.

**Surprising split:** the side-effect jobs (`RequirementMappingJob`, conditional `ContentGenerationJob` slideshow) are triggered from the **controller action** (`ToolboxTalksController.PublishByTalkId`), not the command handler or a domain event — inconsistent with the cleaner CQRS-handler-owns-everything pattern used elsewhere in the same codebase.

### 3.9 Reusability assessment

Every layer — URL (`getStepUrl(talkId, step)`), backend route (`{id:guid}`/`{talkId:guid}` under `api/toolbox-talks`), command shape (`TalkId: Guid` baked into every command record), hook signature (`useTalk(talkId)`, `usePublishTalk(talkId)`, etc.), component props (`{ talkId: string }` uniformly), and reachability logic (`stepOrder.ts` typed directly against the `ToolboxTalk` TS interface) assumes "one `ToolboxTalk` by id" as a structural fact. **There is no seam at any single layer where course support could be slotted in without touching all of them.** This matches — and sharpens — the prior recon's "zero course-concept grep hits" finding: it's not just absent, the architecture actively excludes it.

---

## 4. Course-scope adaptations table (Part 3)

| New-wizard step | What changes for Course scope | Category |
|---|---|---|
| **Step 1 — Input & Config** | Cannot commit to a single `ToolboxTalk` row immediately, because Lesson-vs-Course isn't decidable until after parsing (matching legacy semantics — §2.1). Needs either a course-scoped alternate entry point that defers `ToolboxTalk` creation, or a lightweight pre-talk state holder. | **Different step semantics required** |
| **Step 2 — Parse** | Parsing logic itself is unchanged (same AI call, same prompt) — but the *output* target changes: sections can no longer materialize directly onto one `ToolboxTalk.Sections` collection if the eventual output is a course, since each section may become its own talk later. The admin-editable reorder/rename/delete UI (already built, §3.3) is exactly what's needed for course-item ordering (§2.8) — reusable as-is once the underlying state model supports it. | **Different step semantics required**, UI mostly reusable |
| **Step 3 — Quiz** | Legacy generates quiz per-section but (buggily) applies the full set to every course item (§2.4). Decision needed: replicate that behavior, or fix it now that a rebuild is happening anyway (§6, Product decision). Mechanically, once sections exist as rows, the same single-combined-document AI call could work if quiz is generated once and then deliberately fanned out per item — this is new logic either way. | **Different step semantics required** |
| **Step 4 — Settings** | Legacy drops course-level settings entirely (§2.5 defect) because there's no course-settings UI and no promote-to-course step. The new wizard's Settings step writes directly to `ToolboxTalk` fields — there's no `ToolboxTalkCourse` equivalent target yet. Needs either a genuinely new course-settings step, or explicit acknowledgment that these settings apply per-item exactly as today (and are silently dropped at the course level, preserving the bug) or get a real course-level fields section (fixing it). | **New step entirely** (course-level fields) **+ different semantics** (per-item fields) |
| **Step 5 — Translate** | Legacy translates the whole document once, then redistributes per item at publish. New wizard's Step 5 is inherently per-*talk* (`StartTalkTranslationCommandHandler` takes one `TalkId`). If course items are separate `ToolboxTalk` rows from early on, each would need its own translate/validate pass — N times the API calls and cost versus legacy's one-pass-then-redistribute model. If items are still one macro-draft until late, Step 5 works unmodified but the redistribution logic (§2.7 step 4b/7) must be rebuilt as a new "distribute" phase. | **Different step semantics required** — cost/architecture trade-off, see §6 |
| **Step 6 — Validate** | Same fork as Step 5 — reuses existing components either way (already proven to be shareable, §3.7), but the *data* being validated (one document vs. N items) depends on the Step 5 decision. | **Cascades from Step 5 decision** |
| **Step 7 — Publish** | Legacy publish is where all the actual course-construction work happens (§2.7) — course entity creation, per-item talk creation, translation migration, code generation, validation-run reassociation, `RequirementMappingJob` trigger. New wizard's publish handler is a thin status-flip; none of `PublishAsCourseAsync`'s logic exists here. This is the largest single port regardless of which architecture shape (§6) is chosen. | **New step entirely / largest single port** |
| **Course-level scheduling, assignment, certificates, completion** | Confirmed by the prior recon as fully wizard-agnostic — `ToolboxTalkCourseAssignment`, `AssignCourseCommandHandler`, `CourseProgressService`, `CertificateGenerationService.CertificateType.Course` all operate on the `ToolboxTalkCourse`/`ToolboxTalkCourseItem` rows regardless of which wizard produced them. | **Doesn't apply — zero new work** |

### The biggest orchestration change (Part 3.8)

The linear "one talk being built" mental model has to fork **before Step 1 ever creates a `ToolboxTalk` row** — not at some later step. Legacy code defers the Lesson/Course decision until after parsing precisely because it doesn't commit to a concrete output entity until then (it uses the session's JSON blobs as a neutral intermediate representation). The new wizard's core simplification — "the talk row IS the state, no session indirection" — is exactly the thing that makes this defer-the-decision pattern impossible to replicate without reintroducing *some* form of pre-commitment indirection for the course path specifically. This is not a "Step N needs a tweak" problem; it's a foundational question about whether course creation in the new wizard needs its own state-holder pattern distinct from the one built for single-talk creation.

---

## 5. Data model changes (Part 4)

Existing entities as read directly from source:

- **`ToolboxTalkCourse`** (`TenantEntity`): `Title`, `Description`, `IsActive`, `RequireSequentialCompletion`, `RequiresRefresher`, `RefresherIntervalMonths`, `GenerateCertificate`, `AutoAssignToNewEmployees`, `AutoAssignDueDays`. All the course-level settings fields already exist on the entity — legacy `PublishAsCourseAsync` simply never populates most of them (§2.5).
- **`ToolboxTalkCourseItem`** (`BaseEntity`, join): `CourseId`, `ToolboxTalkId`, `OrderIndex`, `IsRequired`.
- **`ToolboxTalkCourseTranslation`** (`BaseEntity`): `CourseId`, `LanguageCode`, `TranslatedTitle`, `TranslatedDescription` — title/description only, no section-level course translation concept (each course item's own `ToolboxTalkTranslation` carries its own section/question translations).
- **`ToolboxTalkCourseAssignment`**, `CertificateType.Course` — unaffected, wizard-agnostic (§4 table).

**No schema changes are required to support the "compose existing built entities" side of this** — every field a course-in-new-wizard chunk would populate already exists on `ToolboxTalkCourse`/`ToolboxTalkCourseItem`/`ToolboxTalkCourseTranslation`.

**What likely does need a new (nullable) field, depending on the §6 architecture decision:**

- If the new wizard needs a pre-`ToolboxTalk` state holder for the course path (Shape A below), that is either (a) a reuse of `ContentCreationSession` scoped to only the course path — no new entity, but re-couples the "new wizard has zero session dependency" property that was the whole point of the rebuild — or (b) a new, smaller entity purpose-built for this (more schema, but keeps the new wizard's no-session architecture intact for the course path specifically).
- If course items become real `ToolboxTalk` rows early (Shape B below), each such talk needs a way to know "I'm part of an in-progress course wizard run, not a standalone talk" before the course row itself exists — e.g. a nullable `PendingCourseSessionId` or reuse of the existing `IsPartOfCourse` boolean plus a new nullable `CourseId` set before the `ToolboxTalkCourse` row exists (would require making `ToolboxTalkCourseItem.CourseId` creation happen earlier, or a scratch grouping field).
- Either shape needs a `LastEditedStep`-equivalent for course-level resumability — whether that lives on a new session-like entity or as a new field on `ToolboxTalkCourse` itself (nullable, so legacy-created courses are unaffected) is a design detail, not a blocker.

No schema change proposed here touches the legacy path — per the scoping doc's Track 1 principle, any new field is additive and nullable.

---

## 6. Product decisions with recommendations (Part 5)

### Blocking (must decide before any code)

**Q1 — Architecture shape: does the new wizard defer talk creation for courses (Shape A, mirrors legacy), or fork into N real `ToolboxTalk` rows immediately after Parse (Shape B, mirrors the new wizard's own single-talk philosophy)?**

- **Shape A (mirror legacy):** Reintroduce a thin session-like state holder (reusing `ContentCreationSession` scoped to the course path, or a new smaller entity) that carries sections/quiz/settings as an intermediate representation through Parse→Quiz→Settings→Translate→Validate, exactly like today, and only materializes N published `ToolboxTalk` rows at Publish (porting `PublishAsCourseAsync`). Translation/validation runs once against the whole document, same cost profile as today.
  - *Trade-off:* Smallest, most schedule-compatible option — it's substantially a port of existing, already-proven logic into a new step-shell. But it reintroduces the one thing the new wizard was built to eliminate (session indirection), if only for this one path, and it preserves both known defects (§2.4 quiz duplication, §2.5 dropped course settings) unless explicitly fixed during the port.
- **Shape B (fork early):** After Parse + section-editing, if Course is chosen, immediately create N real `ToolboxTalk` rows (one per section) plus a `ToolboxTalkCourse` wrapper, and run Quiz/Settings/Translate/Validate/Publish **per item**, with a new course-level dashboard UI showing N items each at their own step. This is closer to "genuinely first-class course items" — no quiz-duplication bug is possible because each item generates its own quiz; course-level settings get a real home. But translation/validation now runs N times instead of once (cost and time multiply), and there is no existing UI pattern anywhere in the codebase for "one wizard driving N sub-wizards" — this would be new UI paradigm work, not adaptation of existing components.
  - *Trade-off:* Better long-term architecture and fixes real bugs, but is a materially larger build (closer to the "Scenario C, 3-4 weeks" estimate from the prior recon) and risks blowing the 4-6 week timeline already communicated.
- **Recommendation:** Shape A. It is the only option consistent with the already-locked "split-and-author model matches current behaviour" and "additive parallel, 4-6 weeks" commitments in the scoping doc. Shape B is a legitimate future rebuild, not a first cut — flag it as a deliberate deferred improvement, not an oversight, so it doesn't quietly become expected scope.

**Q2 — Do the two known legacy defects (§2.4 quiz duplication across course items, §2.5 dropped course-level settings) get fixed during the port, or replicated as-is?**

- *Option 1 — Fix both, since a port is happening anyway:* `SectionIndex` already exists on the quiz question DTO; filtering per section-talk at the equivalent of `PublishAsCourseAsync`'s loop is a small, contained change. Course-level settings simply need the same field-copy block `PublishAsLessonAsync` already has, applied to the new `ToolboxTalkCourse` entity.
- *Option 2 — Replicate exactly, fix later as a separate documented chunk:* Keeps the port mechanical and low-risk, avoids scope creep into "improve behavior" during a "rebuild the same behavior" chunk, and keeps a single clean diff to review against the legacy trace.
- **Recommendation:** Fix Q2's quiz-duplication bug during the port — it's small, isolated, and clearly wrong (the existing data already supports the fix). Leave the course-level-settings gap as a documented, deliberate parity choice for chunk 1, and pick it up as an explicit small follow-up chunk once the core port is stable — fixing it means designing a course-settings step, which is new UI work, not a bug fix, and shouldn't block the first chunk.

**Q3 — Where does the Lesson/Course choice live in the new wizard's navigation — a distinct entry route (e.g. `learnings/course/new`), or a choice inside the existing `learnings/new` flow?**

- Given Q1 (Shape A) requires an entirely different state holder for the course path from the moment of Step 1, a **distinct entry route** is the natural consequence — trying to cram both into one `learnings/new` page would mean the page doesn't know which backend entity to create until after the user answers a question inside it, which re-litigates the same "when do we commit" problem this recon is about.
- **Recommendation:** Distinct entry route, consistent with legacy's own late-binding pattern being *deferred to Parse*, not *avoided via a shared entry point*. This is a small decision that falls out of Q1, not an independent fork.

### Can be resolved during implementation

**Q4 — Does Translate/Validate run once against the whole document (matching legacy, cost-efficient) or per course item (more correct per-item provenance, N× cost)?**
Directly cascades from Q1. If Shape A is chosen, this is settled by definition (once, then redistributed). Worth flagging explicitly so nobody assumes "course support" implicitly means "per-item validation" — it doesn't, under Shape A, and that's a legitimate scope boundary to state up front rather than discover mid-build.

**Q5 — External review at course level — per-item or course-wide?**
Unchanged from the prior recon's finding: external review has no course concept today for either wizard. Deferrable; does not block Chunk 1.

**Q6 — Course-level scheduling/refresher interaction with per-item refreshers?**
Confirmed code-determined for the *authoring* side (nothing new needed — §4 table, "doesn't apply, zero new work"), but the interaction between a course-level refresher and per-item refreshers at the *scheduling* level is a genuine pre-existing product question independent of this feature — out of scope for course-in-new-wizard specifically.

### Deferred (close to Production)

**Q7 — Do we backfill `RequireSequentialCompletion`/certificate/refresher defaults differently for new-wizard-created courses vs. legacy-created ones, or keep them identical?**
Not a launch blocker — can be decided once the course-settings step (if built, per Q2) has a concrete shape to react to.

---

## 7. First chunk shape (Part 6)

Given Q1 resolves to Shape A (recommended):

- **Backend-first, not full-stack.** Chunk 1 should establish the state-holder decision concretely — either "reuse `ContentCreationSession`, scoped to only the course-authoring path, with a new nullable discriminator so it can coexist with the legacy wizard's own use of the same table" or "introduce a new, smaller entity purpose-built for the new wizard's course draft state" — and get that one architectural question answered in code before any step UI is built. Building a Parse-step UI against the wrong state model would be expensive to unwind.
- **What it produces:** A working backend skeleton — the course-path state holder entity/table, an entry endpoint (`POST` equivalent of `initialise` for courses), and the Parse step's backend half (reusing the existing `IContentParserService` unchanged) writing into the new state holder instead of a `ToolboxTalk`. No frontend UI is required to prove this chunk — a working API smoke test (create course session → upload/parse → sections stored on the new state holder) is sufficient proof of the architecture before investing in step UI.
- **Estimated size:** Multi-day (roughly 2-3 days) — this is a design-and-prove-the-shape chunk, not a large volume of new code, but the design decision itself (§6 Q1) needs to be made and validated against real data flow, not just diagrammed.
- **What it leaves for chunk 2:** The Parse-step frontend UI (largely lifting `SectionList.tsx`'s existing reorder/edit/delete component, which is already state-model-agnostic at the component-prop level), and the Quiz step (backend + frontend).

---

## 8. Preliminary chunk breakdown (Part 6)

In dependency order. Names and one-line descriptions only — not implementation prompts.

1. **Course-path state holder + entry point.** Resolve Q1/Q3; stand up the backend entity and initial API surface a course-creation session writes into. (See §7.)
2. **Parse step (course).** Wire the existing `IContentParserService` + a lifted `SectionList`-equivalent UI into the new state holder; confirm section reorder/edit/delete works against it.
3. **Quiz step (course).** Port `GenerateQuizAsync`'s per-section AI-call loop; decide and implement the Q2 fix (per-item question filtering) at this point since `SectionIndex` data now exists cleanly.
4. **Settings step (course).** Decide Q2's course-settings scope; build whatever UI/fields are in-scope for this cut.
5. **Translate + Validate steps (course).** Port `StartTranslateValidateAsync`'s whole-document translation/validation trigger against the course state holder; reuse existing `ValidationProgressPanel`/`ValidationSectionCard` per §3.7's precedent.
6. **Publish (course) — the big port.** Port `PublishAsCourseAsync` in full: course entity creation, per-item talk materialization, translation migration/remapping, `Code` collision handling, validation-run reassociation, `RequirementMappingJob` trigger.
7. **Cutover wiring + regression pass.** Route `courses/new/page.tsx` and the course list's "Create New" to the new path behind the existing `UseNewWizard`-style gating (or its successor, per the in-flight toggle-retirement work); verify employee-facing course experience, assignment, completion, and certificates are unaffected (expected true unmodified, per the prior recon's "wizard-agnostic" finding).
8. **(Deferred, separate chunk) Course-level settings gap fix.** If Q2 punted this, a follow-up chunk designing and wiring `GenerateCertificate`/`RequiresRefresher`/`AutoAssignToNewEmployees` onto the course path properly.

---

## 9. Estimated total chunks and timeline

**7 chunks for the core build (chunk 8 optional/deferred).** At roughly half-day-to-multi-day per chunk (chunk 1 and chunk 6 are the largest — architecture-proving and the big publish port, respectively; chunks 2-5 are more mechanical ports of already-proven legacy logic into the new step-shell pattern), this lands within the scoping doc's existing 4-6 week estimate, consistent with Shape A being chosen. This recon does not materially change that estimate — it confirms the shape the estimate was implicitly assuming (a port, not a from-scratch design) and identifies the one place (chunk 1) where real design effort — not just coding — is required before proceeding. If Q1 instead resolves to Shape B (fork early), treat the entire estimate as void and re-scope from scratch; that path has no existing precedent to port from and needs its own recon.

---

## 10. Notes for the boss

The single most important thing this recon found: the new wizard's biggest strength for standalone talks — one database row is the entire wizard state, no session layer — is exactly what makes course support hard, because a course needs to defer "is this one talk or several?" until after the document is parsed, and the new wizard has already created its one `ToolboxTalk` row by then. This isn't a missing feature, it's a structural mismatch between two designs that were each individually correct for what they were built for. The practical decision to make is whether to reintroduce a lightweight session-like layer just for the course path (small, matches the current 4-6 week estimate, keeps today's course behavior including two small known bugs — duplicate quiz questions across course items, and course-level certificate/refresher settings being silently dropped) or to build course items as fully independent talks from the start (architecturally cleaner, fixes both bugs by construction, but is a bigger build with no existing UI pattern to lean on and would blow past the current estimate). Recommend the smaller option for this first cut, with the bigger rebuild kept as a known, deliberate future improvement rather than something that creeps into this chunk's scope. Everything else in this recon — data model, chunk breakdown, minor UI decisions — falls out cleanly once that one call is made.
