# Schedule Job Tenant Context — Recon

Read-only recon. No production code changed. All claims are `file:line` against
the current `transval` branch working tree. Section A.3's "confirmed empirically"
claim is not a fresh runtime test performed in this recon — it quotes an existing,
already-committed empirical confirmation (commit `4c896aa`,
`RecurringScheduleRefreshCharacterisationTests.cs`) that ran the real job through
the real Hangfire-style invocation path and observed the failure directly. No
throwaway test file was created or needed for this recon; where a claim is static
analysis only, it is flagged as such.

## Headline

**The cron is broken today, but not for the reason the task brief's background
paragraph implies.** `ProcessToolboxTalkSchedulesJob`'s own due-schedule
*selection* query is correctly tenant-scoped (`IgnoreQueryFilters()` + explicit
per-tenant `Where`, iterating `ITenantRepository.GetAllActiveAsync()`) — this part
works and finds the right schedules for every tenant. The break is one level
deeper: for every schedule it finds, it dispatches
`ProcessToolboxTalkScheduleCommand` through `IMediator.Send(...)`, and the
handler behind that command — `ProcessToolboxTalkScheduleCommandHandler` — was
written assuming the ambient EF Core tenant query filter is already correctly
set (true on the HTTP "Process Now" path, false on the Hangfire cron path). The
handler's own schedule lookup carries no `IgnoreQueryFilters()` of its own, so
under the cron's real invocation conditions (no `HttpContext`,
`IJobTenantContextAccessor.TenantId` never set) the ambient filter collapses to
`TenantId == Guid.Empty`, the lookup matches nothing, and the handler throws
`InvalidOperationException("Schedule with ID '...' not found.")` for every single
schedule, in every tenant, every day. The job's outer per-schedule `try/catch`
swallows this into a log line and moves to the next schedule — no alert, no
error surfaced anywhere an operator would see.

**Net effect: `ProcessToolboxTalkSchedulesJob` has never successfully created a
single `ScheduledTalk` via the cron path since tenant filtering was centralised
(CLAUDE.md Note 14).** Recurring schedules are entirely dependent on admins
manually clicking "Process Now" — which does work, because that path runs with a
real `HttpContext` and therefore a correctly-resolved ambient tenant.

---

## A. How the schedule cron actually runs in production

### A.1 — Registration, invocation, and whether anything sets tenant context

Registered as a Hangfire recurring job in `Program.cs:474-478`:

```csharp
recurringJobManager.AddOrUpdate<ProcessToolboxTalkSchedulesJob>(
    "process-toolbox-talk-schedules",
    job => job.ExecuteAsync(CancellationToken.None),
    "30 6 * * *", // Run at 6:30 AM daily
    new RecurringJobOptions { TimeZone = irelandTimeZone });
```

Enqueued via the concrete class (correctly following Note 21), daily at 6:30 AM
Ireland time. The job class itself is registered `AddScoped<ProcessToolboxTalkSchedulesJob>()`
([Program.cs:145](../src/QuantumBuild.API/Program.cs#L145)) — Hangfire's DI
activator resolves the job and all of its constructor dependencies from **one**
scope for the entire `ExecuteAsync` call.

`ProcessToolboxTalkSchedulesJob`'s constructor
([ProcessToolboxTalkSchedulesJob.cs:23-33](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/ProcessToolboxTalkSchedulesJob.cs#L23-L33))
injects `IMediator`, `IToolboxTalksDbContext`, `ITenantRepository`, `ILogger<...>`
— **no `IJobTenantContextAccessor` anywhere**. Grepping the full file (120 lines,
read in full) confirms zero references to `IJobTenantContextAccessor` or any
other tenant-context-setting mechanism. Nothing sets tenant context before, during,
or after this job runs.

### A.2 — The chain: how a tenant-scoped query normally resolves "current tenant"

Three-hop chain, all confirmed by direct reading:

1. **`ApplicationDbContext.TenantId`** ([ApplicationDbContext.cs:36](../src/Core/QuantumBuild.Core.Infrastructure/Data/ApplicationDbContext.cs#L36)):
   ```csharp
   public Guid TenantId => _currentUserService?.TenantId ?? Guid.Empty;
   ```
2. **`ApplicationDbContext.BypassTenantFilter`** ([ApplicationDbContext.cs:41](../src/Core/QuantumBuild.Core.Infrastructure/Data/ApplicationDbContext.cs#L41)):
   ```csharp
   public bool BypassTenantFilter => _currentUserService?.IsSuperUser == true && TenantId == Guid.Empty;
   ```
3. **`CurrentUserService.TenantId`** ([CurrentUserService.cs:128-151](../src/Core/QuantumBuild.Core.Infrastructure/Services/CurrentUserService.cs#L128-L151)):
   ```csharp
   public Guid TenantId
   {
       get
       {
           if (_jobTenantContextAccessor.TenantId is { } jobTenantId)
               return jobTenantId;

           if (IsSuperUser) { ... reads X-Tenant-Id header, or Guid.Empty ... }

           var tenantIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirstValue("tenant_id");
           if (Guid.TryParse(tenantIdClaim, out var tenantId))
               return tenantId;

           return Guid.Empty;
       }
   }
   ```
   `CurrentUserService.IsSuperUser` ([CurrentUserService.cs:68-75](../src/Core/QuantumBuild.Core.Infrastructure/Services/CurrentUserService.cs#L68-L75)) reads a claim off `_httpContextAccessor.HttpContext?.User` — with `HttpContext == null` (Hangfire), this is `false`, not an exception.

Every `TenantEntity`-derived DbSet used by the ToolboxTalks module (including
`ToolboxTalkSchedule`) carries a centralised global query filter in
`ApplicationDbContext.OnModelCreating` (per CLAUDE.md Note 14). The specific line
for the schedule entity:

```csharp
// ApplicationDbContext.cs:360
modelBuilder.Entity<ToolboxTalkSchedule>().HasQueryFilter(e => !e.IsDeleted && (BypassTenantFilter || e.TenantId == TenantId));
```

### A.3 — What actually happens with no HttpContext (confirmed, not just reasoned)

Walking the chain with `HttpContext == null` and `IJobTenantContextAccessor.TenantId`
never set:

- `_jobTenantContextAccessor.TenantId` is `null` (default, never assigned) →
  falls through.
- `IsSuperUser` → `false` (no HttpContext to read the claim from) → falls through
  to the JWT-claim branch.
- `_httpContextAccessor.HttpContext?.User?.FindFirstValue("tenant_id")` → `HttpContext`
  is `null`, so this whole expression short-circuits to `null` via the `?.`
  operators → `Guid.TryParse(null, ...)` fails → **returns `Guid.Empty`**.
- `ApplicationDbContext.TenantId` → `Guid.Empty`.
- `ApplicationDbContext.BypassTenantFilter` → `_currentUserService.IsSuperUser == true` is
  `false`, so the whole expression is `false` regardless of the `TenantId == Guid.Empty`
  half.
- The global filter predicate for `ToolboxTalkSchedule` (and every other centrally-filtered
  entity) collapses to: `!e.IsDeleted && (false || e.TenantId == Guid.Empty)` →
  effectively `!e.IsDeleted && e.TenantId == Guid.Empty`.

No real tenant ever has `TenantId == Guid.Empty`, so **any query against a
centrally-filtered DbSet that does not call `.IgnoreQueryFilters()` returns zero
rows** when run from inside this job, regardless of what explicit `.Where(...)`
clauses are also present on that query. `CurrentUserService.TenantId` never
throws and never returns `null` in this state — it silently resolves to
`Guid.Empty`, which is the most dangerous failure mode possible here (a query
that "succeeds" with an empty result set, rather than an exception that would
surface in logs/monitoring as a clear failure).

**This is confirmed empirically, not just reasoned from code**, by an
already-committed test. `RecurringScheduleRefreshCharacterisationTests.cs`
(commit `4c896aa`, "Add characterisation tests for recurring schedule processing
defects") contains this class-level doc comment
([RecurringScheduleRefreshCharacterisationTests.cs:39-50](../tests/QuantumBuild.Tests.Integration/ToolboxTalks/RecurringScheduleRefreshCharacterisationTests.cs#L39-L50)):

> "SEPARATE PRE-EXISTING GAP (found while writing the last test, not one of the
> two defects above, and NOT fixed here): `ProcessToolboxTalkSchedulesJob` never
> sets `IJobTenantContextAccessor.TenantId` before calling `_mediator.Send(...)`,
> unlike e.g. `BulkSopImportJob.cs:206`. Without an HttpContext (exactly how
> Hangfire's cron invokes it), `ICurrentUserService.TenantId` falls back to
> `Guid.Empty`, so `ApplicationDbContext`'s global tenant query filter makes
> `ProcessToolboxTalkScheduleCommandHandler`'s schedule lookup match nothing —
> every schedule, in every tenant, throws 'not found' (caught per-schedule and
> merely logged). **Confirmed empirically: the real job cannot successfully
> process ANY schedule when invoked exactly as Hangfire would.**"

The test itself
([RecurringScheduleRefreshCharacterisationTests.cs:271-289](../tests/QuantumBuild.Tests.Integration/ToolboxTalks/RecurringScheduleRefreshCharacterisationTests.cs#L271-L289))
runs the actual `ProcessToolboxTalkSchedulesJob.ExecuteAsync()` (constructed via
`ActivatorUtilities.CreateInstance`, mirroring Hangfire's own activator) against
a real Testcontainers Postgres instance, and had to **manually pre-set**
`IJobTenantContextAccessor.TenantId` on the job's own DI scope purely so the test
could isolate and observe the *other* defect (the due-date filter bug) — its own
comment states this was a workaround for "a separate, pre-existing gap" and that
without the workaround the job "cannot successfully process ANY schedule."

**Direct answer: recurring schedules are NOT being cron-processed in production
today.** Every schedule the job finds as "due" is immediately lost to a
swallowed `InvalidOperationException` the moment it's handed to the shared command
handler. The only way a recurring schedule has ever actually produced a
`ScheduledTalk` in this codebase's current state is via an admin manually
clicking "Process Now" (§C).

### A.4 — Comparison to `BulkSopImportJob`

`BulkSopImportJob` **is** a job that correctly sets `IJobTenantContextAccessor.TenantId`
— but it solves a structurally different problem, and its pattern cannot be
copied verbatim.

- **`BulkSopImportJob` is single-tenant per invocation.** It's enqueued with one
  `sessionId` ([`IBulkSopImportJob.ExecuteAsync(Guid sessionId, ...)`](../src/Core/QuantumBuild.Core.Application/Abstractions/IBulkSopImportJob.cs)),
  loads that one session with `IgnoreQueryFilters()`
  ([BulkSopImportJob.cs:69-71](../src/Core/QuantumBuild.Core.Infrastructure/Jobs/BulkSopImportJob.cs#L69-L71)),
  and reads `tenantId = session.TenantId` **once**
  ([BulkSopImportJob.cs:87](../src/Core/QuantumBuild.Core.Infrastructure/Jobs/BulkSopImportJob.cs#L87)).
  Every unit of work inside the job (one PDF at a time) belongs to that same
  single tenant.
- For each PDF, it creates a **brand new DI scope**
  (`_scopeFactory.CreateAsyncScope()`,
  [BulkSopImportJob.cs:194](../src/Core/QuantumBuild.Core.Infrastructure/Jobs/BulkSopImportJob.cs#L194))
  and, inside that fresh scope, sets
  `itemScope.ServiceProvider.GetRequiredService<IJobTenantContextAccessor>().TenantId = tenantId;`
  ([BulkSopImportJob.cs:206](../src/Core/QuantumBuild.Core.Infrastructure/Jobs/BulkSopImportJob.cs#L206))
  **before** resolving `ISender`/`IToolboxTalksDbContext` from that scope. The
  inline comment at 196-205 explicitly documents why: so the ambient EF filter
  resolves correctly for the shared wizard command handlers (`InitialiseToolboxTalkCommand`,
  `ParseToolboxTalkContentCommand`, `GenerateToolboxTalkQuizCommand`) "without
  changing" them.
- The per-item fresh scope is **also** motivated by Note 23 (DbContext change-tracker
  isolation between independent units of work) — the same scope creation serves
  both purposes at once (fresh tenant-context instance + fresh DbContext instance),
  which is precisely why "just set the tenant context" reads like a one-liner in
  `BulkSopImportJob` but isn't automatically transferable.

- **`ProcessToolboxTalkSchedulesJob` is genuinely cross-tenant within one execution**
  — it loops `foreach (var tenant in tenants)` inside a single `ExecuteAsync` call,
  and for each tenant, loops over that tenant's due schedules
  ([ProcessToolboxTalkSchedulesJob.cs:51-113](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/ProcessToolboxTalkSchedulesJob.cs#L51-L113)).
  It never creates a new DI scope anywhere in its own body — `_mediator`,
  `_dbContext`, `_tenantRepository` are all resolved once, from the single
  outer Hangfire-provided scope, and reused for every tenant and every schedule
  in the run.

This is the structural mismatch the task brief anticipated: `BulkSopImportJob`'s
"set tenant, then do the one tenant's work" pattern maps cleanly onto a
single-tenant job. `ProcessToolboxTalkSchedulesJob` would need to either (a) copy
the fresh-scope-per-unit pattern (per-tenant, or per-schedule) so each unit gets
its own `IJobTenantContextAccessor` instance and its own DbContext, or (b) avoid
the shared-handler dependency on ambient context entirely, mirroring how every
*other* cross-tenant job in this codebase already behaves (§B). Neither option is
chosen here — see §B and §D for what's actually available.

---

## B. Cross-tenant scoping design crux

### B.1 — Where the gap actually bites: selection vs per-item processing

**The gap bites at per-schedule processing (inside the shared command handler),
not at the job's own due-schedule selection.** These are two separate queries
with two different outcomes:

1. **Selection — `ProcessToolboxTalkSchedulesJob.cs:57-63`** — correctly scoped,
   works fine:
   ```csharp
   // NOTE: We use IgnoreQueryFilters() because this runs in a Hangfire background job
   // where ICurrentUserService.TenantId is not set. We explicitly filter by TenantId and IsDeleted.
   var schedulesToProcess = await _dbContext.ToolboxTalkSchedules
       .IgnoreQueryFilters()
       .Where(s => s.TenantId == tenant.Id && !s.IsDeleted)
       .Where(s => s.Status == ToolboxTalkScheduleStatus.Active)
       .Where(s => s.ScheduledDate.Date <= today ||
                  (s.NextRunDate.HasValue && s.NextRunDate.Value.Date <= today))
       .ToListAsync(cancellationToken);
   ```
   This query explicitly bypasses the ambient filter and substitutes an explicit
   `tenant.Id` (sourced from the outer `foreach (var tenant in tenants)` loop,
   itself populated by `ITenantRepository.GetAllActiveAsync()` — `Tenant` is not
   a `TenantEntity` and carries no tenant filter of its own, so this call
   succeeds regardless of ambient context). **This query does find the right due
   schedules, correctly scoped, for every tenant, every day.** The pre-existing
   `recurring-refresh-reachability-recon.md` correctly documents this query's
   *due-date* logic defect (Chunk 2's target — see §C) but that recon's own test
   harness only ever drove the schedule lifecycle through the HTTP `/process`
   endpoint (correct ambient tenant, because HTTP), so it never exercised this
   job's `ExecuteAsync` in a no-HttpContext context and therefore never surfaced
   this tenant-context gap — it was only found afterward, while writing a test
   that specifically had to invoke the job class directly.

2. **Per-schedule processing — `ProcessToolboxTalkScheduleCommandHandler.cs:39-43`**
   — this is where it breaks:
   ```csharp
   var schedule = await _dbContext.ToolboxTalkSchedules
       .Include(s => s.Assignments)
       .Include(s => s.ToolboxTalk)
           .ThenInclude(t => t.Sections)
       .FirstOrDefaultAsync(s => s.Id == request.ScheduleId && s.TenantId == request.TenantId, cancellationToken);

   if (schedule == null)
   {
       throw new InvalidOperationException($"Schedule with ID '{request.ScheduleId}' not found.");
   }
   ```
   **No `.IgnoreQueryFilters()` here.** `request.TenantId` is set correctly by the
   job (`command.TenantId = tenant.Id`,
   [ProcessToolboxTalkSchedulesJob.cs:77](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/ProcessToolboxTalkSchedulesJob.cs#L77))
   — the explicit `s.TenantId == request.TenantId` half of the predicate is fine.
   But the **ambient** global filter is ANDed on top of this automatically by EF
   Core (it isn't bypassed), and per §A.3 that ambient filter is `e.TenantId ==
   Guid.Empty` inside this job. `request.TenantId` is always a real tenant GUID,
   never `Guid.Empty`, so the combined predicate `(request-tenant-match) AND
   (ambient Guid.Empty match)` can never be satisfied. `FirstOrDefaultAsync`
   returns `null`, and the very next line throws.

   The job's own per-schedule `try/catch`
   ([ProcessToolboxTalkSchedulesJob.cs:73-101](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/ProcessToolboxTalkSchedulesJob.cs#L73-L101))
   catches this exception, logs it via `_logger.LogError`, increments `errorCount`,
   and moves on to the next schedule — so this fails **silently** from any
   operational-visibility standpoint short of reading application logs directly;
   there is no alert, no dashboard flag, no distinguishing marker between "this
   schedule genuinely errored" and "this schedule was never actually looked up."

Every other query the handler runs after this point (`ToolboxTalkSettings`,
`Employees`, the `ScheduledTalks.Add(...)` write itself) is unreachable in the
cron path because execution never gets past line 47 — the schedule lookup is
the single point of failure for the entire per-schedule processing step.

### B.2 — Existing precedent for cross-tenant job scoping in this codebase

Every other job in this codebase that iterates all tenants in one run solves
tenant scoping by doing **all of its own database work directly against the
DbContext, with `.IgnoreQueryFilters()` plus an explicit `TenantId ==` clause at
every single query site** — none of them delegate any part of their per-tenant
work to a MediatR command/handler that assumes ambient tenant context. This is
the "self-contained job" pattern, and it's the dominant pattern in this codebase
(6 of 7 comparable jobs use it):

| Job | Iterates tenants? | Pattern |
|---|---|---|
| `ProcessToolboxTalkSchedulesJob` | Yes (`GetAllActiveAsync`) | **Mixed** — self-contained for selection ([:57-63](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/ProcessToolboxTalkSchedulesJob.cs#L57-L63)), but hands off to a shared MediatR handler for the per-item write, and that handler is NOT self-contained (§B.1) |
| `DailyTranslationScanJob` | Yes ([:41](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/DailyTranslationScanJob.cs#L41)) | Fully self-contained — every query (`Employees`, `ToolboxTalks`, `ToolboxTalkTranslations`) uses `.IgnoreQueryFilters()` + explicit `t.TenantId == tenantId` ([:67-68,80,108](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/DailyTranslationScanJob.cs#L67-L68)). Dispatches `MissingTranslationsJob` via `BackgroundJob.Enqueue` (a brand-new job invocation with its own fresh scope), never a same-scope MediatR `Send`. |
| `SendRefresherRemindersJob` | Yes ([:47](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/SendRefresherRemindersJob.cs#L47)) | Fully self-contained — every query on `ScheduledTalks`/`ToolboxTalkCourseAssignments` uses `.IgnoreQueryFilters()` + explicit `st.TenantId == tenant.Id` ([:54-58, 86-90, 117-121, 148-152](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/SendRefresherRemindersJob.cs#L54)). No MediatR handoff at all — direct entity mutation + `SaveChangesAsync` inside the job. |
| `SendToolboxTalkRemindersJob` | Yes ([:52](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/SendToolboxTalkRemindersJob.cs#L52)) | Same self-contained pattern as `SendRefresherRemindersJob` (not fully re-read in this recon, but confirmed same `GetAllActiveAsync` + per-tenant loop signature). |
| `UpdateOverdueToolboxTalksJob` | No — single cross-tenant bulk op | `.IgnoreQueryFilters()` with **no** tenant `Where` at all (deliberately — "this job processes all tenants, so we explicitly filter by `IsDeleted` only", [UpdateOverdueToolboxTalksJob.cs:39-44](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/UpdateOverdueToolboxTalksJob.cs#L39-L44)), one `ExecuteUpdateAsync` covering every tenant in a single SQL statement. No MediatR, no per-tenant loop needed because the operation (flip status if overdue) has no tenant-specific side effects. |
| `ExpiredSessionCleanupJob` | No — single cross-tenant bulk op | Same shape as `UpdateOverdueToolboxTalksJob`: `.IgnoreQueryFilters()`, no tenant `Where`, one query covering all tenants, `session.TenantId` (from the already-loaded row) passed explicitly to `IR2StorageService.DeleteSessionFilesAsync` per row ([ExpiredSessionCleanupJob.cs:30-49](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/ExpiredSessionCleanupJob.cs#L30-L49)). No MediatR handoff. |
| `CorpusRunJob` | No — takes `tenantId` as an explicit method parameter (single-tenant per invocation, like `BulkSopImportJob`) | `.IgnoreQueryFilters()` + explicit `r.TenantId == tenantId` at every query site ([CorpusRunJob.cs:66-69, 115-119, 259, 388-389, 441-442, 524-529](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/CorpusRunJob.cs)). No MediatR handoff; CLAUDE.md Note "Phase 4 (Corpus)" independently documents this pattern. |

**Two distinct precedent shapes exist, and `ProcessToolboxTalkSchedulesJob` currently matches neither cleanly:**

- **Shape 1 — "self-contained, no ambient context needed at all"** (5 of 7 jobs
  above): every query the job needs is issued directly by the job (or by a
  plain service it calls that also takes an explicit `tenantId` parameter and
  does its own `.IgnoreQueryFilters()`), never through a shared MediatR handler
  that was written for the HTTP request pipeline. `IJobTenantContextAccessor` is
  never touched because nothing in the call graph relies on the ambient EF
  filter.
- **Shape 2 — "single-tenant unit of work, set the accessor once per fresh scope,
  then reuse existing HTTP-oriented handlers unmodified"** (`BulkSopImportJob`,
  and structurally `CorpusRunJob` for its own direct-query needs even though it
  doesn't use the accessor): the job already knows which one tenant a given unit
  of work belongs to before doing anything, so the accessor only ever needs one
  value per scope.

`ProcessToolboxTalkSchedulesJob` doesn't fit Shape 1 as written today, because it
delegates the actual write (creating `ScheduledTalk` rows, advancing
`NextRunDate`, the refresh logic) to `ProcessToolboxTalkScheduleCommandHandler`
— a handler that is also the exact same code path the HTTP "Process Now" button
uses (§C), and which itself contains substantial, non-trivial logic (refresh
gate, recurrence calculation, section-progress creation, email dispatch) that a
Shape-1 rewrite would either have to duplicate outside MediatR or leave as-is and
wrap differently. It doesn't fit Shape 2 cleanly either, because — unlike
`BulkSopImportJob`'s one-`sessionId`-one-tenant shape — a single job execution
here spans **every** active tenant and **every** due schedule within each, so
"set it once per scope" would need to mean "once per tenant" or "once per
schedule," and the job as written today reuses one single outer scope (and,
notably, one single shared `IToolboxTalksDbContext` / change tracker) across
the *entire* run rather than creating a fresh scope per tenant or per schedule
the way `BulkSopImportJob` creates a fresh scope per PDF.

### B.3 — A related, not-in-scope observation: shared DbContext across the whole run

Independent of the tenant-context gap, `ProcessToolboxTalkSchedulesJob` reuses
**one** `IToolboxTalksDbContext` instance (injected once via constructor,
[ProcessToolboxTalkSchedulesJob.cs:19,25,30](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/ProcessToolboxTalkSchedulesJob.cs#L19))
across every tenant and every schedule in a single run — including every
`SaveChangesAsync()` call made inside `ProcessToolboxTalkScheduleCommandHandler.Handle`
([:204](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs#L204)),
since MediatR resolves the handler (and its `IToolboxTalksDbContext` dependency)
from the same ambient scope the job itself was resolved from. This is exactly
the shared-change-tracker risk CLAUDE.md Note 23 documents for
`BulkSopImportJob`'s original design ("a failed `SaveChangesAsync` leaves the
bad entity in the tracker in `Added` state, so every subsequent save ... re-fails").
Here the blast radius would be worse than a single session: a poisoned change
tracker from one tenant's failed schedule save could, in principle, cause
`SaveChangesAsync` to fail for every subsequent schedule and tenant processed
later in the same run. This was not something this recon was asked to assess in
depth or confirm empirically, and it is **not** part of the tenant-context gap
itself — flagging it only because any fix that adds per-tenant or per-schedule
scoping (à la `BulkSopImportJob`'s fresh-scope-per-item pattern) would likely
also resolve this side-effect for free, which is relevant context for deciding
between the options in §D of the (not-yet-written) fix design.

---

## C. Interaction with the planned Chunk 2 recurring-refresh fix

### C.1 — Do the Chunk 2 defects manifest via cron, button, or both?

**Both defects documented in `recurring-refresh-reachability-recon.md` and
characterised in `RecurringScheduleRefreshCharacterisationTests.cs` are
currently only reachable via the manual "Process Now" button — not via cron —
because the cron path cannot get past the tenant-context gap to reach the
handler code where those defects live.**

- **Defect 1 (refresh never fires — stale membership)** and **Defect 2 (reprocessed
  every run, not on cadence)** both live entirely inside
  `ProcessToolboxTalkScheduleCommandHandler.Handle` — the refresh gate
  ([:78-95](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs#L78-L95))
  and the end-of-run unconditional reset
  ([:195-200](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs#L195-L200)).
  Neither can be reached by the cron path today, because the cron path throws
  and exits at the schedule lookup ([:45-48](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs#L45-L48))
  before any of that logic executes.
- The characterisation tests added in `4c896aa` prove this by construction: three
  of the four tests
  (`CurrentBehaviour_ProcessCalledTwiceBeforeCadenceInterval_CreatesDuplicateScheduledTalks`,
  `CurrentBehaviour_NewDepartmentMemberAfterProcessing_IsNotPickedUpOnNextRun`,
  `CurrentBehaviour_EndOfRunReset_MarksAllAssignmentsUnprocessed_WhichIsWhyRefreshNeverFires`)
  drive the schedule lifecycle entirely through `AdminClient.PostAsync(".../process", null)`
  — the authenticated HTTP endpoint, i.e. the button path, which has a real
  `HttpContext` and therefore correctly-resolved ambient tenant context. The
  fourth test
  (`CurrentBehaviour_JobDueFilter_SelectsScheduleWithFutureNextRunDate_BecauseScheduledDateNeverAdvances`)
  is the one exception, and it is also the one that needed the manual
  `IJobTenantContextAccessor.TenantId` workaround
  ([:284-289](../tests/QuantumBuild.Tests.Integration/ToolboxTalks/RecurringScheduleRefreshCharacterisationTests.cs#L284-L289))
  precisely because, without it, the job's real behaviour would be "every
  schedule throws not-found" rather than "the due-filter selects a not-yet-due
  schedule" — the test had to manually simulate a correctly-wired job just to
  isolate and prove the *other*, due-filter-shaped defect in isolation.

**Practical consequence for sequencing:** the duplicate-`ScheduledTalk`/stale-membership
defects are **live in production today, but only reachable when an admin manually
clicks "Process Now" on a recurring schedule** — not on the daily 6:30 AM cron,
because the cron never successfully reaches the shared handler code at all. If no
admin has manually re-processed the same recurring schedule twice in quick
succession, Defect 2's duplicate-talk symptom may not yet have surfaced visibly in
any tenant, even though the underlying code defect is real and provably
reachable via a legitimate admin action.

### C.2 — What must not break

The manual "Process Now" HTTP path
([ToolboxTalkSchedulesController.cs:239-275](../src/QuantumBuild.API/Controllers/ToolboxTalkSchedulesController.cs#L239-L275))
is confirmed working correctly today and must not be disturbed by a
tenant-context fix:

```csharp
[HttpPost("{id:guid}/process")]
[Authorize(Policy = "Learnings.Schedule")]
public async Task<IActionResult> Process(Guid id)
{
    try
    {
        var command = new ProcessToolboxTalkScheduleCommand
        {
            TenantId = _currentUserService.TenantId,
            ScheduleId = id
        };

        var result = await _mediator.Send(command);
        ...
```
([:244-254](../src/QuantumBuild.API/Controllers/ToolboxTalkSchedulesController.cs#L244-L254))

Here, `_currentUserService.TenantId` is evaluated with a real `HttpContext`
present — `IJobTenantContextAccessor.TenantId` is still `null` (this code path
never touches it), `IsSuperUser` correctly reads the JWT claim, and (for a
non-SuperUser admin) the `tenant_id` JWT claim resolves to the real tenant GUID.
Both the explicit `request.TenantId == request.TenantId` predicate in the
handler's own `Where` clause *and* the ambient EF global filter agree on the
same, correct value — so the handler's schedule lookup at
[ProcessToolboxTalkScheduleCommandHandler.cs:39-43](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs#L39-L43)
succeeds normally on this path. **This is the only reason recurring schedules
have ever produced real `ScheduledTalk` rows in this codebase's current state.**
Any fix to the cron-side tenant-context gap must leave this HTTP path's
ambient-context resolution untouched — i.e. it must not, for example, force
`IJobTenantContextAccessor.TenantId` to always take precedence in a way that
could leak into a request-scoped `ICurrentUserService` resolution outside a
job context (not observed as a risk in the current code shape, since
`IJobTenantContextAccessor` is scoped and Hangfire jobs and HTTP requests never
share a DI scope, but worth stating explicitly as a fix constraint).

---

## D. Test coverage / verification surface

### D.1 — What exists today

| File | Covers |
|---|---|
| [RecurringScheduleRefreshCharacterisationTests.cs](../tests/QuantumBuild.Tests.Integration/ToolboxTalks/RecurringScheduleRefreshCharacterisationTests.cs) (commit `4c896aa`) | 3 of 4 tests drive the lifecycle entirely via the HTTP `/process` endpoint (button path) and characterise Defects 1 & 2 from `recurring-refresh-reachability-recon.md`. The 4th test (`CurrentBehaviour_JobDueFilter_...`) is the only one that invokes `ProcessToolboxTalkSchedulesJob.ExecuteAsync()` directly, and it must manually pre-set `IJobTenantContextAccessor.TenantId` to do so meaningfully — its class-level doc comment ([:39-50](../tests/QuantumBuild.Tests.Integration/ToolboxTalks/RecurringScheduleRefreshCharacterisationTests.cs#L39-L50)) is the existing, committed empirical confirmation of the tenant-context gap. |
| Everything else under `tests/` | Grep-confirmed: **no other test file references `ProcessToolboxTalkSchedulesJob` or `ProcessToolboxTalkScheduleCommandHandler` at all.** No unit test isolates the handler's schedule-lookup query. No test exercises `ProcessToolboxTalkSchedulesJob.ExecuteAsync()` *without* the manual accessor workaround — i.e. there is no test today that asserts the currently-broken "every schedule throws not-found" outcome as its own explicit expectation; it's only visible as a doc comment and as the reason the 4th test needs its workaround. |

### D.2 — Verifiability of a future fix

A future fix's characterisation surface is already half-built: the 4th test's
existing workaround
([:284-289](../tests/QuantumBuild.Tests.Integration/ToolboxTalks/RecurringScheduleRefreshCharacterisationTests.cs#L284-L289))
is effectively a preview of "what correct tenant-context wiring looks like from
the test's perspective" — a fix that makes that manual `jobScope.ServiceProvider.GetRequiredService<IJobTenantContextAccessor>().TenantId = ...`
line unnecessary (because the job now does the equivalent internally) would be
directly observable: remove the workaround line, re-run the same test, and the
`scheduledTalkCount` assertion should still resolve to a real value rather than
the job silently producing zero talks for every schedule. Beyond that specific
test, a permanent regression test could straightforwardly assert, without any
manual `IJobTenantContextAccessor` workaround:
(a) construct `ProcessToolboxTalkSchedulesJob` via `ActivatorUtilities` exactly as
the existing test does, (b) call `ExecuteAsync()` with a due schedule present and
zero pre-set tenant context (simulating the real cron), (c) assert a
`ScheduledTalk` row now exists for the expected employee — today this assertion
would fail (zero rows), post-fix it should pass.

---

## Definitive Answers

1. **Does the cron run mis-scoped in production today?** **Yes, confirmed.**
   The job's own due-schedule *selection* query is correctly tenant-scoped and
   works. But every schedule it finds is then lost: the shared
   `ProcessToolboxTalkScheduleCommandHandler`'s schedule lookup
   ([:39-43](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs#L39-L43))
   has no `.IgnoreQueryFilters()` and relies on ambient tenant context that the
   job never sets, so the ambient global filter collapses to
   `TenantId == Guid.Empty` (§A.3) and the lookup matches nothing for every
   schedule, every tenant, every day. Confirmed both by static tracing of the
   exact chain (`CurrentUserService.TenantId` → `ApplicationDbContext.TenantId`/`BypassTenantFilter`
   → the centralised `HasQueryFilter` predicate) and by an already-committed,
   already-run integration test whose doc comment states this outcome was
   "confirmed empirically." **Recurring schedules are not being cron-processed
   in production today — they only ever get processed when an admin manually
   clicks "Process Now."**

2. **Where exactly does the gap bite — selection or per-item processing?**
   **Per-item (per-schedule) processing, inside the shared command handler —
   not selection.** The job's own selection query
   ([ProcessToolboxTalkSchedulesJob.cs:57-63](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/ProcessToolboxTalkSchedulesJob.cs#L57-L63))
   already does the right thing (`IgnoreQueryFilters()` + explicit per-tenant
   `Where`). The gap is entirely inside
   `ProcessToolboxTalkScheduleCommandHandler.Handle`'s own schedule reload
   ([:39-43](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs#L39-L43)),
   which was written for (and still correctly serves) the HTTP "Process Now"
   path and was never adapted for the no-`HttpContext` cron path.

3. **Options for cross-tenant scoping given existing precedent (not chosen here):**
   - **Shape 1 — self-contained job, no ambient context ever consulted.** Used by
     `DailyTranslationScanJob`, `SendRefresherRemindersJob`, `SendToolboxTalkRemindersJob`,
     `UpdateOverdueToolboxTalksJob`, `ExpiredSessionCleanupJob` — every query the
     job needs is issued directly (or via a helper that itself takes an explicit
     `tenantId` and does its own `IgnoreQueryFilters()`), never through a shared
     MediatR handler built for the HTTP pipeline. This is the dominant pattern
     (5-6 of 7 comparable jobs).
   - **Shape 2 — single-tenant-per-unit, set `IJobTenantContextAccessor` once per
     fresh scope, reuse existing HTTP-oriented handlers unmodified.** Used by
     `BulkSopImportJob` (fresh `IServiceScopeFactory.CreateAsyncScope()` per PDF,
     accessor set before resolving `ISender`) and structurally mirrored by
     `CorpusRunJob`'s explicit-`tenantId`-parameter, self-contained-query design
     (though `CorpusRunJob` doesn't use the accessor since it doesn't call shared
     MediatR handlers).
   - `ProcessToolboxTalkSchedulesJob` fits neither shape cleanly as currently
     structured: it's cross-tenant like Shape-1 jobs, but delegates real work to
     a shared, HTTP-oriented handler like Shape-2's `BulkSopImportJob` — and,
     unlike `BulkSopImportJob`, does all of that work from one single outer scope
     with one single shared `IToolboxTalksDbContext` (§B.3), rather than a fresh
     scope per unit. Whatever shape a fix adopts, it needs to decide the
     granularity of any accessor-setting (per-tenant vs per-schedule) and
     whether to also address the shared-change-tracker exposure noted in §B.3 —
     this recon does not recommend a shape.

4. **Do the Chunk 2 lifecycle defects (cadence duplication, stale refresh
   membership) manifest via cron, button, or both today?** **Button only, today.**
   Both defects live inside `ProcessToolboxTalkScheduleCommandHandler.Handle`'s
   body, which the cron path never reaches (it throws and exits at the schedule
   lookup before any of that logic runs). The existing characterisation tests
   (`4c896aa`) prove this by construction — 3 of 4 drive the lifecycle purely
   through the authenticated HTTP endpoint, and the 4th needed a manual
   tenant-context workaround specifically because the job's real, unmodified
   behavior would otherwise be "every schedule throws not-found" rather than
   letting the due-filter defect be observed at all.

5. **What must not break?** The HTTP "Process Now" endpoint
   ([ToolboxTalkSchedulesController.cs:244-275](../src/QuantumBuild.API/Controllers/ToolboxTalkSchedulesController.cs#L244-L275)),
   which sets `command.TenantId = _currentUserService.TenantId` with a real
   `HttpContext` present and is confirmed working correctly today — it is the
   only path by which recurring schedules have ever actually produced
   `ScheduledTalk` rows in this codebase's current state. A tenant-context fix
   for the cron side must not alter how ambient tenant resolution behaves for
   HTTP requests.

6. **Test coverage surface for verification.** One integration test file
   (`RecurringScheduleRefreshCharacterisationTests.cs`, commit `4c896aa`)
   exists; no other test anywhere references `ProcessToolboxTalkSchedulesJob` or
   `ProcessToolboxTalkScheduleCommandHandler`. The 4th test's existing manual
   `IJobTenantContextAccessor` workaround is a ready-made verification point — a
   fix that makes that workaround unnecessary (remove the line, same assertions
   still pass) is a direct, low-effort regression check. No test today asserts
   the current broken "every schedule throws not-found" behaviour as an explicit
   expectation in its own right (it's only visible via doc comment and via the
   4th test's need for its workaround) — a genuinely new characterisation test
   asserting zero `ScheduledTalk` rows are created by a bare `ExecuteAsync()`
   call with no tenant-context workaround would close that gap and give Chunk 2
   (or a dedicated tenant-context fix) a clean before/after proof point.
