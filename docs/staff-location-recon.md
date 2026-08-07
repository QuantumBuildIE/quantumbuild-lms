# Staff Location — Recon

Read-only recon. No code changed. All claims cite `file:line`. Facts only — no design, no fix.

---

## A. Does a staff "Location" already exist?

### Classification: **ENTIRELY ABSENT** as a first-class staff concept — but a related, differently-named entity (`Site`) already covers part of the shape.

There is no entity, column, DTO, or UI called `Location` that is tied to staff/employees or tenants for the purpose this pilot client wants (multi-location targeting/filtering). Git history shows no evidence a staff `Location` entity ever existed and was removed — there is no deleted-file trace and no dedicated `Location` class anywhere in the history of this repo (see "History check" below). What exists instead are two unrelated location-shaped things, plus one directly relevant employee-to-place link:

**1. `Site` — the closest existing concept, but not branded "Location" and not multi-valued**

- Entity: [Site.cs](src/Core/QuantumBuild.Core.Domain/Entities/Site.cs) — `TenantEntity`. Doc-comment literally says *"Represents a construction site where stock can be ordered and delivered"* ([Site.cs:6](src/Core/QuantumBuild.Core.Domain/Entities/Site.cs#L6)).
  - Fields: `SiteCode`, `SiteName`, `Address`, `City`, `PostalCode`, `SiteManagerId` (FK → `Employee`), `CompanyId` (FK → `Company`), `Phone`, `Email`, `IsActive`, `Notes`, `Latitude`/`Longitude` (geofencing), `GeofenceRadiusMeters`, `FloatProjectId`/`FloatLinkedAt`/`FloatLinkMethod` (Float integration) — [Site.cs:8-104](src/Core/QuantumBuild.Core.Domain/Entities/Site.cs#L8-L104).
  - Configuration: [SiteConfiguration.cs](src/Core/QuantumBuild.Core.Infrastructure/Data/Configurations/SiteConfiguration.cs) — unique index on `{TenantId, SiteCode}` ([SiteConfiguration.cs:51-53](src/Core/QuantumBuild.Core.Infrastructure/Data/Configurations/SiteConfiguration.cs#L51-L53)).
  - Employee link: `Employee.PrimarySiteId` (`Guid?`, nullable) + `Employee.PrimarySite` navigation — [Employee.cs:63-68](src/Core/QuantumBuild.Core.Domain/Entities/Employee.cs#L63-L68). Configured as `HasOne(...).WithMany().HasForeignKey(e => e.PrimarySiteId).OnDelete(DeleteBehavior.SetNull)` — [EmployeeConfiguration.cs:85-88](src/Core/QuantumBuild.Core.Infrastructure/Data/Configurations/EmployeeConfiguration.cs#L85-L88).
  - **Cardinality: one site per employee, nullable, no join table.** There is no `EmployeeSite` many-to-many — an employee has at most one `PrimarySiteId`.
  - API: full CRUD exists at `/api/sites` (`Core.ManageSites` permission) per the project's route table; entity is a fully live, actively-used concept (Site admin pages, Company linkage, geofencing for attendance).
  - Admin UI: employee list already displays a **read-only** "Default Site" column — [admin/employees/page.tsx:180-185](web/src/app/(authenticated)/admin/employees/page.tsx#L180-L185) — sourced from `employee.primarySiteName`. There is no filter control tied to it (see §C.8).

**2. `QrLocation` — a different, unrelated "location" concept (physical QR training station), NOT a staff attribute**

- Entity: [QrLocation.cs](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Entities/QrLocation.cs) — `TenantEntity` with `Name`, `Description`, `Address`, `IsActive`, and a collection of `QrCode`s ([QrLocation.cs:5-13](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Entities/QrLocation.cs#L5-L13)).
  - `QrCode.QrLocationId` is a required FK to `QrLocation` — [QrCode.cs:8](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Entities/QrCode.cs#L8) — and `QrCode` optionally targets a `ToolboxTalkId` or `CourseId` ([QrCode.cs:11-15](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Entities/QrCode.cs#L11-L15)).
  - **`Employee` has no FK to `QrLocation` anywhere** — confirmed by full read of [Employee.cs](src/Core/QuantumBuild.Core.Domain/Entities/Employee.cs) (only `QrPin*` fields exist, no `QrLocationId`). An employee is associated with a `QrLocation` only transiently, at scan time, via a `QrSession` — not as a persistent staff attribute. This is a physical-station concept for the QR Code Location Training feature (Note 10 in CLAUDE.md), unrelated to "which site does this staff member belong to."

**3. Two more "Location"-named things exist, both irrelevant to staff:**
   - `AddBayLocations` / `AddSourceLocationToStockOrder` migrations — [20251219125924_AddBayLocations.cs](src/Core/QuantumBuild.Core.Infrastructure/Migrations/20251219125924_AddBayLocations.cs), [20251219200722_AddSourceLocationToStockOrder.cs](src/Core/QuantumBuild.Core.Infrastructure/Migrations/20251219200722_AddSourceLocationToStockOrder.cs) — these belong to the **Proposals/Stock module** inherited from the parent "Rascor" application at extraction (warehouse bay locations for stock orders). Confirmed present already in the very first commit of this repo, `9e575bf` ("feat: QuantumBuild LMS - standalone LMS extracted from Rascor") — `git show 9e575bf --stat` lists them alongside `Site`-related files, with no dedicated staff `Location` file. Not staff/tenant-location related.
   - `LocationDisplay.tsx` / `use-geolocation.ts` — [LocationDisplay.tsx](web/src/features/toolbox-talks/components/LocationDisplay.tsx), [use-geolocation.ts](web/src/hooks/use-geolocation.ts) — these render/capture raw GPS lat/long captured at `ScheduledTalk` start/complete (geofencing/audit trail), not a named location entity. Related migrations: [20251221221901_AddSiteGeolocationFields.cs](src/Core/QuantumBuild.Core.Infrastructure/Migrations/20251221221901_AddSiteGeolocationFields.cs), [20260213090223_AddGeolocationToScheduledTalks.cs](src/Core/QuantumBuild.Core.Infrastructure/Migrations/20260213090223_AddGeolocationToScheduledTalks.cs).

**History check (git, full repo history, not just current tree):**

- `git log --all --oneline -i --grep="location"` — every hit is either the QR Location Training feature commits, the Asset-management backlog note commit, or unrelated fix commits (`window.location.origin`). No commit message referencing a staff/tenant `Location` entity being added or removed.
- `git log --all --diff-filter=D --summary | grep -i location` — **zero results**. No location-related file has ever been deleted in this repo's history.
- `git log --all --oneline -S"class Location"` — **zero results**. A class literally named `Location` has never existed in any commit.
- Conclusion stands: not a legacy remnant, not partially reverted — it simply was never built as a distinct concept. `Site` is live and does part of the job by convention/reuse, not by design as "Location."

### A.2 — Shape/history summary (per remnant found)

| Concept | Shape | Per-tenant? | Linked to Employee? | Cardinality |
|---|---|---|---|---|
| `Site` | Real attributes: code, name, address, city, postal code, manager, company, phone, email, GPS, geofence radius, Float linkage | Yes (`TenantEntity`) | Yes, via `Employee.PrimarySiteId` | One site per employee (nullable) |
| `QrLocation` | Simple: name, description, address, active flag | Yes (`TenantEntity`) | No — only via transient `QrCode`/`QrSession` scan flow | N/A (not a staff attribute) |
| Stock "Bay Locations" / "Source Location" | Warehouse/stock concept from parent Rascor app | N/A to LMS | No | N/A |
| GPS lat/long capture | Raw coordinates, not a named place | N/A | Captured per `ScheduledTalk` event | N/A |

### A.3 — Current Employee entity: fields and existing segmentation

Full entity: [Employee.cs](src/Core/QuantumBuild.Core.Domain/Entities/Employee.cs).

- Identity/contact: `EmployeeCode`, `FirstName`, `LastName`, `FullName` (computed), `Email`, `Phone`, `Mobile` — [Employee.cs:13-43](src/Core/QuantumBuild.Core.Domain/Entities/Employee.cs#L13-L43)
- Org fields: `JobTitle` (free text, nullable), `Department` (free text, nullable) — [Employee.cs:48-53](src/Core/QuantumBuild.Core.Domain/Entities/Employee.cs#L48-L53). Neither is a lookup table — both are plain `string?` with `HasMaxLength(100)` ([EmployeeConfiguration.cs:34-38](src/Core/QuantumBuild.Core.Infrastructure/Data/Configurations/EmployeeConfiguration.cs#L34-L38)).
- `UserId` (`Guid?`) — links to Identity/login — [Employee.cs:58](src/Core/QuantumBuild.Core.Domain/Entities/Employee.cs#L58)
- `PrimarySiteId` (`Guid?`) + `PrimarySite` nav — [Employee.cs:63-68](src/Core/QuantumBuild.Core.Domain/Entities/Employee.cs#L63-L68) (see §A.1)
- `IsActive`, `StartDate`, `EndDate`, `Notes` — [Employee.cs:73-88](src/Core/QuantumBuild.Core.Domain/Entities/Employee.cs#L73-L88)
- `PreferredLanguage` — [Employee.cs:94](src/Core/QuantumBuild.Core.Domain/Entities/Employee.cs#L94)
- `GeoTrackerID` — mobile geofence app device ID, unrelated to Site — [Employee.cs:99-108](src/Core/QuantumBuild.Core.Domain/Entities/Employee.cs#L99-L108)
- `SupervisorAssignments` / `OperatorAssignments` — many-to-many-via-join-entity grouping mechanism (Supervisor manages Operators) — [Employee.cs:111-112](src/Core/QuantumBuild.Core.Domain/Entities/Employee.cs#L111-L112). This is the **only existing many-to-many staff-grouping mechanism** in the codebase today; it groups by supervisory relationship, not by place.
- `FloatPersonId`/`FloatLinkedAt`/`FloatLinkMethod` — external Float integration — [Employee.cs:117-127](src/Core/QuantumBuild.Core.Domain/Entities/Employee.cs#L117-L127)
- `QrPin*` fields — QR Location Training PIN infra, unrelated to staff Location — [Employee.cs:134-166](src/Core/QuantumBuild.Core.Domain/Entities/Employee.cs#L134-L166)

**Current segmentation available on Employee, in order of structural strength:**
1. `TenantId` (hard multi-tenant boundary, always enforced)
2. `PrimarySiteId` → `Site` (single, nullable, real entity with FK)
3. `Department` (free-text string, no lookup table, no FK)
4. `JobTitle` (free-text string)
5. Supervisor/Operator grouping via `SupervisorAssignment` (relationship-based, not place-based)
6. Role (Admin/Operator/Supervisor/SuperUser) via linked `User`

There is no site/location **list** (many-to-many) on Employee today — only the single nullable `PrimarySiteId`.

---

## B. The assignment / scheduling flow (shared)

### B.4 — How a single learning is scheduled/assigned to people

Two-stage pipeline: **Schedule → (process) → ScheduledTalk**.

**Stage 1 — Create schedule** ([CreateToolboxTalkScheduleCommand.cs](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/CreateToolboxTalkSchedule/CreateToolboxTalkScheduleCommand.cs), handler: [CreateToolboxTalkScheduleCommandHandler.cs](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/CreateToolboxTalkSchedule/CreateToolboxTalkScheduleCommandHandler.cs)):

- The command carries exactly two ways to target people, mutually exclusive:
  - `AssignToAllEmployees: bool` — [CreateToolboxTalkScheduleCommand.cs:40](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/CreateToolboxTalkSchedule/CreateToolboxTalkScheduleCommand.cs#L40)
  - `EmployeeIds: List<Guid>` — explicit list — [CreateToolboxTalkScheduleCommand.cs:45](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/CreateToolboxTalkSchedule/CreateToolboxTalkScheduleCommand.cs#L45)
- Handler resolves the target employee ID list:
  - If `AssignToAllEmployees`: queries `_coreDbContext.Employees.Where(e => e.TenantId == request.TenantId && e.IsActive && !e.IsDeleted)` — **all active tenant employees, no other filter** — [CreateToolboxTalkScheduleCommandHandler.cs:77-88](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/CreateToolboxTalkSchedule/CreateToolboxTalkScheduleCommandHandler.cs#L77-L88).
  - Else: validates the given `EmployeeIds` exist/are active — [CreateToolboxTalkScheduleCommandHandler.cs:90-105](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/CreateToolboxTalkSchedule/CreateToolboxTalkScheduleCommandHandler.cs#L90-L105).
  - Supervisor-only callers are hard-blocked from `AssignToAllEmployees` and restricted to their assigned-operator set via `ISupervisorAssignmentService.GetAssignedOperatorIdsAsync` — [CreateToolboxTalkScheduleCommandHandler.cs:53-73](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/CreateToolboxTalkSchedule/CreateToolboxTalkScheduleCommandHandler.cs#L53-L73).
- A `ToolboxTalkSchedule` ([ToolboxTalkSchedule.cs](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Entities/ToolboxTalkSchedule.cs)) is created with one `ToolboxTalkScheduleAssignment` row ([ToolboxTalkScheduleAssignment.cs](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Entities/ToolboxTalkScheduleAssignment.cs)) per resolved employee ID — [CreateToolboxTalkScheduleCommandHandler.cs:128-140](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/CreateToolboxTalkSchedule/CreateToolboxTalkScheduleCommandHandler.cs#L128-L140). `ToolboxTalkSchedule.AssignToAllEmployees` is persisted on the schedule itself — [ToolboxTalkSchedule.cs:35](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Entities/ToolboxTalkSchedule.cs#L35) — so recurring schedules can re-derive membership later.

**Stage 2 — Process schedule → ScheduledTalk** ([ProcessToolboxTalkScheduleCommandHandler.cs](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs)):

- Runs on-demand (`POST /schedules/{id}/process`) or daily via the `ProcessToolboxTalkSchedulesJob` Hangfire job (per CLAUDE.md background jobs table).
- For each unprocessed `ToolboxTalkScheduleAssignment`, creates one `ScheduledTalk` row (`Status = Pending`) plus per-section `ScheduledTalkSectionProgress` rows — [ProcessToolboxTalkScheduleCommandHandler.cs:88-119](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs#L88-L119).
- If `AssignToAllEmployees` and recurring, `RefreshAssignmentsForAllEmployees` re-diffs against the current active-employee set before each recurrence — adds newly active employees, physically removes assignments for now-inactive ones — [ProcessToolboxTalkScheduleCommandHandler.cs:201-255](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ProcessToolboxTalkSchedule/ProcessToolboxTalkScheduleCommandHandler.cs#L201-L255). This is the one place where "all employees" membership is dynamically re-evaluated over time — relevant if a future location filter needs to behave the same way (auto-track membership changes) rather than being a frozen snapshot.

**Frontend** ([ScheduleDialog.tsx](web/src/features/toolbox-talks/components/ScheduleDialog.tsx)):

- Employee targeting is a checkbox (`assignToAllEmployees` — [ScheduleDialog.tsx:444-465](web/src/features/toolbox-talks/components/ScheduleDialog.tsx#L444-L465)) or, when unchecked, a manual scrollable checklist of employees filtered only by a free-text name/code search box — [ScheduleDialog.tsx:468-551](web/src/features/toolbox-talks/components/ScheduleDialog.tsx#L468-L551), search predicate at [ScheduleDialog.tsx:249-258](web/src/features/toolbox-talks/components/ScheduleDialog.tsx#L249-L258). Employees are fetched via `useAllEmployees()` (or `useMyOperators()` for supervisors) — [ScheduleDialog.tsx:128-146](web/src/features/toolbox-talks/components/ScheduleDialog.tsx#L128-L146) — no site/department dropdown exists in this picker.

### B.5 — How a course is scheduled/assigned (distinct flow, no Schedule layer)

Course assignment is **simpler and has no Schedule/recurring stage at all** — it goes straight from "pick employees" to `ToolboxTalkCourseAssignment` + `ScheduledTalk` rows in one request.

- DTO: [AssignCourseDto.cs](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Features/CourseAssignments/DTOs/AssignCourseDto.cs) — `CourseId` + `List<EmployeeCourseAssignmentDto> Assignments` (each with `EmployeeId` and optional `IncludedTalkIds`) + optional `DueDate` — [AssignCourseDto.cs:5-26](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Features/CourseAssignments/DTOs/AssignCourseDto.cs#L5-L26). **There is no `AssignToAllEmployees` equivalent for courses** — every employee must be explicitly listed.
- Handler: [AssignCourseCommandHandler.cs](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Features/CourseAssignments/Commands/AssignCourseCommandHandler.cs):
  - Validates course + items — [AssignCourseCommandHandler.cs:38-55](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Features/CourseAssignments/Commands/AssignCourseCommandHandler.cs#L38-L55)
  - Validates the given employee IDs all exist/aren't deleted — [AssignCourseCommandHandler.cs:57-65](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Features/CourseAssignments/Commands/AssignCourseCommandHandler.cs#L57-L65)
  - Skips employees who already have a non-Completed assignment for the course — [AssignCourseCommandHandler.cs:67-82](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Features/CourseAssignments/Commands/AssignCourseCommandHandler.cs#L67-L82)
  - For each eligible employee, creates one `ToolboxTalkCourseAssignment` and one `ScheduledTalk` per included course item — [AssignCourseCommandHandler.cs:90-153](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Features/CourseAssignments/Commands/AssignCourseCommandHandler.cs#L90-L153)
- Frontend: [AssignCourseDialog.tsx](web/src/features/toolbox-talks/components/AssignCourseDialog.tsx) — identical manual-checklist-with-name/email-search pattern as the schedule dialog ([AssignCourseDialog.tsx:62-73](web/src/features/toolbox-talks/components/AssignCourseDialog.tsx#L62-L73) filter predicate; employee source `useAllEmployees()` at [AssignCourseDialog.tsx:49](web/src/features/toolbox-talks/components/AssignCourseDialog.tsx#L49)). No site/department filter in this picker either, and no "select all" shortcut of any kind exists here (unlike the schedule dialog's `handleSelectAllEmployees`).

### B.6 — Where a "target by location" hook would go

Both flows converge on the same shape immediately before persistence: **a resolved `List<Guid>` of target `EmployeeId`s**, produced from one of:
- an explicit ID list passed by the caller, or
- (schedules only) a full-tenant "all active employees" query — [CreateToolboxTalkScheduleCommandHandler.cs:80-83](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/CreateToolboxTalkSchedule/CreateToolboxTalkScheduleCommandHandler.cs#L80-L83)

A location-target parameter (e.g. `List<Guid>? SiteIds`) would need to be expanded into member `EmployeeId`s at that same point — the same query shape as the existing all-employees expansion, just adding `.Where(e => siteIds.Contains(e.PrimarySiteId))`. Facts relevant to sizing that change:
- The expansion point exists **twice, independently** (schedule handler and course handler) — there is no shared "resolve target employees" service today; each handler inlines its own `_coreDbContext.Employees.Where(...)` query (schedule: [CreateToolboxTalkScheduleCommandHandler.cs:80-96](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/CreateToolboxTalkSchedule/CreateToolboxTalkScheduleCommandHandler.cs#L80-L96); course: relies entirely on the caller-supplied list, [AssignCourseCommandHandler.cs:57-65](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Features/CourseAssignments/Commands/AssignCourseCommandHandler.cs#L57-L65) — the course flow has no server-side "all/by-criteria" expansion at all today, only client-supplied IDs).
- The `PrimarySiteId` cardinality constraint (§A.3) means "target one or more locations" today can only ever resolve to "employees whose single `PrimarySiteId` is in this set" — there is no employee-to-many-sites relationship to expand from.
- Recurring schedules re-evaluate `AssignToAllEmployees` membership on every run (§B.4, `RefreshAssignmentsForAllEmployees`); a location-scoped equivalent would need the same re-evaluation behavior to stay consistent with that existing pattern, or explicitly diverge from it.

---

## C. The filtering surfaces (shared)

### C.7 — Skills Matrix: current query/filter mechanism, and where location would hook in

Service: [ToolboxTalkReportsService.cs](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ToolboxTalkReportsService.cs), method `GetSkillsMatrixAsync` ([ToolboxTalkReportsService.cs:386-560](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ToolboxTalkReportsService.cs#L386-L560)); interface: [IToolboxTalkReportsService.cs:67](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Services/IToolboxTalkReportsService.cs#L67).

- **Signature accepts exactly two filters: `employeeIds` (role-scoping) and `category` (string). There is no `siteId`/location parameter.** — [ToolboxTalkReportsService.cs:386-389](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ToolboxTalkReportsService.cs#L386-L389)
- Data is derived entirely from `ScheduledTalks` (per CLAUDE.md's documented "Data Derivation" note) — query built at [ToolboxTalkReportsService.cs:396-413](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ToolboxTalkReportsService.cs#L396-L413), filtered by `employeeIds` ([ToolboxTalkReportsService.cs:403-406](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ToolboxTalkReportsService.cs#L403-L406)) and `category` ([ToolboxTalkReportsService.cs:408-411](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ToolboxTalkReportsService.cs#L408-L411)).
- `employeeIds` itself is **not** a location filter — it's the role-scoping result from `ToolboxTalksController.ResolveScopedEmployeeIdsAsync()` (per CLAUDE.md's Report Scoping section: `null` for Admin/SuperUser, assigned operators for Supervisor, self for Operator) — computed in the controller before calling the service ([ToolboxTalksController.cs:2230](src/QuantumBuild.API/Controllers/ToolboxTalksController.cs#L2230)) and passed straight through ([ToolboxTalksController.cs:2225-2236](src/QuantumBuild.API/Controllers/ToolboxTalksController.cs#L2225-L2236)).
- For the admin path (`employeeIds == null`), employees are built from two sources: everyone who appears in `ScheduledTalks`, plus all active employees with zero assignments (for compliance visibility) — [ToolboxTalkReportsService.cs:437-457](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ToolboxTalkReportsService.cs#L437-L457). A location filter would need to apply to *both* sub-queries (the `ScheduledTalks`-derived employees and the zero-assignment employees pulled directly from `_coreDbContext.Employees`), since a naive filter on only one side would break the "show unassigned employees too" compliance guarantee.
- Endpoints: `GET /api/toolbox-talks/reports/skills-matrix` ([ToolboxTalksController.cs:2223-2243](src/QuantumBuild.API/Controllers/ToolboxTalksController.cs#L2223-L2243), query param today: `category` only) and its Excel export twin `GET /api/toolbox-talks/reports/skills-matrix/export` ([ToolboxTalksController.cs:2250-2264](src/QuantumBuild.API/Controllers/ToolboxTalksController.cs#L2250-L2264)) — both would need the same new param added in lockstep, as the export path re-calls `GetSkillsMatrixAsync` with the identical arguments ([ToolboxTalksController.cs:2259-2262](src/QuantumBuild.API/Controllers/ToolboxTalksController.cs#L2259-L2262)).

**Existing precedent for a site filter, in a sibling report in the same file:** `GetComplianceReportAsync` (same service, [ToolboxTalkReportsService.cs:32-37](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ToolboxTalkReportsService.cs#L32-L37)) already accepts `Guid? siteId` and filters employees by `e.PrimarySiteId == siteId.Value` ([ToolboxTalkReportsService.cs:49-51](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ToolboxTalkReportsService.cs#L49-L51)), then propagates that to the scheduled-talks query via an ID join ([ToolboxTalkReportsService.cs:82-86](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ToolboxTalkReportsService.cs#L82-L86)), and separately produces a full by-site breakdown ([ToolboxTalkReportsService.cs:106-120](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ToolboxTalkReportsService.cs#L106-L120)). The Overdue and Completions reports carry the identical `siteId` parameter and doc-comment "Optional site/department filter" (e.g. [ToolboxTalksController.cs:2118](src/QuantumBuild.API/Controllers/ToolboxTalksController.cs#L2118), [:2149](src/QuantumBuild.API/Controllers/ToolboxTalksController.cs#L2149), [:2182](src/QuantumBuild.API/Controllers/ToolboxTalksController.cs#L2182), [:2284](src/QuantumBuild.API/Controllers/ToolboxTalksController.cs#L2284), [:2326](src/QuantumBuild.API/Controllers/ToolboxTalksController.cs#L2326), [:2373](src/QuantumBuild.API/Controllers/ToolboxTalksController.cs#L2373)). **Skills Matrix is the outlier among the four employee-facing reports in not having this filter.**
- Frontend confirmation the pattern already exists elsewhere: the Completions report page reads `siteId` from the URL query string and renders a site `Select` control — [reports/completions/page.tsx:40](web/src/app/(authenticated)/admin/toolbox-talks/reports/completions/page.tsx#L40), [:48](web/src/app/(authenticated)/admin/toolbox-talks/reports/completions/page.tsx#L48), [:152-153](web/src/app/(authenticated)/admin/toolbox-talks/reports/completions/page.tsx#L152-L153). The Skills Matrix page has no equivalent control (its filters are Category and a client-side learning multi-select, per CLAUDE.md's Skills Matrix UI Features section — no site/location filter listed there).

### C.8 — Staff list view(s): current listing/filtering mechanism, and where location would hook in

- Backend query DTO: `GetEmployeesQueryDto` — `PageNumber`, `PageSize`, `SortColumn`, `SortDirection`, `Search` only — [GetEmployeesQueryDto.cs:3-9](src/Core/QuantumBuild.Core.Application/Features/Employees/DTOs/GetEmployeesQueryDto.cs#L3-L9). **No site/location field.**
- Controller: `GET /api/employees` (paginated list) binds exactly those five query params, nothing else — [EmployeesController.cs:85-100](src/QuantumBuild.API/Controllers/EmployeesController.cs#L85-L100). A second endpoint `GET /api/employees/all` (non-paginated) takes no filter args at all — [EmployeesController.cs:71-80](src/QuantumBuild.API/Controllers/EmployeesController.cs#L71-L80) — this is the endpoint the Schedule/Course assignment employee pickers call (`useAllEmployees()`), confirming those pickers get the entire tenant roster unfiltered by site from the API and filter only client-side by typed search text.
- Frontend: [admin/employees/page.tsx](web/src/app/(authenticated)/admin/employees/page.tsx) — the only filter control is a free-text `Input` bound to `search`, debounced 300ms — [admin/employees/page.tsx:282-286](web/src/app/(authenticated)/admin/employees/page.tsx#L282-L286), state wiring at [admin/employees/page.tsx:53-64](web/src/app/(authenticated)/admin/employees/page.tsx#L53-L64). The table does render employee site as a **display-only** column, "Default Site" (`employee.primarySiteName`) — [admin/employees/page.tsx:180-185](web/src/app/(authenticated)/admin/employees/page.tsx#L180-L185) — but clicking/selecting it does not filter.
- Hook point: same shape as C.7 — add `SiteId`/`SiteIds` to `GetEmployeesQueryDto`, thread it into whatever query backs `GetPaginatedAsync`/`GetAllAsync` in `EmployeeService`, and add a `Select` control to the page mirroring the one already built for the Completions report ([reports/completions/page.tsx:152-153](web/src/app/(authenticated)/admin/toolbox-talks/reports/completions/page.tsx#L152-L153)).

---

## D. Backlog

Searched `BACKLOG.md` and the Backlog section embedded in `CLAUDE.md`.

- **No backlog entry exists for staff Location, location-based scheduling/targeting, or location-based filtering.**
- The only "Location"-adjacent backlog entry is **§2.2.1 "Asset Management"** in [BACKLOG.md:491-582](BACKLOG.md#L491-L582) — a **different, P1, "design partially complete"** feature: a new `Asset` entity (equipment/vehicle/fixed-object training, QR-scan-based), explicitly modeled as "mirrors Employee with deletions." Two direct references to Site/Location as reusable infrastructure, not as something to build:
  - Asset attribute list includes *"Optional Site (physical location)"* — [BACKLOG.md:516](BACKLOG.md#L516)
  - Dependencies list includes *"Site / Location entities (existing)"* — [BACKLOG.md:582](BACKLOG.md#L582)
  
  This confirms the team's own internal shorthand already treats `Site` as "the existing location entity" — consistent with the A.1 classification above — but the Asset entry does not scope or request staff-location targeting/filtering; it's a parallel, unrelated feature (training tracked against equipment, not people) that happens to want to reuse `Site` for its own optional attribute. Asset's own open question at [BACKLOG.md:571](BACKLOG.md#L571) even asks *"Asset → schedule. Can a learning be scheduled on an asset...? v1: scan-only"* — i.e., Asset explicitly defers scheduling-by-target, which is exactly the mechanism this pilot-client ask needs for staff.
- No other `BACKLOG.md` entry, and no entry in CLAUDE.md's own Backlog section (High/Medium/Low), references staff grouping, staff location, or scheduling/filtering by location.

---

## Summary for revive-vs-build decision

| Question | Finding |
|---|---|
| Does staff Location exist (live/partial/absent)? | **Absent** as a named concept. `Site` (construction-site entity) is live, tenant-scoped, real-attributed, and already linked to `Employee` via a single nullable `PrimarySiteId` — functionally the closest thing, and already referred to internally as "the existing location entity" in BACKLOG.md. No legacy remnant of a distinct `Location` entity was ever found in git history. |
| Multi-location per employee today? | No — `PrimarySiteId` is singular and nullable. Any "one or more locations per employee" requirement is a new capability, not a revival. |
| Learning assignment hook point | `CreateToolboxTalkScheduleCommand`'s existing `AssignToAllEmployees` vs. `EmployeeIds` split ([CreateToolboxTalkScheduleCommand.cs:40-45](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/CreateToolboxTalkSchedule/CreateToolboxTalkScheduleCommand.cs#L40-L45)) is the extension point; a location-target parameter would expand to an `EmployeeId` list the same way `AssignToAllEmployees` does today ([CreateToolboxTalkScheduleCommandHandler.cs:77-88](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/CreateToolboxTalkSchedule/CreateToolboxTalkScheduleCommandHandler.cs#L77-L88)). |
| Course assignment hook point | No analogous "all/by-criteria" server-side expansion exists at all today for courses — `AssignCourseDto` takes only an explicit employee list ([AssignCourseDto.cs:5-15](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Features/CourseAssignments/DTOs/AssignCourseDto.cs#L5-L15)); a location target would need a first server-side expansion mechanism built for this flow, not just a filter added to an existing one. |
| Skills Matrix filter hook point | Add `siteId`/`siteIds` to `GetSkillsMatrixAsync` ([ToolboxTalkReportsService.cs:386-389](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ToolboxTalkReportsService.cs#L386-L389)) following the exact pattern already implemented for `GetComplianceReportAsync` in the same file ([ToolboxTalkReportsService.cs:36,49-51,82-86](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ToolboxTalkReportsService.cs#L36-L51)); must apply to both the assigned-employees and zero-assignment-employees sub-queries ([ToolboxTalkReportsService.cs:437-457](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ToolboxTalkReportsService.cs#L437-L457)). |
| Staff list filter hook point | Add `SiteId`/`SiteIds` to `GetEmployeesQueryDto` ([GetEmployeesQueryDto.cs:3-9](src/Core/QuantumBuild.Core.Application/Features/Employees/DTOs/GetEmployeesQueryDto.cs#L3-L9)) and a `Select` control on the employees page, mirroring the Completions report's already-built site filter ([reports/completions/page.tsx:40,48,152-153](web/src/app/(authenticated)/admin/toolbox-talks/reports/completions/page.tsx#L40-L153)). |
| Backlog coverage | None for staff location. Adjacent Asset-management entry (§2.2.1) treats `Site`/`Location` as existing, reusable infrastructure and explicitly defers its own scheduling question. |
