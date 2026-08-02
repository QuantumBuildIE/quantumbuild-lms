# Bulk Translation Job — Foundation Recon

**Date:** 2026-08-02
**Status:** Read-only investigation. No code changed.
**Scope:** What can be safely reused, at the leaf level, to build a new off-peak/scheduled
background job that bulk-translates learnings into a tenant's employee languages, without
touching the existing synchronous new-wizard Translate path.

---

## 1. One-line summary

The true leaf-level, already-background-safe reuse candidate is
`IContentTranslationService.TranslateTextAsync()` (and its siblings `SendCustomPromptAsync`,
`TranslateBatchAsync`) — every one of its dependencies (`IAiUsageLogger`,
`IApplicableFrameworksService`) takes `tenantId` as an explicit parameter and none of them
touch `ICurrentUserService`, so the whole chain is provably Hangfire-safe today, unmodified.
One level up, `GenerateContentTranslationsCommandHandler` (dispatched via
`GenerateContentTranslationsCommand`) is a **whole-learning, one-call orchestration unit that
is already proven safe to invoke from Hangfire** — it is the exact handler `MissingTranslationsJob`
and `DailyTranslationScanJob` dispatch today, and it now threads `explicitTenantId` through every
context-sensitive call. Tenant employee languages are resolved via a simple `Employees` query
already used verbatim by both of those jobs. AI cost logging is fully wired for every translation
call. `DailyTranslationScanJob` is the closest existing multi-tenant scheduled-job precedent to
mirror for off-peak scheduling.

---

## 2. The leaf-level translation unit

### 2.1 Interface

`src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Abstractions/Translations/IContentTranslationService.cs:10-58`

```csharp
public interface IContentTranslationService
{
    Task<ContentTranslationResult> TranslateTextAsync(
        string text, string targetLanguage, bool isHtml = false,
        CancellationToken cancellationToken = default,
        string? sourceLanguage = null, string? sectorKey = null,
        bool isSafetyCritical = false,
        IEnumerable<GlossaryTermInstruction>? glossaryTerms = null,
        Guid tenantId = default, Guid? userId = null,
        bool isSystemCall = false, Guid? toolboxTalkId = null);

    Task<ContentTranslationResult> SendCustomPromptAsync(...);   // line 34-40
    Task<BatchTranslationResult> TranslateBatchAsync(...);       // line 46-57
}
```

The unit of work is **one string** — a section title, a section's HTML content, a talk title,
a talk description, a quiz question, a quiz answer option, an email subject/body line, or one
slideshow-string fragment. There is no "translate a whole learning" method on this interface;
that orchestration is built by callers looping over artefacts (see §3).

### 2.2 Implementation is already context-free

`src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Translations/ContentTranslationService.cs`

- Constructor (lines 28-42) injects `HttpClient`, `IOptions<SubtitleProcessingSettings>`,
  `IOptions<AIProviderOptions>`, `IAiUsageLogger`, `IApplicableFrameworksService`, `ILogger`.
  **No `ICurrentUserService`, no `IToolboxTalksDbContext`, no `IHttpContextAccessor`.**
- `TranslateTextAsync` (lines 45-133) takes `tenantId`, `userId`, `isSystemCall`,
  `toolboxTalkId` as plain method parameters and passes them straight into
  `_aiUsageLogger.LogAsync(...)` (lines 90-99) and into
  `_applicableFrameworksService.GetTranslationInstructionsAsync(tenantId, sectorKey, ...)`
  (line 82).
- The only state read that isn't an explicit parameter is `_claudeModel` /
  `_settings.Claude.*` — both are `IOptions<T>` singletons, safe in any execution context.

Confirmed downstream calls are equally parameter-driven:

- `IApplicableFrameworksService` (`ApplicableFrameworksService.cs:9-123`) — every public method
  takes `Guid tenantId` explicitly; queries use `IgnoreQueryFilters()` +
  `.Where(x => x.TenantId == tenantId)` (e.g. lines 14-18, 20-24). No `ICurrentUserService`
  dependency at all.
- `IAiUsageLogger` (`AiUsageLogger.cs:9-48`) — `LogAsync(tenantId, category, modelId,
  inputTokens, outputTokens, isSystemCall, userId, referenceEntityId, ct)` takes tenant/user
  explicitly, writes an `AiUsageLog` row, and **swallows all exceptions** (lines 42-46) per
  the documented rule in CLAUDE.md ("a logging failure must NEVER fail the AI operation").

**Conclusion for item 2 (context dependencies):** `IContentTranslationService` is safe to call
from a Hangfire job exactly as-is, with no wrapper or adapter needed. A bulk job only needs to
supply `tenantId`, `sourceLanguage`, `targetLanguage`, `sectorKey` (optional), `isSystemCall:
true`, and `toolboxTalkId` — all plain data already resolvable outside HTTP context.

---

## 3. The "whole learning" orchestration unit — already Hangfire-proven

Two call paths translate a full learning today; only one of them is already dispatched from
Hangfire jobs.

### 3.1 `GenerateContentTranslationsCommandHandler` — the reusable candidate

`src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/GenerateContentTranslations/GenerateContentTranslationsCommandHandler.cs`

- Loaded via `IgnoreQueryFilters()` + explicit `t.TenantId == request.TenantId` (lines 59-69) —
  comment at lines 57-58 states this is intentional: *"this handler runs in a Hangfire
  background job context where the DbContext TenantId may not be set."*
- Workflow guard `_workflowService.StartTranslation(...)` is called with
  `explicitTenantId: request.TenantId` (lines 108-114).
- After all languages are translated, `toolboxTalk.TargetLanguageCodes` is updated in-memory
  for every successfully-translated language (lines 147-170) before the single
  `SaveChangesAsync` (line 177) — this closes the gap flagged in the older
  `docs/generate-translations-data-contract-recon.md` (2026-06-24); as of the current code this
  write already exists.
- `_workflowService.RecordTranslationCompleted(...)` is also called with
  `explicitTenantId: request.TenantId` (lines 198-203) — the older recon's "Fix C" (missing
  explicit tenant on this call) is likewise already applied in the current code.
- Per-language work (`TranslateForLanguageAsync`, lines 227-538) translates: talk title
  (required — language is skipped entirely if this fails, lines 256-276), description
  (283-298), every section title+content (304-334), every quiz question text+options
  (343-414), two hardcoded email template strings (423-439), slide OCR text
  (442-479, skips slides already translated for that language), and slideshow HTML
  (481-492, delegates to `TranslateSlideshowAsync`, lines 540-693). Every one of these
  ~10 call sites passes `tenantId: toolboxTalk.TenantId, isSystemCall: true, toolboxTalkId:
  toolboxTalk.Id` explicitly into `IContentTranslationService.TranslateTextAsync`.

**This handler is already invoked from three different Hangfire jobs today** — it is not a
theoretical reuse target, it is a proven one:

| Caller | File:line | Context |
|---|---|---|
| `MissingTranslationsJob.GenerateMissingContentTranslationsAsync` | `MissingTranslationsJob.cs:274-283` | Dispatches `GenerateContentTranslationsCommand` via `ISender.Send` from a Hangfire job, `TriggeredBy = TriggeredByType.System` |
| `DailyTranslationScanJob` → enqueues `MissingTranslationsJob` | `DailyTranslationScanJob.cs:119-120` | Indirect — see §6 |
| `ContentGenerationJob.AutoGenerateTranslationsAsync` | referenced in `docs/translation-flow-investigation.md:106-124` (not re-verified line-by-line in this pass; the dispatch shape is the same `GenerateContentTranslationsCommand` via mediator) | Runs inside `ContentGenerationJob`, itself a Hangfire job |

### 3.2 `StartTalkTranslationCommand` / `TranslationValidationJob` — the new-wizard path (do not touch)

This is the path the task explicitly says must stay unchanged. For completeness:

- Frontend: `web/src/app/(authenticated)/admin/toolbox-talks/learnings/[talkId]/translate/page.tsx`
  (only match under the `learnings/**` new-wizard route per CLAUDE.md Note 29) renders
  `TranslateStep.tsx`, which calls `POST /toolbox-talks/{talkId}/translations/{languageCode}/start-translation`.
- `StartTalkTranslationCommandHandler.cs:34-96` — loads the talk (`IgnoreQueryFilters()` +
  explicit `TenantId`, lines 38-42), guards that the language is already in
  `TargetLanguageCodes` (line 48), calls `_workflow.StartTranslation(..., ct:
  cancellationToken)` **without `explicitTenantId`** (lines 54-59 — safe here because this
  handler only ever runs inside an HTTP request, so `ICurrentUserService.TenantId` is
  populated), creates a `TranslationValidationRun` with `IsNewWizard = true` (lines 67-84),
  and enqueues `TranslationValidationJob` via `_jobScheduler.EnqueueValidation(...)` (line 89).
- `TranslationValidationJob.ExecuteAsync` (`TranslationValidationJob.cs:96-593`) is a Hangfire
  job (`[Queue("content-generation")]`, line 95) that both generates the translation (if
  missing, via `GenerateTranslationForSectionsAsync`, lines 939-1253 — which itself calls the
  same leaf `IContentTranslationService.TranslateTextAsync` with explicit `tenantId:
  tenantId, isSystemCall: true, toolboxTalkId: talkId` at lines 1040-1059, 1094-1109,
  1127-1147) **and** runs back-translation consensus validation per section.

**Important terminology note:** despite the task description calling this "the existing
synchronous new-wizard Translate path," the new-wizard Translate button's actual translation
work already runs asynchronously via Hangfire (`TranslationValidationJob`) — the HTTP call only
enqueues the job and returns a `jobId` (`StartTalkTranslationCommandHandler.cs:89-95`). What is
synchronous is the user-initiated, single-talk, single-language dispatch (one button click → one
job), as opposed to a scheduled job that would sweep many talks × many languages at once. This
distinction doesn't change the instruction — this whole call chain
(`StartTalkTranslationCommandHandler` → `TranslationValidationJob`) must not be modified — it
just means "synchronous" should be read as "on-demand," not "blocking."

### 3.3 Which one to build on

- If the bulk job only needs **translation** (no back-translation validation), reusing
  `GenerateContentTranslationsCommand` end-to-end (via `ISender.Send` from the new job, exactly
  like `MissingTranslationsJob` does) requires the least new orchestration code and is already
  proven Hangfire-safe in production.
- If the bulk job needs the leaf service directly (e.g. to batch differently, add its own
  progress/cost accounting, or avoid the per-request `TranslationStarted`/`TranslationCompleted`
  workflow-event semantics that are meant for the interactive single-talk flow), call
  `IContentTranslationService.TranslateTextAsync` directly per §2 — no wrapper needed.
- Either way, the new-wizard chain in §3.2 is not on the call path and does not need touching.

---

## 4. Resolving target languages

### 4.1 How the wizard determines target languages today

Per `docs/translation-flow-investigation.md` (verified still consistent with current
`StartTalkTranslationCommandHandler.cs:48` guard): the wizard's per-language target list comes
from `ToolboxTalk.TargetLanguageCodes` (a JSON string array set during wizard Step 1
`InputConfigStep` and via `AddTargetLanguageCommand`). This is a **per-talk, user-chosen** list,
not derived from employee data.

### 4.2 How to resolve "this tenant's employee languages" — existing, reusable query

Both `MissingTranslationsJob` and `DailyTranslationScanJob` already resolve a tenant's required
languages from employee data, with the identical query shape:

`MissingTranslationsJob.cs:100-106`:
```csharp
var requiredLanguageCodes = await _coreDbContext.Employees
    .IgnoreQueryFilters()
    .Where(e => e.TenantId == tenantId && !e.IsDeleted
        && e.PreferredLanguage != null && e.PreferredLanguage != sourceLanguageCode)
    .Select(e => e.PreferredLanguage!)
    .Distinct()
    .ToListAsync(cancellationToken);
```

`DailyTranslationScanJob.cs:66-72` — same query, hardcoded to exclude `"en"` instead of the
talk's actual source language (the per-talk exclusion of the real source language is then
applied later per-talk at lines 95-100, since a scan job checks many talks with potentially
different source languages in one tenant pass).

This is a **`Core` module query** (`ICoreDbContext.Employees`, `Employee.PreferredLanguage` —
`src/Core/QuantumBuild.Core.Domain/Entities/Employee.cs`) called from a `ToolboxTalks`-module
Infrastructure job — i.e. the existing jobs already cross that module boundary the same way a
new bulk job would need to. No new query pattern is required; this exact `.Where(...).Select(...).Distinct()` shape is directly reusable.

---

## 5. AI cost logging coverage

Confirmed fully wired for translation specifically, not just in general:

- `AiOperationCategory` enum (`src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Enums/AiOperationCategory.cs:8-18`)
  has `ContentTranslation = 5` and `BackTranslation = 6` as distinct categories.
- Every one of `ContentTranslationService`'s three public methods
  (`TranslateTextAsync`, `SendCustomPromptAsync`, `TranslateBatchAsync`) calls
  `_aiUsageLogger.LogAsync(tenantId, AiOperationCategory.ContentTranslation, parsed.Model,
  parsed.InputTokens, parsed.OutputTokens, isSystemCall:, userId:, referenceEntityId:,
  cancellationToken)` immediately after the Claude call
  (`ContentTranslationService.cs:90-99`, `157-166`, `228-237`).
- `AiUsageLog` entity (`src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Entities/AiUsageLog.cs`)
  captures one row **per API call** (not per learning, not per language) — `TenantId`,
  `OperationCategory`, `ModelId`, `InputTokens`, `OutputTokens`, `CalledAt`, `IsSystemCall`,
  `UserId`, `ReferenceEntityId` (`AiUsageLogger.cs:26-37`). `ReferenceEntityId` is populated
  with `toolboxTalkId` at every current call site, so cost is already attributable per-talk.
- `isSystemCall: true` is passed at every call site inside
  `GenerateContentTranslationsCommandHandler` and `TranslationValidationJob` — the existing
  convention (documented in CLAUDE.md) is that any Hangfire-originated call sets this flag,
  which the new bulk job should follow.
- No dollar/EUR cost amount is stored on `AiUsageLog` itself — raw tokens + model ID only.
  Actual €-cost conversion happens separately via `CostEstimationService`'s static per-model
  EUR rate table (referenced in CLAUDE.md Note 8, corpus/audit context — not re-verified in
  this pass since it's outside scope) and via monthly `AggregateAiUsageJob` rollups into
  `AiUsageSummary`. Per-call token/model data is present and queryable immediately; a
  bulk-job cost dashboard would need to either reuse `CostEstimationService`'s rate table or
  query `AiUsageLog` directly filtered by `OperationCategory == ContentTranslation` (and
  `BackTranslation` if validation is added later) and `ReferenceEntityId` / `CalledAt` range.

**Conclusion for item 4:** translation AI cost is monitorable today at the per-call,
per-tenant, per-talk, per-model granularity. A bulk job that reuses
`IContentTranslationService` (directly or via `GenerateContentTranslationsCommand`) gets this
logging for free with no new code.

---

## 6. Existing scheduled/recurring Hangfire job pattern to mirror

All recurring jobs are registered in one place: `src/QuantumBuild.API/Program.cs:444-493`,
inside `using (var scope = app.Services.CreateScope())`, via
`IRecurringJobManager.AddOrUpdate<TJob>(jobId, job => job.ExecuteAsync(...), cronExpression,
[options])`. Current registrations (lines 451-492):

| Job ID | Class | Cron | Notes |
|---|---|---|---|
| `process-toolbox-talk-schedules` | `ProcessToolboxTalkSchedulesJob` | `30 6 * * *` (Ireland TZ) | |
| `send-toolbox-talk-reminders` | `SendToolboxTalkRemindersJob` | `0 8 * * *` (Ireland TZ) | |
| `update-overdue-toolbox-talks` | `UpdateOverdueToolboxTalksJob` | `0 * * * *` | hourly |
| `send-refresher-reminders` | `SendRefresherRemindersJob` | `0 9 * * *` (Ireland TZ) | |
| `daily-translation-scan` | `DailyTranslationScanJob` | `Cron.Daily(2, 0)` — 2am UTC | **closest precedent, see below** |
| `expired-session-cleanup` | `ExpiredSessionCleanupJob` | `Cron.Daily(3, 0)` — 3am UTC | |
| `stale-ingestion-sweep` | `StaleIngestionSweepJob` | `Cron.Daily(4, 0)` — 4am UTC | single-tenant-agnostic sweep (system entity, no per-tenant loop) |
| `aggregate-ai-usage` | `AggregateAiUsageJob` | `"0 3 1 * *"` — monthly | |

All jobs are registered as `builder.Services.AddScoped<TJob>()` in `Program.cs:146-158` (concrete
class, per Note 21 in CLAUDE.md — enqueue/schedule against the concrete type so
`[AutomaticRetry]` attributes are honoured).

### `DailyTranslationScanJob` — the pattern to mirror for a multi-tenant off-peak sweep

`DailyTranslationScanJob.cs:36-58`:

```csharp
[AutomaticRetry(Attempts = 2)]
public async Task ExecuteAsync(CancellationToken cancellationToken = default)
{
    var cutoff = DateTime.UtcNow.AddHours(-25);
    var tenants = await _tenantRepository.GetAllActiveAsync(cancellationToken);

    foreach (var tenant in tenants)
    {
        try { await ProcessTenantAsync(tenant.Id, cutoff, cancellationToken); }
        catch (Exception ex) { _logger.LogError(ex, "... Continuing to next tenant."); }
    }
}
```

Key characteristics worth mirroring:

- **Iterates all active tenants** via `ITenantRepository.GetAllActiveAsync()`
  (`TenantRepository.cs:19-25` — `IgnoreQueryFilters()` + `!IsDeleted && IsActive`), a
  cross-module `Core` repository already injected into a `ToolboxTalks` job.
- **Per-tenant try/catch** so one tenant's failure doesn't abort the whole sweep
  (`DailyTranslationScanJob.cs:45-54`).
- **Does not translate inline** — it only *detects* gaps and *enqueues* the next job
  (`BackgroundJob.Enqueue<MissingTranslationsJob>(...)`, line 119-120), keeping the sweep job
  itself fast and cheap, and reusing Hangfire's own retry/queue semantics for the actual
  translation work. A new bulk job could follow the same shape: a lightweight scheduled sweep
  that enqueues one `BulkTranslationJob` (or reuses `MissingTranslationsJob` directly) per
  tenant or per talk, rather than doing all translation work inline in the scheduled method.
- **`[AutomaticRetry(Attempts = N)]`** — every job in this codebase declares this explicitly
  (values range 1-2); there is no default-retry job in the list above.
- Registered with plain `Cron.Daily(hour, 0)` in UTC for jobs without a specific
  business-hours requirement (`daily-translation-scan`, `expired-session-cleanup`,
  `stale-ingestion-sweep` are staggered 2am/3am/4am UTC specifically to avoid overlapping
  load) — an "off-peak" bulk translation job would naturally slot into this same staggered
  UTC-early-morning block.

---

## 7. Existing synchronous-path tests that must stay green

These exercise the code the new job must not disturb. All are integration tests under
`tests/QuantumBuild.Tests.Integration/`, collection `"Integration"`.

### 7.1 `GenerateContentTranslationsCommandHandlerTests.cs` (44-...)

Tests the workflow guard wired into the handler the new job is likeliest to reuse (§3.1):

- `InitialLanguage_GuardPasses_WritesTranslationStartedEvent` (line 118-119)
- `AcceptedLanguage_WithoutConfirmOverwrite_GuardBlocks_NoEventWritten` (138-139)
- `AcceptedLanguage_WithConfirmOverwrite_GuardPasses_WritesTranslationStartedEvent` (164-165)
- `MixedLanguages_BlockedDoesNotPreventOtherLanguage` (183-184)

Per the test class's own doc comment (lines 12-41): it dispatches via a MediatR `ISender` from
a factory scope with **no HTTP context**, asserts only which `WorkflowEvent`s are/aren't
written (guard decisions), and does not assert actual translation output (`IContentTranslationService`
is the real implementation and is expected to fail gracefully with no external API key
configured in the test host).

### 7.2 `MissingTranslationsJobTests.cs` (33-...)

- `MissingLanguage_JobDispatchesCommand_WritesSystemTriggeredTranslationStarted` (103-104)
- `AcceptedLanguage_GuardBlocks_NoTranslationStartedWritten` (127-128)

Directly tests the existing Hangfire-caller-of-`GenerateContentTranslationsCommand` pattern a
new bulk job would extend or mirror.

### 7.3 `TranslationWorkflowServiceTests.cs` (16-...)

~60 `[Fact]` tests covering the full `TranslationWorkflowState` state machine, including the
explicit-tenant / system-context paths most relevant to a background job:

- `WorkflowService_SystemContext_ExplicitTenantIdGuidEmpty_ReturnsResultFail` (1917-1918)
- `WorkflowService_SystemContext_RecordTranslationCompleted_WithExplicitTenant_WritesEventWithCorrectTenantId` (1930-1931)
- `WorkflowService_SystemContext_RecordValidationCompleted_WithExplicitTenant_WritesEventWithCorrectTenantId` (1962-1963)
- `WorkflowService_SystemContext_MarkStale_WithExplicitTenant_WritesEventWithCorrectTenantId` (1994-1995)
- `WorkflowService_SystemContext_GetState_WithExplicitTenant_ExcludesEventsFromOtherTenants` (2031-2032)
- Plus the full `StartTranslation_From*` guard matrix (lines 423-527) that governs whether a
  bulk job's translation call would be blocked or allowed from each workflow state.

### 7.4 `TranslationSectorInstructionsTests.cs` (21-...)

Tests `IApplicableFrameworksService.GetTranslationInstructionsAsync` — a direct dependency of
`ContentTranslationService.TranslateTextAsync` (§2.2) — across sector/regulation/standard
combinations (lines 196-307). Any reuse of the leaf service inherits this behaviour unchanged.

### 7.5 Not re-verified in this pass (out of scope, noted for completeness)

- `ClaudeTranslationServiceTests.cs` (subtitle translation — a different service,
  `ITranslationService`, not `IContentTranslationService`; out of scope per the task's
  non-scope note that this recon is about learnings, not subtitles).
- `TranslatedSectionEntryTests.cs`, `BackTranslationSelectorTests.cs` — unit tests for small
  supporting types, unrelated to context-safety.

---

## 8. Summary table — what's reusable, what's not, what's new

| Concern | Existing artefact | Reusable as-is for a bulk job? |
|---|---|---|
| Translate one string | `IContentTranslationService.TranslateTextAsync` | **Yes** — fully parameterized, no ambient context |
| AI usage/cost logging | `IAiUsageLogger.LogAsync` (called inside the above) | **Yes** — automatic, per-call, tenant/model/token granular |
| Sector-aware prompt instructions | `IApplicableFrameworksService.GetTranslationInstructionsAsync` | **Yes** — explicit `tenantId` param |
| Translate one whole learning, one language | `GenerateContentTranslationsCommand` / `GenerateContentTranslationsCommandHandler` | **Yes** — already dispatched from Hangfire jobs today, explicit-tenant throughout |
| Resolve tenant's employee target languages | `Employees.Where(...PreferredLanguage...)` query (`MissingTranslationsJob.cs:100-106`) | **Yes** — copy the exact LINQ shape |
| Iterate all tenants safely in a scheduled job | `DailyTranslationScanJob.ExecuteAsync` shape | **Yes** — pattern to mirror, not code to call |
| Enqueue jobs via concrete class (Note 21) | `TranslationJobScheduler` (`BackgroundJob.Enqueue<MissingTranslationsJob>(...)`) | **Yes** — same pattern for a new bulk job's enqueue calls |
| Per-language translate + validate, new-wizard button | `StartTalkTranslationCommand` → `TranslationValidationJob` | **No — must not be touched** (task requirement) |
| Back-translation validation itself | `ITranslationValidationService.ValidateSectionAsync` / `ConsensusEngine` | **Not investigated** — out of scope; the task's "translate" bulk job doesn't need it, only §3.1/§2 |

---

## 9. Files read

### Backend — Application layer
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/StartTalkTranslation/StartTalkTranslationCommandHandler.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/GenerateContentTranslations/GenerateContentTranslationsCommandHandler.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Abstractions/Translations/IContentTranslationService.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Abstractions/Translations/ITranslationJobScheduler.cs`

### Backend — Infrastructure layer
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/TranslationValidationJob.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/MissingTranslationsJob.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/DailyTranslationScanJob.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/StaleIngestionSweepJob.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Translations/ContentTranslationService.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Translations/TranslationJobScheduler.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Workflows/TranslationWorkflowService.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/AiUsageLogger.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Frameworks/ApplicableFrameworksService.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Enums/AiOperationCategory.cs`
- `src/Core/QuantumBuild.Core.Infrastructure/Repositories/TenantRepository.cs`
- `src/QuantumBuild.API/Program.cs` (lines 140-160, 440-493)

### Frontend
- `web/src/app/(authenticated)/admin/toolbox-talks/learnings/[talkId]/translate/page.tsx` (existence check only, per Grep)

### Tests
- `tests/QuantumBuild.Tests.Integration/ToolboxTalks/GenerateContentTranslationsCommandHandlerTests.cs`
- `tests/QuantumBuild.Tests.Integration/ToolboxTalks/MissingTranslationsJobTests.cs`
- `tests/QuantumBuild.Tests.Integration/Workflows/TranslationWorkflowServiceTests.cs`
- `tests/QuantumBuild.Tests.Integration/ToolboxTalks/TranslationSectorInstructionsTests.cs`

### Prior recon docs consulted (dates noted where the finding might be stale)
- `docs/generate-translations-data-contract-recon.md` (2026-06-24) — its "Fix A" (append to
  `TargetLanguageCodes`) and "Fix C" (explicit tenant on workflow calls) are **already applied**
  in the current `GenerateContentTranslationsCommandHandler.cs`; treat that doc's "Risks and
  Edge Cases" and earlier sections as historical context only, not current-state fact.
- `docs/translation-flow-investigation.md` (2026-06-19) — trigger table and per-content-type
  flow cross-checked against current handler code in §3 above; new-wizard route path was
  re-verified against the current `learnings/**` URL structure (CLAUDE.md Note 29).

### CLAUDE.md sections referenced
- AI Usage Logging
- Notes 21, 22, 23 (Hangfire enqueue-by-concrete-class, TenantId auto-stamp, DbContext isolation)
- Note 29 (wizard cutover — confirms `learnings/**` is the current new-wizard route)
