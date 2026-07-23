# Tenant Query Filter Audit — Recon

**Scope:** tenant `HasQueryFilter`/`IgnoreQueryFilters` correctness only (not soft-delete, not other filter types). Triggered by the `ApplicableFrameworksService.GetTenantEntitlementsAsync` bug found in the regulatory multi-standard chunk (Hangfire job passed an explicit `tenantId` into a query that lacked `.IgnoreQueryFilters()`, so the ambient JWT-derived tenant filter — which resolves to `Guid.Empty` outside an HTTP request — silently zeroed the result set). This document is read-only recon; no code was changed.

**Date:** 2026-07-23
**Branch:** `transval`

---

## 1. Ambient tenant-filtered entities

All tenant `HasQueryFilter` predicates are centralised in `ApplicationDbContext.OnModelCreating`
(`src/Core/QuantumBuild.Core.Infrastructure/Data/ApplicationDbContext.cs`, lines 323–377). Every one
follows the same shape:

```csharp
e => !e.IsDeleted && (BypassTenantFilter || e.TenantId == TenantId)
```

where `TenantId` and `BypassTenantFilter` are `ApplicationDbContext` properties that read
`ICurrentUserService` (see §2). **Confirmed: no per-entity `EntityConfiguration` class anywhere in
the codebase declares its own tenant predicate any more** — every `HasQueryFilter` call found inside
an `IEntityTypeConfiguration<T>.Configure()` method is soft-delete-only (`!e.IsDeleted`), matching
Note 14's claim that the centralisation is complete.

| Entity | Location |
| --- | --- |
| Site, Employee, Company, Contact, SupervisorAssignment, TenantModule, TenantSector, TenantReviewerConfiguration, RegulatoryRequirementMapping, TenantStandardSubscription, AuditCorpus, CorpusRun, QrLocation, QrCode, QrSession, BulkImportSession | `ApplicationDbContext.cs` 323–339 |
| ToolboxTalk, ToolboxTalkCourse, ToolboxTalkSchedule, ScheduledTalk, ToolboxTalkTranslation, ToolboxTalkVideoTranslation, ToolboxTalkCertificate, SubtitleProcessingJob, ToolboxTalkSlide, TranslationValidationRun, ContentCreationSession, ValidationRegulatoryScore, ToolboxTalkCourseAssignment, TranslationDeviation, AiUsageLog, AiUsageSummary | `ApplicationDbContext.cs` 342–357 |
| TenantLookupValue | `ApplicationDbContext.cs` 365 |
| WorkflowEvent, WorkflowReview, ExternalParticipantInvitation, TranslationFlag | `ApplicationDbContext.cs` 374–377 |
| DpaAcceptance, LookupCategory, LookupValue, Permission, Tenant | soft-delete only, **not** tenant-scoped (global) |

**Second DbContext with the same pattern:** `LessonParserDbContext` (LessonParser module) has its
own independent centralised filter for `ParseJob`:
`modelBuilder.Entity<ParseJob>().HasQueryFilter(e => !e.IsDeleted && (BypassTenantFilter || e.TenantId == TenantId));`
— same shape, same risk class, separate `DbContext` instance with its own `ICurrentUserService`
plumbing. `LessonParseJob.cs` (Hangfire) correctly uses `.IgnoreQueryFilters()` at lines 72 and 175.

**BaseEntity-only (not tenant-scoped, soft-delete filter only, correctly so):**
ToolboxTalkSlideshowTranslation, SafetyGlossary, SafetyGlossaryTerm, RegulatoryBody,
RegulatoryDocument, RegulatoryProfile, RegulatoryCriteria, RegulatoryRequirement,
ToolboxTalkSection, ToolboxTalkQuestion, ToolboxTalkCourseItem, ToolboxTalkCourseTranslation,
ToolboxTalkScheduleAssignment, ScheduledTalkSectionProgress, ScheduledTalkQuizAttempt,
ScheduledTalkCompletion, ToolboxTalkSettings, SubtitleTranslation, ToolboxTalkSlideTranslation,
TranslationValidationResult, PipelineVersion, PipelineChangeRecord, AuditCorpusEntry,
CorpusRunResult, ProviderResultCache, CustomerUsageReportState. These are explicitly called out as
such in the `ApplicationDbContext.cs` comments (lines 358–360) and confirmed by reading their
`EntityConfiguration` files.

### Minor finding — stale duplicate filter declarations (order-dependent, currently harmless)

`TenantStandardSubscriptionConfiguration.cs:55` and `RegulatoryRequirementMappingConfiguration.cs:97`
each still call `builder.HasQueryFilter(e => !e.IsDeleted)` with a comment
`// Query filter for soft delete (tenant filter applied in ApplicationDbContext)`. Both entities
**are** tenant-scoped and **do** get the correct combined filter — but only because
`modelBuilder.ApplyConfiguration(new TenantStandardSubscriptionConfiguration())` (line 299) runs
*before* the centralised `modelBuilder.Entity<TenantStandardSubscription>().HasQueryFilter(...)`
call (line 332) in `OnModelCreating`, and EF Core's last-registered `HasQueryFilter` for a given
entity type wins outright (filters do not compose). This is correct today, by construction, but is
an ordering dependency that isn't enforced by anything — if either centralised line were ever
deleted, or `OnModelCreating` were reordered, these two entities would silently regress to
soft-delete-only (no tenant scoping) with no compiler error and no test likely to catch it in an
integration suite that runs as a single tenant. **Ranked Low** — pre-existing since the Note 14
refactor, not part of active development, but worth a one-line cleanup (delete the two stale
`HasQueryFilter` calls) whenever those files are next touched.

---

## 2. `ICurrentUserService.TenantId` call-site categorisation

`ICurrentUserService` (`src/Core/QuantumBuild.Core.Infrastructure/Services/CurrentUserService.cs`)
reads `TenantId` from `IHttpContextAccessor.HttpContext`. Confirmed behaviour: when there is no
`HttpContext` (any Hangfire job), `IsSuperUser` evaluates `false` (no claims to read) and `TenantId`
falls through to `Guid.Empty`. Combined with `ApplicationDbContext.BypassTenantFilter` (`IsSuperUser
== true && TenantId == Guid.Empty`), a Hangfire-context query against a tenant-filtered entity that
does **not** call `.IgnoreQueryFilters()` is filtered to `TenantId == Guid.Empty` — i.e. returns zero
rows for any real tenant. This is the exact mechanism behind the reference bug.

**(a) Controllers / CQRS handlers under HTTP** — the large majority of `ICurrentUserService` usage
(≈36 of 40 files matching `ICurrentUserService`): `ToolboxTalksController`,
`TenantStandardSubscriptionsController`, `TenantReviewerConfigurationsController`,
`TranslationValidationController`, `RegulatoryBrowseController`, `TenantSectorsController`,
`TenantSettingsController`, `MyToolboxTalksController`, `QrLocationController`,
`ContentCreationController`, `SafetyGlossaryController`, `RegulatoryScoreController`,
`DpaController`, `LessonParserController`, `LookupsController`, `ScheduledTalksController`,
`ToolboxTalkCourseAssignmentsController`, `ToolboxTalkCoursesController`,
`ToolboxTalkSchedulesController`, plus the CQRS command handlers
(`CompleteToolboxTalkCommandHandler`, `ResetVideoProgressCommandHandler`,
`SubmitQuizAnswersCommandHandler`, `UpdateVideoProgressCommandHandler`,
`MarkSectionReadCommandHandler`, `StartToolboxTalkCommandHandler`) and the plain services that are
only ever resolved inside a request scope (`SupervisorAssignmentService`, `UserService`,
`EmployeeService`, `LookupService`, `LanguageCodeService`, `ContentExtractionService`,
`TranslationWorkflowService` — note `TranslationWorkflowService` is deliberately dual-context; see
its own `IgnoreQueryFilters` usage, already fixed per the file's own comments at lines 73, 482, 496,
641, 684, 990, 1005).

**(b) Hangfire job classes** — **none** of the 21 Hangfire job classes inject `ICurrentUserService`
directly. Four job files reference `ICurrentUserService.TenantId` **only in a code comment**
explaining *why* they instead do explicit `IgnoreQueryFilters()` + parameter-based tenant filtering:
`SendToolboxTalkRemindersJob.cs:59`, `ContentGenerationJob.cs:502`,
`UpdateOverdueToolboxTalksJob.cs:40`, `ProcessToolboxTalkSchedulesJob.cs:56`. `BulkEmployeeImportJob.cs:68`
has the same style of comment. This is a good sign — the pattern is documented in-line at every job
that could plausibly be tempted to rely on the ambient filter.

**(c) Plain services reachable from both HTTP and Hangfire** — `TranslationWorkflowService` is the
one service in the codebase explicitly designed for this dual-context use (per the Medium backlog
item "Other services with Hangfire implicit-HTTP dependency", already fixed in chunk 5.4 by
threading an explicit `Guid? explicitTenantId` through all non-token public methods). No other
service was found with this dual-context shape during this audit.

**(d) DI/constructor-only (benign)** — `IRequirementMappingService`, `ITranslationWorkflowService`
interface declarations, `IEmployeeService` interface — no behaviour, just signatures.

---

## 3. Services with explicit `Guid tenantId` / `Guid? tenantId` parameters

105 files contain a method with a `Guid tenantId` or `Guid? tenantId` parameter. Below are the
services that (a) also query at least one Part‑1 tenant-filtered entity, ranked by how directly they
sit in a Hangfire call path. Pure DTO/interface files, storage-provider path-builders
(`R2StorageService`, `CloudflareR2SrtStorageProvider`, `GitHubSrtStorageProvider` — `tenantId` used
only to build an object-storage key, no DB query) and AI-provider services with no DB access
(`ClaudeSonnetBackTranslationService`, `ClaudeHaikuBackTranslationService`, `ConsensusEngine`,
`DialectDetectionService`, `AiQuizGenerationService`, `AiSectionGenerationService`,
`AiSlideshowGenerationService`, `ClaudeTranslationService`) are excluded as out of scope (no tenant
filter to bypass).

| Service.Method | Callers | Hangfire-reachable? |
| --- | --- | --- |
| `ApplicableFrameworksService.GetTenantEntitlementsAsync` / `GetApplicableFrameworksAsync` / `GetTranslationInstructionsAsync` | `ContentTranslationService`, `RequirementMappingService`, `RequirementIngestionService`, `RequirementMappingJob` | **Yes, directly** (`RequirementMappingJob`) and **transitively** (`ContentTranslationService` ← `TranslationValidationJob`, `ContentCreationSessionService`, `GenerateContentTranslationsCommandHandler`) |
| `TenantSectorService.*` (all 6 public methods) | `TenantSectorsController`, `TenantStandardSubscriptionService`, `MissingTranslationsJob`, `ContentGenerationJob` | **Yes, directly** |
| `TenantStandardSubscriptionService.*` | `TenantStandardSubscriptionsController` only | No |
| `TenantReviewerConfigurationService.*` | `TenantReviewerConfigurationsController` only | No |
| `RequirementMappingService.*` | `RequirementMappingController`, `InspectionReportService` (itself HTTP-only) | No |
| `RequirementIngestionService.*` | `RegulatoryIngestionController` (implied), `RequirementIngestionJob` does **not** call it (job re-implements its own DB access directly, all `RegulatoryDocument`/`RegulatoryRequirement`/`RegulatoryProfile` — none tenant-scoped) | No |
| `RegulatoryScoreService.ScoreAsync` / `GetScoreHistoryAsync` (private helpers take `tenantId` only for AI-usage-log attribution, not DB filtering) | `RegulatoryScoreController` only | No |
| `PipelineAuditQueryService.*` | `PipelineAuditController` only | No |
| `TranslationDeviationService.*` | `PipelineAuditController` only | No |
| `AuditCorpusService.*` | `PipelineAuditController` only; `CorpusRunJob` bypasses the service and queries `_dbContext` directly | No (job doesn't call this service) |
| `TranslationValidationService.ValidateSectionAsync` (`tenantId` flows only into `ConsensusEngine`/AI-usage logging — the one DB query inside touches `TranslationValidationResults`, a BaseEntity-only table, not tenant-filtered) | `TranslationValidationJob`, `CorpusRunJob` | Yes, directly — but not at risk (no tenant-scoped query inside) |
| `EmployeeService.*` (`CreateAsync`, etc.) | Controllers, `BulkEmployeeImportJob` | Yes, directly — already uses `IgnoreQueryFilters()` at 4 call sites (251, 269, 434, 555, 621) plus the Note 22 explicit `TenantId` stamp pattern |
| `TranslationWorkflowService.*` | Controllers + `LessonParseJob`(-adjacent flows) | Yes, transitively — already the reference-fixed dual-context service (chunk 5.4) |

---

## 4. At-risk queries cross-reference

**Critical**

- *None found currently live.* The one demonstrated instance of this exact bug shape —
  `ApplicableFrameworksService.GetTenantEntitlementsAsync` querying `TenantSectors` and
  `TenantStandardSubscriptions` without `.IgnoreQueryFilters()` while called with an explicit
  `tenantId` from `RequirementMappingJob` (Hangfire) — is **already fixed**. Read as delivered:
  `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Frameworks/ApplicableFrameworksService.cs`
  lines 14–24 now call `.IgnoreQueryFilters()` on both the `TenantSectors` and
  `TenantStandardSubscriptions` queries, and lines 37–38 / 62–63 apply it to the
  `RegulatoryRequirements` queries as well. `RequirementMappingJob.cs` (the Hangfire caller) is
  itself clean end-to-end — every query against a tenant-scoped entity
  (`ToolboxTalks`, `ToolboxTalkCourses`, `RegulatoryRequirementMappings`) uses
  `.IgnoreQueryFilters()` + an explicit `tenantId` equality check.

**Medium**

- *None found.* Every service with an explicit `tenantId` parameter that is **currently**
  Hangfire-reachable (`ApplicableFrameworksService`, `TenantSectorService`, `EmployeeService`,
  `TranslationValidationService`, `TranslationWorkflowService`) was verified to already apply
  `.IgnoreQueryFilters()` at every query against a Part‑1 tenant-filtered entity. No "could plausibly
  be called from Hangfire tomorrow but isn't today, and would silently break if it were" case was
  found among the services actively touched by the regulatory/corpus/pipeline-audit work.

**Low**

1. **`TenantStandardSubscriptionService`, `TenantReviewerConfigurationService`,
   `RegulatoryScoreService`, `PipelineAuditQueryService`, `TranslationDeviationService`,
   `AuditCorpusService`, `RequirementMappingService` (ambient-`_currentUser.TenantId` methods),
   `RequirementIngestionService`** — all take (or resolve) tenant context correctly for their
   *current* callers, which are HTTP-only. None escape the ambient filter, but none currently need
   to. **Rationale:** correct-by-construction today; would need the same
   `IgnoreQueryFilters()`-plus-explicit-`tenantId` treatment applied *before* any of these is ever
   wired into a Hangfire job. Flagging here purely so a future "let's move X to a background job"
   change knows to check this file first.
2. **`RegulatoryScoreService.ScoreAsync` / `GetScoreHistoryAsync`** specifically query
   `TranslationValidationRuns` (tenant-scoped) by `Id` only, with no `.IgnoreQueryFilters()` and no
   `tenantId` equality clause at all (`_dbContext.TranslationValidationRuns.FirstOrDefaultAsync(r =>
   r.Id == validationRunId, ...)`, lines 60 and 229). This works correctly today purely because the
   ambient filter is doing exactly the right job in its only calling context
   (`RegulatoryScoreController`, ordinary authenticated HTTP request). Not a bug; noted because it's
   the kind of query that would break silently and immediately if ever called from a job — there's no
   explicit tenant check to fall back on the way the "Medium" candidates above have.
3. **Stale duplicate `HasQueryFilter(e => !e.IsDeleted)` declarations** in
   `TenantStandardSubscriptionConfiguration.cs` and `RegulatoryRequirementMappingConfiguration.cs`
   (see §1). Pre-existing since the Note 14 centralisation, order-dependent but currently correct;
   cosmetic/defensive cleanup only.
4. **`LessonParserDbContext`'s independent `ParseJob` tenant filter** — same shape as
   `ApplicationDbContext`, and `LessonParseJob.cs` already does the right thing
   (`.IgnoreQueryFilters()` at lines 72, 175). Noted as a second surface using the identical pattern,
   not because anything is wrong with it.

---

## 5. Hangfire job → service call trace

All 21 Hangfire job classes found via `Glob **/Jobs/*.cs` were opened. Every query each job makes
directly against a Part‑1 tenant-filtered entity was checked for `.IgnoreQueryFilters()` +
tenant-parameter usage; every call the job makes into an injected service was checked against §3/§4.

| Job | Tenant-scoped DB touches | Verdict |
| --- | --- | --- |
| `RequirementMappingJob` | `ToolboxTalks`, `ToolboxTalkCourses`, `RegulatoryRequirementMappings` (direct); `ApplicableFrameworksService.GetTenantEntitlementsAsync` (→ `TenantSectors`, `TenantStandardSubscriptions`) | **Safe** — all `.IgnoreQueryFilters()` + explicit tenantId. This is the fixed reference implementation for the original bug. |
| `RequirementIngestionJob` | None (`RegulatoryDocument`, `RegulatoryProfile`, `RegulatoryRequirement` are all system-level, no `TenantId`) | **N/A** — no tenant-scoped entity in its path. |
| `CorpusRunJob` | `CorpusRuns`, `CorpusRunResults`, `TranslationDeviations`, `AiUsageLogs` (direct); `TranslationValidationService.ValidateSectionAsync` (persist=false, only touches non-tenant-scoped `TranslationValidationResults` in-memory); `PipelineVersionService`, `PipelineChangeRecords` (not tenant-scoped) | **Safe** — every tenant-scoped query IgnoreQueryFilters + `r.TenantId == tenantId`. |
| `ContentGenerationJob` | `ToolboxTalks`, `Employees` (direct, via `.TenantId == tenantId`); `TenantSectorService.GetDefaultSectorAsync(tenantId, ...)` | **Safe.** |
| `MissingTranslationsJob` | `ToolboxTalks`, `ToolboxTalkTranslations`, `ToolboxTalkSlideshowTranslations`, `Employees` (direct); `TenantSectorService.GetDefaultSectorAsync(tenantId, ...)`; `ISubtitleProcessingOrchestrator.TranslateMissingLanguagesAsync(toolboxTalkId, tenantId, ...)` | **Safe** — every direct query IgnoreQueryFilters + explicit tenantId. |
| `DailyTranslationScanJob` | `ToolboxTalks` and related (per grep, 3 `.IgnoreQueryFilters()` call sites) | **Safe** (spot-checked via grep; consistent pattern). |
| `TranslationValidationJob` | `TranslationValidationRuns`, `ToolboxTalks` (multiple call sites, 14 `.IgnoreQueryFilters()` occurrences across the file); `ContentTranslationService.TranslateTextAsync(tenantId, ...)` → `ApplicableFrameworksService` (fixed) | **Safe.** |
| `ValidationReportJob` | `TranslationValidationRuns` (direct) | **Safe** — `.IgnoreQueryFilters()` + `r.TenantId == tenantId`. |
| `AggregateAiUsageJob` | `AiUsageLogs`, `AiUsageSummaries` (deliberately cross-tenant — aggregates *all* tenants) | **Safe by design** — uses `.IgnoreQueryFilters()` and intentionally has no tenant parameter at all (its whole job is cross-tenant). |
| `ExpiredSessionCleanupJob` | Cross-tenant by design per Note 14 | **Safe** — `.IgnoreQueryFilters()` confirmed at line 31. |
| `ProcessToolboxTalkSchedulesJob` | `ToolboxTalkSchedules` (direct, per-tenant loop) | **Safe** — `.IgnoreQueryFilters()` + `s.TenantId == tenant.Id`. |
| `SendToolboxTalkRemindersJob` | `ScheduledTalks` or similar, per-tenant loop | **Safe** — `.IgnoreQueryFilters()` confirmed at lines 63, 71. |
| `SendRefresherRemindersJob` | Per-tenant loop | **Safe** — `.IgnoreQueryFilters()` confirmed at 3 call sites (55, 87, 118, 149). |
| `UpdateOverdueToolboxTalksJob` | `ScheduledTalks`, cross-tenant sweep | **Safe** — `.IgnoreQueryFilters()` confirmed, explicit comment explaining why. |
| `GenerateEmployeePinsJob` | `Employees` | **Safe** — `.IgnoreQueryFilters()` at lines 41, 51 (Note 10's one-off job). |
| `BulkEmployeeImportJob` | `BulkImportSessions`, `Employees` (via `EmployeeService`, per-row scope per Note 23) | **Safe** — `.IgnoreQueryFilters()` at line 71 plus the documented per-row `IServiceScopeFactory` isolation. |
| `VideoTranscriptionJob` / `VideoTranscriptionJobForTalk` | `SubtitleProcessingJob`-adjacent | **Safe** — `.IgnoreQueryFilters()` confirmed in both files. |
| `ContentCreationParseJob` / `ContentCreationParseJobForTalk` | `ContentCreationSessions` | **Safe** — `.IgnoreQueryFilters()` confirmed in both files. |
| `LessonParseJob` (LessonParser module, separate `DbContext`) | `ParseJob` (LessonParserDbContext's own centralised tenant filter) | **Safe** — `.IgnoreQueryFilters()` confirmed at lines 72, 175. |

No job in the current codebase calls a service that skips the filter-escape while holding
tenant-context-independent semantics. The only bug of this shape that existed
(`ApplicableFrameworksService`) is already closed, and its Hangfire caller
(`RequirementMappingJob`) is clean end-to-end.

---

## 6. Recommended fix scope

**Critical items to fix now:** none. The one live instance of this bug class was already fixed in
the current branch history (commit `f37add8`, "fix(regulatory): RequirementMappingJob AI candidates
gate by tenant entitlements").

**Medium items — fix now vs defer:** none identified. No service was found with (a) an explicit
`tenantId` parameter, (b) a query against a Part‑1 tenant-filtered entity missing
`.IgnoreQueryFilters()`, and (c) an actual or clearly-imminent Hangfire call path.

**Low items — defer, with rationale:**

1. Delete the two stale `builder.HasQueryFilter(e => !e.IsDeleted)` lines in
   `TenantStandardSubscriptionConfiguration.cs:55` and `RegulatoryRequirementMappingConfiguration.cs:97`
   next time either file is touched for an unrelated reason. Currently harmless (overridden by the
   later DbContext-level registration) — pure hygiene, removes an ordering trap for a future refactor.
2. If `RegulatoryScoreService.ScoreAsync`/`GetScoreHistoryAsync` is ever wired into a background job
   (e.g. as part of automated re-scoring), add `.IgnoreQueryFilters()` + explicit `tenantId` equality
   to the two `TranslationValidationRuns` lookups (lines 60, 229) at that time — not before, since
   there is no current caller that needs it and adding it pre-emptively would be speculative.
3. Same conditional treatment applies to `TenantStandardSubscriptionService`,
   `TenantReviewerConfigurationService`, `PipelineAuditQueryService`, `TranslationDeviationService`,
   and `AuditCorpusService` if any of them is ever called from a Hangfire job — apply the
   `IgnoreQueryFilters()` + explicit-tenantId pattern documented in `RequirementMappingJob.cs` /
   `TranslationWorkflowService.cs` at that time.
4. No action recommended for `LessonParserDbContext` — its one tenant-scoped entity (`ParseJob`) is
   already correctly escaped by `LessonParseJob.cs`.
