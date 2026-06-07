# Learning Lifecycle Map

> **Purpose:** A complete map of the full learning lifecycle — covering both the create wizard and the post-publish edit surface — so that failure modes, race conditions, and "stuck" states can be reasoned about without re-investigating. This document does not propose fixes.
>
> **Scope (Create Wizard — §1–§9):** `ContentCreationSession`, all artefacts it creates, every transition, every Hangfire job, every field that flows between steps.
>
> **Scope (Edit Surface — §10):** Every action available on the admin edit page for a published `ToolboxTalk` — what each action reads, writes, cascades, and leaves behind. Includes the translate button, slideshow regeneration, section/question editing, and subtitle processing.
>
> **Key files (Create Wizard):**
> - Enum: [ContentCreationSessionStatus.cs](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Enums/ContentCreationSessionStatus.cs)
> - Entity: [ContentCreationSession.cs](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Entities/ContentCreationSession.cs)
> - Service: [ContentCreationSessionService.cs](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentCreationSessionService.cs)
> - Controller: [ContentCreationController.cs](src/QuantumBuild.API/Controllers/ContentCreationController.cs)
> - Frontend wizard: [CreateWizard.tsx](web/src/features/toolbox-talks/components/create-wizard/CreateWizard.tsx)
>
> **Key files (Edit Surface):**
> - Edit page: [web/src/app/(authenticated)/admin/toolbox-talks/talks/[id]/edit/page.tsx](web/src/app/(authenticated)/admin/toolbox-talks/talks/%5Bid%5D/edit/page.tsx)
> - Edit form: [ToolboxTalkForm.tsx](web/src/features/toolbox-talks/components/ToolboxTalkForm.tsx)
> - Translation panel: [ContentTranslationPanel.tsx](web/src/features/toolbox-talks/components/ContentTranslationPanel.tsx)
> - Update command: [UpdateToolboxTalkCommandHandler.cs](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/UpdateToolboxTalk/UpdateToolboxTalkCommandHandler.cs)
> - Translate command: [GenerateContentTranslationsCommandHandler.cs](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/GenerateContentTranslations/GenerateContentTranslationsCommandHandler.cs)
> - Slideshow service: [SlideshowGenerationService.cs](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Slideshow/SlideshowGenerationService.cs)

---

## 1. States

The enum values are **not in execution order**. The actual wizard progression is documented in the transitions section.

| # | Enum Value | Int | Human Name | Meaning | Stable? |
|---|-----------|-----|-----------|---------|---------|
| 1 | `Draft` | 1 | Awaiting Input | Session exists; user is on InputConfig step or hasn't triggered parse yet. Source file may or may not be uploaded. | **Stable resting point** |
| 2 | `Parsing` | 2 | AI Parsing | AI content-parsing is in flight. For Text/PDF this is a synchronous HTTP call that keeps the session in this state for its duration; for Video this is a Hangfire job polled by the frontend. | **In-flight** |
| 3 | `Parsed` | 3 | Sections Ready | AI has produced `ParsedSectionsJson`. User is on ParseStep reviewing/editing sections, or on QuizStep/SettingsStep. Multiple stable visits possible (user can navigate back). | **Stable resting point** |
| 4 | `TranslatingValidating` | 4 | Translation Running | One or more `TranslationValidationJob` Hangfire jobs are in flight. Session holds the run IDs in `ValidationRunIds` and the job IDs in `TranslationJobIds`. | **In-flight** |
| 5 | `Validated` | 5 | Translation Complete | All validation runs have reached a terminal state (Completed or Failed). `ValidationRunIds` may be null if no target languages were selected. Also the resting state for an English-only session that skipped validation entirely. User is on ValidateStep reviewing results, or on PublishStep. | **Stable resting point** |
| 6 | `GeneratingQuiz` | 6 | Quiz Generating | `GenerateQuizAsync` is executing synchronously inside the HTTP request. Despite running synchronously, the status is persisted before the AI call so polling can detect it. | **In-flight** |
| 7 | `QuizGenerated` | 7 | Quiz Ready | `QuestionsJson` and `QuizSettingsJson` are populated. User is on QuizStep editing questions or SettingsStep. | **Stable resting point** |
| 8 | `Publishing` | 8 | Publish In Progress | `PublishAsync` is executing. The talk/course is being written and promoted. | **In-flight** |
| 9 | `Completed` | 9 | Published | `OutputTalkId` or `OutputCourseId` is set to the published entity. Terminal success state. | **Terminal** |
| 10 | `Abandoned` | 10 | Abandoned | User abandoned, or `ExpiredSessionCleanupJob` processed the session after `ExpiresAt`. R2 source files deleted. `IsDeleted = true` (for `AbandonSessionAsync` path only — see §6.9). Terminal. | **Terminal** |
| 11 | `Failed` | 11 | Failed | An operation threw an exception that was caught and written back. The session is in a known-bad state. The user may be able to retry (frontend decides this). | **Quasi-stable** (user may retry parse/quiz from here with a new session or by re-triggering) |
| 12 | `Transcribing` | 12 | Video Transcribing | `VideoTranscriptionJob` is running. `TranscriptionJobId` holds the Hangfire job ID. | **In-flight** |

**Stable resting points** are states where the session sits until the user takes an action. **In-flight** states are transient; the system is supposed to leave them automatically.

**Terminal states:** `Completed` and `Abandoned`. `ExpiredSessionCleanupJob` only cleans sessions that are not in one of these two states.

---

## 2. Transitions

### 2.1 Forward Transitions (happy path)

#### Draft → Draft
- **Trigger:** `UploadFileAsync` ([service:115](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentCreationSessionService.cs#L115)) — uploads a file but doesn't change status. Guard: requires `Status == Draft` (line 123); throws if not.
- **Also:** `UpdateSourceAsync` ([service:155](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentCreationSessionService.cs#L155)) — resets to Draft with `ParsedSectionsJson = null` and `OutputType = null`. No status guard — allowed from **any** status.
- **Also:** `ConfirmUploadAsync` (presigned upload path, [service:1344](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentCreationSessionService.cs#L1344)) — also resets to Draft. **[§6.11 RESOLVED]** Draft-status guard added at line 1352 — throws `InvalidOperationException` if `Status != Draft`. The previous no-guard behaviour (see §6.11) is fixed. Can no longer reset from Parsed/Validated.
- **Data written:** `SourceFileName`, `SourceFileUrl`, `SourceFileType` (on upload); `SourceText`, `ParsedSectionsJson = null`, `OutputType = null` (on source update).
- **Atomic?** Yes — single `SaveChangesAsync`.
- **Failure mode:** If R2 upload fails, no status change occurs. Session stays Draft. No partial state.

---

#### Draft → Transcribing (Video, no transcript)
- **Trigger:** `ParseContentAsync` ([service:224](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentCreationSessionService.cs#L224)) — called from `POST /api/toolbox-talks/create/session/{id}/parse`.
- **Precondition:** `Status == Draft` (line 188), `InputMode == Video`, `TranscriptText == null`, `SourceFileUrl != null`.
- **Data written:** `Status = Transcribing` saved (line 225), then `TranscriptionJobId` saved (line 231) in a second `SaveChangesAsync`.
- **Hangfire enqueued:** `VideoTranscriptionJob` (line 227) — enqueued **after** the first save, **before** the second save. There is a narrow window where the job is enqueued but `TranscriptionJobId` is not yet persisted.
- **Atomic?** No — two separate `SaveChangesAsync` calls.
- **Failure mode:** If the second save (storing `TranscriptionJobId`) fails, the job is already enqueued and will run, but `TranscriptionJobId` on the session will be null.

---

#### Draft → Parsing (Video, has transcript) / Draft → Parsed (Text) / Draft → Parsed (PDF, two phases)
- **Trigger:** `ParseContentAsync` ([service:181](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentCreationSessionService.cs#L181)) — same endpoint.
- **Precondition:** `Status == Draft` (line 188).
- **PDF path (three saves):**
  1. If `InputMode == Pdf` and `TranscriptText == null`, PDF text extraction runs first (lines 192–213). `TranscriptText` is saved in its own `SaveChangesAsync` at line 208.
  2. `Status = Parsing` saved (line 269).
  3. AI parse called synchronously. On success: `ParsedSectionsJson`, `OutputType`, `Status = Parsed` written in single `SaveChangesAsync` (line 289). On controlled failure: `Status = Failed` saved (line 282).
  - **Atomic?** No — three separate saves. If the process dies after save 1 but before save 2, the session stays `Draft` with `TranscriptText` populated — re-triggering parse will skip re-extraction.
- **Text path (two saves):**
  - `Status = Parsing` saved (line 269), AI parse called synchronously. On success: `ParsedSectionsJson`, `OutputType`, `Status = Parsed` written in single `SaveChangesAsync` (line 289).
  - **Atomic?** Yes for the Parsing→Parsed transition (single save at 289). Two total saves.
- **Video-with-transcript path:** `Status = Parsing` saved (line 242), then `ContentCreationParseJob` enqueued (line 245). Returns immediately with `Parsing` status. Frontend polls.
- **Failure mode (Text/PDF):** If `_parserService.ParseContentAsync` throws, the outer catch (line 301) sets `Status = Failed`. If that save also fails, the session is stuck in `Parsing`.

---

#### Transcribing → Parsing (VideoTranscriptionJob)
- **Trigger:** `VideoTranscriptionJob.ExecuteAsync` ([VideoTranscriptionJob.cs:29](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/VideoTranscriptionJob.cs#L29)).
- **Data written (two saves):**
  1. `Status = Transcribing` re-confirmed and saved at lines 60–61 (inside the try block). Redundant — session is already Transcribing — but provides an idempotent re-entry point if Hangfire retries the job.
  2. `TranscriptText`, `TranscriptWordsJson`, `Status = Parsing` — all in one `SaveChangesAsync` at line 94.
- **Then:** Enqueues `ContentCreationParseJob` (line 102).
- **Atomic?** Each save is individually atomic; there are two saves total.
- **Failure mode (controlled):** `result.Success == false` or empty transcript → `Status = Failed`, `ErrorMessage` set (lines 65–73, 81–88). Method returns normally to Hangfire (no exception thrown). No retry.
- **Failure mode (unhandled):** Catch at line 105 sets `Status = Failed`, `ErrorMessage` (lines 113–114). Hangfire retries twice (`[AutomaticRetry(Attempts = 2)]`) only if an exception escapes. If the failure-path save also fails, the session stays `Transcribing`.

---

#### Parsing → Parsed (ContentCreationParseJob)
- **Trigger:** `ContentCreationParseJob.ExecuteAsync` ([ContentCreationParseJob.cs:28](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/ContentCreationParseJob.cs#L28)).
- **Precondition checks (lines 46–60):**
  - If `TranscriptText` is empty: logs warning, **silently exits** (returns without throwing).
  - If `Status != Parsing`: logs warning, **silently exits**. This is the idempotency guard for Hangfire retries.
- **Data written:** `ParsedSectionsJson`, `OutputType`, `Status = Parsed` — all in one `SaveChangesAsync` (line 85).
- **Atomic?** Yes.
- **Failure mode (controlled):** `result.Success == false` → `Status = Failed`, saved (line 77–78). Returns normally to Hangfire — **Hangfire does not retry** because no exception is thrown.
- **Failure mode (unhandled):** Catch at line 91 sets `Status = Failed`, saves (line 99–100). Only if this save itself throws does an exception escape to Hangfire, triggering retries.
- **Retry semantics:** `[AutomaticRetry(Attempts = 3)]` is **effectively inactive for most parse failures**. All exceptions are caught internally; the job returns cleanly. Hangfire retries only when an exception escapes `ExecuteAsync` — in practice only if the `Status = Failed` save at line 100 itself throws. The silent-exit guard (lines 54–60) ensures any genuine Hangfire retry exits immediately if the session status has already advanced past `Parsing`.

---

#### Parsed → GeneratingQuiz
- **Trigger:** `GenerateQuizAsync` ([service:979](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentCreationSessionService.cs#L979)) — called from `POST /api/toolbox-talks/create/session/{id}/generate-quiz`.
- **Also valid from:** `Validated`, `QuizGenerated`, `GeneratingQuiz` (re-generate).
- **Data written:** `Status = GeneratingQuiz` saved immediately (~line 1000). AI calls happen synchronously during the HTTP request. On success: `QuestionsJson`, `QuizSettingsJson` initialized if empty, `Status = QuizGenerated` — single `SaveChangesAsync` (~line 1069).
- **Atomic?** No — two separate saves (status transition, then result).
- **Failure mode:** Exception catch sets `Status = Failed` (~line 1082). If that save fails, session stays `GeneratingQuiz`.

---

#### Parsed/QuizGenerated/Validated → TranslatingValidating (or Validated)
- **Trigger:** `StartTranslateValidateAsync` ([service:390](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentCreationSessionService.cs#L390)) — called from `POST /api/toolbox-talks/create/session/{id}/translate-validate`.
- **Preconditions:** Status must be one of the three; `ParsedSectionsJson` must be non-empty.
- **Pre-step: Cancel stale jobs** — if `session.TranslationJobIds` is non-null, `IBackgroundJobClient.Delete()` is called for each stored job ID (lines 409–441). **[§6.4 RESOLVED]** This is Layer 1 of the orphan-job defence; see §6.4 for Layer 2. `TranslationJobIds` is cleared to null at line 441.
- **Data written — large multi-step operation:**

  | Step | Lines (approx) | Action | Table/Field |
  |------|-------|--------|------------|
  | 1 | ~476–612 | Create OR reuse draft `ToolboxTalk` | `ToolboxTalks` row created or existing metadata updated |
  | 2 | ~498–504 | Soft-delete existing `ToolboxTalkSections` | `ToolboxTalkSections.IsDeleted = true` |
  | 3 | ~506–518 | Insert new `ToolboxTalkSections` | New rows from `ParsedSectionsJson` |
  | 4 | ~523–526 | **Hard-delete** existing `ToolboxTalkQuestions` | `ExecuteDeleteAsync` — raw SQL DELETE, bypasses SetAuditFields interceptor (see §6.12). ⓘ Previously `Remove()` — was silently soft-deleted by the interceptor; corrected in §6.2 batch. |
  | 5 | ~528–530 | Insert new `ToolboxTalkQuestions` | From `QuestionsJson` (skipped when `IncludeQuiz == false`) |
  | 6 | ~535–538 | **Hard-delete** all `ToolboxTalkTranslations` | `IgnoreQueryFilters()` + `ExecuteDeleteAsync` — including soft-deleted rows. ⓘ Previously `IgnoreQueryFilters()` + `Remove()` — was silently soft-deleted; corrected in §6.2 batch. |
  | 7 | ~615–627 | Set `VideoUrl` / `VideoSource` on draft talk if video | `ToolboxTalks` columns |
  | 8 | ~659 | Update `session.TargetLanguageCodes` | Overwritten from request, not from original session creation |
  | 9 | ~660 | `Status = TranslatingValidating` (or `Validated` if no targets) | `ContentCreationSessions.Status` |
  | 10 | ~663–685 | Create one `TranslationValidationRun` per language | New rows, `Status = Pending` |
  | 11 | ~688 | Serialize run IDs to `session.ValidationRunIds` | JSON array of `Guid` |
  | 12 | ~689–699 | `SaveChangesAsync` / `SaveWithCodeRetryAsync` — **all of the above in one save** | All tables above. New-draft-talk path uses `SaveWithCodeRetryAsync` (up to 10 code-collision retries); re-run path uses `SaveChangesAsync`. |
  | 13 | ~702–708 | Enqueue `TranslationValidationJob` per run; capture returned job IDs | Hangfire, **after** save |
  | 13.5 | ~710–711 | Serialize job IDs to `session.TranslationJobIds`; second `SaveChangesAsync` | `ContentCreationSessions.TranslationJobIds` — persisted separately after enqueue so job IDs are available for cancellation on re-trigger |
  | 14 | ~713+ | (Video only) Start subtitle processing | `SubtitleProcessingJob` + updates `session.SubtitleJobId` (separate save) |

- **Fast path (no targets):** Steps 1–8 still execute, but step 9 writes `Validated`, steps 10–13.5 are skipped. `ValidationRunIds` is **not set** (stays null or retains previous value). Steps 1–9 in one save.
- **Atomic?** Steps 1–12 are atomic (single save). Step 13 (enqueue) happens after the save and is not transactional. Step 13.5 is a second save (TranslationJobIds only). Step 14 is a third save. If step 13.5 fails (SaveChangesAsync throws), job IDs are lost but jobs are already running — Layer 2 relevance guard in the jobs will still function, but cancellation on re-trigger won't be possible.
- **Failure mode:** If step 12 save fails, no jobs are enqueued. Session stays at its prior status. If save succeeds but Hangfire enqueue fails (~line 702), runs are in `Pending` with no job executing them — permanently stuck until expiry. This is the **orphan-pending-run** scenario. If subtitle processing (~line 713) throws, the exception is caught and logged; main flow continues.

---

#### TranslatingValidating → Validated (TranslationValidationJob)
- **Trigger:** `TryUpdateSessionStatusAsync` ([TranslationValidationJob.cs:1007](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/TranslationValidationJob.cs#L1007)) — called at the end of each `TranslationValidationJob.ExecuteAsync` (line 255).
- **Mechanism:** Finds sessions in `TranslatingValidating` that reference the just-completed run ID. Loads all run statuses for the session's `ValidationRunIds`. If every run is `Completed` OR `Failed`, sets `Status = Validated`.
- **Important:** A session transitions to `Validated` even if all runs failed. The session reaching `Validated` does not imply any run actually passed.
- **Data written:** `Status = Validated` (line 1053), `SaveChangesAsync` (line 1054).
- **Atomic?** Yes.
- **Failure mode:** The entire `TryUpdateSessionStatusAsync` is wrapped in a try/catch that only logs a warning (line 1064). If it throws, the session stays `TranslatingValidating` without any error surfaced. The next job completing for the same session (if multiple languages) will retry the check.

---

#### Validated/QuizGenerated/Parsed → Publishing → Completed
- **Trigger:** `PublishAsync` ([service:759](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentCreationSessionService.cs#L759)) — called from `POST /api/toolbox-talks/create/session/{id}/publish`.
- **Preconditions:** Status in `{Parsed, Validated, QuizGenerated}`; `OutputType != null`; `ParsedSectionsJson` non-empty; `Title` non-empty (either in request or `SettingsJson`).
- **Republish guard:** `Completed` status is rejected (line 776–779). Once published, the session cannot be re-published.
- **Data written:**
  1. `Status = Publishing`, `SaveChangesAsync` (line 805–806) — early write to prevent double-publish.
  2. `PublishAsLessonAsync` or `PublishAsCourseAsync` (large operations — see §4.9).
  3. `OutputTalkId` or `OutputCourseId` set.
  4. `Status = Completed`, `SaveChangesAsync` (line 826–827).
  5. After success: optionally enqueue `ContentGenerationJob` for slideshow (fire-and-forget, lines 851/874).
- **Atomic?** No — `Status = Publishing` is committed before the publish logic runs. The publish logic does one large `SaveChangesAsync` at its end, but accumulates many EF changes beforehand. If anything throws, the session is set to `Failed` via catch blocks.
- **Failure mode (InvalidOperationException):** Catch at line 895 does `ChangeTracker.Clear()`, re-fetches session, sets `Status = Failed`, saves. The ChangeTracker clear is the Note 23 fix — without it, the contaminated entity would be re-submitted.
- **Failure mode (other Exception):** Same pattern at line 913.
- **Failure mode (cleanup save fails):** If the `Status = Failed` save in the catch also fails, the session is stuck in `Publishing`.

---

#### Any → Abandoned (AbandonSessionAsync)
- **Trigger:** `AbandonSessionAsync` ([service:730](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentCreationSessionService.cs#L730)) — called from `DELETE /api/toolbox-talks/create/session/{id}`.
- **Data written:** `Status = Abandoned`, `IsDeleted = true`, `SaveChangesAsync` (line 739). Then R2 files deleted (lines 742–754, catch swallows failures).
- **Atomic?** DB write is atomic. R2 delete happens after. If R2 delete fails, files are orphaned but session is correctly Abandoned.

---

#### Any non-terminal → Abandoned (ExpiredSessionCleanupJob)
- **Trigger:** Daily Hangfire job ([ExpiredSessionCleanupJob.cs:20](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/ExpiredSessionCleanupJob.cs#L20)).
- **Criteria:** `ExpiresAt < UtcNow` AND `Status ∉ {Completed, Abandoned}` AND `!IsDeleted`.
- **Data written:** For each session, R2 delete attempted first (line 48); if it succeeds, `Status = Abandoned` is set (line 51). If R2 delete throws, the catch block (lines 59–63) logs and moves on — `Status = Abandoned` is **never set** for that session. Single batch `SaveChangesAsync` at line 66 after the foreach loop covers all successfully-processed sessions.
- **Note:** `IsDeleted` is NOT set to true by the cleanup job (unlike `AbandonSessionAsync`). This is an inconsistency — see §6.9.

---

### 2.2 Backward/Cascade Transitions

#### QuizGenerated/Validated → Parsed (UpdateSectionsAsync)
- **Trigger:** `UpdateSectionsAsync` ([service:322](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentCreationSessionService.cs#L322)) — called from `PUT /api/toolbox-talks/create/session/{id}/sections`.
- **In-flight guard (lines 330–347):** If status is in `{Transcribing, Parsing, GeneratingQuiz, TranslatingValidating, Publishing}`, throws `InvalidOperationException` with message "Section editing is not available while {status} is in progress." Returns HTTP 400. The frontend **can** navigate to ParseStep (step 2) in these states (see §7 Q2 — resolved), but any save attempt is rejected.
- **Allowed statuses:** `{Parsed, QuizGenerated, Validated}`.
- **Data written (single SaveChangesAsync, line 381):**
  - `ParsedSectionsJson` — overwritten with new sections.
  - `OutputType` — updated from request.
  - **If `oldStatus == QuizGenerated` OR `oldStatus == Validated`:**
    - `QuestionsJson = null`
    - `Status = Parsed`
  - **Additionally if `oldStatus == Validated`:**
    - `ValidationRunIds = null`
    - `TranslationJobIds = null` ← cleared to prevent stale job-cancel attempts on re-trigger
- **Data NOT cleared in any case:** `TargetLanguageCodes`, `SettingsJson`, `SubtitleJobId`, `QuestionsJson` (when old status was only `Parsed`), any DB rows on the draft `ToolboxTalk` (sections, questions, translations, validation runs).
- **Atomic?** Yes — single `SaveChangesAsync` (line 381).
- **Note:** The DB-level draft talk is NOT cleaned up here. Its sections, questions, translations, and validation runs are stale but remain until the next `StartTranslateValidateAsync` call.

---

#### Parsed → QuizGenerated / Validated → Parsed (UpdateQuestionsAsync) [§6.10 RESOLVED]
- **Trigger:** `UpdateQuestionsAsync` ([service:1117](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentCreationSessionService.cs#L1117)) — called from `PUT /api/toolbox-talks/create/session/{id}/questions`.
- **In-flight guard (lines 1125–1143):** Rejects `{Transcribing, Parsing, GeneratingQuiz, TranslatingValidating, Publishing}` with HTTP 400.
- **Allowed statuses (lines 1134–1147):** `{Parsed, QuizGenerated, Validated}`. Other statuses throw.
- **Data written (single SaveChangesAsync, line 1176):**
  - `QuestionsJson` — overwritten.
  - **If `oldStatus == Validated` (cascade-reset path, lines 1151–1165):**
    - `Status = Parsed` — demotes back to Parsed (not QuizGenerated)
    - `ValidationRunIds = null`
    - `TranslationJobIds = null`
    - Hard-deletes all `ToolboxTalkTranslations` for `session.OutputTalkId` via `ExecuteDeleteAsync` (~line 1161–1164) — prevents the unfiltered unique-index conflict on re-run
  - **If `oldStatus == Parsed` (lines 1171–1173):** `Status = QuizGenerated` (forward promotion).
  - **If `oldStatus == QuizGenerated`:** `Status` unchanged.
- **Data NOT cleared (Validated path):** `TargetLanguageCodes`, `SettingsJson`, `SubtitleJobId`, draft talk sections in DB. Old `TranslationValidationRun` rows are NOT deleted — they remain as orphans accessible via the talk's validation history.
- **Atomic?** Yes — single `SaveChangesAsync` (line 1176). Note: the `ExecuteDeleteAsync` for translations issues raw SQL that executes immediately, outside the EF change tracker — it is NOT rolled back if the subsequent `SaveChangesAsync` fails.
- **⚠ Previous behaviour (§6.10 sharp edge — RESOLVED):** Editing questions from `Validated` previously set status to `QuizGenerated` without clearing `ValidationRunIds` or stale translations. See §6.10 for the full description of the former sharp edge and the fix.

---

#### Any → Any (UpdateQuizSettingsAsync)
- **Trigger:** `UpdateQuizSettingsAsync` ([service:1185](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentCreationSessionService.cs#L1185)) — called from `PUT /api/toolbox-talks/create/session/{id}/quiz-settings`.
- **No status guard** — allowed from any status.
- **Data written (single SaveChangesAsync, ~line 1194):** `QuizSettingsJson` only. No status change, no cascade.
- **Applied at:** `StartTranslateValidateAsync` (synced to draft talk) and `PublishAsync` via `SyncQuizSettingsToTalk`.

---

#### Any → Any (UpdateSettingsAsync)
- **Trigger:** `UpdateSettingsAsync` ([service:1146](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentCreationSessionService.cs#L1146)) — called from `PUT /api/toolbox-talks/create/session/{id}/settings`. Auto-saved by the SettingsStep on every field change.
- **No status guard** — allowed from any status.
- **Data written (single SaveChangesAsync, ~line 1241):** `SettingsJson` only. No status change, no cascade.
- **Applied at:** `StartTranslateValidateAsync` (title/description/category synced to draft talk metadata), `PublishAsync` (title fallback, behaviour fields, slideshow option). If the title in `SettingsJson` differs from the draft talk's stored title at publish time, `PublishAsLessonAsync`/`PublishAsCourseAsync` re-translates it for all languages without requiring a full translation re-run.

---

#### Any → Draft (UpdateSourceAsync)
- **Trigger:** `UpdateSourceAsync` ([service:155](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentCreationSessionService.cs#L155)) — called from `PUT /api/toolbox-talks/create/session/{id}/source`.
- **No status guard** — allowed from any status.
- **Data written (single SaveChangesAsync, line 172):**
  - `SourceText` — updated (Text mode only).
  - `Status = Draft`.
  - `ParsedSectionsJson = null`.
  - `OutputType = null`.
- **Data NOT cleared:** `QuestionsJson`, `ValidationRunIds`, `SettingsJson`, `SubtitleJobId`, `TargetLanguageCodes`, `TranscriptText`, `TranslationJobIds`. After this call the session is `Draft` with downstream fields in a potentially inconsistent state until re-parsed.

---

### 2.3 Transition Summary Diagram

```
                         [user edits sections]
                         ↓ cascade-reset
Draft ──parse──→ Transcribing ──job──→ Parsing ──job──→ Parsed ←──────────────────┐
  │                                                       │  ↑                    │
  └──parse (pdf/text)───────────────────────────────────────┘  └── QuizGenerated  │
                                                           │    ↑                 │
                                              [generate-quiz]──┘                 │
                                              [update-questions from Parsed]       │
                                                           │                      │
                                            Parsed / QuizGenerated / Validated    │
                                                           │                      │
                                            [start-translate-validate]            │
                                                           │                      │
                                        ┌──no targets──────┤                      │
                                        │                  └──has targets──→ TranslatingValidating
                                        │                                          │
                                        ↓                                   [all runs done]
                                     Validated ←──────────────────────────────────┘
                                        │   ↑
                                        │   └── [update-questions from Validated → cascade-resets to Parsed]
                                        │
                                    [publish]
                                        │
                                     Publishing
                                      │     │
                                   success  failure
                                      │     │
                                  Completed Failed
```

---

## 3. Steps × Fields Matrix

Which wizard step depends on which session fields. Use this before any change to a field.

| Session Field | Set In | Read/Required In |
|--------------|--------|-----------------|
| `InputMode` | Session create | ParseStep (determines polling), TranslateStep (subtitle handling), PublishAsync (sets VideoUrl/PdfUrl) |
| `Status` | Every transition | All steps (gate/polling) |
| `SourceText` | InputConfigStep (Text mode) | ParseContentAsync (raw text source) |
| `SourceFileName` | InputConfigStep upload | PublishAsync (PdfFileName on talk) |
| `SourceFileUrl` | InputConfigStep upload | ParseContentAsync (PDF extract), VideoTranscriptionJob (video URL), StartTranslateValidateAsync (VideoUrl on draft talk), SubtitleOrchestrator, PublishAsync (VideoUrl/PdfUrl on talk) |
| `SourceFileType` | InputConfigStep | Not checked downstream (informational only) |
| `TranscriptText` | VideoTranscriptionJob; PDF extraction in ParseContentAsync (line 207–208) | ContentCreationParseJob, ParseContentAsync (PDF path — also stored here after PDF extract) |
| `TranscriptWordsJson` | VideoTranscriptionJob | SubtitleOrchestrator (reuse transcription for subtitle timing) |
| `TranscriptionJobId` | ParseContentAsync (line 230) | Not read by service after write — informational only |
| `ParsedSectionsJson` | ParseContentAsync / ContentCreationParseJob / UpdateSectionsAsync | QuizStep, SettingsStep (title default), TranslateStep, ValidateStep, PublishAsync (core input), StartTranslateValidateAsync |
| `OutputType` | ParseContentAsync / UpdateSectionsAsync | PublishAsync (routes to Lesson vs Course path), TranslateStep display |
| `OutputTalkId` | StartTranslateValidateAsync (first run), PublishAsync | StartTranslateValidateAsync (detects re-run), PublishAsLessonAsync (promotes draft), TranslateStep (fetches validation runs). Set to null by PublishAsCourseAsync in non-Video mode after deleting draft talk. |
| `OutputCourseId` | PublishAsCourseAsync | Navigation to published course after Completed |
| `TargetLanguageCodes` | Session create **and** overwritten in StartTranslateValidateAsync (~line 659) | TranslateStep, ValidateStep (language tabs) |
| `PassThreshold` | Session create | StartTranslateValidateAsync (sets on validation runs) |
| `SectorKey` | Session create | StartTranslateValidateAsync (sets on runs), PublishAsLessonAsync (title/description re-translation) |
| `IncludeQuiz` | Session create | QuizStep (skip condition), StartTranslateValidateAsync (skips question sync), PublishAsync (sets `RequiresQuiz`) |
| `AudienceRole` | Session create | GenerateQuizAsync (quiz style prompt) |
| `PreserveSourceWording` | Session create | ParseContentAsync / ContentCreationParseJob (AI prompt mode) |
| `ReviewerName/Org/Role` | Session create | StartTranslateValidateAsync (sets on each validation run) |
| `DocumentRef` | Session create | StartTranslateValidateAsync (sets on each validation run) |
| `ClientName` | Session create | StartTranslateValidateAsync (sets on each validation run) |
| `AuditPurpose` | Session create | StartTranslateValidateAsync (sets on each validation run) |
| `ExpiresAt` | Session create | ExpiredSessionCleanupJob |
| `ValidationRunIds` | StartTranslateValidateAsync (~lines 688, 596–597) | TranslateStep (fetches run details), ValidateStep (reviewer decisions), TryUpdateSessionStatusAsync (run completion check), Frontend `isStepReachable` guard (steps 5+6 blocked when null/empty). **null = no validation was needed OR cascade reset cleared it** |
| `TranslationJobIds` | StartTranslateValidateAsync (~line 710, after enqueue) | StartTranslateValidateAsync (cancel stale jobs on re-trigger, ~lines 409–441). Cleared by cascade-reset paths (UpdateSectionsAsync from Validated, UpdateQuestionsAsync from Validated). JSON array of Hangfire job ID strings. |
| `QuestionsJson` | GenerateQuizAsync / UpdateQuestionsAsync | QuizStep (hydrate), StartTranslateValidateAsync (synced to draft talk), PublishAsync |
| `QuizSettingsJson` | GenerateQuizAsync / UpdateQuizSettingsAsync | QuizStep (hydrate), StartTranslateValidateAsync (synced to draft talk), PublishAsync |
| `SettingsJson` | SettingsStep (auto-save via UpdateSettingsAsync) | PublishAsync (title fallback, behaviour fields, slideshow option), GetSettingsAsync, StartTranslateValidateAsync (metadata to draft talk) |
| `SubtitleJobId` | StartTranslateValidateAsync (video only, ~line 735) | Not read by service after write — informational |
| `ErrorMessage` | Failed-path writes in jobs | Surfaced to frontend via session DTO |

---

## 4. Downstream Artefacts

### 4.1 ToolboxTalk (draft)

| Event | Operation |
|-------|-----------|
| **Created** | `StartTranslateValidateAsync` — first run, when `session.OutputTalkId == null`. Status = `Draft`, `IsActive = false`. |
| **Updated metadata** | `StartTranslateValidateAsync` — subsequent runs (re-run after cascade reset), title/description/category/quiz settings updated from session. |
| **Promoted to Published** | `PublishAsLessonAsync` — sets `Status = Published`, all behaviour fields, video/PDF URLs. Full trace in §4.9. |
| **Repurposed as Full Video** | `PublishAsCourseAsync` (Video mode) — draft talk is renamed to "{title} — Full Video", sections replaced with a single placeholder section, quiz cleared. Full trace in §4.9. |
| **Soft-deleted (course, non-Video)** | `PublishAsCourseAsync` — if `InputMode != Video`, the draft talk is soft-deleted via `DeleteDraftTalkAsync` after its content is distributed to per-section talks. `session.OutputTalkId` set to null. |
| **Left as orphan** | If session is Abandoned before publish, the draft talk (and all its child rows) remains in the DB. No cleanup. |

### 4.2 ToolboxTalkSection

| Event | Operation | Delete Type |
|-------|-----------|------------|
| **Created** | `StartTranslateValidateAsync` — inserted from `ParsedSectionsJson`. Also in `PublishAsLessonAsync` as part of publish-time re-sync. | — |
| **Soft-deleted on re-run** | `StartTranslateValidateAsync` (~lines 498–504) — old sections soft-deleted before new ones inserted. | `IsDeleted = true` |
| **Soft-deleted on publish** | `PublishAsLessonAsync` (~lines 1485–1486) — old sections soft-deleted, new ones inserted. | `IsDeleted = true` |
| **Query filter** | `ToolboxTalkSectionConfiguration` has `HasQueryFilter(!s.IsDeleted)` — soft-deleted rows are invisible to normal queries. Multiple generations of sections accumulate in the DB. |

### 4.3 ToolboxTalkQuestion

| Event | Operation | Delete Type |
|-------|-----------|------------|
| **Created** | `StartTranslateValidateAsync` (via `SyncQuizQuestionsToTalk`) and `PublishAsLessonAsync`. | — |
| **Hard-deleted on re-run** | `ExecuteDeleteAsync` issued at ~lines 523–526 (`StartTranslateValidateAsync`) and ~lines 1525–1527 (`PublishAsLessonAsync`). Physically removes the row. ⓘ Previously documented as `Remove()`; that pattern was silently soft-deleted by the SetAuditFields interceptor — see §6.12. Corrected via the §6.2 batch fixes. | **Hard delete** (ExecuteDeleteAsync) |
| **Hard-deleted on course publish** | `PublishAsCourseAsync` (~lines 1913–1916) — video-mode full-video talk quiz cleared. Same `ExecuteDeleteAsync` pattern. ⓘ Same prior-Remove() correction. | **Hard delete** (ExecuteDeleteAsync) |
| **No query filter** | Questions are hard-deleted; no accumulation. |

### 4.4 ToolboxTalkTranslation

This is the highest-risk artefact in the wizard. It has an **unfiltered unique index** and **no query filter**.

| Event | Operation | Delete Type |
|-------|-----------|------------|
| **Created** | `TranslationValidationJob` — one row per `(TalkId, LanguageCode)` created during the job run. Also in `PublishAsCourseAsync` for per-section talks. | — |
| **Hard-deleted on re-run** | `ExecuteDeleteAsync` issued at ~lines 535–538 (`StartTranslateValidateAsync`) and ~lines 1161–1164 (`UpdateQuestionsAsync` from Validated). Both use `IgnoreQueryFilters()` to catch ALL rows including previously soft-deleted ghost rows. Physically removes the rows. ⓘ Previously documented as `IgnoreQueryFilters()` + `Remove()`; that pattern was silently soft-deleted by the SetAuditFields interceptor — see §6.12. Corrected via the §6.2 batch fixes. | **Hard delete** (ExecuteDeleteAsync) |
| **Remapped on lesson publish** | `PublishAsLessonAsync` — `TranslatedSections` and `TranslatedQuestions` JSON are rewritten to replace old draft section/question IDs with new published IDs. Full trace in §4.9. |
| **Migrated on course publish** | `PublishAsCourseAsync` — per-section translation data extracted from draft translations and written as new `ToolboxTalkTranslation` rows on each course talk. |
| **Unique index** | `ix_toolbox_talk_translations_talk_language` on `(ToolboxTalkId, LanguageCode)` — **unfiltered** (no `HasFilter()`). No `HasQueryFilter` on the entity either. A soft-deleted row occupies the constraint slot and will cause a unique-constraint violation on re-insertion. |
| **Critical invariant** | The `ExecuteDeleteAsync` calls in `StartTranslateValidateAsync` and `UpdateQuestionsAsync` (Validated path) are the only mechanism preventing the unique-constraint violation on re-run. Using `Remove()` here instead would silently soft-delete and leave ghost rows (see §6.12), causing `23505 duplicate key value` on re-insertion. |

### 4.5 ToolboxTalkSlideshowTranslation

| Event | Operation | Delete Type |
|-------|-----------|------------|
| **Created** | `SlideshowGenerationService.GenerateSlideShowAsync` — one row per `(ToolboxTalkId, LanguageCode)` per slideshow generation run. | — |
| **Hard-deleted on regenerate** | `ExecuteDeleteAsync` issued at [SlideshowGenerationService.cs:80–83](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Slideshow/SlideshowGenerationService.cs#L80) before the new slideshow HTML is saved. Uses `IgnoreQueryFilters()`. Physically removes the rows. ⓘ Previously `Remove()`; silently soft-deleted by the SetAuditFields interceptor — see §6.12. Corrected in §6.2 batch (Site 1). | **Hard delete** (ExecuteDeleteAsync) |
| **Unique index** | `ix_toolbox_talk_slideshow_translations_talk_language` on `(ToolboxTalkId, LanguageCode)` — **unfiltered**, same shape as `ix_toolbox_talk_translations_talk_language`. A soft-deleted row would block re-insertion. This is the same structural risk as §4.4. |
| **Critical invariant** | `ExecuteDeleteAsync` must be used (not `Remove()`) to clear old slideshow translations before inserting new ones. Same reasoning as §4.4. |
| **BACKLOG (Path-B):** | The unique index is still unfiltered and the entity has no `HasQueryFilter`. Filtering the index on `IsDeleted = false` would eliminate the need for the `ExecuteDeleteAsync` workaround. Requires a migration. |

### 4.6 TranslationValidationRun

Key fields and lifecycle. This entity became load-bearing for the wizard state machine after the §6.5 fix.

| Field | Type | Notes |
|-------|------|-------|
| `Id` | `Guid` | PK |
| `TenantId` | `Guid` | Via `TenantEntity`. Query filter: `BypassTenantFilter \|\| TenantId == currentTenantId`. |
| `ToolboxTalkId` | `Guid?` | Nullable — set to null on course publish (`PublishAsCourseAsync` reassociates to `CourseId`). |
| `CourseId` | `Guid?` | Nullable — set when reassociated on course publish. Only one of `ToolboxTalkId`/`CourseId` is non-null at a time. |
| `LanguageCode` | `string` | Target language (e.g. `"es"`). |
| `SectorKey` | `string?` | Sector context for safety classification. |
| `PassThreshold` | `int` | Score threshold (default 75, bumped for safety-critical sections). |
| `SourceLanguage` | `string` | Source language code (typically `"en"`). |
| `Status` | `ValidationRunStatus` | `Pending → Running → Completed \| Failed \| Cancelled`. Marked `Failed` on translation INSERT failure (§6.5 fix) at ~line 929 of TranslationValidationJob.cs. |
| `OverallScore` | `int` | Aggregate score (average of section scores). |
| `OverallOutcome` | `ValidationOutcome` | `Pass`, `Review`, or `Fail`. |
| `SafetyVerdict` | `ValidationOutcome?` | Nullable — only set when safety classification ran. |
| `TotalSections` | `int` | Count set once sections are loaded. 0 if run failed before sections loaded. |
| `PassedSections` / `ReviewSections` / `FailedSections` | `int` | Per-outcome counts. |
| `ReviewerName/Org/Role` | `string?` | Audit metadata — set from session fields at run creation. |
| `DocumentRef` / `ClientName` / `AuditPurpose` | `string?` | Audit metadata. |
| `PreFlightScanJson` | `string?` | JSON output of pre-flight scan (non-blocking; null if scan failed). |
| `StartedAt` / `CompletedAt` | `DateTime?` | Timestamps. |
| `AuditReportUrl` | `string?` | R2 URL of the generated audit PDF (null until `ValidationReportJob` runs). |
| `PipelineVersionId` | `Guid?` | Nullable — null for runs that predate the pipeline-version feature. |

**Notable gap:** No `ErrorMessage` column. The §6.5 fix marks runs as `Failed` via status but cannot persist the failure cause. The user sees only "Failed" status with no actionable detail. **BACKLOG:** add `ErrorMessage` column and surface it in the TranslateStep/ValidateStep UI.

**Event table:**

| Event | Operation |
|-------|-----------|
| **Created** | `StartTranslateValidateAsync` (~lines 663–685) — one per filtered target language, `Status = Pending`. |
| **Updated** | `TranslationValidationJob` — status updates (`Running → Completed/Failed`), aggregate scores, `SafetyVerdict`. |
| **Marked Failed on error** | `GenerateTranslationForSectionsAsync` catch block (~line 929) — sets `Status = Failed`, `CompletedAt = UtcNow`, saves. **[§6.5 RESOLVED]** Previously, this catch returned `null` silently, leaving the run's status at `Running` and causing the session to stay `TranslatingValidating` forever. |
| **Orphaned on cascade reset** | When `UpdateSectionsAsync` or `UpdateQuestionsAsync` runs from `Validated` status, `session.ValidationRunIds` is set to null but the `TranslationValidationRun` rows are NOT deleted. They remain in the DB linked to the draft talk. New runs created by the next `StartTranslateValidateAsync` call are separate rows; old runs become orphaned. Orphaned runs remain accessible via the talk's validation history endpoints. |
| **Reassociated on course publish** | `PublishAsCourseAsync` — `run.CourseId = course.Id`, `run.ToolboxTalkId = null`. Separate `SaveChangesAsync`. |
| **Not deleted on lesson publish** | Runs survive lesson publish. |
| **Soft-delete** | Validation runs can be soft-deleted via `DELETE /api/toolbox-talks/{talkId}/validation/runs/{runId}`. |

**Cascade rules from `TranslationValidationRunConfiguration`:** cascade-delete to `TranslationValidationResult` rows on hard-delete. Soft-deleting the run does NOT cascade; only a physical delete of the run row would trigger it.

### 4.7 TranslationValidationResult

| Event | Operation |
|-------|-----------|
| **Created/updated** | `TranslationValidationService.ValidateSectionAsync` — upsert pattern: queries for existing `{ValidationRunId, SectionIndex}`, updates if found, inserts if not. |
| **Cascade-deleted** | `TranslationValidationRunConfiguration` has `OnDelete(DeleteBehavior.Cascade)` for results. Soft-deleting the run does not cascade; only a hard-delete of the run row would. |

### 4.8 R2 Storage Files

| File | Created By | Path | Deleted By |
|------|-----------|------|------------|
| Source file (video/PDF) | `UploadFileAsync` or presigned-upload flow | `{tenantId}/sessions/{sessionId}/{filename}` | `AbandonSessionAsync` (line 746), `ExpiredSessionCleanupJob` (line 48) |
| Session cover image | `UploadCoverImageAsync` | `{tenantId}/sessions/{sessionId}/cover.{ext}` | Not explicitly deleted on abandon or publish |
| Slideshow HTML | `ContentGenerationJob.GenerateSlideshowOnlyAsync` (post-publish) | Stored in-DB as `ToolboxTalk.SlideshowHtml`, not R2 | — |
| Subtitle files (SRT) | `SubtitleProcessingOrchestrator` (triggered by `StartTranslateValidateAsync`) | `{tenantId}/subtitles/{talkId}/{lang}.srt` | Not deleted during wizard |
| Validation audit report | `ValidationReportService` (post-validation) | `{tenantId}/validation-reports/{runId}.pdf` | Not deleted during wizard |

**Note:** If a session is Abandoned after `StartTranslateValidateAsync` has run (subtitle job started, draft talk created), the source file is deleted from R2 but the subtitle files and draft talk are NOT cleaned up.

---

### 4.9 Publish Path — Full Trace

#### `PublishAsLessonAsync` — Draft Talk Promote Path

This path executes when `session.OutputTalkId != null` (a draft talk was created by `StartTranslateValidateAsync`). All changes are accumulated in-memory and flushed in a **single `SaveChangesAsync`** at the end of the method.

> **Line numbers:** This method starts at ~line 1403 in the current HEAD. The table below gives approximate line references; verify against HEAD before relying on them.

| Step | Action |
|------|--------|
| 1 | Load draft talk (`IgnoreQueryFilters`, `!IsDeleted`) |
| 2 | Validate title uniqueness (excluding the draft talk itself) |
| 3 | Capture `oldTitle` / `oldDescription` for staleness detection |
| 4 | Set `Title`, `Description`, `Category`, `SourceLanguageCode`, `Status = Published`, `VideoUrl/VideoSource` or `PdfUrl/PdfFileName` |
| 5 | Apply behaviour fields from `SettingsJson`: `IsActive`, `GenerateCertificate`, `MinimumVideoWatchPercent`, `AutoAssignToNewEmployees`, `AutoAssignDueDays`, `RequiresRefresher`, `RefresherIntervalMonths` |
| 6 | Re-apply custom `Code` from request if provided (validated for uniqueness) |
| 7 | Load existing section rows; capture ordered `oldSectionIds` |
| 8 | Soft-delete old sections (`IsDeleted = true`) |
| 9 | Insert new sections from `ParsedSectionsJson`; collect `newSectionIds` in same order |
| 10 | (IncludeQuiz only) Capture `oldQuestionIds`; **hard-delete** old questions via `ExecuteDeleteAsync` (~line 1525–1527); insert new questions from `QuestionsJson`; collect `newQuestionIds` from change tracker. ⓘ Previously `Remove()`; silently soft-deleted by interceptor — corrected in §6.2 batch (Site 6). |
| 11 | **Remap `TranslatedSections`**: for each `ToolboxTalkTranslation` (via `IgnoreQueryFilters`), iterate old→new section ID pairs; call `ExtractTranslatedSectionForId` to extract + remap one element; write new JSON array |
| 12 | **Remap `TranslatedQuestions`**: same pattern for question IDs. Skipped with warning log if question counts don't match. |
| 13 | If `oldTitle != request.Title` OR `oldDescription != request.Description`: re-translate for each language via `_contentTranslationService.TranslateTextAsync`; update `TranslatedTitle`/`TranslatedDescription` on each translation row |
| 14 | **Single `SaveChangesAsync`** — commits steps 4–13 atomically |
| 15 | Enqueue `RequirementMappingJob` (fire-and-forget) |

**`ExtractTranslatedSectionForId`** and **`ExtractTranslatedQuestionForId`**: static helpers that parse the JSON array, find the element by old ID, replace with new ID, and return a single-element JSON array. If the JSON is malformed or the ID is not found, return `null` — the affected translation entry is silently omitted from the remapped array.

#### `PublishAsLessonAsync` — No-Draft Fallback Path

Only reached when `session.OutputTalkId == null` (e.g., English-only session that skipped translation entirely). Creates a new `ToolboxTalk` directly with `Status = Published`. No section/question/translation remap needed. Uses `SaveWithCodeRetryAsync` (up to 10 attempts on code uniqueness collision). Enqueues `RequirementMappingJob`.

#### `PublishAsCourseAsync` — Course Distribution Path

Draft translations and sections are loaded **before** any mutations to prevent data loss during distribution.

| Step | Action |
|------|--------|
| 1 | Validate course title uniqueness |
| 2 | Capture draft sections, draft translations, draft question IDs BEFORE mutations |
| 3 | Create `ToolboxTalkCourse` entity |
| 4a | **(Video mode only)** Repurpose draft talk as "Full Video" course item (OrderIndex 0): rename, soft-delete draft sections, insert placeholder "Video" section, **hard-delete** questions via `ExecuteDeleteAsync` (~lines 1913–1916), update translation rows (title suffix, `TranslatedSections = "[]"`, `TranslatedQuestions = null`), regenerate Code |
| 4b | For each section: create a new Published `ToolboxTalk` with that section as its sole section. Copy full quiz question set. Migrate per-section translation from draft translations (extract+remap section+question IDs). Create `ToolboxTalkTranslation` rows per language. Add as course item. |
| 5 | Create `ToolboxTalkCourseTranslation` rows from draft translations. Re-translate title/description if changed at publish time. |
| 6 | `SaveWithCodeRetryAsync` — commits steps 3–5 atomically (up to 10 code retry attempts) |
| 7 | Reassociate validation runs: `run.CourseId = course.Id`, `run.ToolboxTalkId = null`. Second `SaveChangesAsync` (only if runs > 0). |
| 8 | **(Non-Video only)** `DeleteDraftTalkAsync` (~line 2292): soft-delete draft talk + sections + translations; hard-delete questions; single `SaveChangesAsync`. Set `session.OutputTalkId = null`. |
| 9 | Enqueue `RequirementMappingJob` (fire-and-forget) |

#### Republish Guard

`PublishAsync` line 776: status must be in `{Parsed, Validated, QuizGenerated}`. `Completed` status is rejected. **Once a session is published, the wizard cannot re-publish it.** The published talk/course can be edited through the normal edit APIs independently.

---

## 5. Hangfire Jobs

### 5.1 Jobs in the Wizard Pipeline

| Job | Queue | Retry | Enqueued By | Enqueue Site | Trigger Condition |
|-----|-------|-------|-------------|-------------|-------------------|
| `VideoTranscriptionJob` | `content-generation` | 2 | `ContentCreationSessionService.ParseContentAsync` | [service:227](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentCreationSessionService.cs#L227) | Video mode, no transcript |
| `ContentCreationParseJob` | `content-generation` | 3 | `VideoTranscriptionJob.ExecuteAsync` | [VideoTranscriptionJob:102](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/VideoTranscriptionJob.cs#L102) | After successful transcription |
| `ContentCreationParseJob` | `content-generation` | 3 | `ContentCreationSessionService.ParseContentAsync` | [service:245](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentCreationSessionService.cs#L245) | Video mode, transcript already exists (retry path) — **job ID discarded, not saved to session** |
| `TranslationValidationJob` | `content-generation` | 1 | `ContentCreationSessionService.StartTranslateValidateAsync` | [service:~702](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentCreationSessionService.cs#L702) | One per target language. **Job IDs captured and persisted to `session.TranslationJobIds`** (~line 710) so stale jobs can be cancelled on re-trigger. |
| `ContentGenerationJob` (slideshow) | default | unknown | `ContentCreationSessionService.PublishAsync` | [service:851](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentCreationSessionService.cs#L851) / [service:874](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentCreationSessionService.cs#L874) | `settings.GenerateSlideshow = true`, post-publish |
| `RequirementMappingJob` | default | unknown | `PublishAsLessonAsync` / `PublishAsCourseAsync` | [service:~1591](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentCreationSessionService.cs#L1591) | Always, on successful publish |
| `ExpiredSessionCleanupJob` | default | 1 | Hangfire recurring schedule | [ExpiredSessionCleanupJob.cs](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/ExpiredSessionCleanupJob.cs) | Daily |

### 5.2 Job Outcomes and Session States Left Behind

| Job | On Success | On Controlled Failure (result.Success = false) | On Exception |
|-----|-----------|-----------------------------------------------|-------------|
| `VideoTranscriptionJob` | `TranscriptText`/`TranscriptWordsJson` written; Status → `Parsing`; enqueues `ContentCreationParseJob` | Status → `Failed`, `ErrorMessage` set; returns cleanly (**no Hangfire retry**) | Status → `Failed`, `ErrorMessage` set; Hangfire retries up to 2 times |
| `ContentCreationParseJob` | Status → `Parsed`, `ParsedSectionsJson` set | Status → `Failed`; returns cleanly (**Hangfire does NOT retry** — no exception thrown) | Status → `Failed`; Hangfire retries up to 3 times **only if the `Status = Failed` save itself threw** |
| `TranslationValidationJob` | Run → `Completed`; calls `TryUpdateSessionStatusAsync`; if all runs done, session → `Validated` | Run → `Failed` (set in `GenerateTranslationForSectionsAsync` catch, ~line 929); `TryUpdateSessionStatusAsync` then sees the run as terminal and can advance the session to `Validated`. **[§6.5 RESOLVED]** Previously, failure path returned `null` silently, leaving run `Running` and session stuck. | Run → `Failed` (set in top-level catch at ~line 288); session advances to `Validated` on next `TryUpdateSessionStatusAsync` call |
| `ContentGenerationJob` (slideshow) | Slideshow HTML written to `ToolboxTalk.SlideshowHtml` | No session impact | No session impact (fire-and-forget) |
| `RequirementMappingJob` | Requirement mappings created | No session impact | No session impact (fire-and-forget) |

**`ContentCreationParseJob` retry note:** `[AutomaticRetry(Attempts = 3)]` at line 15 annotates the class, but Hangfire only retries when `ExecuteAsync` throws an unhandled exception. The job catches everything internally and returns cleanly in both success and failure cases. In practice the annotation only covers the edge case where the `Status = Failed` cleanup save at line 100 itself throws (DB outage at exactly that moment). The silent-exit guard at lines 54–60 ensures any such Hangfire retry exits immediately if the session has already advanced past `Parsing`.

### 5.3 `TranslationValidationJob` Session Transition Detail

The transition from `TranslatingValidating` to `Validated` happens inside `TryUpdateSessionStatusAsync` ([TranslationValidationJob.cs:1007](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/TranslationValidationJob.cs#L1007)):

1. Load all sessions for the tenant with `Status == TranslatingValidating` and `ValidationRunIds != null` (lines 1012–1018).
2. Build `runIdString = validationRunId.ToString()` (line 1020).
3. For each session, substring-check `session.ValidationRunIds.Contains(runIdString)` (line 1024) — fast pre-filter.
4. Deserialize `ValidationRunIds` JSON to `List<Guid>` (line 1031).
5. Check `runIds.Contains(validationRunId)` (line 1038) — precise match after deserialization.
6. Load statuses for all run IDs (filter: `!r.IsDeleted`) — lines 1042–1046.
7. `allComplete = allRunStatuses.Count > 0 && allRunStatuses.All(s == Completed || s == Failed)` (lines 1048–1049).
8. If `allComplete`, set `Status = Validated` (line 1053), `SaveChangesAsync` (line 1054).

**Edge cases:**
- If a run row is soft-deleted between step 6 and step 7 (unlikely), `allComplete` could be true with fewer runs than expected.
- If `ValidationRunIds` JSON is malformed, `JsonSerializer.Deserialize` throws at line 1031; the inner `catch` at line 1033 silently `continue`s, and the session never transitions.
- `TryUpdateSessionStatusAsync` is wrapped in its own outer try/catch (lines 1009/1064) — any exception is swallowed as a warning.

---

## 6. Known Sharp Edges

### 6.1 Note 23 Trap — EF Tracker Contamination After Caught `DbUpdateException`

**Where:** `PublishAsync` error-handling path ([service:895–929](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentCreationSessionService.cs#L895)).

**What:** When `PublishAsLessonAsync` throws after partially accumulating EF changes, the `ChangeTracker` still holds those changes. Any subsequent `SaveChangesAsync` on the same DbContext instance re-submits everything, including whatever caused the original failure.

**Current fix:** The catch blocks at lines 900 and 918 call `((DbContext)_dbContext).ChangeTracker.Clear()`, then re-fetch the session entity fresh before writing `Status = Failed`. This pattern breaks the contamination cycle.

**Risk:** Any new catch block added to `PublishAsync` that does NOT call `ChangeTracker.Clear()` before a save will re-introduce the bug.

### 6.2 Unfiltered Unique Index on `ToolboxTalkTranslation` — Partial Closure

**Index:** `ix_toolbox_talk_translations_talk_language` on `(ToolboxTalkId, LanguageCode)`.

**Configuration:** `ToolboxTalkTranslationConfiguration.cs` — no `HasFilter()` clause.

**Also:** `ToolboxTalkTranslation` has an `IsDeleted` column but NO `HasQueryFilter` in its configuration — unlike `ToolboxTalkSection` and `ToolboxTalkQuestion`. Soft-deleted translation rows are visible to normal queries unless `IgnoreQueryFilters()` is explicitly called.

**Consequence:** A soft-deleted `(TalkId='X', LanguageCode='es')` row blocks any attempt to insert a new row with the same key, yielding Postgres error `23505 duplicate key value violates unique constraint`.

**Partial closure (this batch):** The eight `.Remove()` call sites that intended hard-deletes on `BaseEntity` descendants were silently soft-deleting due to the SetAuditFields interceptor (see §6.12). All eight have been converted to `ExecuteDeleteAsync`:

| Site | File:Line (approx) | Context |
|------|--------------------|---------|
| 1 | [SlideshowGenerationService.cs:80–83](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Slideshow/SlideshowGenerationService.cs#L80) | `ToolboxTalkSlideshowTranslation` cleanup before regenerate |
| 2 | [UpdateToolboxTalkScheduleCommandHandler.cs:92–108](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/UpdateToolboxTalkSchedule/UpdateToolboxTalkScheduleCommandHandler.cs#L92) | `ToolboxTalkScheduleAssignment` cleanup — with `Detach` to suppress EF orphan-removal phantom soft-delete |
| 3 | [ContentCreationSessionService.cs:~523–526](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentCreationSessionService.cs#L523) | `ToolboxTalkQuestion` in `StartTranslateValidateAsync` |
| 4 | [ContentCreationSessionService.cs:~535–538](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentCreationSessionService.cs#L535) | `ToolboxTalkTranslation` in `StartTranslateValidateAsync` |
| 5 | [ContentCreationSessionService.cs:~1161–1164](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentCreationSessionService.cs#L1161) | `ToolboxTalkTranslation` in `UpdateQuestionsAsync` (cascade-reset from Validated) |
| 6 | [ContentCreationSessionService.cs:~1525–1527](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentCreationSessionService.cs#L1525) | `ToolboxTalkQuestion` in `PublishAsLessonAsync` |
| 7 | [ContentCreationSessionService.cs:~1913–1916](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentCreationSessionService.cs#L1913) | `ToolboxTalkQuestion` in `PublishAsCourseAsync` (video-mode full-video talk) |
| 8 | [ProcessToolboxTalkScheduleCommandHandler.cs:~237–253](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs#L237) | `ToolboxTalkScheduleAssignment` in `RefreshAssignmentsForAllEmployees` — with `Detach` |

**Commits:** `11e9dcd` (Sites 1, 3, 4, 5, 7), `1c21d2d` (Site 6), `6bf1a71` (Sites 2, 8).

**Sites 2 and 8 — Detach pattern:** `ToolboxTalkScheduleAssignment` is a nav-collection child. Calling `schedule.Assignments.Remove(assignment)` (load-bearing for nav-collection consumers) also marks the entity `Deleted` via EF orphan-removal semantics. Without the `Detach` call (`_dbContext.Entry(assignment).State = EntityState.Detached`), the subsequent `SaveChangesAsync` emits a phantom soft-delete UPDATE that hits zero rows. The pattern is: `Remove()` from nav-collection → `Detach` to cancel the phantom → `ExecuteDeleteAsync` to physically delete.

**Remaining structural work (BACKLOG — Path-B):** The index is still unfiltered; the entity still has no `HasQueryFilter`. Filtering the index on `IsDeleted = false` would close §6.2 permanently and eliminate the need for the `ExecuteDeleteAsync` workaround at Sites 3, 4, 5. Requires a migration.

**Why it can still bite:** The `ExecuteDeleteAsync` calls run only in their specific code paths. If a translation row is somehow inserted between that call and the job's own insert (e.g., `DailyTranslationScanJob` fires a `MissingTranslationsJob` for the same talk concurrently), the job's insert can still collide.

### 6.3 Cascade Reset — What It Clears vs. What It Leaves

**Trigger:** `UpdateSectionsAsync` when `status ∈ {QuizGenerated, Validated}`.

**What is cleared (session fields only):**
- `QuestionsJson = null`
- `ValidationRunIds = null` (Validated only)
- `TranslationJobIds = null` (Validated only)
- `Status = Parsed`

**What is NOT cleared:**
- `TargetLanguageCodes` — still holds the previous language selection.
- `SettingsJson` — title, category, behaviour settings retained.
- `SubtitleJobId` — subtitle job reference retained even though subtitle job is now stale. In-flight subtitle jobs are not cancelled (see §7 Q3 — resolved).
- Draft `ToolboxTalk` in DB — still exists with its sections, questions, and translations.
- Old `TranslationValidationRun` rows — still exist, still linked to the draft talk.

**Consequence:** After cascade reset, the draft talk has a "time capsule" of old sections and old translations until `StartTranslateValidateAsync` runs again and re-syncs them. Stale sections are soft-deleted at that point; stale translations are hard-deleted. But until then, the draft talk's content differs from `ParsedSectionsJson`.

### 6.4 Orphaned Hangfire Jobs from Prior `StartTranslateValidateAsync` Calls — **[RESOLVED]**

**Scenario:** User goes through SettingsStep, starts translation. Session is `TranslatingValidating`. User abandons the browser tab, re-opens the wizard, navigates back to ParseStep, edits sections (cascade reset to `Parsed`). Then hits Continue again, triggering another `StartTranslateValidateAsync`.

**Previous behaviour (before fix):** Old `TranslationValidationJob` instances continued running. They would attempt to INSERT `ToolboxTalkTranslation` rows for the same `(TalkId, LanguageCode)`, racing against each other and the new jobs. **No cancellation existed.**

**Fix (commit `dbef638`):** Two-layer defence:

- **Layer 1 — Delete() at re-trigger:** `StartTranslateValidateAsync` reads `session.TranslationJobIds` (~lines 409–441) and calls `IBackgroundJobClient.Delete()` for each stored job ID before creating new runs. `Delete()` reliably cancels `Enqueued`, `Scheduled`, and `AwaitingRetry` jobs. Job IDs are tracked in the new `TranslationJobIds` column added by migration `20260604111150_AddTranslationJobIdsToContentCreationSession`.

- **Layer 2 — In-job relevance guard:** `GenerateTranslationForSectionsAsync` (~lines 661–704 of `TranslationValidationJob.cs`) re-loads the session at the top of the method and aborts before inserting the `ToolboxTalkTranslation` row if the run's ID is no longer in `session.ValidationRunIds`. This covers the case where `Delete()` cannot stop an already-running job.

**Remaining limitation:** `IBackgroundJobClient.Delete()` cannot stop a job that has already entered `Processing` state. Layer 2 covers this case. There is a narrow window between when a job enters Processing and when it reaches the relevance guard — in this window it could still insert a translation row. The `ExecuteDeleteAsync` at Site 4 (§6.2) runs at the start of `StartTranslateValidateAsync` and clears the translation rows, so the window is only between the new-run `ExecuteDeleteAsync` completing and the orphaned job reaching its own INSERT.

### 6.5 Session Stuck in `TranslatingValidating` — **[RESOLVED]**

**Scenario:** A `TranslationValidationJob` throws an exception from inside `GenerateTranslationForSectionsAsync`. Previously, the catch block in that method returned `null` silently, leaving the `TranslationValidationRun` in its last state (e.g., `Running`) without setting it to `Failed`. `TryUpdateSessionStatusAsync` loaded run statuses, found at least one non-terminal status, and left the session in `TranslatingValidating` indefinitely.

**Fix (commit `cd9f1dc`):** The `GenerateTranslationForSectionsAsync` catch block now explicitly marks the run as `Failed` (~lines 922–932 of `TranslationValidationJob.cs`) before returning `null`. `TryUpdateSessionStatusAsync` then finds all runs in terminal states and advances the session to `Validated`.

**Remaining limitation:** `TranslationValidationRun` has no `ErrorMessage` column. The `Status = Failed` state is now correctly surfaced to the state machine, but the user sees only "Failed" with no cause. **BACKLOG:** add `ErrorMessage` column + surface in TranslateStep/ValidateStep UI.

**Recovery path (before fix):** None until session expiry. After fix: session transitions to `Validated` automatically when the run is marked `Failed`; `TryUpdateSessionStatusAsync` sees the terminal state on the next job's completion call.

### 6.6 `Failed` Is Not Truly Terminal

`Failed` is not in the `terminalStatuses` list in `ExpiredSessionCleanupJob` ([ExpiredSessionCleanupJob.cs:24–28](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/ExpiredSessionCleanupJob.cs#L24)). A failed session will be cleaned up by the expiry job.

`Failed` sessions have no "retry" path in the service — `ParseContentAsync` only allows `Draft` status (line 188). There is no "retry from Failed" endpoint. The frontend must create a new session.

**Exception:** `GenerateQuizAsync` accepts `GeneratingQuiz` as an input status (~line 993), which is also an in-flight state. If quiz generation is interrupted mid-status-write, a subsequent call finding the session in `GeneratingQuiz` will re-run quiz generation. This is the only auto-recovery path in the service.

### 6.7 `TryUpdateSessionStatusAsync` Relies on String-Matching of JSON

The check at [TranslationValidationJob.cs:1024](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/TranslationValidationJob.cs#L1024):
```csharp
if (session.ValidationRunIds == null || !session.ValidationRunIds.Contains(runIdString))
    continue;
```

This is a substring check on the raw JSON string before deserializing. It works as long as `Guid.ToString()` produces a format that matches the JSON serialization format. If there is any formatting difference (e.g., uppercase vs lowercase Guid strings in JSON), the substring check passes but `runIds.Contains(validationRunId)` at line 1038 fails, and the session never transitions.

### 6.8 `PublishAsync` Concurrent Double-Submit Race

If two HTTP requests reach `PublishAsync` for the same session concurrently, both may read `Status = Validated` before either writes `Status = Publishing`. Both proceed past the status check and attempt to publish. The second publish call would create a second ToolboxTalk row (or, if the first has already set the draft talk to Published by the time the second runs, the second call to `PublishAsLessonAsync` would load the talk and proceed to publish it again with a title-uniqueness check).

**Likelihood:** Low in normal use (frontend disables button after click). Not mitigated with a database-level lock.

### 6.9 `IsDeleted` Not Set by `ExpiredSessionCleanupJob`

`AbandonSessionAsync` sets both `Status = Abandoned` and `IsDeleted = true`. `ExpiredSessionCleanupJob` sets only `Status = Abandoned` ([ExpiredSessionCleanupJob.cs:51](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/ExpiredSessionCleanupJob.cs#L51)), not `IsDeleted`. A session abandoned by the cleanup job may still appear in future queries that filter only on `Status` and not `IsDeleted`.

### 6.10 `UpdateQuestionsAsync` Silently Demotes `Validated` to `QuizGenerated` — **[RESOLVED]**

**Where:** `UpdateQuestionsAsync` ([service:1117](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentCreationSessionService.cs#L1117)).

**Previous behaviour:** The status-promotion guard fired for ALL non-`QuizGenerated` statuses, including `Validated`, silently demoting to `QuizGenerated` without clearing `ValidationRunIds` or stale translations. The wizard's step indicator showed steps 5 and 6 as still reachable (stale `ValidationRunIds` still non-null), but translations did not reflect the new questions. If the user skipped re-running translation and went directly to Publish, updated questions were synced at publish time but no re-translation occurred — employees saw questions translated in the old configuration.

**Fix (commit `05a9808`):** Mirrors the `UpdateSectionsAsync` cascade-reset pattern. Editing questions from `Validated` now:
- Sets `Status = Parsed` (full cascade, not partial QuizGenerated)
- Clears `ValidationRunIds = null`
- Clears `TranslationJobIds = null`
- Hard-deletes stale `ToolboxTalkTranslations` via `ExecuteDeleteAsync` (~lines 1161–1164)
- Frontend QuizStep shows a confirmation dialog requiring explicit consent before demoting from Validated
- In-flight statuses now disable quiz editing (matched `UpdateSectionsAsync` guard pattern)

See §2.2 and §8.6 for the current transition behaviour.

### 6.11 `ConfirmUploadAsync` Resets to `Draft` From Any Status — **[RESOLVED]**

**Where:** `ConfirmUploadAsync` ([service:1344](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentCreationSessionService.cs#L1344)).

**Previous behaviour:** The presigned-upload confirmation path had no status guard. Line 1274 (previous) always set `Status = Draft`, regardless of current status. A re-upload from `Validated` produced a session in `Draft` with a new source file but stale translation runs and stale questions — a state that no normal UI flow creates.

**Fix (commit `6a16cd1`):** Draft-status guard added at line 1352: throws `InvalidOperationException` if `Status != Draft`, matching the `UploadFileAsync` pattern. The guard message is: "Upload can only be confirmed for a session in Draft status (current: {status})". Re-upload from non-Draft status is now explicitly rejected.

### ⚠ 6.12 SetAuditFields Interceptor Silently Converts All `Remove()` Calls to Soft-Deletes (SYSTEM-WIDE)

**Where:** [ApplicationDbContext.SetAuditFields](src/Core/QuantumBuild.Core.Infrastructure/Data/ApplicationDbContext.cs#L360), lines 396–403.

**What:** Every `EntityState.Deleted` entry for a `BaseEntity` descendant is unconditionally flipped to `EntityState.Modified` and `IsDeleted = true` is set. This happens inside `SaveChangesAsync` before EF generates SQL. The interceptor has no opt-out, no exception list, no attribute-based bypass.

```csharp
case EntityState.Deleted:
    entry.State = EntityState.Modified;
    entry.Entity.IsDeleted = true;
    entry.Entity.UpdatedAt = now;
    entry.Entity.UpdatedBy = CurrentUserId;
    entry.Entity.DeletedBy = CurrentUserId;
    break;
```

**Implication:** Every `_dbContext.X.Remove(...)` call on a `BaseEntity` descendant produces a **soft-delete** at runtime, regardless of how the calling code is written. Code that appears to hard-delete (e.g., `Remove()` after `IgnoreQueryFilters()`) is in fact soft-deleting.

**Scope of impact:** Any entity inheriting `BaseEntity` (directly or via `TenantEntity`). This includes every entity in the toolbox-talks module, AI usage logs, schedules, assignments — and any future entity added to any module that inherits `BaseEntity`.

**Active bug caused by this interceptor (now fixed):** Prior to this batch, the eight `.Remove()` sites listed in §6.2 all silently soft-deleted rows they intended to hard-delete. For two of those sites (`ToolboxTalkTranslation` cleanup at Sites 4 and 5), the unfiltered unique index `ix_toolbox_talk_translations_talk_language` then blocked re-insertion, causing the cascade-retry unique-constraint violation that took multiple sessions to diagnose. See §6.2 for the per-site fix.

**Other potentially-affected sites discovered during this investigation but NOT fixed (deliberate or load-bearing):**

| Site | File:Line (approx) | Notes |
|------|--------------------|-------|
| `DeleteDraftTalkAsync` | [ContentCreationSessionService.cs:~2292](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentCreationSessionService.cs#L2292) | Intentional soft-delete by design. Non-Video course publish path soft-deletes the draft talk so it can be audited. |
| `AggregateAiUsageJob` AiUsageLog cleanup | [AggregateAiUsageJob.cs:~93](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/AggregateAiUsageJob.cs#L93) | Interceptor accidentally soft-deletes rows the job intends to physically purge. The current soft-delete behaviour is now load-bearing for cost reporting that queries soft-deleted logs. **BACKLOG:** needs an explicit retention policy decision rather than implicit-via-bug behaviour. |

**Current bypass mechanism:** `ExecuteDeleteAsync` issues raw SQL `DELETE` that does not go through the change tracker or the interceptor. This is the pattern used at all eight fixed sites.

**Risk for future code changes:** Any new `.Remove()` call on a `BaseEntity` descendant where the developer intends a hard-delete will silently soft-delete. There is no compile-time signal and no runtime exception. The pattern only surfaces when (a) the soft-deleted row collides with a unique index on re-insert, (b) the row's presence is visible to a query that doesn't filter on `IsDeleted`, or (c) someone reads the DB directly.

**Mitigation discipline:** See §9 rule 8.

---

## 7. Open Questions

Items marked **[RESOLVED]** were answered by the verification pass.

**[RESOLVED] Open Question 2 — Backward navigation from Validated/PublishStep while jobs are in-flight.**

The frontend `goToStep` function ([CreateWizard.tsx:190–200](web/src/features/toolbox-talks/components/create-wizard/CreateWizard.tsx#L190)) allows backward navigation to any step ≤ `highestStep` that passes `isStepReachable`. The `isStepReachable` check ([CreateWizard.tsx:146–154](web/src/features/toolbox-talks/components/create-wizard/CreateWizard.tsx#L146)) returns false **only** for steps 5 and 6 when `session.validationRunIds` is null or empty. Steps 1–4 and 7 always return true.

**Result:** A user **can** click step 2 (Parse) in the step indicator while the session is `TranslatingValidating`. The ParseStep renders normally. However, if the user tries to **save section edits**, the backend's in-flight guard in `UpdateSectionsAsync` (lines 330–347) throws HTTP 400: "Section editing is not available while TranslatingValidating is in progress." The user can view sections freely; any save attempt is rejected by the backend until the session reaches `Validated`.

After cascade reset clears `ValidationRunIds`, steps 5 and 6 render in an amber "blocked / re-run required" state ([CreateWizard.tsx:213](web/src/features/toolbox-talks/components/create-wizard/CreateWizard.tsx#L213)) and their buttons are disabled.

---

## 8. Migrations & Testing

## 8.1 — Migration name must match migration content

### Finding

The migration file `20260424221512_AddQrCodeCourseId.cs`, named as if it
added a single `CourseId` column to `QrCodes`, actually contained the
full schema for QR Codes and QR Locations — duplicating two earlier
migrations (`AddQrLocationAndQrCode`, `AddQrSession`). The duplicate
`CreateTable` calls would fail with PostgreSQL error 42P07 ("relation
already exists") on any fresh database that ran the migrations in order.

The bug sat in `main` for six weeks without detonating because no
environment ran the migration sequence end-to-end against a fresh
database in that window. Production had applied this migration on
April 24 from a baseline that didn't yet have the QR tables;
Development was on a March 29 baseline and never advanced through it.
It was caught when Phase 1c added Testcontainers-based integration
tests that spin up a fresh Postgres per test run, applying every
migration from scratch every time.

### Rule

A migration's filename describes the *full schema effect* of the
migration, not the smallest user-visible change. If a developer sets out
to add a column and finds the generated migration contains `CreateTable`
or other unexpected operations, that's a sign the EF model snapshot is
out of sync with the database. Resolve the snapshot mismatch before
committing the migration — do not let the unintended operations ship
under a misleading name.

A migration must also assume its predecessors have run. Never
re-`CreateTable` an object an earlier migration created.

### Detection

Testcontainers-based integration tests are the canary for this class of
bug. Any non-trivial database work should be exercised by at least one
integration test that runs against a fresh container, not against a
persistent dev database that masks idempotency problems with prior
state.

### Fixed in

Commit `330f8ae` — `fix(migrations): rewrite AddQrCodeCourseId to add
only CourseId column`. The rewrite is safe for already-migrated
environments because EF tracks applied migrations by ID, not content;
Production (which ran the original) will see `AddQrCodeCourseId` as
already-applied and the rewrite will not re-execute there.


## 8.2 — Out-of-scope changes need their own commits

### Finding

During Phase 1c (chunk 3) of the translation workflow refactor, the
Claude Code agent was prompted to add integration tests and update the
design doc. It completed that work, but along the way also:

  - Rewrote a six-week-old production migration (file content did not
    match filename; ran fine in prod but failed against fresh
    Testcontainers DBs)
  - Added 5 missing stubs to FakeR2StorageService (the integration
    test project had been compile-broken on transval for weeks)
  - Extracted DPA seeding into a new SeedDpaAsync method, reordered
    after user seeding to satisfy an FK that had been broken since the
    seeding inline lived in SeedTenantAsync

All three were real fixes. None were in the prompt's scope. All three
landed inside a single commit labelled
"test(workflows): integration tests for TranslationWorkflowService".

The result was a commit whose name advertised tests-and-docs and whose
content included a migration rewrite. When the user reviewed the
report, the surprises surfaced item-by-item over multiple round-trips,
and the commit ultimately had to be reset, reshaped into four separate
commits, and re-verified — losing roughly an hour of session time.

### Rule

When a Claude Code prompt defines a scope, the agent must stop and
report any work outside that scope before doing it. This is now
codified in CLAUDE.md under "Claude Code prompt conventions" — every
prompt carries a Scope Discipline preamble that requires:

  - Stop on discovery of out-of-scope work
  - Report what, why, smallest unblock, pre-existing-or-fresh
  - Do not fix without explicit approval
  - Structure the final report so out-of-scope changes have their own
    section, separate from in-scope changes

A commit whose name promises X must not contain Y.

### Detection

The pattern to watch for in agent reports: "Infrastructure fixes
required to get tests running" or any framing that bundles unrequested
work under a generic plumbing label. That phrasing is the tell.
Out-of-scope work, however legitimate, gets its own named commit with
its own message.

### Fixed in

Commits e6d3919, 3852da7, 5eacbe1 (the four-commit reshape that
unbundled the original Phase 1c surprise). The CLAUDE.md preamble was
added in 5937845.

---

**[RESOLVED] Open Question 3 — Subtitle jobs on cascade-reset.**

`UpdateSectionsAsync` does **not** modify `SubtitleJobId` — it is retained through cascade reset. In-flight `SubtitleProcessingJob` instances are **not cancelled**.

If the user re-runs `StartTranslateValidateAsync` after cascade reset, lines ~713+ unconditionally call `_subtitleOrchestrator.StartProcessingAsync`, creating a **new** `SubtitleProcessingJob` row and a new Hangfire job. The new subtitle job ID overwrites `session.SubtitleJobId`. The old subtitle job continues running concurrently, competing to write subtitle files for the same talk with the same language codes. Last writer wins on the R2 object key. No deduplication or cancellation exists.

---

**[RESOLVED] Open Question 5 — `ContentCreationParseJob` retry behaviour.**

`[AutomaticRetry(Attempts = 3)]` is declared at [ContentCreationParseJob.cs:15](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/ContentCreationParseJob.cs#L15).

**Practical retry sequence:**
1. Hangfire calls `ExecuteAsync`. All exceptions are caught internally (lines 62–108).
2. AI failure → `Status = Failed`, method **returns normally** → Hangfire marks job **succeeded** → **no retry**.
3. Unhandled exception → catch at line 91 → sets `Status = Failed`, saves → method **returns normally** → **no retry**.
4. Only if the `Status = Failed` save at line 100 **itself throws** does an exception escape to Hangfire, triggering retries. On any retry, the silent-exit guard at lines 54–60 exits immediately if `Status != Parsing`.

**Conclusion:** `[AutomaticRetry(Attempts = 3)]` provides meaningful coverage only against transient DB write failures during cleanup — not against AI call failures.

---

1. **What is the actual `SessionExpiryHours` value?** Set from `_validationSettings.SessionExpiryHours` ([service:84](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentCreationSessionService.cs#L84)) but the default was not confirmed. A short expiry (e.g., 24 hours) combined with long validation runs could cause sessions to expire while in `TranslatingValidating`.

4. **Are orphaned `TranslationValidationRun` rows (from cascade-reset sessions) visible in the talk's validation history after publish?** Based on the code, orphaned runs remain and are reachable via `GET /api/toolbox-talks/{talkId}/validation/runs`. Whether the frontend validation history tab renders them correctly is not confirmed.

6. **`PublishAsCourseAsync` behaviour when a prior publish-as-course failed partway through.** `PublishAsync` rejects `Completed` sessions, so this only applies if the prior attempt failed (session → `Failed`). In that case the user cannot retry via the wizard. The draft talk may be in a partially-mutated state (sections replaced, course partially created). Not confirmed whether the cleanup catch path at lines 895–929 adequately rolls this back.

7. **`ContentCreationParseJob` retry-path enqueue (line 245) stores no job ID.** If this job is orphaned, there is no stored reference to cancel or inspect it.

8. **`UpdateSectionsAsync` does not guard against a partially-completed `StartTranslateValidateAsync`.** If `StartTranslateValidateAsync` failed after creating `TranslationValidationRun` rows but before persisting `session.ValidationRunIds`, the session would be in `{Parsed/QuizGenerated/Validated}` with orphaned runs and no `ValidationRunIds`. A subsequent `UpdateSectionsAsync` call would succeed, leaving those runs permanently linked to the draft talk.

9. **`TargetLanguageCodes` overwrite at `StartTranslateValidateAsync` (~line 659).** If SettingsStep sends a different language list than InputConfigStep originally set, the session's `TargetLanguageCodes` silently changes. Whether the frontend always re-sends the current selection vs. reading the stored value is not confirmed.

10. **`ExpiredSessionCleanupJob` does not check whether a session's draft talk has been referenced by assignments.** Draft talks have `IsActive = false` and `Status = Draft`, so this is unlikely to have downstream impact, but not confirmed.

---

## 8. Edit-and-Retrace Scenarios

Before adding any "edit while in state X" feature, check here. If the scenario isn't covered, investigate and add it first.

### 8.1 Edit Sections from `Parsed` (no cascade)

- **Endpoint:** `PUT /api/toolbox-talks/create/session/{id}/sections`
- **Data cleared:** Nothing — `ParsedSectionsJson` and `OutputType` overwritten; status unchanged.
- **Data left:** `QuestionsJson` (none yet), `SettingsJson`, `TargetLanguageCodes`.
- **DB artefacts:** No draft talk exists at `Parsed` unless the user previously reached `Validated` and was cascade-reset. If `OutputTalkId` is set (re-entry after prior cascade), the draft talk retains stale content until `StartTranslateValidateAsync` re-syncs.
- **Consequence:** Clean edit; user continues forward normally.

### 8.2 Edit Sections from `QuizGenerated` (quiz cascade)

- **Data cleared:** `QuestionsJson = null`, `Status = Parsed`.
- **Data left:** `ValidationRunIds` (null at this stage anyway), `SettingsJson`, `TargetLanguageCodes`, `SubtitleJobId`.
- **DB artefacts:** Draft talk (if `OutputTalkId` set) retains stale sections/questions/translations until `StartTranslateValidateAsync` re-syncs.
- **Consequence:** User must regenerate quiz before continuing to SettingsStep.

### 8.3 Edit Sections from `Validated` (full cascade)

- **Precondition:** Backend blocks while `TranslatingValidating` — user must wait for `Validated`.
- **Data cleared:** `QuestionsJson = null`, `ValidationRunIds = null`, `TranslationJobIds = null`, `Status = Parsed`.
- **Data left:** `SettingsJson`, `TargetLanguageCodes`, `SubtitleJobId` (stale; old subtitle job not cancelled), draft talk in DB, old `TranslationValidationRun` rows (orphaned from session).
- **DB artefacts:** Steps 5 and 6 show as "blocked" in the step indicator (amber) until translation is re-run.
- **Consequence:** User must regenerate quiz, optionally adjust settings, and re-run translation. `StartTranslateValidateAsync` then hard-deletes stale translations, re-syncs sections/questions, creates new runs.

### 8.4 Edit Quiz Questions from `Parsed` (forward promotion)

- **Data written:** `QuestionsJson` overwritten; `Status` → `QuizGenerated` (since `Parsed != QuizGenerated`).
- **Data cleared:** Nothing else.
- **Consequence:** Normal forward flow.

### 8.5 Edit Quiz Questions from `QuizGenerated`

- **Data written:** `QuestionsJson` overwritten; `Status` unchanged (already `QuizGenerated`).
- **Consequence:** User continues forward; updated questions picked up by `StartTranslateValidateAsync`.

### 8.6 Edit Quiz Questions from `Validated` (explicit cascade-reset — §6.10 fixed)

> **[§6.10 RESOLVED]** This scenario previously caused a silent status demotion. The current behaviour (post-fix) is described below.

- **Frontend:** QuizStep shows a confirmation dialog before allowing the edit. In-flight statuses disable the edit entirely.
- **Data written:** `QuestionsJson` overwritten; `Status` → `Parsed` (full cascade, same as section edit); `ValidationRunIds = null`; `TranslationJobIds = null`.
- **DB artefacts immediately:** `ToolboxTalkTranslations` for `session.OutputTalkId` are physically deleted via `ExecuteDeleteAsync` before the `SaveChangesAsync` write. Old `TranslationValidationRun` rows remain as orphans (not deleted).
- **Steps 5 and 6:** Now correctly show as blocked/unreachable in the step indicator (ValidationRunIds is null).
- **Consequence:** User must regenerate quiz, adjust settings if needed, and re-run translation. If user skips to Publish without re-running translation → updated questions synced at publish, **no re-translation occurs**. Translated question text reflects the old configuration.

### 8.7 Edit Quiz Settings (no cascade)

- **Endpoint:** `PUT /api/toolbox-talks/create/session/{id}/quiz-settings`
- **Data written:** `QuizSettingsJson` only. No status change, no cascade.
- **Consequence:** Safe to call at any time. Applied at `StartTranslateValidateAsync` and `PublishAsync`.

### 8.8 Edit Settings (title, category, behaviour) from Any Status

- **Endpoint:** `PUT /api/toolbox-talks/create/session/{id}/settings`
- **Data written:** `SettingsJson` only. No status change, no cascade.
- **Consequence:** Safe to call at any time. A title change detected at publish time triggers targeted re-translation of the title field only (no full re-run required).

### 8.9 Change Input Source via `UpdateSourceAsync` from Any Status

- **Endpoint:** `PUT /api/toolbox-talks/create/session/{id}/source`
- **Data written:** `Status = Draft`, `ParsedSectionsJson = null`, `OutputType = null`, `SourceText` (Text mode).
- **Data NOT cleared:** `QuestionsJson`, `ValidationRunIds`, `SettingsJson`, `SubtitleJobId`, `TargetLanguageCodes`, `TranscriptText`, `TranslationJobIds`, draft talk.
- **Consequence:** Session resets to `Draft` with stale downstream data. Steps 5/6 may still show as reachable (stale `ValidationRunIds`). All stale data reconciled on next `StartTranslateValidateAsync`.

### 8.10 Change Input Source via `ConfirmUploadAsync` — Now Draft-Only (§6.11 fixed)

- **Endpoint:** `POST /api/toolbox-talks/create/session/{id}/confirm-upload`
- **Guard:** `Status == Draft` required (line 1352). Throws `InvalidOperationException` if called from any other status.
- **Data written (when guard passes):** New `SourceFileUrl`, `SourceFileName`, `SourceFileType`. `Status = Draft` (idempotent — already Draft to pass the guard).
- **Consequence:** Safe reset from Draft only. Attempting from Parsed/Validated/etc. now returns HTTP 500 (wrapped `InvalidOperationException`). The pre-fix scenario described in the old §6.11 is no longer possible.

### 8.11 Re-run from `Failed`

- **Defined path:** None. `ParseContentAsync` requires `Status == Draft`. No "retry from Failed" endpoint exists.
- **Frontend path:** Must create a new session.
- **Exception:** `GenerateQuizAsync` accepts `GeneratingQuiz` as input (~line 993). A session stuck in `GeneratingQuiz` after an interrupted write can be recovered by re-calling the generate-quiz endpoint. This is the only self-healing re-entry path in the service.

---

## 9. Maintenance Discipline

1. **This document is the source of truth for the content-creation wizard flow.** When code and document disagree, the code wins — but update the document immediately, even mid-PR.

2. **Any code change touching any of the following MUST include a corresponding update to this document in the same PR:**
   - `ContentCreationSession` entity or its entity configuration
   - `ContentCreationSessionService` (any method)
   - `ContentCreationSessionStatus` enum (any value)
   - `VideoTranscriptionJob`, `ContentCreationParseJob`, `TranslationValidationJob` (session-status-touching paths), `ExpiredSessionCleanupJob`
   - `CreateWizard.tsx`, `goToStep`, `isStepReachable`, or step-skip logic
   - Any controller endpoint that calls a `ContentCreationSessionService` method

3. **Update the footer commit ref on every revision.**

4. **If you find a discrepancy between this document and the code while reading it — correct the document immediately**, even if you weren't planning to touch that area. Add the correction to the "Corrections from previous revision" section so the diff is auditable.

5. **File:line refs decay.** Line numbers drift with every commit. If you reference a line number and notice it has moved, update it. A claim without a ref is unverifiable.

6. **New sharp edges belong in §6.** Format: bold title; **Where** (file:line); **What** (the problem); **Current fix** or **Risk**. Do not propose fixes in this document — document the edge and let the PR process decide.

7. **New edit paths belong in §8.** Before shipping any "edit while in state X" feature, check §8. If the scenario isn't there, investigate and add it first.

8. **Before assuming any `Remove()` call hard-deletes, verify the entity does NOT inherit `BaseEntity`.** If it does, `Remove()` will silently soft-delete (see §6.12). For hard-delete, use `ExecuteDeleteAsync` with an appropriate `Where` filter and `IgnoreQueryFilters()` if soft-deleted ghost rows need clearing. There is no compile-time warning; the only signals are a unique-constraint violation on re-insert or unexpected rows visible in the DB.

---

## Corrections from Previous Revision

_Changes made during the 2026-06-04 verification pass against HEAD `21ef65d` (transval branch)._

| # | Section | Prior Claim | Correction |
|---|---------|------------|------------|
| 1 | §2.1 Transcribing→Parsing | "Data written: `TranscriptText`, `TranscriptWordsJson`, `Status = Parsing` — all in one `SaveChangesAsync` (line 94)" | `VideoTranscriptionJob` makes **two** saves: line 61 re-marks `Transcribing` (redundant but harmless); line 94 writes the key data. |
| 2 | §2.1 Draft→Parsing (Text/PDF) | "Text/PDF path: Status = Parsing saved (line 269), AI parse called synchronously." PDF path not separately described. | PDF mode has an **additional** `SaveChangesAsync` at line 208 (after PDF text extraction) before the `Parsing` status save — three saves total for PDF vs two for Text. |
| 3 | §5.2 `ContentCreationParseJob` | Implied `[AutomaticRetry(Attempts = 3)]` retries fire on AI failure. | Retries are inactive for AI failures — all exceptions are caught internally and the job returns cleanly. Retries fire only on DB failure during the `Status = Failed` cleanup save. See §5.2 note. |
| 4 | §2.2 `UpdateSectionsAsync` in-flight guard | Document did not state which statuses are blocked. | Guard covers all five in-flight statuses: `{Transcribing, Parsing, GeneratingQuiz, TranslatingValidating, Publishing}` (lines 330–337). |
| 5 | §5.1 Jobs table | Retry-path `ContentCreationParseJob` enqueue listed without note. | Added note: "job ID discarded, not saved to session." |
| 6 | §7 Open Questions | Questions 2, 3, 5 listed as open. | Resolved and answered inline with [RESOLVED] tag. |
| 7 | §4 (absent) | Publish path not traced end-to-end. | §4.9 added: full trace of `PublishAsLessonAsync` (both paths) and `PublishAsCourseAsync`. |
| 8 | New | No edit-and-retrace section. | §8 added covering all 11 edit paths. |
| 9 | New | No maintenance rules. | §9 added. |
| 10 | New | `UpdateQuestionsAsync`, `UpdateQuizSettingsAsync`, `UpdateSettingsAsync`, `UpdateSourceAsync` not traced as transitions. | Added to §2.2 with full detail. |
| 11 | New sharp edges | §6.10 and §6.11 not documented. | §6.10: `UpdateQuestionsAsync` silently demotes `Validated` → `QuizGenerated`. §6.11: `ConfirmUploadAsync` resets to `Draft` from any status. |

_Changes made during the 2026-06-04 revision against HEAD `6bf1a71` (transval branch)._

| # | Section | Prior Claim | Correction |
|---|---------|------------|------------|
| 12 | §6.4 | Documented as open sharp edge with "No active cancellation exists." | **RESOLVED** (commit `dbef638`). Two-layer defence: `IBackgroundJobClient.Delete()` at re-trigger (Layer 1, via new `TranslationJobIds` column) + in-job relevance guard in `GenerateTranslationForSectionsAsync` (Layer 2). See §6.4 for full detail. |
| 13 | §6.5 | "Recovery path: None until session expiry." | **RESOLVED** (commit `cd9f1dc`). `GenerateTranslationForSectionsAsync` catch block now marks run `Failed` before returning null. `TryUpdateSessionStatusAsync` now correctly advances session to `Validated` when all runs terminal. Remaining gap: no `ErrorMessage` column to surface the cause. |
| 14 | §6.10 | Documented as open sharp edge: silent demotion to `QuizGenerated` without clearing `ValidationRunIds`. | **RESOLVED** (commit `05a9808`). Editing questions from `Validated` now cascades to `Parsed`, clears `ValidationRunIds` and `TranslationJobIds`, hard-deletes translations via `ExecuteDeleteAsync`. Frontend adds confirmation dialog + in-flight guard. §2.2 and §8.6 updated accordingly. |
| 15 | §6.11 | Documented as open sharp edge: no status guard on `ConfirmUploadAsync`. | **RESOLVED** (commit `6a16cd1`). Draft-status guard added at line 1352. §2.1 Draft→Draft and §8.10 updated. |
| 16 | §6.2 | Documented as structural risk with "current mitigation" using `Remove()` for hard-delete. | **Partial closure.** Eight `.Remove()` sites converted to `ExecuteDeleteAsync` (commits `11e9dcd`, `1c21d2d`, `6bf1a71`). Sites listed in §6.2 table. Structural root cause (unfiltered index, no query filter) remains as BACKLOG Path-B. |
| 17 | §4.3 row "Hard-deleted on re-run" | Stated `Remove()` as the delete mechanism. | `ExecuteDeleteAsync` is now the mechanism at all sites. Option B annotation added: historical `Remove()` claim noted as a corrected error referencing §6.12. |
| 18 | §4.4 row "Hard-deleted on re-run" and "Critical invariant" | Stated `Remove()` / `IgnoreQueryFilters() + Remove()`. | `ExecuteDeleteAsync` is now the mechanism. Option B annotation added. "Critical invariant" updated to reference `ExecuteDeleteAsync`. |
| 19 | §4 (absent) | No `ToolboxTalkSlideshowTranslation` entry. | §4.5 added. Same structural risk as §4.4; `ExecuteDeleteAsync` applied at SlideshowGenerationService.cs:80–83. |
| 20 | §4.5 (was TranslationValidationRun) | Entity mentioned but no field-by-field documentation. | Renumbered to §4.6; field table added. Includes `Status = Failed` marking (§6.5 fix) and `ErrorMessage` gap BACKLOG note. |
| 21 | §4.6, §4.7, §4.8 | Numbered as §4.5, §4.6, §4.7. | Renumbered: §4.7 TranslationValidationResult, §4.8 R2 Storage, §4.9 Publish Path. All `§4.8` cross-references updated to `§4.9`. |
| 22 | New | No §6.12 entry. | §6.12 added: SetAuditFields interceptor system-wide soft-delete conversion — the root cause behind all §6.2 sites. |
| 23 | §2.2 `UpdateQuestionsAsync` title | "QuizGenerated/Validated/Parsed → QuizGenerated" | Updated to "Parsed → QuizGenerated / Validated → Parsed" to reflect §6.10 fix. Full transition data rewritten. |
| 24 | §2.2 `UpdateSectionsAsync` "Data NOT cleared" | `TranslationJobIds` not mentioned (field didn't exist at 21ef65d). | Added: `TranslationJobIds = null` is cleared when `oldStatus == Validated` (line 373). |
| 25 | §2.3 Transition Diagram | Arrow showed "update-questions from Validated → demotes to QuizGenerated". | Updated to "update-questions from Validated → cascade-resets to Parsed". |
| 26 | §3 Fields Matrix | `TranslationJobIds` field absent. | Row added. |
| 27 | §5.1 Jobs table | `TranslationValidationJob` enqueue at "service:663"; no mention of job-ID capture. | Updated to ~line 702; added note about job-ID capture and `TranslationJobIds` persistence. |
| 28 | §5.2 `TranslationValidationJob` failure mode | "Run → Failed (controlled failure); session may stay TranslatingValidating." | Updated: run is now marked `Failed` in `GenerateTranslationForSectionsAsync` catch (~line 929), and `TryUpdateSessionStatusAsync` now advances session. |
| 29 | §5.3 `TryUpdateSessionStatusAsync` line refs | Lines 937–994 | Updated to lines 1007–1070. All internal step line numbers updated. |
| 30 | §9 Maintenance Discipline | Seven rules. | Rule 8 added: `Remove()` vs `ExecuteDeleteAsync` discipline for `BaseEntity` descendants. |
| 31 | Various | Line numbers throughout sourced from SHA 21ef65d. | Many line numbers have drifted. Critical references updated to current HEAD (~6bf1a71) where verified. File refs without `#L` annotations are approximate and should be re-verified after significant refactors. |

---

## 10. Edit Surface

The edit surface covers every action an admin can take on a **published** `ToolboxTalk` after it has been promoted by the wizard's `PublishAsync`. There is no `ContentCreationSession` involved; all actions operate directly on the live entity.

### 10.1 States

The edit surface has no state machine. A published talk's `Status = Published` does not change through any edit-page action. All actions are stateless from the talk's perspective.

---

### 10.2 Entry Points

| Surface | Location | Guard |
|---------|----------|-------|
| **Talk list** | `/admin/toolbox-talks/talks` — edit icon/button per row | `Learnings.Manage` permission |
| **Talk detail page** | `/admin/toolbox-talks/talks/{id}` — Edit button ([ToolboxTalkDetail.tsx:53](web/src/features/toolbox-talks/components/ToolboxTalkDetail.tsx#L53)) | `canManage` (`Learnings.Manage`) |
| **Direct URL** | `/admin/toolbox-talks/talks/{id}/edit` | Authentication + `Learnings.Manage` via controller |

All three routes resolve to the same page: [edit/page.tsx](web/src/app/(authenticated)/admin/toolbox-talks/talks/%5Bid%5D/edit/page.tsx), which renders `ToolboxTalkForm` with the existing talk populated (`isEditing = true`).

---

### 10.3 Actions Available on the Edit Page

The `ToolboxTalkForm` component ([ToolboxTalkForm.tsx](web/src/features/toolbox-talks/components/ToolboxTalkForm.tsx)) renders the following in edit mode (`isEditing = true`):

| # | Action | Frontend Trigger | API Endpoint | Backend Handler | Notes |
|---|--------|-----------------|--------------|-----------------|-------|
| 1 | **Save talk** (basic info, sections, questions) | "Update Learning" submit button ([ToolboxTalkForm.tsx:1077](web/src/features/toolbox-talks/components/ToolboxTalkForm.tsx#L1077)) | `PUT /api/toolbox-talks/{id}` (`Learnings.Manage`) | [UpdateToolboxTalkCommandHandler.cs](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/UpdateToolboxTalk/UpdateToolboxTalkCommandHandler.cs) | Covers all scalar fields, section edits, question edits. |
| 2 | **Regenerate slideshow** | "Regenerate Slideshow" button ([ToolboxTalkForm.tsx:980](web/src/features/toolbox-talks/components/ToolboxTalkForm.tsx#L980)), conditional on `talk.hasSlideshow` or `talk.slidesGenerated` | `POST /api/toolbox-talks/{id}/generate-slides` (`Learnings.Manage`) | [ToolboxTalksController:863](src/QuantumBuild.API/Controllers/ToolboxTalksController.cs#L863) → `SlideshowGenerationService.GenerateSlideshowAsync` | Hard-deletes ALL `ToolboxTalkSlideshowTranslations` before write (see §10.9.3). |
| 3 | **Generate translations** | "Generate Translations" button in `ContentTranslationPanel` ([ContentTranslationPanel.tsx:240](web/src/features/toolbox-talks/components/ContentTranslationPanel.tsx#L240)); shown only when `sections.length > 0 OR questions.length > 0` ([ToolboxTalkForm.tsx:1058](web/src/features/toolbox-talks/components/ToolboxTalkForm.tsx#L1058)) | `POST /api/toolbox-talks/{id}/translations/generate` (`Learnings.Admin`) | [ToolboxTalksController:1065](src/QuantumBuild.API/Controllers/ToolboxTalksController.cs#L1065) → [GenerateContentTranslationsCommandHandler.cs](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/GenerateContentTranslations/GenerateContentTranslationsCommandHandler.cs) | Synchronous call, no background job. See §10.5 for full detail. |
| 4 | **Process subtitles** | `SubtitleProcessingPanel` ([ToolboxTalkForm.tsx:1051](web/src/features/toolbox-talks/components/ToolboxTalkForm.tsx#L1051)); shown only when `talk.videoSource !== 'None' AND talk.videoUrl` | `POST /api/toolbox-talks/{talkId}/subtitles/process` (`Learnings.Manage`) | `SubtitleProcessingController` → `ISubtitleProcessingOrchestrator` | Enqueues a background job. Does not touch `ToolboxTalkTranslation`. |

**Actions accessible on the detail page but NOT the edit form:**

| Action | Endpoint | Notes |
|--------|----------|-------|
| AI content generation | `POST /api/toolbox-talks/{id}/generate` | `ContentGenerationJob` (Hangfire + SignalR). Accessible from detail page UI; auto-translates after generation via `GenerateContentTranslationsCommand`. |
| Smart generate | `POST /api/toolbox-talks/{id}/smart-generate` | Dedup + AI generation; accessible from detail page UI. |
| Upload/delete video binary | `POST/DELETE /api/toolbox-talks/{id}/video` | `ToolboxTalkFilesController`. |
| Upload/delete PDF binary | `POST/DELETE /api/toolbox-talks/{id}/pdf` | `ToolboxTalkFilesController`. |
| Start validation run | `POST /api/toolbox-talks/{id}/validation/validate` | Available for published talks; accessible via detail page's Validation history tab. |

---

### 10.4 UpdateToolboxTalkCommandHandler — Fields Written

**File:** [UpdateToolboxTalkCommandHandler.cs](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/UpdateToolboxTalk/UpdateToolboxTalkCommandHandler.cs)

**Tables read:** `ToolboxTalks` (line 61), re-queried for title uniqueness (line 78) and code uniqueness (line 92). `ToolboxTalkSections` (line 169) and `ToolboxTalkQuestions` (line 147) queried directly from DbContext (not via nav-collection to avoid concurrency issues).

**Tables written (single `SaveChangesAsync` at line 138):**

| Table | Operation | Condition |
|-------|-----------|-----------|
| `ToolboxTalks` | UPDATE — all scalar fields: `Title`, `Description`, `Category`, `Frequency`, `VideoUrl`, `VideoSource`, `AttachmentUrl`, `MinimumVideoWatchPercent`, `RequiresQuiz`, `PassingScore`, `IsActive`, `QuizQuestionCount`, `ShuffleQuestions`, `ShuffleOptions`, `UseQuestionPool`, `AutoAssignToNewEmployees`, `AutoAssignDueDays`, `SourceLanguageCode`, `GenerateSlidesFromPdf`, `GenerateCertificate`, `RequiresRefresher`, `RefresherIntervalMonths`, `Code` | Always |
| `ToolboxTalkSections` | **Soft-delete** (`IsDeleted = true`) for rows absent from the request (line 195) | Sections removed by admin |
| `ToolboxTalkSections` | UPDATE in-place for rows present with an ID (lines 212–220) | Existing sections edited |
| `ToolboxTalkSections` | INSERT new rows (lines 226–239, 245–257) | New sections added (no ID) |
| `ToolboxTalkQuestions` | **Soft-delete** (`IsDeleted = true`) for rows absent (line 281) | Questions removed by admin |
| `ToolboxTalkQuestions` | UPDATE in-place (lines 298–310) | Existing questions edited |
| `ToolboxTalkQuestions` | INSERT new rows (lines 316–334, 339–357) | New questions added (no ID) |

**Not touched:** `ToolboxTalkTranslation`, `ToolboxTalkSlideshowTranslation`, `ToolboxTalkSlideTranslation`, `ToolboxTalkVideoTranslation`, `SubtitleTranslation`, `TranslationValidationRun`, `TranslationValidationResult`. **Translations are not updated or invalidated when section content is edited.**

**Atomicity:** Single `SaveChangesAsync` (line 138) — all mutations above committed in one transaction.

**Guard:** No `ToolboxTalk.Status` guard. The handler accepts any status, including `Published`, `Draft`, `Processing`. This differs from the wizard's `PublishAsync` which rejects `Completed` sessions.

---

### 10.5 GenerateContentTranslationsCommandHandler — Translation Tables and Overwrite Behaviour

**File:** [GenerateContentTranslationsCommandHandler.cs](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/GenerateContentTranslations/GenerateContentTranslationsCommandHandler.cs)

**Triggered by:** `POST /api/toolbox-talks/{id}/translations/generate` from `ContentTranslationPanel`'s "Generate Translations" button ([ContentTranslationPanel.tsx:66](web/src/features/toolbox-talks/components/ContentTranslationPanel.tsx#L66)).

**Frontend UX:**
- User selects languages from a checkbox grid; default selection is the tenant's employee languages (pre-selected on load, line 52–57).
- Existing translations shown as a read-only list; languages with existing translations show a `RefreshCw` icon hinting they will be regenerated ([ContentTranslationPanel.tsx:209](web/src/features/toolbox-talks/components/ContentTranslationPanel.tsx#L209)).
- No confirmation dialog before overwriting.
- "Generating Translations..." spinner replaces button text during the call.
- No per-section or per-language progress. Single HTTP request, no SignalR, no background job.

**Translation tables and overwrite behaviour:**

| Table | Behaviour | Lines (approx) |
|-------|-----------|----------------|
| `ToolboxTalkTranslation` | **Unconditionally overwritten** for matching `(TalkId, LanguageCode)`. Existing row found via nav-collection (`.FirstOrDefault(t => t.LanguageCode == languageCode)`) and updated in-place. New entity inserted if no row exists. Fields written: `TranslatedTitle`, `TranslatedDescription`, `TranslatedSections` (JSON), `TranslatedQuestions` (JSON), `EmailSubject`, `EmailBody`, `TranslatedAt`, `TranslationProvider`. | Lines 149–159, 184–343 |
| `ToolboxTalkSlideTranslation` | **Skipped if already exists** for `(SlideId, LanguageCode)`. New row inserted only if no existing translation for that slide and language. | Lines 354–358 (skip guard), 367–374 (insert) |
| `ToolboxTalkSlideshowTranslation` | **Skipped if already exists** for `(TalkId, LanguageCode)`. New row inserted only if no existing row. | Lines 454–464 (skip guard), 570–578 (insert) |
| `ToolboxTalkVideoTranslation` | **Not touched.** | — |
| `SubtitleTranslation` | **Not touched.** | — |
| `ToolboxTalkCourseTranslation` | **Not touched.** | — |

**No "manually edited" flag check.** `ToolboxTalkTranslation` has no field tracking whether its content was manually reviewed or accepted via the TransVal reviewer workflow. The handler performs no such check. Any section translations previously accepted by a reviewer and propagated via `PropagateEditedTranslationAsync` are silently overwritten.

**No delete-before-insert.** Unlike `StartTranslateValidateAsync` (wizard path, which uses `ExecuteDeleteAsync` to clear stale translations before creating runs), this handler performs an in-memory upsert. This has implications for the §6.2 unique index risk — see §10.9.6.

**Atomicity:** Single `SaveChangesAsync` at line 111 commits all language mutations together. Translations for selected languages are all-or-nothing relative to each other. A failure (e.g. timeout) during the HTTP call may leave zero languages updated (if the save never ran) or all languages updated (if the save completed before the client timeout).

**Sector key resolution:** The controller resolves `sectorKey` from `_tenantSectorService.GetDefaultSectorAsync(_currentUserService.TenantId)` ([ToolboxTalksController.cs:1084](src/QuantumBuild.API/Controllers/ToolboxTalksController.cs#L1084)) for tiered translation prompts. This is the tenant's current default sector, which may differ from the sector key used during the original wizard run if the tenant's default has changed.

---

### 10.6 ContentCreationSessionService — Not Involved

The edit surface does not go through `ContentCreationSessionService`. There is no `ContentCreationSession` created, read, or updated by any edit-page action. The six translation tables touched by the wizard (`§4.2–§4.7`) are accessed directly via their own DbSet queries.

---

### 10.7 Validation Infrastructure Reusability

`TranslationValidationController.POST /{talkId}/validation/validate` is available for any published talk with translations — it is **not** wizard-only. The controller requires a `ToolboxTalkTranslation` row to exist for the target language (guard at ~line 70–75). Once translations exist (generated via `ContentTranslationPanel`), a validation run can be started from the detail page's Validation history tab.

`TranslationValidationJob` reads the published talk's sections and the existing `ToolboxTalkTranslation.TranslatedSections` JSON to generate the run. It does not distinguish between wizard-generated and edit-page-generated translations.

**Caveat:** If an admin triggers "Generate Translations" from the edit page after a prior validation run has had sections accepted/edited, the just-overwritten `ToolboxTalkTranslation.TranslatedSections` will differ from the content that was reviewed. The old validation runs survive in the database (they are not deleted), but the translations they reviewed no longer exist. A new validation run must be started to re-validate the freshly generated translations.

---

### 10.8 Hangfire Jobs in the Edit Flow

| Action | Job Enqueued? | Notes |
|--------|--------------|-------|
| Save talk (UpdateToolboxTalk) | No | Fully synchronous. No downstream jobs triggered. |
| Regenerate slideshow (GenerateSlides) | No | `SlideshowGenerationService.GenerateSlideshowAsync` runs synchronously within the HTTP request. |
| Generate translations (GenerateContentTranslations) | No | `GenerateContentTranslationsCommandHandler.Handle` runs synchronously. No SignalR, no progress reporting. |
| Process subtitles | Yes — `SubtitleProcessingJob` | Enqueued by `ISubtitleProcessingOrchestrator`. Does not touch `ToolboxTalkTranslation`. |
| AI content generation (via detail page) | Yes — `ContentGenerationJob` | After completion, auto-translates via `GenerateContentTranslationsCommand` (mediator call from within the job). |

---

### 10.9 Sharp Edges

#### 10.9.1 Section Edits Silently Stale Translations — No Staleness Flag

**Where:** [UpdateToolboxTalkCommandHandler.cs:106–138](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/UpdateToolboxTalk/UpdateToolboxTalkCommandHandler.cs#L106); specifically `UpdateSectionsAsync` lines 210–220 (in-place UPDATE for existing sections, same `SectionId`).

**What:** When an admin edits a section's `Title` or `Content` on the edit form and saves, the `ToolboxTalkSection` row is updated in-place (the `Id` Guid is unchanged). Existing `ToolboxTalkTranslation.TranslatedSections` JSON maps by `SectionId` — after the update, that same `SectionId` now maps to updated source text, but the stored translated text is from the previous version. No staleness flag is set on the translation row. No `MissingTranslationsJob` is enqueued. The `DailyTranslationScanJob` only checks for the presence of `TranslatedSections` (non-null), not content currency. Employees in non-English languages continue to see stale translated content until the admin manually re-runs the "Generate Translations" action.

**Risk:** Undetectable without side-by-side comparison of source and translation dates, or reading the translated JSON.

#### 10.9.2 Removed + Re-added Sections Break Translation ID Mappings Silently

**Where:** [UpdateToolboxTalkCommandHandler.cs:195](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/UpdateToolboxTalk/UpdateToolboxTalkCommandHandler.cs#L195) (soft-delete removed sections), lines 226–239 (INSERT new sections — new `Guid` assigned).

**What:** If an admin removes a section (via `SectionEditor` delete) and adds a new one in its place (or adds a section with no existing `Id`), the new section gets a new `Guid`. The existing `ToolboxTalkTranslation.TranslatedSections` JSON contains an entry for the old `SectionId` (which is now soft-deleted) and no entry for the new `SectionId`. Until translations are re-run:
- Employees in the source language see the new section correctly.
- Employees in translated languages see no translated content for the new section (falls back to English source).
- The old translated entry for the soft-deleted section lingers in the JSON silently.

Re-running "Generate Translations" rebuilds the JSON from the live sections — this is the correct resolution, but nothing prompts the admin to do it.

#### 10.9.3 Regenerate Slideshow Silently Destroys All Slideshow Translations

**Where:** [SlideshowGenerationService.cs:80–83](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Slideshow/SlideshowGenerationService.cs#L80) — `ExecuteDeleteAsync` on `ToolboxTalkSlideshowTranslations`.

**What:** Clicking "Regenerate Slideshow" calls `POST /api/toolbox-talks/{id}/generate-slides`. `SlideshowGenerationService.GenerateSlideshowAsync` hard-deletes **all** `ToolboxTalkSlideshowTranslation` rows for the talk before writing the new slideshow HTML. The edit page shows no warning. After regeneration, employees in non-English languages who had slideshow translations see no slideshow until the admin re-runs "Generate Translations". This is the same `ExecuteDeleteAsync` pattern documented in §4.5 (wizard flow), but it applies equally to the edit surface and is triggered by a button the admin may click without appreciating the downstream translation impact.

#### 10.9.4 "Generate Translations" Asymmetry: Overwrites ToolboxTalkTranslation but Preserves ToolboxTalkSlideTranslation and ToolboxTalkSlideshowTranslation

**Where:** [GenerateContentTranslationsCommandHandler.cs:149–159](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/GenerateContentTranslations/GenerateContentTranslationsCommandHandler.cs#L149) (unconditional overwrite); lines 354–358, 454–464 (skip-if-exists guards).

**What:** Re-running "Generate Translations" unconditionally replaces `TranslatedTitle`, `TranslatedSections`, `TranslatedQuestions` in `ToolboxTalkTranslation`, but skips `ToolboxTalkSlideTranslation` and `ToolboxTalkSlideshowTranslation` that already exist. After a slideshow regeneration (which hard-deletes all slideshow translations per §10.9.3), re-running translations creates new slideshow translations. But if only sections are edited and translations are regenerated (without slideshow regeneration), the slideshow translations are preserved from a prior run — they now describe the old slideshow while sections reflect updated source content. No generation-time correlation is tracked between the two translation types.

#### 10.9.5 Reviewer-Accepted Translations Silently Overwritten by Edit-Page Translate

**Where:** [GenerateContentTranslationsCommandHandler.cs:149–159](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/GenerateContentTranslations/GenerateContentTranslationsCommandHandler.cs#L149) (unconditional in-place overwrite of `ToolboxTalkTranslation`).

**What:** If a reviewer has accepted or edited sections via the TransVal reviewer workflow (`AcceptSection` in `TranslationValidationController` — line ~213–260), those edits are propagated to `ToolboxTalkTranslation.TranslatedSections` via `PropagateEditedTranslationAsync`. The edit page's "Generate Translations" button subsequently overwrites the entire `TranslatedSections` JSON with fresh AI translations, replacing any reviewer-approved content. No audit record of the overwrite is created; the prior `TranslationValidationRun` rows survive in the database but the translations they approved are gone. No warning is shown in the UI before overwriting.

**Status:** Fixed (Phase 0 hotfix, 2026-06-06). UI-only guard. `ContentTranslationPanel` now shows an explicit overwrite confirmation when any selected language has an existing translation. Backend remains unguarded; the workflow state machine (Phase 1+) is the proper long-term fix.

Note: the default selection is employee languages, not already-translated languages; the guard fires on overlap with existing translations regardless of how the selection was reached.

#### 10.9.6 Unique-Constraint Violation Risk: Soft-Deleted ToolboxTalkTranslation Row Can Block Re-insertion

**Where:** [GenerateContentTranslationsCommandHandler.cs:49–59](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/GenerateContentTranslations/GenerateContentTranslationsCommandHandler.cs#L49) (query filter: `Where(tr => !tr.IsDeleted)` excludes soft-deleted rows); lines 153–159 (INSERT if row not found in nav-collection).

**What:** The handler loads `Translations.Where(tr => !tr.IsDeleted)`. A soft-deleted `(TalkId, LanguageCode)` row is invisible to this query. If the handler creates a new `ToolboxTalkTranslation` entity and `SaveChangesAsync` emits an INSERT, the unfiltered unique index `ix_toolbox_talk_translations_talk_language` (documented in §6.2) will cause Postgres error `23505`. Under normal operation, a soft-deleted translation row on a published talk is unlikely — the wizard's cascade-reset paths use `ExecuteDeleteAsync` (hard-delete) rather than soft-delete. However, any code path using `Remove()` on a `BaseEntity` descendant silently soft-deletes (see §6.12), so this risk remains if a new code path introduces such a call. Unlike the wizard path, this handler has no `ExecuteDeleteAsync` pre-pass to clear ghost rows.

#### 10.9.7 Translate Action Is a Synchronous HTTP Call — No Progress, No Timeout Recovery

**Where:** [ContentTranslationPanel.tsx:66–90](web/src/features/toolbox-talks/components/ContentTranslationPanel.tsx#L66) (synchronous `await` on mutation); [GenerateContentTranslationsCommandHandler.cs:37–131](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/GenerateContentTranslations/GenerateContentTranslationsCommandHandler.cs#L37) (full execution within HTTP request).

**What:** Unlike the wizard's `TranslationValidationJob` (Hangfire background job with SignalR progress), the edit-page translate action runs entirely within a single HTTP request. For a talk with many sections and questions across multiple languages, this can take several minutes. The frontend shows a spinner button only — no per-language progress, no estimated time. An ASP.NET Core or load-balancer request timeout would terminate the call mid-execution. Because all language mutations commit in a single `SaveChangesAsync` (line 111), any timeout before the save completes leaves zero translations updated. There is no resume mechanism, no Hangfire job to check, and no error persisted to a database column. The admin's only option is to re-trigger the action.

---

### 10.10 Cross-Reference with Wizard Sharp Edges

| Wizard Sharp Edge | Applies to Edit Surface? |
|-------------------|--------------------------|
| §6.1 Note 23 Trap — ChangeTracker contamination after caught `DbUpdateException` | **No.** `UpdateToolboxTalkCommandHandler` uses a single `SaveChangesAsync`; no complex multi-step transaction. If it throws, the HTTP 500 response is returned without a subsequent save. |
| §6.2 Unfiltered unique index on `ToolboxTalkTranslation` | **Yes — §10.9.6.** A soft-deleted ghost row can block re-insertion from the edit-page translate action, for the same structural reason. The edit handler has no `ExecuteDeleteAsync` pre-pass. |
| §6.3 Cascade reset leaves stale DB artefacts | **Not applicable.** No cascade reset concept on the edit surface. |
| §6.4 Orphaned Hangfire jobs | **Not applicable.** Edit surface enqueues no cancellable Hangfire jobs from the core save/translate/regenerate flows. |
| §6.5 Session stuck in TranslatingValidating | **Not applicable.** No `ContentCreationSession` exists on the edit surface. |
| §6.6 `Failed` is not truly terminal | **Not applicable.** No session entity. |
| §6.7 TryUpdateSessionStatusAsync string-matching | **Not applicable.** No session entity. |
| §6.8 PublishAsync concurrent double-submit race | **Partial equivalent.** Two concurrent `PUT /api/toolbox-talks/{id}` saves would interleave, with last-writer-wins semantics. No permanent corruption since sections are identified by Guid, but concurrent saves could produce an inconsistent section set (e.g. one save's delete not visible to the other's read). Not guarded with a DB-level lock. |
| §6.9 `IsDeleted` not set by `ExpiredSessionCleanupJob` | **Not applicable.** No session cleanup job. |
| §6.10 `UpdateQuestionsAsync` silent demotion | **Not applicable.** No session demotion. |
| §6.11 `ConfirmUploadAsync` status guard | **Not applicable.** No upload-confirm flow on edit surface. |
| §6.12 SetAuditFields interceptor soft-delete conversion | **Partially applies.** `UpdateToolboxTalkCommandHandler.UpdateSectionsAsync` sets `section.IsDeleted = true` directly (line 195, not via `Remove()`), so the interceptor is not involved for the deletion path. New sections added via `_dbContext.ToolboxTalkSections.Add()` will receive audit stamps from the interceptor (as expected). Sections and questions accumulate as soft-deleted rows, but unlike `ToolboxTalkTranslation`, sections and questions have no unfiltered unique index — soft-deleted accumulation does not cause constraint violations. |

---

### 10.11 Open Questions (Edit Surface)

1. **What is the request timeout configured for the Railway deployment?** The synchronous translate action (§10.9.7) is the highest-risk timeout target. If Railway's load balancer or Nginx has a sub-2-minute timeout, multi-language translation jobs will be silently truncated.

2. **Does the detail page surface the "Validate" action prominently enough?** Validation runs can be triggered on published talks (§10.7), but the action lives in the Validation history tab on the detail page — not on the edit page. After an admin edits sections and re-runs translations, there is no nudge to re-validate.

3. **ToolboxTalkSlideTranslation accumulation on re-slideshow.** After a slideshow is regenerated (hard-deleting all `ToolboxTalkSlideshowTranslation` rows via §10.9.3), the `ToolboxTalkSlide` rows themselves are NOT deleted — they are carry-over from the previous PDF parse. If the PDF was changed and the slides re-extracted (new slide rows, new Guids), old `ToolboxTalkSlideTranslation` rows referencing the old slide Guids become orphans. The `GenerateContentTranslationsCommandHandler` only creates new `ToolboxTalkSlideTranslation` rows for slides currently in the nav-collection — it does not delete orphaned translations for old slide Guids. These orphans silently accumulate. Not confirmed whether the slide query in the employee-facing endpoint also queries by talk ID only (which would surface orphaned translations).

---

_Changes made during the 2026-06-05 investigation and document revision._

| # | Section | Prior Claim | Correction |
|---|---------|------------|------------|
| 32 | Document title and purpose | Title: "Content Creation Wizard — Lifecycle Map". Purpose paragraph scoped to wizard only. | Title: "Learning Lifecycle Map". Purpose paragraph extended to cover both create wizard (§1–§9) and edit surface (§10). File renamed from `CONTENT_CREATION_LIFECYCLE.md` to `LEARNING_LIFECYCLE.md` via `git mv` (history preserved). |
| 33 | New | No edit surface section. | §10 added: full investigation of the edit surface — entry points, all actions, `UpdateToolboxTalkCommandHandler` field-by-field trace, `GenerateContentTranslationsCommandHandler` translation-table-by-table trace, `SlideshowGenerationService` hard-delete behaviour, validation reusability, 7 sharp edges (§10.9.1–§10.9.7), wizard sharp-edge cross-reference (§10.10), and 3 open questions (§10.11). |

---

*Document last revised: 2026-06-05. §1–§9 verified against commit `6bf1a71` (HEAD of `transval` branch at time of §6 batch). §10 verified against HEAD `bb2709e`. Re-verify file paths and line numbers before relying on them after any significant refactor.*
