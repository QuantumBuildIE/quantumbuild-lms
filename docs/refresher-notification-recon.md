# Refresher Notification at ScheduledTalk Creation — Recon

Date: 2026-07-08
Scope: read-only recon, no code changes. Verified against code at HEAD on `transval`, one week after `docs/scheduled-refresh-flow-recon.md`. Every claim below was re-verified by reading the current files, not by citing the prior recon.

## Headline

**Mirror with caveats.** `RefresherSchedulingService` gaining `IToolboxTalkEmailService` + `ICoreDbContext` and calling `SendTalkAssignmentEmailAsync` / `SendCourseAssignmentEmailAsync` once per path is the right shape, but three things the fix has to accommodate beyond "add a call":

1. The two *existing* initial-assignment paths don't agree with each other on save/send ordering — one sends before `SaveChangesAsync`, the other sends after. "Mirror the pattern" is ambiguous until you pick which one.
2. `SendTalkAssignmentEmailAsync` (the method the standalone refresher would reuse) hard-dereferences `scheduledTalk.ToolboxTalk.Title`/`.Description` — a navigation property. The refresher's newly-created `ScheduledTalk` doesn't have this navigation explicitly assigned; it would rely on EF Core's automatic relationship fixup rather than an explicit assignment (the initial-assignment handler doesn't rely on fixup — it sets the navigation explicitly).
3. `RefresherSchedulingService` currently has no route to an `Employee` object at all — not via a loaded navigation, not via a parameter, not via a dependency. Both overloads only carry `EmployeeId`. A new `ICoreDbContext` dependency (or a passed-in `Employee`) is required, and `CourseProgressService` — one of the two callers — doesn't have an `Employee` in scope either, so threading it through call sites is not free.

None of this blocks the fix shape; it just means "same template, mirror the pattern" needs a explicit decision on which pattern, plus one extra Employee lookup.

## 1. Current gap confirmed

`RefresherSchedulingService.cs` (both overloads, full file read) has exactly two dependencies in its primary constructor:

```
public class RefresherSchedulingService(
    IToolboxTalksDbContext context,
    ILogger<RefresherSchedulingService> logger) : IRefresherSchedulingService
```

[RefresherSchedulingService.cs:9-11](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Services/RefresherSchedulingService.cs#L9). No `IToolboxTalkEmailService`, no `ICoreDbContext`, no other notification-capable dependency anywhere in the file. Confirmed independently — this is not a citation of the prior recon.

**Standalone creation site** (`ScheduleRefresherIfRequired(ScheduledTalk completedTalk, ...)`):

```csharp
var refresher = new ScheduledTalk
{
    Id = Guid.NewGuid(),
    TenantId = completedTalk.TenantId,
    ToolboxTalkId = completedTalk.ToolboxTalkId,
    EmployeeId = completedTalk.EmployeeId,
    RequiredDate = refresherDueDate.AddDays(-14),
    DueDate = refresherDueDate,
    Status = ScheduledTalkStatus.Pending,
    IsRefresher = true,
    OriginalScheduledTalkId = completedTalk.Id,
    RefresherDueDate = refresherDueDate,
};

context.ScheduledTalks.Add(refresher);
var saved = await context.SaveChangesAsync(ct);
```
[RefresherSchedulingService.cs:42-57](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Services/RefresherSchedulingService.cs#L42) — no email call after `SaveChangesAsync`, only a `logger.LogInformation` at line 59.

**Course creation site** (`ScheduleRefresherIfRequired(ToolboxTalkCourseAssignment completedAssignment, ...)`):

```csharp
var refresherAssignment = new ToolboxTalkCourseAssignment
{
    Id = Guid.NewGuid(),
    TenantId = completedAssignment.TenantId,
    CourseId = completedAssignment.CourseId,
    EmployeeId = completedAssignment.EmployeeId,
    AssignedAt = DateTime.UtcNow,
    DueDate = refresherDueDate,
    Status = CourseAssignmentStatus.Assigned,
    IsRefresher = true,
    OriginalAssignmentId = completedAssignment.Id,
    RefresherDueDate = refresherDueDate,
};

// per-item ScheduledTalk creation loop (courseItems.OrderBy(ci => ci.OrderIndex))
...
context.ToolboxTalkCourseAssignments.Add(refresherAssignment);
var saved = await context.SaveChangesAsync(ct);
```
[RefresherSchedulingService.cs:94-129](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Services/RefresherSchedulingService.cs#L94) — same shape, no email call, only `logger.LogInformation` at line 131.

Gap is real and confirmed by direct reading, not inference.

## 2. Initial-assignment pattern — standalone (`ProcessToolboxTalkScheduleCommandHandler`)

- **Interface:** `IToolboxTalkEmailService` — injected as `_emailService` in the constructor [ProcessToolboxTalkScheduleCommandHandler.cs:17,23,28](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs#L17).
- **Method:** `SendTalkAssignmentEmailAsync` — confirmed, not a different name.
- **Signature:** `Task SendTalkAssignmentEmailAsync(ScheduledTalk scheduledTalk, Employee employee, CancellationToken cancellationToken = default)` [IToolboxTalkEmailService.cs:14-17](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Services/IToolboxTalkEmailService.cs#L14). Two domain objects, no extra flags — **no boolean/enum parameter distinguishing "new assignment" vs "refresher."**
- **Ordering vs `SaveChangesAsync` — email fires BEFORE the save.** The call site is inside the per-assignment `foreach` loop:
  ```csharp
  _dbContext.ScheduledTalks.Add(scheduledTalk);
  assignment.IsProcessed = true;
  assignment.ProcessedAt = now;
  talksCreated++;

  if (employees.TryGetValue(assignment.EmployeeId, out var employee))
  {
      scheduledTalk.ToolboxTalk = schedule.ToolboxTalk;   // explicit nav fixup, not relying on EF auto-fixup
      try
      {
          await _emailService.SendTalkAssignmentEmailAsync(scheduledTalk, employee, cancellationToken);
      }
      catch (Exception ex) { _logger.LogError(...); /* continue */ }
  }
  ```
  [ProcessToolboxTalkScheduleCommandHandler.cs:121-146](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs#L121), and the single `await _dbContext.SaveChangesAsync(cancellationToken);` for the whole batch happens later, outside and after the loop, at [line 191](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs#L191). So the email for row N is sent before that row (or any later row in the same batch) is actually persisted. If `SaveChangesAsync` throws for any reason after emails have gone out for some assignments, those emails were sent for rows that never committed.
- **Exception handling:** wrapped in try/catch, logs via `_logger.LogError`, comment explicitly says "Continue processing - don't fail the entire operation due to email failure" — **best-effort, not transactional.** A failed email does not fail the assignment.

## 3. Initial-assignment pattern — course (`AssignCourseCommandHandler`)

- **Method:** `SendCourseAssignmentEmailAsync` — confirmed, not a different name.
- **Signature:** `Task SendCourseAssignmentEmailAsync(ToolboxTalkCourse course, Employee employee, int talkCount, DateTime? dueDate, CancellationToken cancellationToken = default)` [IToolboxTalkEmailService.cs:57-62](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Services/IToolboxTalkEmailService.cs#L57). Takes the `ToolboxTalkCourse` object directly (not via a `ScheduledTalk.Course` navigation) plus a `talkCount` int and an optional `dueDate` — **this method has no per-item shape at all; it is inherently one-call-per-course.**
- **Ordering vs `SaveChangesAsync` — opposite of the standalone path: `SaveChangesAsync` fires FIRST, then the email loop runs after.**
  ```csharp
  // ... assignment + scheduledTalk creation loop ...
  await _dbContext.SaveChangesAsync(cancellationToken);   // line 177

  // Send email notifications to each assigned employee
  foreach (var assignmentDto in eligibleAssignments)
  {
      var employee = employeeLookup[assignmentDto.EmployeeId];
      var talkCount = ...;
      try
      {
          await _emailService.SendCourseAssignmentEmailAsync(course, employee, talkCount, dto.DueDate, cancellationToken);
      }
      catch (Exception ex) { _logger.LogError(...); /* continue */ }
  }
  ```
  [AssignCourseCommandHandler.cs:177-203](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Features/CourseAssignments/Commands/AssignCourseCommandHandler.cs#L177). This is the "safe" ordering — email only sent for rows that are confirmed persisted.
- **Exception handling:** identical try/catch-and-continue pattern to the standalone path — best-effort, not transactional.
- **One email per course, confirmed** — the loop is `foreach (assignmentDto in eligibleAssignments)` (one per *employee*), not per scheduled talk item; `talkCount` is passed as a number, and the course items themselves never appear in the email loop. This directly answers §8: initial course assignment is one-email-per-course-per-employee, not one-per-item.

**Divergence to flag:** the two existing handlers disagree on save/send ordering. Standalone sends-before-save (a live, if minor, existing quirk); course sends-after-save (the safer pattern). "Mirror the existing pattern" therefore doesn't resolve to one answer — see §5 for what this means for the refresher paths specifically.

## 4. `RefresherSchedulingService`'s current constructor and DI registration

Constructor (repeated for clarity): `IToolboxTalksDbContext context, ILogger<RefresherSchedulingService> logger`. No email, no Core module dependency.

DI registration: `services.AddScoped<IRefresherSchedulingService, Services.RefresherSchedulingService>();` [DependencyInjection.cs:36](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/DependencyInjection.cs#L36) — **Scoped.**

`IToolboxTalkEmailService` registration: `services.AddScoped<IToolboxTalkEmailService, ToolboxTalkEmailService>();` [ServiceCollectionExtensions.cs:57](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/ServiceCollectionExtensions.cs#L57) — **Scoped.**

Both scoped, both resolvable from the same scope — no lifetime mismatch. Adding `IToolboxTalkEmailService` (and, per §7, `ICoreDbContext`, which is already consumed elsewhere as scoped by both handlers in §2/§3) to `RefresherSchedulingService`'s constructor is a trivial DI change with no lifetime conflict.

## 5. Ordering — which pattern applies, and where the call would sit

**Confirmed shape:** `RefresherSchedulingService`'s own `SaveChangesAsync` is a separate database round-trip from the completion's `SaveChangesAsync` in both callers — this is **not** shape (a) from the task brief (single shared transaction), it's shape (b) (two independent saves).

- **Standalone path:** `CompleteToolboxTalkCommandHandler.Handle` calls `_dbContext.SaveChangesAsync(cancellationToken)` at [line 197](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/CompleteToolboxTalk/CompleteToolboxTalkCommandHandler.cs#L197) (persists the completion + status change), and only afterward, at [line 207](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/CompleteToolboxTalk/CompleteToolboxTalkCommandHandler.cs#L207), calls `await _refresherSchedulingService.ScheduleRefresherIfRequired(scheduledTalk, cancellationToken);` — which then does its own `SaveChangesAsync` inside `RefresherSchedulingService` (line 57). Two separate saves, sequential, same `IToolboxTalksDbContext` scoped instance but two distinct round-trips (not wrapped in an explicit `BeginTransaction`/`IDbContextTransaction` — none found anywhere in this call chain).
- **Course path:** `CourseProgressService.UpdateProgressAsync` calls its own `dbContext.SaveChangesAsync(cancellationToken)` at [line 62](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Services/CourseProgressService.cs#L62) (persists the assignment status transition to `Completed`), then at [line 67](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Services/CourseProgressService.cs#L67) calls `await refresherSchedulingService.ScheduleRefresherIfRequired(assignment, cancellationToken);`, which does its own third-in-sequence `SaveChangesAsync` (line 129 in `RefresherSchedulingService`). Same two-separate-saves shape.

**Where the notification call would sit:** given `RefresherSchedulingService` already does its row creation + `SaveChangesAsync` as one atomic unit inside each overload, the natural (and safest — matches the course-assignment pattern, not the standalone-assignment pattern) insertion point is **immediately after** each overload's own `SaveChangesAsync` call (after line 57 for the standalone overload, after line 129 for the course overload) — i.e. send only once the refresher row(s) are confirmed persisted. This deliberately does **not** mirror `ProcessToolboxTalkScheduleCommandHandler`'s send-before-save quirk; it mirrors `AssignCourseCommandHandler`'s send-after-save pattern, which is the safer of the two existing behaviors and requires no new risk to be introduced.

## 6. Best-effort vs transactional

Both existing initial-assignment call sites wrap the email call in `try/catch`, log via `ILogger.LogError` on failure, and explicitly continue — the assignment/course-assignment operation never fails because of an email failure. This is a firm, consistent pattern across both existing paths (the only place they disagree is ordering, not error-handling philosophy).

**Refresher notification should follow the identical best-effort pattern** — wrap in try/catch, log, swallow, never let a failed refresher-notification email roll back or fail the already-committed refresher row. This requires `RefresherSchedulingService` to also take an `ILogger` for this specific failure path (it already has one for its existing `LogInformation` calls, so no new dependency needed there).

## 7. Employee data availability

**Neither overload of `ScheduleRefresherIfRequired` currently has an `Employee` object in scope — only `EmployeeId`.** Confirmed by re-reading the full file: `completedTalk.EmployeeId` and `completedAssignment.EmployeeId` are referenced throughout, but neither parameter type (`ScheduledTalk`, `ToolboxTalkCourseAssignment`) is loaded with an `Employee` navigation anywhere in this service, and no `Employee` parameter exists on either public method.

Checking the callers doesn't help uniformly:

- **`CompleteToolboxTalkCommandHandler`** *does* have an `Employee` object in scope — loaded at [lines 49-53](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/CompleteToolboxTalk/CompleteToolboxTalkCommandHandler.cs#L49) as local variable `employee` (matched via `UserId`), and the `scheduledTalk` passed to `ScheduleRefresherIfRequired` is loaded via `.Include(st => st.SectionProgress).Include(st => st.QuizAttempts).Include(st => st.Completion).Include(st => st.ToolboxTalk).ThenInclude(t => t.Sections)` [lines 61-69](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/CompleteToolboxTalk/CompleteToolboxTalkCommandHandler.cs#L61) — **no `.Include(st => st.Employee)`**, so `scheduledTalk.Employee` itself is null; only the separately-loaded local `employee` variable is populated.
- **`CourseProgressService.UpdateProgressAsync`** — the `assignment` is loaded via `.Include(a => a.ScheduledTalks).Include(a => a.Course).ThenInclude(c => c.CourseItems)` [lines 16-20](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Services/CourseProgressService.cs#L16) — **no `Employee` include, and no separate `Employee` local variable exists anywhere in this method.** Only `assignment.EmployeeId` is ever referenced. This caller has strictly less Employee data available than the standalone path.

**Conclusion:** the cleanest fix does not thread `Employee` through either call site's signature (that would require changing `CourseProgressService.UpdateProgressAsync`'s signature and its own caller, `CompleteToolboxTalkCommandHandler.cs:202`, plus the standalone call site) — it instead gives `RefresherSchedulingService` a new `ICoreDbContext` dependency and has each overload look up `Employee` itself by `EmployeeId` (a single `FirstOrDefaultAsync` on `context.Employees`, following the exact query shape already used in [ProcessToolboxTalkScheduleCommandHandler.cs:65-67](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs#L65) and [AssignCourseCommandHandler.cs:60-62](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Features/CourseAssignments/Commands/AssignCourseCommandHandler.cs#L60)). This keeps both `ScheduleRefresherIfRequired` signatures unchanged and requires no caller-side changes in `CompleteToolboxTalkCommandHandler` or `CourseProgressService`.

**Separate, real risk found while checking this:** `SendTalkAssignmentEmailAsync` dereferences `scheduledTalk.ToolboxTalk.Title` and `.Description` directly [ToolboxTalkEmailService.cs:80-81](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ToolboxTalkEmailService.cs#L80) — a navigation property, not a separate parameter. In the standalone `ScheduleRefresherIfRequired` overload, the `talk` variable (the `ToolboxTalk` entity) is loaded in the same `DbContext` at line 16-17, and the new `refresher` `ScheduledTalk`'s `ToolboxTalkId` matches `talk.Id`, but **the code never explicitly assigns `refresher.ToolboxTalk = talk`** the way `ProcessToolboxTalkScheduleCommandHandler.cs:133` does defensively (`scheduledTalk.ToolboxTalk = schedule.ToolboxTalk;` immediately before its email call). EF Core's automatic relationship fixup would likely populate this navigation once both entities are tracked in the same context and the FK matches, but the existing initial-assignment code doesn't rely on that fixup — it assigns explicitly. The refresher implementation should do the same (explicit assignment) rather than assume fixup, to avoid a null-reference risk that would only surface at runtime, not at compile time.

## 8. Course path — one email per course, confirmed

`SendCourseAssignmentEmailAsync`'s signature takes the `ToolboxTalkCourse` object plus a `talkCount` int directly — there is no per-item overload and no per-`ScheduledTalk` parameter at all. `AssignCourseCommandHandler`'s email loop iterates `eligibleAssignments` (one per employee), calling this method exactly once per employee regardless of how many course items exist (confirmed at [AssignCourseCommandHandler.cs:180-203](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Features/CourseAssignments/Commands/AssignCourseCommandHandler.cs#L180)).

The course refresher path in `RefresherSchedulingService` already has the `course` object in scope (loaded at [line 67](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Services/RefresherSchedulingService.cs#L67) with `.Include(c => c.CourseItems.Where(ci => !ci.IsDeleted))`) and computes `courseItems` as a local list (line 109) — so the refresher path can call `SendCourseAssignmentEmailAsync(course, employee, courseItems.Count, refresherAssignment.DueDate, ct)` exactly **once**, after the per-item `ScheduledTalk` creation loop, mirroring the "one per course" behavior exactly. No spam risk for multi-item courses; this matches what the initial course assignment already does, and is a straightforward mirror with no ambiguity (unlike §5's ordering question).

## 9. Tenant-setting gating

**None found.** `ToolboxTalkSettings.cs` (full file read) has no flag gating `SendTalkAssignmentEmailAsync` or `SendCourseAssignmentEmailAsync`. The only notification-related toggles on the entity are four booleans — `NotifyOnTranslationComplete`, `NotifyOnValidationComplete`, `NotifyOnFailure`, `NotifyOnExternalReviewResponse` [ToolboxTalkSettings.cs:117-120](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Entities/ToolboxTalkSettings.cs#L117) — and per their doc comment these are **Admin-recipient** notifications (translation/validation pipeline status), unrelated to employee-facing assignment emails. Neither `ToolboxTalkEmailService.SendTalkAssignmentEmailAsync` nor `SendCourseAssignmentEmailAsync` reads `ToolboxTalkSettings` at all — the only tenant-level lookup either method performs is `GetTeamNameAsync` (branding string for the email footer, via `ITenantSettingsService`, not `ToolboxTalkSettings`). No existing gate to respect; none needs to be added to match existing behavior.

## Recommended implementation shape (one paragraph, no code)

`RefresherSchedulingService`'s constructor gains two dependencies — `ICoreDbContext coreContext` and `IToolboxTalkEmailService emailService` (both already registered as Scoped, no lifetime conflict). In the standalone overload, immediately after `var saved = await context.SaveChangesAsync(ct);` (current line 57), explicitly set `refresher.ToolboxTalk = talk;` (mirroring the defensive fixup in `ProcessToolboxTalkScheduleCommandHandler.cs:133` rather than relying on EF auto-fixup), look up the `Employee` via `await coreContext.Employees.FirstOrDefaultAsync(e => e.Id == completedTalk.EmployeeId, ct)`, and — wrapped in the same try/catch-log-and-continue shape used at both existing call sites (§6) — call `await emailService.SendTalkAssignmentEmailAsync(refresher, employee, ct);` if the employee lookup succeeded. In the course overload, immediately after its own `var saved = await context.SaveChangesAsync(ct);` (current line 129), do the equivalent `Employee` lookup by `completedAssignment.EmployeeId`, then call `await emailService.SendCourseAssignmentEmailAsync(course, employee, courseItems.Count, refresherAssignment.DueDate, ct)` exactly once (not once per item), same try/catch shape. Both insertions sit after their respective `SaveChangesAsync`, deliberately following `AssignCourseCommandHandler`'s send-after-save pattern rather than `ProcessToolboxTalkScheduleCommandHandler`'s send-before-save pattern, since send-after-save is the safer of the two existing behaviors and there's no reason to import the riskier one into new code. No signature changes to `IRefresherSchedulingService`, `CompleteToolboxTalkCommandHandler`, or `CourseProgressService` are required.

## Notes / things worth flagging that aren't blockers

- **MailerSend 429 handling (BACKLOG §5.6/Medium)** — refresher notifications will go through the same `IEmailProvider` → MailerSend path as every other email in this codebase, and inherit its current behavior of silently dropping on HTTP 429 with no retry/backoff. Not a reason to hold this fix; just means a dropped refresher-notification email fails exactly as silently as a dropped initial-assignment email does today. Nothing new introduced, nothing existing fixed.
- **Employee-active/deleted scoping gap** — out of scope per the brief (this is item 2 on the boss list), but worth remembering that whatever `Employee` lookup is added in §7 should use the same tenant-scoped, non-deleted query shape already used by the two existing handlers (`e.TenantId == ... && !e.IsDeleted`) purely for consistency — not to fix the departed-employee gap documented in the prior recon, which remains open and unaffected by this change.
