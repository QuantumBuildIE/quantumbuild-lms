# Translation Scan Behaviour Recon

Read-only recon. No code was modified for this document.

## Deciding question

> Would learnings created by a future bulk SOP-to-learnings flow be auto-translated by the existing scheduled scan (`DailyTranslationScanJob`) **without** the bulk flow explicitly enqueuing a translation job?

**Answer: Conditionally yes, but not unconditionally.**

`DailyTranslationScanJob` is a genuine scan-and-fill job — it queries the database for talks with translation gaps rather than requiring pre-specified targets. But its scan is **time-windowed to the last 25 hours** (`CreatedAt`/`UpdatedAt`/`ContentGeneratedAt >= now - 25h`) and **requires `Status == Published`**. `MissingTranslationsJob`, by contrast, is pure explicit-target — every code path that invokes it passes a specific `toolboxTalkId`; it never discovers talks on its own.

**Practical implication for the bulk flow:** if bulk-created learnings are published and the scan's next 2am UTC run falls within 25 hours of that publish, they get swept automatically with zero code from the bulk flow. If a talk is created, not touched again, and the scan is delayed or fails past the 25h window (e.g. a Hangfire outage, or a talk left in Draft past that window), it falls out of the window permanently — there is no unbounded backward sweep and no automatic retry once the window has passed. The bulk flow's design must either (a) guarantee talks are Published within the window and accept the scan's daily cadence as the delivery mechanism, or (b) explicitly enqueue `MissingTranslationsJob` per created talk, sidestepping the window/status dependency entirely.

---

## 1. `DailyTranslationScanJob` — trigger, mechanism, selection logic

File: `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/DailyTranslationScanJob.cs`

### Trigger

Registered as a Hangfire recurring job in `src/QuantumBuild.API/Program.cs:474-477`:

```csharp
recurringJobManager.AddOrUpdate<DailyTranslationScanJob>(
    "daily-translation-scan",
    job => job.ExecuteAsync(CancellationToken.None),
    Cron.Daily(2, 0)); // 2am UTC daily
```

No `RecurringJobOptions { TimeZone = ... }` is passed (contrast with the reminder job registered immediately above it at `Program.cs:470-472`, which does pass an Ireland timezone). `Cron.Daily(2, 0)` therefore runs at **02:00 UTC, once per day, server-wide** (not per tenant).

### Class-level doc comment (`DailyTranslationScanJob.cs:10-15`)

```csharp
/// Daily Hangfire job that scans recently created/modified published talks for
/// translation gaps. Only checks talks touched in the last 25 hours (overlap
/// with 24-hour schedule to avoid gaps). Dispatches MissingTranslationsJob
/// per talk that has missing translations — does NOT translate directly.
```

The doc comment's claim was independently verified against the executable code below — it accurately describes the implementation.

### Execution flow

`ExecuteAsync` (`DailyTranslationScanJob.cs:36-58`):

```csharp
var cutoff = DateTime.UtcNow.AddHours(-25);
var tenants = await _tenantRepository.GetAllActiveAsync(cancellationToken);

foreach (var tenant in tenants)
{
    try
    {
        await ProcessTenantAsync(tenant.Id, cutoff, cancellationToken);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "DailyTranslationScanJob failed for tenant {TenantId}. Continuing to next tenant.", tenant.Id);
    }
}
```

Loops every active tenant (`ITenantRepository.GetAllActiveAsync`, no cap — see §6), with per-tenant isolation so one tenant's failure doesn't block others.

`ProcessTenantAsync` (`DailyTranslationScanJob.cs:60-128`) per tenant:

1. Resolves required languages from employee `PreferredLanguage` (lines 65-72 — quoted in full in §4).
2. Returns early if the tenant has no non-English preferred languages (lines 74-75).
3. **Selection query — the core scan-and-fill logic** (`DailyTranslationScanJob.cs:77-86`):

```csharp
// Find published talks created/modified/generated in the last 25 hours
var recentTalks = await _toolboxTalksDbContext.ToolboxTalks
    .IgnoreQueryFilters()
    .Where(t => t.TenantId == tenantId && !t.IsDeleted
        && t.Status == ToolboxTalkStatus.Published
        && (t.CreatedAt >= cutoff
            || t.UpdatedAt >= cutoff
            || (t.ContentGeneratedAt != null && t.ContentGeneratedAt >= cutoff)))
    .Select(t => new { t.Id, t.SourceLanguageCode })
    .ToListAsync(cancellationToken);
```

   This is an unambiguous **time-windowed** query. There is no other query anywhere in the file that reaches talks outside the `cutoff` (`now - 25h`) boundary — confirmed by reading the file in full (129 lines).

4. For each recent talk, computes `languagesNeeded` = required languages minus the talk's own `SourceLanguageCode` (lines 95-103).
5. Queries `ToolboxTalkTranslations` for that talk's `existingLanguageCodes` (lines 106-111).
6. `missingLanguages = languagesNeeded.Except(existingLanguageCodes, ...)` (lines 113-115).
7. If any language is missing, enqueues `MissingTranslationsJob` for that talk (lines 117-122) — the scan job itself never calls a translation provider or writes a translation record; it only decides *whether* to dispatch, deferring all translation-generation logic to `MissingTranslationsJob`.

```csharp
if (missingLanguages.Count > 0)
{
    BackgroundJob.Enqueue<MissingTranslationsJob>(
        job => job.ExecuteAsync(talk.Id, tenantId, null, CancellationToken.None));
    jobsQueued++;
}
```
(`DailyTranslationScanJob.cs:117-122`)

### Preconditions for a talk to be swept by this scan

Both conditions below are `AND`-ed in the single `Where` clause at `DailyTranslationScanJob.cs:80-84`:

- `Status == ToolboxTalkStatus.Published` (line 81) — **Draft talks are excluded from the scan entirely.**
- At least one of `CreatedAt`, `UpdatedAt`, or `ContentGeneratedAt` must be `>= cutoff` (now − 25h) (lines 82-84).

No other fields are checked. There is no `TargetLanguageCodes` field read anywhere in this file (confirmed by full-file read — no such property reference exists in `DailyTranslationScanJob.cs`).

---

## 2. `MissingTranslationsJob` — explicit-target, no scan of its own

File: `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/MissingTranslationsJob.cs` (413 lines, read in full)

### Signature — confirms explicit-target

```csharp
[AutomaticRetry(Attempts = 1)]
[Queue("content-generation")]
public async Task ExecuteAsync(
    Guid toolboxTalkId,
    Guid tenantId,
    string? connectionId,
    CancellationToken cancellationToken = default)
```
(`MissingTranslationsJob.cs:61-67`)

There is no overload and no code path in this file that queries the DB for *candidate* talks to process — it always operates on the single `toolboxTalkId` passed by its caller. Every call site found repo-wide (table below) passes an explicit talk ID; grep of all `MissingTranslationsJob` references across `src/` (24 matches) turned up no invocation without one.

### What it does, given a talk ID (its own internal completeness check — not a talk-discovery scan)

1. Loads the one talk by `Id`/`TenantId`/`!IsDeleted` — **no `Status` filter at all** (`MissingTranslationsJob.cs:76-80`):

```csharp
var talk = await _toolboxTalksDbContext.ToolboxTalks
    .IgnoreQueryFilters()
    .Where(t => t.Id == toolboxTalkId && t.TenantId == tenantId && !t.IsDeleted)
    .Select(t => new { t.SourceLanguageCode, t.RequiresQuiz, t.SlidesGenerated })
    .FirstOrDefaultAsync(cancellationToken);
```

   This means if a Draft talk's ID were passed to this job directly (which none of today's callers do, but nothing in this job prevents it), it would still be processed. Status gating exists only inside `DailyTranslationScanJob`'s selection query, not in `MissingTranslationsJob` itself, and none of the other explicit-enqueue call sites (§2 table) filter on `Status` before enqueuing either.

2. Loads existing `ToolboxTalkTranslations` records for the talk, selecting `LanguageCode, TranslatedTitle, TranslatedSections, TranslatedQuestions` (`MissingTranslationsJob.cs:87-91`).
3. Resolves required languages from employee `PreferredLanguage` (lines 99-106 — quoted in §4).
4. Per required language, checks completeness (`MissingTranslationsJob.cs:115-163`):
   - No translation record at all → missing (lines 120-124).
   - `TranslatedTitle` empty → incomplete (lines 128-135).
   - `TranslatedSections` empty or `"[]"` → incomplete (lines 137-144).
   - `RequiresQuiz == true` and `TranslatedQuestions` empty → incomplete (lines 146-153).
   - `RequiresQuiz == true` and `TranslatedQuestions == "[]"` → incomplete (lines 155-162).
5. If `SlidesGenerated == true`, separately checks `ToolboxTalkSlideshowTranslations` existence per required language (lines 166-186).
6. Dispatches `GenerateContentTranslationsCommand` via `ISender.Send` for whatever's missing/incomplete (lines 200-202, 274-283).
7. Independently triggers subtitle-translation gap filling via `ISubtitleProcessingOrchestrator.TranslateMissingLanguagesAsync` (lines 207-208, 342-343) — checks a separate subtitle-job data source, not `ToolboxTalkTranslation` rows.

No time-window restriction of any kind exists in this file — the completeness check applies to whichever single talk it's given, regardless of when that talk was created or last modified.

### All call sites (repo-wide grep of `MissingTranslationsJob`, `src/**/*.cs`)

| Caller | File:line | Trigger context |
|---|---|---|
| `DailyTranslationScanJob.ProcessTenantAsync` | `DailyTranslationScanJob.cs:119-120` | Nightly scan, per-talk gap found (§1) |
| `ToolboxTalksController` (content-reuse endpoint) | `ToolboxTalksController.cs:1161` | After `ReuseContentAsync` direct content reuse |
| `ToolboxTalksController` (smart-generate endpoint) | `ToolboxTalksController.cs:1502` | After full content reuse (no AI generation needed) in smart-generate flow |
| `EmployeeLanguageChangeHandler.HandleLanguageChangeAsync` | `EmployeeLanguageChangeHandler.cs:74` | When an employee's preferred language is new to the tenant, once per that employee's assigned talks |
| `TranslationJobScheduler.EnqueueMissingTranslationsJob` | `TranslationJobScheduler.cs:14` | Thin wrapper (Note 21 concrete-class-enqueue pattern); called by: |
| — `TranslationQueueService.QueueTranslationsForTalksAsync` | `TranslationQueueService.cs:96` | Lesson Parser module, once per newly-generated talk after a parsed lesson produces talks |
| `ContentGenerationJob` (slideshow-only path) | `ContentGenerationJob.cs:330` | After slideshow-only generation completes, to translate the new slideshow HTML into languages that already have a `ToolboxTalkTranslation` record |

Every one of these seven call sites passes an explicit `toolboxTalkId` — none scan or batch-discover talks to enqueue against.

---

## 3. Preconditions summary — state a bulk-created learning must be in

| Precondition | Required by `DailyTranslationScanJob` scan? | Required by `MissingTranslationsJob` itself? |
|---|---|---|
| `Status == Published` | **Yes** (`DailyTranslationScanJob.cs:81`) | No — no `Status` filter at all (`MissingTranslationsJob.cs:78`) |
| Touched (`CreatedAt`/`UpdatedAt`/`ContentGeneratedAt`) within last 25h | **Yes** (`DailyTranslationScanJob.cs:82-84`) | N/A — not a scan, operates on the ID it's given |
| `TargetLanguageCodes` set on the talk | **No** — field is never read by either job (confirmed by full-file reads of both; no reference found) | **No** |
| Tenant has employees with non-`en`/non-source preferred languages | **Yes** — tenant skipped entirely otherwise (`DailyTranslationScanJob.cs:74-75`) | Job still runs but `languagesNeedingTranslation` ends up empty, so it's effectively a no-op |

Note on `TargetLanguageCodes`: per `docs/bulk-translation-job-recon.md`, this field belongs to the *new-wizard* per-talk explicit language selection path (`StartTalkTranslationCommandHandler`), a separate mechanism entirely from the two jobs analyzed here. Neither `DailyTranslationScanJob` nor `MissingTranslationsJob` reads it — both derive their required-language set purely from tenant employee preferences (§4).

---

## 4. Language scoping — confirmed: tenant employee languages only, never the full supported-language list

`DailyTranslationScanJob.cs:65-72`:

```csharp
// Get all required languages from employee preferences (excluding "en")
var requiredLanguageCodes = await _coreDbContext.Employees
    .IgnoreQueryFilters()
    .Where(e => e.TenantId == tenantId && !e.IsDeleted
        && e.PreferredLanguage != null && e.PreferredLanguage != "en")
    .Select(e => e.PreferredLanguage!)
    .Distinct()
    .ToListAsync(cancellationToken);
```

`MissingTranslationsJob.cs:99-106`:

```csharp
// Get all required languages from employee preferences (excluding source language)
var requiredLanguageCodes = await _coreDbContext.Employees
    .IgnoreQueryFilters()
    .Where(e => e.TenantId == tenantId && !e.IsDeleted
        && e.PreferredLanguage != null && e.PreferredLanguage != sourceLanguageCode)
    .Select(e => e.PreferredLanguage!)
    .Distinct()
    .ToListAsync(cancellationToken);
```

Both queries target `Employees` (`ICoreDbContext`), filtered to the tenant, not-deleted, non-null `PreferredLanguage`, excluding English (scan job) or the talk's own source language (missing-translations job), and `Distinct()`. Confirmed: the required-language set is strictly the tenant's actual employee preferred-language footprint, never the full ~33-language catalogue.

---

## 5. Relationship between the two jobs

`DailyTranslationScanJob` is a thin dispatcher: its only side effect on a translation gap is enqueuing `MissingTranslationsJob` (`DailyTranslationScanJob.cs:119-120`). All translation-generation logic (the completeness check, the `GenerateContentTranslationsCommand` dispatch, the subtitle-translation gap fill) lives exclusively in `MissingTranslationsJob`. There is no logic duplication between the two — they are complementary, not redundant: the scan job answers "which recently-touched published talks might have gaps" (coarse, cheap language-code-existence check against `ToolboxTalkTranslations`), and the missing-translations job answers "what exactly is incomplete for this one talk" (fine-grained, field-level completeness check) before generating.

---

## 6. Per-run caps / throttling / batch limits

**None found in either job.**

- `DailyTranslationScanJob.ExecuteAsync` iterates **all** active tenants returned by `ITenantRepository.GetAllActiveAsync` — no `Take(N)`, no paging (`DailyTranslationScanJob.cs:41-55`).
- `ProcessTenantAsync`'s `recentTalks` query has no `Take(N)` — every published talk touched in the last 25h for a tenant is processed in the same run (`DailyTranslationScanJob.cs:78-86`).
- `MissingTranslationsJob` processes every language in `requiredLanguageCodes` for its one talk — no cap on language count (`MissingTranslationsJob.cs:115-163`).
- Grep of `Take(`, `MaxDegreeOfParallelism`, `Throttle`, `BatchSize` across `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/` returned one hit unrelated to either job (`CorpusRunJob.cs:95`, `allActive.Take(5)`).
- The only throttling present is Hangfire's own per-queue worker concurrency for the `"content-generation"` queue (`[Queue("content-generation")]`, `MissingTranslationsJob.cs:62`) — an infrastructure-level concurrency limit, not an application-level cap on talks-per-run or tenants-per-run.

**Implication for a 24-learning bulk batch:** if all 24 are Published within the 25h scan window, the next 2am UTC run enqueues up to 24 `MissingTranslationsJob` instances (one per talk with a gap) in a single pass, with no batching/delay between them beyond Hangfire's queue worker concurrency. Each `MissingTranslationsJob` execution itself calls `GenerateContentTranslationsCommand` for every missing language on its talk — for N talks × M tenant languages, that's up to N×M translation-generation calls dispatched in the same run, gated only by however many workers are configured to drain the `content-generation` queue. There is no code-level spreading of this load across the off-peak window; it is however-fast-Hangfire-workers-can-drain-the-queue, not a deliberately paced rollout.

---

## Files read in full

- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/DailyTranslationScanJob.cs` (129 lines)
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/MissingTranslationsJob.cs` (413 lines)
- `src/QuantumBuild.API/Program.cs` (job registration region, lines ~145-493)

## Files read in part (call-site verification)

- `src/QuantumBuild.API/Controllers/ToolboxTalksController.cs` (lines ~1140-1178, ~1480-1508)
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/EmployeeLanguageChangeHandler.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Translations/TranslationJobScheduler.cs`
- `src/Modules/LessonParser/QuantumBuild.Modules.LessonParser.Infrastructure/Services/TranslationQueueService.cs`
- `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/ContentGenerationJob.cs` (lines ~300-340)

All line numbers verified directly against current file contents at time of writing (2026-08-02); not taken solely from subagent report.
