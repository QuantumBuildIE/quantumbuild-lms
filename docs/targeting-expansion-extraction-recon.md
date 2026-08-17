# Targeting Expansion Extraction — Recon

Read-only recon. No code changed. All claims are `file:line` against the current `transval` branch working tree.

## 1. The two named inline blocks, in full context

### 1.1 `CreateToolboxTalkScheduleCommandHandler.cs:138-156` (create-time initial resolution)

Full surrounding context, [CreateToolboxTalkScheduleCommandHandler.cs:78-162](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/CreateToolboxTalkSchedule/CreateToolboxTalkScheduleCommandHandler.cs#L78-L162):

```csharp
// Get employee IDs to assign
List<Guid> employeeIdsToAssign;
var criteriaDerivedIds = new HashSet<Guid>();
if (request.AssignToAllEmployees)
{
    // Get all active employees for the tenant
    employeeIdsToAssign = await _coreDbContext.Employees
        .Where(e => e.TenantId == request.TenantId && e.IsActive && !e.IsDeleted)
        .Select(e => e.Id)
        .ToListAsync(cancellationToken);

    if (!employeeIdsToAssign.Any())
    {
        throw new InvalidOperationException("No active employees found to assign the learning.");
    }
}
else
{
    // Validate provided employee IDs exist and are active
    var validEmployeeIds = await _coreDbContext.Employees
        .Where(e => e.TenantId == request.TenantId && e.IsActive && !e.IsDeleted && request.EmployeeIds.Contains(e.Id))
        .Select(e => e.Id)
        .ToListAsync(cancellationToken);

    var invalidIds = request.EmployeeIds.Except(validEmployeeIds).ToList();
    if (invalidIds.Any())
    {
        throw new InvalidOperationException($"The following employee IDs are invalid or inactive: {string.Join(", ", invalidIds)}");
    }

    // Validate department/site targets belong to the caller's tenant
    if (request.TargetDepartmentIds.Any())
    {
        var validDepartmentIds = await _coreDbContext.Departments
            .Where(d => d.TenantId == request.TenantId && !d.IsDeleted && request.TargetDepartmentIds.Contains(d.Id))
            .Select(d => d.Id)
            .ToListAsync(cancellationToken);

        var invalidDepartmentIds = request.TargetDepartmentIds.Except(validDepartmentIds).ToList();
        if (invalidDepartmentIds.Any())
        {
            throw new InvalidOperationException($"The following department IDs are invalid: {string.Join(", ", invalidDepartmentIds)}");
        }
    }

    if (request.TargetSiteIds.Any())
    {
        var validSiteIds = await _coreDbContext.Sites
            .Where(s => s.TenantId == request.TenantId && !s.IsDeleted && request.TargetSiteIds.Contains(s.Id))
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        var invalidSiteIds = request.TargetSiteIds.Except(validSiteIds).ToList();
        if (invalidSiteIds.Any())
        {
            throw new InvalidOperationException($"The following site IDs are invalid: {string.Join(", ", invalidSiteIds)}");
        }
    }

    // Expand department/site targets to member employees (union, active only)
    if (request.TargetDepartmentIds.Any() || request.TargetSiteIds.Any())
    {
        var expandedIds = await _coreDbContext.Employees
            .Where(e => e.TenantId == request.TenantId && e.IsActive && !e.IsDeleted
                && ((e.DepartmentId.HasValue && request.TargetDepartmentIds.Contains(e.DepartmentId.Value))
                    || (e.PrimarySiteId.HasValue && request.TargetSiteIds.Contains(e.PrimarySiteId.Value))))
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);

        // Supervisors only reach the operators assigned to them, even via a department/location target
        if (isSupervisorOnly)
        {
            expandedIds = expandedIds.Intersect(supervisorPermittedIds!).ToList();
        }

        criteriaDerivedIds = expandedIds.ToHashSet();
    }

    employeeIdsToAssign = validEmployeeIds.Union(criteriaDerivedIds).ToList();

    if (!employeeIdsToAssign.Any())
    {
        throw new InvalidOperationException("No employees resolved from the selected employees, departments, or locations.");
    }
}
```

**Purpose:** runs once, synchronously, inside the `Handle` method that creates a brand-new `ToolboxTalkSchedule`. Produces the full initial `employeeIdsToAssign` list (explicit + criteria-derived, unioned) that becomes the schedule's `ToolboxTalkScheduleAssignment` rows — [CreateToolboxTalkScheduleCommandHandler.cs:187-200](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/CreateToolboxTalkSchedule/CreateToolboxTalkScheduleCommandHandler.cs#L187-L200). It also builds `criteriaDerivedIds`, which is written to `ToolboxTalkScheduleAssignment.IsCriteriaDerived` per-row ([CreateToolboxTalkScheduleCommandHandler.cs:197](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/CreateToolboxTalkSchedule/CreateToolboxTalkScheduleCommandHandler.cs#L197)) — this flag is what the process-handler's refresh logic later uses to know which assignments it's allowed to touch.

### 1.2 `ProcessToolboxTalkScheduleCommandHandler.cs:281-286` (recurring refresh-diff)

Full surrounding context, [ProcessToolboxTalkScheduleCommandHandler.cs:266-333](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs#L266-L333) (method `RefreshAssignmentsForTargetCriteria`):

```csharp
/// <summary>
/// Re-derives the department/location-targeted portion of a recurring schedule's assignments.
/// Only touches assignments flagged IsCriteriaDerived — explicitly-added employees (EmployeeIds
/// at creation/edit time) are never added or removed by this refresh, mirroring how
/// RefreshAssignmentsForAllEmployees never coexists with an explicit list (AssignToAllEmployees
/// and EmployeeIds are mutually exclusive there).
/// </summary>
private async Task RefreshAssignmentsForTargetCriteria(
    ToolboxTalkSchedule schedule,
    Guid tenantId,
    CancellationToken cancellationToken)
{
    var targetDepartmentIds = schedule.TargetDepartmentIds;
    var targetSiteIds = schedule.TargetSiteIds;

    var currentTargetEmployeeIds = (await _coreDbContext.Employees
        .Where(e => e.TenantId == tenantId && e.IsActive && !e.IsDeleted
            && ((e.DepartmentId.HasValue && targetDepartmentIds.Contains(e.DepartmentId.Value))
                || (e.PrimarySiteId.HasValue && targetSiteIds.Contains(e.PrimarySiteId.Value))))
        .Select(e => e.Id)
        .ToListAsync(cancellationToken)).ToHashSet();

    var existingEmployeeIds = schedule.Assignments.Select(a => a.EmployeeId).ToHashSet();
    var existingCriteriaDerivedEmployeeIds = schedule.Assignments
        .Where(a => a.IsCriteriaDerived)
        .Select(a => a.EmployeeId)
        .ToHashSet();

    // Add newly-qualifying employees (skip anyone already present, whether explicit or criteria-derived)
    foreach (var employeeId in currentTargetEmployeeIds)
    {
        if (!existingEmployeeIds.Contains(employeeId))
        {
            var assignment = new ToolboxTalkScheduleAssignment
            {
                Id = Guid.NewGuid(),
                ScheduleId = schedule.Id,
                EmployeeId = employeeId,
                IsProcessed = false,
                ProcessedAt = null,
                IsCriteriaDerived = true
            };
            schedule.Assignments.Add(assignment);
        }
    }

    // Remove criteria-derived assignments that no longer qualify (department/site changed, or employee
    // went inactive). Explicit (non-criteria-derived) assignments are never removed by this refresh.
    var noLongerQualifying = existingCriteriaDerivedEmployeeIds.Except(currentTargetEmployeeIds).ToHashSet();
    var assignmentsToRemove = schedule.Assignments
        .Where(a => a.IsCriteriaDerived && noLongerQualifying.Contains(a.EmployeeId))
        .ToList();

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
}
```

**Purpose:** called only when a recurring schedule (`Frequency != Once`) has zero unprocessed assignments left and is targeted by department/site (not `AssignToAllEmployees`) — [ProcessToolboxTalkScheduleCommandHandler.cs:84-90](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs#L84-L90). It re-runs the department/site → employee resolution against **current** org data and diffs the result against the schedule's **existing** assignment rows, adding newly-qualifying employees and removing (physically, via `ExecuteDeleteAsync`) criteria-derived employees who no longer qualify. It never creates the schedule and never touches explicitly-added (non-criteria-derived) assignments.

## 2. Precise comparison

### 2.1 The core resolution step — IDENTICAL

Both blocks run the exact same LINQ shape against `_coreDbContext.Employees`:

Create handler, [CreateToolboxTalkScheduleCommandHandler.cs:140-145](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/CreateToolboxTalkSchedule/CreateToolboxTalkScheduleCommandHandler.cs#L140-L145):
```csharp
var expandedIds = await _coreDbContext.Employees
    .Where(e => e.TenantId == request.TenantId && e.IsActive && !e.IsDeleted
        && ((e.DepartmentId.HasValue && request.TargetDepartmentIds.Contains(e.DepartmentId.Value))
            || (e.PrimarySiteId.HasValue && request.TargetSiteIds.Contains(e.PrimarySiteId.Value))))
    .Select(e => e.Id)
    .ToListAsync(cancellationToken);
```

Process handler, [ProcessToolboxTalkScheduleCommandHandler.cs:281-286](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs#L281-L286):
```csharp
var currentTargetEmployeeIds = (await _coreDbContext.Employees
    .Where(e => e.TenantId == tenantId && e.IsActive && !e.IsDeleted
        && ((e.DepartmentId.HasValue && targetDepartmentIds.Contains(e.DepartmentId.Value))
            || (e.PrimarySiteId.HasValue && targetSiteIds.Contains(e.PrimarySiteId.Value))))
    .Select(e => e.Id)
    .ToListAsync(cancellationToken)).ToHashSet();
```

Field-by-field: `TenantId ==`, `IsActive`, `!IsDeleted`, `DepartmentId.HasValue && targetDepartmentIds.Contains(...)`, OR `PrimarySiteId.HasValue && targetSiteIds.Contains(...)`, `.Select(e => e.Id)`. The only textual differences are the local variable names (`request.TargetDepartmentIds`/`request.TenantId` vs. `targetDepartmentIds`/`tenantId`) and whether the caller wraps the result in `.ToHashSet()` immediately or leaves it a `List<Guid>` — the query itself, predicate, and result set are the same operation. **This is the shared, extractable core.**

### 2.2 Supervisor restriction

**Only the create handler's block applies it, and only there.** Immediately after building `expandedIds`, the create handler intersects with `supervisorPermittedIds` when `isSupervisorOnly` — [CreateToolboxTalkScheduleCommandHandler.cs:147-151](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/CreateToolboxTalkSchedule/CreateToolboxTalkScheduleCommandHandler.cs#L147-L151):
```csharp
// Supervisors only reach the operators assigned to them, even via a department/location target
if (isSupervisorOnly)
{
    expandedIds = expandedIds.Intersect(supervisorPermittedIds!).ToList();
}
```

The process handler's `RefreshAssignmentsForTargetCriteria` (containing lines 281-286) has **no supervisor-restriction step anywhere in the method** — confirmed by reading the full method body, [ProcessToolboxTalkScheduleCommandHandler.cs:273-333](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs#L273-L333) — no reference to `ISupervisorAssignmentService`, `isSupervisorOnly`, or `supervisorPermittedIds` in this class at all (the class has no such field/injection — see its constructor, [ProcessToolboxTalkScheduleCommandHandler.cs:20-30](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs#L20-L30), which injects only `IToolboxTalksDbContext`, `ICoreDbContext`, `IToolboxTalkEmailService`, `ILogger`). This is expected: `ProcessToolboxTalkScheduleCommandHandler` runs as a Hangfire background job dispatch (recurring schedule processing) with no per-request caller identity to scope by — supervisor restriction was already baked into `schedule.TargetDepartmentIds`/`TargetSiteIds` and into which assignments exist as `IsCriteriaDerived` at creation time; the refresh just re-evaluates the same criteria against current org data.

**Placement relative to resolution:** the supervisor intersection is applied by the **caller**, immediately *after* the resolution query returns, as a second, separate LINQ statement (`expandedIds.Intersect(...)`) — not folded into the `Where` predicate of the resolution query itself. See §4.

### 2.3 Post-resolution handling — DIFFERS (confirmed, and is NOT part of the shared step)

- **Create handler:** unions the criteria-derived set with the explicitly-validated `validEmployeeIds` into one final `employeeIdsToAssign` list — [CreateToolboxTalkScheduleCommandHandler.cs:156](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/CreateToolboxTalkSchedule/CreateToolboxTalkScheduleCommandHandler.cs#L156) — then constructs a brand-new `ToolboxTalkScheduleAssignment` entity per ID, all with `IsProcessed = false` — [CreateToolboxTalkScheduleCommandHandler.cs:188-200](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/CreateToolboxTalkSchedule/CreateToolboxTalkScheduleCommandHandler.cs#L188-L200). No existing assignments to reconcile against — the schedule is new.
- **Process handler:** diffs the freshly-resolved `currentTargetEmployeeIds` against the schedule's **existing** assignment rows (`schedule.Assignments`, loaded via EF `Include`) — adds only employees not already present ([ProcessToolboxTalkScheduleCommandHandler.cs:294-310](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs#L294-L310)), and physically removes (`ExecuteDeleteAsync`) only `IsCriteriaDerived` rows that no longer qualify ([ProcessToolboxTalkScheduleCommandHandler.cs:312-332](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs#L312-L332)). Explicitly-added assignments are untouched by this diff regardless of whether they'd now also match the department/site criteria.

Confirmed: this post-resolution logic is genuinely different (create vs. diff-and-reconcile) and must stay in each caller — it is not part of the shared resolution step.

## 3. The exact shared boundary

**Shared operation:** given `tenantId`, `departmentIds`, `siteIds`, return the distinct set of active, non-deleted employee IDs in that tenant whose `DepartmentId` is in `departmentIds` OR whose `PrimarySiteId` is in `siteIds`.

Confirmed identical in both copies per §2.1 — same predicate, same fields, same union-via-OR semantics (a single query with an OR clause, not two separate queries; EF translates this to one SQL statement with `Contains` on both sides of an `OR`, so "union" here is a property of the WHERE clause, not two round-trips being concatenated client-side).

**Proposed shared-service signature (documentation only — not proposing exact code, per non-scope):**
- **Input:** `tenantId: Guid`, `departmentIds: IEnumerable<Guid>`, `siteIds: IEnumerable<Guid>`
- **Output:** `List<Guid>` (or `HashSet<Guid>`) of distinct employee IDs matching the OR predicate above
- **Caller-retained (NOT in the shared service):**
  - Tenant-ownership validation of the incoming department/site IDs themselves (the two `_coreDbContext.Departments`/`_coreDbContext.Sites` existence checks at [CreateToolboxTalkScheduleCommandHandler.cs:109-135](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/CreateToolboxTalkSchedule/CreateToolboxTalkScheduleCommandHandler.cs#L109-L135)) — this only exists in the create/update handlers, not in the process handler's refresh (the refresh trusts `schedule.TargetDepartmentIds`/`TargetSiteIds` as already-validated, persisted values). A course-assignment caller would need to decide independently whether it wants this validation.
  - Supervisor intersection (§2.2, §4)
  - Union with explicitly-selected `EmployeeIds` (create/update only)
  - `IsCriteriaDerived` flag bookkeeping (create/update only)
  - Diff-and-reconcile against existing assignment rows (process/refresh only)
  - The "no results → throw" guard (create/update only, worded per-caller: [CreateToolboxTalkScheduleCommandHandler.cs:158-161](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/CreateToolboxTalkSchedule/CreateToolboxTalkScheduleCommandHandler.cs#L158-L161) throws `"No employees resolved..."`; the process handler's refresh has no equivalent guard — an empty result is a legitimate "nobody qualifies anymore" outcome during a refresh, not an error)

This shared boundary requires zero behavioural change to either schedule handler: both already compute exactly this value as an intermediate (`expandedIds` / `currentTargetEmployeeIds`) before applying their own caller-specific logic on top.

## 4. Supervisor restriction placement — confirmed OUTSIDE the resolution

Per §2.2, the supervisor intersection in the create handler is a separate statement (`expandedIds.Intersect(supervisorPermittedIds!).ToList()`, [CreateToolboxTalkScheduleCommandHandler.cs:150](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/CreateToolboxTalkSchedule/CreateToolboxTalkScheduleCommandHandler.cs#L150)) that runs strictly after the resolution query completes and the result set is materialised as a `List<Guid>`. It is not a SQL predicate merged into the `Where` clause of the resolution query — it's client-side `Enumerable.Intersect` against an already-fetched list.

This confirms the current code already allows the separation the shared service needs:
- **Schedule callers** (create/update, when `isSupervisorOnly`): call the shared resolution, then intersect the result with `supervisorPermittedIds` themselves.
- **Process/refresh caller:** calls the shared resolution, applies no intersection (matches current behaviour — no supervisor scoping exists in the refresh path today).
- **Course caller (new, no supervisor restriction per requirements):** calls the shared resolution, uses the result directly.

No entanglement found — the supervisor step is caller-applied today in both handlers where it exists (create and, identically, update — see §6), and the shared service can be extracted with the resolution query alone, leaving 100% of the supervisor logic (permission lookup via `ISupervisorAssignmentService.GetAssignedOperatorIdsAsync`, the `AssignToAllEmployees` supervisor block, the `Intersect`) in the caller.

## 5. The single correct employee predicate

Confirmed identical across all three inline occurrences found (see §6 for the third):

```
e.TenantId == tenantId
&& e.IsActive
&& !e.IsDeleted
&& (
      (e.DepartmentId.HasValue && departmentIds.Contains(e.DepartmentId.Value))
   || (e.PrimarySiteId.HasValue && siteIds.Contains(e.PrimarySiteId.Value))
   )
```

Note on redundancy: `Employee` is a `TenantEntity` and the DbContext already applies a global query filter equivalent to `!e.IsDeleted && (BypassTenantFilter || e.TenantId == TenantId)` — [ApplicationDbContext.cs:339](../src/Core/QuantumBuild.Core.Infrastructure/Data/ApplicationDbContext.cs#L339). The explicit `e.TenantId == tenantId && !e.IsDeleted` clauses in all three handler copies are therefore redundant with that global filter under normal (non-`BypassTenantFilter`) execution, but harmless — they don't change the result set for a tenant-scoped caller. This is a pre-existing pattern in the codebase, not something introduced by the targeting feature; the shared service should keep the explicit clauses to match existing style and remain correct if ever called with `BypassTenantFilter` active (e.g. a SuperUser context), where the global filter alone would not scope to `tenantId`.

There is no other predicate variant found anywhere in the three copies — `IsActive` (not `!IsActive`, not omitted) and `!IsDeleted` are consistent everywhere department/site expansion happens.

For contrast, the sibling `AssignToAllEmployees` path (not part of this extraction, but relevant to confirm it's a *different* predicate — no department/site clause) uses the same active/non-deleted/tenant clauses without the `DepartmentId`/`PrimarySiteId` OR: [CreateToolboxTalkScheduleCommandHandler.cs:84-87](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/CreateToolboxTalkSchedule/CreateToolboxTalkScheduleCommandHandler.cs#L84-L87) and [ProcessToolboxTalkScheduleCommandHandler.cs:216-219](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs#L216-L219) (`RefreshAssignmentsForAllEmployees`). This confirms the department/site predicate is a distinct, separately-identifiable query shape, not a superset/subset of the all-employees query that could be accidentally conflated during extraction.

## 6. Additional finding: a third, previously unmentioned inline copy

The task named two blocks, but a **third, near-byte-identical copy** of both the resolution query and the supervisor-intersection step exists in `UpdateToolboxTalkScheduleCommandHandler.cs`, which was not in the original two-file list:

- Resolution query: [UpdateToolboxTalkScheduleCommandHandler.cs:143-148](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/UpdateToolboxTalkSchedule/UpdateToolboxTalkScheduleCommandHandler.cs#L143-L148) — identical predicate to §2.1.
- Supervisor intersection: [UpdateToolboxTalkScheduleCommandHandler.cs:150-154](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/UpdateToolboxTalkSchedule/UpdateToolboxTalkScheduleCommandHandler.cs#L150-L154) — identical shape to §2.2.
- Full supervisor-scoping block: [UpdateToolboxTalkScheduleCommandHandler.cs:51-79](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/UpdateToolboxTalkSchedule/UpdateToolboxTalkScheduleCommandHandler.cs#L51-L79) — same `isSupervisorOnly` derivation, same `GetAssignedOperatorIdsAsync` call, same unauthorised-employee guard as the create handler's [CreateToolboxTalkScheduleCommandHandler.cs:48-76](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/CreateToolboxTalkSchedule/CreateToolboxTalkScheduleCommandHandler.cs#L48-L76).

Behaviourally, `UpdateToolboxTalkScheduleCommandHandler` is create-shaped, not diff-shaped, for the *resolution* — it recomputes `employeeIdsToAssign` from scratch exactly like the create handler (§1.1), then separately diffs the result against `schedule.Assignments` for the add/remove of `ToolboxTalkScheduleAssignment` rows ([UpdateToolboxTalkScheduleCommandHandler.cs:184-225](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/UpdateToolboxTalkSchedule/UpdateToolboxTalkScheduleCommandHandler.cs#L184-L225), a different diff shape than `RefreshAssignmentsForTargetCriteria`'s `IsCriteriaDerived`-only diff in §1.2). Flagging this for the extraction plan: if the shared service boundary in §3 is adopted, this third copy is an equally valid (arguably higher-value, since it's a third duplicate of the exact same query) retrofit target alongside the two named handlers. Not fixing it — flagging per the scope-discipline convention, since it's new information discovered during recon that affects how many call sites the extraction should target.

## 7. Existing test coverage protecting the retrofit

`ScheduleTargetingTests.cs` ([tests/QuantumBuild.Tests.Integration/ToolboxTalks/ScheduleTargetingTests.cs](../tests/QuantumBuild.Tests.Integration/ToolboxTalks/ScheduleTargetingTests.cs)) contains 7 `[Fact]` tests, all exercising the **create** path only (`POST /api/toolbox-talks/schedules`, which routes to `CreateToolboxTalkScheduleCommandHandler`) — none exercise `UpdateToolboxTalkScheduleCommandHandler` or `ProcessToolboxTalkScheduleCommandHandler`'s refresh path directly:

| Test | Line | Verifies |
|---|---|---|
| `CreateSchedule_TargetDepartment_AssignsExactlyActiveDepartmentMembers` | [16](../tests/QuantumBuild.Tests.Integration/ToolboxTalks/ScheduleTargetingTests.cs#L16) | Department target resolves to exactly its active members; excludes inactive member and outsider |
| `CreateSchedule_TargetSite_AssignsExactlySiteMembers` | [50](../tests/QuantumBuild.Tests.Integration/ToolboxTalks/ScheduleTargetingTests.cs#L50) | Site target resolves to exactly its members; excludes outsider |
| `CreateSchedule_DepartmentAndSiteTargets_UnionDeduplicated` | [80](../tests/QuantumBuild.Tests.Integration/ToolboxTalks/ScheduleTargetingTests.cs#L80) | Dept ∪ site union, employee in both counted once (`AssignmentCount == 3` for dept-only + site-only + both) |
| `CreateSchedule_ExplicitEmployeePlusDepartmentTarget_UnionDeduplicated` | [113](../tests/QuantumBuild.Tests.Integration/ToolboxTalks/ScheduleTargetingTests.cs#L113) | Explicit `EmployeeIds` ∪ criteria-derived, no duplicate when the same employee is both explicit and department-matched |
| `CreateSchedule_AssignToAllEmployeesWithDepartmentTarget_IgnoresDepartmentAndAssignsEveryone` | [144](../tests/QuantumBuild.Tests.Integration/ToolboxTalks/ScheduleTargetingTests.cs#L144) | `AssignToAllEmployees=true` overrides/ignores a simultaneous department target |
| `CreateSchedule_DepartmentFromAnotherTenant_ReturnsBadRequest` | [182](../tests/QuantumBuild.Tests.Integration/ToolboxTalks/ScheduleTargetingTests.cs#L182) | Foreign-tenant department ID rejected (400) |
| `CreateSchedule_SupervisorTargetsDepartment_OnlyReachesAssignedOperators` | [204](../tests/QuantumBuild.Tests.Integration/ToolboxTalks/ScheduleTargetingTests.cs#L204) | Supervisor + department target intersects with `GetAssignedOperatorIdsAsync` result — in-department-but-unassigned employee excluded |

**Coverage gap for the retrofit:** `tests/QuantumBuild.Tests.Integration/ToolboxTalks/SchedulingTests.cs` also exists and does exercise the process (`POST /api/toolbox-talks/schedules/{id}/process`) and update (`PUT /api/toolbox-talks/schedules/{id}`) endpoints — e.g. `ProcessSchedule_CreatesScheduledTalks` ([SchedulingTests.cs:355](../tests/QuantumBuild.Tests.Integration/ToolboxTalks/SchedulingTests.cs#L355)), `ProcessSchedule_RecurringWeekly_SetsNextRunDate` ([SchedulingTests.cs:383](../tests/QuantumBuild.Tests.Integration/ToolboxTalks/SchedulingTests.cs#L383)), `UpdateSchedule_ModifyNotes_ReturnsOk` ([SchedulingTests.cs:496](../tests/QuantumBuild.Tests.Integration/ToolboxTalks/SchedulingTests.cs#L496)), `UpdateSchedule_WithDateOnlyScheduledDateString_Succeeds` ([SchedulingTests.cs:573](../tests/QuantumBuild.Tests.Integration/ToolboxTalks/SchedulingTests.cs#L573)) — but every one of these builds its schedule with `EmployeeIds` only (explicit assignment), never `TargetDepartmentIds`/`TargetSiteIds`. Confirmed by grep: `TargetDepartmentIds`/`TargetSiteIds` do not appear anywhere in `SchedulingTests.cs`, and none of its `ProcessSchedule_*` tests create a recurring (`Weekly`/`Monthly`/`Annually`) schedule *targeted by department or site* and then process it a second time to actually trigger `RefreshAssignmentsForTargetCriteria` — `ProcessSchedule_RecurringWeekly_SetsNextRunDate` only asserts on the *first* process call's `NextRunDate`, it never calls `/process` again to exercise the refresh branch at all (recurring + `AssignToAllEmployees=false` + department/site target + zero unprocessed assignments is the specific condition at [ProcessToolboxTalkScheduleCommandHandler.cs:84-90](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs#L84-L90) that invokes the second named block).

So: **zero existing tests, in either file, exercise `RefreshAssignmentsForTargetCriteria` (§1.2) or the department/site expansion inside `UpdateToolboxTalkScheduleCommandHandler` (§6) at all.** `ScheduleTargetingTests.cs`'s 7 tests protect only the create-time resolution (§1.1). If the shared-service extraction retrofits the process-refresh and update call sites, there is currently no automated regression coverage for either — new tests analogous to `ScheduleTargetingTests.cs` (department-only, site-only, union, explicit+criteria union, supervisor intersection) would need to be added for the recurring-refresh and update paths as part of that retrofit to have the same safety net the create path already has.
