# Course Assignment Targeting — Recon

Read-only recon. No code changed. Every claim below is backed by a `file:line` citation, verified directly against the working tree on `transval` (2026-08-14).

Purpose: before adding Department/Location targeting to COURSE assignment (mirroring the targeting already shipped for learning schedules — `ToolboxTalkSchedule`), establish exactly how course assignment works today, and the precise point where an expansion step would hook in.

---

## A. Course assignment flow as it is today

### A.1 Endpoint / controller / route / policy

`src/QuantumBuild.API/Controllers/ToolboxTalkCourseAssignmentsController.cs`

- Class-level: `[Route("api/toolbox-talks/course-assignments")]` (line 19), `[Authorize(Policy = "Learnings.View")]` (line 20).
- Assign action: `[HttpPost]` (line 70), `[Authorize(Policy = "Learnings.Manage")]` (line 71), `Assign([FromBody] AssignCourseDto dto)` (line 74).
- Builds `AssignCourseCommand { TenantId = _currentUserService.TenantId, Dto = dto }` (lines 78–82) and sends via MediatR (line 84).
- Effective route: `POST /api/toolbox-talks/course-assignments`, gated by `Learnings.Manage` (stacked on top of the class-level `Learnings.View`, per the class+action `[Authorize]` stacking rule in CLAUDE.md Note 24 — both must pass).
- `KeyNotFoundException` → 404 (lines 87–90), `InvalidOperationException` → 400 (lines 91–94).

Command wrapper: `AssignCourseCommand : IRequest<List<ToolboxTalkCourseAssignmentDto>>` with `TenantId`/`Dto` — `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Features/CourseAssignments/Commands/AssignCourseCommand.cs:6-10`.

### A.2 AssignCourseDto — explicit list only, no expansion

`src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Features/CourseAssignments/DTOs/AssignCourseDto.cs:5-26`:

```csharp
public record AssignCourseDto
{
    [Required]
    public Guid CourseId { get; init; }

    [Required]
    [MinLength(1)]
    public List<EmployeeCourseAssignmentDto> Assignments { get; init; } = new();

    public DateTime? DueDate { get; init; }
}

public record EmployeeCourseAssignmentDto
{
    [Required]
    public Guid EmployeeId { get; init; }

    /// <summary>
    /// Talk IDs to include. If null or empty, all talks are included.
    /// </summary>
    public List<Guid>? IncludedTalkIds { get; init; }
}
```

Confirmed: **no** `AssignToAllEmployees`, `TargetDepartmentIds`, or `TargetSiteIds` field anywhere on this DTO — target employees are supplied purely as an explicit `List<EmployeeCourseAssignmentDto>`. Only Data-Annotations validation (`[Required]`, `[MinLength(1)]`) is present — no FluentValidation validator exists for this DTO/command (see B.7).

### A.3 AssignCourseCommandHandler

`src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Features/CourseAssignments/Commands/AssignCourseCommandHandler.cs` (207 lines total, full file read and verified).

**No expansion branch anywhere in the file.** Employee IDs are taken directly from the DTO:

```csharp
// line 58
var employeeIds = dto.Assignments.Select(a => a.EmployeeId).Distinct().ToList();
```

**Step order in `Handle` (lines 32-206):**

1. Load course + active, non-deleted course items (lines 38-55). Throws if course not found (43-44), inactive (46-47), or has no talks (54-55).
2. Load employees for the distinct explicit ID list, tenant-scoped, non-deleted (lines 58-65). Throws `KeyNotFoundException` if any requested ID doesn't resolve (64-65).
3. **Already-assigned skip** — lines 67-82 (see A.3.1 below).
4. Per-eligible-employee loop (lines 90-175): creates one `ToolboxTalkCourseAssignment` + one `ScheduledTalk` per included course item (see A.3.2/A.3.3).
5. `SaveChangesAsync` (line 177).
6. Email notification loop, failures logged and swallowed, don't fail the whole operation (lines 180-203).

#### A.3.1 "Already assigned" skip logic (lines 67-82)

Runs **before** any row creation, computed once for the whole batch:

```csharp
// 3. Check for existing active assignments - skip employees who already have one
var existingEmployeeIds = await _dbContext.ToolboxTalkCourseAssignments
    .Where(a => a.CourseId == dto.CourseId
        && employeeIds.Contains(a.EmployeeId)
        && a.Status != CourseAssignmentStatus.Completed
        && !a.IsDeleted)
    .Select(a => a.EmployeeId)
    .ToListAsync(cancellationToken);

var existingEmployeeIdSet = existingEmployeeIds.ToHashSet();
var eligibleAssignments = dto.Assignments
    .Where(a => !existingEmployeeIdSet.Contains(a.EmployeeId))
    .ToList();

if (!eligibleAssignments.Any())
    throw new InvalidOperationException("All selected employees already have active assignments for this course");
```

An employee is skipped if they have **any** non-`Completed`, non-deleted `ToolboxTalkCourseAssignment` row for the same `CourseId` — i.e. `Assigned`, `InProgress`, or `Overdue` status blocks re-assignment; only a fully `Completed` prior assignment allows a new one. The rest of the loop (steps 4-6) iterates `eligibleAssignments`, not the raw `dto.Assignments`.

**Implication for expansion:** any department/location-expanded employee ID must be merged into the same `employeeIds`/`dto.Assignments`-equivalent collection **before** this check runs, so an expanded employee who already has an active assignment is silently skipped exactly like an explicitly-listed one — not bypassed.

#### A.3.2 `IncludedTalkIds` default behavior (lines 107-115)

```csharp
// Determine which talks to include
var itemsToInclude = courseItems;
if (assignmentDto.IncludedTalkIds != null && assignmentDto.IncludedTalkIds.Any())
{
    var includedSet = assignmentDto.IncludedTalkIds.ToHashSet();
    itemsToInclude = courseItems
        .Where(ci => includedSet.Contains(ci.ToolboxTalkId))
        .ToList();
}
```

`itemsToInclude` defaults to **all** `courseItems` and is only narrowed when `IncludedTalkIds` is both non-null and non-empty. So: unspecified/null `IncludedTalkIds` → all talks in the course, matching the XML doc comment on the DTO field (line 22-24 of the DTO file above).

**Implication for expansion:** department/location-expanded employees have no per-employee `IncludedTalkIds` value to supply. Since the existing default for "not specified" is already "all course talks," an expansion step can simply construct `EmployeeCourseAssignmentDto { EmployeeId = ..., IncludedTalkIds = null }` for expanded employees and get the natural "all talks" behavior with zero special-casing.

#### A.3.3 Per-employee entity creation

- `ToolboxTalkCourseAssignment` — lines 94-105, tenant-stamped (`TenantId = tenantId`, line 97), `Status = CourseAssignmentStatus.Assigned` (line 104).
- `ScheduledTalk` per included item — lines 119-151, loop over `itemsToInclude`, tenant-stamped (line 124), `CourseAssignmentId = assignment.Id` (line 132), `CourseOrderIndex = item.OrderIndex` (line 133), `Status = ScheduledTalkStatus.Pending` (line 131), `DueDate` falls back to `DateTime.UtcNow.AddDays(30)` when `dto.DueDate` is null (line 130).

### A.4 Preview endpoint — separate code path, does not share the assign contract

`POST /api/toolbox-talks/course-assignments/preview` — `[HttpPost("preview")]` at `src/QuantumBuild.API/Controllers/ToolboxTalkCourseAssignmentsController.cs:40`, method `GetAssignmentPreview` (line 43). Gated only by the controller-level `Learnings.View` policy — no additional `[Authorize]` attribute on the action (lines 40-43), so it does **not** require `Learnings.Manage`.

Request shape is a flat DTO local to the controller file, distinct from `AssignCourseDto`:

```csharp
// lines 12-16
public class GetAssignmentPreviewRequest
{
    public Guid CourseId { get; set; }
    public List<Guid> EmployeeIds { get; set; } = new();
}
```

No `IncludedTalkIds`, no `DueDate`. Handled by `GetCourseAssignmentPreviewQueryHandler` (`src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Features/CourseAssignments/Queries/GetCourseAssignmentPreviewQueryHandler.cs`) — a pure read that loads the course+items and the named employees independently and reports `AlreadyCompleted`/`CompletedAt` per employee/talk pair from `ScheduledTalks`. It does **not** run the active-assignment skip check from A.3.1, creates nothing, and shares no code with `AssignCourseCommandHandler` beyond independently loading the same `Course`/`CourseItems`/`Employees` shapes.

---

## B. Supervisor restriction and tenant safety for course assignment

### B.5 Supervisor scoping — none exists today

`AssignCourseCommandHandler`'s constructor injects only `IToolboxTalksDbContext`, `ICoreDbContext`, `IToolboxTalkEmailService`, `ILogger` (lines 20-30) — no `ISupervisorAssignmentService`, no `ICurrentUserService`, no role checks anywhere in the 207-line file (confirmed by full read).

The controller's `Assign` action (lines 74-100) likewise performs no supervisor-scope check — it only enforces the `Learnings.Manage` policy attribute (line 71), which is a coarse permission gate, not a data-scoping gate.

**Contrast with schedule creation**, which does restrict supervisor callers — see C.8 below (`CreateToolboxTalkScheduleCommandHandler.cs:49-76`).

**Gap:** any caller holding `Learnings.Manage` — which includes the Supervisor role per CLAUDE.md's role table (`Learnings.Schedule` maps to scheduling; `Learnings.Manage` policy composition wasn't traced further in this recon and should be confirmed against `Permissions.cs`/policy config before relying on this) — can today assign a course to **any** employee ID in the tenant, not just their own operators. This must be preserved as a known gap or closed by the targeting chunk, mirroring the schedule flow's restriction.

### B.6 Tenant scoping — enforced explicitly in handler code, plus EF global filters as defense-in-depth

Explicit `Where` clauses in the handler:

- Course lookup: `c.TenantId == tenantId` — `AssignCourseCommandHandler.cs:41`.
- Employee lookup: `e.TenantId == tenantId` — `AssignCourseCommandHandler.cs:61`.
- New rows stamped `TenantId = tenantId` — assignment (line 97), scheduled talk (line 124).

EF Core global query filters also apply (defense-in-depth, per the CLAUDE.md Note 14 cross-tenant fix pattern) — `src/Core/QuantumBuild.Core.Infrastructure/Data/ApplicationDbContext.cs`:

```
339: modelBuilder.Entity<Employee>().HasQueryFilter(e => !e.IsDeleted && (BypassTenantFilter || e.TenantId == TenantId));
359: modelBuilder.Entity<ToolboxTalkCourse>().HasQueryFilter(e => !e.IsDeleted && (BypassTenantFilter || e.TenantId == TenantId));
371: modelBuilder.Entity<ToolboxTalkCourseAssignment>().HasQueryFilter(e => !e.IsDeleted && (BypassTenantFilter || e.TenantId == TenantId));
```

So tenant safety for course assignment is real and not solely reliant on the (dead) validator layer — it is enforced both explicitly in the handler and via global query filters.

### B.7 FluentValidation validator — does not exist for course assignment; and even where validators exist elsewhere, they don't auto-execute

No validator class exists for `AssignCourseCommand`/`AssignCourseDto`. The `CourseAssignments` feature folder contains no `*Validator.cs` file (compared with e.g. `CreateToolboxTalkScheduleCommandValidator.cs`, which does exist for the schedule flow).

More generally, and directly relevant to whether adding a validator would even help: there is **no MediatR pipeline behavior** wiring FluentValidation into request handling anywhere in the codebase. A repo-wide search for `IPipelineBehavior`/`ValidationBehavior`/`ValidationBehaviour` returns zero matches. DI registration is:

```
src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/DependencyInjection.cs:24:  services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/DependencyInjection.cs:27:  services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
```

`AddValidatorsFromAssembly` only registers `IValidator<T>` in DI — nothing invokes it automatically. Handlers that do validate call `_validator.ValidateAsync(...)`/`ValidateAndThrowAsync(...)` manually inside `Handle` (e.g. `UpdateToolboxTalkTenantDefaultsCommandHandler.cs:32`, `InitialiseToolboxTalkCommandHandler.cs:33`). `CreateToolboxTalkScheduleCommandHandler` has **no injected `IValidator` field at all** — so even its existing `CreateToolboxTalkScheduleCommandValidator` is never invoked in that flow; the `catch (FluentValidation.ValidationException ex)` blocks in `ToolboxTalkSchedulesController.cs:149,189` are dead/defensive code for that command.

**Conclusion for course targeting:** there is no validator today, and this codebase's established pattern means adding one would require manually calling it inside `AssignCourseCommandHandler.Handle` — no automatic pipeline exists to invoke it otherwise. Tenant/supervisor safety must be (and currently is, for tenant scoping only) enforced directly in handler code, not via a validator.

---

## C. Learning schedule targeting (for comparison — already shipped)

### C.8 Department/Location → employee-list resolution

`src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/CreateToolboxTalkSchedule/CreateToolboxTalkScheduleCommandHandler.cs`, `Handle` (lines 32-207).

**Supervisor scoping** (lines 49-76):

```csharp
// line 49
var isSupervisorOnly = !_currentUserService.IsSuperUser
    ...
```

- `AssignToAllEmployees` is rejected outright for supervisor callers (lines 61-63).
- `supervisorPermittedIds` is populated from `_supervisorAssignmentService` (assigned-operators lookup) when `isSupervisorOnly` (lines 53-70).
- Explicit `request.EmployeeIds` outside the permitted set are rejected (line 72: `request.EmployeeIds.Except(supervisorPermittedIds)`).

**Tenant-scoped validation of criteria** (lines 109-135): `TargetDepartmentIds` validated against `_coreDbContext.Departments.Where(d => d.TenantId == request.TenantId && !d.IsDeleted && ...)` (lines 109-116); `TargetSiteIds` validated the same way against `Sites` (lines 123-130).

**Expansion — union, dedup, active-only, tenant-scoped, supervisor-intersected** (lines 138-156):

```csharp
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
```

(Verified directly: `CreateToolboxTalkScheduleCommandHandler.cs:138-156`.) Union is OR-of-department-or-site match, deduped via `HashSet`/`.Union`, filtered to `IsActive && !IsDeleted`, tenant-scoped (`e.TenantId == request.TenantId`), and — critically — when the caller is a supervisor, the expanded set is intersected with `supervisorPermittedIds` (line 150) so a supervisor cannot reach employees outside their team even through a department/location target.

Resulting assignment rows are flagged `IsCriteriaDerived = criteriaDerivedIds.Contains(employeeId)` (line 197) to distinguish explicit vs. expanded membership — this flag is what later re-evaluation (C.10) uses to know which rows it's allowed to add/remove automatically.

### C.9 Reusability of the expansion logic

**Not currently an extracted, shared service.** No `IEmployeeTargetingService` (or similarly named) interface/class exists anywhere in the repo (confirmed: zero matches on a repo-wide search).

The expansion logic is:
1. **Inline** in `CreateToolboxTalkScheduleCommandHandler.Handle` (lines 79-162 as shown above), and
2. **Duplicated** (re-written, not called-into) in `ProcessToolboxTalkScheduleCommandHandler.RefreshAssignmentsForTargetCriteria` — `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs:281-286`:

```csharp
var currentTargetEmployeeIds = (await _coreDbContext.Employees
    .Where(e => e.TenantId == tenantId && e.IsActive && !e.IsDeleted
        && ((e.DepartmentId.HasValue && targetDepartmentIds.Contains(e.DepartmentId.Value))
            || (e.PrimarySiteId.HasValue && targetSiteIds.Contains(e.PrimarySiteId.Value))))
    .Select(e => e.Id)
    .ToListAsync(cancellationToken)).ToHashSet();
```

The query itself depends only on `ICoreDbContext.Employees/Departments/Sites` (Core-module, not ToolboxTalks-specific types) and `ISupervisorAssignmentService` (also Core: `src/Core/QuantumBuild.Core.Application/Features/Employees/ISupervisorAssignmentService.cs`) — so nothing in the query is ToolboxTalks-coupled and it is conceptually generic enough to extract. But as written today it is copy-pasted inline code in two places, not a reusable component. A course-targeting chunk has two options: duplicate the same inline pattern a third time (matching existing codebase style), or extract a shared helper as a small preparatory step — this recon does not judge which; it only establishes that no reusable service exists yet.

### C.10 Recurring/re-evaluation for schedules

Yes — schedules have a recurring re-evaluation concept that courses do not (see D.11).

`ProcessToolboxTalkSchedulesJob.ExecuteAsync` (`src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/ProcessToolboxTalkSchedulesJob.cs`) is a Hangfire recurring job registered daily in `src/QuantumBuild.API/Program.cs:454-458`. It finds `Active` schedules due today (lines 57-63 of the job file) and dispatches `ProcessToolboxTalkScheduleCommand` per schedule (lines 75-79).

`ProcessToolboxTalkScheduleCommandHandler.Handle` (lines 32-208 of the same-named handler file):
- For `AssignToAllEmployees` + recurring schedules with no unprocessed assignments: calls `RefreshAssignmentsForAllEmployees` (lines 76-80; body 210-264) to pick up newly-hired active employees and drop now-inactive ones.
- For department/site-targeted + recurring schedules: calls `RefreshAssignmentsForTargetCriteria` (lines 82-90; body 266-333), which re-runs the expansion query, adds newly-qualifying employees (295-310), and removes previously-criteria-derived employees who no longer qualify — while rows with `IsCriteriaDerived == false` (explicit assignments) are left untouched (comment at 267-271, logic at 312-325).
- Next run date computed by `CalculateNextRunDate` (335-344); schedule status/`NextRunDate` updated (158-198) and assignments reset to unprocessed for the next cycle when the schedule continues (192-196).

---

## D. Course assignment recurrence

### D.11 No recurring/re-evaluation concept for courses — confirmed via code, not assumption

- `Glob` of `src/Modules/ToolboxTalks/**/Jobs/*.cs` lists 20 Hangfire job classes; the only assignment-related one is `ProcessToolboxTalkSchedulesJob.cs`, which is schedule-specific and never touches `ToolboxTalkCourseAssignment`.
- `AssignCourseCommandHandler.Handle` runs entirely synchronously within the single request: load → validate → skip-check → create rows → `SaveChangesAsync` (line 177) → send emails → return. No job is enqueued.
- `ToolboxTalkCourseAssignment` (`src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Entities/ToolboxTalkCourseAssignment.cs`, fields lines 14-41) has no `NextRunDate`/`Frequency`/recurrence field, and no `IsCriteriaDerived` flag — nothing analogous to `ToolboxTalkSchedule`'s recurrence machinery exists on this entity.
- The only other creator of `ToolboxTalkCourseAssignment` rows is `AutoAssignmentService.AssignNewEmployeeTrainingAsync` (`src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Services/AutoAssignmentService.cs:27-148`) — a one-shot "assign on hire" hook fired synchronously at new-employee-creation time for courses flagged `AutoAssignToNewEmployees` (lines 43-47, 51-101). It does not use Department/Site targeting at all and is not a recurring job.

**Conclusion:** course assignment is strictly a one-time synchronous operation, triggered either by the `POST /api/toolbox-talks/course-assignments` API call or by the new-employee auto-assign hook. There is no background job, no `IsCriteriaDerived`-style flag, and no re-evaluation concept comparable to `ProcessToolboxTalkSchedulesJob`. This means a department/location expansion for courses, once resolved to an employee list at the moment of the assign call, needs **no** re-evaluation/refresh design — it only needs to feed the existing per-employee assignment loop once, at call time. (If a "catch newly-matching employees later" behavior is desired for courses, that would be new scope beyond mirroring current schedule behavior, since even schedules only get this via their own dedicated recurring job.)

---

## Summary for the next chunk

- **Hook point:** a department/location expansion step belongs immediately before step 2 (employee load) / step 3 (already-assigned skip) in `AssignCourseCommandHandler.Handle` — i.e. resolve `TargetDepartmentIds`/`TargetSiteIds` (new fields, not yet on `AssignCourseDto`) to a set of employee IDs, union with the explicit `dto.Assignments` employee IDs, and feed the combined, deduped set through the **existing** `employeeIds`/already-assigned-skip/creation loop unchanged. `EmployeeCourseAssignmentDto.IncludedTalkIds = null` for expansion-derived employees naturally yields "all course talks" via the existing default (A.3.2) — no special-casing needed.
- **Reuse potential:** the department/site → employee-ID expansion query used by schedules (C.8) is generic (Core-module types only) but not extracted into a shared service (C.9) — it's duplicated inline in two schedule-handler locations today. Course targeting will either add a third inline copy or be the trigger to finally extract a shared helper; this recon does not decide which.
- **Safety gap to close/mirror:** course assignment today has **no supervisor scoping at all** (B.5) — unlike schedules, which intersect expanded criteria with `supervisorPermittedIds` (C.8). Tenant scoping is already handled correctly and explicitly in handler code (B.6), so that part is safe to mirror as-is. Any validator added for course targeting must be manually invoked inside the handler to actually run — there is no MediatR validation pipeline in this codebase (B.7).
- **No recurrence design needed:** courses have no recurring re-evaluation today and this recon found nothing requiring one be added to match schedule behavior exactly (D.11) — schedules' recurring refresh is itself an extra feature of `ToolboxTalkSchedule`, not a baseline that all targeting must replicate.
