# Bulk Learning Generation — Recon

**Read-only recon. No code changed. No design proposed.** This document maps the CURRENT single-learning ("ToolboxTalk") creation pipeline end to end, as verified reality, so a future bulk-SOP-upload design can be written against it. All claims are cited `file:line`, paths relative to repo root.

Scope note: the regulatory ingestion/extraction pipeline (`RegulatoryIngestionController`, `RequirementIngestionService`, etc.) is explicitly out of scope and was not analyzed beyond confirming it shares the same storage abstraction (§1.5).

---

## 0. Two parallel wizards exist

Per `TenantSettings.UseNewWizard` (CLAUDE.md Note 29), there are two independent creation pipelines:

- **New wizard** — `/admin/toolbox-talks/learnings/**`, URL-per-step, creates the `ToolboxTalk` row immediately at step 1 ("talk-first").
- **Legacy wizard** — `/admin/toolbox-talks/create`, single SPA route, state lives in a `ContentCreationSession` until the Translate step, where a draft `ToolboxTalk` is first created ("session-first").

Discriminator confirmed in code: `InitialiseToolboxTalkCommandHandler.cs:143` sets `LastEditedStep = 1` on creation (new wizard only). Both pipelines converge on the same underlying AI generation services and the same `TranslationValidationRun`/reviewer machinery — only the orchestration/session layer differs.

This recon documents both where they diverge materially, and treats the new wizard as primary since it's the active development target (legacy is being phased out per Note 29).

---

## 1. The single-learning creation flow, end to end

### 1.1 Entry points

**New wizard** frontend routes, `web/src/app/(authenticated)/admin/toolbox-talks/learnings/`:
- `new/page.tsx` (Step 1 — `InputConfigStep`)
- `[talkId]/parse/page.tsx`, `[talkId]/quiz/page.tsx`, `[talkId]/settings/page.tsx`, `[talkId]/translate/page.tsx`, `[talkId]/validate/page.tsx`, `[talkId]/publish/page.tsx`
- Step components: `web/src/features/toolbox-talks/components/learning-wizard/steps/*.tsx`

**Legacy wizard**: `web/src/app/(authenticated)/admin/toolbox-talks/create/page.tsx:1-7` renders `<CreateWizard />`; steps are internal React state (`create-wizard/CreateWizard.tsx:142-146`); step components in `create-wizard/steps/*.tsx`.

### 1.2 Stage 1 — Input & Config

**New wizard:**
- Frontend submit: `InputConfigStep.tsx:349-410`
- File upload (if PDF/video/docx): `useUploadSourceFile.ts:27-33` → `POST /toolbox-talks/learning-wizard/upload-source-url` → `ToolboxTalksController.cs:397-418` (`GetUploadSourceUrl`) — generates a **presigned R2 PUT URL** directly (`ToolboxTalksController.cs:408`); the browser PUTs straight to R2 (`useUploadSourceFile.ts:56-64`), bypassing the API for the byte transfer entirely.
- Talk creation: `InputConfigStep.tsx:368-388` → `useInitialiseToolboxTalk.ts:33-36` → `POST /toolbox-talks/initialise` → `ToolboxTalksController.cs:364-389` → `InitialiseToolboxTalkCommandHandler.cs:31-150`. Title-uniqueness check `:36-40`. Creates `ToolboxTalk` with `Status = ToolboxTalkStatus.Draft` (`:124`), `LastEditedStep = 1` (`:143`).

**Legacy wizard:**
- `create-wizard/steps/InputConfigStep.tsx` → `POST /toolbox-talks/create/session` → `ContentCreationController.cs:34-57` (`CreateSession`) → `ContentCreationSessionService.cs:82-175`, `session.Status = ContentCreationSessionStatus.Draft` (`:174`). No `ToolboxTalk` row exists yet.
- File upload: multipart at `ContentCreationController.cs:63-94` (`UploadFile`), or presigned-URL variant `ContentCreationController.cs:560-625` (`GetUploadUrl`/`ConfirmUpload`).

### 1.3 Stage 2 — Parse

**New wizard:**
- `parseTalk()` (`web/src/lib/api/toolbox-talks/toolbox-talks.ts:141-144`) → `POST /toolbox-talks/{id}/parse` → `ToolboxTalksController.cs:425-457` → `ParseToolboxTalkContentCommandHandler.cs:36-58`.
- Guarded to `talk.Status == Draft` only (`:46-49`, else `FailureCode.WorkflowInvalidState` → HTTP 409).
- Text/PDF/Docx: `Draft → Processing → Draft`, `LastEditedStep = 2`, **synchronous**, returns in-request (`:66,96,139` transition in; `:83-84,126-127,165-166` transition out + step bump).
- Video: `Draft → Processing`, `LastEditedStep = 2` (`:183-184`), then enqueues Hangfire `VideoTranscriptionJobForTalk` via `ParseJobScheduler.cs:13-17` — **async**, status stays `Processing` until the job completes; frontend polls.
- Sections persisted via `MaterialiseSectionsAsync` (`:192-226`).

**Legacy wizard:** `POST .../session/{id}/parse` → `ContentCreationController.cs:133-161` → `ContentCreationSessionService.ParseContentAsync` (`:186-390`). Session-only transitions: `Draft → Transcribing`(video)/`Parsing` → `Parsed` or `Failed`. No `ToolboxTalk` entity yet.

### 1.4 Stage 3 — Quiz

**New wizard:** `generateQuiz()` → `POST /toolbox-talks/{id}/quiz/generate` → `ToolboxTalksController.cs:498-531` → `GenerateToolboxTalkQuizCommandHandler.cs` (same `Draft`-only guard). Edits: `PUT /toolbox-talks/{id}/questions` (`:537-569`), `PUT /toolbox-talks/{id}/quiz-settings` (`:575-613`).

**Legacy wizard:** session equivalents at `ContentCreationController.cs:304-427` → `ContentCreationSessionService.GenerateQuizAsync` (`:981-1175`): `Status → GeneratingQuiz` (`:1002`) → `QuizGenerated`/`Failed` (`:1070`/`:1084`).

### 1.5 Stage 4 — Settings & PDF/upload storage

**New wizard:** `PUT /toolbox-talks/{id}/settings` → `ToolboxTalksController.cs:620-656` → `UpdateToolboxTalkSettingsCommandHandler`. Special 409 `TitleNotUnique` code at `:648-649`. Cover image: `POST /toolbox-talks/{id}/cover-image` → **`ToolboxTalkFilesController.cs:294-341`** (separate controller), gated `talk.Status == Draft` (`:321-322`).

**PDF storage trace** (answers "where does the uploaded PDF land"):
- Endpoint: `ToolboxTalkFilesController.cs:122-174` (`UploadPdf`), `[Authorize(Policy = "Learnings.Manage")]`.
- Signature accepts exactly one `IFormFile file` param (`:127-130`).
- Delegates to `IR2StorageService.UploadPdfAsync` → `R2StorageService.cs:164-213`.
- Key pattern: `BuildKey(tenantId, "pdfs", fileName)` → `{tenantId}/pdfs/{fileName}` (`R2StorageService.cs:184`, `BuildKey` at `:928-931`).
- Public URL: `{PublicUrl}/{tenantId}/pdfs/{fileName}` (`:901-907`).
- Entity update: `talk.PdfUrl` / `talk.PdfFileName` set at `ToolboxTalkFilesController.cs:164-165`.
- **vs. regulatory document upload**: `RegulatoryIngestionController.cs:176-219` → `RequirementIngestionService.UploadSourceDocumentAsync` (`:361-392`) → same `IR2StorageService` abstraction, different method `UploadRegulatoryDocumentAsync` (`R2StorageService.cs:714-`). Comment at `:729`: *"No tenant prefix — RegulatoryDocument is system-managed, unlike every other [upload type]."* **Same storage abstraction reused; key structure differs because regulatory docs are system-level, not tenant-scoped.**

**Legacy wizard:** `PUT .../session/{id}/settings` (`ContentCreationController.cs:459-488`), `POST .../session/{id}/cover-image` (`:493-523`) — session JSON, no talk row yet.

### 1.6 Stage 5 — Translate

**New wizard:** `startTalkTranslation()` → `POST /toolbox-talks/{id}/translations/{code}/start-translation` → `ToolboxTalksController.cs:1634-1664` → `StartTalkTranslationCommandHandler.cs:34-96`: verifies language in `talk.TargetLanguageCodes` (`:48-51`), creates a `TranslationValidationRun` with `Status = Pending`, `IsNewWizard = true` (`:67-84`), enqueues via `ITranslationJobScheduler.EnqueueValidation` (`:89`). **`ToolboxTalkStatus` is untouched** — stays `Draft` throughout.

**Legacy wizard:** `POST .../session/{id}/translate-validate` → `ContentCreationController.cs:236-261` → `ContentCreationSessionService.StartTranslateValidateAsync` (`:392-780`). **This is where the draft `ToolboxTalk` is first created**: `Status = ToolboxTalkStatus.Draft` at `:546-557`, added `:611`, `session.OutputTalkId = talkId` (`:613`). Session: `→ TranslatingValidating` (`:662`) → `Validated` (`:640`, no-target-language fast path).

### 1.7 Stage 6 — Validate

Both wizards share the same `TranslationValidationRun`/`TranslationValidationResult` reviewer-decision machinery (`TranslationValidationController.cs`), surfaced via each wizard's own `ValidateStep.tsx`.

### 1.8 Stage 7 — Publish

**New wizard:** `POST /toolbox-talks/{talkId}/publish` → `ToolboxTalksController.cs:664-708` → `PublishToolboxTalkCommandHandler.cs:20-86`. Preconditions (§4 below has full detail): talk not already `Published` (`:31-33`), ≥1 section (`:35-38`), and — **only if `TargetLanguageCodes` is non-empty** — translation-validation gate (`:43-75`). `talk.Status = Published` directly at `:79` — **`ReadyForReview` is never set on the publish path**. Controller then fire-and-forgets `RequirementMappingJob` (`:686-687`) and, if `GenerateSlidesFromPdf`, `ContentGenerationJob.GenerateSlideshowOnlyAsync` (`:692-704`).

**Legacy wizard:** `POST .../session/{id}/publish` → `ContentCreationController.cs:266-299` → `ContentCreationSessionService.PublishAsync` (`:807-979`) → `PublishAsLessonAsync` (`:1405+`) sets `draftTalk.Status = Published` directly (`:1437`); session `→ Publishing` (`:853`) `→ Completed` (`:874`). **This path has no translation-validation gate at all** (see §4).

**Finding:** Neither wizard's publish path ever produces a talk in `ReadyForReview`. That status is only reachable via a *third*, non-wizard path — standalone `POST /toolbox-talks` (`:337-358`) + `POST /{id}/generate` (`:1259-1334`, Hangfire `ContentGenerationJob` → `ContentGenerationService.cs:298`: `Draft → ReadyForReview` when `errors.Count == 0`) — or dedup/reuse paths (`ContentDeduplicationService.cs:470,852`).

### 1.9 Entity status summary

| Enum | File:line |
|---|---|
| `ToolboxTalkStatus` (Draft=1, Processing=2, ReadyForReview=3, Published=4) | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Enums/ToolboxTalkStatus.cs:6-27` |
| `ContentCreationSessionStatus` (legacy wizard only) | `.../Domain/Enums/ContentCreationSessionStatus.cs:6-20` |

### 1.10 Key files

- Controllers: `src/QuantumBuild.API/Controllers/ToolboxTalksController.cs`, `ToolboxTalkFilesController.cs`, `ContentCreationController.cs`
- New-wizard commands: `.../Application/Commands/{InitialiseToolboxTalk,ParseToolboxTalkContent,UpdateToolboxTalkSections,GenerateToolboxTalkQuiz,UpdateToolboxTalkQuestions,UpdateToolboxTalkQuizSettings,UpdateToolboxTalkSettings,PublishToolboxTalk,StartTalkTranslation}/*Handler.cs`
- Legacy engine: `.../Infrastructure/Services/ContentCreation/ContentCreationSessionService.cs` (2488 lines), `ParseJobScheduler.cs`
- Storage: `.../Infrastructure/Services/Storage/R2StorageService.cs`
- Frontend: `web/src/features/toolbox-talks/components/learning-wizard/**` (new), `create-wizard/**` (legacy)

---

## 2. AI generation inventory — what runs, when, sync/async, model, cost shape

All five generation steps use **`claude-sonnet-4-5`** (configured `src/QuantumBuild.API/appsettings.json:27`). Every AI call site confirmed to call `IAiUsageLogger.LogAsync` immediately after a successful response — historical per-call cost data should already exist in `AiUsageLog`, tagged by `AiOperationCategory`.

### 2.1 Content / section generation

- **Trigger**: explicit user action — `POST /{id}/generate` (`ToolboxTalksController.cs:1259`, legacy) or `POST /{id}/smart-generate` (`:1406`, dedup-aware). Never automatic on bare talk creation.
- **Sync/background**: **Background.** `BackgroundJob.Enqueue<ContentGenerationJob>(...)` at `:1317-1318` / `:1486-1487` (concrete class, Note 21 compliant).
- **Orchestration**: `ContentGenerationJob.ExecuteAsync` (`Jobs/ContentGenerationJob.cs:69`) → `ContentGenerationService.GenerateContentAsync` (`Services/ContentGenerationService.cs:42`) chains sections → quiz → (`AutoGenerateSlidesAsync`, `:150`) → (`AutoGenerateTranslationsAsync`, `:152`) in **one job**.
- **Model/call site**: `Services/AiSectionGenerationService.cs:35,85`.
- **Calls per learning**: **1** Claude call for the whole talk (`ContentGenerationService.cs:179-187` → `AiSectionGenerationService.cs:105`, no per-section loop).
- **Usage logging**: `AiSectionGenerationService.cs:124-133` (`SectionGeneration`).

### 2.2 Quiz generation (bundled, inside content generation)

- Same trigger/background path as §2.1 (chained in the same job).
- **Model**: `Services/AiQuizGenerationService.cs:35,87,226`.
- **Calls per learning**: **1–2** — one call for the whole talk (`ContentGenerationService.cs:227-236` → `AiQuizGenerationService.cs:107`), plus a **conditional second call** only if video content included and the final video portion produced no question (`AiQuizGenerationService.cs:141-156,242`).
- **Usage logging**: `AiQuizGenerationService.cs:127-136,257-266` (`QuizGeneration`).

### 2.3 Quiz generation (standalone, new-wizard Step 3)

- **Trigger**: explicit manual button — `POST /{id}/quiz/generate` (`ToolboxTalksController.cs:498`), requires `Draft` + existing sections.
- **Sync/background**: **Synchronous** — controller awaits `_mediator.Send` directly (`:513-524`), handler awaits `AiQuizGenerationService.GenerateQuizAsync` directly (`GenerateToolboxTalkQuizCommandHandler.cs:62-72`). No Hangfire job.
- **Model/calls**: same service as §2.2, but `videoFinalPortionContent: null` is passed explicitly (`:65`), so the retry branch never fires from this entry point — effectively always 1 call.

### 2.4 Slideshow generation

- **Trigger**: three paths — (1) auto inside content-generation job if `GenerateSlidesFromPdf` (`Jobs/ContentGenerationJob.cs:150` → `AutoGenerateSlidesAsync:378`); (2) auto on new-wizard **Publish** if `GenerateSlidesFromPdf` (`ToolboxTalksController.cs:692-697`); (3) manual `POST /{id}/generate-slides` (`:1343`).
- **Sync/background**: paths 1 & 2 background (`ContentGenerationJob.GenerateSlideshowOnlyAsync`, `:277`, `[Queue("content-generation")]`); path 3 **synchronous** — controller directly awaits `GenerateSlideshowAsync` and returns in-request (`:1376-1389`), no job enqueued.
- After path 1/2 succeeds, it fire-enqueues `MissingTranslationsJob` for the new slideshow (`Jobs/ContentGenerationJob.cs:330-331`).
- **Model/calls**: `Services/Slideshow/AiSlideshowGenerationService.cs:44,74,183,305` — **1–2** calls per talk (1 retry only if Claude's response was truncated, `stop_reason == "max_tokens"`, e.g. `:116-135`).
- **Usage logging**: `AiSlideshowGenerationService.cs:419-428` (`SlideshowGeneration`).

### 2.5 Translation generation — highest cost-multiplier by far

- **Trigger**: (1) auto inside content-gen job for uncovered employee-preferred languages (`Jobs/ContentGenerationJob.cs:152` → `AutoGenerateTranslationsAsync:478`); (2) **manual** wizard button `POST /{id}/translations/generate` (`ToolboxTalksController.cs:1545`); (3) auto gap-fill via `MissingTranslationsJob` (dispatched after smart-generate reuse `:1502-1503`, after slideshow-only gen `Jobs/ContentGenerationJob.cs:330-331`, and daily via `DailyTranslationScanJob`).
- **Sync/background**: paths 1 & 3 background; **path 2 (the wizard's own Translate step button) is synchronous** — controller directly awaits `_mediator.Send(GenerateContentTranslationsCommand)` (`ToolboxTalksController.cs:1588-1601`), no job enqueued, meaning a full multi-language translation run can execute entirely on the HTTP request thread.
- **Model/site**: `Services/Translations/ContentTranslationService.cs:38,265`.
- **Calls per learning** (all per target language, outer loop `GenerateContentTranslationsCommandHandler.cs:93`; within each language, one Claude call per individually translated string via `TranslateForLanguageAsync:227`):
  - Title: 1 (`:256-259`)
  - Description: 1 (`:283-286`)
  - Per section: 2 (title + content) — loop `:304`, calls `:306-309`/`:311-314`
  - Per question: 1 (question text, `:345-348`) + 1 **per answer option** (nested loop `:371`, call `:373-376`)
  - Email subject: 1 (`:423-427`); email body: 1 (`:432-436`)
  - Per slide with extracted text: 1 each (loop `:447`, call `:456-459`)
  - Slideshow HTML: 1 per unique translatable string (loop `:603`, call `:607-610`)
  - **Approx total** ≈ `languages × (2 + 2×sections + questions×(1+options) + 2 + slides + unique_slideshow_strings)` — for a talk with 7 sections, 5 questions×4 options, 20 slides, 3 languages: well over 100 individual Claude calls for one learning's translation pass alone.
- **Usage logging**: single shared site `ContentTranslationService.cs:90-99` (`ContentTranslation`) — one `AiUsageLog` row per string translated, so per-call historical cost data already exists at this granularity.

### 2.6 Regulatory requirement mapping

- **Trigger**: fire-and-forget, **only on Publish**, both wizards — new wizard `ToolboxTalksController.cs:686-687`; legacy `ContentCreationSessionService.cs:1677-1678` (draft-exists), `:1775-1776` (fallback), `:2176` (course-level). Never fires during content/quiz/translation generation itself.
- **Sync/background**: background — `BackgroundJob.Enqueue<RequirementMappingJob>(...)` at all four sites, `[AutomaticRetry(Attempts = 1)]`, `[Queue("content-generation")]` (`Jobs/RequirementMappingJob.cs:59-60`).
- **Model/site**: `Jobs/RequirementMappingJob.cs:56,274`.
- **Calls per learning**: **1–2** — one call for the whole talk (all content + all approved requirements for the tenant's sectors in a single prompt, `BuildContentStringAsync:120`, `BuildMappingPrompt:227`, sent `:270`), conditional retry only on JSON parse failure (`:215-219`). Not looped per-requirement.
- **Usage logging**: `Jobs/RequirementMappingJob.cs:301-310` (`RequirementMapping`).

### 2.7 Summary table

| Step | Trigger | Sync/Background | Job class | Calls/learning |
|---|---|---|---|---|
| Sections | Manual (`/generate`, `/smart-generate`) | Background | `ContentGenerationJob` | 1 |
| Quiz (bundled) | Same as above | Background | `ContentGenerationJob` | 1–2 |
| Quiz (standalone) | Manual (`/quiz/generate`) | **Synchronous** | n/a | 1 |
| Slideshow (auto) | Auto (content gen / publish) | Background | `ContentGenerationJob.GenerateSlideshowOnlyAsync` | 1–2 |
| Slideshow (manual) | Manual (`/generate-slides`) | **Synchronous** | n/a | 1–2 |
| Translations (auto) | Auto (content gen / gap-fill) | Background | `ContentGenerationJob` / `MissingTranslationsJob` | many/language |
| Translations (wizard button) | Manual (`/translations/generate`) | **Synchronous** | n/a | many/language |
| Requirement mapping | Auto-on-publish | Background | `RequirementMappingJob` | 1–2 |

---

## 3. Synchronous vs background — implication for bulk

Two generation steps that a bulk flow over 24 files would almost certainly need — **standalone quiz generation** and, critically, **the wizard's own translation-generation button** (§2.5, up to 100+ Claude calls per learning) — currently execute **synchronously on the request thread**, not as Hangfire jobs. Only section/quiz-bundled generation, auto-slideshow, auto-translation-gap-fill, and requirement mapping are already background jobs. A bulk flow reusing the wizard's own translation endpoint as-is would tie up a request thread for a potentially very long multi-language, multi-string translation run — this is a fact about current sync/async placement, not a recommendation.

---

## 4. The review / publish gate — is review the real bottleneck?

**Finding: No forced per-item bottleneck exists in the general case.** The only enforceable backend gate is narrow — non-`Pass` translation-validation sections, and only when target languages are declared. A batch of English-only talks could be published back-to-back with zero human interaction and zero possibility of rejection.

### 4.1 `ReadyForReview` is informational only

Set at `ContentGenerationService.cs:298` (`errors.Count == 0 ? ReadyForReview : Draft`) and `ContentDeduplicationService.cs:470,852` — purely a function of whether AI generation itself errored, not of any human action. **`PublishToolboxTalkCommandHandler` never checks `Status == ReadyForReview`** — it only rejects `Status == Published` (line 31). A talk can be published straight from `Draft` with no status-machine enforcement.

### 4.2 What a reviewer can/must do — nothing is forced except translation review

- **Sections** (`ParseStep.tsx`): editable, but nothing forces an edit or explicit "approve." Step reachability (`stepOrder.ts:50-52`) only requires a talk to exist — no reviewed/approved flag checked.
- **Quiz** (`QuizStep.tsx`): no "approve" concept exists (grep for `approve`/`Approve` returned nothing). Reachability only requires `sections.length > 0 && requiresQuiz` (`stepOrder.ts:54-56`).
- **Regulatory requirement mappings**: confirmation (`Suggested`/`Confirmed`/`Rejected`) happens **after** publish, fire-and-forget, and is never a publish gate.
- **Translation validation — the one real gate**: `ValidateStep.tsx:308-311` states the rule in UI copy: *"Pass sections are accepted automatically. Accept, edit, or retry any Review or Fail sections before publishing."* Backend enforcement mirrors this exactly: `TranslationValidationController.cs:163-166` — `hasPendingDecisions = Status==Completed && Results.Any(r => r.Outcome != Pass && r.ReviewerDecision == Pending)`. **Only non-Pass (Review/Fail) sections require a reviewer decision — Pass sections need none.**

### 4.3 Publish endpoint preconditions — the actual gate, in full

`PublishToolboxTalkCommandHandler.cs` (new wizard), lines 28-76:
1. Talk exists, not deleted (`:28-29`)
2. Not already `Published` (`:31-33`)
3. ≥1 non-deleted `Section` (`:35-38`)
4. **Only if `TargetLanguageCodes` is non-empty**: ≥1 target language has a `Completed` `TranslationValidationRun` (`:43-54`), AND no non-Pass section across completed runs has `ReviewerDecision == Pending` (`:56-75`).

**No check exists** for: quiz question count/existence, section text quality, requirement-mapping confirmation, or `Status == ReadyForReview`. **If `TargetLanguageCodes` is empty (English-only talk), the entire gate (lines 43-76) is skipped — publish is unconditional beyond "has ≥1 section."**

Legacy wizard's `ContentCreationSessionService.PublishAsync` (`:807`) has a different, looser precondition set (session status in `{Parsed, Validated, QuizGenerated}`, `OutputType` set, non-empty parsed sections, a title) — **and has no translation-validation reviewer-decision gate at all.**

### 4.4 Drafts list

`web/src/app/(authenticated)/admin/toolbox-talks/learnings/drafts/page.tsx` — fetches via `useDraftsList()` → `getToolboxTalks({ status: 'Draft' })`, an **exact-match filter** (`GetToolboxTalksQueryHandler.cs:51-53`), so this list shows **only** `Status == Draft` talks; `ReadyForReview`/`Processing` talks never appear here. Per-draft: title, `"Legacy"` badge when `lastEditedStep === null` (`drafts/page.tsx:37-39`), creator/date, step label. Legacy drafts resume to the talk detail page; new-wizard drafts resume to the specific wizard step URL.

### 4.5 Bulk-review/bulk-publish capability

**None found** in the learnings/talks domain. No `bulk`/`PublishAll`/`SelectAll` in `ToolboxTalksController.cs`, no bulk-select UI under `admin/toolbox-talks/talks`. (The only "confirm-all"/"approve-all" patterns in the codebase — `RequirementMappingsController /confirm-all`, `RegulatoryIngestionController /documents/{id}/approve-all` — are in unrelated, out-of-scope domains and are not wired to talk publish.)

**Answer to the key question**: for an English-only batch, publish is unconditional beyond having sections — bulk generation *could* auto-publish with zero review, purely as a matter of what the current publish endpoint checks. Per-item review is only forced when target languages are declared and back-translation produced non-Pass sections; in that case each such section (not each learning) needs an explicit Accept/Edit/Retry decision.

---

## 5. Reference pattern: `BulkEmployeeImportController` / `BulkEmployeeImportJob`

Documented in full as the candidate reference implementation for a bulk SOP flow.

### 5.1 Controller

`src/QuantumBuild.API/Controllers/BulkEmployeeImportController.cs`, class-gated `[Authorize(Policy = "Core.ManageEmployees")]` (`:71`), route `api/employees/bulk-import` (`:70`).

Tenant targeting: `ICurrentUserService.TenantId` (`:63-68,129-132`); `X-Tenant-Id` header is honored only for SuperUser callers (doc comment); `Upload` rejects `tenantId == Guid.Empty` (`:130`) — the case where a SuperUser omits the header.

| Endpoint | Method:Line | Behavior |
|---|---|---|
| `GET /template` | `GetTemplate`, `:112-114` | Static hardcoded CSV (`TemplateCsv`, `:83-87`) as file download. |
| `POST /` | `Upload`, `:124-195` | Validates content-type/size → `IBulkEmployeeImportValidationService.ValidateAsync` on the stream (`:152`) → uploads CSV to R2 (`:169`) → creates `BulkImportSession` directly in `Validated` status (`:177`) → persists `ValidationResultJson` → returns validation summary. |
| `POST /{id}/confirm` | `Confirm`, `:205-249` | Loads session (tenant-filtered) → stuck-session recovery (§5.5) → rejects non-`Validated` with 400 → enqueues `BulkEmployeeImportJob` via **concrete class** (`:242-243`) → returns Hangfire job ID. |
| `GET /{id}` | `GetStatus`, `:257-280` | Loads session, deserialises `ValidationResultJson`/`ProcessingResultJson`, returns combined status DTO. **This is the polling endpoint.** |
| `GET /{id}/failed-rows` | `DownloadFailedRows`, `:290-313` | Builds corrective CSV, preferring post-processing failures over validation failures (`:356-413`); excludes `AlreadyExisted` rows (`:368-369`). |

Session lookup `GetOwnedSessionAsync` (`:322-324`) relies purely on the EF Core tenant query filter, no explicit `WHERE TenantId`.

### 5.2 Session entity

`src/Core/QuantumBuild.Core.Domain/Entities/BulkImportSession.cs`, `BulkImportSession : TenantEntity` (`:11`):

| Field | Line | Purpose |
|---|---|---|
| `CsvR2Key` | 14 | R2 object key |
| `Status` | 16 | `BulkImportStatus`, default `Uploaded` |
| `UploadedAt` | 22 | separate from `CreatedAt` |
| `ValidationResultJson` | 29 | nullable, serialized `BulkImportValidationResult` |
| `ProcessingStartedAt` | 38 | nullable, set the instant job transitions to `Processing`; doc comment ties to the 30-min stuck-recovery window |
| `ProcessingResultJson` | 45 | nullable, serialized `BulkImportProcessingResult` |
| `IsRerun` | 53 | bool, default `false` |

Enum `src/Core/QuantumBuild.Core.Domain/Enums/BulkImportStatus.cs`: `Uploaded=1, Validated=2, Processing=3, Completed=4, Failed=5, Cancelled=6`.

### 5.3 Per-row tracking — JSON blobs, not a child table

- Validation-phase: `BulkImportRowResult` (`Features/BulkImport/BulkImportValidationResult.cs:28-40+`) — `RowNumber`, `Status` (`Valid/Warning/Failed`), messages, normalised fields. List lives in `.Rows` (`:20`), serialized into `ValidationResultJson`.
- Processing-phase: `BulkImportRowOutcome` (`Features/BulkImport/BulkImportProcessingResult.cs:46-64`) — `RowNumber`, `Status` (`Created/Failed/AlreadyExisted`), `EmployeeId`, `LinkedUserId`, `InvitationEmailSent`, `FailureReason`. List lives in `.Rows` (`:39`), serialized into `ProcessingResultJson`.

Both deserialised on demand by `GetStatus`/`DownloadFailedRows` — no separate SQL table per row.

### 5.4 Job class + isolation

`src/Core/QuantumBuild.Core.Infrastructure/Jobs/BulkEmployeeImportJob.cs:34`, `[AutomaticRetry(Attempts = 0)]` (`:33` — rationale: a retry could re-attempt already-succeeded rows).

Concrete-class enqueue (Note 21): `BackgroundJob.Enqueue<BulkEmployeeImportJob>(j => j.ExecuteAsync(id, ct))` (`BulkEmployeeImportController.cs:242-243`), with an explicit comment on why (interface-based enqueue loses the attribute).

Per-row DbContext isolation (Note 23): `IServiceScopeFactory _scopeFactory` (`:39,56`), used per row (`:122-128`) — `await using var rowScope = _scopeFactory.CreateAsyncScope();` then resolving `IEmployeeService` from that scope. Outer constructor-injected `ICoreDbContext` retained only for session-level status writes (`:70-96,161-165,199-213`).

### 5.5 Stuck-session recovery

`BulkEmployeeImportController.cs:212-233`: if `Status == Processing` and `UtcNow - ProcessingStartedAt >= StuckProcessingThreshold` (`TimeSpan.FromMinutes(30)`, `:76`), resets `Status = Validated`, `IsRerun = true`, `ProcessingStartedAt = null`, then falls through to the same enqueue call used for a fresh confirm.

### 5.6 `IsRerun` behavior

Set only in recovery (`:228`). Consumed in `ProcessRowAsync` (`BulkEmployeeImportJob.cs:275-308`): if `isRerun && ErrorCode == DuplicateEmail`, the row is recorded `AlreadyExisted` instead of `Failed` (row already succeeded before the interrupted run). `DownloadFailedRows` excludes `AlreadyExisted` rows from the correction CSV.

### 5.7 Frontend polling

Page: `web/src/app/(authenticated)/admin/employees/bulk-import/page.tsx` — states `"upload"|"validation-summary"|"processing"|"results"` (`:17`). `POLL_INTERVAL_MS = 3000` (`:19`). Polling only active while `flowState === "processing"` (`:34-39`). A `useEffect` (`:71-77`) flips to `"results"` once status is `Completed`/`Failed`. Resume-on-reload via `?session=` URL param (`:42-68`).

Hook `web/src/lib/api/admin/use-bulk-import.ts:39-49` — plain TanStack Query `refetchInterval`, gated entirely by the page's `flowState`, no backoff.

Results panel: `web/src/components/admin/bulk-import-results-panel.tsx` — stat cards, `notableRows` filter (`Failed`/`AlreadyExisted` only, `:131-133`), "Download Failed Rows" button when `failedCount > 0`.

### 5.8 Session status state machine

| Transition | Where | Trigger |
|---|---|---|
| *(none)* → `Validated` | `Upload`, `:177` | Session created directly in `Validated`; **`Uploaded` is never actually assigned anywhere** — the entity default is overwritten before save. |
| `Validated` → `Processing` | `Job.ExecuteAsync`, `:93-96` | Job start, guarded to `Status == Validated` else abort (`:80-86`). |
| `Processing` → `Completed` | `Job.ExecuteAsync`, `:161-165` | Normal end (row-level failures don't block this). |
| `Processing` → `Failed` | `Job.ExecuteAsync` catch, `:190-213` | Unhandled exception outside the per-row try/catch. |
| `Processing`(stuck) → `Validated` | `Confirm`, `:227-232` | Recovery, also sets `IsRerun = true`. |
| `Cancelled` | *(enum exists, no code path sets it)* | Doc comment describes intended use; not implemented. |

---

## 6. Multi-file / ZIP handling — confirmed absent

- No `IFormFile[]`, `List<IFormFile>`, or `IFormFileCollection` anywhere in `src/` or `web/src/`. Every upload controller action takes a single `IFormFile file`: `ContentCreationController.cs:72,501`, `RegulatoryIngestionController.cs:183`, `BulkEmployeeImportController.cs:127`, `LessonParserController.cs:74,125,171`, `SafetyGlossaryController.cs:504`, `ToolboxTalkFilesController.cs:62,129,301`.
- `ZipArchive` exists only for **export**, not import: `ScormPackageService.cs:1,231` (SCORM package export) and its test harness. Two prior recon docs (`docs/scorm-export-recon.md:12`, `docs/multi-document-regulation-recon.md:59,61`) independently confirm no ZIP-import infrastructure exists anywhere.
- `ToolboxTalkFilesController.cs:60-63` (video) and `:127-130` (PDF) both take exactly one `IFormFile` — confirmed single-file at the type level, not just by convention.
- Wizard frontend inputs are strictly single-file: new wizard `InputConfigStep.tsx:652-666` (`<input type="file">`, no `multiple`; drop handler `:621-626` takes `files[0]` only). Legacy wizard `InputConfigStep.tsx:523-529,584-590,649-655` (PDF/Docx/Video) — same pattern, no `multiple` prop on any of the three inputs.

**Confirmed: no multi-file/ZIP handling exists anywhere in the learning-creation path; the wizard is strictly one file at a time.**

---

## 7. Cost gating

### 7.1 `CostEstimationService` exists — but is Corpus-run-only

`src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Validation/CostEstimationService.cs`. Static EUR rate table (`:11-29`): Haiku/Sonnet/Gemini per-1K-token rates, DeepL per-character rate, `Round3EstimatedFraction = 0.30m` (`:29`). `EstimateCorpusRunCostEur` (`:33-80`) caps smoke tests at 5 entries (`:42-44`), approximates tokens as `chars/3.5` (`:53-54`).

**Only consumer**: `AuditCorpusService.cs:22,32,295-296` (`PrepareRunAsync`). Registration `ServiceCollectionExtensions.cs:385`. No other service, job, or controller calls it.

### 7.2 No cost gate wired into any of the five learning-generation steps

Explicit grep of `ContentGenerationJob.cs`, `MissingTranslationsJob.cs`, `TranslationValidationJob.cs`, `RequirementMappingJob.cs` for `CostEstimationService|EstimateCost|ApprovalRequired|CostGate` returned **no matches** in any of the four files. The only "threshold" hits present (`ContentGenerationJob.cs:85,93`; `TranslationValidationJob.cs:236,255,366,1401`) are translation-quality `PassThreshold` values, unrelated to cost. **No cost gate exists for content generation, quiz generation, translation generation, slideshow generation, or requirement mapping.**

### 7.3 Corpus cost-gate mechanics (for judging reusability later — mechanics only, not an endorsement)

`src/QuantumBuild.API/Controllers/PipelineAuditController.cs`:
1. **Prepare/estimate** — `POST corpus/{id}/runs` (`:888-940`): cooldown check `RunCooldown = 10 min` (`AuditCorpusService.cs:27`, check `:269-279`); rejects if a Pending/Running run already exists (`:281-290`); computes cost via `ICostEstimationService` (`:295-296`), persists `CorpusRun{Status=Pending, EstimatedCostEur}` (`:300-320`). Controller then: `requiresConfirmation = cost > €3`, `requiresSuperUserApproval = cost > €10` (`PipelineAuditController.cs:903-904`). `>€10` + non-SuperUser: left `Pending`, not enqueued (`:906-915`). `≤€3`: enqueued immediately (`:917-921`). `€3–€10`: returned without enqueueing, awaits confirm.
2. **Confirm/enqueue** — `POST corpus/{id}/runs/confirm` (`:945-982`): re-checks `>€10 && !SuperUser` → `Forbid()` (`:966-967`); otherwise `EnqueueRunAsync` → `BackgroundJob.Enqueue<CorpusRunJob>` (`AuditCorpusService.cs:336-337`).

Given learning generation is heavier per item than regulatory extraction (§2 shows up to 100+ Claude calls per learning for translation alone), and this mechanism is currently wired to exactly one caller, a 24-file batch's cost is **currently uncapped and unsurfaced anywhere in the learning-generation path** — established as fact, not as a design recommendation.

---

## 8. Where generated learnings land / how they're reviewed

Covered in §4.4 (drafts list) and §1.8/§4.1 (status transitions). Summary: a talk generated via `/generate` lands in `ReadyForReview` (`ContentGenerationService.cs:298`) if generation was fully clean, or stays `Draft` if any generation error occurred (§9 below) — but **neither wizard's own publish flow ever produces or checks for `ReadyForReview`**; both wizards publish straight from `Draft`. The drafts list (`drafts/page.tsx`) only surfaces `Status == Draft` talks, so a `ReadyForReview` talk from the standalone `/generate` path would not appear there — no page was found in this recon that lists `ReadyForReview` talks specifically (out of scope to search further; noted as an open question for design time, not investigated exhaustively).

---

## 9. Failure / partial behavior

### 9.1 `ContentGenerationJob` / `ContentGenerationService`

- `GenerateContentAsync` sets `Status = Processing` at start (`ContentGenerationService.cs:98`).
- **Extraction failure**: resets `Status = Draft`, returns immediately (`:113-134`, reset at `:122`).
- **Section or quiz generation failure** (extraction succeeded): failure recorded into an `errors` list only (`:191-198` sections, `:241-248` quiz) — **execution continues**; `SaveGeneratedContentAsync` still persists whatever succeeded (`:290-295`). Final status: `errors.Count == 0 ? ReadyForReview : Draft` (`:298`). **A partial failure (e.g. sections OK, quiz failed) still saves the sections and lands the talk in `Draft` — not a dedicated "Failed" state, and not rolled back.**
- Outer `catch (Exception ex)` (`:336-385`) for unexpected exceptions: resets `Status = Draft` in a nested try/catch (`:350-373`, reset `:359`), returns `Success:false` **without rethrowing**.
- `ContentGenerationJob.ExecuteAsync`'s own `catch` blocks (`:213-234,235-263`) never touch `ToolboxTalk.Status` (confirmed via grep, no `Status =` in this file) — they send a SignalR failure message and `throw;`, letting Hangfire's `[AutomaticRetry(Attempts = 3)]` (`:67`) retry. Since the inner service self-catches and returns `Success:false` rather than throwing, this outer catch mainly fires for exceptions in `AutoGenerateSlidesAsync`/`AutoGenerateTranslationsAsync` (only reached on `result.Success`) or the initial fetch/save.
- **There is no dedicated "Failed" `ToolboxTalkStatus`.** A generation failure always leaves the talk in `Draft` (clean failure path) or, in the rare outer-catch case, whatever status the DB last had (no write on that path).
- **Retry**: no dedicated retry endpoint — frontend just re-invokes the same parse/generate mutation (new wizard `ParseStep.tsx:257-266`; legacy `ParseStep.tsx:398-410`).

### 9.2 `MissingTranslationsJob`

- Three `catch` blocks, **none set any `ToolboxTalk` status field** (confirmed via grep — no `Status =`/`ToolboxTalkStatus.` in the file). Outer catch (`:210-225`) logs and sends a SignalR "Translations can be generated manually" message. A subtitle-translation-specific catch (`:366-377`) explicitly does not fail the whole job. **No exception is ever rethrown — the job always reports success to Hangfire even on translation failure.**
- No manual retry endpoint; re-running requires re-triggering the originating action (content gen, smart-generate, etc.) or waiting for `DailyTranslationScanJob`.

### 9.3 `RequirementMappingJob`

- Single outer `catch` (`:111-117`) with an explicit comment: `// Don't rethrow — Hangfire job should not fail noisily.` No `ToolboxTalk`/mapping-run status field is set on failure. A secondary `catch (JsonException)` (`:335-340`) on malformed Claude output returns `null`, treated by the caller as "no suggestions" (`:95-99`).
- No manual retry; re-running requires re-triggering publish (its only trigger, §2.6).

### 9.4 Hangfire `[AutomaticRetry]` attributes — actual values

| Job | Attribute | File:line |
|---|---|---|
| `ContentGenerationJob.ExecuteAsync` | `Attempts = 3` | `Jobs/ContentGenerationJob.cs:67` |
| `ContentGenerationJob.GenerateSlideshowOnlyAsync` | `Attempts = 3` | `:275` |
| `MissingTranslationsJob.ExecuteAsync` | `Attempts = 1` | `Jobs/MissingTranslationsJob.cs:61` |
| `RequirementMappingJob.MapRequirementsAsync` | `Attempts = 1` | `Jobs/RequirementMappingJob.cs:59` |

None uses `Attempts = 0` (unlike `BulkEmployeeImportJob`). However, because `MissingTranslationsJob` and `RequirementMappingJob` swallow all their own exceptions and never rethrow, Hangfire's retry setting is effectively moot for those two. `ContentGenerationJob` is the only one where the outer catch rethrows (`throw;`), making its `Attempts = 3` the only one of the three that's actually live.

### 9.5 Net effect for a 24-file bulk flow

No entity is ever left "stuck" in `Processing` on a clean failure path — worst case for a single item is a silent partial result stranded in `Draft` (content-gen) or a silently-skipped translation/mapping step with no error signal on the entity itself (the only signal is a SignalR message and, for content-gen, the general Hangfire retry). There is no per-item terminal failure status anywhere in this pipeline for a bulk process to poll or branch on — a bulk flow would have to infer failure from the absence of expected data (no sections, no translations, no mapping suggestions) rather than reading a status field, since none of the three background jobs write one on failure.

---

## Summary for design-time reference

- **Storage**: PDF/video upload is single-file, presigned-R2-direct for the new wizard's Input step; same `IR2StorageService` abstraction used everywhere including regulatory docs, just a different tenant-scoped vs. system-scoped key builder.
- **Generation cost driver**: translation is the overwhelming cost multiplier (§2.5) — potentially 100+ Claude calls per learning per batch of languages — and it currently runs **synchronously** when triggered from the wizard's own button, not as a background job.
- **Review bottleneck**: not enforced for English-only talks; enforced only per non-Pass translated section when target languages are declared. Nothing forces content/quiz review.
- **Reference pattern available**: `BulkEmployeeImportController`/`BulkEmployeeImportJob` is a complete, working session→validate→confirm→background-process→poll pattern (§5) that could structurally be mirrored, including its per-row-scope DbContext isolation and stuck-session recovery.
- **No multi-file support, no cost gate, no per-item terminal failure status** exist anywhere in the current learning-generation pipeline — all three are currently absent, full stop, not partially built.
