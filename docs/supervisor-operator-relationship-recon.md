# Supervisor–Operator Relationship — Feature Recon

_Investigation date: 2026-06-22. Read-only. No code changes._

---

## 1. One-Line Summary

A Supervisor employee is explicitly linked to one or more Operator employees via the `SupervisorAssignment` join table; this scopes reports, skills matrix, and team-management UI to the assigned group, but schedule creation at the backend does **not** enforce that a Supervisor can only assign learnings to their own operators.

---

## 2. Data Model

### `SupervisorAssignment` entity

**File:** `src/Core/QuantumBuild.Core.Domain/Entities/SupervisorAssignment.cs`

Inherits `TenantEntity → BaseEntity`, giving it:

| Field | Type | Source | Notes |
|---|---|---|---|
| `Id` | `Guid` | `BaseEntity` | Primary key |
| `TenantId` | `Guid` | `TenantEntity` | Multi-tenancy FK |
| `SupervisorEmployeeId` | `Guid` | Own field | FK → `Employee` |
| `OperatorEmployeeId` | `Guid` | Own field | FK → `Employee` |
| `Supervisor` | nav property | Own field | Navigation to supervisor `Employee` |
| `Operator` | nav property | Own field | Navigation to operator `Employee` |
| `IsDeleted` | `bool` | `BaseEntity` | Soft-delete flag |
| `CreatedAt` / `CreatedBy` | audit | `BaseEntity` | Set on first assignment |
| `UpdatedAt` / `UpdatedBy` | audit | `BaseEntity` | Set on restore-on-reassign |
| `DeletedBy` | `string?` | `BaseEntity` | Set on unassignment soft-delete |

### Entity configuration

**File:** `src/Core/QuantumBuild.Core.Infrastructure/Data/Configurations/SupervisorAssignmentConfiguration.cs`

- **Primary key:** `Id`
- **Unique index:** `IX_SupervisorAssignments_TenantId_SupervisorEmployeeId_OperatorEmployeeId` — composite unique on `(TenantId, SupervisorEmployeeId, OperatorEmployeeId)`. The index is **not** partial — it covers soft-deleted rows too, which is what makes the restore-on-reassign pattern necessary.
- **Supervisor FK:** `HasForeignKey(e => e.SupervisorEmployeeId)` with `OnDelete: Restrict` — a supervisor Employee cannot be hard-deleted while assignment rows exist.
- **Operator FK:** `HasForeignKey(e => e.OperatorEmployeeId)` with `OnDelete: Restrict` — same guard on the operator side.

### Query filter

**File:** `src/Core/QuantumBuild.Core.Infrastructure/Data/ApplicationDbContext.cs:323`

```csharp
modelBuilder.Entity<SupervisorAssignment>()
    .HasQueryFilter(e => !e.IsDeleted && (BypassTenantFilter || e.TenantId == TenantId));
```

All queries through the DbSet automatically exclude soft-deleted rows and rows belonging to other tenants. `BypassTenantFilter` is true only for SuperUsers with no active tenant selected. Services that need to see soft-deleted rows (e.g., the restore-on-reassign path) call `.IgnoreQueryFilters()` explicitly.

### Employee entity — supervisor-related fields

**File:** `src/Core/QuantumBuild.Core.Domain/Entities/Employee.cs`

There is **no** denormalised supervisor-related field on `Employee` (no `SupervisorId`, no `IsSupervisor` flag). All relationships flow exclusively through `SupervisorAssignment`. The entity exposes two inverse collections:

- `SupervisorAssignments` — assignments where this employee is the supervisor
- `OperatorAssignments` — assignments where this employee is the operator

Whether an employee acts as a supervisor is inferred by whether a `User` account linked to them carries the Supervisor role, combined with (separately) whether any `SupervisorAssignment` rows point to them as supervisor.

### Restore-on-reassign pattern

**File:** `src/Core/QuantumBuild.Core.Application/Features/Employees/SupervisorAssignmentService.cs` (~line 142)

When assigning an operator to a supervisor:

1. Query existing non-deleted assignments to skip duplicates.
2. Query soft-deleted assignments (`.IgnoreQueryFilters()`) for the same supervisor–operator pair.
3. If a soft-deleted row is found, restore it: `IsDeleted = false`, update `UpdatedBy`/`UpdatedAt`. This is necessary because the unique index covers soft-deleted rows — inserting a second row for the same pair would violate the constraint.
4. If no prior row exists, insert a new `SupervisorAssignment`.

### One-supervisor-per-operator?

The unique index is on `(TenantId, **Supervisor**EmployeeId, OperatorEmployeeId)`. It does **not** prevent the same operator appearing in assignments for multiple different supervisors. A single operator can be assigned to N supervisors simultaneously, and will therefore appear in each supervisor's scoped report data.

---

## 3. Role Definitions

### Role permission matrix

**Files:** `src/Core/QuantumBuild.Core.Infrastructure/Persistence/DataSeeder.cs` (lines 379–397),  
`src/Core/QuantumBuild.Core.Infrastructure/Identity/Permissions.cs`

| Permission | SuperUser | Admin | Supervisor | Operator |
|---|:---:|:---:|:---:|:---:|
| `Learnings.View` | ✓ | ✓ | ✓ | ✓ |
| `Learnings.Schedule` | ✓ | ✓ | ✓ | — |
| `Learnings.Manage` | ✓ | ✓ | — | — |
| `Learnings.Admin` | ✓ | ✓ | — | — |
| `Core.ManageEmployees` | ✓ | ✓ | — | — |
| `Core.ManageSites` | ✓ | ✓ | — | — |
| `Core.ManageUsers` | ✓ | ✓ | — | — |
| `Core.ManageRoles` | ✓ | ✓ | — | — |
| `Tenant.Manage` | ✓ | — | — | — |

Supervisor gets exactly two permissions: `Learnings.View` and `Learnings.Schedule`.

### `CleanupSupervisorPermissionsAsync`

**File:** `src/Core/QuantumBuild.Core.Infrastructure/Persistence/DataSeeder.cs` (line 239–272)

Run on every `DataSeeder.SeedAsync` call. Removes `Core.ManageEmployees` and `Core.ManageSites` from the Supervisor role if they are present. The seeder comment explains the reason: supervisors previously had those permissions, but the feature was narrowed so supervisors manage their team exclusively through the My Team page and the assignment endpoints. The cleanup handles tenants that were seeded before the narrowing.

### How the Supervisor role is acquired

A user becomes a Supervisor by being assigned the Supervisor `Role` via the user management UI (`PUT /api/users/{id}` with the Supervisor role ID in `RoleIds`). The supervisor role cannot be changed through the employee form — role editing for existing employees is only on the linked user's edit form. There is no logic that automatically promotes or demotes a user to/from Supervisor based on the presence or absence of `SupervisorAssignment` rows.

### Enforcement points

**Backend:**

- Controller-level `[Authorize(Policy = "Learnings.View")]` gates all five supervisor-assignment endpoints.
- `CanAccessSupervisorData` / `CanManageSupervisorData` helper methods in `EmployeesController` (lines 476–491) enforce that non-Admin/non-SU users can only access their own supervisorId. A Supervisor cannot manage another supervisor's assignments through the API.
- `ResolveScopedEmployeeIdsAsync` in `ToolboxTalksController` (lines 114–150) returns a filtered list of employee IDs based on role: `null` for Admin/SU (all data), the supervisor's assigned operator IDs for Supervisor, or a single-element list (self) for Operator.

**Frontend:**

- The employee-facing `/toolbox-talks` layout (`web/src/app/(authenticated)/toolbox-talks/layout.tsx:25`) gates the additional My Team nav items on `user?.roles?.includes("Supervisor")`.
- The My Team page (`team/page.tsx:32–35`) redirects non-Supervisor users immediately.
- The admin layout (`web/src/app/(authenticated)/admin/layout.tsx`) grants access to anyone with any permission in `[Core.ManageEmployees, Core.ManageUsers, Learnings.Manage, Learnings.Schedule, Learnings.Admin, LessonParser.Use]`. Because Supervisor has `Learnings.Schedule`, they can access the admin area.

---

## 4. Backend Surface

### Supervisor-assignment endpoints (`EmployeesController`)

**File:** `src/QuantumBuild.API/Controllers/EmployeesController.cs`

| Verb | Path | Auth policy | Behaviour | Scoping guard |
|---|---|---|---|---|
| GET | `/api/employees/{supervisorId}/operators` | `Learnings.View` | Returns all active (non-soft-deleted) `SupervisorAssignment` rows for the given supervisor | `CanAccessSupervisorData`: Admin/SU pass; others must own the supervisorId |
| GET | `/api/employees/{supervisorId}/operators/available` | `Learnings.View` | Returns employees not yet assigned to this supervisor, excluding Admin/Supervisor/SuperUser roles | `CanManageSupervisorData`: same ownership guard |
| POST | `/api/employees/{supervisorId}/operators` | `Learnings.View` | Bulk-assigns a list of operator IDs; restore-on-reassign for soft-deleted pairs | `CanManageSupervisorData` |
| DELETE | `/api/employees/{supervisorId}/operators/{operatorId}` | `Learnings.View` | Soft-deletes the `SupervisorAssignment` row | `CanManageSupervisorData` |
| GET | `/api/employees/my-operators` | `Learnings.View` | Resolves the current user's `employee_id` JWT claim and calls `GetAssignedOperatorIdsAsync` | No supervisorId needed — self-scoped |

All five endpoints use `Learnings.View` as the policy (not `Learnings.Schedule`), which is consistent with CLAUDE.md's documented intent but means the assignment management endpoints are accessible to any user with that permission (including Operators, though the `CanManageSupervisorData` guard would then protect against cross-user manipulation).

### Report scoping — `ResolveScopedEmployeeIdsAsync`

**File:** `src/QuantumBuild.API/Controllers/ToolboxTalksController.cs` (lines 114–150)

```
SuperUser  → return null  (no filter)
Admin      → return null  (no filter)
Supervisor → return [operator1Id, operator2Id, ...]  (from SupervisorAssignmentService)
Operator   → return [ownEmployeeId]
(no employee) → return []  (empty = see nothing)
```

This method is called before every report query and passed as `employeeIds` to the query handler. A `null` result disables employee filtering entirely. A Supervisor with zero assigned operators receives an empty list and will see no report data.

**Endpoints that use this scoping (all at `Learnings.View`):**

- Dashboard KPIs (`/api/toolbox-talks/dashboard`)
- Compliance report + export
- Overdue report + export
- Completions report + export
- Skills matrix + export
- Certificate report

### Schedule creation — scoping gap

**File:** `src/Modules/ToolboxTalks/.../Commands/CreateToolboxTalkSchedule/CreateToolboxTalkScheduleCommandHandler.cs`

The `POST /api/toolbox-talks/schedules/` endpoint is gated by `Learnings.Schedule`. The command handler accepts either `AssignToAllEmployees = true` (which targets all active employees in the tenant) or an explicit list of `EmployeeIds`. The handler validates only that the provided IDs exist and are active within the tenant. There is **no check** that the requesting user is a Supervisor and no check that the provided employees are in the Supervisor's assigned-operator list. A Supervisor could craft a request to schedule a learning to any employee in the tenant.

### Employee deletion guard

**File:** `src/Core/QuantumBuild.Core.Application/Features/Employees/EmployeeService.cs` (~lines 861–910)

`DeleteAsync` queries active (non-soft-deleted) `SupervisorAssignment` rows and blocks deletion in two directions:

- If the employee is a **supervisor** with active assignments → error message naming the count of assigned operators.
- If the employee is an **operator** assigned to supervisors → error message naming the count of supervisors.

Deletion is only blocked at the `EmployeeService` layer; the DB itself would also prevent hard-delete via `Restrict` FK behaviour, but the service check returns a user-readable error before that point.

---

## 5. Frontend Surface

### Toolbox-talks layout — conditional nav

**File:** `web/src/app/(authenticated)/toolbox-talks/layout.tsx`

All employees see: **My Learnings**, **My Certificates**.

When `user?.roles?.includes("Supervisor")` is true, three additional nav items appear:
- **My Team** → `/toolbox-talks/team`
- **Skills Matrix** → `/toolbox-talks/team/skills-matrix`
- **Team Reports** → `/toolbox-talks/reports`

SuperUser users are redirected to `/admin/tenants`. Admin users without an `employeeId` are redirected to `/admin/toolbox-talks`. Supervisors have an `employeeId` and remain in the employee portal.

### My Team page

**File:** `web/src/app/(authenticated)/toolbox-talks/team/page.tsx`

- Hard redirect to `/toolbox-talks` for non-Supervisor users (line 32).
- Calls `GET /api/employees/my-operators` via `useMyOperators()`.
- Displays a table of assigned operators: Full Name, Employee Code, Department, Job Title, Remove button.
- Opens `AssignOperatorsDialog` for assigning new operators.
- Calls `DELETE /api/employees/{supervisorId}/operators/{operatorId}` via `useUnassignOperator()`.
- If no operators are assigned: empty-state card with instructions to use Assign Operators.

The `supervisorId` is taken from `user?.employeeId` (the JWT `employee_id` claim). If the Supervisor user has no linked employee record, the Assign Operators button does not render.

### Skills Matrix (supervisor view)

**File:** `web/src/app/(authenticated)/toolbox-talks/team/skills-matrix/page.tsx`

- No explicit role guard — relies on the nav gating.
- Calls `GET /api/toolbox-talks/reports/skills-matrix?category=...`; data returned is already scoped to the supervisor's team via `ResolveScopedEmployeeIdsAsync`.
- Category filter and Export to Excel button present.
- Uses the same `SkillsMatrixGrid` component as the admin view.

### Team Reports landing

**File:** `web/src/app/(authenticated)/toolbox-talks/reports/page.tsx`

- No explicit role guard.
- Calls `useMyOperators()` to display the assigned-operator count; shows an empty state if zero operators.
- Links to three sub-reports: Compliance, Overdue, Completions.
- Each sub-report hits the same API endpoints as the admin reports; data is scoped server-side by `ResolveScopedEmployeeIdsAsync`.

### Admin layout — supervisor access

**File:** `web/src/app/(authenticated)/admin/layout.tsx`

`corePermissions` includes `Learnings.Schedule`, so Supervisors pass the admin-area access check. They see only the **Learnings** tab (pointing to `/admin/toolbox-talks`), because the other admin tabs require permissions they don't have. The page header title shows "Learning Management" instead of "Administration" when `isSupervisorOnly` is true.

This means a Supervisor can:
- View the admin Toolbox Talks list, individual talk details, schedules, and assignments.
- Create, update, and delete schedules via the admin schedule UI.
- The schedule creation form targets any employee in the tenant (see scoping gap above).

### Top-nav profile menu

**File:** `web/src/components/layout/top-nav.tsx`

- Non-SU users with an `employeeId` get a "My Learnings" link.
- Users with any `hasAdminAccess` permission get an additional "Administration" (or "Learning Management" for `isSupervisorOnly`) link pointing to `/admin/toolbox-talks`.
- `isSupervisorOnly` is defined as: not SuperUser AND has Supervisor role AND does not have Admin role.

### Admin employee detail — assigned operators section

**File:** `web/src/app/(authenticated)/admin/employees/[id]/page.tsx:307–311`

When viewing an employee detail in the admin area, if the viewed employee's linked user has the Supervisor role (`isSupervisor = linkedUser?.roles?.some((r) => r.name === "Supervisor")`), the page renders an `AssignedOperatorsSection` component. This component displays the full list of operators assigned to that supervisor and allows Admin/SU to add or remove assignments.

This is the only place an Admin (rather than the Supervisor themselves) can manage assignments on behalf of a given supervisor.

---

## 6. Workflows Currently Supported

### Assigning operators to a supervisor

**Who can do it:** The Supervisor themselves (via My Team page), or an Admin/SU (via the admin employee detail page).

**Flow:**
1. Open My Team → click "Assign Operators" → dialog shows available operators (employees not already assigned and not in Admin/Supervisor/SU roles).
2. Multi-select → confirm → `POST /api/employees/{supervisorId}/operators`.
3. Restore-on-reassign fires if the pair was previously unassigned.
4. Operator immediately appears in the supervisor's My Team list.

**Limitations:** No bulk-import or automation. No notification sent to the operator. No record that a specific admin performed the assignment on behalf of a supervisor (audit log is written, but the supervisor's view doesn't show this history).

### Viewing and removing team members

**Who can do it:** Supervisor via My Team page; Admin via employee detail page.

The Supervisor sees a table of their assigned operators. Clicking the remove icon triggers `DELETE /api/employees/{supervisorId}/operators/{operatorId}`, which soft-deletes the row. The operator is immediately gone from the Supervisor's team and from scoped report data.

### Viewing scoped reports

**Who can do it:** Supervisor via `/toolbox-talks/reports/*`.

Three reports are available: Compliance, Overdue, and Completions. All use the same backend endpoints as the admin views, but `ResolveScopedEmployeeIdsAsync` narrows the returned data to the supervisor's assigned operators. Export to Excel is available on Overdue and Completions.

### Viewing scoped skills matrix

**Who can do it:** Supervisor via `/toolbox-talks/team/skills-matrix`.

Displays an employee × learning grid scoped to the supervisor's team. Matches the admin-side matrix but filtered to the supervisor's operators.

### Scheduling learnings

**Who can do it:** Supervisor via admin schedule creation UI (`/admin/toolbox-talks/schedules/new`).

Supervisors can reach the admin Toolbox Talks area because `Learnings.Schedule` is included in `corePermissions`. They can create, update, and cancel schedules. The schedule creation form does not restrict which employees can be targeted — a Supervisor has the same employee picker as an Admin (see Gap §8).

---

## 7. Workflows NOT Currently Supported

### Supervisor-initiated learning assignment without scheduling

There is no lightweight "assign this learning to my team now" action in the supervisor-facing UI. The only path is via the admin schedule creation flow, which involves selecting a talk, frequency, dates, and employee targets.

### Notifications to operators

When a Supervisor assigns or removes an operator, no email or in-app notification is sent to the operator. The operator discovers their team membership (or removal) implicitly — their scoped data changes, but they are not told about it.

### Notifications to supervisors about team activity

There is no mechanism to notify a Supervisor when one of their operators completes, goes overdue, or fails a quiz. The Supervisor must check their Team Reports manually.

### Supervisor visibility into operator's full certificate history

The Team Reports → Completions report is scoped and paginated, but there is no certificate sub-page in the supervisor's employee portal equivalent to the admin certificate management page. Supervisors can see skills matrix status but cannot drill into a specific operator's certificate PDF from the supervisor UI.

### Supervisor visibility into "who assigned me this learning"

Operators see their assigned talks but have no UI indication that a Supervisor (rather than an Admin) created the schedule. The assigned-talk detail shows due date and status, not who scheduled it.

### Soft-delete history for assignments

Soft-deleted `SupervisorAssignment` rows (unassignments) are preserved in the database but there is no UI or API endpoint to view them. An admin cannot see that an operator was previously assigned to a supervisor and later removed.

---

## 8. Gaps, Inconsistencies, Half-Built Pieces

### Gap A — Schedule creation is not team-scoped at the backend

The `CreateToolboxTalkScheduleCommandHandler` accepts any `EmployeeIds` that are active within the tenant. No check confirms that the caller is a Supervisor or that the targets are in their assigned-operator list. A Supervisor with `Learnings.Schedule` can schedule a learning for any employee in the tenant via a crafted API call. The frontend schedule creation form currently does not constrain the employee picker to the Supervisor's team, so this gap is also present in the standard UI path.

### Gap B — `AssignToAllEmployees` flag is accessible to Supervisors

The `CreateToolboxTalkScheduleCommand.AssignToAllEmployees = true` path assigns to every active employee in the tenant. There is no backend guard preventing a Supervisor from using this flag.

### Gap C — Team Reports pages have no role guard

`/toolbox-talks/reports` and its children have no explicit Supervisor role check. An Operator who navigates directly to `/toolbox-talks/reports` will see the page. The data will be scoped to their own record only (via `ResolveScopedEmployeeIdsAsync`), so no data leak, but the page is framed as a team-management view that is confusing for a solo Operator. The nav hides the link from Operators, but deep-linking bypasses the nav.

### Gap D — Role vs. assignment mismatch not validated

A user can have the Supervisor role with zero operator assignments — the My Team page shows an empty state and Team Reports show no data. Conversely, there is no code that requires a user to have the Supervisor role in order to be given a `SupervisorAssignment` — an Admin could (via the employee detail page) set up assignments for an employee whose linked user is an Operator. That Operator would never see the assignments or scoped reports because the reports and My Team nav are gated on the Supervisor role.

### Gap E — `Learnings.Schedule` policy inconsistency on assignment endpoints

The five supervisor-assignment endpoints all use `Learnings.View` as the auth policy. Conceptually, creating or removing assignments is a management action closer to `Learnings.Schedule`. The current policy means an Operator (who has `Learnings.View`) can call `POST /api/employees/{supervisorId}/operators` if they know the supervisorId — the `CanManageSupervisorData` guard protects against a supervisor ID that isn't theirs, but an Operator with their own `employeeId` as the supervisorId would pass both the policy and the guard, then fail at the data layer (no assignments would match). This is low-risk practically but the policy is misleading.

### Gap F — `available` endpoint excludes roles by name, not by permission

`GET /api/employees/{supervisorId}/operators/available` excludes employees whose linked user has the Admin, Supervisor, or SuperUser role. This is correct as a heuristic but is a string comparison against role names, not a permission check. If a future role is added that should also be excluded (e.g., Auditor), this filter needs manual updating.

### Gap G — Admin employee detail uses role name detection, not permission check

`admin/employees/[id]/page.tsx` shows the `AssignedOperatorsSection` when `linkedUser?.roles?.some(r => r.name === "Supervisor")`. If the Supervisor role is renamed or a second supervisor-like role is introduced, this component would silently stop rendering.

### Gap H — No audit trail viewable from the UI

The system writes audit log entries for `AssignOperator` and `UnassignOperator` actions (via `AuditActions.Employee.*` constants). However, there is no UI surface that exposes this audit history — neither on the employee detail page nor in a dedicated assignments history view.

---

## 9. Open Product Questions

1. **Should schedule creation be restricted to a Supervisor's assigned operators?** Currently it is not enforced at the backend. If the answer is yes, should this also apply to `AssignToAllEmployees`? What error should be returned if a Supervisor provides employee IDs outside their team?

2. **Can an operator be assigned to multiple supervisors simultaneously?** The schema allows it. If allowed, whose report does the operator appear in — all supervisors, or only the primary one? Should there be a "primary supervisor" concept?

3. **Should unassignment history be visible?** The soft-deleted rows exist in the DB. Is there business value in exposing a history view to Admins or Supervisors?

4. **Should notifications be sent when a Supervisor assigns or removes an operator?** If so, to whom — the operator, other supervisors, or Admins?

5. **What happens to scheduled learnings created by a Supervisor after that supervisor's operators are removed or re-assigned?** The `ScheduledTalk` rows created by the schedule processor are not re-scoped when assignment changes occur. A completed operator might still have open scheduled talks from a previous supervisor relationship.

6. **Should the My Team page be the only place a Supervisor manages assignments, or should an Admin be able to bulk-assign operators across multiple supervisors?** Currently the admin employee detail page shows one supervisor at a time.

7. **Is the Supervisor role intended only for employees with an `employeeId` (i.e., people who are also learners), or can an admin user (no `employeeId`) be a Supervisor?** The current code assumes `user?.employeeId` is non-null for a Supervisor; if it is null, the Assign Operators button silently disappears.

8. **Should a Supervisor be able to view the full admin Toolbox Talks list and talk detail pages?** Currently they can, because `Learnings.Schedule` grants admin area access. Whether this is intentional or an accidental side effect is not documented.

9. **Should the Team Reports and Skills Matrix pages redirect non-Supervisors who deep-link directly?** Currently they do not.

10. **What is the expected behaviour when a Supervisor is demoted to Operator?** Their `SupervisorAssignment` rows are not cleaned up; the Operator will no longer see My Team or Team Reports nav items but the assignment data remains.

---

## 10. Risks

### R1 — Supervisor can schedule to entire tenant

A Supervisor with `Learnings.Schedule` can schedule a learning to any active employee in the tenant, including employees they do not supervise, by providing explicit `EmployeeIds` or setting `AssignToAllEmployees = true`. This is undetected and silently succeeds. Impact: training assignments appearing for employees outside the intended team; compliance reporting inflated or mis-attributed.

### R2 — Supervisor demotion leaves orphaned assignments

If a Supervisor user is downgraded to Operator role, their `SupervisorAssignment` rows remain in the database with no cleanup. The former supervisor will no longer see My Team (role check) or have their operators returned by `ResolveScopedEmployeeIdsAsync` (returns single self-element for Operator). Operators assigned to the former supervisor effectively lose their supervisor. No alert or cleanup is triggered.

### R3 — Supervisor with no linked employee

If a user is assigned the Supervisor role but has no linked Employee record (`employeeId = null`), the My Team page's Assign Operators button silently disappears. The user cannot manage assignments and the `/api/employees/my-operators` endpoint returns an empty list (because `GetCurrentEmployeeId` returns null). No error surfaces. The user is effectively a Supervisor with no functional capability.

### R4 — Multi-supervisor assignment leads to double-counting in reports

An operator assigned to two supervisors will appear in each supervisor's scoped report data. If both supervisors export or print a compliance report, the same operator's record is counted separately under each. Cross-department analysis will double-count operators with multiple supervisor assignments.

### R5 — `Restrict` FK and delete workflow coupling

Both FK relationships on `SupervisorAssignment` use `OnDelete: Restrict`. The application-layer `EmployeeService.DeleteAsync` check guards against this at the service level with user-readable errors. However, if any code path deletes an `Employee` entity without going through `EmployeeService` (e.g., a direct DB query or a future admin bulk-delete feature), the delete will fail with a FK violation at the database level with no helpful error message to the caller.

### R6 — Available-operators filter by role name, not permission

The `available` endpoint excludes users by role name string comparison (`"Admin"`, `"Supervisor"`, `"SuperUser"`). If a new privileged role is added, it will not be excluded from the available-operators list unless this filter is manually updated.

### R7 — Team Reports pages have no access guard

An Operator who deep-links directly to `/toolbox-talks/reports` will see the page rendered with their own data (one-person scope). The UI framing ("Compliance reports and analytics for your assigned operators") and the operator-count card showing "0 operators" may be confusing or expose information about the existence of these features.

---

_Document produced from static code analysis. No code was changed. File references are point-in-time and should be verified against current HEAD if used to inform implementation work._
