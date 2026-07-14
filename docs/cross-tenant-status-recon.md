# Cross-Tenant Leak Status Recon — 2026-06-23

**Author:** Claude Code security recon agent
**Trigger:** Customer demo 2026-06-26. Prior session claimed the cross-tenant leak was "surfaced and triaged in demo-prep work around 22 June 2026."

---

## 1. One-Line Summary

**Claim is partially confirmed but the timing is wrong.** The 12-entity fix landed in commit `bda907a` on **2026-05-17**, not "around 22 June 2026" — the prior session misremembered the date. The fix is real and in place. However, **four additional TenantEntity types have no tenant predicate in ApplicationDbContext** and their entity configurations only filter by soft-delete — a gap introduced before the fix but never caught by it, because those entities were already outside the original "12" scope. **Demo risk is LOW** for the core demo path, **MEDIUM** for the AI-usage billing trail if two tenants ever land on the same demo instance.

---

## 2. Original Finding

**Source:** Git commit `bda907a` — "security: fix cross-tenant data leak in Toolbox Talks module" — dated **2026-05-17 16:36 +0100**.

**Commit message states:** TenantEntity-derived Toolbox Talks entities had global query filters with `!IsDeleted` only — no tenant predicate. Any query path missing an explicit `.Where(t => t.TenantId == ...)` clause returned rows from all tenants.

**No pre-existing BACKLOG.md entry or docs/ file** was found for this finding. It appears to have been discovered and fixed in the same session without a separate triage document. The CLAUDE.md Note 14 entry was added as the documentation artifact. There is no §7.9 or any other backlog item explicitly labelled "cross-tenant leak."

**The "22 June 2026" date in the prior session claim is incorrect.** The fix is 37 days old, not 1 day old.

---

## 3. Closure Trail

### Primary fix — commit `bda907a` (2026-05-17)

Files changed:
- `src/Core/QuantumBuild.Core.Infrastructure/Data/ApplicationDbContext.cs` — replaced the deferral comment with 12 HasQueryFilter calls adding `BypassTenantFilter || e.TenantId == TenantId` predicate
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Persistence/Configurations/ContentCreationSessionConfiguration.cs` — removed soft-delete-only filter
- `ScheduledTalkConfiguration.cs` — removed soft-delete-only filter
- `SubtitleProcessingJobConfiguration.cs` — removed soft-delete-only filter
- `ToolboxTalkCertificateConfiguration.cs` — removed soft-delete-only filter
- `ToolboxTalkConfiguration.cs` — removed soft-delete-only filter
- `ToolboxTalkCourseConfiguration.cs` — removed soft-delete-only filter
- `ToolboxTalkScheduleConfiguration.cs` — removed soft-delete-only filter
- `ToolboxTalkSlideConfiguration.cs` — removed soft-delete-only filter
- `ToolboxTalkTranslationConfiguration.cs` — removed soft-delete-only filter
- `ToolboxTalkVideoTranslationConfiguration.cs` — removed soft-delete-only filter
- `TranslationValidationRunConfiguration.cs` — removed soft-delete-only filter
- `ValidationRegulatoryScoreConfiguration.cs` — removed soft-delete-only filter
- `Jobs/ExpiredSessionCleanupJob.cs` — added `IgnoreQueryFilters()` + `!IsDeleted` for legitimate cross-tenant cleanup
- `Jobs/CorpusRunJob.cs` — added `IgnoreQueryFilters()` to 4 queries

**The "12 entities" claim is exactly correct.** The 12 TenantEntity types covered by the fix are:

1. ToolboxTalk
2. ToolboxTalkCourse
3. ToolboxTalkSchedule
4. ScheduledTalk
5. ToolboxTalkTranslation
6. ToolboxTalkVideoTranslation
7. ToolboxTalkCertificate
8. SubtitleProcessingJob
9. ToolboxTalkSlide
10. TranslationValidationRun
11. ContentCreationSession
12. ValidationRegulatoryScore

### Secondary hardening — commit `fe33aad` (2026-05-17, 10 minutes after `bda907a`)

`RequirementMappingJob.cs` — added explicit TenantId predicates to ToolboxTalk and ToolboxTalkCourse queries in `BuildContentStringAsync`. No HasQueryFilter change.

### Post-fix new entities — commits `b17c53a`, `b567ec8`, `497dd53` (all after `bda907a`)

Three commits added new TenantEntity types to ApplicationDbContext after the fix, all with correct HasQueryFilter in the context:

- `b17c53a` (workflows): WorkflowEvent, WorkflowReview, ExternalParticipantInvitation, TranslationFlag — all added to ApplicationDbContext HasQueryFilter block correctly (lines 364–367 of ApplicationDbContext.cs)
- `b567ec8` (bulk import): BulkImportSession — added correctly (line 333)
- `497dd53` (monitoring): CustomerUsageReportState — BaseEntity, no TenantId, correct not to include

---

## 4. Coverage Gaps in Original Closure

The original fix addressed entities that had *already received* soft-delete-only `HasQueryFilter` in their entity configurations. Four TenantEntity types that were present before the fix but either never had a `HasQueryFilter` in their entity configuration, or had it in their config as `!IsDeleted` only and were **not listed in the fix commit**, are currently unprotected at the DbContext query-filter layer:

### Gap A — `ToolboxTalkCourseAssignment` (TenantEntity)

- **Config:** `ToolboxTalkCourseAssignmentConfiguration.cs` line 117 has `builder.HasQueryFilter(x => !x.IsDeleted)` — soft-delete only
- **ApplicationDbContext:** No HasQueryFilter call for this type
- **Exposure:** Queries that don't include an explicit `TenantId` predicate in the `Where` clause would return cross-tenant rows
- **Current mitigation:** All query handler paths verified to include explicit `TenantId` predicates in `Where` clauses (e.g., `GetCourseAssignmentsQueryHandler.cs:21`, `GetCourseAssignmentByIdQueryHandler.cs:21`). The `AutoAssignmentService`, `CourseProgressService`, and `RefresherSchedulingService` all scope by ID chains that are already tenant-scoped through the parent ScheduledTalk or ToolboxTalk. Risk is medium-low but the missing filter creates a defence-in-depth gap.

### Gap B — `TranslationDeviation` (TenantEntity)

- **Config:** `TranslationDeviationConfiguration.cs` line 104 has `builder.HasQueryFilter(d => !d.IsDeleted)` — soft-delete only
- **ApplicationDbContext:** No HasQueryFilter call
- **Exposure:** Same pattern as above
- **Current mitigation:** `TranslationDeviationService.cs` always includes explicit `d.TenantId == tenantId` in every query (lines 80–81, 110–111, 127–128, 137–138, 160–162). `PipelineAuditController.cs` line 611 also includes `d.TenantId == tenantId`. Risk is low because of consistent service-layer guards.

### Gap C — `AiUsageLog` (TenantEntity)

- **Config:** `AiUsageLogConfiguration.cs` line 70 has `builder.HasQueryFilter(e => !e.IsDeleted)` — soft-delete only
- **ApplicationDbContext:** No HasQueryFilter call
- **Exposure:** AI usage billing data could cross tenants if any query path omits TenantId
- **Current mitigation:** Not verified in detail; `AiUsageLog` is written to by `AiUsageLogger` (always sets TenantId), read primarily in `AggregateAiUsageJob` which uses `IgnoreQueryFilters()` by design (cross-tenant aggregation job). `GetCustomerUsageReportQueryHandler` also uses `IgnoreQueryFilters()` and is SuperUser-only. Lower read exposure in normal tenant flows.

### Gap D — `AiUsageSummary` (TenantEntity)

- **Config:** `AiUsageSummaryConfiguration.cs` line 66 has `builder.HasQueryFilter(e => !e.IsDeleted)` — soft-delete only
- **ApplicationDbContext:** No HasQueryFilter call
- **Same pattern as Gap C**

**Why these four were missed:** The original 12 entities were those that previously had a `HasQueryFilter` in their entity configuration that was being removed and centralised. These four either (a) had no `HasQueryFilter` at all before the fix and so weren't on the radar, or (b) were added slightly before or around the same time and fell through the gap. They are not mentioned in the commit message or CLAUDE.md Note 14.

---

## 5. New Gaps Introduced Since Closure

A thorough search of all commits after `bda907a` finds **no new TenantEntity gaps**. The three entity additions after the fix (`WorkflowEvent`, `WorkflowReview`, `ExternalParticipantInvitation`, `TranslationFlag`, `BulkImportSession`) all received correct HasQueryFilter entries in ApplicationDbContext at the time of their introduction.

One BACKLOG entry (item B5, added post-fix) notes that `ToolboxTalkSlideshowTranslation` and `ToolboxTalkSlideTranslation` lack `HasQueryFilter(!IsDeleted)` — but both are `BaseEntity` (no TenantId), so this is a soft-delete consistency issue only, not a cross-tenant issue.

---

## 6. Demo-Risk Assessment

The Friday demo involves: homecare tenant creation, sector assignment, regulatory requirements browse, Training Evidence Pack generation, content upload + parse, operator invite/login, and schedule creation.

**Step 1 — Admin creates homecare tenant, assigns sector**
- `TenantSector` is covered by HasQueryFilter (line 325 ApplicationDbContext). `Sector` is BaseEntity (no TenantId needed). No cross-tenant risk.

**Step 2 — Admin invites operator user**
- User creation, Employee creation — Core entities, all covered. No risk.

**Step 3 — Admin views regulatory requirements for their sector**
- `RegulatoryBrowseController.Browse()` passes `tenantId` explicitly to `GetBrowsableRequirementsAsync`. `RegulatoryRequirementMapping` is covered by HasQueryFilter (line 326). `RegulatoryBody`, `RegulatoryProfile`, `RegulatoryRequirement` are BaseEntity (system-managed). `GetApplicability()` uses `IgnoreQueryFilters()` + explicit `!IsDeleted` guard correctly (RegulatoryRequirements are BaseEntity anyway). No cross-tenant risk.

**Step 4 — Admin generates Training Evidence Pack (inspection readiness report)**
- `RequirementMappingService.GetComplianceChecklistAsync` explicitly scopes by `tenantId` on all queries (lines 154–203). No cross-tenant risk.

**Step 5 — Admin uploads source, parses content**
- `ParseToolboxTalkContentCommandHandler` queries with `t.TenantId == request.TenantId` (line 40). No cross-tenant risk. ContentCreationSession queries in `ContentCreationSessionService` always include `s.TenantId == tenantId` (line 1391). No cross-tenant risk.

**Step 6 — Operator logs in, sees assigned talk**
- `ScheduledTalk` is covered by HasQueryFilter (line 339). No cross-tenant risk.

**Step 7 — Admin creates schedule with recurring schedule**
- `ToolboxTalkSchedule` covered by HasQueryFilter (line 338). Schedule handlers include explicit `s.TenantId == request.TenantId` checks in all write paths. No cross-tenant risk.

**Summary:** The demo path touches only entities with correct query filters or with explicit service-layer TenantId predicates throughout. The four gap entities (CourseAssignment, TranslationDeviation, AiUsageLog, AiUsageSummary) are not in the critical demo path. **Demo risk: LOW.**

The one scenario that could surface the gap during the demo is if the demo instance has data from multiple tenants and a `ToolboxTalkCourseAssignment` query somehow bypassed its service-layer `TenantId` guard — but the demo is against a clean instance (Development environment from the `transval` branch), so there is only one tenant present.

---

## 7. Recommended Actions Before Friday

### Critical — None

No critical cross-tenant exposure is present in the demo path.

### High

**H1 — Add missing HasQueryFilter for four TenantEntity types**
Adds defence-in-depth for the four unguarded TenantEntity types. Not needed for the demo itself, but closes a real structural gap that will matter as the product scales to multiple production tenants.

- File: `src/Core/QuantumBuild.Core.Infrastructure/Data/ApplicationDbContext.cs`
- Lines: after line 347 (end of Toolbox Talks block), add:

```csharp
modelBuilder.Entity<ToolboxTalkCourseAssignment>().HasQueryFilter(e => !e.IsDeleted && (BypassTenantFilter || e.TenantId == TenantId));
modelBuilder.Entity<TranslationDeviation>().HasQueryFilter(e => !e.IsDeleted && (BypassTenantFilter || e.TenantId == TenantId));
modelBuilder.Entity<AiUsageLog>().HasQueryFilter(e => !e.IsDeleted && (BypassTenantFilter || e.TenantId == TenantId));
modelBuilder.Entity<AiUsageSummary>().HasQueryFilter(e => !e.IsDeleted && (BypassTenantFilter || e.TenantId == TenantId));
```

- Also remove the entity-config `HasQueryFilter` lines from: `ToolboxTalkCourseAssignmentConfiguration.cs:117`, `TranslationDeviationConfiguration.cs:104`, `AiUsageLogConfiguration.cs:70`, `AiUsageSummaryConfiguration.cs:66`
- Also update `AggregateAiUsageJob` to add `IgnoreQueryFilters()` on its AiUsageLog/AiUsageSummary queries (it already needs cross-tenant visibility)
- No migration needed (model-level change only)
- Effort: 30–45 minutes. Safe to do before Friday.

### Medium

**M1 — Update CLAUDE.md Note 14 with correct date and gap list**
Note 14 currently says the fix was landed but does not mention the four uncovered entity types. Add a follow-up note to prevent the gap from being re-discovered independently.

- File: `CLAUDE.md`
- Effort: 5 minutes.

**M2 — Verify `AggregateAiUsageJob` uses `IgnoreQueryFilters()`**
Once H1 adds the tenant filter to `AiUsageLog`, the aggregate job (which intentionally reads across all tenants) must call `.IgnoreQueryFilters()` on its queries, or it will silently aggregate only the current-tenant rows (which in a Hangfire context is `TenantId == Guid.Empty`).

- File: `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/AggregateAiUsageJob.cs`
- Lines 29 and 51 already call `.IgnoreQueryFilters()` — **this is already done**. Confirm these remain in place after H1 is applied.
- Effort: verification only, 5 minutes.

### Low

**L1 — Add soft-delete HasQueryFilter to `ToolboxTalkSlideshowTranslation` and `ToolboxTalkSlideTranslation`**
Tracked in BACKLOG.md as B5. Both are BaseEntity (no TenantId), so this is a soft-delete consistency gap only, not a security gap. No demo risk.

---

## Appendix — Entity Coverage Matrix (ToolboxTalks TenantEntities)

| Entity | HasQueryFilter in ApplicationDbContext | Entity Config filter | Gap? |
|---|---|---|---|
| ToolboxTalk | Line 336 — tenant + soft-delete | None (removed by `bda907a`) | No |
| ToolboxTalkCourse | Line 337 | None | No |
| ToolboxTalkSchedule | Line 338 | None | No |
| ScheduledTalk | Line 339 | None | No |
| ToolboxTalkTranslation | Line 340 | None | No |
| ToolboxTalkVideoTranslation | Line 341 | None | No |
| ToolboxTalkCertificate | Line 342 | None | No |
| SubtitleProcessingJob | Line 343 | None | No |
| ToolboxTalkSlide | Line 344 | None | No |
| TranslationValidationRun | Line 345 | None | No |
| ContentCreationSession | Line 346 | None | No |
| ValidationRegulatoryScore | Line 347 | None | No |
| ToolboxTalkCourseAssignment | **MISSING** | `!IsDeleted` only (line 117) | **YES** |
| TranslationDeviation | **MISSING** | `!IsDeleted` only (line 104) | **YES** |
| AiUsageLog | **MISSING** | `!IsDeleted` only (line 70) | **YES** |
| AiUsageSummary | **MISSING** | `!IsDeleted` only (line 66) | **YES** |
| TenantSector | Line 325 (Core block) | None | No |
| RegulatoryRequirementMapping | Line 326 | `!IsDeleted` only (line 97) — redundant | No |
| AuditCorpus | Line 327 | `!IsDeleted` only (line 54) — redundant | No |
| CorpusRun | Line 328 | `!IsDeleted` only (line 66) — redundant | No |
| QrLocation | Line 329 | None | No |
| QrCode | Line 330 | None | No |
| QrSession | Line 331 | None | No |
| WorkflowEvent | Line 364 | None | No |
| WorkflowReview | Line 365 | None | No |
| ExternalParticipantInvitation | Line 366 | None | No |
| TranslationFlag | Line 367 | None | No |
| BulkImportSession | Line 333 | None | No |
