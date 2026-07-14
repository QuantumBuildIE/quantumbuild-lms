# Reminder Employee Scoping — Recon

Date: 2026-07-08
Scope: read-only recon, no code changes. Verified against code at HEAD on `transval` (commit `08a1801`). Builds on `docs/scheduled-refresh-flow-recon.md` (2026-07-08) and `docs/refresher-notification-recon.md` (2026-07-08, same day) — every claim below was re-verified by reading current files, not by citing either prior recon.

## Headline

**Filter on `!IsDeleted` only — semantics clear and consistent, and this is the codebase's own current precedent, not a guess.** `Employee.IsActive` has a real, coherent meaning in this codebase, but it is *not* "should this person currently receive communications" — it is "is this employee eligible to be selected for something new" (schedules, supervisor assignment, reports, QR PIN issuance). The code that decides "should we email this employee about something that already exists" — including `RefresherSchedulingService`, committed *today* in the same session that produced the prior two recons — consistently filters on tenant + `!IsDeleted` only, never `IsActive`. Adding `IsActive` to the reminder-job filter would not be "extra safety," it would be a new, uninvited rule this codebase has never applied to reminder/notification code, anywhere.

## Part 1 — Verify the current gap

### Query shape confirmed

Both jobs live in `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/`:

- **`SendRefresherRemindersJob.cs`** — four separate queries (talk 2-week, talk 1-week, course 2-week, course 1-week), all shaped identically:
  [SendRefresherRemindersJob.cs:54-67](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/SendRefresherRemindersJob.cs#L54) (and the three siblings at lines 85, 115, 145) — `.IgnoreQueryFilters().Include(st => st.Employee).Include(st => st.ToolboxTalk).Where(st => st.TenantId == tenant.Id && !st.IsDeleted && ...)`. No predicate anywhere touches `Employee`.
- **`SendToolboxTalkRemindersJob.cs`** — one query, [lines 70-79](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/SendToolboxTalkRemindersJob.cs#L70): `.IgnoreQueryFilters().Include(st => st.Employee).Include(st => st.ToolboxTalk).Where(st => st.TenantId == tenant.Id && !st.IsDeleted)...`. Same absence of any `Employee` predicate.

**Email dispatch path confirmed:** both jobs pass `talk.Employee` (or `assignment.Employee`) directly into the email service —
`_emailService.SendRefresherReminderAsync(talk, talk.Employee, ...)` ([SendRefresherRemindersJob.cs:73](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/SendRefresherRemindersJob.cs#L73)) and `_emailService.SendReminderEmailAsync(talk, talk.Employee, ...)` ([SendToolboxTalkRemindersJob.cs:103](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/SendToolboxTalkRemindersJob.cs#L103)) — whatever `Employee.Email` the ignore-filtered query returns is what gets used, including a soft-deleted employee's last-known address. `SendToolboxTalkRemindersJob` does guard `if (talk.Employee != null)` before sending (line 101), but with `IgnoreQueryFilters()` cascading to the `Include`, `talk.Employee` is *never* null for a soft-deleted employee — it's the deleted row, not a null reference. The null-check protects against a genuinely missing FK target, not against a soft-deleted one.

**Specific lines requiring modification:**

| Job | Query location(s) |
|---|---|
| `SendRefresherRemindersJob.cs` | Lines 54-67, 85-97, 115-127, 145-156 (four `Where` clauses) |
| `SendToolboxTalkRemindersJob.cs` | Lines 70-79 (one `Where` chain) |

### EF Core 9 `IgnoreQueryFilters()` + `Include()` cascade — confirmed, not just inferred

This is documented, current EF Core behavior (unchanged since the feature's introduction, still true in EF Core 9): `IgnoreQueryFilters()` disables global query filters for *every* entity type the query touches, including entities reached via `Include`/`ThenInclude`. There is no per-navigation opt-back-in — it is all-or-nothing for the query. No test in this repo exercises this specific behavior (searched `tests/` for `SendRefresherRemindersJob` / `SendToolboxTalkRemindersJob` — zero matches; neither job has any test coverage at all), so this recon relies on documented framework semantics plus the absence of any counter-example in this codebase — every other `IgnoreQueryFilters()` + `Include()` combination found here (listed below) either (a) includes an entity that doesn't carry a soft-delete filter worth worrying about for this bug's purposes, or (b) explicitly re-adds the predicate it needs, which is itself evidence the codebase's own authors understand the cascade and compensate for it manually when it matters.

**Existing codebase precedent for compensating with explicit predicates** (proof the "shape" is already established elsewhere, just not applied to `Employee` in these two jobs):

- `RequirementMappingService.cs:164-169` — `.IgnoreQueryFilters().Where(r => !r.IsDeleted && r.IsActive && r.IngestionStatus == RequirementIngestionStatus.Approved && ...).Include(r => r.RegulatoryProfile)`. This is the closest precedent in the whole codebase for "ignore filters on the root, then explicitly re-add both `!IsDeleted` *and* `IsActive`" — but note this is gating a `RegulatoryRequirement`'s own `IsActive` (a content-publication flag), not an `Include`d navigation's `IsActive`. It's the right shape reference for *how* to write the predicate, not evidence that `Employee.IsActive` specifically should be included.
- `TenantSectorService.cs` (lines 19-21, 106-107, 131-132, 172-173, 192-193) — `.IgnoreQueryFilters().Where(ts => ts.TenantId == tenantId && !ts.IsDeleted).Include(ts => ts.Sector)`. Re-scopes tenant + soft-delete explicitly on the root; doesn't touch the included `Sector`'s own flags (Sector has no soft-delete-relevant concern here).
- `RequirementIngestionService.cs:80-81, 277-278` and `PipelineAuditQueryService.cs:164-171, 211-213` — same shape: explicit `!IsDeleted` (and sometimes a status/`IsActive` check) on the root entity, then `Include()` a related entity without a matching explicit predicate on that related entity.

**No existing precedent anywhere in the codebase re-scopes an `Include`d `Employee` navigation after `IgnoreQueryFilters()`.** The three places that need a well-formed `Employee` for a similar purpose (see Part 2) all sidestep the problem entirely by **not** using `Include` for `Employee` — they run a *separate*, explicitly-scoped query against `Employees` and join in memory (dictionary lookup). That is the actual precedent this codebase has established for "get a correctly-scoped `Employee` alongside an ignore-filtered root query" — not an `Include`-side predicate. See Part 4 for what this means for the fix shape.

## Part 2 — `Employee.IsActive` semantics audit

Doc comment on the entity is minimal: `Employee.cs:70-73` — `/// Employee status - true if active` `public bool IsActive { get; set; } = true;`. No further elaboration anywhere in code comments. The only substantive definition of intended meaning found anywhere in the codebase is the **frontend form description**, which is explicit and product-authored:

> `employee-form.tsx:552-556` — Checkbox labelled **"Active"**, `FormDescription`: **"Inactive employees won't appear in selection dropdowns."**

That sentence is the closest thing to a spec this field has. Everything below either confirms or is consistent with that stated intent.

### 3. Where `Employee.IsActive` is set to `false`

| Where | What happens | File:line |
|---|---|---|
| **Employee edit form → `UpdateAsync`** | Admin directly toggles the "Active" checkbox; `employee.IsActive = dto.IsActive` is a straight passthrough, independent of delete | [EmployeeService.cs:664](../src/Core/QuantumBuild.Core.Application/Features/Employees/EmployeeService.cs#L664) |
| **Employee delete (soft) → `DeleteAsync`** | Sets `IsDeleted = true` **and** `IsActive = false` in the same operation, atomically, before the same `SaveChangesAsync` | [EmployeeService.cs:894-895](../src/Core/QuantumBuild.Core.Application/Features/Employees/EmployeeService.cs#L894) |
| **Bulk import (`BulkEmployeeImportJob`)** | Always creates with `IsActive: true` — never sets it false | [BulkEmployeeImportJob.cs:244](../src/Core/QuantumBuild.Core.Infrastructure/Jobs/BulkEmployeeImportJob.cs#L244) |
| **Seed / migration** | Not found setting Employee-specific `IsActive` to false anywhere in `DataSeeder.cs` or seed data files (the many `IsActive = true` hits under `src/Core` are for `User`, `Tenant`, `Role` seeding, not `Employee`) | grep-confirmed |

**Key finding: the delete path always flips both flags together.** There is currently no code path where `IsDeleted = true` and `IsActive` stays `true` — meaning today, *every* soft-deleted employee is also `IsActive = false` by construction. The inverse is not true: the edit-form path can and does produce `IsActive = false` with `IsDeleted = false` — a genuine "administratively marked inactive but still present" state exists and is reachable through the UI.

### 4. Where `Employee.IsActive == false` changes behaviour (creation/selection-time gates)

| Context | Behaviour | File:line |
|---|---|---|
| Schedule processing — who gets new `ScheduledTalk` rows | `activeEmployeeIds` computed from `e.IsActive && !e.IsDeleted` before building the candidate list | [ProcessToolboxTalkScheduleCommandHandler.cs:208](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs#L208) |
| Creating a schedule (initial employee pool + explicit-ID validation) | Two separate `.Where(e => e.IsActive && !e.IsDeleted...)` filters | [CreateToolboxTalkScheduleCommandHandler.cs:80,93](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/CreateToolboxTalkSchedule/CreateToolboxTalkScheduleCommandHandler.cs#L80) |
| Updating a schedule (same pattern) | Same as above | [UpdateToolboxTalkScheduleCommandHandler.cs:83,96](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/UpdateToolboxTalkSchedule/UpdateToolboxTalkScheduleCommandHandler.cs#L83) |
| Supervisor-operator assignment eligibility | Three checks: listing available operators, validating supervisor is active, validating operator is active | [SupervisorAssignmentService.cs:78,112,123](../src/Core/QuantumBuild.Core.Application/Features/Employees/SupervisorAssignmentService.cs#L78) |
| Reports (compliance / skills-matrix base population) | Three separate `!IsDeleted && IsActive` filters over the reportable-employee set | [ToolboxTalkReportsService.cs:47,115,443](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ToolboxTalkReportsService.cs#L47) |
| QR PIN generation (one-off job when tenant enables QR training) | Only issues PINs to `!IsDeleted && IsActive` employees | [GenerateEmployeePinsJob.cs:42](../src/Core/QuantumBuild.Core.Infrastructure/Jobs/GenerateEmployeePinsJob.cs#L42) |
| QR PIN validation at scan time | Same gate re-applied at use-time | [QrScanController.cs:137](../src/QuantumBuild.API/Controllers/QrScanController.cs#L137) |
| `GetUnlinkedAsync` (employee-picker dropdown for linking a user account) | `e.UserId == null && e.IsActive` — this is *literally* the "selection dropdown" scenario the form's own description names | [EmployeeService.cs:922](../src/Core/QuantumBuild.Core.Application/Features/Employees/EmployeeService.cs#L922) |

**Not checked (and this matters — see below):**

- **Login** — `AuthService.cs:61,169` checks `!user.IsActive` (the **`User`** entity — Identity account status, a *different* field on a *different* entity) to reject login/token-refresh. `Employee.IsActive` is never read anywhere in the login/auth path. An employee marked inactive via the edit form, whose linked `User.IsActive` is still `true` (these two flags are not synced by the edit-form path — only the delete path's `DeactivateLinkedUserAsync` syncs them), **can still log in and use the portal**, including completing already-assigned talks.
- **`GetMyToolboxTalksQueryHandler`** (My Learnings / pending / overdue / completed) — filters only `TenantId`, `EmployeeId`, `!IsDeleted` (on `ScheduledTalk`), `Status != Cancelled` [GetMyToolboxTalksQueryHandler.cs:23-27](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Queries/GetMyToolboxTalks/GetMyToolboxTalksQueryHandler.cs#L23) — no read of the requesting employee's `IsActive` at all.
- **`RefresherSchedulingService`** (both overloads, **committed today**, same session as the prior two recons) — its `Employee` lookup for the assignment-notification email filters `e.TenantId == ... && !e.IsDeleted` only. No `IsActive` check. [RefresherSchedulingService.cs:68-69, 163-164](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Services/RefresherSchedulingService.cs#L68).
- **`ProcessToolboxTalkScheduleCommandHandler`**'s own per-employee lookup used to build the email-recipient dictionary — `e.TenantId == ... && !e.IsDeleted && employeeIds.Contains(e.Id)`, **no `IsActive`** [ProcessToolboxTalkScheduleCommandHandler.cs:65-67](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs#L65) — note this is *distinct* from the `activeEmployeeIds` filter at line 208 in the same file, which *does* check `IsActive` but is used only to decide which employees are eligible for a *new* assignment in the first place, not to gate the email for employees who already made it into the assignment batch.
- **`AssignCourseCommandHandler`**'s equivalent lookup — `employeeIds.Contains(e.Id) && e.TenantId == tenantId && !e.IsDeleted`, **no `IsActive`** [AssignCourseCommandHandler.cs:60-62](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Features/CourseAssignments/Commands/AssignCourseCommandHandler.cs#L60).
- **`SendScheduledTalkReminderCommandHandler`** (the admin's manual "Send reminder" button on a single assignment) — no `IgnoreQueryFilters()` at all (runs in an authenticated HTTP context, so global filters apply normally to both `ScheduledTalk` and the `Include`d `Employee`), and even so, it never reads `Employee.IsActive` — an admin can manually re-send a reminder to an inactive-but-not-deleted employee today, by design or by omission, but at minimum *consistently* with the other reminder-adjacent code. [SendScheduledTalkReminderCommandHandler.cs:24-51](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/SendScheduledTalkReminder/SendScheduledTalkReminderCommandHandler.cs#L24).

### 5. Where `IsActive == false` is *not* respected but arguably should be

- **The two reminder jobs themselves** — confirmed gap, this recon's subject.
- **Nothing else was found that clearly should check it and doesn't.** Every other "should we act on this employee" decision point that creates *new* work (schedules, course assignments, supervisor links, QR PINs) already checks it. Every decision point that acts on *already-existing* work (reminders, refresher-creation email, initial-assignment email, manual resend) consistently does **not** check it — which reads as a deliberate, if undocumented, split rather than scattered inconsistency (see Part 3).

### 6. Documentation / intent

- Entity doc comment: one line, no elaboration (`Employee.cs:70-72`).
- Frontend form description: **the only real spec** — "Inactive employees won't appear in selection dropdowns" (`employee-form.tsx:554-556`).
- No BACKLOG entry, design doc, or CLAUDE.md note defines `Employee.IsActive` semantics. (CLAUDE.md's only `IsActive`-adjacent notes concern `ToolboxTalk.IsActive`, a completely different field on a different entity — see the caveat below.)
- No "system administrator's guide" found distinguishing Inactive-vs-Deleted for employees.

### Caveats explicitly checked per the brief

- **`Employee.IsActive` vs `User.IsActive`** — confirmed distinct fields, distinct entities, distinct purposes. `User.IsActive` gates authentication (`AuthService.cs:61,169`) and is what `DeactivateLinkedUserAsync` sets alongside a hard lockout (`LockoutEnd = DateTimeOffset.MaxValue`) when an employee is deleted ([EmployeeService.cs:972-973](../src/Core/QuantumBuild.Core.Application/Features/Employees/EmployeeService.cs#L972)). `Employee.IsActive` gates selection/eligibility only and has no bearing on login. These two flags are only synced together on the **delete** path, not the edit-form **deactivate** path — an admin can flip `Employee.IsActive` to `false` via the edit form without touching `User.IsActive` at all, so a "deactivated" employee's account keeps working for login and portal access.
- **`Employee.IsActive` vs `ToolboxTalk.IsActive`** — also confirmed distinct. `ToolboxTalk.IsActive` is the field prior recon (`scheduled-refresh-flow-recon.md`, and referenced there as "§5.30") found to be functionally decorative on the completion/refresher path. That finding is about talk content, not employees, and is unrelated to this recon's question — flagging only to make sure the two `IsActive` fields aren't conflated by a reader skimming both docs.
- **Bulk import (§3.2 of the brief)** — checked, always sets `IsActive: true`, no bug found here.
- **Employee delete path** — checked, `IsDeleted` and `IsActive` are set together, atomically, in `DeleteAsync`. No employee currently exists in the "soft-deleted but still IsActive=true" state through this path.
- **GDPR / "IsActive-as-leaving" angle** — no evidence found that any tenant or code path treats `IsActive = false` as the "this person has left the company" signal in place of `IsDeleted`. The one UI description of the field frames it purely as a selection/visibility toggle, and the delete path (the actual "this person left" action) manipulates `IsDeleted`, not `IsActive` alone. No separate "offboarded" state distinct from both flags was found.

## Part 3 — Consistency characterisation

**Clear semantics, consistently respected** — with an important qualifier: the "consistency" is not "the same behavior everywhere `IsActive` is read," it's "`IsActive` governs eligibility for new/future selection, and every place that needs that decision reads it; nothing that governs continuity of already-existing assignments reads it, including code committed today in this same problem area." That is a real, coherent design line, not noise:

- **Creation-time / selection-time code** (schedule creation & processing, course assignment building the initial employee pool, supervisor-operator assignment, report population, QR PIN issuance) — **always** checks `IsActive`. Zero exceptions found.
- **Continuity-time / notification code about work that already exists** (initial-assignment emails, refresher-assignment emails, refresher scheduling itself, manual admin resend, the two reminder jobs) — **never** checks `IsActive`, only `IsDeleted`. Zero exceptions found (until you count the two reminder jobs' failure to even check `IsDeleted` correctly, which is this recon's actual bug).

This is not the "ambiguous" or "inconsistently respected" bucket — those would apply if some continuity-time code checked `IsActive` and other continuity-time code didn't. It doesn't happen anywhere in this codebase. The two reminder jobs are missing the `!IsDeleted` check that *every other piece of continuity-time code already has* — they are not missing an `IsActive` check that other continuity-time code has, because no continuity-time code has one.

## Part 4 — Recommended fix shape

**Filter both jobs on `!IsDeleted` only, matching the established continuity-time pattern. Do not add `IsActive`.**

Adding `IsActive` here would make the reminder jobs the *first* piece of continuity-time code in the entire codebase to respect it, diverging from the pattern `RefresherSchedulingService` just established today, `ProcessToolboxTalkScheduleCommandHandler`, `AssignCourseCommandHandler`, and `SendScheduledTalkReminderCommandHandler` all follow. That's not a safety margin, it's a new, undiscussed policy choice with a real consequence: an employee an admin has temporarily flagged "inactive" (e.g., extended leave, temporary site closure) but not deleted would stop getting reminders about talks/refreshers they are still expected to complete on return — while being fully able to log in and complete them, since `IsActive` doesn't gate login. If that's a genuinely wanted behavior change, it should be its own product decision with its own ticket, not a rider on the "stop emailing deleted employees" fix.

### Query shape

Two structurally different fixes are available depending on which query is being touched, and the codebase's own precedent points to different shapes for different situations:

**For the two reminder jobs specifically (bulk, multi-row queries that already `Include(st => st.Employee)`):** add an explicit predicate alongside the existing `.IgnoreQueryFilters()`, rather than removing `Include` and restructuring to a separate dictionary lookup. Since `ScheduledTalk.Employee` is a required (non-nullable) navigation, `!st.Employee.IsDeleted` translates to a plain join-condition in SQL — no restructuring needed:

```csharp
.IgnoreQueryFilters()
.Include(st => st.Employee)
.Include(st => st.ToolboxTalk)
.Where(st => st.TenantId == tenant.Id
    && !st.IsDeleted
    && !st.Employee.IsDeleted
    && st.IsRefresher
    ...)
```

(and the equivalent `!a.Employee.IsDeleted` for the two `ToolboxTalkCourseAssignment` queries in `SendRefresherRemindersJob`, and the single `ScheduledTalk` query in `SendToolboxTalkRemindersJob`). This mirrors the shape already used in this codebase for "ignore filters on the root, explicitly re-add what you actually need" (`RequirementMappingService.cs:164-169`, `TenantSectorService.cs`), just extended one hop through the `Include`.

This is a **more targeted, more minimal fix** than restructuring these two jobs to use the separate-dictionary-lookup pattern the three assignment/notification handlers use (`ProcessToolboxTalkScheduleCommandHandler`, `AssignCourseCommandHandler`, `RefresherSchedulingService`) — that pattern exists in those three places because each only needs *one* employee at a time (or a small handful, keyed by ID), not because it's a stylistic preference over adding a `Where` predicate. The reminder jobs process potentially many rows per tenant per run; keeping the `Include` and adding one predicate is both correct and the lower-diff change.

**Do not** restructure to remove `IgnoreQueryFilters()` and rely on the ambient global filter instead — these jobs run inside Hangfire with no `HttpContext`/`ICurrentUserService.TenantId`, so the existing `IgnoreQueryFilters()` + explicit `TenantId` predicate is required scaffolding (correctly commented as such at [SendToolboxTalkRemindersJob.cs:58-59](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/SendToolboxTalkRemindersJob.cs#L58)) and must stay.

## Additional BACKLOG entries needed

None. This is the "Clear + consistent" case per the brief's own framing — there is no other place in the codebase where `IsActive` is inconsistently respected for continuity-time/notification code that would need its own separate tracking entry. The one soft finding worth a low-priority backlog line, if the team wants it tracked, is **not** about `IsActive` at all:

- **`Employee.IsActive` and `User.IsActive` are only synced on the delete path, not the edit-form deactivate path.** An admin flipping an employee to "Inactive" via the edit form does not touch the linked `User.IsActive`, so a "deactivated" employee can still log in and use the portal. This may be entirely intentional (deactivating ≠ locking out — matches "won't appear in dropdowns" framing, which says nothing about login), but it's worth a line in BACKLOG if anyone later assumes "Inactive" implies "can't log in."

## Notes for the boss

This fix stops reminder emails (both the refresher pre-due reminders and the generic overdue reminders) from going out to employees who have been deleted/offboarded in the system — that's a real, currently-reproducible gap, and the fix is a one-line addition to five queries across the two jobs. It does **not** change anything about employees who are merely marked "Inactive" without being deleted — those employees will keep receiving reminders exactly as they do today, because every other piece of notification-sending code in this codebase (including a fix shipped today for refresher-assignment emails) treats "Inactive" as a "don't show them in dropdowns for new work" flag, not a "stop talking to them" flag, and `Inactive` employees can still log in and complete their outstanding training regardless. If we ever want "Inactive" to also silence reminders, that's a distinct product decision — bundling it into this fix would make it the only piece of code in the system to make that call, with no ticket or discussion behind it.
