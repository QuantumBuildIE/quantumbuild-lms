# Staff Department — Recon

Read-only recon. No code changed. All claims cite `file:line`. Facts only — no design, no fix, no migration.

This recon assumes the decision already made: formalise `Employee.Department` (free-text) into a tenant-scoped lookup entity, one department per employee, **optional** (nullable FK), mirroring `Site`. The shared assignment/scheduling and report-filtering flows are already mapped in [docs/staff-location-recon.md](staff-location-recon.md) — not re-mapped here. This document covers only the department-specific facts: current field usage, the reference "no site" behaviour department's "no department" case must mirror, the exact `Site` pattern to copy, and a read-only reconciliation query.

---

## A0. How "no site" is handled today — the reference pattern for "no department"

`Employee.PrimarySiteId` is `Guid?` — nullable — [Employee.cs:63](../src/Core/QuantumBuild.Core.Domain/Entities/Employee.cs#L63). Three report methods in [ToolboxTalkReportsService.cs](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ToolboxTalkReportsService.cs) accept an **optional single-value** `Guid? siteId` filter, all with the identical shape:

```csharp
if (siteId.HasValue)
{
    employeesQuery = employeesQuery.Where(e => e.PrimarySiteId == siteId.Value);
}
```
— `GetComplianceReportAsync` [ToolboxTalkReportsService.cs:49-51](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ToolboxTalkReportsService.cs#L49-L51) (also re-applied to the scheduled-talks join at [:82-86](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ToolboxTalkReportsService.cs#L82-L86)), the Overdue report at [:241-243](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ToolboxTalkReportsService.cs#L241-L243), and the Completions report at [:321-323](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ToolboxTalkReportsService.cs#L321-L323).

**Exact "no site" behaviour, precisely:**

1. **Filter not applied at all (`siteId == null`, the default/"All Sites" case):** every employee is included regardless of whether `PrimarySiteId` is null or set — there is no exclusion. This is the only path a null-site employee can appear in a site-filtered report.
2. **Filter applied to a specific site (`siteId` has a value):** the predicate is `e.PrimarySiteId == siteId.Value`. In SQL/LINQ this is a strict equality — a `null` `PrimarySiteId` can never equal a non-null `siteId.Value`, so **null-site employees are silently excluded** whenever any specific site is selected.
3. **There is no explicit "no site" option anywhere.** The `Guid? siteId` parameter has exactly two states — "unfiltered" (`null`) or "exactly this site" — never "employees with no site set." Confirmed at the one live frontend site-filter control, the Completions report page: the `Select` has only `"all"` (→ `siteId: null`) plus one `SelectItem` per real site from `sitesData?.items` — [reports/completions/page.tsx:150-166](../web/src/app/(authenticated)/admin/toolbox-talks/reports/completions/page.tsx#L150-L166). No sentinel value (e.g. `Guid.Empty`, `"none"`) exists for "unassigned."
4. The Skills Matrix report (`GetSkillsMatrixAsync`) has **no** `siteId` parameter at all today — confirmed in the location recon ([staff-location-recon.md §C.7](staff-location-recon.md#c7--skills-matrix-current-queryfilter-mechanism-and-where-location-would-hook-in)) — so null-site employees are trivially included there (no filter exists to exclude them).

**Reference pattern for "no department," stated precisely:** a nullable single-value `Guid? departmentId` filter, unfiltered by default (includes employees with no department), and — if department gets the identical treatment as site — selecting a specific department excludes no-department employees with no explicit way to select "no department" as its own bucket. If the formalisation wants a different (better) UX for the no-department case, that is a deliberate deviation from the existing precedent, not a continuation of it — flag it as a design decision, not an accident.

---

## A. Current usage of the free-text `Employee.Department` field

### A.1 — Entity, storage, validation

- `Employee.Department` — `string?`, nullable — [Employee.cs:50-53](../src/Core/QuantumBuild.Core.Domain/Entities/Employee.cs#L50-L53).
- EF configuration: `HasMaxLength(100)`, no other constraint (no unique index, no FK) — [EmployeeConfiguration.cs:37-38](../src/Core/QuantumBuild.Core.Infrastructure/Data/Configurations/EmployeeConfiguration.cs#L37-L38).
- FluentValidation: `MaximumLength(100)` only, applied identically on create and update — [CreateEmployeeValidator.cs:50-53](../src/Core/QuantumBuild.Core.Application/Features/Employees/CreateEmployeeValidator.cs#L50-L53), [UpdateEmployeeValidator.cs:50-53](../src/Core/QuantumBuild.Core.Application/Features/Employees/UpdateEmployeeValidator.cs#L50-L53).

### A.2 — Backend read/write sites (every DTO and service touchpoint)

`Department` is carried end-to-end as a plain `string?` through every employee-shaped DTO:

| DTO | File:line |
|---|---|
| `EmployeeDto` (API response) | [EmployeeDto.cs:13](../src/Core/QuantumBuild.Core.Application/Features/Employees/DTOs/EmployeeDto.cs#L13) |
| `CreateEmployeeDto` | [CreateEmployeeDto.cs:15](../src/Core/QuantumBuild.Core.Application/Features/Employees/DTOs/CreateEmployeeDto.cs#L15) |
| `UpdateEmployeeDto` | [UpdateEmployeeDto.cs:11](../src/Core/QuantumBuild.Core.Application/Features/Employees/DTOs/UpdateEmployeeDto.cs#L11) |
| `SupervisorOperatorDto` (My Team / assignment lists) | [SupervisorOperatorDto.cs:7](../src/Core/QuantumBuild.Core.Application/Features/Employees/DTOs/SupervisorOperatorDto.cs#L7) |
| `CreateUserEmployeeDto` (inline employee-creation-from-user-form) | [CreateUserEmployeeDto.cs:12](../src/Core/QuantumBuild.Core.Application/Features/Users/DTOs/CreateUserEmployeeDto.cs#L12) |
| `CreateEmployeeForUserDto` (link-existing-user-to-new-employee) | [UserLinkageDto.cs:18](../src/Core/QuantumBuild.Core.Application/Features/Users/DTOs/UserLinkageDto.cs#L18) |

`EmployeeService.cs` reads/writes `.Department` in six places: `GetAllAsync` projection [:70](../src/Core/QuantumBuild.Core.Application/Features/Employees/EmployeeService.cs#L70), `GetPaginatedAsync` projection [:138](../src/Core/QuantumBuild.Core.Application/Features/Employees/EmployeeService.cs#L138), `GetByIdAsync` projection [:207](../src/Core/QuantumBuild.Core.Application/Features/Employees/EmployeeService.cs#L207), `CreateAsync` entity assignment [:302](../src/Core/QuantumBuild.Core.Application/Features/Employees/EmployeeService.cs#L302) and response projection [:448](../src/Core/QuantumBuild.Core.Application/Features/Employees/EmployeeService.cs#L448), `UpdateAsync` entity assignment [:660](../src/Core/QuantumBuild.Core.Application/Features/Employees/EmployeeService.cs#L660) and response projection [:751](../src/Core/QuantumBuild.Core.Application/Features/Employees/EmployeeService.cs#L751), `GetUnlinkedAsync` projection [:935](../src/Core/QuantumBuild.Core.Application/Features/Employees/EmployeeService.cs#L935), `LinkToUserAsync` projection [:1108](../src/Core/QuantumBuild.Core.Application/Features/Employees/EmployeeService.cs#L1108), `CreateUserForEmployeeAsync` projection [:1234](../src/Core/QuantumBuild.Core.Application/Features/Employees/EmployeeService.cs#L1234).

**Not used in the paginated employee search filter.** `GetPaginatedAsync`'s search predicate matches `EmployeeCode`, `FirstName`, `LastName`, full name, `Email`, and `JobTitle` — but **not** `Department` — [EmployeeService.cs:108-115](../src/Core/QuantumBuild.Core.Application/Features/Employees/EmployeeService.cs#L108-L115). `GetEmployeesQueryDto` itself has no department field at all — only `PageNumber/PageSize/SortColumn/SortDirection/Search` — [GetEmployeesQueryDto.cs:3-9](../src/Core/QuantumBuild.Core.Application/Features/Employees/DTOs/GetEmployeesQueryDto.cs#L3-L9). Sorting also has no `department` case in `ApplySorting` — [EmployeeService.cs:165-185](../src/Core/QuantumBuild.Core.Application/Features/Employees/EmployeeService.cs#L165-L185) (only `employeecode/firstname/lastname/fullname/email/jobtitle/primarysitename/isactive`).

Other backend writers of `Employee.Department`:
- `UserService.cs` — inline employee creation from the user form, two call sites: [:330](../src/Core/QuantumBuild.Core.Application/Features/Users/UserService.cs#L330) (create-user-with-new-employee) and [:841](../src/Core/QuantumBuild.Core.Application/Features/Users/UserService.cs#L841) (`CreateEmployeeForUserAsync`-style flow).
- `TenantOnboardingService.cs` — hardcodes `Department = "Management"` for the auto-created Admin employee on tenant creation — [TenantOnboardingService.cs:140](../src/Core/QuantumBuild.Core.Application/Features/Tenants/TenantOnboardingService.cs#L140).
- `DataSeeder.cs` — same hardcode for the seeded dev Admin employee — [DataSeeder.cs:748](../src/Core/QuantumBuild.Core.Infrastructure/Persistence/DataSeeder.cs#L748).
- `SupervisorAssignmentService.cs` — read-only projection of operator `Department` in two places: assigned-operators list [:46](../src/Core/QuantumBuild.Core.Application/Features/Employees/SupervisorAssignmentService.cs#L46) and available-operators list [:88](../src/Core/QuantumBuild.Core.Application/Features/Employees/SupervisorAssignmentService.cs#L88).

**Genuine report/export usage (not just pass-through):**
- Skills Matrix Excel export writes `employee.Department` as column 3 — [ToolboxTalkExportService.cs:74](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ToolboxTalkExportService.cs#L74) (header), [:101](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ToolboxTalkExportService.cs#L101) (data). Backing DTO field: `SkillsMatrixDto.cs:21`.

**False friend — "Department" in the Compliance report is NOT `Employee.Department`.** `ComplianceReportDto.ByDepartment` (type `DepartmentComplianceDto`) is entirely `Site`-derived: the service iterates `_coreContext.Sites` and sets `DepartmentName = site.SiteName` — [ToolboxTalkReportsService.cs:106-138](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ToolboxTalkReportsService.cs#L106-L138) (`DepartmentComplianceDto` shape at [ComplianceReportDto.cs:72-108](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/DTOs/Reports/ComplianceReportDto.cs#L72-L108)). The free-text `Employee.Department` field plays **no role** in this breakdown. Both frontend compliance pages render it under the heading "Compliance by Department" with subtitle "Completion rates **by site**" — [admin/toolbox-talks/reports/compliance/page.tsx:279-280](../web/src/app/(authenticated)/admin/toolbox-talks/reports/compliance/page.tsx#L279-L280) and identically at [toolbox-talks/reports/compliance/page.tsx:284-285](../web/src/app/(authenticated)/toolbox-talks/reports/compliance/page.tsx#L284-L285) — i.e. the UI itself already conflates "Department" and "Site" terminology. **When formalising a real Department entity, this report's naming will become actively misleading** (a screen literally titled "Compliance by Department" that has nothing to do with the new Department entity) — worth flagging to whoever scopes the fix, though out of scope for this recon to resolve.

### A.3 — Frontend read/write sites

**Editable inputs — inconsistent today, three different patterns for essentially the same field:**

| Form | Component | Input type | File:line |
|---|---|---|---|
| Employee create/edit | `EmployeeForm` | `LookupField categoryName="Department"` (combobox + free-text fallback, see §C.9) | [employee-form.tsx:236-252](../web/src/components/admin/employee-form.tsx#L236-L252) |
| Create user + inline new employee | `UserForm` | `LookupField categoryName="Department"` | [user-form.tsx:423-436](../web/src/components/admin/user-form.tsx#L423-L436) |
| Create employee for an existing user | `CreateEmployeeForUserDialog` | plain `Input` (raw free text, no lookup, no suggestions) | [create-employee-for-user-dialog.tsx:160-169](../web/src/components/admin/create-employee-for-user-dialog.tsx#L160-L169) |

Both `LookupField` usages bind `categoryName="Department"` and allow arbitrary custom text via `allowCustomValue={true}` — see §C.9. `CreateEmployeeForUserDialog` doesn't use `LookupField` at all — a third, plain-text-only path with no suggestion source.

**Display-only sites:**
- Employee detail page: `<DetailItem label="Department" value={employee.department} />` — [admin/employees/[id]/page.tsx:268](<../web/src/app/(authenticated)/admin/employees/[id]/page.tsx#L268>).
- Employee list page (`admin/employees/page.tsx`): **no Department column and no Department filter at all** — confirmed by direct search returning zero matches. (Contrast with Site, which has a read-only "Default Site" column per the location recon §C.8, but still no filter either.)
- My Team page (Supervisor's own operators) — displays department, and **is department's one genuine client-side text-search usage today**: search predicate matches `fullName`, `employeeCode`, `department`, `jobTitle` — [assigned-operators-section.tsx:178-183](../web/src/components/admin/assigned-operators-section.tsx#L178-L183) (assign-dialog), and department is a plain display column at [:110](../web/src/components/admin/assigned-operators-section.tsx#L110) / [:121](../web/src/components/admin/assigned-operators-section.tsx#L121). Same free-text search pattern on the employee-facing "My Team" list — [toolbox-talks/team/page.tsx:107](../web/src/app/(authenticated)/toolbox-talks/team/page.tsx#L107), [:121](../web/src/app/(authenticated)/toolbox-talks/team/page.tsx#L121). This is a client-side `.includes()` substring match over already-fetched data, not a server-side filter parameter — there is no `department` query param anywhere in the API.
- Compliance report frontend — see the "False friend" note in §A.2; it renders `Site` data under a "Department" label, not `Employee.Department`.

### A.4 — Bulk employee import

CSV column `department` is read, length-validated (≤100, matching `CreateEmployeeValidator`), and passed straight through as free text — **no matching against existing values, no lookup, no creation of anything**:
- Column read: [BulkEmployeeImportValidationService.cs:113](../src/Core/QuantumBuild.Core.Infrastructure/Services/BulkEmployeeImportValidationService.cs#L113).
- Length validation: [BulkEmployeeImportValidationService.cs:204](../src/Core/QuantumBuild.Core.Infrastructure/Services/BulkEmployeeImportValidationService.cs#L204) (comment explicitly says "must match `CreateEmployeeValidator`").
- Row result carries `Department` string through to `BulkImportRowResult`, assigned at [BulkEmployeeImportValidationService.cs:246](../src/Core/QuantumBuild.Core.Infrastructure/Services/BulkEmployeeImportValidationService.cs#L246).
- `BulkEmployeeImportJob.ProcessRowAsync` maps it 1:1 into `CreateEmployeeDto.Department` — [BulkEmployeeImportJob.cs:236](../src/Core/QuantumBuild.Core.Infrastructure/Jobs/BulkEmployeeImportJob.cs#L236) — which flows into `EmployeeService.CreateAsync` exactly as any UI-created employee does (§A.2). No site-equivalent is even attempted: `PrimarySiteId: null` is hardcoded for every bulk-imported row — [BulkEmployeeImportJob.cs:237](../src/Core/QuantumBuild.Core.Infrastructure/Jobs/BulkEmployeeImportJob.cs#L237) — confirming bulk import has never had to solve "match free text to an existing lookup entity" for *any* field; formalising Department will be the first time this import path needs that logic (match existing / create / reject / leave null — all open, undecided).
- The upload-panel's CSV template documents the column alongside the others — [bulk-import-upload-panel.tsx:233](../web/src/components/admin/bulk-import-upload-panel.tsx#L233).

---

## B. Existing data shape (migration scope)

### B.5 — Seed / hardcoded values found in code

Only one literal Department value exists anywhere in the codebase: `"Management"`, assigned to the auto-created tenant Admin employee, in two places — [TenantOnboardingService.cs:140](../src/Core/QuantumBuild.Core.Application/Features/Tenants/TenantOnboardingService.cs#L140) (live tenant-creation path) and [DataSeeder.cs:748](../src/Core/QuantumBuild.Core.Infrastructure/Persistence/DataSeeder.cs#L748) (dev seed). No other code path hardcodes a Department value — everything else is user/CSV-entered free text with no fixed vocabulary. Actual per-tenant distinct values in the real database are unknown from code and must be queried (§B.6).

**A parallel, disconnected "Department" concept already exists: the Lookup system.** `LookupCategory` "Department" is seeded (alongside "TrainingCategory" and "JobTitle") with `AllowCustom = true` and **zero** global `LookupValue` rows — [DataSeeder.cs:430-435](../src/Core/QuantumBuild.Core.Infrastructure/Persistence/DataSeeder.cs#L430-L435) (contrast with the `Language` category, which is seeded with `AllowCustom = false` **and** real values — [DataSeeder.cs:479-499](../src/Core/QuantumBuild.Core.Infrastructure/Persistence/DataSeeder.cs#L479-L499)). This means on a fresh tenant, the Department combobox starts empty and any values that exist today are 100% tenant-authored via Settings → Lookups (`LookupCategorySection` — [lookup-category-section.tsx:28-30](../web/src/components/admin/lookup-category-section.tsx#L28-L30) registers the "Departments" display card). Entities: `LookupCategory` ([LookupCategory.cs](../src/Core/QuantumBuild.Core.Domain/Entities/LookupCategory.cs)), `LookupValue` — global defaults ([LookupValue.cs](../src/Core/QuantumBuild.Core.Domain/Entities/LookupValue.cs)), `TenantLookupValue` — tenant overrides/customs, `TenantEntity` ([TenantLookupValue.cs](../src/Core/QuantumBuild.Core.Domain/Entities/TenantLookupValue.cs)).

**Critically, this Lookup-system "Department" is disconnected from `Employee.Department` — it is suggestion data only, not a source of truth or a constraint.** `LookupField` renders a `Combobox` with `allowCustomValue={true}` — [lookup-field.tsx:54-65](../web/src/components/admin/lookup-field.tsx#L54-L65) — so a user can type any string regardless of what lookup values exist, and if no lookup values exist yet it silently falls back to a plain `Input` — [lookup-field.tsx:31-45](../web/src/components/admin/lookup-field.tsx#L31-L45). Nothing validates `Employee.Department` against `TenantLookupValue`/`LookupValue` rows at save time (`CreateEmployeeValidator`/`UpdateEmployeeValidator`, §A.1, only check max length). **This means any per-tenant `TenantLookupValue` rows already saved for the "Department" category are a candidate seed list for the new formal entity, but are not guaranteed to match the actual free-text values on existing `Employee` rows** — the two have never been kept in sync.

### B.6 — Read-only reconciliation queries (NOT executed — for manual DB inspection)

Distinct free-text `Employee.Department` values per tenant, with counts, including an explicit null/empty bucket:

```sql
SELECT
    "TenantId",
    COALESCE(NULLIF(TRIM("Department"), ''), '(none)') AS "DepartmentValue",
    COUNT(*) AS "EmployeeCount"
FROM "Employees"
WHERE "IsDeleted" = false
GROUP BY "TenantId", COALESCE(NULLIF(TRIM("Department"), ''), '(none)')
ORDER BY "TenantId", "EmployeeCount" DESC;
```

Per-tenant summary — how many employees have no department at all, for planning the optional/nullable scope:

```sql
SELECT
    "TenantId",
    COUNT(*) AS "TotalEmployees",
    COUNT(*) FILTER (WHERE "Department" IS NULL OR TRIM("Department") = '') AS "NoDepartmentCount",
    COUNT(DISTINCT NULLIF(TRIM("Department"), '')) AS "DistinctDepartmentValues"
FROM "Employees"
WHERE "IsDeleted" = false
GROUP BY "TenantId"
ORDER BY "TenantId";
```

Cross-check against the parallel Lookup system (§B.5) — per-tenant custom Department lookup values already saved, to compare against the actual free-text values above:

```sql
SELECT
    tlv."TenantId",
    tlv."Name" AS "LookupDepartmentName",
    tlv."IsEnabled"
FROM "TenantLookupValues" tlv
JOIN "LookupCategories" lc ON lc."Id" = tlv."CategoryId"
WHERE lc."Name" = 'Department' AND tlv."IsDeleted" = false
ORDER BY tlv."TenantId", tlv."Name";
```

---

## C. The `Site` pattern to mirror

### C.7 — Entity shape

`Site` is a tenant-scoped lookup entity extending `TenantEntity` — [Site.cs:8](../src/Core/QuantumBuild.Core.Domain/Entities/Site.cs#L8). Relevant shape for the mirror (ignore the construction/geofencing-specific fields — `Latitude`/`Longitude`/`GeofenceRadiusMeters`/Float fields — those are `Site`-specific, not part of the pattern to copy):
- `SiteCode` (`string`, required) + `SiteName` (`string`, required) — [Site.cs:13,18](../src/Core/QuantumBuild.Core.Domain/Entities/Site.cs#L13).
- `IsActive` (`bool`, default `true`) — [Site.cs:68](../src/Core/QuantumBuild.Core.Domain/Entities/Site.cs#L68).
- Optional descriptive fields (`Address`, `City`, `PostalCode`, `Phone`, `Email`, `Notes`) — all `string?`.

### C.8 — Employee FK + `OnDelete` behaviour

- `Employee.PrimarySiteId` — `Guid?`, nullable — [Employee.cs:63](../src/Core/QuantumBuild.Core.Domain/Entities/Employee.cs#L63) — plus `Employee.PrimarySite` navigation — [Employee.cs:68](../src/Core/QuantumBuild.Core.Domain/Entities/Employee.cs#L68).
- Configured in `EmployeeConfiguration`, not `SiteConfiguration`:
```csharp
builder.HasOne(e => e.PrimarySite)
    .WithMany()
    .HasForeignKey(e => e.PrimarySiteId)
    .OnDelete(DeleteBehavior.SetNull);
```
— [EmployeeConfiguration.cs:85-88](../src/Core/QuantumBuild.Core.Infrastructure/Data/Configurations/EmployeeConfiguration.cs#L85-L88). `WithMany()` with no inverse collection — `Site` has no `ICollection<Employee>` back-reference (confirmed absent from [Site.cs](../src/Core/QuantumBuild.Core.Domain/Entities/Site.cs)). `OnDelete(DeleteBehavior.SetNull)` means deleting/deactivating a Site does not block or cascade — the employee's FK is simply nulled, which is exactly the "no site" state described in §A0.
- **One site per employee — no join table, no many-to-many.** A new Department FK would follow this exact single-nullable-FK shape, not a collection.

### C.9 — Configuration / unique-index pattern

`SiteConfiguration` — [SiteConfiguration.cs](../src/Core/QuantumBuild.Core.Infrastructure/Data/Configurations/SiteConfiguration.cs):
```csharp
builder.Property(e => e.SiteCode).HasMaxLength(50).IsRequired();
builder.Property(e => e.SiteName).HasMaxLength(200).IsRequired();
...
builder.HasIndex(e => new { e.TenantId, e.SiteCode })
    .IsUnique()
    .HasDatabaseName("IX_Sites_TenantId_SiteCode");
```
— [SiteConfiguration.cs:13-19](../src/Core/QuantumBuild.Core.Infrastructure/Data/Configurations/SiteConfiguration.cs#L13-L19), [:51-53](../src/Core/QuantumBuild.Core.Infrastructure/Data/Configurations/SiteConfiguration.cs#L51-L53). The pattern: a required short `Code` and a required display `Name`, both `HasMaxLength`, with a composite unique index on `{TenantId, Code}` — codes are unique per tenant, not globally. A new `Department` entity would take the identical `{TenantId, Code}` (or `{TenantId, Name}`, depending on whether a code field is wanted) unique-index shape.

### C.10 — DTO / API exposure pattern

- Full CRUD controller at `/api/sites` gated by `Core.ManageSites` (per CLAUDE.md's endpoint table) — the pattern a new `/api/departments` controller would follow.
- Employee DTOs expose both the FK and a denormalised display name read via `Include(e => e.PrimarySite)` + `.SiteName` projection, e.g. `EmployeeDto(..., Guid? PrimarySiteId, string? PrimarySiteName, ...)` — [EmployeeDto.cs:14-15](../src/Core/QuantumBuild.Core.Application/Features/Employees/DTOs/EmployeeDto.cs#L14-L15), populated via `e.PrimarySite != null ? e.PrimarySite.SiteName : null` in every `EmployeeService` projection (§A.2's six sites). A new `DepartmentId`/`DepartmentName` pair on `EmployeeDto` would follow this identical `Id` + denormalised-`Name` shape, replacing the current bare `string? Department`.
- Employee create/update forms already have a live `Select`-with-"None"-option pattern for exactly this FK shape (`primarySiteId`) — [employee-form.tsx:421-452](../web/src/components/admin/employee-form.tsx#L421-L452), sentinel value `"__none__"` mapped to empty/null on change ([:428](../web/src/components/admin/employee-form.tsx#L428)). A converted Department field would follow this control shape rather than the current `LookupField`/combobox pattern (§A.3), since a real FK needs a fixed, ID-backed option list rather than free-text-with-suggestions.

---

## D. Confirm shared hooks (referencing, not re-deriving, the location recon)

Per [staff-location-recon.md](staff-location-recon.md), the shared hook points for any employee-segmentation attribute are:
- **Assignment/scheduling:** the `AssignToAllEmployees` vs. `EmployeeIds` expansion point in `CreateToolboxTalkScheduleCommandHandler` ([staff-location-recon.md §B.6](staff-location-recon.md#b6--where-a-target-by-location-hook-would-go)) — a `DepartmentIds` target would expand to an `EmployeeId` list the same way a `SiteIds` target would, at the identical point, with the identical caveat that courses have no server-side "all/by-criteria" expansion today (only explicit `EmployeeIds`).
- **Filtering:** `GetSkillsMatrixAsync` (no site/department param today — §C.7 of the location recon) and the sibling `GetComplianceReportAsync`/Overdue/Completions precedent (`Guid? siteId`, §A0 above) are the two hook shapes; a `Guid? departmentId` parameter would be added to `GetSkillsMatrixAsync` following the exact same pattern already implemented for `siteId` in the other three reports, and would need to apply to both the assigned-employees and zero-assignment-employees sub-queries in Skills Matrix for the same "show unassigned/no-data employees too" reason documented in the location recon.

**Confirmed: no divergence found.** Every mechanism inspected for this recon — the `PrimarySiteId`/`Guid?` FK shape, the `siteId`-style optional filter parameter, the `EmployeeDto` denormalised-name projection, the `LookupField`/`Select`-with-none-option UI patterns — is either already department-adjacent (the Lookup system, §B.5) or structurally identical to what Site already does. The **only** genuine difference found is that department's "no value" case has no additional edge case beyond what §A0 already establishes for site: an unfiltered query includes no-department employees; a department-scoped filter excludes them with no explicit "no department" bucket, unless that behaviour is deliberately changed as part of formalisation.

One department-specific detail with no location equivalent: the disconnected Lookup-system "Department" category (§B.5) has no `Site` counterpart — Site has never had a parallel free-text-suggestion mechanism, because `PrimarySiteId` has been a real FK since Site's introduction. Reconciling or retiring that Lookup category (migrate its `TenantLookupValue` rows into the new entity? leave it for some other free-text field? remove it?) is a decision the location recon never had to make and department formalisation will.

---

## Summary for scoping

| Question | Finding |
|---|---|
| How does "no site" behave today? | Unfiltered → included. Filtered to a specific site → excluded (strict equality against a nullable column). No explicit "no site" selector exists anywhere. §A0. |
| Where is `Employee.Department` read/written? | 6 DTOs, 9 `EmployeeService` call sites, 2 other service writers (`UserService`, `TenantOnboardingService`), 1 genuine report/export consumer (Skills Matrix Excel), 1 false-friend report ("Compliance by Department" is actually Site data), 3 different frontend input patterns (2 `LookupField`, 1 raw `Input`), 1 display-only detail page, 2 client-side text-search usages (My Team pages). Full list in §A. |
| Is Department in the employee list search/filter/sort? | No — confirmed absent from `GetEmployeesQueryDto`, the search predicate, and `ApplySorting`. §A.2. |
| Does bulk import handle Department specially? | No — read, length-validated, passed straight through as free text with zero matching/creation logic. First field to need that logic once formalised. §A.4. |
| Does a lookup/reference table already exist for Department? | Yes — `LookupCategory`/`LookupValue`/`TenantLookupValue`, seeded with `AllowCustom = true` and zero default values, powering the `LookupField` combobox — but it is suggestion-only, disconnected from `Employee.Department`, and not guaranteed to match real data. §B.5. |
| What's the exact Site pattern to mirror? | `TenantEntity` with `Code`+`Name`+`IsActive`, `{TenantId, Code}` unique index, single nullable `Guid?` FK on Employee with `OnDelete(SetNull)`, `WithMany()` no back-reference, DTO exposes `Id` + denormalised `Name`, `Select`-with-none-option UI control. §C. |
| Does Department reuse the same assignment/filter hooks as Location? | Yes, confirmed — no divergence in mechanism. The only extra wrinkle is reconciling the pre-existing, disconnected Lookup-system "Department" data, which Site never had to deal with. §D. |
