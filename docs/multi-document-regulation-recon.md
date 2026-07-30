# Multi-Document Regulation Support — Recon

**Date:** 2026-07-30
**Purpose:** Read-only recon into whether the regulatory ingestion/compliance stack can handle a regulation represented by a *document set* (main regulation + SOPs + matrix/relationship docs) rather than a single PDF, ahead of the homecare tenant onboarding next week. No code changed.

**Headline finding:** The backend data model already supports multiple documents per regulatory body with no schema change required — this was true before this week and remains true after the Kind/Standards work that shipped 2026-07-23/24. The actual gaps are (1) no bulk/multi-file admin upload UX, (2) the tenant-facing compliance checklist collapses document identity that the browse page already preserves, (3) dedup is local to a single document and does nothing across documents, and (4) there is no cost gate on ingesting many documents back-to-back. None of these require a schema migration. This is smaller than "add ZIP upload" sounds, and also not *just* "add ZIP upload."

---

## 1. Current data model capability

**A RegulatoryBody can already have multiple RegulatoryDocument rows — nothing prevents it.**

- `RegulatoryDocument.RegulatoryBodyId` is a plain required FK (`RegulatoryDocument.cs:12`). The only index on it is non-unique, lookup-only: `ix_regulatory_documents_body` (`RegulatoryDocumentConfiguration.cs:76-77`).
- `RegulatoryBody.Documents` is declared as a plain one-to-many collection (`RegulatoryBody.cs:39`, `RegulatoryBodyConfiguration.cs:60-63`) with no cardinality cap. The only unique constraint on `RegulatoryBodies` is on `Code` (`RegulatoryBodyConfiguration.cs:72-74`).
- `RegulatoryProfile` is keyed on **(RegulatoryDocumentId, SectorId)**, not (Body, Sector) — `ix_regulatory_profiles_document_sector`, unique (`RegulatoryProfileConfiguration.cs:73-75`). Since `RegulatoryRequirement.RegulatoryProfileId` is the only FK a requirement carries (`RegulatoryRequirement.cs:13`), the full chain **Body → Document → Profile → Requirement already threads through a specific document**. Requirements are not merged into an undifferentiated body-level bucket at the data layer — document-level provenance exists structurally today, it's just not surfaced everywhere (see §4).
- Admin UI already tolerates multiple documents per body without breaking: `admin/regulatory/system/page.tsx:175-249` renders a flat documents table where each row independently carries `regulatoryBodyCode`/`regulatoryBodyName` — nothing assumes one row per body. `CreateRegulatoryDocumentRequest` (`IngestionDtos.cs:202-208`) takes only a `RegulatoryBodyId`; nothing blocks creating a second document against an existing body. There's just no dedicated "documents under this body" grouped view — the flat table is the entire UI for it.

**This week's Standards/Kind work is orthogonal and did not touch document cardinality.** Commit `eee9562` and its follow-ons (`de600e6`, `c0d8828`, `7a49e37`, `81e30b4`, `a25d5dd`, `ed4916b`, `f37add8`, `be996fe`, all 2026-07-23/24) added:
- `RegulatoryBody.Kind` (`RegulatoryBodyKind`: `Regulation` = legally mandated, applies via sector automatically; `Standard` = voluntary, tenant subscribes — `RegulatoryBody.cs:23`, `Enums/RegulatoryBodyKind.cs:6-17`).
- `RegulatoryBody.SectorId`, required for Standard-kind bodies, forbidden for Regulation-kind (`ValidateSectorConsistency`, `RegulatoryBody.cs:45-52`).
- A new `TenantStandardSubscription` entity/table (composite unique `{TenantId, RegulatoryBodyId}`) so tenants can opt into voluntary Standard bodies (`TenantStandardSubscription.cs`).

None of this changed `RegulatoryDocument`, its FK, or its indexes. It operates one level up, at the body-kind/subscription level, and is a genuinely separate axis from "how many documents does one body have."

**`ApplicableFrameworksService` already unions across an arbitrary number of bodies.** `GetApplicableFrameworksAsync` (`ApplicableFrameworksService.cs:29-105`) queries all Approved, active `RegulatoryRequirement` rows where the body `Kind == Regulation` and the profile's sector matches the tenant's sectors (lines 37-42), grouped by body — and separately unions in every Standard-kind body the tenant is subscribed to (lines 60-101), including subscribed bodies with zero requirements so far. This already does exactly what BACKLOG §42 asks for at the aggregation layer: "N regulation bodies + N standard bodies, all flowing to the tenant automatically by sector/subscription" is live code today, not a gap.

**Verbatim client ask (BACKLOG.md:2792-2798, §42, P1, Open):**
> "There is a new legal regulatory requirement from the Irish Government in the homecare sector, which means there are now two regulatory documents that homecare providers must adhere to from HIQA and Legal. The system currently allows for a Regulatory document at the application level and Standards documents at the tenant level. We need to ensure we can upload multiple Regulatory documents at the application level and multiple Standards documents at the tenant level, to cater for these new Regulatory changes."

Read literally, §42 is about **document/body count**, not concatenation — and per the above, the schema and aggregation layer already support it. `client-eval-triage.md` (written earlier today, independently) reached the same conclusion and flagged §42 as "may already be moot — verify against the shipped feature" (docs/client-eval-triage.md:120). This recon confirms that suspicion at the data-model level. The open question is entirely in ingestion UX, dedup, and display — Parts 2–5 below.

---

## 2. Ingestion pipeline behaviour

`RequirementIngestionJob.ExecuteAsync(Guid regulatoryDocumentId, ...)` runs **per RegulatoryDocument**, one Hangfire job per document (`[AutomaticRetry(Attempts = 1)]`, queue `content-generation`), enqueued by `RequirementIngestionService.StartIngestionAsync` (`RequirementIngestionService.cs:69-70`).

- **Extraction:** a single, non-chunked Claude Sonnet call over the entire extracted PDF text — no page/size cap (`PdfExtractionService.ExtractTextAsync`, `PdfExtractionService.cs:26-116`; prompt built at `RequirementIngestionJob.cs:317-347`; `MaxTokens = 8192`). One retry with a stricter prompt if the response isn't valid JSON (job.cs:296-314) — worst case 2 full-document calls per document.
- **Attribution:** requirements are persisted once per active `RegulatoryProfile` of the document (job.cs:135-140), each stamped with `RegulatoryProfileId` (job.cs:454). There is **no direct `RegulatoryDocumentId` on `RegulatoryRequirement`** — document attribution is only reachable by joining through `Profile → Document`.
- **Dedup: exact-title-match, scoped to the single profile being ingested, nothing broader.** `PersistDraftRequirementsAsync` loads all existing titles (including soft-deleted, via `IgnoreQueryFilters()`) for that one `profile.Id` (job.cs:429-433) and skips any new extraction with a matching lowercased title (job.cs:446-450). No search hit anywhere in this pipeline for merge/semantic-similarity logic.
- **Consequence for a multi-document body:** ingesting Document A then Document B under the same body produces two independent `RegulatoryProfile` rows (unique per `{DocumentId, SectorId}`), so **the title-dedup check never runs across A and B at all** — even if both describe the same underlying obligation in slightly different words, both sets of requirements land, untouched by any cross-document check. Nothing in the schema blocks or collides on this either (`RegulatoryRequirement`'s only relevant index is `{RegulatoryProfileId, IngestionStatus}` — `RegulatoryRequirementConfiguration.cs:91-95`).
- **No cost/concurrency gate.** No `[DisableConcurrentExecution]`, no lock, no pre-run cost estimate. `StartIngestionAsync` doesn't even check `LastIngestionStatus` before enqueueing, so re-triggering mid-run is possible. Contrast with the Corpus-run feature (CLAUDE.md Note 8), which has exactly this problem solved already: `CostEstimationService` + a two-step estimate → confirm (>€3) → SuperUser approval (>€10) gate, plus a 10-minute cooldown. Regulatory ingestion has no analogue — cost scales linearly with document count and nobody sees the number before it's spent.

---

## 3. Current upload flow

Regulatory document ingestion is **URL-fetch-based, not direct-file-to-LLM**, via three separate endpoints on `RegulatoryIngestionController` (class-gated `Tenant.Manage`):

| Step | Endpoint | What it does |
|---|---|---|
| 1 | `POST /api/regulatory/documents` | Creates a `RegulatoryDocument` row (metadata only — title, version, effective date, body, optional SourceUrl). No file. |
| 2 | `POST /api/regulatory/documents/{id}/upload` | Accepts one `IFormFile` (PDF only, `[RequestSizeLimit(52428800)]` = 50MB, matches the ToolboxTalks PDF limit), uploads to R2, sets `RegulatoryDocument.SourceUrl`. Does **not** trigger ingestion. |
| 3 | `POST /api/regulatory/documents/{id}/ingest` | Takes a `SourceUrl` (defaults to the document's stored one), validates it, enqueues `RequirementIngestionJob` via Hangfire. Returns immediately. |

No page-count limit anywhere. Ingestion always resolves its input by **fetching a URL** — step 2 is a convenience that hosts the PDF on R2 first so step 3 has something to fetch; raw file bytes never reach the ingestion job directly.

**Today, onboarding N documents means N × (create + upload + ingest) = 3N manual admin actions, with zero batching.** There is no multi-file/`IFormFile[]` endpoint anywhere in the codebase for this flow, and no grouped "documents for this body" creation UI — each document is created, uploaded, and ingested as an independent, unrelated admin action.

**No ZIP handling for import exists anywhere in the codebase.** The only `ZipArchive` usage found repo-wide is `ScormPackageService.cs:231` (`ZipArchiveMode.Create`) — building a downloadable SCORM export package, an **output** path, unrelated to ingestion.

**The closest existing "bulk" reference pattern is CSV-based, not document-based:** `BulkEmployeeImportController` + `BulkEmployeeImportJob` — single file upload → synchronous validation in-request → `BulkImportSession` row → separate `/confirm` enqueues the actual Hangfire processing → polled `GET /{id}` for status. CLAUDE.md's own backlog already names this the reference implementation for a future "Bulk SOP import" feature (docs reference: BACKLOG "Bulk SOP import — allow admins to upload multiple SOPs as background batch job with notification," listed High priority).

---

## 4. Compliance display implications

**Two different endpoints exist, and they disagree on whether document identity survives.**

- **Regulations browse** (`GET /api/regulatory/browse`, `RegulatoryBrowseController`) → `RequirementIngestionService.GetBrowsableRequirementsAsync` (`RequirementIngestionService.cs:279-359`) **already builds a full Body → Document → PrincipleGroup → Requirement hierarchy** (comment at line 313, nested `GroupBy`s at 315/324/335). The frontend (`admin/regulatory/regulations/page.tsx:150-167`) renders one card per body, with an explicit `DocumentSection` per document inside it (title, version, its own accordion) — full per-document visual separation already shipped, for this one page.
- **Compliance checklist** (`GET /api/toolbox-talks/requirement-mappings/compliance/{sectorKey}`) → `RequirementMappingService.GetComplianceChecklistAsync` (`RequirementMappingService.cs:159-399) groups **only by Principle** (lines 358-369). The body-level header field on the response is a comma-joined, deduplicated, alphabetized string of body *names* (lines 376-383) — it actively collapses "how many documents contributed" into a single label. Within a principle group, requirements from different documents (even different bodies) are interleaved with no separator. Neither `ComplianceRequirementDto` nor `PendingMappingDto` carries a document field at all (`MappingDtos.cs:72-86`, `:6-28`) — only `SourceBodyName`/`SourceBodyKind`.

**Net effect for the actual scenario this recon is about:** if HIQA homecare is represented by 8 (or 24) documents, the read-only browse page would show them cleanly separated. The **compliance checklist — the page a compliance officer would actually use to show an inspector "here's what we cover"** — would show one flat, principle-grouped list with no way to tell which of the 8/24 source documents any given requirement came from, and no way to tell "these two look-alike rows are legitimately two different documents' obligations" from "this is an accidental duplicate." This is precisely the "confusing duplicate output" risk named in the task's design goal, and it's a real, already-reachable gap — not a hypothetical.

**The natural admin need (§10 in the prompt) — one grouped "HIQA regulations" view with document drill-in available — already exists as a UI *pattern*, just on the wrong page.** Porting the browse page's Body→Document grouping (or a lighter version of it) onto the compliance checklist is the fix, not a new concept.

---

## 5. Ingestion cost and quality implications

- **Cost scales linearly, uncapped, per document**, per §2 above — no chunking limit, no size/page ceiling before the full text goes to Claude Sonnet.
- **No rate-limit collision found** — the standard `GetClaudePolicy` Polly retry (2s/4s/8s backoff on 429/5xx) applies at the HttpClient level regardless of call volume, so transient throttling is handled the same way it is everywhere else in the codebase. Nothing suggests 24 back-to-back ingestion jobs would hard-fail on rate limits.
- **What's actually missing is cost *visibility and gating*, not cost *survivability*.** The Corpus-run feature already solved this exact problem (estimate → confirm >€3 → SuperUser approval >€10, per CLAUDE.md Note 8) for a structurally similar "many small AI calls in one batch" scenario. Regulatory ingestion has no equivalent — an admin kicking off ingestion on a 20-document real-world set (vs. this recon's 24-document *synthetic* set, see §8) would have zero pre-flight cost estimate and zero approval gate.
- **Duplicate/contradictory content across documents:** current behaviour is "present both, ingestion doesn't notice." There is no dedup option currently implemented beyond same-profile exact-title matching (§2). Of the three options named in the task brief — dedup-during-ingestion (risky), present-both-let-admin-decide (current de facto behaviour, but silent/unlabeled), post-process semantic merge (not built) — **today's actual behaviour is closest to "present both," except it isn't even flagged as a possible duplicate to the admin.** The requirement mapping review UI (`Suggested/Confirmed/Rejected` workflow, `RequirementMappingController`) is the one place a human reviews AI output before it's tenant-visible, but nothing in that review surfaces "this looks similar to a requirement from a sibling document" — that comparison isn't computed anywhere.

---

## 6. ZIP handling status

**None exists for import.** Confirmed by full-repo grep (`System.IO.Compression`, `ZipArchive`, `ZipFile`, `.zip`, `IFormFile[]`/`List<IFormFile>`) — the only hit is the SCORM **export** writer noted in §3. This is net-new work if pursued. Shape, if built: a controller action accepting either a `.zip` `IFormFile` (unzip server-side via `System.IO.Compression.ZipArchive`, same library the SCORM code already depends on so no new package) or a true multi-file `IFormFile[]` (skips the archive step entirely, arguably simpler and matches how a browser folder-picker/multi-select behaves) feeding into a validate → confirm → background-job → poll flow modeled on `BulkEmployeeImportJob`.

---

## 7. Recommended interpretation of the customer need

**Reject concatenation (Option C from the task brief) outright** — the sample document set (§8) is already cleanly split into small, single-topic documents. Concatenating 24 of them into one ~30K-word blob before extraction would (a) blow past what a single clean extraction call handles well, (b) throw away the one-topic-per-document structure that makes per-document attribution both possible and useful, and (c) make the eventual "which SOP does this training map to" traceability impossible to recover.

**The right shape is Option B: ingest each document as its own `RegulatoryDocument` row under the appropriate body, keeping the document as the unit of ingestion and attribution — which is what the pipeline already does per-call today.** The gap isn't the ingestion unit; it's everything wrapped around it (bulk trigger UX, cross-document dedup awareness, document-level display, cost gating). This matches the recon's own instinct-check: the natural "add a bulk upload endpoint" reaction is *necessary but not sufficient* — the compliance-display gap (§4) and the ingestion cost-gate gap (§5) matter as much as upload UX and are easy to miss if the fix stops at "add ZIP upload."

**On per-document traceability specifically:** the sample set's embedded metadata (Document Reference, Related Standard/Requirement ID, Coverage Type — see §8) shows the customer's own source material already encodes exactly the kind of cross-document relationship that would make traceability valuable ("this requirement came from HIQA SOP-3, and it's the Standard-side of a dual-coverage pair with REG-3"). Nothing in the current ingestion prompt or schema captures that relationship — it would be extracted as prose (if at all) and then discarded, since `RegulatoryRequirement` has no field for "related requirement" or "coverage type." This is real information loss, but it's separable from the urgent onboarding need and should be scoped as its own (likely deferred) chunk — see §10.

---

## 8. Sample document set analysis

`docs/HIQA-SOPs-With-Matrix-Relationships.zip` contains **24 PDFs, ~5.7–5.8KB each, 3 pages each** (~1,000–1,200 words per document), in two folders of 12:

- `HIQA/001-Leadership-Accountability.pdf` … `012-Safe-Environment.pdf` — `Type: Quality Standard`, referenced as `STD-001`…`STD-012`.
- `Regulatory/001-Corporate-Structure.pdf` … `012-Infection-Control.pdf` — `Type: Legal Requirement`, referenced as `REG-001`…`REG-012`.

**These are not scanned/authentic HIQA publications — they are uniformly templated, almost certainly synthetically generated demo/test material.** Every document shares an identical 6-section skeleton (Purpose & Scope → Responsibilities → Key Hazards & Controls → Pre-Care Checks → Step-by-Step Procedures → Escalation), with only the specific topic content swapped in. This matters for scoping: it's a good stress-test shape (many small documents, clean structure) but should not be treated as evidence of what a *real* HIQA/regulatory PDF set looks like in length or formatting — real regulatory PDFs are typically far longer, less structurally uniform, and not written to be machine-extractable.

**The "matrix relationship" is embedded per-document metadata, not a separate matrix file.** Despite the ZIP's name, there is no standalone cross-reference/matrix document. Instead, every document carries structured header fields:
```
Document Reference: STD-001 / REG-007
Related Regulatory Requirement: REG-001   (on HIQA/Standard docs)
Related HIQA Standard: STD-007            (on Regulatory docs)
Coverage Type: DUAL | REGULATION ONLY     (presumably also STANDARD ONLY, not sampled)
```
Confirmed on 4 sampled documents (`STD-001`↔`REG-001` pair, `REG-007` referencing `STD-007`). This is a **1:1 numeric pairing across the two folders** (001↔001, 007↔007, etc.) — i.e., the same underlying compliance topic is deliberately represented twice, once as a voluntary "Standard" framing and once as a mandatory "Regulatory/Legal" framing, exactly mirroring the `RegulatoryBodyKind.Standard` vs `RegulatoryBodyKind.Regulation` split that shipped this week (§1). `Coverage Type: DUAL` on `STD-001` and `Coverage Type: REGULATION ONLY` on `REG-007` (no matching `STD-007` sampled as "dual") suggests not every pair is symmetric — some topics only exist Regulation-side or only Standard-side.

**This set is very plausibly purpose-built test data for the just-shipped Kind/subscription feature**, not a literal preview of what will be uploaded for the real homecare client next week. Whether or not that's its origin, it's a strong proxy for the actual structural question this recon needs to answer, and it answers cleanly: **the real-world need looks like "many small, already-atomic, already-cross-referenced documents," not "one regulation arbitrarily split across 8 files."** No cross-references were found *between* different topic numbers (e.g., nothing in `001` mentions `005`) — relationships are pairwise (Standard↔Regulation for the *same* topic number), not a dense web ingestion would need to resolve globally.

---

## 9. Gap analysis — what breaks vs. what needs adding

| Area | Status | Breaks with multi-doc? | Needs adding? |
|---|---|---|---|
| RegulatoryBody → RegulatoryDocument cardinality | Already unbounded, no schema change needed | No | No |
| Body↔Document↔Profile↔Requirement provenance chain | Already threads through the specific document | No | No |
| Kind (Regulation/Standard) + subscription aggregation | Already unions across arbitrary body counts | No | No |
| Admin document creation (single) | Works, unrestricted per body | No | No |
| Bulk/multi-file/ZIP admin upload | **Does not exist** | N/A | **Yes** — new endpoint(s) + UI, CSV-bulk-import pattern as reference |
| Ingestion cost visibility/gating for a batch | **Does not exist** | Silent linear cost scaling | **Yes** — reuse Corpus-run `CostEstimationService` pattern |
| Cross-document dedup | Exists only within one profile (same doc+sector); no cross-document check | Look-alike requirements from sibling documents both land, unflagged | **Yes**, at minimum a "similar title in sibling document" flag for human review — do not auto-merge (legally-important content, transparency required per task brief) |
| Compliance checklist document attribution | DTO has no document field; UI groups by Principle only | Requirements from different source docs are visually indistinguishable | **Yes** — extend DTO with document title/id, port Body→Document grouping pattern already built for the browse page |
| Regulations browse page | Already has full Body→Document hierarchy | No | No (reference implementation for the fix above) |
| Cross-reference/coverage-type metadata (Related Standard/Requirement, DUAL coverage) | Not modeled anywhere; would be discarded during extraction | Real information loss vs. source material | Optional/deferred — separate, larger design question |

---

## 10. Rough scope, ordered by dependency

1. **Compliance checklist document attribution** (small–medium). Add document id/title to `ComplianceRequirementDto`/`PendingMappingDto` (join already available via `Profile.RegulatoryDocumentId`), extend `GetComplianceChecklistAsync`'s projection, add a document sub-grouping or badge to `SectorChecklistView`/`RequirementRow` mirroring the existing `DocumentSection` component from the browse page. No schema migration. Should land before the homecare tenant's compliance checklist is shown to anyone external — this is the piece that directly prevents "confusing duplicate output."
2. **Bulk admin document ingestion UX** (medium). Multi-file (or ZIP) upload endpoint + validate → confirm → background-job → poll flow, modeled directly on `BulkEmployeeImportJob`/`BulkEmployeeImportController`. Depends on nothing above. Note: given the homecare set is only ~24 small files and the manual flow (create+upload+ingest ×24) is tedious but *possible* today, this could plausibly be done manually once for next week's onboarding while this chunk is built properly for repeat/scale use — worth an explicit decision rather than assuming it blocks the deadline.
3. **Ingestion cost gate for batches** (small–medium). Extend/reuse `CostEstimationService` (already exists for corpus runs) to estimate a batch before enqueueing N `RequirementIngestionJob`s; add a simple concurrency/queue awareness so a large batch doesn't fire unbounded parallel Claude calls. Depends on #2 existing to have a "batch" to gate in the first place (a gate on manual one-at-a-time triggering has much lower value).
4. **Cross-document dedup-awareness for reviewers** (medium). At minimum, flag "a requirement with a similar title exists in a sibling document under this body" during the Suggested-mapping review step, surfaced to the human reviewer — not silent, not automatic. Depends on #1 (needs document identity to compare against) and benefits from #2/#3 existing since it's most valuable exactly when many documents are ingested in one push.
5. **Cross-reference/coverage-type capture** (large, product-decision-gated, likely deferred). Modeling `Related Standard/Requirement` + `Coverage Type` as first-class relationships between requirements would require new schema (a self-referencing or join table on `RegulatoryRequirement`), a materially different extraction prompt (asking the model to identify and preserve the relationship, not just extract the requirement), and new UI to display paired/dual-coverage requirements. Not needed for the immediate onboarding; flagged here because the sample data shows the customer's own source material already has this structure, so the ask may resurface. Recommend treating as its own scoped follow-up, not folded into the above.

**None of chunks 1–4 requires an EF migration** except possibly a nullable "similar-requirement" annotation in #4 depending on implementation choice (a computed/on-the-fly similarity check would need none). Chunk 5 is the only one that clearly needs new schema.
