# Regulatory Architecture Recon — Tenant Scope and Multi-Document Capacity

**Date:** 2026-07-23
**Status:** Read-only recon. Facts only — no design proposed here.

**Trigger:** Customer feedback from a 2026-07-22 demo raised a gap: some tenants
follow multiple regulatory frameworks simultaneously (e.g. FSAI + BRC), and the
regulatory feature completed the day before (three sub-chunks: URI validation +
failure surfacing, file upload, create-document flow — commits `ce6c0b2`,
`c0d8828`, `de600e6`, all 2026-07-15) doesn't make clear how, or whether, this is
supported. This document establishes current state before any design work
begins.

---

## Part 1 — Tenant scoping audit

| Entity | Base type | Has `TenantId`? | Query-filter tenant scoping | Where the filter lives |
|---|---|---|---|---|
| `RegulatoryBody` | `BaseEntity` | No | None (soft-delete only) | `RegulatoryBodyConfiguration.cs:58` — `!e.IsDeleted` |
| `RegulatoryDocument` | `BaseEntity` | No | None (soft-delete only) | `RegulatoryDocumentConfiguration.cs:80` — `!e.IsDeleted` |
| `RegulatoryProfile` | `BaseEntity` | No | None (soft-delete only) | `RegulatoryProfileConfiguration.cs:81` — `!e.IsDeleted` |
| `RegulatoryCriteria` | `BaseEntity` + manual nullable `TenantId` | Yes, but nullable (`null` = system default, `Guid` = tenant override — SafetyGlossary pattern) | **None at the EF level.** Explicit code comment: *"tenant filtering handled at service level"* | `RegulatoryCriteriaConfiguration.cs:70-71`. No service in the ingestion/browse/mapping/sectors code paths currently queries this entity at all — it is defined but not yet wired into any read path. |
| `RegulatoryRequirement` | `BaseEntity` | No | None (soft-delete only). Tenant visibility is gated entirely by `IngestionStatus == Approved`, an application-level filter, not a query filter | `RegulatoryRequirementConfiguration.cs:98` |
| `RegulatoryRequirementMapping` | `TenantEntity` | **Yes, required** | **Yes — tenant + soft-delete**, centralized | Entity-config only sets `!IsDeleted` (`RegulatoryRequirementMappingConfiguration.cs:97`, comment: *"tenant filter applied in ApplicationDbContext"*). The tenant predicate is layered on afterward and wins: `ApplicationDbContext.cs:329` — `!e.IsDeleted && (BypassTenantFilter \|\| e.TenantId == TenantId)` |
| `Sector` | `BaseEntity` | No | None (soft-delete only) — system-wide lookup, same tier as `LookupCategory`/`Permission`/`Tenant` | `SectorConfiguration.cs:61` |
| `TenantSector` | `TenantEntity` | **Yes, required** | **Yes — tenant + soft-delete**, centralized only (no entity-config filter at all) | `ApplicationDbContext.cs:327` — `!e.IsDeleted && (BypassTenantFilter \|\| e.TenantId == TenantId)` |
| `ValidationRegulatoryScore` | `TenantEntity` | Yes, required | **Yes — tenant + soft-delete**, centralized only (no entity-config filter) | `ApplicationDbContext.cs:350` |

**Only two entities in the whole regulatory chain are genuinely tenant-scoped:
`RegulatoryRequirementMapping` and `TenantSector`** (plus `ValidationRegulatoryScore`,
which is adjacent — TransVal scoring, not ingestion/mapping). Everything upstream
of a mapping (`RegulatoryBody`, `RegulatoryDocument`, `RegulatoryProfile`,
`RegulatoryRequirement`) is application-wide: every tenant reads from the same
global rows, filtered down only by `IngestionStatus` and by the tenant's own
`Sector` assignments at query time — not by any tenant column on the source data
itself.

There is a codebase-wide pattern (documented in CLAUDE.md Note 14) of moving
tenant `HasQueryFilter` predicates out of individual `EntityConfiguration`
classes and into a single centralized block in `ApplicationDbContext.OnModelCreating`
(lines ~319-374), applied after all `ApplyConfiguration()` calls so the later
filter wins. `RegulatoryRequirementMapping`, `TenantSector`, and
`ValidationRegulatoryScore` follow this pattern correctly. `ApplicationDbContext.cs:355`
contains an explicit code comment enumerating the deliberately-not-tenant-scoped
`BaseEntity` set, which includes `RegulatoryBody, RegulatoryDocument,
RegulatoryProfile, RegulatoryCriteria, RegulatoryRequirement` by name — this is a
conscious architectural choice already recorded in the code, not an oversight.

### Consistency of tenant filtering in code (beyond the schema)

Traced every controller/service touching these entities:

- **`RegulatoryIngestionController`** (`/api/regulatory/*`, class-gated `Tenant.Manage` = SuperUser-only per the role matrix) — every action (`GetDocuments`, `GetBodies`, `CreateDocument`, `UploadSourceDocument`, `StartIngestion`, `GetIngestionStatus`, `GetDraftRequirements`, `ApproveRequirement`, `RejectRequirement`, `UpdateDraftRequirement`, `ApproveAllDrafts`) queries `RegulatoryDocument`/`RegulatoryBody`/`RegulatoryRequirement` with **no tenant predicate anywhere** — correct, since these entities carry no `TenantId` to filter by. Practical effect: **approving or rejecting a draft requirement is a global, tenant-blind operation** — `ApproveRequirementAsync` (`RequirementIngestionService.cs:122-153`) loads a `RegulatoryRequirement` purely by its own ID and flips it to `Approved`, at which point it becomes visible to **every** tenant whose `Sector` matches, via `RegulatoryBrowseController` and the mapping pipeline. Only SuperUser can do this (class-level `Tenant.Manage`), so there's no cross-tenant-admin leak today, but the entity itself has no concept of "approved for tenant X only."
- **`RegulatoryBrowseController`** (`/api/regulatory/browse`, class-gated `Learnings.Admin`) — `Browse()` correctly resolves `tenantId` server-side and filters `RegulatoryRequirement` down to `IngestionStatus == Approved && tenantSectorKeys.Contains(r.RegulatoryProfile.SectorKey)` (`RequirementIngestionService.cs:280-299`). One secondary endpoint, `GetApplicability(sectorKey)` (`RegulatoryBrowseController.cs:67-100`), takes a `sectorKey` param with **no check that the caller's tenant actually has that sector assigned** — it only returns counts/names against globally-Approved data, so it's a probe surface (any tenant admin can query counts for a sector their tenant doesn't have) but not a data leak of tenant-specific content.
- **`RequirementMappingController`** (`/api/toolbox-talks/requirement-mappings/*`, class-gated `Learnings.Admin`) — every action resolves `tenantId` from `ICurrentUserService` server-side (never client-supplied) and filters explicitly, e.g. `GetPendingMappingsAsync`: `Where(m => m.TenantId == tenantId)`; `GetComplianceChecklistAsync` additionally guards with `TenantSectors.AnyAsync(ts => ts.TenantId == tenantId && ...)` before proceeding, throwing `UnauthorizedAccessException` (→ 403) if the tenant doesn't have that sector. This is the one place in the whole flow with a genuine tenant-entitlement check against sector, and `RegulatoryRequirementMapping`'s own DbContext-level filter provides defense in depth on top.
- **`TenantSectorsController`** (`/api/tenants/{tenantId}/sectors`) — GET actions explicitly guard `if (!IsSuperUser && currentUserService.TenantId != tenantId) return Forbid()`; POST (assign) additionally allows `Learnings.Admin` on the caller's own tenant (per CLAUDE.md's documented "POST permission broadened" change); DELETE/PUT (`RemoveSector`, `SetDefault`) are gated to `Tenant.Manage` only (SuperUser), consistent with the documented model.

---

## Part 2 — RegulatoryDocument and RegulatoryBody structure

### RegulatoryBody
- File: `Domain/Entities/RegulatoryBody.cs`; config: `Infrastructure/Persistence/Configurations/RegulatoryBodyConfiguration.cs`
- Fields: `Name` (req, max 100), `Code` (req, max 20), `Country` (req, max 100), `Website` (optional, max 500)
- **Unique constraint:** `Code` is globally unique (`ix_regulatory_bodies_code`) — one row per code, system-wide, not per-tenant
- No check constraints

### RegulatoryDocument
- File: `Domain/Entities/RegulatoryDocument.cs`; config: `RegulatoryDocumentConfiguration.cs`
- Fields: `RegulatoryBodyId` (FK, req), `Title` (req, max 500), `Version` (req, max 50), `EffectiveDate` (optional), `Source`/`SourceUrl` (optional), `IsActive` (default true), plus ingestion-tracking fields added in yesterday's sub-chunk 1 (`LastIngestedAt`, `LastIngestionStatus` enum Idle/Ingesting/Success/Failed, `LastIngestionErrorMessage`, `LastIngestionErrorCode`)
- **No uniqueness constraint on `Title`/`Version`, or on the pair.** A body can have multiple documents, and nothing in the schema or the newly-added create endpoint (`de600e6`) prevents duplicate titles. The `de600e6` commit message explicitly notes this was a conscious choice: duplicate titles are allowed by design ("If duplicates prove confusing in practice, that's a separate design decision").
- Index: non-unique on `RegulatoryBodyId` only.

### RegulatoryBody creation path
- **No create/update/delete endpoint exists for `RegulatoryBody` anywhere in the codebase.** The only touch point is `GET /api/regulatory/bodies` (added in `de600e6`, yesterday), a read-only list used to populate the body picker in the new "create document" dialog.
- The only place a `RegulatoryBody` row is ever constructed is `RegulatoryProfileSeedData.cs:37-45`, inside an idempotent seeder invoked unconditionally at every app startup (`Program.cs:506`, not gated by environment — this is "system data," per CLAUDE.md Note 31, distinct from the dev/demo-only credentialed-account seeding).
- **The four seeded bodies are the only ones that exist today, confirmed both in code and in the live Development database:**

  | Code | Name | Country |
  |---|---|---|
  | HIQA | Health Information and Quality Authority | Ireland |
  | HSA | Health and Safety Authority | Ireland |
  | FSAI | Food Safety Authority of Ireland | Ireland |
  | RSA | Road Safety Authority | Ireland |

- **This is a hard limit only for the body itself, not for documents.** Adding a 5th body (e.g. "BRC") today requires a DB-direct insert or a seed-data change — there is genuinely no admin UI or API to create one. But the `de600e6` sub-chunk already added a working **document**-creation flow (`POST /api/regulatory/documents`) that operates generically against whichever bodies exist — `CreateDocumentAsync` looks up `RegulatoryBodyId` by ID with no hardcoded set, and the frontend body picker (`create-regulatory-document-dialog.tsx`) is populated dynamically from `GET /bodies`, not hardcoded to 4 options. So: a second FSAI-adjacent document, or even a second document against an existing body, is already fully supported through UI today. A second *body* (e.g. BRC as a new regulator) is not.
- A document created via this new flow starts with **zero `RegulatoryProfile` rows** attached (`CreateDocumentAsync` returns `SectorKeys = []`) — there is no UI/API path to attach a sector to a new document (create a `RegulatoryProfile`). That step exists only via direct seed data. `RequirementIngestionJob` explicitly checks for this and no-ops with a warning log if `document.Profiles.Count == 0` — so a document created through the new UI cannot productively ingest anything until a profile is added out-of-band.

### RegulatoryProfile (the document × sector join)
- Unique constraint: `{RegulatoryDocumentId, SectorId}` (`ix_regulatory_profiles_document_sector`) — **this is the actual cardinality-defining constraint.** It permits one document to have profiles across many sectors, and (schema-wise) permits one sector to appear in profiles from multiple different documents/bodies. Nothing in the schema forces "one profile per sector" globally.
- **Live proof this is already many-to-many in the "one document → many sectors" direction:** seed data attaches HIQA's single document to **two** sectors (`homecare`, `healthcare`), and HSA's single document to **two** sectors (`construction`, `manufacturing`). Confirmed both in seed code and in the live Development DB query below.
- **Not yet realized in the "one sector → many documents/bodies" direction** — every seeded sector currently maps to exactly one body. But nothing structurally prevents it; see Part 5.

Live Development DB query results (2026-07-23):

| Body | Document | SectorKey |
|---|---|---|
| HSA | Safety, Health and Welfare at Work Regulations | construction |
| FSAI | Food Safety Authority of Ireland Regulations | food_hospitality |
| RSA | Road Transport Regulations | transport |
| HIQA | Draft National Standards for Home Support Services | homecare |
| HSA | Safety, Health and Welfare at Work Regulations | manufacturing |
| HIQA | Draft National Standards for Home Support Services | healthcare |

---

## Part 3 — Requirement and Mapping flow

### RegulatoryRequirement
- File: `Domain/Entities/RegulatoryRequirement.cs`; config: `RegulatoryRequirementConfiguration.cs`
- Fields: `RegulatoryProfileId` (FK, req), `Title`, `Description`, `Section`/`SectionLabel`, `Principle`/`PrincipleLabel`, `Priority`, `DisplayOrder`, `IngestionSource` (Manual/Automated), `IngestionStatus` (Draft/Approved/Rejected), `IngestionNotes`, `IsActive`
- No `TenantId` — system-wide, gates tenant visibility purely via `IngestionStatus == Approved`
- Indexes: non-unique on `RegulatoryProfileId`, non-unique on `IngestionStatus`. **No unique constraint on title, or on any combination.**

### Ingestion job (`RequirementIngestionJob`, Infrastructure/Jobs/)
- Invoked with a single `regulatoryDocumentId`. Loads that document's active `Profiles` and calls the persistence routine **once per profile** — i.e. requirements extracted from one document PDF get cloned into every active sector-profile attached to that document.
- **Dedup check exists, but is narrowly scoped:** it queries existing `RegulatoryRequirement.Title` values filtered to `RegulatoryProfileId == profile.Id` only, and skips an extracted requirement if its lowercased title exact-matches one already present **for that same profile**. This only prevents re-ingesting the *same* document into the *same* profile twice — it has no visibility into requirements belonging to a different profile (i.e. a different document/body), even if that profile targets the identical sector.
- There is no unique DB constraint backing this dedup — it's purely an application-level, exact-string check inside the job.

### RequirementMappingJob (Infrastructure/Jobs/)
- Candidate requirement selection is scoped by the **tenant's assigned sectors**, not by document or body: it resolves the tenant's `TenantSector` sector keys, finds all active `RegulatoryProfile` rows matching those keys (`Where(sectorKeys.Contains(p.SectorKey))`) **regardless of which document/body each profile belongs to**, then loads all `Approved` requirements under those profiles into one flat candidate list sent to Claude for mapping. The AI prompt has no per-source grouping or body/document labeling beyond section/principle text.
- Persistence dedup/restore is keyed on `(TenantId, RequirementId, ToolboxTalkId or CourseId)` — per requirement, not per topic or source document. Two different `RegulatoryRequirementId`s from two different documents (even if semantically identical, e.g. both "hand hygiene") are entirely independent keys with no cross-reference or merge logic.

### RegulatoryRequirementMapping unique constraints
- `{TenantId, RegulatoryRequirementId, ToolboxTalkId}` unique (filtered, TalkId not null)
- `{TenantId, RegulatoryRequirementId, CourseId}` unique (filtered, CourseId not null)
- Check constraint enforcing exactly one of Talk/Course
- **The constraint is scoped on `RequirementId`, not on `ToolboxTalkId` alone** — it prevents the same requirement mapping twice to the same talk, but does **not** prevent two different requirements (sourced from two different documents/bodies) both mapping to the same talk. Both rows would insert successfully, independently.

### Compliance checklist read path (`RequirementMappingService.GetComplianceChecklistAsync`)
- Pulls requirements purely by `sectorKey` — **no filter on document or body** — so if a sector ever has profiles from two different documents/bodies, their approved requirements are already combined into one flat list today, grouped only by `Principle`/`PrincipleLabel`.
- However, the DTO's single `RegulatoryBody`/`ScoreLabel` header field is populated from **only the first requirement in sort order** (`requirements[0].RegulatoryProfile.RegulatoryDocument.RegulatoryBody.Name`) — an arbitrary pick. With two bodies contributing to one sector's checklist, the header would show one body's name while the requirement list underneath silently contains items from both.

### End-to-end flow trace
Admin uploads document → `RequirementIngestionJob` runs, requirements land as `Draft` under specific profile(s) → SuperUser (only `Tenant.Manage` holders) reviews and approves/rejects, globally, with no tenant dimension → `RequirementMappingJob` (per-tenant, triggered on publish) pulls **all** approved requirements across **all** profiles matching the tenant's sectors, regardless of source document, and asks Claude to propose mappings to that tenant's published talks/courses → tenant's `Learnings.Admin` reviews/confirms suggested mappings via `/requirement-mappings/pending` → confirmed mappings feed the compliance checklist (`/compliance/{sectorKey}`) and inspection-readiness report.

**Assumption check:** every step from `RequirementMappingJob` onward already treats "requirements for this sector" as a set that could span multiple documents/bodies — it does not assume single-source input. The one step that does assume single-source is the **ingestion dedup check**, which only guards against re-ingesting the same document into the same profile, not against a second document producing overlapping content for the same sector.

---

## Part 4 — Sector / category role

- **Sector** (`Domain/Entities/Sector.cs`) is a first-class, system-wide lookup: `Key` (globally unique), `Name`, `Icon`, `DisplayOrder`, `IsActive`. Not tenant-scoped.
- **TenantSector** is the tenant-scoped junction: `{TenantId, SectorId}` unique, `IsDefault` flag. **A tenant can already have multiple sectors simultaneously** — this is an existing many-to-many relationship, not something that needs to be built.
- **`/admin/regulatory/my-sectors`** is exactly this mechanism surfaced in the UI: tenant admins add sectors from the full system list (`GET /toolbox-talks/sectors`) via `POST /tenants/{tenantId}/sectors`. It is explicitly **add-only** — the page renders "Sectors can be added but not removed from this page. To remove a sector, contact support," matching the documented product decision (removal requires SuperUser).
- **`/admin/regulatory/regulations` (browse)** filters strictly by the tenant's assigned `Sector` keys, joined through `RegulatoryProfile.SectorKey`. A tenant with no sectors assigned sees nothing.
- **The tenant admin never selects a regulatory body or document directly, anywhere in the UI.** The `/admin/regulatory` landing page's tenant-facing cards are Regulations / Compliance / Mappings / My Sectors — body/document identity (e.g. "FSAI") only appears as a read-only display label inside already-sector-filtered results, never as a selectable filter. Body/document selection only exists on the SuperUser-only `/admin/regulatory/system` pages, where a document is created against one body with **no sector selector at all** — sector attachment (creating a `RegulatoryProfile`) is not exposed in any UI, SuperUser or tenant.
- **Sector ↔ RegulatoryDocument is confirmed many-to-many by schema and by live seed data**, not one-to-one: HIQA's single document already has two `RegulatoryProfile` rows (homecare, healthcare); HSA's single document already has two (construction, manufacturing). The reverse direction — two different bodies both having a profile for the same sector — is architecturally unblocked (the unique index is on `{DocumentId, SectorId}`, not `SectorId` alone; `GetApplicability` and `GetBrowsableRequirementsAsync` both query/group as if multiple bodies could appear under one sector) but is **not realized in any current data** — every sector today resolves to exactly one body.

**This directly answers the framing question in the trigger:** Sector is the axis a tenant actually interacts with ("what industry am I in"), and it is completely orthogonal to "which regulatory body/document applies" from the tenant's point of view. A tenant already effectively gets multiple frameworks today if it has multiple sectors that each happen to map to a different body (e.g. a homecare+construction tenant already gets both HIQA and HSA content via two separate `TenantSector` rows) — the schema already supports this. What is **not** supported by any existing UI is a *single* sector resolving to *two* bodies (e.g. a food_hospitality tenant needing both FSAI and BRC) — the data model would tolerate it (profiles are keyed per document×sector, so a second profile for `food_hospitality` against a hypothetical BRC document is structurally legal) but nothing exists today to create that second profile, and the downstream code that would then combine the two bodies' data (compliance checklist header, ingestion dedup, prompt engineering) has gaps described in Parts 3 and 5.

---

## Part 5 — Predicted naive multi-document impact

Scenario: a tenant admin (or SuperUser, since document/body creation is SuperUser-only) uploads both an FSAI document and a BRC document, both get ingested, both produce requirements, both produce mappings that overlap on the same toolbox talk. This is a code trace / prediction, not a test run.

1. **Would the ingestion job even complete for two documents?**
   Yes. `RequirementIngestionJob` is invoked per-document with no awareness of any other document's state, and nothing in `RequirementIngestionService` references sibling documents. Both ingestions would run and complete independently without error — **conditional on a `RegulatoryProfile` for BRC × food_hospitality being created first**, since no UI exists to do that (Part 2) and the job explicitly no-ops if a document has zero active profiles.

2. **Would requirements be duplicated in the DB?**
   Yes, functionally — not as literal duplicate rows of the same entity, but as **separate, uncollapsed `RegulatoryRequirement` rows covering the same real-world obligation.** The only dedup check (title-exact-match) is scoped per `RegulatoryProfileId`, so BRC's profile has an empty comparison set relative to FSAI's existing titles. A "hand hygiene" requirement from FSAI and a "Hand Hygiene" (or differently worded) requirement from BRC would both persist as independent rows, both eventually `Approved`, both visible together in every read path that queries by `sectorKey` alone (browse, compliance checklist).

3. **Would mappings conflict or overwrite?**
   No conflict, no overwrite — but also no merge. The unique constraints on `RegulatoryRequirementMapping` are keyed by `RequirementId`, so FSAI's requirement and BRC's requirement, both mapping to the same `ToolboxTalkId`, insert as two entirely independent rows. Nothing errors.

4. **Would the tenant's admin see a coherent picture, or confusing overlapping data?**
   Confusing, specifically in two places already identified in Part 3:
   - The **compliance checklist** would show what looks like duplicate/redundant line items (two "hand hygiene" requirements, differently worded, both listed under whatever `Principle` grouping each document's AI extraction happened to produce) with a single header naming only **one** body/score-label (whichever requirement happened to sort first) — misleadingly implying the entire checklist is scored against just that one framework.
   - The **requirement-mappings review screen** (`/pending`) would show two suggested mappings to the same talk for what a human reviewer would recognize as the same underlying obligation, with no indication they're duplicative — a tenant admin would need to manually notice and reconcile this.
   - The **AI ingestion dedup** would not have prevented the duplicate content from being created in the first place, since it never compares across documents.

Net: **the code runs without throwing, but produces a duplicated, unlabeled, and partially misattributed compliance picture** — exactly the "runs fine but confusing outcome" pattern flagged as a risk to distinguish going into this recon.

---

## Part 6 — Current usage patterns (live Development DB, 2026-07-23)

Queried directly against the Development Postgres instance (`rascor_stock`):

- **RegulatoryBodies:** exactly the 4 seeded rows (RSA, HIQA, FSAI, HSA) — no others exist.
- **RegulatoryDocuments:** exactly 4 rows, one per body, all `IsActive = true`, all `LastIngestionStatus = Idle` (none has actually been run through the new ingestion-status tracking added yesterday).
- **RegulatoryProfiles:** exactly the 6 seeded rows described in Part 2/4 — no additional profiles have been created by any tenant or admin action since seeding.
- **RegulatoryRequirements:** only 15 rows exist in total, all under the `homecare` profile, all `IngestionStatus = Approved` (matches CLAUDE.md Note 35's "seeded with 15 HIQA homecare requirements" — i.e. these are seed data, not admin-ingested content). Every other profile (construction, manufacturing, food_hospitality, transport, healthcare) has **zero** requirements.
- **TenantSectors:** only 6 tenants have any sector assignment at all, and all 6 are Playwright E2E test tenants (`E2E Tenant <timestamp>`), every one assigned to exactly `construction` as its default sector. **Zero tenants have more than one sector assigned.**
- **RegulatoryRequirementMappings:** **zero rows exist** — no mapping has ever been created (suggested, confirmed, or otherwise) in this Development database.

**Conclusion: there is no current data indicating multi-document or multi-sector usage today.** No tenant has ever had more than one sector, no requirement mapping has ever been created, and no admin has ever ingested a document beyond the original seed data. This is confirmed to be a forward-looking design question raised by customer feedback, not a case of existing data being broken or already in a confusing multi-document state.

---

## Additional finding: hardcoded body/sector assumptions elsewhere in the code

Grepped the full codebase for the literal body codes (HIQA, HSA, FSAI, RSA). Ingestion, mapping, and scoring services (`RequirementIngestionJob`, `RequirementMappingJob`, `RegulatoryScoreService`) are **fully data-driven** — no branch anywhere keys off a specific body code.

**One exception exists:** `TranslationPrompts.GetSectorInstructions(sectorKey, ...)`
(`Application/Prompts/TranslationPrompts.cs:222-264`) is a hardcoded `switch` on
sector key (`homecare`/`healthcare` → HIQA-flavored prompt text; `construction`/
`manufacturing` → HSA-flavored; `food_hospitality` → FSAI-flavored; `transport` →
RSA-flavored), each branch baking in body-specific compliance terminology
(safeguarding/DLP/PIC for HIQA, PSDP/PSCS for HSA, HACCP/allergens for FSAI,
tachograph/driver-hours for RSA) directly into the AI translation prompt.
Unmatched sector keys fall through to `null` (no sector-specific instructions —
not an error). **This switch branches on sector, not body directly, but it
implicitly assumes one sector maps to one body's terminology** — it has no
mechanism to blend or select between two bodies' terminology if a sector ever
resolves to more than one.

Also confirmed: `RegulatoryProfileSeedData.cs` is the only place any of the four
codes are used as data (not logic), and yesterday's `de600e6` sub-chunk's new
`GetRegulatoryBodiesAsync`/`CreateDocumentAsync` code is fully generic — it does
not hardcode "4 bodies" anywhere; a 5th seeded body would surface automatically.

---

## Summary — surface area affected by supporting multiple frameworks per tenant

Facts-only inventory of what touches this question, not a design:

1. **`RegulatoryBody` creation** — currently zero UI/API path exists; DB-direct or seed-only. Would need to be addressed if "add BRC" is meant to be admin-driven rather than an engineering task.
2. **`RegulatoryProfile` creation** (attaching a sector to a document) — currently zero UI/API path exists on either the SuperUser or tenant side, even though the schema and unique constraint (`{DocumentId, SectorId}`) already support a sector having profiles from more than one document. This is the most direct gap for "one sector, two frameworks."
3. **Ingestion dedup** (`RequirementIngestionJob`) — scoped per-profile only; has no visibility across documents/profiles targeting the same sector, so a second framework's ingestion would not detect overlap with the first.
4. **Compliance checklist header** (`RequirementMappingService.GetComplianceChecklistAsync`) — picks a single `RegulatoryBody`/`ScoreLabel` from the first sorted requirement; would misrepresent a sector backed by two bodies.
5. **Requirement-mapping review UI** — no grouping or provenance indicator by source document/body; a reviewer has no way to see which framework a suggested mapping came from.
6. **AI prompt engineering** (`TranslationPrompts.GetSectorInstructions`) — hardcoded per-sector body terminology; assumes one body per sector.
7. **`RegulatoryRequirementMapping` uniqueness** — already keyed per-requirement, so this specific constraint would not need to change to support multiple frameworks (it already allows independent mappings from different sources to coexist); the gap is presentation/reconciliation, not the constraint itself.
8. **`TenantSector` many-to-many** — already fully supports a tenant having multiple sectors today; not a gap. The open question is specifically "one sector needs two frameworks" vs. "assign the tenant a second, framework-specific sector," which are different shapes of the same underlying need and were not evaluated for suitability here (that's design, out of scope for this recon).

No fix prompts, no design decisions are proposed above — this is the inventory requested for a follow-on design discussion.
