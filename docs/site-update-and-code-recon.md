# Site (Location) Write-Path Recon — SiteCode Optionality & Partial Update

Read-only recon. No code was changed. Facts only, each with a `file:line` citation, gathered
for two planned fixes:

1. Make `SiteCode` genuinely optional (nullable, unique only when present).
2. Make Site `UPDATE` a partial update (patch) instead of a full overwrite.

---

## A. SiteCode — current constraints and dependents

### A1. Current definition

- **Entity** — `SiteCode` is a non-nullable `string`, default `string.Empty`:
  `src/Core/QuantumBuild.Core.Domain/Entities/Site.cs:13`
  ```csharp
  public string SiteCode { get; set; } = string.Empty;
  ```
- **EF configuration** — required, max length 50:
  `src/Core/QuantumBuild.Core.Infrastructure/Data/Configurations/SiteConfiguration.cs:13-15`
  ```csharp
  builder.Property(e => e.SiteCode)
      .HasMaxLength(50)
      .IsRequired();
  ```
- **Unique index** — confirmed unique on `{TenantId, SiteCode}`, not filtered (applies to every row):
  `src/Core/QuantumBuild.Core.Infrastructure/Data/Configurations/SiteConfiguration.cs:51-53`
  ```csharp
  builder.HasIndex(e => new { e.TenantId, e.SiteCode })
      .IsUnique()
      .HasDatabaseName("IX_Sites_TenantId_SiteCode");
  ```
- **DB column** — `nullable: false`, confirmed in the original migration:
  `src/Core/QuantumBuild.Core.Infrastructure/Migrations/20251216112816_InitialModularMonolith.cs:573`
  ```csharp
  SiteCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
  ```
  No later migration alters this column's nullability — every subsequent `*.Designer.cs` model
  snapshot up to the current one (e.g.
  `src/Core/QuantumBuild.Core.Infrastructure/Migrations/20260812111130_RemoveDepartmentLookupCategory.Designer.cs:991`)
  still shows `b.Property<string>("SiteCode")` with no `IsNullable` marker, i.e. still `NOT NULL`.
- **Tenant scoping** — Site's global query filter (applied at `ApplicationDbContext` level, so
  every query against `_context.Sites` is implicitly tenant-scoped even without an explicit
  `.Where(TenantId == ...)`):
  `src/Core/QuantumBuild.Core.Infrastructure/Data/ApplicationDbContext.cs:337`
  ```csharp
  modelBuilder.Entity<Site>().HasQueryFilter(e => !e.IsDeleted && (BypassTenantFilter || e.TenantId == TenantId));
  ```

### A2. What reads or depends on SiteCode being non-null

Full inventory of every `SiteCode`/`siteCode` reference in the codebase (backend `.cs`
non-migration files: 6 files; frontend: 7 files). Migration/model-snapshot files are excluded
below since they are schema history, not runtime dependents.

**Backend (`SiteService.cs` and DTOs) — string-safe, no explicit null-dependence found:**

- Sort/search — `.OrderBy(s => s.SiteCode)` and `s.SiteCode.ToLower().Contains(searchLower)`:
  `src/Core/QuantumBuild.Core.Application/Features/Sites/SiteService.cs:25`,
  `SiteService.cs:72`, `SiteService.cs:128`, `SiteService.cs:138`.
  `.ToLower().Contains()` on a nullable string would throw `NullReferenceException` at
  `SiteService.cs:72` if `SiteCode` is null and the caller passes a search term — this is a
  concrete null-handling gap that a nullable `SiteCode` would introduce (currently unreachable
  because the column can't be null).
- Duplicate-code check on create/update — equality comparison, not `Contains`, so `s.SiteCode ==
  dto.SiteCode` degrades safely to matching only other null-coded sites if `dto.SiteCode` is
  null: `SiteService.cs:217` (create), `SiteService.cs:326` (update, excludes current row).
- DTO projections simply pass the string through: `SiteDto` positional field
  (`src/Core/QuantumBuild.Core.Application/Features/Sites/DTOs/SiteDto.cs:5`), populated at
  `SiteService.cs:28,91,152,257,382`.
- No other backend module (`src/Modules/ToolboxTalks/**`, `Features/Employees/**`,
  `Features/BulkImport/**`) references `SiteCode` at all — confirmed via full-tree search;
  zero matches outside `Features/Sites/*`. Employee's site relationship is by `PrimarySiteId`
  (FK, `src/Core/QuantumBuild.Core.Domain/Entities/Employee.cs:63`) and displays
  `PrimarySite.SiteName`, never `SiteCode`
  (`src/Core/QuantumBuild.Core.Application/Features/Employees/EmployeeService.cs:75,146,218,476,801,988,1164,1293`).

**Frontend — three display sites concatenate SiteCode directly into a label, one column
renders it through a null-safe generic table cell:**

- `web/src/components/admin/user-form.tsx:506` — `{site.siteName} ({site.siteCode})` in a
  site-picker `<SelectItem>`. If `siteCode` becomes `undefined`, React renders `SiteName ()`
  (empty parens) — not a crash, but a cosmetic artifact needing null-handling (e.g. conditional
  parens).
- `web/src/components/admin/employee-form.tsx:454` — identical pattern, same file-level
  duplication.
- `web/src/app/(authenticated)/admin/settings/locations/[id]/edit/page.tsx:92` — `Editing:
  {site.siteName} ({site.siteCode})` in the edit-page header, same empty-parens issue.
- `web/src/app/(authenticated)/admin/settings/locations/page.tsx:135` — DataTable column
  `{ key: "siteCode", header: "Code", sortable: true }` with no custom `render`. The DataTable's
  default cell renderer already null-coalesces:
  `web/src/components/shared/data-table.tsx:420-422`
  ```tsx
  : String(
      (item as Record<string, unknown>)[column.key] ?? ""
    )}
  ```
  So this specific surface needs **no** change — it already renders an empty string for a
  missing code.
- `web/src/types/admin.ts:3` — the `Site` TypeScript interface types `siteCode: string`
  (non-optional). Changing the backend to nullable would need this widened to
  `siteCode: string | null` (or `string | null | undefined`) to match reality, which is itself
  the trigger for the three call sites above needing review.

**Conclusion for A2:** nothing hard-depends on `SiteCode` being non-null in a way that would
throw today (the one real risk, `.ToLower().Contains()` at `SiteService.cs:72`, is inside the
search filter and only reachable if `SiteCode` is nullable). Three frontend label
concatenations would show empty parens rather than break. No exports, no cross-module code
keys off `SiteCode`.

### A3. How SiteCode is currently set on create/update

- **Backend `CreateAsync`** — takes `dto.SiteCode` verbatim, no generation/fallback logic:
  `src/Core/QuantumBuild.Core.Application/Features/Sites/SiteService.cs:227`
  ```csharp
  SiteCode = dto.SiteCode,
  ```
- **Backend `UpdateAsync`** — same, verbatim overwrite:
  `SiteService.cs:333`
  ```csharp
  site.SiteCode = dto.SiteCode;
  ```
- **Backend validation** — both `CreateSiteValidator` and `UpdateSiteValidator` require
  `SiteCode` to be non-empty and ≤50 chars:
  `src/Core/QuantumBuild.Core.Application/Features/Sites/CreateSiteValidator.cs:10-14`,
  `src/Core/QuantumBuild.Core.Application/Features/Sites/UpdateSiteValidator.cs:10-14`.
- **Frontend fallback (the workaround this fix removes)** — `LocationForm`'s Zod schema makes
  `siteCode` optional (`web/src/components/admin/location-form.tsx:26`), but `onSubmit` then
  synthesizes a code client-side if the user left it blank, specifically because the backend
  requires a non-empty value:
  `web/src/components/admin/location-form.tsx:44-56`
  ```tsx
  // A blank Location Code is a UI convenience, not a valid API value - SiteCode
  // is NOT NULL and unique per tenant at the DB level. Derive a fallback from
  // the name plus a random suffix so blank codes never collide with each other.
  function generateFallbackCode(name: string): string {
    const base = name
      .trim()
      .toUpperCase()
      .replace(/[^A-Z0-9]+/g, "-")
      .replace(/^-+|-+$/g, "")
      .slice(0, 40);
    const suffix = Math.random().toString(36).slice(2, 6).toUpperCase();
    return `${base || "LOC"}-${suffix}`;
  }
  ```
  invoked at `location-form.tsx:82`:
  ```tsx
  const siteCode = values.siteCode?.trim() || generateFallbackCode(values.siteName);
  ```
  This is the only code-generation logic anywhere for `SiteCode` — there is no server-side
  auto-generation (unlike `ToolboxTalk.Code`, which auto-generates from title initials per
  CLAUDE.md's "ToolboxTalk Code Field" section; Site has no equivalent backend behaviour).

---

## B. Site UPDATE semantics and its callers

### B4. `SiteService.UpdateAsync` is confirmed a full overwrite

Every entity field is unconditionally assigned from the DTO, with no per-field
"was this provided" check:
`src/Core/QuantumBuild.Core.Application/Features/Sites/SiteService.cs:333-346`
```csharp
site.SiteCode = dto.SiteCode;
site.SiteName = dto.SiteName;
site.Address = dto.Address;
site.City = dto.City;
site.PostalCode = dto.PostalCode;
site.SiteManagerId = dto.SiteManagerId;
site.CompanyId = dto.CompanyId;
site.Phone = dto.Phone;
site.Email = dto.Email;
site.IsActive = dto.IsActive;
site.Notes = dto.Notes;
site.Latitude = dto.Latitude;
site.Longitude = dto.Longitude;
site.GeofenceRadiusMeters = dto.GeofenceRadiusMeters;
```
`FloatProjectId` gets slightly different treatment — it's still driven unconditionally by the
DTO, but with an extra guarded branch that also stamps `FloatLinkedAt`/`FloatLinkMethod` when
the value changes: `SiteService.cs:348-365`.
```csharp
if (dto.FloatProjectId != site.FloatProjectId)
{
    if (dto.FloatProjectId.HasValue) { ... site.FloatLinkedAt = DateTime.UtcNow; site.FloatLinkMethod = "Manual"; }
    else { site.FloatProjectId = null; site.FloatLinkedAt = null; site.FloatLinkMethod = null; }
}
```
This is still "DTO fully determines the field" — a caller that omits `floatProjectId` (defaults
to `null` per `UpdateSiteDto`'s `int? FloatProjectId = null` at
`src/Core/QuantumBuild.Core.Application/Features/Sites/DTOs/UpdateSiteDto.cs:18`) will clear an
existing Float link, exactly like the other fields.

### B5. Every caller of Site update, and whether any relies on overwrite semantics

**Backend:** `ISiteService.UpdateAsync` has exactly one implementation
(`SiteService.cs:286`) and exactly one caller, `SitesController.Update`:
`src/QuantumBuild.API/Controllers/SitesController.cs:108-120`. No other backend code, job, or
test calls it — full-tree search for `UpdateAsync`/`SiteService` under `tests/` returned no
matches.

**Frontend — exactly two callers, both added in the current "Location management UI" chunk,
both already work around the full-overwrite behaviour by echoing every field back:**

1. `LocationForm` (edit mode) —
   `web/src/components/admin/location-form.tsx:85-108`. Sends all editable fields plus every
   non-editable construction/Float field read back from the loaded `site` record:
   ```tsx
   siteManagerId: site.siteManagerId ?? null,
   companyId: site.companyId ?? null,
   latitude: site.latitude ?? null,
   longitude: site.longitude ?? null,
   geofenceRadiusMeters: site.geofenceRadiusMeters ?? null,
   floatProjectId: site.floatProjectId ?? null,
   ```
   with the comment directly above (`location-form.tsx:98-100`) stating this is a deliberate
   workaround for the overwrite:
   ```tsx
   // Construction/Float fields are not shown in this form - echo the
   // record's current values back unchanged so the full-overwrite
   // update API doesn't null them out.
   ```
2. `LocationsPage.handleToggleActive` (Active/Inactive toggle in the list view) —
   `web/src/app/(authenticated)/admin/settings/locations/page.tsx:99-131`. Same pattern: reads
   the current `site` row and echoes every field, only flipping `isActive`.

**No caller relies on the overwrite clearing a field it deliberately omits.** Both existing
callers do the opposite — they exist specifically to avoid triggering the overwrite's clearing
behaviour by re-sending the current value of every field they don't intend to change. This means
converting `UpdateAsync` to true partial-update semantics would not break either caller (both
would continue to work if changed to send only the fields they actually edit, and would
continue to work unchanged if left as-is, since sending the current value is a no-op under
patch semantics too).

There is no `UpdateSiteDto` caller anywhere else in the frontend (`web/src/lib/api/admin/use-sites.ts:49-59`
is the only mutation hook wrapping it, confirmed via search for `useUpdateSite`/`updateSite(`
across `web/`).

### B6. Construction fields — no other update path exists

Searched all of `src/**/*.cs` for direct assignment to `Site.Latitude`, `Site.Longitude`,
`Site.GeofenceRadiusMeters`, `Site.FloatProjectId`, `Site.FloatLinkedAt`, or
`Site.FloatLinkMethod`. The only hits are inside `SiteService.CreateAsync` (initial values,
`SiteService.cs:238-243`) and `SiteService.UpdateAsync` (`SiteService.cs:344-363`) — there is
no separate geofence-setting feature, Float-linking job, or other service that writes these
fields on `Site` outside the create/update DTO flow. (The similarly-named `FloatLinkMethod`/
`FloatLinkedAt` fields on `Employee`, set at
`src/Core/QuantumBuild.Core.Application/Features/Employees/EmployeeService.cs:340-341,722-730`,
are a distinct pair of properties on a different entity and are irrelevant here — Employee has
its own independent Float-link fields.) A partial-update change to `Site.UpdateAsync` therefore
has no other in-flight writer to conflict with for these four fields.

---

## C. Feasibility / approach facts

### C7. Filtered unique index for optional SiteCode

The codebase already uses Postgres partial/filtered unique indexes via EF Core migrations
elsewhere, confirming the mechanism is available and already in use in this schema — though the
existing example filters on `IsDeleted`, not on the indexed column's own nullness:
`src/Core/QuantumBuild.Core.Infrastructure/Migrations/20260312191539_FilteredUniqueCodeIndex.cs:18-24`
```csharp
migrationBuilder.CreateIndex(
    name: "IX_ToolboxTalks_TenantId_Code",
    schema: "toolbox_talks",
    table: "ToolboxTalks",
    columns: new[] { "TenantId", "Code" },
    unique: true,
    filter: "\"IsDeleted\" = false");
```
The equivalent for `SiteCode` would be a `migrationBuilder.CreateIndex(... filter:
"\"SiteCode\" IS NOT NULL")` (or the EF Fluent equivalent, `builder.HasIndex(...).HasFilter("\"SiteCode\" IS NOT NULL")`,
already used for a different nullable column on the same entity —
`SiteConfiguration.cs:56-58`:
```csharp
builder.HasIndex(s => s.FloatProjectId)
    .HasFilter("\"FloatProjectId\" IS NOT NULL")
    .HasDatabaseName("IX_Sites_FloatProjectId");
```
— this is a direct, already-in-file precedent for "nullable column, unique/filtered index
skips nulls," on the very same `SiteConfiguration` class, though `FloatProjectId`'s index is
not currently combined with `IsUnique()`.

No conflict was found in A2 between making the column nullable and any existing constraint —
`SiteCode` has no `[Required]`-equivalent consumer beyond FluentValidation (app-layer, easy to
relax) and the EF `.IsRequired()` (schema-layer, the actual change needed).

### C8. Existing patch pattern to mirror, and the omit-vs-null crux

**No entity in this codebase currently has a general partial-update endpoint.** The one
existing precedent for "update less than the whole record" is a different pattern — dedicated,
narrow, single-field `PATCH` endpoints, not a DTO that distinguishes omitted-vs-provided across
many optional fields:
`src/QuantumBuild.API/Controllers/ToolboxTalksController.cs:793` (`PATCH .../step`) and
`ToolboxTalksController.cs:824` (`PATCH .../active`, explicitly commented as a "narrow
single-field update" at `ToolboxTalksController.cs:820-822`). Each of these takes a small
request DTO with exactly the one field being changed — it sidesteps the omit-vs-null problem
entirely by not having any other fields to omit.

**The crux fact:** `UpdateSiteDto` cannot currently tell "field not sent" apart from "field sent
as null/default." The API's JSON deserialization is configured only with camelCase naming and
an enum string converter —
`src/QuantumBuild.API/Program.cs:177-181`:
```csharp
builder.Services.AddControllers()
    ...
    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
```
No `RespectNullableAnnotations`, no `required` members, no custom converters. `UpdateSiteDto`
is a plain positional record
(`src/Core/QuantumBuild.Core.Application/Features/Sites/DTOs/UpdateSiteDto.cs:3-19`) with no
`required` keyword on any parameter. Under System.Text.Json's constructor-based record
deserialization, a JSON payload that omits a property binds that constructor parameter to its
default (`null` for reference/nullable types, `false`/`0` for `bool`/`int`) — identical to what
an explicit `null`/`0`/`false` in the payload would produce. This holds even for the
currently-non-nullable `SiteCode` and `SiteName` string parameters (no compile-time non-null
enforcement carries through to runtime JSON binding) and for `IsActive` (`bool`, no nullable
wrapper) — so today, omitting `isActive` from a PUT body silently binds to `false`, not "leave
unchanged." No `[Required]`/model-validation attribute catches this either; FluentValidation
only rejects empty *strings*, not "was this key present in the JSON."

**Implication for a patch design:** because the DTO layer cannot distinguish "omitted" from
"null," a genuine partial-update implementation cannot use the current `UpdateSiteDto` shape
as-is. It would need one of: (a) wrapping every field in an explicit tri-state type (e.g., a
custom `Optional<T>`/`JsonElement`-based DTO — no such type exists anywhere in this codebase
today, confirmed via search for `Optional<`, `JsonElement`, `PartialUpdate`, `Patch` returning
no hits in `Features/`), (b) switching to `JsonPatchDocument` (also unused anywhere in this
codebase — the `HttpPatch` search only found the two narrow single-field endpoints above, which
use plain request DTOs, not `JsonPatchDocument`), or (c) the narrow-endpoint pattern already in
use for ToolboxTalks (split Site update into several small single- or few-field PATCH
endpoints instead of one general-purpose PUT). This is a design decision for the fix itself,
noted here only as the mechanical fact needed to choose safely — not a chosen approach.

---

## Summary for safe implementation

- **SiteCode-optional:** entity/config/DB column are all currently `NOT NULL` + unique on
  `{TenantId, SiteCode}` (A1). Nothing outside `Features/Sites/*` and three frontend label
  concatenations depends on non-null (A2) — the one real risk is
  `SiteService.cs:72`'s `.ToLower().Contains()` needing a null guard. A filtered-unique-index
  precedent already exists in this exact file for a different column (`FloatProjectId`,
  `SiteConfiguration.cs:56-58`) and elsewhere in the schema for a different filter condition
  (`FilteredUniqueCodeIndex` migration) — both confirm the migration mechanism works here (C7).
- **Partial update:** `UpdateAsync` is a full, unconditional overwrite of every field
  (B4). It has exactly one backend caller (the controller) and exactly two frontend callers,
  both of which already defensively echo back every field they don't intend to change — neither
  relies on omission-clears-field behaviour (B5). The four construction/Float fields have no
  other writer to conflict with (B6). The blocking fact for design: the DTO cannot currently
  distinguish omitted-from-null at the JSON binding layer, and no tri-state/patch pattern exists
  anywhere in the codebase to copy — the only present partial-update precedent is
  narrow single-field PATCH endpoints, a structurally different approach (C8).
