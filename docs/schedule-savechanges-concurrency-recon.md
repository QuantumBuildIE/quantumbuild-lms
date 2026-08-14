# Schedule SaveChanges Concurrency Exception — Recon

Read-only recon. No code changed. All claims are `file:line` against the current
`transval` branch working tree, and the central claim is backed by a live
reproduction (Testcontainers PostgreSQL 16, via `dotnet test`) with EF Core
command-level SQL logging captured — not inference from reading code alone.

## 0. Headline finding

**This is not a concurrency-token, query-filter, or Hangfire-tenant-context bug.**
It is a plain EF Core change-tracking defect: a brand-new
`ToolboxTalkScheduleAssignment` added to an **already-tracked** parent's
`Assignments` navigation collection (via `schedule.Assignments.Add(...)`, never
via an explicit `_dbContext.Add(...)`) is picked up by `DetectChanges()` and
resolved to **`EntityState.Modified`, not `EntityState.Added`** — because its
primary key (`Id`) was pre-assigned client-side (`Id = Guid.NewGuid()`) and the
`Id` property's EF metadata is `ValueGeneratedOnAdd()` (the ordinary convention
default for a `Guid` key — no override exists anywhere for this entity).

At `SaveChangesAsync`, EF therefore emits an **`UPDATE ... WHERE "Id" = @p"`**
for a row that has never been inserted. Postgres reports 0 rows matched. EF/Npgsql
raises `DbUpdateConcurrencyException: expected to affect 1 row(s), but actually
affected 0 row(s)`.

This was captured directly in a live run — see §2 for the exact SQL batch.

## 1. The two handlers, at the SaveChangesAsync call

### 1.1 `UpdateToolboxTalkScheduleCommandHandler`

Load (tracked, `Include`d, not `AsNoTracking`):
[UpdateToolboxTalkScheduleCommandHandler.cs:35-38](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/UpdateToolboxTalkSchedule/UpdateToolboxTalkScheduleCommandHandler.cs#L35-L38):
```csharp
var schedule = await _dbContext.ToolboxTalkSchedules
    .Include(s => s.Assignments)
    .Include(s => s.ToolboxTalk)
    .FirstOrDefaultAsync(s => s.Id == request.Id && s.TenantId == request.TenantId, cancellationToken);
```
`schedule` is a query result — tracked from the moment it's materialised, in
`Unchanged` state initially.

Genuine business-property writes (schedule becomes `Modified` for real reasons,
unrelated to the assignment collection):
[UpdateToolboxTalkScheduleCommandHandler.cs:174-181](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/UpdateToolboxTalkSchedule/UpdateToolboxTalkScheduleCommandHandler.cs#L174-L181) —
`ScheduledDate`, `EndDate`, `Frequency`, `AssignToAllEmployees`,
`TargetDepartmentIds`, `TargetSiteIds`, `NextRunDate`, `Notes` are set
unconditionally on every update call.

Removal — physically deleted via a **separate, immediate** `ExecuteDeleteAsync`
round-trip, fully decoupled from the `ChangeTracker`/`SaveChangesAsync` pipeline:
[UpdateToolboxTalkScheduleCommandHandler.cs:192-209](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/UpdateToolboxTalkSchedule/UpdateToolboxTalkScheduleCommandHandler.cs#L192-L209):
```csharp
var assignmentIdsToRemove = new List<Guid>();
foreach (var assignment in assignmentsToRemove)
{
    schedule.Assignments.Remove(assignment); // load-bearing for nav-collection consumers
    _dbContext.Entry(assignment).State = EntityState.Detached;
    assignmentIdsToRemove.Add(assignment.Id);
}

if (assignmentIdsToRemove.Count > 0)
{
    await _dbContext.ToolboxTalkScheduleAssignments
        .Where(a => assignmentIdsToRemove.Contains(a.Id))
        .ExecuteDeleteAsync(cancellationToken);
}
```
`ExecuteDeleteAsync` issues its own `DELETE ... WHERE "Id" = ANY(...)` immediately
against the database — it does not go through `SaveChangesAsync` at all, and the
`Detach` call removes the entity from the tracker before it can ever be considered
by `DetectChanges`.

Addition — the entity that becomes the problem:
[UpdateToolboxTalkScheduleCommandHandler.cs:212-225](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/UpdateToolboxTalkSchedule/UpdateToolboxTalkScheduleCommandHandler.cs#L212-L225):
```csharp
var employeesToAdd = newEmployeeIds.Except(currentEmployeeIds);
foreach (var employeeId in employeesToAdd)
{
    var assignment = new ToolboxTalkScheduleAssignment
    {
        Id = Guid.NewGuid(),
        ScheduleId = schedule.Id,
        EmployeeId = employeeId,
        IsProcessed = false,
        ProcessedAt = null,
        IsCriteriaDerived = criteriaDerivedIds.Contains(employeeId)
    };
    schedule.Assignments.Add(assignment);
}
```
Note what is **absent**: no `_dbContext.ToolboxTalkScheduleAssignments.Add(assignment)`,
no `_dbContext.Add(assignment)`, no `_dbContext.Entry(assignment).State = EntityState.Added`.
The only thing that links this new object into the tracked graph is the plain
`ICollection<T>.Add` call on an already-tracked parent's navigation property.

SaveChanges call: [UpdateToolboxTalkScheduleCommandHandler.cs:227](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/UpdateToolboxTalkSchedule/UpdateToolboxTalkScheduleCommandHandler.cs#L227) — `await _dbContext.SaveChangesAsync(cancellationToken);`

### 1.2 `ProcessToolboxTalkScheduleCommandHandler`

Load (tracked, `Include`d): [ProcessToolboxTalkScheduleCommandHandler.cs:35-39](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs#L35-L39).

Genuine business-property writes, unconditional on every process call (status
transition, `NextRunDate`, and/or resetting every assignment's `IsProcessed`/
`ProcessedAt` for the next recurrence): [ProcessToolboxTalkScheduleCommandHandler.cs:158-198](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs#L158-L198).

Addition in `RefreshAssignmentsForAllEmployees` — same pattern, same defect:
[ProcessToolboxTalkScheduleCommandHandler.cs:224-238](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs#L224-L238) — `schedule.Assignments.Add(assignment)`, no explicit `Add()` on the `DbContext`/`DbSet`.

Addition in `RefreshAssignmentsForTargetCriteria` — same pattern:
[ProcessToolboxTalkScheduleCommandHandler.cs:295-310](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs#L295-L310).

Removal in both refresh methods — same `ExecuteDeleteAsync` + `Detach` pattern as
§1.1: [ProcessToolboxTalkScheduleCommandHandler.cs:246-263](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs#L246-L263) and [ProcessToolboxTalkScheduleCommandHandler.cs:319-332](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs#L319-L332).

SaveChanges call: [ProcessToolboxTalkScheduleCommandHandler.cs:200](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs#L200).

### 1.3 Contrast: `CreateToolboxTalkScheduleCommandHandler` (unaffected — and why)

[CreateToolboxTalkScheduleCommandHandler.cs:171-203](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/CreateToolboxTalkSchedule/CreateToolboxTalkScheduleCommandHandler.cs#L171-L203):
```csharp
var schedule = new ToolboxTalkSchedule { Id = Guid.NewGuid(), ... };

foreach (var employeeId in employeeIdsToAssign)
{
    var assignment = new ToolboxTalkScheduleAssignment { Id = Guid.NewGuid(), ... };
    schedule.Assignments.Add(assignment);
}

_dbContext.ToolboxTalkSchedules.Add(schedule);   // <-- line 202
await _dbContext.SaveChangesAsync(cancellationToken);
```
`schedule` here is a **brand-new, never-tracked local variable** — it is not a
query result. `_dbContext.ToolboxTalkSchedules.Add(schedule)` is called explicitly,
and crucially it is called **after** the assignments have already been added to
`schedule.Assignments`. EF Core's `Add()` forces the entire object graph reachable
from the added root into `EntityState.Added`, unconditionally, regardless of any
key value already present on the reachable entities — this is the one situation
where the key-value heuristic in §3 is bypassed entirely. This is the mechanism
difference that makes "create" immune: the new assignments are swept into `Added`
state by explicit graph-propagation from `Add()`, not resolved via the
key-value heuristic that misfires for Update/Process.

## 2. Live reproduction — the actual failing SQL

Reproduced by running the (pre-existing, uncommitted) integration test
`ScheduleTargetingUpdateAndRefreshTests` against a real PostgreSQL 16
Testcontainers instance (`dotnet test tests/QuantumBuild.Tests.Integration/QuantumBuild.Tests.Integration.csproj --filter "FullyQualifiedName~ScheduleTargetingUpdateAndRefreshTests"`).

Result: **3 of 5 fail**, all three and only the three that add a new assignment
in the same handler call as a genuine parent business-property write; the two
that pass are pure-remove and a no-op-diff case. This exactly matches the
add-fails/remove-passes/create-unaffected characterisation in the task brief:

| Test | Assignment diff | Result |
|---|---|---|
| `UpdateSchedule_TargetDepartment_ResolvesToExactlyActiveDepartmentMembers` | adds 2, removes 1 (via `ExecuteDeleteAsync`) | **FAIL** — 500, `DbUpdateConcurrencyException` |
| `UpdateSchedule_DepartmentAndSiteTargets_UnionDeduplicated` | adds 1, removes 0 | **FAIL** — 500, `DbUpdateConcurrencyException` |
| `UpdateSchedule_SupervisorTargetsDepartment_OnlyReachesAssignedOperators` | adds 0, removes 0 (no-op diff) | Pass |
| `ProcessSchedule_RecurringTargetCriteria_AddsNewlyQualifyingEmployee` | adds 1, removes 0 | **FAIL** — 500, `DbUpdateConcurrencyException` |
| `ProcessSchedule_RecurringTargetCriteria_RemovesNoLongerQualifying_PreservesExplicit` | adds 0, removes 1 (via `ExecuteDeleteAsync`) | Pass |

Re-ran `UpdateSchedule_DepartmentAndSiteTargets_UnionDeduplicated` alone with EF
Core command-level `Debug` logging enabled (via env vars
`Logging__LogLevel__Microsoft.EntityFrameworkCore.Database.Command=Debug` and
`Logging__LogLevel__Microsoft.EntityFrameworkCore.Update=Debug` — no source/config
files edited) to capture the exact batch. The failing `SaveChangesAsync` call
emits:

```
dbug: Microsoft.EntityFrameworkCore.Update[20700]
      Executing 2 update commands as a batch.
dbug: Microsoft.EntityFrameworkCore.Database.Command[20100]
      Executing DbCommand [...]
      UPDATE toolbox_talks."ToolboxTalkScheduleAssignments" SET "DeletedBy" = @p0, "EmployeeId" = @p1, "IsCriteriaDerived" = @p2, "IsDeleted" = @p3, "IsProcessed" = @p4, "ProcessedAt" = @p5, "ScheduleId" = @p6, "UpdatedAt" = @p7, "UpdatedBy" = @p8
      WHERE "Id" = @p9;
      UPDATE toolbox_talks."ToolboxTalkSchedules" SET "TargetSiteIds" = @p10, "UpdatedAt" = @p11, "UpdatedBy" = @p12
      WHERE "Id" = @p13;
dbug: Microsoft.EntityFrameworkCore.Update[10006]
      Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException: The database operation was expected to affect 1 row(s), but actually affected 0 row(s); ...
```

**Both statements in the batch are `UPDATE`. There is no `INSERT` anywhere in this
`SaveChanges` call.** The newly-added `ToolboxTalkScheduleAssignment` (for
`siteOnly`, the one genuinely new employee in this test) is being sent as an
`UPDATE ... WHERE "Id" = @p9` against a row that was never inserted.

This is deterministic, not a race: the schedule row (`@p13`) was loaded moments
earlier by a successful `SELECT` in the same request and is matched by primary
key — a PK-keyed `UPDATE` against a row proven to exist cannot return 0 rows.
The assignment row (`@p9`) has no corresponding prior `INSERT` anywhere in this
request's command log — it cannot exist, so its `UPDATE` is the only one of the
two statements that can legitimately return 0 rows. The assignment `UPDATE` is
therefore the failing statement, by elimination as well as by construction.

Corroborating detail: the failing `UPDATE`'s `SET` list contains every
`ToolboxTalkScheduleAssignment` column **except** `Id` (the `WHERE` key),
`CreatedAt`, and `CreatedBy`. That is exactly the signature of
`SetAuditFields()`'s `EntityState.Modified` branch —
[ApplicationDbContext.cs:432-444](../src/Core/QuantumBuild.Core.Infrastructure/Data/ApplicationDbContext.cs#L432-L444) —
which stamps `UpdatedAt`/`UpdatedBy` and explicitly blocks `CreatedAt`/`CreatedBy`
from being flagged modified, but does nothing for `Added` entities in that branch.
Confirms the entity had already resolved to `EntityState.Modified` *before*
`SetAuditFields()` ran (i.e. by the time `ChangeTracker.Entries<BaseEntity>()` at
[ApplicationDbContext.cs:413](../src/Core/QuantumBuild.Core.Infrastructure/Data/ApplicationDbContext.cs#L413) triggered its implicit `DetectChanges()`), not
something the interceptor itself caused.

## 3. Why EF resolves the new assignment to `Modified` instead of `Added`

Entity configuration — `Id` has no override, so it carries the ordinary EF Core
convention for a `Guid` primary key: [ToolboxTalkScheduleAssignmentConfiguration.cs:17-18](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Persistence/Configurations/ToolboxTalkScheduleAssignmentConfiguration.cs#L17-L18) (`HasKey(a => a.Id)`, nothing further). No `.ValueGeneratedNever()`, no
`HasValueGenerator()`, no `ConfigureConventions()` override anywhere in the
codebase for this entity or globally (`ApplicationDbContext.cs` has no
`ConfigureConventions` override at all).

The resolved model metadata, from the current EF model snapshot, confirms the
convention landed on server-generation semantics: [ApplicationDbContextModelSnapshot.cs:5381-5383](../src/Core/QuantumBuild.Core.Infrastructure/Migrations/ApplicationDbContextModelSnapshot.cs#L5381-L5383):
```csharp
b.Property<Guid>("Id")
    .ValueGeneratedOnAdd()
    .HasColumnType("uuid");
```

The mechanism:

1. The application constructs `ToolboxTalkScheduleAssignment` with `Id = Guid.NewGuid()`
   set client-side ([UpdateToolboxTalkScheduleCommandHandler.cs:217](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/UpdateToolboxTalkSchedule/UpdateToolboxTalkScheduleCommandHandler.cs#L217), same pattern in
   both `RefreshAssignments*` methods) — a real, non-default `Guid`, not
   `Guid.Empty`.
2. It is linked into the graph purely via `schedule.Assignments.Add(assignment)`
   on an **already-tracked** `schedule` (a query result). No `DbSet.Add`/
   `DbContext.Add`/explicit `EntityState.Added` assignment ever touches this
   specific object.
3. At `SaveChangesAsync`, EF's automatic change detection (`DetectChanges`,
   invoked implicitly the moment `ChangeTracker.Entries<T>()` is enumerated —
   here first triggered by `SetAuditFields()`'s own `ChangeTracker.Entries<BaseEntity>()`
   call, [ApplicationDbContext.cs:413](../src/Core/QuantumBuild.Core.Infrastructure/Data/ApplicationDbContext.cs#L413)) walks the tracked graph reachable from
   `schedule` and discovers this untracked object hanging off the `Assignments`
   navigation.
4. For an entity discovered this way (via graph traversal/fixup, not an explicit
   `Add`), EF Core must guess whether it is a genuinely new row or an existing
   row being re-attached. Its convention-based rule keys off the primary key: if
   the key property is `ValueGeneratedOnAdd` (server/DB-generated) *and* its
   current CLR value is still the type default (`Guid.Empty`), EF is confident
   it's new → `Added`. Here the key already holds a real, non-default value
   (because the application pre-assigns it) — EF cannot distinguish that from
   "a row that already exists and is being reattached with its known key", and
   resolves it to `Unchanged`/`Modified` rather than `Added`.
5. Because this object has no original/DB snapshot to diff against (it was never
   queried), EF's standard behaviour for a `Modified` entity with no known
   original values is to flag *every* writable scalar property as modified —
   which is exactly the full-column `SET` list captured in §2 (everything except
   the PK and the two audit fields `SetAuditFields()` explicitly protects).
6. The resulting `UPDATE ... WHERE "Id" = @pN` targets a row that was never
   inserted → 0 rows affected → `DbUpdateConcurrencyException`.

This is a well-known, named EF Core pitfall: application-assigned ("client-side")
key values on entities that are expected to be tracked via graph-fixup, combined
with the default `ValueGeneratedOnAdd()` convention for `Guid` keys, defeats EF's
Added-vs-Modified heuristic. The standard fix (not applied — no fix in this recon)
is either (a) configure the key `.ValueGeneratedNever()` so EF stops trying to
infer "is this new" from the key's default-ness, or (b) explicitly call
`_dbContext.ToolboxTalkScheduleAssignments.Add(assignment)` (or
`_dbContext.Entry(assignment).State = EntityState.Added`) instead of relying on
navigation-collection fixup for entities reached from an already-tracked parent.

## 4. Answering the specific investigation questions

**Which statement fails (Q2):** The `UPDATE` on `ToolboxTalkScheduleAssignments`
for the newly-added row, not the parent's `UPDATE` on `ToolboxTalkSchedules`.
Directly captured in §2; also provable by elimination since the schedule row is
proven to exist by a preceding successful `SELECT` in the same request.

**Concurrency tokens / DB-generated columns in the WHERE clause (Q3):** None.
`ToolboxTalkScheduleAssignmentConfiguration.cs` and `ToolboxTalkScheduleConfiguration.cs`
configure no `IsConcurrencyToken()`, no `[Timestamp]`/`RowVersion`, and
`BaseEntity`/`TenantEntity` ([BaseEntity.cs](../src/Core/QuantumBuild.Core.Domain/Common/BaseEntity.cs)) declare none either. The captured
`UPDATE` statements confirm this directly — their `WHERE` clauses are bare
`WHERE "Id" = @pN`, nothing else. The "expected 1 row" check EF performs is the
ordinary "did my `UPDATE`-by-primary-key hit exactly the row I meant to touch"
check that EF always performs for every tracked `Modified`/`Deleted` entity — it
is not evidence of an explicit concurrency token; it is EF's normal safety check
that a same-transaction `UPDATE` by PK found the row it expected. It fires here
purely because the row doesn't exist yet, not because of a stale-data race.

**Query filters / tenant mismatch (Q4):** Ruled out. `ToolboxTalkScheduleAssignment`
carries no `TenantId` field at all — it is `BaseEntity`, not `TenantEntity`
([ToolboxTalkScheduleAssignment.cs:10](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Entities/ToolboxTalkScheduleAssignment.cs#L10)). Its only query filter is the
soft-delete-only `!IsDeleted` predicate configured directly on the entity
([ToolboxTalkScheduleAssignmentConfiguration.cs:77](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Persistence/Configurations/ToolboxTalkScheduleAssignmentConfiguration.cs#L77)) — this is intentional, not an
instance of the cross-tenant leak class fixed under Note 14: the child is scoped
via its parent `ToolboxTalkSchedule` (which *is* tenant-filtered), and
`ApplicationDbContext.cs` explicitly documents this entity as one of the
deliberately non-tenant-scoped children ([ApplicationDbContext.cs:376-377](../src/Core/QuantumBuild.Core.Infrastructure/Data/ApplicationDbContext.cs#L376-L377)). More
fundamentally: EF Core global query filters affect `SELECT` predicates only (and
`ExecuteDelete`/`ExecuteUpdate` bulk operations, which apply the filter to their
own `WHERE`) — they play no role in shaping the `WHERE` clause of an ordinary
`SaveChanges`-generated `UPDATE`/`DELETE` for a tracked entity, which is always
keyed on the primary key alone. Confirmed directly in the captured SQL: the
failing `UPDATE`'s `WHERE` is bare `"Id" = @p9`, with no `IsDeleted`/`TenantId`
predicate present.

**Hangfire tenant context for the Process handler (Q5):** Ruled out.
`ProcessToolboxTalkScheduleCommandHandler` never relies on ambient
`ICurrentUserService.TenantId` — it takes `request.TenantId` as an explicit
command parameter and uses it directly in its own query
([ProcessToolboxTalkScheduleCommandHandler.cs:39](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs#L39)). The Hangfire job that
dispatches it explicitly threads the correct tenant through per-tenant, with an
`IgnoreQueryFilters()` + explicit `TenantId`/`IsDeleted` predicate specifically
because it runs outside HTTP context: [ProcessToolboxTalkSchedulesJob.cs:55-63](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/ProcessToolboxTalkSchedulesJob.cs#L55-L63). The
live reproduction in §2 additionally proves the bug fires with **fully correct**
tenant context (the test invokes the `/process` HTTP endpoint directly, tenant
resolved normally via the authenticated request) — so tenant-context correctness
is not a variable in this failure at all, in either execution path.

**Does the parent need to be `Modified` for the bug to trigger (Q6):** No — and
the task brief's "Modified parent + Added child" framing, while an accurate
behavioural correlation in this specific codebase, is not the causal condition.
The mechanism in §3 depends only on step 2 (a new, key-pre-assigned child linked
via nav-collection `Add()` to an *already-tracked* parent) and is completely
independent of whether that parent also happens to have unrelated business
properties changed. The correlation holds here only because, empirically, every
code path in these two handlers that adds an assignment also happens to write
real business columns on the schedule in the same call — `Update` always writes
`ScheduledDate`/`EndDate`/etc. unconditionally
([UpdateToolboxTalkScheduleCommandHandler.cs:174-181](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/UpdateToolboxTalkSchedule/UpdateToolboxTalkScheduleCommandHandler.cs#L174-L181)), and `Process` always
writes `Status`/`NextRunDate`/assignment-reset fields unconditionally
([ProcessToolboxTalkScheduleCommandHandler.cs:158-198](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs#L158-L198)) — there is no code path
in either handler that adds an assignment while leaving the parent scalar
properties completely untouched, so the two conditions always travel together in
practice. But the parent write is not what causes the child to be misclassified;
`DetectChanges` walks the *entire* tracked graph reachable from any tracked
entity regardless of that entity's own state, so a schedule that stayed
genuinely `Unchanged` while a new assignment was added to its collection would
be expected to hit the identical `Modified`-not-`Added` misclassification for
the child (not verified with a dedicated new test, per the no-code-changes
constraint of this recon, but it follows directly from EF's documented
`DetectChanges` graph-traversal behaviour and is consistent with everything
observed in §2). The parent's own genuine, intentional business-property writes
are not a side effect of the collection `.Add()` call, nor is the child's
misclassification a side effect of the parent's `Modified` state — the two are
independently true facts about the same `SaveChanges` call.

**Why remove-only and create differ from add (Q7):** Both already covered by
§1.3 (create) and by construction (remove): removal never touches the
`SaveChanges`/`ChangeTracker` pipeline for the removed rows at all — it is a
`Detach()` followed by an immediate, separate `ExecuteDeleteAsync()` round-trip
([UpdateToolboxTalkScheduleCommandHandler.cs:196-209](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/UpdateToolboxTalkSchedule/UpdateToolboxTalkScheduleCommandHandler.cs#L196-L209), same pattern at both
refresh sites) that completes and commits before `SaveChangesAsync` is ever
called. There is no `Added`-vs-`Modified` ambiguity to resolve for a removal,
because the removed entity is never in a state where EF has to guess — it's
either `Detach`ed (ignored entirely by `SaveChanges`) or gone from the graph.
Create is immune because `_dbContext.ToolboxTalkSchedules.Add(schedule)`
([CreateToolboxTalkScheduleCommandHandler.cs:202](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/CreateToolboxTalkSchedule/CreateToolboxTalkScheduleCommandHandler.cs#L202)) forces the whole reachable
graph (schedule + every assignment already added to its collection) into
`Added` unconditionally — `Add()`'s graph-propagation bypasses the key-value
heuristic described in §3 entirely; the heuristic only comes into play for
entities discovered via `DetectChanges` on an *already-tracked* root, which is
exactly the Update/Process situation and never the Create situation.

## 5. Summary for fix selection

The defect is entity-state resolution, not concurrency, not tenancy, not query
filters. Any fix needs to make EF treat the newly-added
`ToolboxTalkScheduleAssignment` as genuinely `Added` in the Update and Process
handlers' `SaveChanges` calls. The two standard, well-established EF Core
remedies (not proposed as a specific implementation — out of scope for this
recon) are:

- Configure `ToolboxTalkScheduleAssignment.Id` with `.ValueGeneratedNever()` in
  `ToolboxTalkScheduleAssignmentConfiguration.cs`, so EF no longer uses key
  default-ness as its Added/Modified signal for this entity (would require an EF
  migration check — value-generation strategy is model metadata, not a schema
  change, so likely no actual migration needed, but must be verified).
- Replace the `schedule.Assignments.Add(assignment)` navigation-fixup pattern
  with an explicit `_dbContext.ToolboxTalkScheduleAssignments.Add(assignment)`
  (or `_dbContext.Entry(assignment).State = EntityState.Added`) at each of the
  three add sites (`UpdateToolboxTalkScheduleCommandHandler.cs:224`,
  `ProcessToolboxTalkScheduleCommandHandler.cs:236`,
  `ProcessToolboxTalkScheduleCommandHandler.cs:308`), which forces `Added` state
  directly and sidesteps the heuristic regardless of key configuration.

Either fix (or both, for defence in depth) should be validated against the
existing uncommitted test file
[ScheduleTargetingUpdateAndRefreshTests.cs](../tests/QuantumBuild.Tests.Integration/ToolboxTalks/ScheduleTargetingUpdateAndRefreshTests.cs),
which already reproduces the failure end-to-end and would need to go from 3/5
to 5/5 passing.
