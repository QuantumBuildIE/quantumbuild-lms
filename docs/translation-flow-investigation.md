# Translation Flow Investigation

**Date:** 2026-06-19  
**Investigator:** Claude Code (read-only)  
**Branch:** transval  
**Scope:** End-to-end translation flow covering every content type and every translatable artefact

---

## 1. One-Line Summary

Translation in QuantumBuild LMS takes **two distinct code paths depending on workflow generation**: the **new wizard path** (`POST .../translations/{languageCode}/start-translation`) creates a `TranslationValidationRun` and enqueues `TranslationValidationJob` which both translates and back-translation validates sections sequentially using Claude via Hangfire; the **legacy/edit-page path** (`POST .../translations/generate`) calls `GenerateContentTranslationsCommandHandler` synchronously in-process via `IContentTranslationService`, translating but not validating. For **video talks**, an entirely separate `SubtitleProcessingOrchestrator` / `SubtitleProcessingJob` pipeline (ElevenLabs transcription → Claude SRT translation → R2 storage) runs in parallel with text translation but is never coupled to it. For **PDF talks**, slide text and slideshow HTML translation run inside the same `GenerateContentTranslationsCommandHandler` as section content. Gap-filling across all artefacts is handled by `MissingTranslationsJob` (on-demand) and `DailyTranslationScanJob` (2am UTC nightly). There is **no automatic staleness-driven re-translation**: when source content is edited post-publish, existing translations are marked `Stale` via the workflow event log, but re-translation requires an explicit user action.

---

## 2. Translation Triggers

| Trigger | Initiated from | Calls endpoint | Dispatches | Operates on |
|---|---|---|---|---|
| New-wizard Step 5 "Start" per language | `TranslateStep.tsx` handleStart | `POST /toolbox-talks/{talkId}/translations/{languageCode}/start-translation` | `StartTalkTranslationCommand` → enqueues `TranslationValidationJob` | sections, quiz, title, description, for the single language |
| New-wizard Step 5 "Start All" | `TranslateStep.tsx` handleStartAll (1s stagger per language) | Same endpoint per language | Same as above, one job per language | Same |
| Edit-page "Translate" per language | `TranslationWorkflowPanel.tsx` | `POST /toolbox-talks/{id}/translations/generate` | `GenerateContentTranslationsCommand` (synchronous, in-process) | sections, quiz, title, description, slides, slideshow HTML for all requested languages |
| Content generation completion | `ContentGenerationJob.AutoGenerateTranslationsAsync` | (internal, not HTTP) | `GenerateContentTranslationsCommand` | same as edit-page path |
| Slideshow-only generation | `ContentGenerationJob.GenerateSlideshowOnlyAsync` | (internal, not HTTP) | enqueues `MissingTranslationsJob` | slideshow HTML + any other missing artefacts |
| Missing-language gap fill (on-demand) | Code: `MissingTranslationsJob.ExecuteAsync` | (internal job) | `GenerateContentTranslationsCommand` + `SubtitleProcessingOrchestrator.TranslateMissingLanguagesAsync` | all incomplete artefacts per language |
| Daily gap scan | `DailyTranslationScanJob.ExecuteAsync` (2am UTC) | (internal job) | enqueues `MissingTranslationsJob` per qualifying talk | talks created/modified in last 25h missing any translation records |
| Employee language change | `EmployeeLanguageChangeHandler` (event, on employee save) | (internal) | enqueues `MissingTranslationsJob` per assigned talk | only talks assigned to that employee; only if language is new to tenant |
| Video subtitle processing | `SubtitleProcessingOrchestrator.StartProcessingAsync` → `ProcessAsync` | `POST /toolbox-talks/{id}/subtitles/process` | `SubtitleProcessingOrchestrator.ProcessAsync` (Hangfire) | ElevenLabs transcription → English SRT → per-language SRT via Claude |
| Subtitle re-translation / add language | `MissingTranslationsJob.GenerateMissingSubtitleTranslationsAsync` | (internal) | `SubtitleProcessingOrchestrator.TranslateMissingLanguagesAsync` | new language SRT using stored English SRT (no re-transcription) |
| Subtitle retry | `SubtitleProcessingOrchestrator.RetryAsync` | `POST /toolbox-talks/{id}/subtitles/retry` | Re-runs `ProcessAsync` for failed languages | failed subtitle language rows |
| Add target language | `AddTargetLanguagePicker.tsx` | `POST /toolbox-talks/{talkId}/target-languages` | `AddTargetLanguageCommand` (no job) | Only updates `TargetLanguageCodes` JSON; actual translation deferred to next `MissingTranslationsJob` run |
| Reviewer edits section source | `TranslationValidationController` `EditSection` | `PUT .../sections/{idx}/edit` | `TranslationValidationJob` (single-section re-run) | re-validates only the edited section; `PropagateEditedTranslationAsync` pushes to `ToolboxTalkSection.Content` on Accept |
| Reviewer retries section | same controller `RetrySection` | `POST .../sections/{idx}/retry` | `TranslationValidationJob` (single-section re-run) | re-runs back-translation consensus for that section only |
| Session wizard `StartTranslateValidateAsync` | `ContentCreationSessionService` (internal, wizard Step 5 navigation) | `POST /toolbox-talks/create/session/{sessionId}/translate-validate` | one `TranslationValidationJob` per target language; `SubtitleProcessingOrchestrator.StartProcessingAsync` for video | all text artefacts + subtitles for all languages |

---

## 3. Per Content Type Flow

### TEXT TALK (manual sections, no video, no PDF)

```
CREATION PATH (new wizard):
User → Step 1 InputConfig (title, category, SourceLanguageCode, TargetLanguageCodes)
     → Step 2 Parse (manual sections or AI from text)
     → Step 3 Quiz (AI or manual quiz questions)
     → Step 4 Settings (quiz settings, certificate, refresher, due days)
     → Step 5 TranslateStep: "Start" per language / "Start All"
          → WizardTranslationPanel shows state: Initial / AIGenerated / Translating / Validating / Validated / Stale
          → POST /toolbox-talks/{talkId}/translations/{languageCode}/start-translation
          → StartTalkTranslationCommandHandler:
               1. workflow.StartTranslation() → writes TranslationStarted event → state = Translating
               2. creates TranslationValidationRun (IsNewWizard=true, PassThreshold=75)
               3. enqueues TranslationValidationJob(runId)
          → TranslationValidationJob.ExecuteAsync (Hangfire, queue=content-generation):
               a. workflow.StartValidation() → state = Validating
               b. ContentCreationSessionService.LoadSectionsAsync():
                    - substitutes EditedTranslation where reviewer edits exist
               c. per section:
                    - IContentTranslationService.TranslateTextAsync() [Claude API]
                    - GlossaryReplacementService applies locked glossary terms to translated text
                    - ConsensusEngine: Round 1 (Claude Haiku + DeepL back-translate → score)
                    - if inconclusive → Round 2 (+ Gemini)
                    - if still inconclusive → Round 3 (+ Claude Sonnet)
                    - SafetyClassificationService scores safety-criticality
                    - GlossaryTermVerificationService checks translations
                    - upserts TranslationValidationResult row
                    - SignalR: SectionCompleted event
               d. updates TranslationValidationRun totals, Status=Completed
               e. SignalR: ValidationComplete event
          → Frontend WorkflowSubscriber receives ValidationComplete
          → invalidates workflow-state query → WizardTranslationPanel re-renders with Validated state
     → Step 6 ValidateStep:
          → displays section-by-section ValidationSectionCard list
          → reviewer can: Accept (PUT .../accept), Edit (PUT .../edit), Retry (POST .../retry)
          → "Ready to publish" banner appears when allLanguagesReady === true
     → Publish → POST /toolbox-talks/create/session/{sessionId}/publish

LEGACY / EDIT-PAGE PATH:
User edits a talk → TranslationWorkflowPanel → "Translate" button
     → POST /toolbox-talks/{id}/translations/generate
     → GenerateContentTranslationsCommandHandler (synchronous, in-process):
          per language:
               1. workflow.StartTranslation() → state = Translating
               2. IContentTranslationService.TranslateTextAsync() per artefact (title, description, sections, quiz)
               3. writes / upserts ToolboxTalkTranslation row
               4. workflow.RecordTranslationCompleted() → state = AIGenerated
          single SaveChangesAsync() after all languages
          IToolboxTalkNotificationService.NotifyTranslationCompleted()
     → validation is SEPARATE: user must explicitly click "Validate" button
     → POST /toolbox-talks/{id}/validation/validate → StartTalkTranslationCommand (with a new run) → TranslationValidationJob
```

### PDF TALK (AI from PDF, has slides and slideshow HTML)

```
PDF TALK CREATION:
Step 1: Upload PDF → talk gets PdfUrl, SlidesGenerated may become true
Step 2: ContentGenerationJob.ExecuteAsync (Hangfire):
     → extracts text from PDF → AI generates sections and quiz
     → calls AutoGenerateSlidesAsync:
          → SlideshowGenerationService.GenerateSlideshowAsync():
               - hard-deletes all ToolboxTalkSlideshowTranslation rows for this talk
               - sets talk.SlideshowHtml = new HTML
               - sets talk.SlidesGenerated = true
     → calls AutoGenerateTranslationsAsync:
          → builds GenerateContentTranslationsCommand with TargetLanguages
          → GenerateContentTranslationsCommandHandler runs synchronously:
               per language:
                    - TranslateForLanguageAsync():
                         title, description, sections (title + content), quiz, email templates
                         → TranslateSlideshowAsync():
                              - checks SlideshowHtml non-null
                              - skips if ToolboxTalkSlideshowTranslation already exists (it won't — just deleted)
                              - extracts const slides = [...] JS array via regex
                              - finds translatable strings, deduplicates
                              - translates each string individually via TranslateTextAsync()
                              - reconstructs HTML with translated strings
                              - saves new ToolboxTalkSlideshowTranslation row
                         → slide text translation (ToolboxTalkSlideTranslation):
                              - iterates talk.Slides
                              - skips if SlideTranslation already exists for the language
                              - TranslateTextAsync() per slide
                              - creates ToolboxTalkSlideTranslation row

   (DIVERGENCE FROM TEXT TALK: PDF adds slide text and slideshow HTML translation)

Step 5 TranslateStep + Step 6 ValidateStep: same as text talk.

SLIDESHOW REGENERATION (after publish):
POST /toolbox-talks/{id}/generate-slides
     → ContentGenerationJob.GenerateSlideshowOnlyAsync():
          - deletes ALL ToolboxTalkSlideshowTranslation rows (hard delete, bypasses soft-delete)
          - writes new SlideshowHtml
          - chains: enqueues MissingTranslationsJob
     → MissingTranslationsJob:
          - completeness check finds missing ToolboxTalkSlideshowTranslation rows
          - dispatches GenerateContentTranslationsCommand with SlidesGenerated=true flag
          - TranslateSlideshowAsync() re-runs for all required languages
```

### VIDEO TALK (AI from video, has subtitles)

```
VIDEO TALK CREATION:
Step 1: Upload video → SubtitleProcessingOrchestrator.StartProcessingAsync():
     - creates SubtitleProcessingJob (Status=Pending)
     - creates SubtitleTranslation rows: English + one per target language
     - enqueues SubtitleProcessingOrchestrator.ProcessAsync(jobId)

SubtitleProcessingOrchestrator.ProcessAsync (Hangfire, PARALLEL to text pipeline):
     Stage 1: CachedTranscriptWordsJson check:
          - if present: deserialise, SKIP ElevenLabs
          - else: IVideoSourceProvider.GetDirectUrlAsync() → ITranscriptionService.TranscribeAsync (ElevenLabs)
     Stage 2: ISrtGeneratorService.GenerateSrt(words, WordsPerSubtitle) → English SRT string
     Stage 3: Upload English SRT to R2; store SrtUrl on EnglishTranslation; store EnglishSrtContent on job
     Stage 4: per non-English language:
          - ITranslationService.TranslateSrtBatchAsync (Claude API) in batches
          - on batch failure: falls back to English text for that batch (non-fatal)
          - uploads translated SRT to R2
          - updates SubtitleTranslation row (Status=Completed, SrtUrl, SrtContent)
          - SignalR: SubtitleProcessingHub progress event
     Stage 5: SubtitleProcessingJob.Status = Completed
     SignalR: 100% complete

   (DIVERGENCE FROM TEXT TALK: video adds subtitle pipeline, INDEPENDENT of text translation)
   (Text translation for sections/quiz runs concurrently via TranslationValidationJob)
   (No coupling: subtitle completion doesn't wait for text translation and vice versa)

Step 5 TranslateStep: same as text talk (TranslationValidationJob covers text artefacts only)
Step 5 subtitle panel: separate SubtitleProcessingPanel using useSubtitleProcessing hook
     - polls every 5s via refetchInterval while status is non-terminal
     - also subscribes to /hubs/subtitle-processing SignalR

ADD LANGUAGE TO EXISTING VIDEO TALK:
POST /toolbox-talks/{talkId}/target-languages → AddTargetLanguageCommand
     - only updates TargetLanguageCodes JSON on the talk entity
     - NO immediate job enqueue

Next MissingTranslationsJob run:
     → GenerateMissingSubtitleTranslationsAsync():
          - finds latest COMPLETED SubtitleProcessingJob for the talk
          - checks EnglishSrtContent is populated (fails gracefully if not)
          - for each missing language: creates SubtitleTranslation row on the existing job
          - calls TranslateLanguageAsync() reusing stored EnglishSrtContent
          - NO ElevenLabs call (no re-transcription)
     → GenerateMissingContentTranslationsAsync() for text artefacts
```

---

## 4. Per Artefact Translation

| Artefact | When translated | Triggered by | Depends on | Stored in | Source edit → existing translation |
|---|---|---|---|---|---|
| Section title | Every translation run | `GenerateContentTranslationsCommandHandler.TranslateForLanguageAsync` (line 228–252) or `TranslationValidationJob` | Section content must be present | `ToolboxTalkTranslation.TranslatedSections` (JSON) | Left intact, workflow state → Stale, `NeedsRevalidation = true` |
| Section content (HTML) | Every translation run | Same as above | Sections must exist | `ToolboxTalkTranslation.TranslatedSections` (JSON) | Same as above |
| Quiz question text | Every translation run (when `RequiresQuiz=true`) | Same as above | Quiz questions exist | `ToolboxTalkTranslation.TranslatedQuestions` (JSON) | Same as above |
| Quiz answer options | Every translation run | Same (per-option translate loop) | Quiz questions exist | `ToolboxTalkTranslation.TranslatedQuestions` (JSON) | Same as above |
| Talk title | Every translation run | Same; re-translated at publish if changed | Talk must be persisted | `ToolboxTalkTranslation.TranslatedTitle` | Left intact, marked Stale |
| Talk description | Every translation run (non-fatal if fails) | Same | — | `ToolboxTalkTranslation.TranslatedDescription` | Left intact, marked Stale |
| Email subject | Every translation run | `TranslateForLanguageAsync` lines 393–408 | Hard-coded template strings | `ToolboxTalkTranslation.EmailSubject` | Left intact, marked Stale |
| Email body | Same | Same | Same | `ToolboxTalkTranslation.EmailBody` | Left intact, marked Stale |
| Video subtitles (SRT/VTT) | Video processing (`SubtitleProcessingOrchestrator.ProcessAsync`) | `POST .../subtitles/process` or `StartTranslateValidateAsync` or `MissingTranslationsJob` | ElevenLabs transcription must complete; English SRT must be stored | `SubtitleTranslation.SrtContent` + R2 file at `{slug}_{langCode}.srt` | Not stale-marked (no coupling to workflow); requires new subtitle processing run or `TranslateMissingLanguagesAsync` |
| Slide page text (OCR) | Content translation run (skip if already exists for language) | `GenerateContentTranslationsCommandHandler` lines 411–449 | PDF slides must be parsed (`talk.Slides`) | `ToolboxTalkSlideTranslation.TranslatedText` (one row per slide per language) | Not stale-marked; existing rows are skipped (no re-translation unless row is deleted first) |
| Slideshow HTML | Content translation run (skip if already exists) | `GenerateContentTranslationsCommandHandler.TranslateSlideshowAsync` lines 452–663 | `talk.SlideshowHtml` must be non-null | `ToolboxTalkSlideshowTranslation.TranslatedHtml` | Hard-deleted when source slideshow is regenerated; re-translated by subsequently enqueued `MissingTranslationsJob` |
| Course title | Course translation run | (separate course translation path, not fully investigated) | Course must exist | `ToolboxTalkCourseTranslation.TranslatedTitle` | No staleness mechanism found |
| Course description | Same | Same | Same | `ToolboxTalkCourseTranslation.TranslatedDescription` | Same |
| Back-translation validation | Each `TranslationValidationJob` run | `StartTalkTranslationCommand` / `ContentCreationSessionService.StartTranslateValidateAsync` | Primary translation must already exist | `TranslationValidationResult` (per section per run) | Old runs remain; reviewer decisions recorded per run; `NeedsRevalidation = true` on translation when source changes |

---

## 5. Re-Translation Behaviour

### Does the system mark translations stale on source change?

**Yes, via two separate mechanisms:**

**Mechanism 1 — Workflow event (primary, edit-page path):**
File: `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/UpdateToolboxTalk/UpdateToolboxTalkCommandHandler.cs`, lines 156–193

When a published talk's sections, quiz questions, or title/description are edited via the edit form:
1. A staleness change is detected (`anyStaleningChange` boolean).
2. All `ToolboxTalkTranslation` rows for the talk have `NeedsRevalidation = true` set (line 176).
3. `_workflowService.MarkStale(talkId, languageCode)` is called for each language (line 192–193), appending a `MarkedStale` workflow event.
4. The computed workflow state for each language becomes `TranslationWorkflowState.Stale` (value = 7).
5. `WizardTranslationPanel` (line 21–31) renders the `Stale` state as "Stale — needs retranslation" with a "Retranslate" button.

**What counts as a stalening change:**
- Section added or removed
- Section `Title` or `Content` changed
- Question added or removed
- Question text changed (reordering alone does NOT count)
- Talk `Title` or `Description` changed

**What does NOT stale (gaps):**
- Edits via the wizard path (`UpdateToolboxTalkSectionsCommandHandler`, `UpdateToolboxTalkQuestionsCommandHandler`) — those operate only on draft talks and have no stale-marking logic.
- Slide/slideshow source changes — no staleness mechanism for `ToolboxTalkSlideTranslation` or `ToolboxTalkSlideshowTranslation` rows.

**Mechanism 2 — Session cascade reset (wizard path):**
File: `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentCreationSessionService.cs`, lines 1163–1165

When quiz is re-edited in the wizard (`UpdateQuestionsAsync`), all `ToolboxTalkTranslation` rows for the draft talk are **physically deleted** (not marked stale), ensuring `StartTranslateValidateAsync` will run fresh translation on next visit to Step 5.

### Does it auto-clear translations?

**No** — for published talks, translations are never auto-deleted. Only the stale marker is set. The translated text remains readable and in use until the user explicitly triggers re-translation.

For draft talks in the wizard, translations ARE physically deleted when the quiz is re-edited (see above).

### Is there an explicit re-translate affordance?

**Yes:** The `WizardTranslationPanel` (wizard) shows a "Retranslate" button when state is `Stale`. The `TranslationWorkflowPanel` (edit page) shows a "Translate" button that pops an overwrite confirmation dialog when state is `Accepted` or `ReviewerAccepted` (file: `TranslationWorkflowPanel.tsx`, lines 270–421).

### Is re-translation per-section, per-language, or all-at-once?

- **Per language** — each language has its own workflow state and button. "Start All" stirs them with a 1-second stagger but is per-language under the hood.
- Translation itself (within a language) processes **all artefacts in one pass** — title, description, sections, quiz, slides, slideshow are all translated in a single handler invocation.
- Back-translation validation is **per section** within the language's `TranslationValidationJob`, but re-validation (after a reviewer edit) can be **single-section** via the `sectionIndices` parameter.

---

## 6. Add-Language Behaviour

### What endpoint is called?

`POST /toolbox-talks/{talkId}/target-languages` (file: `src/QuantumBuild.API/Controllers/ToolboxTalksController.cs`, line 1614)

### What the `AddTargetLanguageCommand` handler does

File: `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/AddTargetLanguage/AddTargetLanguageCommandHandler.cs`, lines 31–93

1. Validates the language code.
2. Appends the code to `talk.TargetLanguageCodes` JSON array.
3. Saves. **That's all.** No translation job is enqueued. No workflow event is written.

### When does actual translation happen?

Translation for the newly added language is deferred to the next `MissingTranslationsJob` run, which can be triggered by:
- `DailyTranslationScanJob` at 2am UTC the next day.
- Another employee having their `PreferredLanguage` set to the new language (via `EmployeeLanguageChangeHandler`).
- A new content generation event.
- No immediate path — **there is no "add language and immediately translate" user flow.** The `TranslateStep` in the wizard re-renders with the new language in `Initial` state (cache invalidated by `useAddTargetLanguage` hook), allowing the user to manually click "Start" for that language.

### Same code path as initial translation?

**For text artefacts:** When the user clicks "Start" for the new language in the wizard's `TranslateStep`, the same `POST .../translations/{languageCode}/start-translation` endpoint is called, which enqueues `TranslationValidationJob` — same as the initial translation path.

For the legacy `MissingTranslationsJob` background gap-fill, it dispatches `GenerateContentTranslationsCommand` — the same handler used on the edit page, different from the new-wizard path.

**For subtitles:** `MissingTranslationsJob.GenerateMissingSubtitleTranslationsAsync` calls `SubtitleProcessingOrchestrator.TranslateMissingLanguagesAsync`, which reuses the stored English SRT (`job.EnglishSrtContent`) and calls `TranslateLanguageAsync` — this is the same per-language translate step as in the initial processing, but ElevenLabs is NOT re-called. If `EnglishSrtContent` was never persisted (old jobs), subtitle translation for the new language silently fails (returns 0, file: `SubtitleProcessingOrchestrator.cs`, lines 607–615).

**For slide text:** `GenerateContentTranslationsCommandHandler` skips slides already translated (line 421: `if (existingSlideTranslations.ContainsKey(...)) continue`). New language won't have entries, so it translates fresh.

**For slideshow HTML:** Same skip-if-exists pattern (line 521–529). New language translates fresh.

### What state must the talk be in?

- `AddTargetLanguageCommand` has no status guard — it can be called on any talk.
- Actual translation via the wizard path requires the talk to be in `Published` or `Draft` status with sections populated.
- `MissingTranslationsJob` requires `talk.Status == Published` (line 80 check in `MissingTranslationsJob.cs`).

### Cost equivalence vs initial translation?

Adding a language after publish is **equivalent in cost** to initial translation for that language: all the same artefacts are translated. Subtitle add-language avoids the ElevenLabs transcription cost (~largest single cost item) but still runs all SRT translation API calls.

---

## 7. State Model

### Translation entities and their relationships

| Entity | Base class | Per-language key | Status fields | Staleness fields | Notes |
|---|---|---|---|---|---|
| `ToolboxTalkTranslation` | `TenantEntity` | `{ToolboxTalkId, LanguageCode}` unique index | `TranslatedAt` (DateTime) | `NeedsRevalidation` (bool, default false) | Primary text translation store; includes translated sections and quiz as JSON blobs |
| `SubtitleProcessingJob` | `TenantEntity` | One per video per talk (not per-language) | `Status` (SubtitleProcessingStatus enum, 7 values) | `CachedTranscriptWordsJson` for ElevenLabs skip; no staleness field | Parent for subtitle translations |
| `SubtitleTranslation` | `BaseEntity` | `{SubtitleProcessingJobId, LanguageCode}` unique | `Status` (SubtitleTranslationStatus: Pending/InProgress/Completed/Failed) | None | `SrtContent` stored in DB + R2 URL |
| `ToolboxTalkSlideshowTranslation` | `BaseEntity` | `{ToolboxTalkId, LanguageCode}` unique | `TranslatedAt` (DateTime) | None | Hard-deleted on slideshow regeneration |
| `ToolboxTalkSlideTranslation` | `BaseEntity` | `{SlideId, LanguageCode}` unique | None | None | Per-slide OCR text, skip-if-exists on re-run |
| `ToolboxTalkCourseTranslation` | `BaseEntity` | `{CourseId, LanguageCode}` unique | None | None | Soft-delete via query filter; no TranslatedAt |
| `ToolboxTalkVideoTranslation` | `TenantEntity` | `{ToolboxTalkId, LanguageCode}` unique | `Status` (VideoTranslationStatus: 5 values) | None | Scaffolding only, no real dubbing service |
| `TranslationValidationRun` | `TenantEntity` | One run per {talkId, languageCode, timestamp} | `Status` (ValidationRunStatus: 6 values) | `IsNewWizard` (bool) distinguishes path | Indexes on `{TenantId, ToolboxTalkId, LanguageCode}` |
| `TranslationValidationResult` | `BaseEntity` | `{ValidationRunId, SectionIndex}` unique | `ReviewerDecision` (Pending/Accepted/Rejected/Edited) | `NeedsRevalidation` via parent run; `EditedTranslation` and `EditedSource` fields | Upsert pattern prevents race conditions |
| `WorkflowEvent` | `TenantEntity` | `{TargetEntityId, TargetEntitySubKey (=languageCode)}` per event | `EventType` (string) | — | Append-only; `TranslationWorkflowState` is **computed** from last event |

### `TranslationWorkflowState` (derived, not stored)

The 10-value enum is computed by `TranslationWorkflowService.GetState()` from the most-recent `WorkflowEvent.EventType` for the `(talkId, languageCode)` pair. No column stores this value anywhere. Event types → states:

| EventType | → State |
|---|---|
| (no events) | Initial |
| `TranslationStarted` | Translating |
| `TranslationCompleted` | AIGenerated |
| `ValidationStarted` | Validating |
| `ValidationCompleted` | Validated |
| `ReviewerAccepted`, `ReviewerEdited` | ReviewerAccepted |
| `ExternalReviewRequested` | AwaitingThirdParty |
| `ExternalReviewSubmitted`, `ThirdPartyConfirmed` | ThirdPartyReviewed |
| `ExternalReviewRejected`, `ExternalReviewCancelled`, `ExternalReviewDeclined` | ReviewerAccepted (back to previous terminal) |
| `ExternalReviewConfirmed`, `AcceptedAsFinal` | Accepted |
| `MarkedStale` | Stale |
| Unknown | Initial |

### Validation-to-translation entity relationship

```
ToolboxTalk
  ├─ ToolboxTalkTranslation[] (1 per language)
  │    └─ NeedsRevalidation (bool)
  ├─ TranslationValidationRun[] (1+ per language)
  │    └─ TranslationValidationResult[] (1 per section per run)
  │         ├─ EditedTranslation (reviewer's corrected text)
  │         └─ EditedSource (reviewer's corrected source → triggers NeedsRevalidation on Translation)
  ├─ SubtitleProcessingJob[]
  │    └─ SubtitleTranslation[] (1 per language)
  ├─ ToolboxTalkSlideshowTranslation[] (1 per language)
  ├─ ToolboxTalkSlide[]
  │    └─ ToolboxTalkSlideTranslation[] (1 per language per slide)
  └─ WorkflowEvent[] (append-only, per-language state log)
```

---

## 8. Risks and Findings

### R1 — Language names vs codes inconsistency (BACKLOG 1.2.12)

`GenerateContentTranslationsCommand.TargetLanguages` (field on the command record) carries **language name strings** (e.g. `"Polish"`, `"Spanish"`), not ISO codes. The handler converts to code via `ILanguageCodeService.GetLanguageCodeAsync()` internally before calling the workflow service. `MissingTranslationsJob` also converts codes to names before dispatch (line 243–247 of `MissingTranslationsJob.cs`). If a language name has different spellings across call sites the conversion could silently fail. The `StartTalkTranslationCommand` (new-wizard path) uses ISO codes throughout.

File refs: `GenerateContentTranslationsCommand.cs` line 15 (`TargetLanguages` type `IEnumerable<string>`); `MissingTranslationsJob.cs` lines 243–247.

### R2 — Hardcoded English assumptions (BACKLOG §9, 13 code paths)

`SourceLanguageCode` defaults to `"en"` at 13 sites across `ContentCreationSessionService`, `MissingTranslationsJob`, `DailyTranslationScanJob`, `ContentGenerationJob`, `TranslationValidationJob`, `ContentTranslationService`, `SubtitleProcessingOrchestrator`, `ContentExtractionService`, `TranscriptService`, `EmployeeLanguageChangeHandler`. A non-English source language would silently produce English→English translation runs in several paths.

File refs: As listed in BACKLOG §9 (BACKLOG.md line ~1120–1160).

### R3 — `AddTargetLanguage` leaves a gap between registration and translation

When a user clicks "Add Language", the language is registered in `TargetLanguageCodes` immediately but no translation is triggered. The user sees the new language panel in `Initial` state in the wizard's `TranslateStep` and must manually click "Start". For published talks viewed in the edit page, translation is delayed until `DailyTranslationScanJob` runs (up to 24h). There is no in-app signal that the new language needs action.

File refs: `AddTargetLanguageCommandHandler.cs` lines 31–93; `DailyTranslationScanJob.cs` line 119.

### R4 — `DailyTranslationScanJob` coarser completeness check than `MissingTranslationsJob`

`DailyTranslationScanJob` only checks for completely absent `ToolboxTalkTranslation` rows — it does not detect incomplete rows (null `TranslatedTitle`, empty `TranslatedSections`, etc.). `MissingTranslationsJob` does the finer check. If a translation job crashed after creating the row but before populating any fields, `DailyTranslationScanJob` would see the row as "present" and skip the talk.

File refs: `DailyTranslationScanJob.cs` lines 106–119; `MissingTranslationsJob.cs` lines 114–162.

### R5 — Slide translations never re-translated on slide content change

`ToolboxTalkSlideTranslation` has no staleness mechanism. The `TranslateForLanguageAsync` handler skips slides already translated (`existingSlideTranslations.ContainsKey(slide.Id)`, line 421 of `GenerateContentTranslationsCommandHandler.cs`). If slide OCR text changes (e.g. PDF re-uploaded), existing `ToolboxTalkSlideTranslation` rows are not cleared. The `UpdateToolboxTalkCommandHandler` stale-marking logic does not include slides.

File refs: `GenerateContentTranslationsCommandHandler.cs` line 421; `UpdateToolboxTalkCommandHandler.cs` lines 156–193.

### R6 — Slideshow HTML translation silently fails if already-exists (after crash scenario)

`TranslateSlideshowAsync` skips if `ToolboxTalkSlideshowTranslation` already exists (line 521–529 of handler). If a previous slideshow translation attempt partially succeeded (row created, HTML empty or garbled), the skip prevents a clean re-run without manual row deletion.

File refs: `GenerateContentTranslationsCommandHandler.cs` lines 521–529.

### R7 — Subtitle `EnglishSrtContent` required for add-language; old jobs may not have it

`SubtitleProcessingOrchestrator.TranslateMissingLanguagesAsync` requires `job.EnglishSrtContent` to be populated (lines 607–615). This field was presumably added after some jobs were already processed. Adding a new language to a talk with an older subtitle job would silently fail with a logged warning and return 0 translations. User sees no error.

File refs: `SubtitleProcessingOrchestrator.cs` lines 592–615.

### R8 — No soft-delete query filter on `ToolboxTalkSlideshowTranslation` and `ToolboxTalkSlideTranslation`

The entity configurations for these two types do not apply a `HasQueryFilter(!IsDeleted)`. Soft-deleted rows could appear in queries if the tables ever accumulate soft-deleted records. Currently both tables are either hard-deleted (slideshow) or append-only-with-skip (slides), so this is low risk but worth noting.

File refs: Investigation of entity configurations found no query filter on these two types.

### R9 — `ToolboxTalkCourseTranslation` has no `TranslatedAt` field and no staleness marker

If a course title/description is edited after translation, no stale signal is propagated. The only course translation mechanism found is the `BaseEntity`-based table with soft-delete; no workflow events are written for course translations.

### R10 — `ContentTranslationService.TranslateTextAsync` does not propagate `CancellationToken` to `HttpClient`

The method accepts a `CancellationToken` in its signature but does not pass it to `HttpClient.SendAsync()` (noted in agent findings). A long-running Claude API call cannot be cancelled even if the callers cooperate with cancellation tokens.

File refs: `ContentTranslationService.cs` line 269.

### R11 — Reviewer-accepted translations overwritten by re-generation (BACKLOG §10.9.5, UI-guarded but not DB-guarded)

The `TranslationWorkflowPanel` shows an overwrite confirmation when state is `Accepted` or `ReviewerAccepted`. However, background jobs (`MissingTranslationsJob`, `DailyTranslationScanJob`, `ContentGenerationJob`) use `GenerateContentTranslationsCommand` which calls `workflow.StartTranslation()` — this blocks from `Accepted`/`ReviewerAccepted` only when `ConfirmOverwrite=true` is absent. System-triggered jobs pass `TriggeredBy=System` and no overwrite flag, so they should be blocked. The guard is in `TranslationWorkflowService.StartTranslation()` at line 155–190 of the workflow service. Verify this is correctly enforced for all background call sites.

### R12 — Two-path architecture creates parallel artefact sets

The new wizard path (`TranslationValidationJob`) creates `TranslationValidationRun` + `TranslationValidationResult` rows (and workflow events). The legacy path (`GenerateContentTranslationsCommand`) only creates/updates `ToolboxTalkTranslation` rows and calls `RecordTranslationCompleted` on the workflow service. A talk that was translated via the legacy path and then opened in the new wizard has `ToolboxTalkTranslation` rows but may have no `TranslationValidationRun`. The `ValidateStep` and `TranslationWorkflowPanel` show workflow state as `AIGenerated` (correct — `TranslationCompleted` event written), but the reviewer UI requiring per-section results would have nothing to display.

---

## 9. Files Read

### CLAUDE.md / Docs
- `c:\WORK\Contracts - RASCORE\Applications\quantumbuild-lms\CLAUDE.md`
- `c:\WORK\Contracts - RASCORE\Applications\quantumbuild-lms\CLAUDE-archive.md` (referenced)
- `c:\WORK\Contracts - RASCORE\Applications\quantumbuild-lms\BACKLOG.md`
- `c:\WORK\Contracts - RASCORE\Applications\quantumbuild-lms\docs\TRANSLATION_WORKFLOW_DESIGN.md`
- `c:\WORK\Contracts - RASCORE\Applications\quantumbuild-lms\docs\LEARNING_LIFECYCLE.md`

### Frontend — React/TypeScript
- `web/src/features/toolbox-talks/components/learning-wizard/steps/TranslateStep.tsx`
- `web/src/features/toolbox-talks/components/learning-wizard/components/WizardTranslationPanel.tsx`
- `web/src/features/toolbox-talks/components/TranslationWorkflowPanel.tsx`
- `web/src/features/toolbox-talks/components/learning-wizard/steps/ValidateStep.tsx`
- `web/src/features/toolbox-talks/components/detail/AddTargetLanguagePicker.tsx`
- `web/src/features/toolbox-talks/components/ToolboxTalkDetail.tsx`
- `web/src/lib/api/toolbox-talks/use-toolbox-talks.ts`
- `web/src/lib/api/toolbox-talks/toolbox-talks.ts`
- `web/src/lib/api/toolbox-talks/use-subtitle-processing.ts`
- `web/src/lib/api/toolbox-talks/subtitle-processing.ts`
- `web/src/features/toolbox-talks/hooks/use-content-creation.ts`
- `web/src/lib/api/toolbox-talks/content-creation.ts`
- `web/src/features/toolbox-talks/hooks/use-validation-hub.ts`

### Backend — Controllers
- `src/QuantumBuild.API/Controllers/ToolboxTalksController.cs`

### Backend — Application Layer Commands
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/GenerateContentTranslations/GenerateContentTranslationsCommand.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/GenerateContentTranslations/GenerateContentTranslationsCommandHandler.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/StartTalkTranslation/StartTalkTranslationCommand.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/StartTalkTranslation/StartTalkTranslationCommandHandler.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/AddTargetLanguage/AddTargetLanguageCommandHandler.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/UpdateToolboxTalk/UpdateToolboxTalkCommandHandler.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/UpdateToolboxTalkSections/UpdateToolboxTalkSectionsCommandHandler.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/UpdateToolboxTalkQuestions/UpdateToolboxTalkQuestionsCommandHandler.cs`

### Backend — Infrastructure Jobs
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/MissingTranslationsJob.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/DailyTranslationScanJob.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/ContentGenerationJob.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/TranslationValidationJob.cs`

### Backend — Infrastructure Services
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Translations/ContentTranslationService.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Subtitles/SubtitleProcessingOrchestrator.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Workflows/TranslationWorkflowService.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentCreationSessionService.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/EmployeeLanguageChangeHandler.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Slideshow/SlideshowGenerationService.cs`

### Backend — Domain Entities
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Entities/ToolboxTalk.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Entities/ToolboxTalkTranslation.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Entities/SubtitleProcessingJob.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Entities/SubtitleTranslation.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Entities/ToolboxTalkSlideshowTranslation.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Entities/ToolboxTalkSlideTranslation.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Entities/ToolboxTalkCourseTranslation.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Entities/ToolboxTalkVideoTranslation.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Entities/ContentCreationSession.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Entities/ToolboxTalkSettings.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Entities/TranslationValidationRun.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Entities/TranslationValidationResult.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Entities/TranslationDeviation.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Entities/Workflows/WorkflowEvent.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Entities/Workflows/WorkflowReview.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Entities/Workflows/ExternalParticipantInvitation.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Enums/TranslationWorkflowState.cs`

### Backend — Abstractions
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Abstractions/Subtitles/ITranslationService.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Abstractions/Translations/IContentTranslationService.cs`
