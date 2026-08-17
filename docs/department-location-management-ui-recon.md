# Department / Location (Site) Management UI — Recon

Read-only recon. No code changed. All claims cited `file:line`. Goal: establish
what exists to build a tenant-admin "manage my departments" / "manage my
locations" screen on, and whether it should be one unified screen or two.

---

## A. Backend CRUD that already exists

### A1. Department — `/api/departments`

Controller: [DepartmentsController.cs](../src/QuantumBuild.API/Controllers/DepartmentsController.cs)

| Verb | Route | Handler | Policy |
|---|---|---|---|
| GET | `/api/departments/all` | `GetAll()` — non-paginated list | `[Authorize]` only (any authenticated user) — [DepartmentsController.cs:23-24](../src/QuantumBuild.API/Controllers/DepartmentsController.cs#L23-L24) |
| GET | `/api/departments` | `GetPaginated(pageNumber, pageSize, sortColumn, sortDirection, search)` | `[Authorize]` only — [DepartmentsController.cs:39-40](../src/QuantumBuild.API/Controllers/DepartmentsController.cs#L39-L40) |
| GET | `/api/departments/{id:guid}` | `GetById(id)` | `[Authorize]` only — [DepartmentsController.cs:61-62](../src/QuantumBuild.API/Controllers/DepartmentsController.cs#L61-L62) |
| POST | `/api/departments` | `Create(CreateDepartmentDto)` | `Core.ManageDepartments` — [DepartmentsController.cs:77-79](../src/QuantumBuild.API/Controllers/DepartmentsController.cs#L77-L79) |
| PUT | `/api/departments/{id:guid}` | `Update(id, UpdateDepartmentDto)` | `Core.ManageDepartments` — [DepartmentsController.cs:97-99](../src/QuantumBuild.API/Controllers/DepartmentsController.cs#L97-L99) |

**No DELETE endpoint** — deliberate. The controller's XML doc says deactivation is
done via `PUT` with `IsActive = false`; there is no separate delete because
employees may already reference the department (nullable FK, `SetNull` on true
removal intentionally not exposed) — [DepartmentsController.cs:91-96](../src/QuantumBuild.API/Controllers/DepartmentsController.cs#L91-L96).

**DTOs** ([Departments/DTOs/*.cs](../src/Core/QuantumBuild.Core.Application/Features/Departments/DTOs/)):
- `CreateDepartmentDto(string Name, string? Code, bool IsActive)` — [CreateDepartmentDto.cs:3-7](../src/Core/QuantumBuild.Core.Application/Features/Departments/DTOs/CreateDepartmentDto.cs#L3-L7)
- `UpdateDepartmentDto(string Name, string? Code, bool IsActive)` — [UpdateDepartmentDto.cs:3-7](../src/Core/QuantumBuild.Core.Application/Features/Departments/DTOs/UpdateDepartmentDto.cs#L3-L7)
- `DepartmentDto(Guid Id, string Name, string? Code, bool IsActive)` — [DepartmentDto.cs:3-8](../src/Core/QuantumBuild.Core.Application/Features/Departments/DTOs/DepartmentDto.cs#L3-L8)
- `GetDepartmentsQueryDto(int PageNumber=1, int PageSize=20, string? SortColumn, string? SortDirection, string? Search)` — [GetDepartmentsQueryDto.cs:3-9](../src/Core/QuantumBuild.Core.Application/Features/Departments/DTOs/GetDepartmentsQueryDto.cs#L3-L9)

**Service** ([DepartmentService.cs](../src/Core/QuantumBuild.Core.Application/Features/Departments/DepartmentService.cs)):
- Uniqueness validated on Create/Update by `Name` only (not `Code`) — checked against `_context.Departments` (already tenant-filtered by the DbContext-level query filter, see A3) — [DepartmentService.cs:108-114](../src/Core/QuantumBuild.Core.Application/Features/Departments/DepartmentService.cs#L108-L114) and [DepartmentService.cs:146-152](../src/Core/QuantumBuild.Core.Application/Features/Departments/DepartmentService.cs#L146-L152). Entity also enforces this at the DB level via a unique index on `{TenantId, Name}` — [DepartmentConfiguration.cs:20-22](../src/Core/QuantumBuild.Core.Infrastructure/Data/Configurations/DepartmentConfiguration.cs#L20-L22).
- Sortable columns: `name`, `code`, `isactive` (default `name`) — [DepartmentService.cs:69-80](../src/Core/QuantumBuild.Core.Application/Features/Departments/DepartmentService.cs#L69-L80).
- Search matches `Name` or `Code` (case-insensitive `Contains`) — [DepartmentService.cs:41-48](../src/Core/QuantumBuild.Core.Application/Features/Departments/DepartmentService.cs#L41-L48).

**Verdict: usable as-is.** List/create/update/search/sort/paginate/deactivate are
all present and shaped exactly like a management-grid data source needs
(`PaginatedList<DepartmentDto>` with `pageNumber`/`pageSize`/`totalCount`, same
envelope every other admin grid in the app uses). Nothing is missing for a
straightforward Department management screen.

### A2. Site — `/api/sites`

Controller: [SitesController.cs](../src/QuantumBuild.API/Controllers/SitesController.cs)

| Verb | Route | Handler | Policy |
|---|---|---|---|
| GET | `/api/sites/all` | `GetAll()` | `[Authorize]` only — [SitesController.cs:24-25](../src/QuantumBuild.API/Controllers/SitesController.cs#L24-L25) |
| GET | `/api/sites` | `GetPaginated(...)` | `[Authorize]` only — [SitesController.cs:46-47](../src/QuantumBuild.API/Controllers/SitesController.cs#L46-L47) |
| GET | `/api/sites/{id:guid}` | `GetById(id)` | `[Authorize]` only — [SitesController.cs:70-71](../src/QuantumBuild.API/Controllers/SitesController.cs#L70-L71) |
| POST | `/api/sites` | `Create(CreateSiteDto)` | `Core.ManageSites` — [SitesController.cs:88-90](../src/QuantumBuild.API/Controllers/SitesController.cs#L88-L90) |
| PUT | `/api/sites/{id:guid}` | `Update(id, UpdateSiteDto)` | `Core.ManageSites` — [SitesController.cs:108-110](../src/QuantumBuild.API/Controllers/SitesController.cs#L108-L110) |
| DELETE | `/api/sites/{id:guid}` | `Delete(id)` (real soft delete, sets `IsDeleted = true`) | `Core.ManageSites` — [SitesController.cs:127-129](../src/QuantumBuild.API/Controllers/SitesController.cs#L127-L129), [SiteService.cs:411-427](../src/Core/QuantumBuild.Core.Application/Features/Sites/SiteService.cs#L411-L427) |

Unlike Department, Site has **two independent removal mechanisms**:
1. `IsActive` (bool field on the entity, settable via `Update`) — hides from
   dropdown-style selection while the record stays fully manageable and
   resolvable, same convention as Department.
2. `DELETE /api/sites/{id}` — a genuine soft delete (`site.IsDeleted = true` —
   [SiteService.cs:423](../src/Core/QuantumBuild.Core.Application/Features/Sites/SiteService.cs#L423)), which then excludes the row from every query
   because of the DbContext-level query filter (A3). A site an admin "deletes"
   this way disappears even from `GetById`, unlike deactivation.

**Service** ([SiteService.cs](../src/Core/QuantumBuild.Core.Application/Features/Sites/SiteService.cs)):
- Create/Update validate `SiteManagerId` (must be an existing Employee) and
  `CompanyId` (must be an existing Company) when provided — [SiteService.cs:192-213](../src/Core/QuantumBuild.Core.Application/Features/Sites/SiteService.cs#L192-L213), [SiteService.cs:300-322](../src/Core/QuantumBuild.Core.Application/Features/Sites/SiteService.cs#L300-L322).
- Uniqueness validated on `SiteCode` (not `SiteName`) — [SiteService.cs:216-222](../src/Core/QuantumBuild.Core.Application/Features/Sites/SiteService.cs#L216-L222), [SiteService.cs:325-331](../src/Core/QuantumBuild.Core.Application/Features/Sites/SiteService.cs#L325-L331).
- Sortable columns: `sitecode`, `sitename`, `city`, `sitemanagername`, `companyname`, `isactive` — [SiteService.cs:122-140](../src/Core/QuantumBuild.Core.Application/Features/Sites/SiteService.cs#L122-L140).
- Search matches `SiteCode`, `SiteName`, `City`, `Address` — [SiteService.cs:68-77](../src/Core/QuantumBuild.Core.Application/Features/Sites/SiteService.cs#L68-L77).
- `FloatProjectId` set implicitly stamps `FloatLinkedAt`/`FloatLinkMethod = "Manual"` as a side effect of Create/Update — [SiteService.cs:242-243](../src/Core/QuantumBuild.Core.Application/Features/Sites/SiteService.cs#L242-L243), [SiteService.cs:349-365](../src/Core/QuantumBuild.Core.Application/Features/Sites/SiteService.cs#L349-L365).

**Verdict: usable as-is, and more complete than Department** (it already has a
real delete, not just deactivate). A management UI does not need any new
backend work for Site either — but the DTO is heavier (see A3) and a UI must
choose which fields to expose.

### A3. Entity shapes as a management UI would see them

**Department** (`Department : TenantEntity`, [Department.cs](../src/Core/QuantumBuild.Core.Domain/Entities/Department.cs)):
- `Name` (string, required, max 100 — [DepartmentConfiguration.cs:13-15](../src/Core/QuantumBuild.Core.Infrastructure/Data/Configurations/DepartmentConfiguration.cs#L13-L15))
- `Code` (string?, max 20 — [DepartmentConfiguration.cs:17-18](../src/Core/QuantumBuild.Core.Infrastructure/Data/Configurations/DepartmentConfiguration.cs#L17-L18))
- `IsActive` (bool, default true)

Every field on the entity is user-relevant. **Nothing to hide.**

**Site** (`Site : TenantEntity`, [Site.cs](../src/Core/QuantumBuild.Core.Domain/Entities/Site.cs)), full `SiteDto` shape ([SiteDto.cs:3-24](../src/Core/QuantumBuild.Core.Application/Features/Sites/DTOs/SiteDto.cs#L3-L24)):

| Field | User-relevant for a "Location" screen? |
|---|---|
| `SiteCode`, `SiteName` | Yes — required identity fields |
| `Address`, `City`, `PostalCode` | Yes |
| `SiteManagerId` / `SiteManagerName` | Yes, but is an Employee picker (extra dependency a Department screen doesn't have) |
| `CompanyId` / `CompanyName` | Yes, but is a Company picker (same caveat — and there is currently no admin Company management page either, see B4) |
| `Phone`, `Email`, `Notes` | Yes |
| `IsActive` | Yes |
| `Latitude`, `Longitude`, `GeofenceRadiusMeters` | **No** — geofencing/construction-specific. The entity's own doc comment calls it "a construction site where stock can be ordered and delivered" — [Site.cs:5-7](../src/Core/QuantumBuild.Core.Domain/Entities/Site.cs#L5-L7) |
| `FloatProjectId`, `FloatLinkedAt`, `FloatLinkMethod` | **No** — third-party scheduling tool (Float) integration, construction-sector-specific — [Site.cs:90-103](../src/Core/QuantumBuild.Core.Domain/Entities/Site.cs#L90-L103) |

A general-purpose "Manage Locations" screen (multi-sector tenants, not just
construction) should expose the top group and omit geofence/Float fields
entirely — or put them behind an "Advanced" collapsible section reserved for
tenants that actually use geofencing/Float. The **API already returns and
accepts all fields** — `CreateSiteDto`/`UpdateSiteDto` require the caller to
supply values for irrelevant fields ([CreateSiteDto.cs:3-19](../src/Core/QuantumBuild.Core.Application/Features/Sites/DTOs/CreateSiteDto.cs#L3-L19)), so a
scoped-down UI would simply omit those inputs and rely on the DTOs' nullability
(all the fields to hide are nullable) rather than needing any backend change.

**Tenant scoping (both entities):** enforced at the `ApplicationDbContext`
model level, not per-entity-config, per the cross-tenant fix in CLAUDE.md note
14:
```
modelBuilder.Entity<Site>().HasQueryFilter(e => !e.IsDeleted && (BypassTenantFilter || e.TenantId == TenantId));
modelBuilder.Entity<Department>().HasQueryFilter(e => !e.IsDeleted && (BypassTenantFilter || e.TenantId == TenantId));
```
[ApplicationDbContext.cs:337-338](../src/Core/QuantumBuild.Core.Infrastructure/Data/ApplicationDbContext.cs#L337-L338). Both are safe by default — no explicit `TenantId` filtering needed in service code, `_context.Departments`/`_context.Sites` are already tenant-scoped.

---

## B. Where the management UI should live

### B4. Existing admin structure

The admin app currently has **no dedicated Sites or Companies management page
at all** — despite CLAUDE.md's frontend page table listing `/admin/sites` and
`/admin/companies`, no such route exists in the current tree (`Glob` for
`admin/sites/**` and `admin/companies/**` returns nothing). The admin dashboard
quick-links only surface **Employees** and **Users** —
[admin/page.tsx:32-51](../web/src/app/(authenticated)/admin/page.tsx#L32-L51) (`quickLinks` array has exactly two entries). CLAUDE.md is stale
on this point; Site (and Company) CRUD today is API-only, exactly as the
project memory for the Department formalisation chunk already flagged: *"No
admin UI page for managing departments yet (mirrors that Site itself also has
no dedicated admin CRUD page today, only API + combobox)"*.

Two live precedents for where tenant-level lookup/config screens are grouped today:

1. **`/admin/settings/*`** — a permission-gated section (`Learnings.Admin` OR
   `Core.ManageUsers`) with a tab strip: General / Languages / Lookups —
   [admin/settings/layout.tsx:9-18](../web/src/app/(authenticated)/admin/settings/layout.tsx#L9-L18), [admin/settings/layout.tsx:15-18](../web/src/app/(authenticated)/admin/settings/layout.tsx#L15-L18). This is
   where tenant-wide configuration and lookup-style data already lives.
2. **`/admin/employees`, `/admin/users`** — dedicated top-level sections with
   their own `layout.tsx` permission gate, a `DataTable`-driven list page, and
   separate `/new` and `/[id]/edit` routes.

A "Manage Departments" / "Manage Locations" screen fits the pattern of (1) more
naturally than (2): both are simple lookup-shaped tenant config, not
high-volume record types needing dedicated create/edit pages. The existing
`/admin/settings` tab strip ([settingsNavItems](../web/src/app/(authenticated)/admin/settings/layout.tsx#L9-L13)) is a plausible place to add
"Departments" and/or "Locations" tabs alongside General/Languages/Lookups.

### B5. Closest existing CRUD pattern to mirror

Two candidate precedents exist, and they differ significantly:

**Precedent 1 — the "Lookups" screen** (`/admin/settings/lookups`,
[page.tsx](../web/src/app/(authenticated)/admin/settings/lookups/page.tsx)): a card-per-category, inline-editable list —
add via a small form at the bottom of the card, inline rename, drag-to-reorder,
enable/disable toggle or delete — all in [LookupCategorySection](../web/src/components/admin/lookup-category-section.tsx) ([lookup-category-section.tsx:46-420](../web/src/components/admin/lookup-category-section.tsx#L46-L420)). This is the more structurally similar
precedent for Department (small flat entity, no relations) and would need only
a new card wired to `/api/departments` instead of `/api/lookups/Department/values`.
**Important wrinkle:** this exact screen **already renders a card literally
titled "Departments"** — [lookup-category-section.tsx:28-31](../web/src/components/admin/lookup-category-section.tsx#L28-L31) — but it is
wired to the old `LookupCategory`/`TenantLookupValue` system (`/api/lookups`,
see C below), **not** the new `Department` entity. See C for why this matters.

**Precedent 2 — the "Employees" list page** (`/admin/employees`,
[page.tsx](../web/src/app/(authenticated)/admin/employees/page.tsx)): full `DataTable` component
([data-table.tsx](../web/src/components/shared/data-table.tsx)) with URL-driven pagination/sort/search state, a
permission-gated `layout.tsx`, dropdown row actions (View/Edit/Delete), and
separate `/new` + `/[id]/edit` form routes — [employees/page.tsx:43-318](../web/src/app/(authenticated)/admin/employees/page.tsx#L43-L318), [employees/layout.tsx:7-35](../web/src/app/(authenticated)/admin/employees/layout.tsx#L7-L35).
This is the precedent for a heavier, dedicated-page CRUD screen, and is the
better fit for **Site/Location** given its larger field set (10+ fields incl.
two foreign-key pickers), even scoped down to only user-relevant fields (A3).

### B6. Permission model

- `Core.ManageDepartments` — write-gates Department create/update (A1). Not
  currently documented in CLAUDE.md's Core permission table (that table lists
  `ManageEmployees`, `ManageSites`, `ManageCompanies`, `ManageUsers` but omits
  `ManageDepartments` — a documentation gap, this permission is real and live:
  [Permissions.cs:25-30](../src/Core/QuantumBuild.Core.Infrastructure/Identity/Permissions.cs#L25-L30)).
- `Core.ManageSites` — write-gates Site create/update/delete (A2).
- Both are assigned to **Admin** (all permissions except `Tenant.Manage`) and
  **SuperUser** (all permissions) by the seeder — [DataSeeder.cs:385-386](../src/Core/QuantumBuild.Core.Infrastructure/Persistence/DataSeeder.cs#L385-L386),
  [DataSeeder.cs:383](../src/Core/QuantumBuild.Core.Infrastructure/Persistence/DataSeeder.cs#L383). **Neither Supervisor nor Operator** gets either
  permission — [DataSeeder.cs:388-393](../src/Core/QuantumBuild.Core.Infrastructure/Persistence/DataSeeder.cs#L388-L393) (Supervisor only gets
  `Learnings.View`/`Learnings.Schedule`; Operator only `Learnings.View`).
  `Core.ManageSites` was in fact explicitly *removed* from Supervisor in an
  earlier chunk — [DataSeeder.cs:240-242](../src/Core/QuantumBuild.Core.Infrastructure/Persistence/DataSeeder.cs#L240-L242).
- **Both permissions already exist and are already correctly scoped for
  "tenant admin manages their own lookups."** No new permission is needed —
  a management UI for either entity can gate its layout on
  `Core.ManageDepartments` / `Core.ManageSites` directly (mirroring how
  `admin/employees/layout.tsx` gates on `Core.ManageEmployees`), or on both if
  unified (C).
- Contrast: the **old** Lookup-category "Departments" card (B5, Precedent 1) is
  gated on `Learnings.Admin`, a completely different permission from
  `Core.ManageDepartments` — [LookupsController.cs:42,57,72,87,102](../src/QuantumBuild.API/Controllers/LookupsController.cs#L42) (all
  four mutating actions use `[Authorize(Policy = "Learnings.Admin")]`). A
  tenant Admin has both permissions, so this is invisible to them today, but it
  means the two "Departments" surfaces are not just different data, they are
  gated by different policies too.

---

## C. Unified vs separate

### What the two CRUD surfaces actually share

| | Department | Site (scoped to user-relevant fields) |
|---|---|---|
| Fields | `Name`, `Code`, `IsActive` (3) | `SiteCode`, `SiteName`, `Address`, `City`, `PostalCode`, `SiteManagerId`, `CompanyId`, `Phone`, `Email`, `IsActive`, `Notes` (11, two of which are entity pickers) |
| Uniqueness | `Name` (with DB unique index on `{TenantId, Name}`) | `SiteCode` (checked in service, no separate unique index found in `SiteConfiguration`†) |
| Removal | Deactivate only (`IsActive = false`, no delete) | Deactivate (`IsActive = false`) **and** real soft-delete (`DELETE`, sets `IsDeleted`) |
| Related pickers | None | `SiteManagerId` → Employee, `CompanyId` → Company (Company itself has no admin management page either) |
| Sort/search/paginate shape | Identical envelope (`PaginatedList<T>`, `GetPaginated` query pattern) | Identical envelope |
| List endpoint for selects | `/api/departments/all` | `/api/sites/all` |

† not independently verified in this pass — `SiteConfiguration.cs` was not
read; the uniqueness guarantee for `SiteCode` comes only from the
service-layer check in [SiteService.cs:216-222](../src/Core/QuantumBuild.Core.Application/Features/Sites/SiteService.cs#L216-L222).

**A generic unified "manage lookups" screen** (one component, driven by config
describing which fields/entity) would need to handle: a variable field set (3
vs 11 fields), one relation-free entity vs one with two FK pickers, and two
different removal semantics (deactivate-only vs deactivate-or-delete). This is
very close to what the *existing* Lookup system already generalizes over — but
that system's `TenantLookupValue` entity is deliberately minimal (`Code`,
`Name`, `Metadata` (freeform JSON string), `SortOrder`, `IsEnabled` —
[TenantLookupValue.cs:11-15](../src/Core/QuantumBuild.Core.Domain/Entities/TenantLookupValue.cs#L11-L15)) specifically because every category using it is
flat and relation-free. Site does not fit that shape without stuffing
`SiteManagerId`/`CompanyId`/etc. into the freeform `Metadata` string, which
would forfeit real foreign-key integrity, validation, and joined display names
that `SiteService` already provides.

**Two separate focused screens** — "Manage Departments" (mirrors Precedent 1's
simplicity) and "Manage Locations" (mirrors Precedent 2's dedicated-page
richness) — match each entity's actual shape without forcing one generic
abstraction to cover both a 3-field flat lookup and an 11-field entity with two
relations. Given B5 already identifies two *different* best-fit precedents for
the two entities, building two focused screens is the smaller, more direct
path; a unified screen would need to be engineered generically enough to
degrade gracefully for Site's extra complexity, for a payoff (one shared
component) that mostly matters if more lookup-shaped entities are expected to
follow this same pattern later.

### The naming collision this creates

Whatever gets built, it will **not** be the first thing in the product called
"Departments." The existing Lookup-category "Departments" card on
`/admin/settings/lookups` (B5) is fully functional today (create, rename,
reorder, delete) and sits on a page whose description literally says "Manage
learning categories, **departments**, and job titles" —
[lookups/page.tsx:15-18](../web/src/app/(authenticated)/admin/settings/lookups/page.tsx#L15-L18). Per the project memory for the
formalisation chunk, this old Lookup-category Department system is now
**disconnected** from `Employee.DepartmentId` — its wiring was removed from
all three employee forms in chunk 1, but the `LookupCategory` row and any
`TenantLookupValue` rows under it were deliberately left untouched in the
database, and the category is still unconditionally re-seeded
([DataSeeder.cs:432-436](../src/Core/QuantumBuild.Core.Infrastructure/Persistence/DataSeeder.cs#L432-L436)) and still fully editable via `/admin/settings/lookups`
today. Any new "Manage Departments" screen needs a namespacing decision (e.g.
relocate/relabel/retire the old Lookup card) or tenants will see two
independently-editable "Departments" lists that do different things and share
no data — this is flagged as a fact to resolve during design, not fixed here
per NON-SCOPE.

---

## D. Integration points

### D8. Consumption in the employee form

Both selects live in the same component, `employee-form.tsx`, and follow an
**identical pattern**:

- **Site** — `useAllSites()` ([use-sites.ts:23-28](../web/src/lib/api/admin/use-sites.ts#L23-L28), calling `GET /api/sites/all`) feeds
  a `Select` at [employee-form.tsx:434-465](../web/src/components/admin/employee-form.tsx#L434-L465), filtered client-side to
  `sites?.filter(s => s.isActive)` ([employee-form.tsx:452](../web/src/components/admin/employee-form.tsx#L452)).
- **Department** — `useAllDepartments()` ([use-departments.ts:22-27](../web/src/lib/api/admin/use-departments.ts#L22-L27), calling
  `GET /api/departments/all`) feeds a `Select` at [employee-form.tsx:237-265](../web/src/components/admin/employee-form.tsx#L237-L265),
  filtered client-side to `departments?.filter(d => d.isActive)`
  ([employee-form.tsx:255](../web/src/components/admin/employee-form.tsx#L255)).

**Consequence for "does a newly created department/location immediately
appear":** yes — both selects read the live `/all` endpoint via TanStack Query,
and both the Department and Site create/update mutation hooks
(`useCreateDepartment`/`useUpdateDepartment` in
[use-departments.ts:37-58](../web/src/lib/api/admin/use-departments.ts#L37-L58), `useCreateSite`/`useUpdateSite`/`useDeleteSite`
in [use-sites.ts:38-70](../web/src/lib/api/admin/use-sites.ts#L38-L70)) already call
`queryClient.invalidateQueries({queryKey: DEPARTMENTS_KEY / SITES_KEY})` on
success. **The full frontend CRUD hook layer for both entities already exists
and is wired for cache invalidation — it is simply not called from any page
yet**, since no management screen exists to call it. Building the management
UI is wiring a new page onto an already-complete API + hooks stack, not
building new plumbing.

**Consequence for "does deactivating behave sensibly":** yes for the common
case — deactivating (`IsActive = false`) removes the option from both selects'
dropdown list on next fetch, while the entity itself remains resolvable by ID
(Department has no delete at all; Site's `IsActive` deactivation is distinct
from its separate real-delete). One edge case neither form appears to handle:
if an employee's *currently assigned* department/site becomes inactive, the
`Select`'s option list will no longer contain that department/site's `id`
(the filter excludes it unconditionally, including the one currently
selected), so re-opening that employee's edit form would show the dropdown
without its current value present in the list, even though the underlying
`departmentId`/`primarySiteId` on the employee is untouched. This is a
pre-existing UI display gap in the current single-select forms, not something
a new management screen introduces or needs to fix, but worth noting since a
new screen makes deactivation more discoverable/likely to happen.

**Employee list / reports:** per the Department formalisation project memory,
downstream read-only consumers (Employee detail page, Skills Matrix Excel
export, `SupervisorOperatorDto`) were deliberately left on the legacy
free-text `Employee.Department` field in chunk 1 and were not migrated to
`DepartmentId`/`AssignedDepartment`. A department management screen does not
change that gap, but any UI copy describing "where do departments show up"
should not claim reports already reflect the new structured field —
they don't, yet.

---

## Summary for scoping

- **Department**: backend is 100% usable as-is (list/paginate/search/sort/
  create/update/deactivate). Frontend API client + TanStack Query hooks already
  exist and already invalidate correctly. Only a page is missing. Entity has 3
  fields, all user-relevant, no relations, no delete (deactivate only).
- **Site/Location**: backend is 100% usable as-is (list/paginate/search/sort/
  create/update/deactivate/delete). Frontend API client + hooks already exist.
  Only a page is missing. Entity has ~11 user-relevant fields (including two
  FK pickers to Employee and Company) plus geofence/Float fields that a general
  Location screen should omit or hide behind an advanced section.
- **Where**: `/admin/settings` (alongside General/Languages/Lookups) is the
  natural home given both are tenant-config/lookup-shaped, not
  high-volume-record-shaped; Site's relative complexity is the case for giving
  it more page real estate (Precedent 2 shape) than Department (Precedent 1
  shape) even if both live under Settings.
- **Permissions**: `Core.ManageDepartments` and `Core.ManageSites` already
  exist, are correctly tenant-scoped, and are already assigned to exactly the
  roles (Admin, SuperUser) that should get them. No new permission plumbing
  needed. `CLAUDE.md`'s permission table should be updated to include
  `Core.ManageDepartments` (documentation gap, not a code gap).
  the old Lookup-category "Departments" card
  is gated on the *different* `Learnings.Admin` permission — a fact to be
  aware of, not a blocker, since Admin/SuperUser hold both.
- **Unified vs separate**: separate is the better shape given the field-count
  and relation-count mismatch (3 flat fields vs 11 fields with two FK
  pickers), and given the two closest existing precedents (Lookups card vs.
  Employees-style dedicated page) are themselves different UI shapes.
- **Naming collision to resolve during design (not fixed here)**: an old,
  fully-functional, but now-disconnected "Departments" Lookup card already
  exists on `/admin/settings/lookups`, wired to a different backend
  (`/api/lookups`) and a different permission (`Learnings.Admin`). Building a
  new "Manage Departments" screen without addressing this leaves two
  same-named, independently-editable department lists in the product.
