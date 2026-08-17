# Old Department Lookup — Retirement Recon

Read-only recon. No code changed. All claims cite `file:line`. Goal: map exactly
what is Department-category-specific in the generic `LookupCategory` /
`LookupValue` / `TenantLookupValue` system (removable) vs. what is shared
infrastructure serving other categories (must stay), so the old Department
lookup slice can be retired without touching the shared system or any other
category.

---

## A. The generic system and its other categories (must NOT be touched)

### A.1 — Entities

- `LookupCategory : BaseEntity` — `Name`, `Module`, `AllowCustom`, `IsActive`, plus
  `Values` and `TenantValues` collection navs — [LookupCategory.cs:5-14](../src/Core/QuantumBuild.Core.Domain/Entities/LookupCategory.cs#L5-L14).
  **Not tenant-scoped** (`BaseEntity`, not `TenantEntity`) — a `LookupCategory` row
  is global, shared by every tenant.
- `LookupValue : BaseEntity` — global default value under a category (`CategoryId`,
  `Code`, `Name`, `Metadata`, `SortOrder`, `IsActive`) — [LookupValue.cs:5-14](../src/Core/QuantumBuild.Core.Domain/Entities/LookupValue.cs#L5-L14).
- `TenantLookupValue : TenantEntity` — tenant-scoped override-or-custom value
  (`CategoryId`, optional `LookupValueId` back-reference, `Code`, `Name`,
  `Metadata`, `SortOrder`, `IsEnabled`) — [TenantLookupValue.cs:5-16](../src/Core/QuantumBuild.Core.Domain/Entities/TenantLookupValue.cs#L5-L16).
  `LookupValueId == null` ⇒ tenant custom value; `LookupValueId` set ⇒ tenant
  override/disable of a global value.

### A.2 — EF configuration (generic, no category-specific branching)

- [LookupCategoryConfiguration.cs](../src/Core/QuantumBuild.Core.Infrastructure/Data/Configurations/LookupCategoryConfiguration.cs) — unique index on `Name` [:23-25](../src/Core/QuantumBuild.Core.Infrastructure/Data/Configurations/LookupCategoryConfiguration.cs#L23-L25); cascade delete from category to both `Values` and `TenantValues` [:27-35](../src/Core/QuantumBuild.Core.Infrastructure/Data/Configurations/LookupCategoryConfiguration.cs#L27-L35).
- [LookupValueConfiguration.cs](../src/Core/QuantumBuild.Core.Infrastructure/Data/Configurations/LookupValueConfiguration.cs) — unique index on `{CategoryId, Code}` [:26-28](../src/Core/QuantumBuild.Core.Infrastructure/Data/Configurations/LookupValueConfiguration.cs#L26-L28).
- [TenantLookupValueConfiguration.cs](../src/Core/QuantumBuild.Core.Infrastructure/Data/Configurations/TenantLookupValueConfiguration.cs) — unique index on `{TenantId, CategoryId, Code}` [:26-28](../src/Core/QuantumBuild.Core.Infrastructure/Data/Configurations/TenantLookupValueConfiguration.cs#L26-L28); `SetNull` FK to `LookupValue` on delete [:30-33](../src/Core/QuantumBuild.Core.Infrastructure/Data/Configurations/TenantLookupValueConfiguration.cs#L30-L33).
- DbContext registration and query filters, all three entities, none category-aware:
  [ApplicationDbContext.cs:65-67](../src/Core/QuantumBuild.Core.Infrastructure/Data/ApplicationDbContext.cs#L65-L67) (DbSets),
  [:263-265](../src/Core/QuantumBuild.Core.Infrastructure/Data/ApplicationDbContext.cs#L263-L265) (config registration),
  [:380-382](../src/Core/QuantumBuild.Core.Infrastructure/Data/ApplicationDbContext.cs#L380-L382) (query filters — `TenantLookupValue` is the only one of the three with a tenant predicate; `LookupCategory`/`LookupValue` filter only on `!IsDeleted`, confirming they are global, not per-tenant).
- `ICoreDbContext` exposes the three `DbSet`s — [ICoreDbContext.cs:30-32](../src/Core/QuantumBuild.Core.Application/Interfaces/ICoreDbContext.cs#L30-L32).

### A.3 — Service and controller (100% generic — `categoryName` is a runtime parameter everywhere, never hardcoded per-category)

- `ILookupService` — [ILookupService.cs:6-14](../src/Core/QuantumBuild.Core.Application/Features/Lookups/ILookupService.cs#L6-L14): `GetEffectiveValuesAsync`, `GetCategoriesAsync`, `CreateTenantValueAsync`, `UpdateTenantValueAsync`, `DeleteTenantValueAsync`, `ToggleGlobalValueAsync`.
- `LookupService` — [LookupService.cs:1-345](../src/Core/QuantumBuild.Core.Application/Features/Lookups/LookupService.cs#L1-L345). Every method takes `categoryName` (or `category.Id` derived from it) as a parameter and queries `_context.LookupCategories`/`LookupValues`/`TenantLookupValues` generically — no `if (categoryName == "Department")` branch anywhere in the file.
- `LookupsController` (`/api/lookups`) — [LookupsController.cs:1-113](../src/QuantumBuild.API/Controllers/LookupsController.cs#L1-L113): `GET {categoryName}/values`, `GET categories`, `POST {categoryName}/values`, `PUT values/{id}`, `DELETE values/{id}`, `PUT {categoryName}/values/{lookupValueId}/toggle`. All mutation actions are gated by policy `Learnings.Admin` at the **action** level ([:42](../src/QuantumBuild.API/Controllers/LookupsController.cs#L42), [:57](../src/QuantumBuild.API/Controllers/LookupsController.cs#L57), [:72](../src/QuantumBuild.API/Controllers/LookupsController.cs#L72), [:87](../src/QuantumBuild.API/Controllers/LookupsController.cs#L87), [:102](../src/QuantumBuild.API/Controllers/LookupsController.cs#L102)) — this gates the **entire generic lookup-mutation surface**, not Department specifically (see B.4).
- DTOs, all category-agnostic: `LookupCategoryDto`, `LookupValueDto`, `CreateTenantLookupValueDto`, `UpdateTenantLookupValueDto`, `ToggleGlobalValueDto` — [LookupDtos.cs:1-41](../src/Core/QuantumBuild.Core.Application/Features/Lookups/DTOs/LookupDtos.cs#L1-L41).

### A.4 — All lookup categories currently in use (4 total, seeded/referenced)

| Category | Seeded where | `AllowCustom` | Global `LookupValue` rows? | Consumers |
|---|---|---|---|---|
| `TrainingCategory` | `SeedLookupCategoriesAsync` (category) [DataSeeder.cs:433](../src/Core/QuantumBuild.Core.Infrastructure/Persistence/DataSeeder.cs#L433) + `SeedTrainingCategoriesAsync` (tenant values, `QUANTUMBUILD` tenant only) [DataSeeder.cs:594-](../src/Core/QuantumBuild.Core.Infrastructure/Persistence/DataSeeder.cs#L594) | `true` | No (tenant-custom values only) | Learning/talk category filters — e.g. `CategoryPanel.tsx`, `InputConfigStep.tsx` (create/new-wizard steps), reports filters |
| **`Department`** | `SeedLookupCategoriesAsync` (category only) [DataSeeder.cs:434](../src/Core/QuantumBuild.Core.Infrastructure/Persistence/DataSeeder.cs#L434) | `true` | **No — never seeded** | **None** (see §B/§C — superseded) |
| `JobTitle` | `SeedLookupCategoriesAsync` (category only) [DataSeeder.cs:435](../src/Core/QuantumBuild.Core.Infrastructure/Persistence/DataSeeder.cs#L435) | `true` | No (tenant-custom values only) | `EmployeeForm` [employee-form.tsx:225-230](../web/src/components/admin/employee-form.tsx#L225-L230), `UserForm` (inline employee creation) [user-form.tsx:413-418](../web/src/components/admin/user-form.tsx#L413-L418) |
| `Language` | `SeedLanguageLookupAsync` — dedicated seed function, creates the category with `AllowCustom = false` and 33 global `LookupValue` rows (ElevenLabs-supported languages) — [DataSeeder.cs:475-592](../src/Core/QuantumBuild.Core.Infrastructure/Persistence/DataSeeder.cs#L475-L592) | `false` (system-managed, tenants can only enable/disable) | Yes — 33 rows | Subtitle/translation language pickers, `admin/settings/languages` page, employee language preference, many `web/src` sites |

`SeedLookupCategoriesAsync`'s call-site order confirms all three (`TrainingCategory`,
`Department`, `JobTitle`) are seeded from one shared array in one function, and
`Language` is seeded separately — [DataSeeder.cs:66-68](../src/Core/QuantumBuild.Core.Infrastructure/Persistence/DataSeeder.cs#L66-L68).

### A.5 — The `/admin/settings/lookups` screen — confirmed multi-category, one card per category

- Page: [lookups/page.tsx:1-53](../web/src/app/(authenticated)/admin/settings/lookups/page.tsx#L1-L53). It calls `useLookupCategories()` (generic `GET /api/lookups/categories`), filters out `Language` only (`c.name !== 'Language'`, [:10](../web/src/app/(authenticated)/admin/settings/lookups/page.tsx#L10) — Language has its own dedicated page at `admin/settings/languages`), then **maps one `<LookupCategorySection>` per remaining category** [:40-42](../web/src/app/(authenticated)/admin/settings/lookups/page.tsx#L40-L42). Today that renders exactly two cards: `TrainingCategory` and `JobTitle` — **`Department` also renders as a third card today**, since its `LookupCategory` row still exists and is returned by `GET /api/lookups/categories` (nothing has removed the row).
- Card renderer: `LookupCategorySection` — [lookup-category-section.tsx:46-309](../web/src/components/admin/lookup-category-section.tsx#L46-L309). Fully generic — takes one `category` prop, fetches/creates/updates/deletes/toggles values scoped to `category.name`. The **only** category-name-specific code in the whole component is a display-label lookup table, `CATEGORY_DISPLAY_NAMES` [:23-40](../web/src/components/admin/lookup-category-section.tsx#L23-L40), with a hardcoded `Department` entry at [:28-31](../web/src/components/admin/lookup-category-section.tsx#L28-L31). Categories not in the map fall back to `category.name` / `Manage ${category.name} lookup values` [:47-50](../web/src/components/admin/lookup-category-section.tsx#L47-L50) — i.e. removing the `Department` map entry does not break anything, the card (if it still renders) just gets a generic title.
- **Confirmed: removing ONE card (by removing its underlying `LookupCategory` row, or by the API simply no longer returning it) leaves the other cards untouched** — the page maps over whatever the API returns; there is no hardcoded expectation of exactly N cards or of `Department` specifically.

### A.6 — Test infrastructure treats all three tables as shared, seeded infrastructure

`CustomWebApplicationFactory`'s Respawn reset config explicitly excludes `LookupCategories`, `LookupValues`, `TenantLookupValues` from the per-test DB wipe, with the comment "seeded by DataSeeder on startup (languages, categories, etc.)" — [CustomWebApplicationFactory.cs:288-291](../tests/QuantumBuild.Tests.Integration/Fixtures/CustomWebApplicationFactory.cs#L288-L291). This is category-agnostic (all rows in all three tables survive test resets) — another confirmation the tables are generic shared infrastructure, not Department-specific.

---

## B. Everything Department-category-specific (candidate for removal)

Exhaustive list — every place that names or filters by the `Department` lookup
category specifically:

1. **Seed entry** — the `Department` element of the `categoriesToSeed` array in
   `SeedLookupCategoriesAsync` — [DataSeeder.cs:434](../src/Core/QuantumBuild.Core.Infrastructure/Persistence/DataSeeder.cs#L434). This only creates the `LookupCategory` row named `"Department"` (global, once, first time the app runs against a DB where it doesn't already exist — `existingNames.Contains` check at [:448](../src/Core/QuantumBuild.Core.Infrastructure/Persistence/DataSeeder.cs#L448)). **No corresponding value-seed function exists for Department** — unlike `Language` (`SeedLanguageLookupAsync`) and `TrainingCategory` (`SeedTrainingCategoriesAsync`), there is no `SeedDepartmentValuesAsync`; confirmed by grep — no such function or call site exists anywhere in `DataSeeder.cs`.
2. **Frontend display label** — the `Department` entry in `CATEGORY_DISPLAY_NAMES` — [lookup-category-section.tsx:28-31](../web/src/components/admin/lookup-category-section.tsx#L28-L31) (`title: "Departments"`, `description: "Organisational departments for employee grouping"`). Purely cosmetic (§A.5) — no behavioural dependency.
3. **Page copy** — the lookups page's intro paragraph mentions "departments" generically in prose: *"Manage learning categories, departments, and job titles."* — [lookups/page.tsx:17](../web/src/app/(authenticated)/admin/settings/lookups/page.tsx#L17). Not code-functional, just needs updating if the card is removed.
4. **No DTO, query, service method, permission, or constant names or filters by "Department" anywhere else in the lookup system.** `LookupService`/`LookupsController`/DTOs are 100% generic (§A.3) — there is no `DepartmentLookupService`, no `if (categoryName == "Department")` branch, no `Permissions.*.ManageDepartmentLookup`-style constant. The one permission that *sounds* related — `Permissions.Core.ManageDepartments` ([DataSeeder.cs:416](../src/Core/QuantumBuild.Core.Infrastructure/Persistence/DataSeeder.cs#L416)) — gates the **new** `Department` entity's controller (`/api/departments`, per [docs/department-location-management-ui-recon.md](department-location-management-ui-recon.md) §A1), not the lookup category. The lookup category's own mutation endpoints are gated by the generic `Learnings.Admin` policy shared by every category (§A.3) — there is no Department-specific permission gate.

That is the complete inventory: **one seed-array line, one display-label map entry, one sentence of page copy.** Nothing else in the codebase names the `Department` lookup category.

---

## C. All readers of the Department lookup category anywhere else

This is the safety inventory: everything that would break if the `Department`
`LookupCategory` row and its `TenantLookupValue` rows disappeared.

**Historical wiring (now removed):** per [docs/staff-department-recon.md:74-78](staff-department-recon.md#L74-L78) (the pre-formalisation recon) and [docs/staff-department-reconciliation.md:52-74](staff-department-reconciliation.md#L52-L74) (the chunk-1 reconciliation note), two forms previously bound `<LookupField categoryName="Department">`:
- `EmployeeForm` (was `employee-form.tsx:236-252`)
- `UserForm`'s inline employee-creation section (was `user-form.tsx:423-436`)

**Confirmed current state — both are gone.** Live-grepping `web/src` today for
`categoryName.*Department`, `"Department"`, `'Department'`, and `LookupField`
together returns exactly two `LookupField` usages, and both are `categoryName="JobTitle"`:
- `employee-form.tsx:225-230](../web/src/components/admin/employee-form.tsx#L225-L230)`
- `user-form.tsx:413-418](../web/src/components/admin/user-form.tsx#L413-L418)`

`employee-form.tsx`'s Department field is now a `<Select>` bound to
`form.control` name `departmentId`, sourced from `useAllDepartments()` (the new
structured `Department` entity, `/api/departments/all`) — [employee-form.tsx:80](../web/src/components/admin/employee-form.tsx#L80), [:237-250](../web/src/components/admin/employee-form.tsx#L237-L250). `user-form.tsx` and `create-employee-for-user-dialog.tsx` are on the same new-entity pattern (`useAllDepartments`) — [user-form.tsx](../web/src/components/admin/user-form.tsx), [create-employee-for-user-dialog.tsx:35,64](../web/src/components/admin/create-employee-for-user-dialog.tsx#L35).

**Every other file matching a broad `"Department"` grep in `web/src`** (14 files:
`types/admin.ts`, `lib/api/admin/use-departments.ts`, `lib/api/admin/departments.ts`,
`user-form.tsx`, `employee-form.tsx`, `create-employee-for-user-dialog.tsx`,
compliance report pages ×2, `admin/employees/[id]/page.tsx`, `types/toolbox-talks.ts`,
`assigned-operators-section.tsx`, `bulk-import-upload-panel.tsx`,
`lookup-category-section.tsx`, `toolbox-talks/team/page.tsx`) was individually
spot-checked. All of them reference either the **new** `Department` entity
(`DepartmentDto`, `departmentId`, `departmentName`, `useAllDepartments`) or the
**legacy free-text** `Employee.Department` string column (bulk-import CSV column
docs, My Team display/search, the "Compliance by Department" report which is
actually Site-derived per [docs/staff-department-recon.md:66](staff-department-recon.md#L66)). **None of them call `useLookupValues("Department", ...)` or otherwise read the `Department` `LookupCategory`.**

**Backend-wide grep** for `LookupCategor|LookupValue|TenantLookupValue` across all
`.cs` files (outside migrations, which are historical snapshots and never re-run
against a live schema) surfaces exactly the entity/config/DbContext/service/
controller files already covered in §A, plus `DataSeeder.cs`'s seed calls. **No
report service, export service, query handler, or validator anywhere reads
`TenantLookupValue`/`LookupValue` rows filtered to the `Department` category.**

**Conclusion: nothing else reads it.** The only remaining live reader of the
`Department` `LookupCategory` is the generic `/admin/settings/lookups` page
itself, which renders it as a card purely because `GET /api/lookups/categories`
still returns the row (§A.5) — not because any code specifically asks for
`"Department"`.

---

## D. The orphaned data

No global `LookupValue` rows exist for `Department` (§A.4/B.1 — never seeded).
Any existing data is confined to `TenantLookupValue` rows with `CategoryId`
pointing at the `Department` category and `LookupValueId = null` (tenant-authored
custom entries added via the old card's "Add" button, back when `EmployeeForm`/
`UserForm` still wired `LookupField categoryName="Department"` to it).

**Read-only counting query** (do not run — for the leave-vs-delete decision):

```sql
SELECT
    tlv."TenantId",
    COUNT(*) AS "RowCount"
FROM "TenantLookupValues" tlv
JOIN "LookupCategories" lc ON lc."Id" = tlv."CategoryId"
WHERE lc."Name" = 'Department' AND tlv."IsDeleted" = false
GROUP BY tlv."TenantId"
ORDER BY tlv."TenantId";
```

Per-value detail (same predicate, no aggregation) is already documented as
existing "reconciliation harvest" SQL in [docs/staff-department-reconciliation.md:60-69](staff-department-reconciliation.md#L60-L69) — same table/join, listing `Name`/`IsEnabled` per row instead of counting. That document explicitly frames these rows as "disconnected from `Employee.Department` free text... suggestions only, never enforced" and recommends using them only as a naming signal when defining the new canonical `Department` entity list, not as authoritative data.

**Confirmed nothing from §C still reads these rows** — they are fully inert today:
not read by any form, report, export, or service. Safe to leave in place
indefinitely (harmless, excluded from test resets per §A.6) or delete outright;
this recon takes no position on which, per its non-scope.

---

## E. The removal boundary

### Safe to remove (Department-specific, from §B)

1. `DataSeeder.cs:434` — the `new { Name = "Department", Module = "Core" }` array
   element inside `categoriesToSeed`. Removing it only affects **fresh** databases
   going forward (the seeder's `existingNames.Contains` check is additive-only,
   [:448](../src/Core/QuantumBuild.Core.Infrastructure/Persistence/DataSeeder.cs#L448) — it never deletes categories that already exist). On any database that has
   already run the seeder once, the `LookupCategory` row named `Department` will
   still exist and still be returned by `GET /api/lookups/categories` until
   explicitly deleted (a separate, data-level decision — see next item).
2. `lookup-category-section.tsx:28-31` — the `Department` entry in
   `CATEGORY_DISPLAY_NAMES`. Purely cosmetic; the component and its two siblings
   (`TrainingCategory`, `JobTitle`) are unaffected either way (§A.5 fallback path).
3. `lookups/page.tsx:17` — the "departments" mention in the intro paragraph
   (copy-only).
4. **Optional data-level step**: soft- or hard-delete the `LookupCategory` row
   named `Department` itself (plus its `TenantLookupValue` children — see cascade
   note below). This is what makes the card actually disappear from the
   `/admin/settings/lookups` screen; steps 1-3 alone (code-only) leave the
   existing row in the DB and the card still renders (generically titled, if step
   2 is also done) until the row is removed.

### Must remain (generic infrastructure + other categories, from §A)

- `LookupCategory`, `LookupValue`, `TenantLookupValue` entities, their EF
  configurations, DbContext registration/query filters/DbSets, `ICoreDbContext`.
- `ILookupService`/`LookupService`, `LookupsController`, all lookup DTOs — fully
  generic, serve `TrainingCategory`, `JobTitle`, and `Language` too.
- `SeedLookupCategoriesAsync` itself (the function) — still needed to seed
  `TrainingCategory` and `JobTitle`; only the one array element is removed.
- `SeedLanguageLookupAsync`, `SeedTrainingCategoriesAsync` — untouched, different
  categories.
- `/admin/settings/lookups` page, `LookupCategorySection` component, `LookupField`
  component (still actively used for `JobTitle` in both `EmployeeForm` and
  `UserForm`), `use-lookups.ts` hooks — all untouched; they continue serving
  `TrainingCategory` and `JobTitle` cards/fields exactly as today.
- The Respawn test-reset exclusion list (§A.6) — table-level, not row-level;
  no change needed regardless of what happens to Department rows.

### Danger points / intertwined code — where to be surgical

- **`categoriesToSeed` array** ([DataSeeder.cs:431-436](../src/Core/QuantumBuild.Core.Infrastructure/Persistence/DataSeeder.cs#L431-L436)) holds all three non-Language categories in one array literal inside one shared function. Remove only the `Department` element — do not touch the function, the `TrainingCategory`/`JobTitle` elements, or the surrounding seeding call chain ([:66-68](../src/Core/QuantumBuild.Core.Infrastructure/Persistence/DataSeeder.cs#L66-L68)).
- **`CATEGORY_DISPLAY_NAMES`** ([lookup-category-section.tsx:23-40](../web/src/components/admin/lookup-category-section.tsx#L23-L40)) is one object literal shared by all four categories (including `Language`, even though `Language` is filtered out of this particular page — the map itself is not page-scoped, just unused for Language here). Remove only the `Department:` key.
- **Cascade delete on the `LookupCategory` row itself**: `LookupCategoryConfiguration.cs:27-35](../src/Core/QuantumBuild.Core.Infrastructure/Data/Configurations/LookupCategoryConfiguration.cs#L27-L35)` configures `OnDelete(DeleteBehavior.Cascade)` from `LookupCategory` to **both** `Values` (`LookupValue`) and `TenantValues` (`TenantLookupValue`), scoped by FK (`CategoryId`). Deleting the `Department` category row will cascade-delete only rows whose `CategoryId` matches it — since Department has zero `LookupValue` rows (§B.1) and its `TenantLookupValue` rows are the orphaned data from §D, this cascade is safe and will **not** touch any `TrainingCategory`/`JobTitle`/`Language` row (different `CategoryId`). No manual cleanup of child rows is needed before deleting the category row if a hard delete is chosen — but confirm this is genuinely wanted (data loss) versus a soft-delete (`IsDeleted = true`, which the entity supports via `BaseEntity` and which the existing query filters already respect, [ApplicationDbContext.cs:380](../src/Core/QuantumBuild.Core.Infrastructure/Data/ApplicationDbContext.cs#L380)) before acting — this recon takes no position, per its non-scope.
- **No other danger points found.** Because the lookup system was already built category-agnostic (categoryName as a runtime string, no per-category code paths in service/controller/DTOs), there is no risk of Department-specific logic being accidentally entangled with shared logic beyond the two small, clearly-delimited edits above (one array element, one map entry) plus the optional data-level row deletion.
