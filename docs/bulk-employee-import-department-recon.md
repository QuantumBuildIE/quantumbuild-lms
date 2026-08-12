# Bulk Employee Import — Department Recon

Read-only recon. No code changed. All claims cite `file:line`. Facts only — no
design, no fix.

**Context correction:** the task brief describes Department as already
formalised with a structured `DepartmentId` FK, and that is correct — but the
brief's premise that bulk import "still writes the legacy free-text
`Employee.Department` string" undersells what already exists: `DepartmentId`
is already wired end-to-end through `CreateEmployeeDto`, `EmployeeService`,
and the `Department` entity/config/migration (§A). What bulk import lacks is
specifically the **string-to-`DepartmentId` matching step** — everything else
downstream already works. This was already identified as a known gap in the
prior chunk's own reconciliation doc: [staff-department-reconciliation.md
§"Known gap not covered by chunk 1"](staff-department-reconciliation.md#known-gap-not-covered-by-chunk-1-or-this-reconciliation-step).

---

## A. Current import flow and department handling

### A.1 — End-to-end flow

1. **Upload + validate** — `POST /api/employees/bulk-import` —
   [BulkEmployeeImportController.cs:127-195](../src/QuantumBuild.API/Controllers/BulkEmployeeImportController.cs#L127-L195).
   Reads the CSV stream, calls `IBulkEmployeeImportValidationService.ValidateAsync`
   [:152](../src/QuantumBuild.API/Controllers/BulkEmployeeImportController.cs#L152),
   persists the raw CSV to R2, and stores the full validation result as JSON on a new
   `BulkImportSession` row [:172-185](../src/QuantumBuild.API/Controllers/BulkEmployeeImportController.cs#L172-L185).
   No employee is created at this step.
2. **Confirm** — `POST /api/employees/bulk-import/{id}/confirm` —
   [BulkEmployeeImportController.cs:206-249](../src/QuantumBuild.API/Controllers/BulkEmployeeImportController.cs#L206-L249).
   Enqueues `BulkEmployeeImportJob` via the concrete class (Hangfire attribute
   visibility, see CLAUDE.md Note 21) [:242-243](../src/QuantumBuild.API/Controllers/BulkEmployeeImportController.cs#L242-L243).
3. **Validation service** — `BulkEmployeeImportValidationService.ValidateAsync` —
   [BulkEmployeeImportValidationService.cs:31-263](../src/Core/QuantumBuild.Core.Infrastructure/Services/BulkEmployeeImportValidationService.cs#L31-L263).
   Parses the CSV with CsvHelper, case-insensitive header matching
   [:58](../src/Core/QuantumBuild.Core.Infrastructure/Services/BulkEmployeeImportValidationService.cs#L58),
   validates each row, and returns a `BulkImportValidationResult` with one
   `BulkImportRowResult` per row.
4. **Job / creation** — `BulkEmployeeImportJob.ExecuteAsync` —
   [BulkEmployeeImportJob.cs:64-215](../src/Core/QuantumBuild.Core.Infrastructure/Jobs/BulkEmployeeImportJob.cs#L64-L215).
   Deserialises the stored `ValidationResultJson`, then for each non-Failed row calls
   `ProcessRowAsync` [:122-128](../src/Core/QuantumBuild.Core.Infrastructure/Jobs/BulkEmployeeImportJob.cs#L122-L128),
   which builds a `CreateEmployeeDto` and calls `IEmployeeService.CreateAsync`
   [:228-256](../src/Core/QuantumBuild.Core.Infrastructure/Jobs/BulkEmployeeImportJob.cs#L228-L256).
   Each row runs in its own DI scope (`IServiceScopeFactory.CreateAsyncScope()`) per
   CLAUDE.md Note 23 [:125](../src/Core/QuantumBuild.Core.Infrastructure/Jobs/BulkEmployeeImportJob.cs#L125).

**CSV format** — required columns: `FirstName`, `LastName`, `Email`,
`CreateUserAccount`
[:78](../src/Core/QuantumBuild.Core.Infrastructure/Services/BulkEmployeeImportValidationService.cs#L78).
Optional columns include `department` (lowercase, header-matching is
case-insensitive)
[:113](../src/Core/QuantumBuild.Core.Infrastructure/Services/BulkEmployeeImportValidationService.cs#L113).
Full header order documented in the template constant:
`FirstName,LastName,Email,CreateUserAccount,Phone,Mobile,JobTitle,Department,StartDate,EndDate,Notes,PreferredLanguage,UserRole`
[BulkEmployeeImportController.cs:84](../src/QuantumBuild.API/Controllers/BulkEmployeeImportController.cs#L84).
There is no `DepartmentId` (or department-code) column in the CSV format
today — only the free-text `Department` column.

### A.2 — Exactly how department is populated today

- **Read:** `csv.GetField<string?>("department")`, trimmed via
  `TrimOptions.Trim` on the CSV reader, null if blank —
  [BulkEmployeeImportValidationService.cs:113](../src/Core/QuantumBuild.Core.Infrastructure/Services/BulkEmployeeImportValidationService.cs#L113).
- **Validated:** length only, `≤ 100` chars, comment explicitly says "must
  match `CreateEmployeeValidator`" —
  [BulkEmployeeImportValidationService.cs:204](../src/Core/QuantumBuild.Core.Infrastructure/Services/BulkEmployeeImportValidationService.cs#L204).
  No matching, no lookup, no existence check against `Department` rows.
- **Carried through** the row result unchanged —
  [BulkEmployeeImportValidationService.cs:246](../src/Core/QuantumBuild.Core.Infrastructure/Services/BulkEmployeeImportValidationService.cs#L246)
  → `BulkImportRowResult.Department` field
  [BulkImportValidationResult.cs:43](../src/Core/QuantumBuild.Core.Application/Features/BulkImport/BulkImportValidationResult.cs#L43).
- **Written:** `BulkEmployeeImportJob.ProcessRowAsync` maps the string 1:1 into
  `CreateEmployeeDto.Department` **and hardcodes `DepartmentId: null`** —
  [BulkEmployeeImportJob.cs:236-237](../src/Core/QuantumBuild.Core.Infrastructure/Jobs/BulkEmployeeImportJob.cs#L236-L237):
  ```csharp
  Department: row.Department,
  DepartmentId: null,
  ```
  This is the exact point that needs to change. Confirmed: the free-text
  string is written (`Employee.Department` via
  `CreateEmployeeDto.Department` →
  [EmployeeService.cs:324](../src/Core/QuantumBuild.Core.Application/Features/Employees/EmployeeService.cs#L324)),
  while the structured FK is always left null
  (`CreateEmployeeDto.DepartmentId` →
  [EmployeeService.cs:325](../src/Core/QuantumBuild.Core.Application/Features/Employees/EmployeeService.cs#L325)).
- **`CreateEmployeeDto` already carries both fields** — `Department` (marked
  legacy, "survives only for callers not yet migrated (e.g. bulk import)") and
  `DepartmentId` —
  [CreateEmployeeDto.cs:15-21](../src/Core/QuantumBuild.Core.Application/Features/Employees/DTOs/CreateEmployeeDto.cs#L15-L21).
  So the DTO plumbing to pass a resolved `DepartmentId` already exists; bulk
  import simply never populates it.
- **Downstream in `EmployeeService.CreateAsync`:** if `DepartmentId` is
  supplied, it is validated to exist and belong to the target tenant before
  the employee is created —
  [EmployeeService.cs:269-280](../src/Core/QuantumBuild.Core.Application/Features/Employees/EmployeeService.cs#L269-L280):
  ```csharp
  if (dto.DepartmentId.HasValue)
  {
      var departmentExists = await _context.Departments
          .IgnoreQueryFilters()
          .AnyAsync(d => d.TenantId == tenantId && !d.IsDeleted && d.Id == dto.DepartmentId.Value);
      if (!departmentExists)
          return Result.Fail<EmployeeDto>($"Department with ID {dto.DepartmentId} not found");
  }
  ```
  This means: **if the bulk import fix resolves a `DepartmentId` and passes
  it, `EmployeeService.CreateAsync` already validates and assigns it with no
  further change needed there.** The only work is producing that
  `DepartmentId` from the CSV string before the DTO is built (i.e. inside
  `BulkEmployeeImportValidationService` or `BulkEmployeeImportJob`).
- **Frontend:** the upload panel's documentation text lists `Department`
  under "Optional columns" alongside `Phone, Mobile, JobTitle, Notes` with no
  special handling —
  [bulk-import-upload-panel.tsx:230-236](../web/src/components/admin/bulk-import-upload-panel.tsx#L230-L236).
  The downloadable template embeds sample department values `Operations` /
  `Warehouse` as plain text —
  [BulkEmployeeImportController.cs:84-87](../src/QuantumBuild.API/Controllers/BulkEmployeeImportController.cs#L84-L87).

### A.3 — Tenant scoping of the import (confirmed)

- The target tenant is resolved once, at upload time, from
  `ICurrentUserService.TenantId` —
  [BulkEmployeeImportController.cs:129](../src/QuantumBuild.API/Controllers/BulkEmployeeImportController.cs#L129).
  If it is `Guid.Empty` (a SuperUser with no `X-Tenant-Id` header), the upload
  is rejected outright —
  [BulkEmployeeImportController.cs:130-132](../src/QuantumBuild.API/Controllers/BulkEmployeeImportController.cs#L130-L132).
  Per CLAUDE.md, non-SuperUsers always operate on their own tenant
  (`ICurrentUserService` ignores the header for them).
- The resolved `tenantId` is stamped onto the `BulkImportSession` row at
  creation —
  [BulkEmployeeImportController.cs:175](../src/QuantumBuild.API/Controllers/BulkEmployeeImportController.cs#L175).
- The Hangfire job re-reads `session.TenantId` (not `ICurrentUserService`,
  which returns empty in a job context per CLAUDE.md Note 22) —
  [BulkEmployeeImportJob.cs:88](../src/Core/QuantumBuild.Core.Infrastructure/Jobs/BulkEmployeeImportJob.cs#L88)
  and passes it as `tenantIdOverride` into `EmployeeService.CreateAsync`
  [BulkEmployeeImportJob.cs:256](../src/Core/QuantumBuild.Core.Infrastructure/Jobs/BulkEmployeeImportJob.cs#L256) →
  [EmployeeService.cs:254](../src/Core/QuantumBuild.Core.Application/Features/Employees/EmployeeService.cs#L254)
  (`var tenantId = tenantIdOverride ?? _currentUserService.TenantId;`).
- **Conclusion: yes, the import is already fully per-tenant scoped**, and that
  same `tenantId` (available both in `BulkEmployeeImportValidationService` via
  the ambient query filter, and explicitly in the job via `session.TenantId`)
  is exactly what a Department-matching query would need to scope by tenant.
  `BulkEmployeeImportValidationService` already has `ICoreDbContext` injected
  [:13,26-29](../src/Core/QuantumBuild.Core.Infrastructure/Services/BulkEmployeeImportValidationService.cs#L13),
  which exposes `Departments` (see §B.4) and is subject to the same
  ambient tenant query filter already relied on for the `Employees` and
  `Users` uniqueness pre-loads at the top of `ValidateAsync`
  [:37-47](../src/Core/QuantumBuild.Core.Infrastructure/Services/BulkEmployeeImportValidationService.cs#L37-L47).

---

## B. What a structured mapping would need

### B.4 — Existing lookup/match mechanism to build on

- **`Department` entity** — `TenantEntity`, `Name` (required, max 100),
  `Code` (optional, max 20), `IsActive` —
  [Department.cs:10-27](../src/Core/QuantumBuild.Core.Domain/Entities/Department.cs#L10-L27).
- **Uniqueness constraint:** composite unique index on `{TenantId, Name}` —
  [DepartmentConfiguration.cs:20-22](../src/Core/QuantumBuild.Core.Infrastructure/Data/Configurations/DepartmentConfiguration.cs#L20-L22).
  This is a case-**sensitive** database index (no explicit collation
  override) — two rows named `Operations` and `operations` are not blocked by
  this index.
- **`DepartmentService.CreateAsync` duplicate-name check is also
  case-sensitive:** `_context.Departments.AnyAsync(d => d.Name == dto.Name)` —
  [DepartmentService.cs:108-109](../src/Core/QuantumBuild.Core.Application/Features/Departments/DepartmentService.cs#L108-L109)
  (same in `UpdateAsync`
  [:146-147](../src/Core/QuantumBuild.Core.Application/Features/Departments/DepartmentService.cs#L146-L147)).
  No explicit `TenantId` predicate is written in either check — it relies on
  the ambient EF query filter (confirmed next bullet), so this is
  tenant-scoped but exact-case-only.
- **Tenant query filter is already correctly wired at the `ApplicationDbContext`
  level** (not per-entity-config — the CLAUDE.md Note 14 pattern):
  ```csharp
  modelBuilder.Entity<Department>().HasQueryFilter(e => !e.IsDeleted && (BypassTenantFilter || e.TenantId == TenantId));
  ```
  — [ApplicationDbContext.cs:338](../src/Core/QuantumBuild.Core.Infrastructure/Data/ApplicationDbContext.cs#L338).
  `Departments` is exposed as `DbSet<Department> Departments => Set<Department>()` —
  [ApplicationDbContext.cs:51](../src/Core/QuantumBuild.Core.Infrastructure/Data/ApplicationDbContext.cs#L51) —
  and on `ICoreDbContext` (confirmed present via the earlier grep of that
  interface file), so it is already reachable everywhere `Employees`/`Users`
  are queried in the import path today.
- **No existing case-insensitive name-matching precedent anywhere comparable.**
  `EmployeeService.CreateAsync`'s `PrimarySiteId` validation is the closest
  analogous "does this FK exist for this tenant" check, but it matches by
  **ID**, not by name —
  [EmployeeService.cs:257-267](../src/Core/QuantumBuild.Core.Application/Features/Employees/EmployeeService.cs#L257-L267) —
  because the frontend Site picker always sends an ID, never free text. Site
  has never needed string-to-entity matching because, per the staff-location
  recon, `PrimarySiteId` has been a real FK since Site's introduction — there
  was never a free-text Site field to import from. **Department (via bulk
  import) will be the first place in the codebase that needs to turn an
  arbitrary user-typed/CSV string into a tenant-scoped entity match.** No
  reusable helper (e.g. a shared "find-or-suggest by name" service) exists to
  call into; one of the closest general patterns in the codebase for
  case-insensitive substring matching is `DepartmentService.GetPaginatedAsync`'s
  search predicate, `d.Name.ToLower().Contains(searchLower)`
  [DepartmentService.cs:44-45](../src/Core/QuantumBuild.Core.Application/Features/Departments/DepartmentService.cs#L44-L45)
  — a `.ToLower()` comparison, not an index-backed one, and it's a *search*
  filter, not an *exact-match* lookup.
- **Employee → Department relationship:** `Employee.DepartmentId` (`Guid?`) +
  `Employee.AssignedDepartment` navigation, `OnDelete(DeleteBehavior.SetNull)`,
  `WithMany()` with no inverse collection —
  [EmployeeConfiguration.cs:90-93](../src/Core/QuantumBuild.Core.Infrastructure/Data/Configurations/EmployeeConfiguration.cs#L90-L93)
  — structurally identical to the `PrimarySite` pattern.
- **Departments CRUD API** already exists: `GET /api/departments/all` (no
  policy beyond `[Authorize]`), `GET /api/departments` (paginated/search, same),
  `GET /api/departments/{id}`, `POST /api/departments` (policy
  `Core.ManageDepartments`), `PUT /api/departments/{id}` (same policy) —
  [DepartmentsController.cs:8-110](../src/QuantumBuild.API/Controllers/DepartmentsController.cs#L8-L110).
  `Core.ManageDepartments` is a registered permission —
  [Permissions.cs:27](../src/Core/QuantumBuild.Core.Infrastructure/Identity/Permissions.cs#L27).
  There is no delete endpoint by design — deactivation only, via `IsActive`
  on update (see doc comment)
  [DepartmentsController.cs:91-96](../src/QuantumBuild.API/Controllers/DepartmentsController.cs#L91-L96).
  Any "auto-create a Department during import" path could call
  `IDepartmentService.CreateAsync` directly (already tenant-scoped via the
  ambient filter, no need to go through the controller/HTTP layer) —
  [DepartmentService.cs:104-133](../src/Core/QuantumBuild.Core.Application/Features/Departments/DepartmentService.cs#L104-L133).
  Note `CreateAsync` does **not** accept an explicit tenant ID parameter — it
  relies on `_context.SaveChangesAsync()`'s auto-stamp of `TenantId` from
  `ICurrentUserService` (per CLAUDE.md Note 22), which is `Guid.Empty` inside a
  Hangfire job with no HTTP context — **calling `DepartmentService.CreateAsync`
  as-is from `BulkEmployeeImportJob` would silently create a
  `TenantId = Guid.Empty` department**, unless the department entity is
  constructed and tenant-stamped explicitly (bypassing `DepartmentService`),
  mirroring how `EmployeeService.CreateAsync` explicitly accepts and uses
  `tenantIdOverride` (§A.3). This is a fact about current code shape, not a
  chosen fix.

### B.5 — The three outcomes and what each would touch

The three cases the brief describes, evaluated against what exists today:

1. **Value matches an existing Department (by name, tenant-scoped).** No
   existing exact-match-by-name query exists to call — would need a new query
   of the shape `_context.Departments.FirstOrDefaultAsync(d => d.TenantId ==
   tenantId && d.Name == value)` (or a case-insensitive variant — see below).
   Once matched, setting `CreateEmployeeDto.DepartmentId` to the found ID and
   leaving `CreateEmployeeDto.Department` as the raw string (or clearing it —
   undecided) is a one-line change at
   [BulkEmployeeImportJob.cs:237](../src/Core/QuantumBuild.Core.Infrastructure/Jobs/BulkEmployeeImportJob.cs#L237),
   and `EmployeeService.CreateAsync` already validates/assigns it (§A.2) —
   **no downstream change needed** beyond producing the ID.
   - **Case sensitivity is an open question with no precedent to inherit.**
     The DB unique index (§B.4) and `DepartmentService`'s own duplicate check
     are both case-sensitive exact-match. A case-insensitive CSV match (e.g.
     CSV says `operations`, DB has `Operations`) would need a new
     `.ToLower()`/`ILIKE`-style comparison that doesn't exist anywhere in the
     Department code today — the closest precedent,
     `DepartmentService.GetPaginatedAsync`'s search
     [:44-45](../src/Core/QuantumBuild.Core.Application/Features/Departments/DepartmentService.cs#L44-L45),
     is a substring `Contains` search, not an exact match, so it isn't a
     drop-in.
2. **Value is empty/blank.** Already the default/no-op case: `department` is
   read as `null` when blank
   ([:113](../src/Core/QuantumBuild.Core.Infrastructure/Services/BulkEmployeeImportValidationService.cs#L113),
   via `NullIfEmpty`
   [:281-282](../src/Core/QuantumBuild.Core.Infrastructure/Services/BulkEmployeeImportValidationService.cs#L281-L282)),
   and `CreateEmployeeDto.DepartmentId: null` is already what's hardcoded
   today
   [BulkEmployeeImportJob.cs:237](../src/Core/QuantumBuild.Core.Infrastructure/Jobs/BulkEmployeeImportJob.cs#L237).
   **This case needs no new code** — it's already the exact behaviour
   Option B calls for (nullable FK, valid to leave unset), matching the
   `PrimarySiteId`-style "no value" precedent documented in
   [staff-department-recon.md §A0](staff-department-recon.md#a0--how-no-site-is-handled-today--the-reference-pattern-for-no-department).
3. **Value does not match any existing Department.** No existing mechanism
   for this at all — this is the genuinely new case requiring a decision.
   Three named options in the brief, evaluated against current code:
   - **Create a new Department.** Mechanically possible (§B.4's
     `DepartmentService.CreateAsync`, or a direct `_context.Departments.Add`
     with explicit `TenantId` stamping to avoid the Hangfire-context gap noted
     above) but has product-level consequences the recon is explicitly not
     scoping: silently proliferating departments from typos, e.g. `Ops` and
     `Operations` and `operations` as three CSV rows would (absent
     case-insensitive matching) create three separate Department rows on a
     tenant that only wanted one — directly undoing the "define a canonical
     list" reconciliation workflow described in
     [staff-department-reconciliation.md](staff-department-reconciliation.md).
   - **Reject the row.** The validation service already has a `Fail()`
     closure used identically for other unresolvable values (e.g.
     `CreateUserAccount` must be `Yes`/`No`) —
     [BulkEmployeeImportValidationService.cs:102](../src/Core/QuantumBuild.Core.Infrastructure/Services/BulkEmployeeImportValidationService.cs#L102),
     example use at
     [:143](../src/Core/QuantumBuild.Core.Infrastructure/Services/BulkEmployeeImportValidationService.cs#L143).
     Reusing this mechanism for an unmatched department would produce a
     `BulkImportRowStatus.Failed` row, surfaced identically to any other
     validation failure (§B.6) — this is a drop-in fit, no new plumbing.
   - **Import with null department and report it (warning).** The service
     already has a parallel `Warn()` closure for exactly this shape of
     "not a hard failure, but flag it" outcome —
     [BulkEmployeeImportValidationService.cs:103](../src/Core/QuantumBuild.Core.Infrastructure/Services/BulkEmployeeImportValidationService.cs#L103),
     already used for an analogous case: `UserRole` supplied but ignored
     because `CreateUserAccount` is `No`
     [:222-223](../src/Core/QuantumBuild.Core.Infrastructure/Services/BulkEmployeeImportValidationService.cs#L222-L223)
     (`Warn("UserRole is set but CreateUserAccount is 'No' — the role will not be applied.")`).
     An unmatched-department warning would follow this exact precedent and is
     also a drop-in fit.

   The service's existing `Fail`/`Warn` closures (§B.6) mean **both** the
   reject and the warn-and-continue options reuse an established, in-file
   mechanism with a direct precedent already in this same file. Create-new
   has no direct precedent and the most product risk (unintended dept
   proliferation). All three are mechanically available; none is chosen here.

### B.6 — Existing validation / partial-success model (confirmed)

Bulk employee import is **already row-level partial-success**, not
all-or-nothing:

- Three-state row status: `Valid = 1, Warning = 2, Failed = 3` —
  [BulkImportValidationResult.cs:3-8](../src/Core/QuantumBuild.Core.Application/Features/BulkImport/BulkImportValidationResult.cs#L3-L8).
- Each row accumulates zero or more `Messages` regardless of outcome —
  [BulkImportValidationResult.cs:33](../src/Core/QuantumBuild.Core.Application/Features/BulkImport/BulkImportValidationResult.cs#L33).
- The job only skips rows whose validation `Status == Failed`; `Warning` rows
  are still processed and created —
  [BulkEmployeeImportJob.cs:110-112](../src/Core/QuantumBuild.Core.Infrastructure/Jobs/BulkEmployeeImportJob.cs#L110-L112):
  ```csharp
  var rowsToProcess = validationResult.Rows
      .Where(r => r.Status != BulkImportRowStatus.Failed)
      .ToList();
  ```
- The frontend already renders a distinct `Warning` badge separate from
  `Failed` —
  [bulk-import-validation-panel.tsx:33](../web/src/components/admin/bulk-import-validation-panel.tsx#L33)
  (`Warning: <Badge variant="secondary">Warning</Badge>`), with a dedicated
  warnings count summary label
  [bulk-import-validation-panel.tsx:101](../web/src/components/admin/bulk-import-validation-panel.tsx#L101).
- A second, independent per-row status enum exists for the **processing**
  (post-creation) phase — `BulkImportRowOutcomeStatus`: `Created = 1, Failed =
  2, AlreadyExisted = 3` —
  [BulkImportProcessingResult.cs:3-13](../src/Core/QuantumBuild.Core.Application/Features/BulkImport/BulkImportProcessingResult.cs#L3-L13)
  — this is unrelated to department matching (it tracks employee-creation
  outcomes, not validation), noted only to avoid confusing the two "Failed"
  concepts.
- Failed rows (from either phase) are downloadable as a correctable CSV via
  `GET .../failed-rows`, which re-embeds the original `Department` column
  value —
  [BulkEmployeeImportController.cs:403](../src/QuantumBuild.API/Controllers/BulkEmployeeImportController.cs#L403).

**Conclusion for point 6:** the import already has exactly the mechanism the
brief asks whether it has — per-row `Warning` (partial success, still
processed) distinct from per-row `Failed` (skipped, correctable via
re-download) — with a live, in-file precedent (`UserRole`/`CreateUserAccount`
interaction) for using `Warn()` on a "value present but not fully usable"
condition, which is structurally identical to "department value present but
unmatched."

---

## C. Interaction with reconciliation and existing data

### C.7 — Onboarding vs. ongoing usage

Nothing in the code distinguishes an "onboarding" bulk import from an
"ongoing" one — there is no session flag, mode, or separate endpoint for
either use case; every session goes through the same
upload → validate → confirm → process pipeline (§A.1). This recon cannot
determine from code alone how often the tool is used for new-tenant
onboarding (where a tenant's Department list plausibly doesn't exist yet, so
unknown departments would be common) versus ongoing headcount additions
(where the Department list is presumably already established, so unknown
departments would be rarer/typos). This is a usage-pattern question, not a
code fact — recommend confirming with whoever runs onboarding for tenants
today rather than inferring it from the codebase.

One relevant code fact bearing on likelihood: the `Department` unique-index
and `DepartmentsController` (§B.4) are net-new in the current session's work
(migration timestamped `20260811082938_AddDepartmentEntity`, i.e. today), and
per
[staff-department-reconciliation.md](staff-department-reconciliation.md),
**zero** `Department` rows have been created for any existing tenant yet —
reconciliation (defining the canonical per-tenant list) is an unstarted
manual step. This means, as of now, **every** bulk import against an existing
tenant would hit the "no match" case for every department value, until a
human runs the reconciliation step. This sharpens the urgency of the
unknown-department decision beyond a purely per-tenant onboarding question —
it currently affects all tenants, all the time, until reconciliation is done
tenant-by-tenant.

### C.8 — Free-text field still written elsewhere (confirm no stray write is missed)

`Employee.Department` (free text) is written from bulk import at exactly one
place —
[BulkEmployeeImportJob.cs:236](../src/Core/QuantumBuild.Core.Infrastructure/Jobs/BulkEmployeeImportJob.cs#L236)
— which flows into `EmployeeService.CreateAsync`'s single write of
`Department = dto.Department` —
[EmployeeService.cs:324](../src/Core/QuantumBuild.Core.Application/Features/Employees/EmployeeService.cs#L324).
There is no second write path for bulk-imported employees (no post-creation
update step, no separate department-sync job). If a fix changes the import to
populate `DepartmentId`, the existing free-text `Department` field would, per
`CreateEmployeeDto`'s own doc comment
([:15-19](../src/Core/QuantumBuild.Core.Application/Features/Employees/DTOs/CreateEmployeeDto.cs#L15-L19)),
still need an explicit decision on whether to keep passing the raw CSV string
into `Department` alongside the resolved `DepartmentId` (mirroring what
manual employee-edit already does per
[UpdateEmployeeDto.cs:11-17](../src/Core/QuantumBuild.Core.Application/Features/Employees/DTOs/UpdateEmployeeDto.cs#L11-L17)'s
"never overwritten" comment) or to null it out once a `DepartmentId` is
resolved — not a stray write to hunt down, just a one-line decision at the
single existing write site.

---

## Summary for scoping

| Question | Finding |
|---|---|
| Where does bulk import read/write department today? | Read: CSV `department` column, length-validated only — [BulkEmployeeImportValidationService.cs:113,204](../src/Core/QuantumBuild.Core.Infrastructure/Services/BulkEmployeeImportValidationService.cs#L113). Write: `CreateEmployeeDto.Department = row.Department`, `DepartmentId` hardcoded `null` — [BulkEmployeeImportJob.cs:236-237](../src/Core/QuantumBuild.Core.Infrastructure/Jobs/BulkEmployeeImportJob.cs#L236-L237). §A.2. |
| Is `DepartmentId` already plumbed elsewhere? | Yes — `CreateEmployeeDto.DepartmentId` exists, and `EmployeeService.CreateAsync` already validates/assigns it if supplied ([EmployeeService.cs:269-280,325](../src/Core/QuantumBuild.Core.Application/Features/Employees/EmployeeService.cs#L269-L280)). The only missing piece is producing the ID from the CSV string. §A.2. |
| Is the import tenant-scoped? | Yes, confirmed at upload (`ICurrentUserService.TenantId` or explicit `X-Tenant-Id` for SuperUser), persisted on the session, and re-read explicitly in the job (`session.TenantId` → `tenantIdOverride`) because `ICurrentUserService` returns empty in a Hangfire context. §A.3. |
| Does a name-matching mechanism already exist to build on? | No exact-match-by-name lookup exists anywhere in the codebase for Department or any comparable entity — Site has never needed one (always FK-driven, no free-text Site field ever existed). The nearest relative, `DepartmentService.GetPaginatedAsync`'s search, is a case-insensitive `Contains` substring filter, not an exact match. Case-sensitivity of any new match query is an open decision with no precedent either way — the DB unique index and `DepartmentService`'s own duplicate check are both case-sensitive. §B.4. |
| What would each of the three outcomes need? | Match → one-line ID assignment, no downstream change needed (already validated by `EmployeeService`). Empty → already fully handled, zero new code. Unknown → genuinely new; reject and warn-and-continue both reuse the file's existing `Fail()`/`Warn()` closures with direct in-file precedent (`CreateUserAccount`, `UserRole` warnings); create-new is mechanically possible but has no matching precedent and the most product risk (typo-driven department proliferation, undermines the reconciliation goal). §B.5. |
| Does the import already support partial success / per-row reporting? | Yes — three-state row status (`Valid`/`Warning`/`Failed`), `Warning` rows still get created, dedicated frontend `Warning` badge, failed-rows are downloadable/correctable. This is the exact mechanism to reuse for reporting an unmatched department, whichever policy is chosen. §B.6. |
| Onboarding vs. ongoing — is unknown-department common? | Not determinable from code; no session mode distinguishes the two. However, since zero `Department` rows exist for any tenant yet (reconciliation is an unstarted manual step per the prior chunk's own recon doc), **every** department value in **every** current import will be "unknown" until a tenant's reconciliation is done — this affects all tenants today, not just new ones. §C.7. |
| Any stray free-text write the fix must not miss? | No — exactly one write site, already identified ([BulkEmployeeImportJob.cs:236](../src/Core/QuantumBuild.Core.Infrastructure/Jobs/BulkEmployeeImportJob.cs#L236)). Whether to keep, blank, or conditionally set the legacy `Department` string alongside a resolved `DepartmentId` is a one-line decision at that same site, not a search-and-fix problem. §C.8. |
