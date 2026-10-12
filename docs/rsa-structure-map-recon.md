# RSA 'no_structure_map' ingestion failure: recon

Read-only. No code changes. Document in Sentry: `8a9aac83-6756-48b2-b571-9da7b7690b24` 'Road Transport Regulations' (RSA).

## 1. Can a structure map be created other than by seed data?

**OBSERVED: No.**

- The only writer of `RegulatoryStructureMap` rows is `RegulatoryStructureMapSeedData.SeedAsync` (`Persistence/Seed/RegulatoryStructureMapSeedData.cs`), called from `Program.cs:678` at startup.
- `RegulatoryStructureMapVerificationService.VerifyAsync` is registered in DI (`ServiceCollectionExtensions.cs:359`). It has no caller outside DI and its own tests. No controller, job or UI invokes it. It only flips an existing map Draft to Verified, so it cannot create one.
- No controller exposes structure map create/read/update. `RegulatoryIngestionController` covers documents, ingest, draft requirements and approve/reject only.
- No draft-generation job exists. `RequirementIngestionJob` only reads maps (`TryGetStructureMapAsync`).
- `RegulatoryStructureMapProvider` is read-only (flat queries on `RegulatoryStructureMaps`).

**Consequence:** any non-HIQA document can never be ingested today. Even HIQA can't be moved to Verified without a manual DB edit or ad hoc code, because the verify service is unreachable.

## 2. How the HIQA map is seeded

**OBSERVED:**

- **Seed source:** static class `Services/Regulatory/StructureMaps/HiqaStructureMap.cs` (`HiqaStructureMap.Principles`, `RegulatoryBodyCode`). Its doc comment says it is a seed source only, not the runtime source of truth.
- **Seeder:** `RegulatoryStructureMapSeedData.SeedAsync`. It runs after `RegulatoryProfileSeedData` on every startup via `SeedToolboxTalksDataAsync`.
- **Binding:** by **body code**, not document ID or title. It finds the body with `Code == "HIQA"`, then takes `FirstOrDefault` of any non-deleted `RegulatoryDocument` for that body (no ordering, no title filter), and uses its Id as `RegulatoryDocumentId`. Writes `Status = Draft`, `CreatedBy = "system"`, with Principle, Standard and Feature children.
- **Idempotency:** if a non-deleted map already exists for that document, it logs and returns. It never overwrites human edits.
- **Adding a second seeded map needs:**
  1. A new static source class (e.g. `RsaStructureMap`) with authored content.
  2. Either generalise the seeder or add a second `SeedAsync` call in `Program.cs`.
  3. A deploy. The seeder runs on startup, so Production gets it on the next boot. No migration is needed because the tables exist (`20260731163851_AddDbBackedRegulatoryStructureMap`).
  4. The content itself, which means someone transcribing the RSA document's structure. That is the real work.

**Risk (INFERRED):**

- The HIQA seeder's `FirstOrDefault` is non-deterministic if HIQA ever has a second document. The map could bind to the wrong one.
- If the RSA seeder copied this pattern, it would bind to whichever RSA document sorts first. See section 4.

## 3. Extraction against a Draft (unverified) map

**OBSERVED: it proceeds, flagged provisional.** `RequirementIngestionJob.cs:139-148`:

- `isProvisional = !structureMap.IsVerified`.
- Logs a Warning.
- Every persisted requirement gets `IsProvisional = true` (line ~1051).

It refuses only when **no map row exists** (lines 125-137), which is the failure seen here. Since nothing can verify a map (section 1), all requirements in the system are currently provisional.

## 4. RSA RegulatoryDocument rows

**OBSERVED, local DB (`rascor_stock`):** one RSA row.

| Id | Title | Status | CreatedAt | SourceUrl |
|---|---|---|---|---|
| 84221a8c-eba1-4e82-9b36-07416945e94c | Road Transport Regulations | Idle | 2026-03-17 13:15:49Z | (empty) |

- No duplicates locally.
- Local Id differs from Production's `8a9aac83-...` because the seed uses `Guid.NewGuid()` per environment.
- The local DB has one structure map (HIQA doc `4354fb96-...`, Draft).

**INFERRED, duplicates on Production:**

- Unlikely from retries. The seed de-duplicates on `(BodyId, Title)`, and the ingestion job never creates documents (a retry re-runs against the same document Id).
- Possible only if an admin created a document by hand (`RegulatoryIngestionService` create path at ~line 517 takes a title and SourceUrl, with no uniqueness check seen).
- Note the stale-sweep and retry (`AutomaticRetry(Attempts=1)`) apply only to thrown exceptions. `no_structure_map` returns without throwing, so there is **no retry**, and one Sentry event per attempt is user-triggered.

**Production query (read-only):**

```sql
SELECT d."Id", d."Title", d."IsActive", d."IsDeleted",
       d."LastIngestionStatus", d."LastIngestionErrorCode",
       d."SourceUrl", d."CreatedAt", d."CreatedBy",
       (SELECT count(*) FROM toolbox_talks."RegulatoryStructureMaps" m
         WHERE m."RegulatoryDocumentId" = d."Id" AND NOT m."IsDeleted") AS map_count
FROM toolbox_talks."RegulatoryDocuments" d
JOIN toolbox_talks."RegulatoryBodies" b ON b."Id" = d."RegulatoryBodyId"
WHERE b."Code" = 'RSA'
ORDER BY d."CreatedAt";
```

Column and table names are taken from my local query, which ran fine. I haven't run this on Production.

## 5. Log level and distinguishability of `no_structure_map`

**OBSERVED:**

- **Level:** `MarkFailedAsync` (`RequirementIngestionJob.cs:390`) logs `LogError` for **every** failure code. That includes `invalid_uri`, `fetch_failed`, `parse_failed`, `extraction_*`, `no_structure_map` and `unknown`. `Program.cs:49` sets `Sentry MinimumEventLevel = Error`, so each one becomes a Sentry event.
- **Skipped** (e.g. `no_active_profiles`) uses `LogWarning`, so it does not reach Sentry. Only `Failed` does.
- **Distinguishability:**
  - The code is in the message text `[no_structure_map]` and persisted in `LastIngestionErrorCode`.
  - There is no structured property for Sentry grouping or filtering, because the code is embedded in the format string and not a tag.
  - The code comment at lines 344-348 classes `no_structure_map` among "expected, non-fatal" outcomes, yet it is logged identically to system faults.
- **UI:** the admin document page maps `LastIngestionErrorCode` to friendly text (`system/[documentId]/page.tsx:118`). I didn't verify whether it has a `no_structure_map` entry.

**Suggested follow-ups (not done):**

- Log expected user-actionable codes (`no_structure_map`, `invalid_uri`, `no_active_profiles`) at Warning.
- Reserve Error for genuine faults.
- Tag the error code as a structured property.

## Notable risks

1. Structure maps are seed-only and unverifiable via any UI or API. Non-HIQA ingestion is blocked and HIQA stays Draft forever.
2. RSA has no authored map content. Adding one is a content-authoring task as much as a code task.
3. The `FirstOrDefault` document binding in the HIQA seeder is fragile if a second document is added.
4. Expected failures page Sentry as errors, which adds noise and hides real faults.
