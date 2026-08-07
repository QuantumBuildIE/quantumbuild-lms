# AI / External-Service Error Message Handling — Recon

Read-only fact-finding. No code was changed. Every claim below is anchored to a file:line citation, spot-checked against the actual source for the highest-stakes claims (ElevenLabs leak point, global exception handler, `FailureCode` enum — all confirmed verbatim).

Purpose: map how failures from AI/external services currently reach the user, so a consistent friendly-message + categorisation fix can be scoped. No fix is proposed here.

---

## 1. Executive summary

- **There is no central error-to-message mapping layer**, backend or frontend. Each service/handler builds its own user-facing string inline (§3).
- **The ElevenLabs leak is real and is the worst offender**, but it is not unique: raw `ex.Message` / raw HTTP response bodies reach user-visible fields or UI text in at least five other places across four different feature areas (§2).
- **Failures are not categorised** as transient-vs-persistent anywhere in the AI/external-service call paths. The one enum that exists for structured errors (`FailureCode`) is unrelated — it's used only to pick an HTTP status code for a handful of non-AI workflow errors, not for AI/external-service failures at all (§4).
- **The full technical error is reliably logged server-side** via structured `_logger.LogError` calls, separately from whatever is returned to the caller, in every path examined. A fix that hides detail from the user will not remove it from logs (§5).
- **The global exception handler is already correct** (`Program.cs:350-374`) — it never leaks `ex.Message`, always returns a fixed generic string. The leaks happen *before* that layer, inside service/job code that deliberately builds a message containing raw provider text and returns it as a success-shaped `Result`/DTO field rather than throwing — so the global handler never sees it.
- **The DTO/field contract to preserve** is a plain nullable `string` field per feature (`ErrorMessage`, `errorMessage`, `error`), populated at various layers and rendered directly by the frontend with no transformation in most places (§6).

---

## 2. Every place a raw AI/external error can reach the UI

| # | Service | Leak point (file:line) | What leaks | Reaches UI at |
|---|---------|------------------------|------------|----------------|
| 1 | ElevenLabs transcription | `ElevenLabsTranscriptionService.cs:114` — `TranscriptionResult.FailureResult($"ElevenLabs API error: {response.StatusCode} - {responseBody}")` | Full raw ElevenLabs HTTP response body | `SubtitleProcessingPanel.tsx:363` — `<AlertDescription>{status.errorMessage}</AlertDescription>` (verbatim) |
| 2 | ElevenLabs / R2 upload | `CloudflareR2SrtStorageProvider.cs:117` — `$"Upload failed: {ex.Message}"`, propagated via `SubtitleProcessingOrchestrator.cs:259,377` | Raw AWS/S3 SDK exception text | `SubtitleProcessingPanel.tsx:336` — `title={lang.errorMessage}` tooltip |
| 3 | Claude — section/quiz/parse generation | `ParseToolboxTalkContentCommandHandler` / `GenerateToolboxTalkQuizCommandHandler` return `Result.Fail<T>(parseResult.ErrorMessage / quizResult.ErrorMessage ?? ...)`, sourced from `AiSectionGenerationService.cs:158,167,176`, `AiQuizGenerationService.cs` (same pattern), `ContentParserService.cs:111-120,148-174` — all embed raw `ex.Message` | .NET/HTTP exception text (not full provider JSON body — status-code-only in the non-2xx branch, but full `ex.Message` in catch blocks) | `ToolboxTalksController.cs:447,521` → `BadRequest(new { error = result.Errors.FirstOrDefault() })` → frontend `ParseStep.tsx`/`QuizStep.tsx` — see §6 for why this is currently *masked* rather than shown, and the one place (`create-wizard/ParseStep.tsx:569-573`, `Error detail: {parseErrorDetail}`) where it **is** shown, sourced from `ContentCreationSession.ErrorMessage` set to raw `ex.Message` in `VideoTranscriptionJob.cs:71,114` |
| 4 | TransVal — orchestration-level exceptions (DB, JSON parse, unexpected HTTP failures escaping the whole validation job) | `TranslationValidationJob.cs:545-553` — type-switch interpolates `ex.Message` into `clientMessage`, e.g. `$"External translation service unavailable: {ex.Message}"` | Raw exception message (not full provider body — per-provider back-translation failures are fully contained, see §2a) | `use-validation-hub.ts:169-171` (`setError(payload.message)`) → `TranslateValidateStep.tsx:361` — `<AlertDescription>{hub.error}</AlertDescription>` |
| 5 | Lesson Parser | `LessonGeneratorService.cs:323-327` throws `InvalidOperationException($"Claude API error: {response.StatusCode}")` → `LessonParseJob.cs:180-182` sets `ParseJob.ErrorMessage = ex.Message` | Status-code-only message (not full body) | Live SignalR path only: `use-lesson-parser-hub.ts:50-54` → `page.tsx:429` — `<p>{errorMessage}</p>` (verbatim). The persisted history table fetches `ParseJobDto.ErrorMessage` but never renders it. |
| 6 | Content Translation (`ContentTranslationService`) | `ContentTranslationService.cs:292-298` — `throw new HttpRequestException($"Claude API error: {response.StatusCode} - {responseBody}")` — full raw body | Full raw Claude response body | **Contained** — caught into `ContentTranslationResult.ErrorMessage` but that field is never surfaced to any DTO the frontend reads (confirmed dead-end) |

### 2a. Paths that are already contained (do NOT leak raw text to the user)

- **DeepL / Gemini / Claude Sonnet back-translation (TransVal per-section)** — `DeepLTranslationService.cs:178-179`, `GeminiTranslationService.cs:99-100`, `ClaudeSonnetBackTranslationService.cs:104-105` all return only `$"{Provider} API error: {response.StatusCode}"` (status code, no body) into `BackTranslationResult.FailureResult`. `ConsensusEngine.cs:185-188` only logs these (`LogWarning`), never surfaces them — a failed provider round degrades the section to a low/zero score, not a rejected run. `TranslationValidationResult` has **no error-text field at all** (confirmed via entity definition), so there is structurally no way for this specific path to leak text to the UI.
- **`ClaudeTranslationService`** (subtitle translation, sibling to ElevenLabs transcription in the same feature) — `ClaudeTranslationService.cs:79-83` logs the raw body (`:81`) but explicitly excludes it from the returned `FailureResult`, returning only `$"Claude API error: {response.StatusCode}"` (`:82`). This is the one sibling service that does **not** follow the ElevenLabs pattern, despite living in the same subtitle-processing feature — worth noting as an inconsistency (some engineer already made the "don't leak the body" call for this one call site, but not the ElevenLabs one three files over).
- **Regulatory ingestion** — `RequirementIngestionJob.cs:881-885` throws only `$"Claude API error: {response.StatusCode}"` (full body logged server-side only, `:883`); persisted to `RegulatoryDocument.LastIngestionErrorMessage`/`LastIngestionErrorCode` (truncated to 2000 chars), and the frontend already has a light friendly-mapping layer for it — `describeIngestionError()` in `web/src/app/(authenticated)/admin/regulatory/system/[documentId]/page.tsx:122-137` — the **only** friendly-mapping function found anywhere in the codebase (see §3).
- **Requirement mapping (`RequirementMappingJob`)** — failure is neither persisted nor surfaced anywhere; it is a fire-and-forget Hangfire job whose outer catch (`:111-117`) deliberately swallows the exception so the job "should not fail noisily." No leak, but also no user visibility at all.
- **Regulatory scoring** — `RegulatoryScoreService.cs:503-509` throws `$"Claude API error: {response.StatusCode}"`; `RegulatoryScoreController.cs:77-81` returns it as `BadRequest({ message: ex.Message })`; but the frontend's Axios client never unwraps `response.data.message` (see §6), so even this already-sanitized message never reaches the toast the user sees (`RegulatoryScorePanel.tsx:158-175` shows Axios's own generic `"Request failed with status code 400"` instead).
- **AI Chat Assistant / Help chat** — `HelpChatController.cs:45-83` returns hand-written generic strings (`"AI service unavailable."`, `"AI service returned an error."`, `"AI service not configured."`) — no raw text at any point. Frontend (`HelpAssistant.tsx:220-229`) doesn't even read the backend's message; it shows its own hardcoded fallback on any error. **Note:** this feature is fully implemented in code; `BACKLOG.md` still lists "AI Chat Assistant — UI Help / Data Q&A" as open/deferred — the backlog entry appears stale.
- **General content translation gap-fill (`MissingTranslationsJob`)** — always sends a generic SignalR message ("Some content translations could not be generated. They can be generated manually.") regardless of the underlying provider error; the real error is logged only (`:313`).

---

## 3. Is error-to-message mapping centralised or scattered?

**Scattered.** Confirmed by exhaustive search (backend and frontend) for anything resembling a shared mapper:

- No `ErrorMapper`, `FriendlyErrorService`, `ExceptionTranslator`, `UserFacingException`, or similarly-named class exists anywhere in the repo.
- Each service/handler independently decides what string to build on failure — the same failure category (a Claude API non-2xx response) produces differently-shaped messages depending on which of ~8 AI service classes hit it: `"Claude API error: {StatusCode}"` (status only) in most section/quiz/parse services, vs. `"Claude API error: {StatusCode} - {Response}"` (full body) in `ContentTranslationService.cs:292-298`, vs. no message reaching the user at all in `RequirementMappingJob`.
- One genuine (but narrow) mapping function exists: `ContentGenerationService.GetUserFriendlyError` (`ContentGenerationService.cs:391-415`) — string-`Contains` sniffing on the exception text for `"timeout"`, `"network"`/`"connection"`, `"cancelled"`, `"api"`+(`"key"`|`"unauthorized"`), `"rate limit"`/`"too many requests"`; anything else falls through to the raw text truncated to 200 chars. This only wraps the top-level unhandled-exception path of one job (`ContentGenerationJob`'s outer catch replaces everything with a single fixed string regardless — `ContentGenerationJob.cs:258`), and does not apply to the per-field `sectionResult.ErrorMessage`/`quizResult.ErrorMessage` strings that the actual wizard UI reads.
- The other genuine mapping function is page-local: `describeIngestionError()` (`.../regulatory/system/[documentId]/page.tsx:122-137`), which switches on a regulatory-ingestion-specific string field (`invalid_uri`/`fetch_failed`/`parse_failed`) — not reusable, not connected to any other feature.
- Frontend: `getApiErrorMessage` (`web/src/lib/utils.ts:12-30`) is a shared *extraction* utility (pulls `response.data.errors[]`/`message` → falls back to `Error.message` → falls back to a caller-supplied default) used by bulk-import panels, but it does not do any friendly-language *mapping* — it just extracts whatever string the backend already produced. Several components duplicate their own local `getErrorMessage`/`extractErrorMessage` instead of using it (`SubtitleProcessingPanel.tsx:150-163`, `SendForReviewDialog.tsx:50`, `DeactivateToolboxTalkDialog.tsx:24`, `settings/external-reviewers-section.tsx:59`).
- The `Result<T>`/`FailureCode` pattern (CLAUDE.md Note 25) is the closest thing to a structured-error contract in the codebase, but it is **not used by any AI/external-service code path** — see §4.

**Conclusion:** a fix needs to introduce a mapping layer; none exists to extend. The natural seam is the `Result<T>` factory pattern already used for HTTP-status selection (§4), since it is already the shared vocabulary between most (not all) of these services and their controllers.

---

## 4. Categorisation: transient vs. persistent — does any exist?

**No categorisation exists for AI/external-service failures anywhere in the codebase.** Specifically:

- `FailureCode` enum (`src/Core/QuantumBuild.Core.Application/Models/FailureCode.cs:3-17`) has 12 members — `DuplicateEmail`, `WorkflowInvitationNotFound`, `WorkflowTokenInvalid`, `WorkflowTokenAlreadyUsed`, `WorkflowTokenExpired`, `WorkflowInvalidState`, `WorkflowSubmissionInvalid`, `WorkflowInitiationInvalid`, `WorkflowConfirmationRequired`, `WorkflowReasonRequired`, `TitleNotUnique`, `Conflict`. **All are business-workflow errors (external-review invitation tokens, duplicate records) — none relate to AI/external-service transience.** Confirmed via `Result.cs:1-81` (`Fail(string, FailureCode)` factory) and usage sites (`ExternalReviewController.cs:36-38,75-86,116-121`), which branch on `ErrorCode` purely to pick an HTTP status (404/409/410/400) — never to select a user-facing message variant.
- The ElevenLabs transcription example named in this recon's brief is the clearest illustration: `ElevenLabsTranscriptionService.cs:111-115` treats every non-success status code identically — there is no branch for 401 (auth, persistent) vs 429 (rate limit, transient) vs 5xx (transient) when building the final message. (Retry/backoff for 429/5xx *does* happen one layer up, at the Polly `HttpClient` policy — `ResiliencePolicies.GetElevenLabsPolicy`, `ResiliencePolicies.cs:58-70`, 2 retries with backoff, correctly excludes 401 from the retry set — but once retries are exhausted or a non-retried status like 401 arrives, the downstream message construction doesn't know or care which happened.)
- `ResiliencePolicies.GetClaudePolicy` (`ResiliencePolicies.cs:27-52`) has the same property: after 3 retries are exhausted, Polly returns/rethrows the same `HttpResponseMessage`/`HttpRequestException` type as an unretried first-attempt failure. **Downstream code cannot distinguish "failed once" from "failed after 3 retries and ~14s of backoff"** — both produce an identical message.
- Every "friendly-ish" mapping found (`GetUserFriendlyError` in `ContentGenerationService.cs:391-415`, and the ElevenLabs UI copy referenced in the task brief — `"This can happen during high demand. Please try again"`) is **cause-blind**: it either string-sniffs a few keywords (timeout/network/rate-limit/api-key) with no formal transient/persistent split, or (per the brief's own example) applies a generic "try again" framing regardless of whether the underlying cause is a bad API key (persistent — retrying is futile) or actual rate-limiting (transient — retrying may help). No code path currently reads HTTP status code 401 vs 429 vs 5xx and produces a differentiated *user-facing* message anywhere in this recon's scope.

**Conclusion:** there is no categorisation today, at any layer, for any of the AI/external-service integrations. A fix has a clean slate here — it isn't fighting an existing-but-wrong taxonomy, there simply isn't one yet.

---

## 5. Is the full technical error logged server-side, independent of what the user sees?

**Yes, consistently, everywhere examined.** Every failure path pairs a structured `_logger.LogError` call (with the raw response body/exception passed as a structured parameter, not string-concatenated) with a separately-constructed value returned to the caller. Representative confirmed examples:

- `ElevenLabsTranscriptionService.cs:113` — `_logger.LogError("ElevenLabs API error: {StatusCode} - {Response}", response.StatusCode, responseBody)` immediately precedes the (separately-built, and in this case identical-content) return value at `:114`.
- `ClaudeTranslationService.cs:81` — logs the full raw body, then deliberately returns only the status code at `:82` (the one place body and returned message diverge in the "safe" direction).
- `DeepLTranslationService.cs:175-177` — logs `StatusCode`, `BaseUrl`, `ResponseBody`, and a hint, structured; returns only `$"DeepL API error: {response.StatusCode}"`.
- `RequirementIngestionJob.cs:881-885` — logs `{Status} — {Body}`; throws only `$"Claude API error: {response.StatusCode}"`.
- `RegulatoryIngestionController.cs` (repeated at lines 52-53, 74, 103, 136, 166, 216, 251, 277, 298, 327, 356, 385, 407) — `_logger.LogError(ex, "Error retrieving regulatory documents")` (full exception object) then `StatusCode(500, new { message = "Error retrieving regulatory documents" })` (fixed generic string) — logging and user message are fully decoupled here.
- The global exception handler (`Program.cs:358-360`) logs the full unhandled exception (`logger.LogError(ex, "Unhandled exception on {Method} {Path}", ...)`) before writing the fixed generic Problem Details response — this is the last-resort backstop and it also preserves full detail server-side.

No path was found where narrowing the user-facing message would also remove the underlying detail from logs — the two are already independently constructed in every case. A fix can freely change the user-facing string without any log-visibility regression, and does not need to add new logging to compensate.

---

## 6. UI display contract — DTO fields and frontend consumption

### Field shape (backend → frontend)

Every long-running AI/external-service feature exposes the same informal contract: a nullable `string`-typed error field alongside a status enum, populated at various points in the pipeline and read verbatim by the frontend. There is no shared error-shape type — each feature reinvented an equivalent field:

| Feature | Entity/DTO field | Backend source file:line |
|---|---|---|
| Subtitle processing | `SubtitleProcessingJob.ErrorMessage` (`SubtitleProcessingJob.cs:25`) → `SubtitleProcessingStatusDto.ErrorMessage` (mapped `SubtitleProcessingOrchestrator.cs:821`, verbatim) | `SubtitleProcessingController.cs:105-117` (`GET .../subtitles/status`) |
| Content creation wizard | `ContentCreationSession.ErrorMessage` (`ContentCreationSession.cs:86`) → `ContentCreationSessionDto.ErrorMessage` (`ContentCreationSessionService.cs:2429`) | `ContentCreationController.cs:166-191` (`GET session/{id}`) |
| Wizard `/parse`, `/quiz/generate` | Ad hoc `{ error: result.Errors.FirstOrDefault() }` anonymous object | `ToolboxTalksController.cs:447,521` |
| TransVal | No entity field — `clientMessage` is constructed at failure time and pushed only via SignalR (`TranslationValidationJob.cs:545-553,886-910`), never persisted to `TranslationValidationRun`/`TranslationValidationResult` (neither entity has an error-text column) | `ValidationRunDetailDto` has no error field (`TranslationValidationController.cs:807-881`) |
| Lesson Parser | `ParseJob.ErrorMessage` → `ParseJobDto.ErrorMessage` | `LessonParserController.cs` (`GetJobs`/`GetJobById`) |
| Regulatory ingestion | `RegulatoryDocument.LastIngestionErrorMessage` + `.LastIngestionErrorCode` → `IngestionSessionDto` (`RequirementIngestionService.cs:628-657`) | `RegulatoryIngestionController.cs:224-280` |
| Help chat | No persistence — inline `{ error: "..." }` on the same response | `HelpChatController.cs:45-83` |

### Real-time (SignalR) contract

Several features push the same field shape live via a hub instead of (or in addition to) polling: `ProgressUpdate` (subtitle processing, includes `ErrorMessage`), `ValidationComplete` (TransVal, `{ success, message }`), `ReceiveFailed` (lesson parser, `errorMessage`), `CorpusRunFailed` (`errorMessage`). All are consumed by dedicated `use-*-hub.ts` hooks that store the payload string in local state with **no transformation** — e.g. `use-validation-hub.ts:169-171` (`setError(payload.message)`), `use-lesson-parser-hub.ts:50-54`, `use-corpus-run-hub.ts:146-151`.

### Frontend rendering — the critical inconsistency to know about

There are effectively **two different frontend behaviors** in play, and a fix must account for both:

1. **Direct render, no transformation** — the majority pattern. The raw string from the DTO/hub payload is interpolated straight into a shadcn `Alert`/`AlertDescription` or a plain `<p>`: `SubtitleProcessingPanel.tsx:363`, `TranslateValidateStep.tsx:361`, `page.tsx:429` (lesson parser). Whatever string the backend puts in the field is exactly what the user sees, unformatted.
2. **Masked by a generic Axios message** — for mutation-based flows (`toast.error(...)` in an `onError` handler), many components read `error.message` off the Axios error object rather than `error.response.data.*`. Since `web/src/lib/api/client.ts:82-178`'s response interceptor only handles 401/refresh logic and does no error-shape normalization, `AxiosError.message` defaults to Axios's own generic string (e.g. `"Request failed with status code 400"`) — meaning the backend's already-crafted message (friendly or not) is silently discarded before it ever reaches the toast. Confirmed at `RegulatoryScorePanel.tsx:158-175` and is the likely behavior of `QuizStep.tsx`/`ParseStep.tsx` (learning-wizard) unless they specifically unwrap `error.response.data`. The shared `getApiErrorMessage` helper (`web/src/lib/utils.ts:12-30`) does this unwrapping correctly and is used in bulk-import flows — but it is not used consistently across all AI-feature call sites, and several components maintain their own local, slightly different extraction logic instead.

**Implication for a fix:** simply changing what string a backend service returns is not sufficient by itself for the mutation/toast code paths that read `error.message` — those need to also either (a) route through `getApiErrorMessage` (or a successor), or (b) be fixed so the backend's message reaches the toast at all. The polling/SignalR-driven Alert components, by contrast, already render whatever the backend sends verbatim, so a backend-only fix is sufficient for those.

---

## 7. Answers to the specific brief questions

1. **ElevenLabs leak — exact point:** `ElevenLabsTranscriptionService.cs:114` builds the raw string; it is written unmodified onto `SubtitleProcessingJob.ErrorMessage` at `SubtitleProcessingOrchestrator.cs:772` (inside `FailJobAsync`, prefixed with `"Transcription failed: "` at `:213`), exposed via `SubtitleProcessingStatusDto.ErrorMessage` (`MapToStatusDto`, `SubtitleProcessingOrchestrator.cs:821`) and pushed live via SignalR (`SignalRProgressReporter.cs:28-37`), and rendered verbatim at `SubtitleProcessingPanel.tsx:363`. No transformation exists anywhere in this chain (§2, row 1).
2. **Centralised or scattered:** scattered — no mapping layer exists; each service builds its own message (§3).
3. **Categorisation (transient vs persistent):** none exists anywhere in the AI/external-service paths; `FailureCode` is unrelated (workflow/business errors only) (§4).
4. **Server-side logging independent of user message:** confirmed present and independent in every path checked — a fix can narrow user messages without any log-detail loss (§5).
5. **UI contract:** a per-feature nullable `string` error field (name varies: `ErrorMessage`/`errorMessage`/`error`) read either directly (polling/SignalR Alerts) or via Axios error extraction (mutation toasts, currently inconsistent — see §6) (§6).

---

## Appendix: files referenced (backend)

- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Subtitles/ElevenLabsTranscriptionService.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Subtitles/ClaudeTranslationService.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Subtitles/SubtitleProcessingOrchestrator.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Subtitles/CloudflareR2SrtStorageProvider.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Subtitles/GoogleDriveVideoSourceProvider.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/AiSectionGenerationService.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/AiQuizGenerationService.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentParserService.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Slideshow/AiSlideshowGenerationService.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentGenerationService.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/ContentGenerationJob.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/VideoTranscriptionJob.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/ContentCreationParseJob.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Validation/DeepLTranslationService.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Validation/GeminiTranslationService.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Validation/ClaudeSonnetBackTranslationService.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Validation/DeepSeekTranslationService.cs` (obsolete)
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Validation/ConsensusEngine.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/TranslationValidationJob.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/MissingTranslationsJob.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Translations/ContentTranslationService.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/RequirementIngestionJob.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/RequirementMappingJob.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/RegulatoryScoreService.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/LessonGeneratorService.cs` (Lesson Parser)
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/LessonParseJob.cs`
- `src/QuantumBuild.API/Program.cs` (global exception handler)
- `src/QuantumBuild.API/Controllers/ToolboxTalksController.cs`
- `src/QuantumBuild.API/Controllers/SubtitleProcessingController.cs`
- `src/QuantumBuild.API/Controllers/ContentCreationController.cs`
- `src/QuantumBuild.API/Controllers/TranslationValidationController.cs`
- `src/QuantumBuild.API/Controllers/RegulatoryIngestionController.cs`
- `src/QuantumBuild.API/Controllers/RegulatoryScoreController.cs`
- `src/QuantumBuild.API/Controllers/LessonParserController.cs`
- `src/QuantumBuild.API/Controllers/HelpChatController.cs`
- `src/QuantumBuild.API/Controllers/ExternalReviewController.cs`
- `src/Core/QuantumBuild.Core.Application/Models/Result.cs`
- `src/Core/QuantumBuild.Core.Application/Models/FailureCode.cs`
- `src/Core/QuantumBuild.Core.Application/Http/ResiliencePolicies.cs`

## Appendix: files referenced (frontend)

- `web/src/lib/api/client.ts` (Axios interceptor)
- `web/src/lib/utils.ts` (`getApiErrorMessage`)
- `web/src/lib/api/toolbox-talks/subtitle-processing.ts`, `use-subtitle-processing.ts`
- `web/src/features/toolbox-talks/components/SubtitleProcessingPanel.tsx`
- `web/src/features/toolbox-talks/hooks/use-subtitle-hub.ts`
- `web/src/features/toolbox-talks/hooks/use-validation-hub.ts`
- `web/src/features/toolbox-talks/hooks/use-corpus-run-hub.ts`
- `web/src/features/toolbox-talks/components/create-wizard/steps/TranslateValidateStep.tsx` / `TranslateStep.tsx` / `ValidateStep.tsx` / `ParseStep.tsx` / `QuizStep.tsx` / `PublishStep.tsx`
- `web/src/features/toolbox-talks/components/learning-wizard/hooks/useTalkStatusPolling.ts`
- `web/src/features/toolbox-talks/components/learning-wizard/steps/ParseStep.tsx`, `QuizStep.tsx`
- `web/src/features/toolbox-talks/components/TranslationWorkflowPanel.tsx`
- `web/src/features/toolbox-talks/components/bulk-sop-import/BulkSopImportUploadPanel.tsx`, `BulkSopImportValidationPanel.tsx`
- `web/src/components/admin/bulk-import-upload-panel.tsx`, `bulk-import-validation-panel.tsx`
- `web/src/lib/api/admin/use-regulatory-ingestion.ts`
- `web/src/app/(authenticated)/admin/regulatory/system/[documentId]/page.tsx` (`describeIngestionError`)
- `web/src/features/lesson-parser/hooks/use-lesson-parser-hub.ts`
- `web/src/app/(authenticated)/admin/lesson-parser/page.tsx`
- `web/src/features/help/components/HelpAssistant.tsx`
- `web/src/features/toolbox-talks/components/settings/external-reviewers-section.tsx`
- `web/src/app/auth/set-password/page.tsx`
