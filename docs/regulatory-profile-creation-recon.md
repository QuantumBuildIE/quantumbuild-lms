# Regulatory Profile Creation Recon

**Date:** 2026-07-30
**Status:** Read-only recon. Facts only, no design, no code changes.

**Trigger:** Onboarding the real HIQA homecare document next week requires
attaching a sector to a newly created `RegulatoryDocument` (creating a
`RegulatoryProfile`). No UI or API path does this today — only seed data and
test helper code construct a `RegulatoryProfile` row. This document maps
everything a create-profile capability would have to fit into, so an
implementation prompt can be written against verified current-state facts.

**Relationship to prior recon:** `docs/regulatory-architecture-recon.md`
(2026-07-23) already investigated this exact gap in its Part 2 and Summary
item 2 ("currently zero UI/API path exists ... this is the most direct gap").
That recon is 7 days old and some of its other findings are now stale — e.g.
it stated "No create/update/delete endpoint exists for `RegulatoryBody`
anywhere in the codebase" (line 76), but `RegulatoryIngestionController` now
has `POST /api/regulatory/bodies` (`RegulatoryIngestionController.cs:84-106`)
committed since. This document re-verifies every claim against the current
code rather than citing the older recon as ground truth, and flags where the
two recons diverge.

---

## 1. RegulatoryProfile entity and EF configuration

**Entity:** `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Entities/RegulatoryProfile.cs:11-37`

```
public class RegulatoryProfile : BaseEntity
{
    public Guid RegulatoryDocumentId { get; set; }
    public Guid SectorId { get; set; }
    public string SectorKey { get; set; } = string.Empty;
    public string ScoreLabel { get; set; } = string.Empty;
    public string ExportLabel { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CategoryWeightsJson { get; set; } = "[]";
    public bool IsActive { get; set; } = true;

    public RegulatoryDocument RegulatoryDocument { get; set; } = null!;
    public Sector Sector { get; set; } = null!;
    public ICollection<RegulatoryCriteria> Criteria { get; set; } = new List<RegulatoryCriteria>();
}
```

`BaseEntity` (not `TenantEntity`) supplies `Id`, `CreatedAt`, `CreatedBy`,
`UpdatedAt`, `UpdatedBy`, `IsDeleted`, `DeletedBy` — there is no `TenantId`
on this entity; it is system-managed, matching `RegulatoryBody` and
`RegulatoryDocument`.

**EF configuration:** `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Persistence/Configurations/RegulatoryProfileConfiguration.cs`

| Field | Constraint | Line |
|---|---|---|
| `RegulatoryDocumentId` | Required (Guid) | 14-15 |
| `SectorId` | Required (Guid) | 17-18 |
| `SectorKey` | Required, max 50 | 20-22 |
| `ScoreLabel` | Required, max 200 | 24-26 |
| `ExportLabel` | Required, max 200 | 28-30 |
| `Description` | Required, max 500 | 32-34 |
| `CategoryWeightsJson` | Required, `text` column type | 36-38 |
| `IsActive` | Required, DB default `true` | 40-42 |
| `CreatedAt` | Required | 45-46 |
| `CreatedBy` | Required, max 256 | 48-50 |
| `UpdatedAt` | Optional | 52 |
| `UpdatedBy` | Optional, max 256 | 54-55 |
| `IsDeleted` | Required, DB default `false` | 57-59 |

Relationships (lines 62-70):
- `Sector` — `HasOne().WithMany()` (no inverse collection on `Sector`), FK `SectorId`, `OnDelete(DeleteBehavior.Restrict)`.
- `Criteria` — `HasMany().WithOne()`, FK `RegulatoryProfileId` on `RegulatoryCriteria`, `OnDelete(DeleteBehavior.Restrict)`.
- No explicit `HasOne` for `RegulatoryDocument` in this file; the FK is established from `RegulatoryDocumentConfiguration`'s side (confirmed by the migration below creating `FK_RegulatoryProfiles_RegulatoryDocuments_RegulatoryDocumentId` with `Restrict`).

Indexes (lines 73-78):
- **Unique** `{RegulatoryDocumentId, SectorId}` → `ix_regulatory_profiles_document_sector`.
- Non-unique `SectorKey` → `ix_regulatory_profiles_sector_key`.

Query filter (line 81): `!e.IsDeleted` only — no tenant predicate (there is
no `TenantId` to filter on). This matches `ApplicationDbContext.cs`'s
documented list of deliberately-not-tenant-scoped `BaseEntity` types, which
names `RegulatoryProfile` explicitly (see `ApplicationDbContext.cs` comment
referenced in `docs/regulatory-architecture-recon.md:44-48`; verified still
true — `RegulatoryProfile` carries no `TenantId` field at all today).

**Migration (source of truth for the actual DB schema):**
`src/Core/QuantumBuild.Core.Infrastructure/Migrations/20260317131056_AddRegulatoryProfileChain.cs:66-186`
creates the `RegulatoryProfiles` table under schema `toolbox_talks`, with FKs
to `RegulatoryDocuments` (Restrict) and `Sectors` (Restrict), and the same
three indexes as above. The unique index at lines 168-173:

```csharp
migrationBuilder.CreateIndex(
    name: "ix_regulatory_profiles_document_sector",
    schema: "toolbox_talks",
    table: "RegulatoryProfiles",
    columns: new[] { "RegulatoryDocumentId", "SectorId" },
    unique: true);
```

**No `filter:` argument** — this is a plain unique constraint over *all*
rows in the table regardless of `IsDeleted`. Contrast with
`RegulatoryRequirementMapping`'s indexes in the later
`20260318162722_AddRegulatoryRequirements.cs:108-119`, which both carry
`filter: "\"CourseId\" IS NOT NULL"` / `filter: "\"ToolboxTalkId\" IS NOT NULL"`
— i.e. the codebase does use partial/filtered unique indexes elsewhere when
it wants soft-deleted or null rows excluded from the constraint, but this
pattern was **not** applied to `RegulatoryProfile`.

**What a valid new row requires:** `RegulatoryDocumentId` (must reference an
existing `RegulatoryDocument`), `SectorId` (must reference an existing
`Sector`), `SectorKey` (denormalised copy of `Sector.Key`, ≤50 chars),
`ScoreLabel` (≤200 chars), `ExportLabel` (≤200 chars), `Description` (≤500
chars), `CategoryWeightsJson` (a JSON string; entity default is `"[]"`), and
the pair `{RegulatoryDocumentId, SectorId}` must not already exist as a row
in the table (soft-deleted or not — see §6).

---

## 2. Every place a RegulatoryProfile is currently constructed

**Production code — exactly one site, the seeder:**
`src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Persistence/Seed/RegulatoryProfileSeedData.cs:234-248`

```csharp
newProfiles.Add(new RegulatoryProfile
{
    Id = Guid.NewGuid(),
    RegulatoryDocumentId = docId,
    SectorId = sector.Id,
    SectorKey = sectorKey,
    ScoreLabel = scoreLabel,
    ExportLabel = exportLabel,
    Description = description,
    CategoryWeightsJson = categoryWeightsJson,
    IsActive = true,
    CreatedAt = now,
    CreatedBy = "system"
});
```

Guarded by a pre-check at line 231: `if (existingProfiles.Any(p => p.RegulatoryDocumentId == docId && p.SectorId == sector.Id)) continue;` — the seeder does its own idempotency check in application code (querying with `IgnoreQueryFilters()` at line 187) rather than relying on catching the unique-constraint violation.
`CategoryWeightsJson` here is a real, hand-authored JSON array of `{Key, Label, Weight}` scoring-category objects (lines 196, 201, 206, 211, 216, 221) — not left at the `"[]"` default.

Confirmed via `Grep` for `new RegulatoryProfile` across `src/`: this seed file is the **only** production-code construction site. `Grep` for `RegulatoryProfiles.Add` across the whole repo also returns only this file plus test files (§ below) — no controller/service/job in `src/` ever inserts a `RegulatoryProfile`.

**Test helper code — 7 test files, all following the identical field-by-field shape**, confirmed by direct read of each: `ComplianceStandardsDisplayTests.cs:78-93`, `RegulatoryApplicabilityTests.cs:48-63`, `RequirementMappingLiveFilterTests.cs:80-95`, `RequirementMappingJobCandidateGatingTests.cs:94-109` and `:132-147`, `RegulatoryIngestionTests.cs:99-114`, `TranslationSectorInstructionsTests.cs:116-126`. Representative example (`RegulatoryIngestionTests.cs:99-109`):

```csharp
var profile = new RegulatoryProfile
{
    Id = Guid.NewGuid(),
    RegulatoryDocumentId = document.Id,
    SectorId = sector.Id,
    SectorKey = sector.Key,
    ScoreLabel = "Test Score",
    ExportLabel = "TSP",
    Description = "Integration test profile",
    IsActive = true
};
```

Every test helper builds a `RegulatoryBody` → `RegulatoryDocument` → `RegulatoryProfile` chain by hand in that order, adds all three to the context, then calls `SaveChangesAsync()` once. None of the 7 test files omit `SectorKey`, `ScoreLabel`, or `ExportLabel` — all treat them as required in practice even though only `SectorKey` is technically required at the DB level with no default (`ScoreLabel`/`ExportLabel`/`Description` are also `IsRequired()` in the EF config, so an empty string would violate `NOT NULL`... they are non-nullable `string` C# properties defaulting to `string.Empty`, so omitting them in an object initializer still satisfies the DB `NOT NULL` — just with an empty string, which the config's `IsRequired()` does not forbid at the DB level, only forbids `NULL`). `CategoryWeightsJson` is omitted in most test helpers, relying on the entity's C# default `"[]"`.

**No test exercises the soft-delete-then-recreate collision** (§6) — none of the 7 files ever sets `IsDeleted = true` on a `RegulatoryProfile` or attempts a second insert for the same `{RegulatoryDocumentId, SectorId}` pair after a delete.

---

## 3. RegulatoryIngestionController — pattern to mirror

**File:** `src/QuantumBuild.API/Controllers/RegulatoryIngestionController.cs`

- **Class-level gate:** `[Authorize(Policy = "Tenant.Manage")]` (line 17), route `[Route("api/regulatory")]` (line 16). Per CLAUDE.md's role matrix, `Tenant.Manage` is held only by `SuperUser`.
- **Constructor injection:** `IRequirementIngestionService`, `ICurrentUserService` (injected but not used in the file body — no action currently reads it), `ILogger<RegulatoryIngestionController>` (lines 20-36).
- **Action pattern**, consistent across all 11 actions: try/catch three exception tiers —
  1. A specific domain exception (`InvalidSourceUrlException`) → `400` with `{ message, errorCode }` (e.g. lines 124-128, 209-213).
  2. `InvalidOperationException` → `400` with `{ message = ex.Message }` (e.g. lines 129-133, 214-218, 291-294).
  3. Generic `Exception` → `500` with a generic `{ message }`, and `_logger.LogError` (e.g. lines 134-138, 219-223).
- **DTOs returned directly** (not wrapped in a `Result<T>` envelope) — confirmed by the absence of any `if (!result.Success) return BadRequest(result)` check anywhere in this controller. This matches CLAUDE.md Note 18's "DTOs directly" bucket, which explicitly lists `RegulatoryIngestionController` alongside `TranslationValidation` and others.
- **Two existing actions already show the exact shape a `CreateProfile` action would need**, since they create sibling entities in the same chain:
  - `POST /bodies` → `CreateBody` (lines 84-106) calls `_ingestionService.CreateRegulatoryBodyAsync(request, cancellationToken)`, catches `InvalidOperationException` for validation failures.
  - `POST /documents` → `CreateDocument` (lines 112-139) calls `_ingestionService.CreateDocumentAsync(request, cancellationToken)`, additionally catches `InvalidSourceUrlException`.
- **A create-profile action would naturally sit** immediately after `CreateDocument` (line 139) and before `UploadSourceDocument` (line 146), following the same `[HttpPost("documents/{documentId:guid}/...")]` nesting already used by `UploadSourceDocument` (line 146) and `StartIngestion` (line 194) for other per-document sub-resources.

**Service layer:** `IRequirementIngestionService`
(`src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Common/Interfaces/IRequirementIngestionService.cs`)
is the single interface the controller depends on; its implementation is
`RequirementIngestionService`
(`src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Ingestion/RequirementIngestionService.cs`).//
The interface's own XML doc comment on `CreateDocumentAsync`
(`IRequirementIngestionService.cs:98-104`) states explicitly:

> "Creates a new regulatory document. Persists with LastIngestionStatus=Idle
> and no profiles — ingestion and sector-profile setup remain separate,
> later actions."

This is the current design intent documented in the interface itself — sector-profile creation was always meant to be a distinct, later action, not yet built.

**`CreateDocumentAsync` implementation** (`RequirementIngestionService.cs:497-553`) is the closest structural analog for a hypothetical `CreateProfileAsync`: validates required string fields with length checks (`InvalidOperationException` on failure), loads the parent entity by ID (`RegulatoryBody` there; would be `RegulatoryDocument` for a profile) with `?? throw new InvalidOperationException(...)`, constructs the new entity, `Add()`s it, `SaveChangesAsync()`s, logs via `_logger.LogInformation`, and returns a DTO. `CreateRegulatoryBodyAsync` (lines 420-495) additionally shows the pattern for a cross-field invariant check (`ValidateSectorConsistency()`, `RegulatoryBody.cs:45-52`) called both by the service ("defence in depth", comment at `RequirementIngestionService.cs:476`) and enforced by a DB check constraint (`ck_regulatory_bodies_kind_sector`) — i.e. the codebase's established convention for a business-rule invariant is "validate in the service AND back it with a DB constraint," which would be the applicable pattern for the `{RegulatoryDocumentId, SectorId}` uniqueness check too (already backed by the DB unique index; the service would need to pre-check and translate the DB violation to a friendly `InvalidOperationException`, matching how `CreateRegulatoryBodyAsync` pre-checks `codeExists` at lines 452-456 before insert rather than letting the DB uniqueness violation surface raw).

---

## 4. Sector lookup for selection

**Endpoint:** `GET /api/toolbox-talks/sectors` — `SectorsController.cs:15-21`, `[Authorize]` only (any authenticated user, no specific permission policy), route `api/toolbox-talks/sectors`.

```csharp
[HttpGet]
public async Task<IActionResult> GetActiveSectors(CancellationToken cancellationToken)
{
    var sectors = await sectorService.GetActiveSectorsAsync(cancellationToken);
    return Ok(sectors);
}
```

**Service:** `SectorService.GetActiveSectorsAsync`
(`src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Sectors/SectorService.cs:10-16`)
queries `dbContext.Sectors.IgnoreQueryFilters().Where(s => s.IsActive && !s.IsDeleted).OrderBy(s => s.DisplayOrder)`, returns `List<SectorDto>`.

**DTO shape:** `SectorDto`
(`.../Application/DTOs/Sectors/SectorDto.cs:3-9`) — `Id` (Guid), `Key`
(string), `Name` (string), `Icon` (string?), plus (not shown in the grep
excerpt but referenced elsewhere in CLAUDE.md) `DisplayOrder`/`IsActive`.
Returns DTOs directly (bare `Ok(sectors)`, no envelope).

**Existing consumption pattern:** `CreateRegulatoryBodyRequest.SectorId` (a
`Guid?`) is populated in the "Add Body" dialog flow, and
`CreateRegulatoryBodyAsync` resolves it via
`_dbContext.Sectors.FirstOrDefaultAsync(s => s.Id == request.SectorId.Value, ...)`
(`RequirementIngestionService.cs:461-463`) — i.e. the body-creation flow
already has a body/sector relationship, but note this is `RegulatoryBody`'s
own **direct** `SectorId` field (used only for `Standard`-kind bodies, see
§7), a structurally separate mechanism from `RegulatoryProfile`. The
document-create dialog (`create-regulatory-document-dialog.tsx`, read in
full — §5) does **not** currently consume the sectors endpoint at all; it
only consumes `useRegulatoryBodies()` for the body picker. No existing
frontend dialog today fetches `GET /toolbox-talks/sectors` for a
document/profile-attachment picker — that consumption pattern would be new,
though the endpoint itself is already live and already consumed elsewhere in
the app (e.g. `/admin/regulatory/my-sectors`, per
`docs/regulatory-architecture-recon.md:146`).

---

## 5. Frontend surface — SuperUser regulatory document view

**Files read in full:**
`web/src/app/(authenticated)/admin/regulatory/system/page.tsx` (list page)
and
`web/src/app/(authenticated)/admin/regulatory/system/[documentId]/page.tsx`
(detail page).

**List page** (`system/page.tsx`):
- Gated client-side by `useIsSuperUser()` (line 58, 65-71) — not gated at the layout/route level in this file itself.
- Renders two tables: "Regulatory Bodies" (lines 74-150, with an "Add Body" button opening `CreateRegulatoryBodyDialog`) and "Regulatory Documents" (lines 152-249, with an "Add Document" button opening `CreateRegulatoryDocumentDialog`).
- The documents table **does show a "Sectors" column** (header at line 182, cell rendering at lines 215-223): `doc.sectorKeys.map(key => <Badge>{key}</Badge>)` — i.e. a document's attached sectors (via its `RegulatoryProfile` rows, per `RegulatoryDocumentListDto.SectorKeys`, § below) are already visibly rendered on the list page today, just with no way to add to them.
- Each document row has one action: a "Manage" button linking to `/admin/regulatory/system/${doc.id}` (lines 230-234).

**Detail page** (`system/[documentId]/page.tsx`):
- Also gated client-side by `useIsSuperUser()` (lines 508, 572-578).
- Shows: document title (header), a 4-cell status grid (Status / Last Ingested / Draft count / Approved count, lines 611-639), an "Upload PDF" control (`RegulatoryDocumentUpload`, lines 644-653), a Source URL input + "Ingest Requirements"/"Retry Ingestion" button (lines 655-704), a failure-detail callout when `status === "Failed"` (lines 706-716), a Draft Requirements list with per-draft approve/edit/reject cards (lines 722-753), and an Accordion summarizing Approved/Rejected counts (lines 765-802).
- **This detail page has no sector or profile display or control anywhere** — confirmed by reading the full 806-line file: no reference to `sectorKeys`, `Profile`, or any sector-picker UI exists on the detail page. The only place sectors currently surface for a document is the read-only badge list on the **list** page (`system/page.tsx:217-221`), not the detail page.
- **Natural attach point:** the detail page's "Document Details & Ingestion" card (lines 597-720) is the only card on the page and structurally the obvious place a "Sectors / Profiles" section or an "Add Sector" control would be added, positioned before the "Draft Requirements" card so a sector/profile exists prior to ingestion — consistent with `RequirementIngestionJob`'s no-op-without-a-profile behavior (§7). This is a factual observation about where the existing card boundary falls, not a design proposal.

**Data flow confirming the "Sectors" column source:** `RegulatoryDocumentListDto.SectorKeys`
(`IngestionDtos.cs:229`) is populated in `GetDocumentsAsync`
(`RequirementIngestionService.cs:269`): `SectorKeys = doc.Profiles.Select(p => p.SectorKey).ToList()` — i.e. directly from the document's `RegulatoryProfile` rows (`Include(d => d.Profiles).ThenInclude(p => p.Sector)`, lines 237-239). A document with zero profiles (e.g. one just created via `CreateDocument`) renders an empty badge row in that column today, per `CreateDocumentAsync`'s explicit `SectorKeys = new List<string>()` (`RequirementIngestionService.cs:548`).

---

## 6. Duplicate/uniqueness edge case

**At the DB level:** inserting a second `RegulatoryProfile` row with the
same `{RegulatoryDocumentId, SectorId}` pair violates the unique index
`ix_regulatory_profiles_document_sector` (§1) and fails with a Postgres
unique-violation error (raised by EF Core as a `DbUpdateException` wrapping
a `Npgsql.PostgresException`, code `23505`) — there is no existing
try/catch anywhere in the current codebase that specifically catches this
for `RegulatoryProfile`, because nothing currently inserts one outside the
seeder (which pre-checks in application code, §2) and tests (which never
duplicate a pair).

**Soft-deleted-row collision:** the unique index has **no `filter:`
clause** (§1), so it applies across *all* rows in the table regardless of
`IsDeleted`. If a `RegulatoryProfile` for `{DocumentId, SectorId}` were ever
soft-deleted (`IsDeleted = true`) and a caller then tried to re-add a
profile for the identical pair, the insert would still violate the unique
index and fail — the soft-deleted row still occupies the constrained slot.
This is a real structural gap **if** a delete/deactivate path for
`RegulatoryProfile` is ever built, but as of today:

- **No code path soft-deletes a `RegulatoryProfile`.** `Grep` across `src/`
  for any assignment of `IsDeleted = true` scoped to this entity, or any
  delete endpoint referencing `RegulatoryProfile`, returns nothing — there
  is no delete/deactivate action for profiles anywhere in the current
  codebase (consistent with there being no create action either).
- The entity does carry a **separate, non-soft-delete deactivation flag**,
  `IsActive` (bool, default `true`), which **is** actively read: both
  `RequirementIngestionJob` (implied by "active profiles" language in its
  skip-reason string, `RequirementIngestionJob.cs:150`) and
  `RequirementMappingJob.cs:192,194` filter on `r.IsActive` /
  `r.RegulatoryProfile.IsActive` when selecting candidate requirements.
  `IsActive = false` would be the natural "soft off-switch" for a profile
  without touching `IsDeleted` at all, and would **not** collide with the
  unique index on a later re-activation, since it never removes the row
  from the constrained set in the first place — but this is a distinction
  worth being explicit about: `IsActive=false` and `IsDeleted=true` are two
  different fields with different query-filter/index implications on this
  entity, and only one of them (`IsDeleted`) participates in the EF
  `HasQueryFilter` (`RegulatoryProfileConfiguration.cs:81`).

**Net:** the only correctness edge that exists in the current schema is the
un-filtered unique index colliding with a hypothetical future soft-delete of
a profile. It has not been triggered by any code path or test to date.

---

## 7. Downstream effects of creating a profile

**Profile creation is not wired to trigger anything.** Confirmed by the
`IRequirementIngestionService.CreateDocumentAsync` XML doc
(`IRequirementIngestionService.cs:98-104`, quoted in §3) stating
"sector-profile setup remain[s a] separate, later action[]" from ingestion
— i.e. even in the current design intent, adding a profile is explicitly
meant to be inert with respect to the ingestion pipeline, a pure prerequisite
step.

**What profile *existence* gates, read-path only (no writes triggered):**
- `RequirementIngestionJob` (`RequirementIngestionJob.cs:146-153`) —
  invoked separately, per-document, via an explicit "Ingest Requirements"
  action (§5). It loads the document's `Profiles` and, if
  `extractedRequirements.Count > 0` but no active profile exists to attach
  them to, calls `MarkSkippedAsync(document, "no_active_profiles", ...)`
  (line 148) and returns without persisting anything (line 152). This is
  the exact behavior CLAUDE.md's `IngestionSessionDto` doc comment
  describes (`IngestionDtos.cs:27`: `"no_active_profiles"` as one of the
  possible `LastIngestionErrorCode` values). **A profile must exist before
  ingestion can succeed, but creating one does not itself start or affect
  any in-flight or already-completed ingestion** — ingestion is a wholly
  separate, explicitly-triggered action (the "Ingest Requirements" button,
  §5).
- If a document already has **approved** `RegulatoryRequirement` rows under
  *other* profiles, adding a brand-new profile to that same document has no
  retroactive effect on those existing requirements — each
  `RegulatoryRequirement` is scoped to one `RegulatoryProfileId`
  (`RegulatoryRequirement.RegulatoryProfileId`, confirmed via
  `RequirementIngestionService.GetDraftRequirementsAsync`,
  lines 92-95, which filters by `profileIds` derived from
  `RegulatoryProfiles.Where(p => p.RegulatoryDocumentId == regulatoryDocumentId)`
  — a newly added profile simply grows that `profileIds` list going
  forward for that document's *next* ingestion run; it does not touch
  requirements tied to profiles that already existed).
- `RequirementMappingJob` (`RequirementMappingJob.cs:190-201`) selects
  candidate requirements by joining through `RegulatoryProfile.SectorKey`
  against a **tenant's own** assigned sectors (`TenantSector`), entirely
  independent of when or how the profile was created. A new profile whose
  `SectorKey` matches a tenant's existing sector assignment would make that
  profile's (future) approved requirements eligible for that tenant's next
  mapping run — but this only matters once the profile has
  `Approved`-status requirements under it, which requires the separate
  ingestion + review steps to have happened first. Creating an empty
  profile (zero requirements) has no observable effect on any tenant's
  mapping job.

**Conclusion:** profile-creation is, and by explicit code-comment design is
intended to remain, a pure pre-ingestion setup step with no side effects of
its own. It only *unblocks* a subsequent, separately-triggered ingestion
run.

---

## 8. Auth — who can create/upload/ingest documents today

Confirmed directly from the controller source
(`RegulatoryIngestionController.cs:17`):

```csharp
[Authorize(Policy = "Tenant.Manage")]
```

This is a **class-level** gate applying to every action in the controller —
`GetDocuments`, `GetBodies`, `CreateBody`, `CreateDocument`,
`UploadSourceDocument`, `StartIngestion`, `GetIngestionStatus`,
`GetDraftRequirements`, `ApproveRequirement`, `RejectRequirement`,
`UpdateDraftRequirement`, `ApproveAllDrafts` — with no per-action override
to a different (e.g. lower-privilege) policy anywhere in the file. Per
CLAUDE.md's documented role matrix, `Tenant.Manage` is granted only to
`SuperUser` (Admin's permission set is "all permissions except
`Tenant.Manage`" — CLAUDE.md Roles table). This matches both the class
doc-comment ("SuperUser-only controller...", lines 11-14) and the frontend's
own `useIsSuperUser()` gate on both `/admin/regulatory/system` pages (§5).

**A create-profile action added to this same controller would inherit this
identical `Tenant.Manage` (SuperUser-only) gate automatically**, with no
additional `[Authorize]` attribute needed on the new action itself — this is
the same mechanism `CreateBody` and `CreateDocument` already rely on (they
carry no action-level `[Authorize]` of their own; the class-level policy is
sufficient). This is consistent with CLAUDE.md Note 24's warning about
class-level + action-level `[Authorize]` stacking (both must pass
independently) — here there is no competing action-level policy to
conflict with, so a new action simply inherits the class gate cleanly, with
no risk of the Note 24 trap.

---

## Non-scope confirmation

No implementation was written, no endpoint/DTO/UI was designed, no
`RegulatoryProfile` rows were created or modified, and the ingestion job /
extraction path / learning-generation pipeline were not touched — this
document is a facts-only inventory per the request.
