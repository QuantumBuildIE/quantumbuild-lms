# Faithful, Structure-Preserving Regulatory Extraction — Recon

**Date:** 2026-07-31
**Status:** Read-only recon. Facts only, file:line for every claim. No code changed, no data changed, no fix or design proposed.
**Branch:** `transval`, HEAD `4bbceb8`.

**Requirement this recon serves:** extraction must faithfully transcribe the source document's own discrete, numbered obligations — not free-summarise them — using the document's own identifiers and placement. Current extraction instead free-extracts model-judged, model-titled "requirements," consolidating and cross-contaminating.

**Important scoping fact established by this recon, read first:** the codebase's segmented (per-principle) extraction architecture — `PrincipleNumbers`, `ExtractPrincipleSegmentAsync`, the standard-ID completeness check — did not exist until the current HEAD commit, `4bbceb8` ("Report all failed principle segments, not just the first", 2026-07-30 16:42 +0100). The prior commit on this file, `d257361`, and everything before it, ran a single whole-document Claude call with no segmentation and no completeness check of any kind (confirmed by `git log --oneline -- .../RequirementIngestionJob.cs`, 9 commits total, `PrincipleNumbers`/`ExtractPrincipleSegmentAsync` absent from every diff before `4bbceb8`). The "confirmed defects" this recon was asked to treat as context (cross-boundary contamination between P2/P3, Standard 1.1 consolidated to 4 requirements, non-deterministic counts) were therefore necessarily observed against the **pre-segmentation, whole-document architecture** — not the code currently on HEAD. Segmentation likely closes some of the cross-*principle* bleeding (each call is now explicitly scoped to one principle, §A.1), but as shown below, it changes none of the underlying faithfulness gaps: the prompt still asks for free-judged "requirements" with model-invented titles, still has no concept of the document's own feature numbering, and the new completeness check still operates at the standard level only, not the feature level.

---

## A. Current extraction semantics

### A.1 — What the prompt actually asks for

**File:** `RequirementIngestionJob.cs:633-666` (`BuildExtractionPrompt`), called once per principle number (`:127-129, 443`).

Structure (quoting the codebase's own field-spec labels and instruction headers, not the source document):

- Opening sentence: "extract only the requirements under Principle {N} that relate to staff training, competency, or compliance obligations" (`:635`). "All" is not the operative word here (unlike the pre-`4bbceb8` whole-document prompt, which the prior recon `docs/regulatory-extraction-rebuild-recon.md:111` quotes as asking to extract "**all** requirements") — the current prompt's unit of extraction is still **"requirements"**, a term the prompt never defines against the document's own structure.
- A topical scope filter, restated twice: "extract only the requirements under Principle {N} that relate to staff training, competency, or compliance obligations" (`:635`) and, in "IMPORTANT RULES": "Extract ONLY requirements belonging to Principle {principleNumber} that relate to staff training, competency, skills, or compliance obligations" / "Do NOT include general policy statements, organisational structure requirements, or non-training items" (`:658-659`).
- Per-item field spec, 8 fields (`:639-647`): `title`, `description`, `section`, `sectionLabel`, `principle`, `principleLabel`, `priority`, `displayOrder`. Four (`section`, `sectionLabel`, `principle`, `principleLabel`) are marked "MANDATORY — never return null or omit it" (`:642-644, 661`).
  - `title`: "A concise title (max 200 chars) for the training/competency requirement" (`:640`) — explicitly asks the model to compose a title, not transcribe one (the source document's numbered items have no titles of their own, per §A.4/B.4 below).
  - `description`: "A detailed description (max 2000 chars) of what the requirement entails" (`:641`) — asks for a description of what the item "entails," not a verbatim or near-verbatim transcription of the source paragraph.
  - `section`: "The section or article reference from the document (e.g. \"Standard 2.3\", \"Article 4\", \"§7\")... Use the standard numbering from the source document" (`:642`) — the two worked examples are both **standard-level** references. Nothing in the field spec, its examples, or its description names or gives an example of a **feature-level** identifier (the document's own `1.1.1`-style numbering, confirmed to exist in the source — §B.4). The prompt does not ask the model to preserve, or even mention, feature-level numbering anywhere.
  - `displayOrder`: "Sequential numbering within THIS principle's requirements, starting from 1 (final numbering across the whole document is assigned separately once all principles are extracted)" (`:647`) — this is the codebase's own sequence number for whatever list of "requirements" the model decides to produce; it is never populated from, or reconciled against, the document's own numbering (confirmed at the assembly step, `:154-165`: every item's `DisplayOrder` is unconditionally overwritten by its position in the concatenated list, discarding whatever the model returned).
- "CANONICAL PRINCIPLE LABELS" block (`:649-655`) hard-codes exactly three principle/label pairs — P2 "Safety & Wellbeing", P3 "Responsiveness", P4 "Accountability" — with no entry for Principle 1. The source document's actual Principle 1 title is "A Human Rights-based Approach" (confirmed §B.4/B.5). This gap is a fact about the current prompt, not something this recon evaluates for impact.
- Closing instruction: "Respond ONLY with a valid JSON array — no preamble, no markdown, no explanation" (`:662`).

**Where interpretation/consolidation is invited vs. constrained, precisely:**
- **Constrained:** which principle a call may draw from (one Claude call per principle number, explicit "do not extract requirements belonging to any other principle" instruction, `:637`); the 8-field output shape; the four mandatory fields; standard-level section referencing.
- **Invited, by omission and by explicit instruction:** (1) the topical filter ("relate to staff training, competency... Do NOT include general policy statements... or non-training items", `:635, 658-659`) hands the model a judgment call over which of the source document's numbered features count as in-scope, with no reference to the document's own feature boundaries to anchor that judgment; (2) nothing in the prompt asks the model to enumerate or account for every one of the document's own numbered features, so an item the model judges out-of-scope, or simply omits, leaves no trace and triggers no failure — the only automated check that exists (§C) operates one level up, at the standard, not the feature; (3) `title` and `description` are explicitly framed as authored output ("a concise title for," "a description of what it entails"), not transcription, so two runs over identical source text are not expected to produce identical wording even for the same underlying obligation — consistent with the non-deterministic-count symptom cited in this task's context, though this recon did not re-run extraction to confirm it live.

### A.2 — How a "requirement" is represented today

**Entity:** `RegulatoryRequirement.cs:11-45`. **Configuration:** `RegulatoryRequirementConfiguration.cs:18-43`. **DTO:** `IngestionDtos.cs:59-75` (`RegulatoryRequirementDto`, a 1:1 field mirror of the entity plus two profile-lookup fields).

Fields actually populated by the ingestion path (`RequirementIngestionJob.cs:787-807`, `PersistDraftRequirementsAsync`):

| Field | Max length | Populated from | Nature |
|---|---|---|---|
| `Title` | 200 | `extracted.Title`, length-clamped | Model-invented (per §A.1, the prompt asks for a composed title) |
| `Description` | 2000 | `extracted.Description`, or `extracted.Title` if blank, length-clamped | Model-authored paraphrase ("what it entails," not a quote) |
| `Section` | 20 | `extracted.Section`, length-clamped | Standard-level reference, e.g. "Standard 2.3" — no feature-level identifier concept exists in the prompt or schema (§A.1) |
| `SectionLabel` | 200 | `extracted.SectionLabel`, length-clamped | Model-derived short label |
| `Principle` | 20 | `extracted.Principle`, length-clamped | Canonical (P2-P4) or model-inferred |
| `PrincipleLabel` | 200 | `extracted.PrincipleLabel`, length-clamped | Canonical or model-derived |
| `Priority` | 20 | `ValidatePriority(extracted.Priority)` (`:824-831`), clamped to high/med/low | Model-judged |
| `DisplayOrder` | — (required int) | Always overwritten post-assembly to the item's position in the concatenated cross-principle list (`:162-165`) | Assembly-assigned, not model- or document-derived |
| `IngestionSource` | — | Hard-coded `Automated` | — |
| `IngestionStatus` | — | Hard-coded `Draft` | — |

**Conclusion for A.2:** the schema **can technically hold** a document-native identifier in `Section` (20 chars is enough room for a string like "Standard 1.1.7"), but no field, current prompt instruction, or persistence code path populates or expects one — the field's actual contract (per both the prompt's worked examples and its own doc-comment usage) is "standard-level article reference," not "the document's own discrete-obligation identifier." There is no field anywhere on the entity that distinguishes which of the source document's two feature blocks (person-experience vs. provider, §B.4/B.5) an extracted item corresponds to, nor one that records a feature-level identifier distinct from the standard-level `Section`. The schema and its current usage assume one authored summary object per model-judged "requirement," not a 1:1 transcription of a document-numbered feature.

### A.3 — Does anything carry the document's own feature identifier today?

No. Confirmed by: (1) direct read of the entity (`RegulatoryRequirement.cs:11-45`) and its DTO (`IngestionDtos.cs:59-75`) — no field named or documented as a feature/item identifier; (2) repo-wide grep for `FeatureId`, `FeatureNumber`, `ExpectedFeatures`, `PersonFeature`, `ProviderFeature`, and the literal `1.1.1` — zero hits anywhere in `src/`; (3) the only structural map that exists anywhere in the ingestion path, `HiqaExpectedStandardsByPrinciple` (`RequirementIngestionJob.cs:393-400`), is keyed and valued at the **standard** level (`"1.1"`, `"2.3"`, etc.) — it contains no feature-level entries and is not consulted by, or connected to, the `Section` field's persistence path in any way that would compare a persisted item's `Section` against a feature identifier. The document's own feature numbering (`1.1.1`, `1.1.2`, ...) is discarded at extraction time — it is never asked for, never stored, and never checked.

---

## B. What faithful extraction would require

### B.4 — The document's actual structure, and the authoritative per-standard/per-block feature inventory

**Source verified directly:** the PDF now present at `web/public/documents/Draft-National-Standards-for-Home-Support-Services.pdf` (untracked, added to the working tree at the time of this recon) — title and filename match the `RegulatoryDocument` seeded for the HIQA body exactly (`RegulatoryProfileSeedData.cs:131`: `("HIQA", "Draft National Standards for Home Support Services", "Draft Nov 2024", ...)`; the entity's own doc-comment example at `RegulatoryDocument.cs:8` is this same title). This is confirmed to be the actual document the HIQA ingestion path targets, not an unrelated sample. Text was extracted with `pdftotext` (both `-layout` and raw reading-order modes, for cross-checking) — a general-purpose PDF text extractor, not the codebase's own `PdfExtractionService`, but a reasonable proxy for the codebase's own PdfPig-based `page.Text` extraction (§B.6 assesses fidelity).

**Confirmed two-block-per-standard pattern.** Every one of the document's 17 standards presents two independently-numbered feature lists, in this fixed order:
1. A block titled **"Features that demonstrate how a person should experience a service that is meeting this standard include:"** — numbered `X.Y.1`, `X.Y.2`, ... — the *person-experience features*.
2. A block titled, inconsistently across the document, either **"Features of a service provider meeting this standard are likely to include:"** or **"Features of service provider meeting this standard are likely to include:"** (both wordings appear in the source — see §B.6) — separately numbered starting again from `X.Y.1` — the *provider features*.

**Principle structure:** 4 principles, matching the prompt's `PrincipleNumbers = {1,2,3,4}` (`RequirementIngestionJob.cs:382`) and the completeness check's standard groupings (`:393-400`) exactly:
- Principle 1 — "A Human Rights-based Approach" — Standards 1.1-1.4
- Principle 2 — "Safety and Wellbeing" — Standards 2.1-2.5
- Principle 3 — "Responsiveness" — Standards 3.1-3.3
- Principle 4 — "Accountability" — Standards 4.1-4.5

17 standards total, matching CLAUDE.md's own count and the current `HiqaExpectedStandardsByPrinciple` map exactly.

**Authoritative per-standard, per-block feature counts** (counted directly from both extraction passes; the exact numbered range is given so any future extraction or completeness check can be checked against it):

| Standard | Person-experience features | Count | Provider features | Count | Notes |
|---|---|---|---|---|---|
| 1.1 | 1.1.1-1.1.8 | 8 | 1.1.1-1.1.3 | 3 | Matches the exact example given in this task's brief |
| 1.2 | 1.2.1-1.2.5 | 5 | 1.2.1-1.2.3 | 3 | |
| 1.3 | 1.3.1-1.3.8 | 8 | 1.3.1-1.3.2 | 2 | |
| 1.4 | 1.4.1-1.4.5 | 5 | 1.4.1-1.4.4 | 4 | |
| 2.1 | 2.1.1-2.1.4 | 4 | 2.1.1-2.1.3 | 3 | |
| 2.2 | 2.2.1-2.2.8 | 8 | 2.2.1-2.2.5 | 5 | |
| 2.3 | 2.3.1-2.3.7 | 7 | 2.3.2-2.3.8 | 7 | Provider block starts at `.2`, not `.1` — a numbering gap in the source itself, confirmed identically in both the `-layout` and raw extraction passes (`hiqa_layout.txt:1344-1352`, `hiqa_raw.txt:463-464`) — not an extraction artifact |
| 2.4 | 2.4.1-2.4.4 | 4 | 2.4.1-2.4.4 | 4 | Aligned |
| 2.5 | 2.5.1-2.5.2 | 2 | 2.5.1-2.5.2 | 2 | Aligned |
| 3.1 | 3.1.1-3.1.5 | 5 | 3.1.1-3.1.5 | 5 | Aligned |
| 3.2 | 3.2.1-3.2.3 | 3 | 3.2.1-3.2.2 | 2 | Provider block is short one item vs. person block — source fact, not extraction |
| 3.3 | 3.3.1-3.3.5 | 5 | 3.3.1-3.3.5 | 5 | Aligned |
| 4.1 | 4.1.1-4.1.5 | 5 | 4.1.1-4.1.8 | 8 | Provider block has *more* items than the person block |
| 4.2 | 4.2.1-4.2.2 | 2 | 4.2.1-4.2.2 | 2 | Aligned |
| 4.3 | 4.3.1-4.3.3 | 3 | 4.3.1-4.3.2 | 2 | |
| 4.4 | 4.4.1-4.4.6 | 6 | 4.4.1-4.4.6 | 6 | Aligned |
| 4.5 | 4.5.1-4.5.3 | 3 | 4.5.1-4.5.5 | 5 | Provider block has *more* items; source uses trailing-period numbering here (`4.5.1.`) unlike every other standard — see §B.6 |
| **Total** | | **83** | | **68** | **151 combined** |

Only **8 of 17 standards** (2.4, 2.5, 3.1, 3.3, 4.2, 4.4, and by count-only not alignment none others) have person/provider blocks that are both equal in count *and* numbered in step. The remaining 9 differ in count, numbering alignment, or both. This is a directly relevant fact for §B.5: the two blocks are not parallel restatements of one obligation per standard — they are two independently-sized, independently-numbered lists, and in 2.3 specifically the provider list's own internal numbering has a gap (starts at `.2`) that exists in the source document itself.

**This 151-combined figure corroborates, independently, the ~150-requirement estimate** that `docs/regulatory-extraction-size-recon.md:52-55` used (anchored to seed-data proxies, not a direct document read) to argue the pre-guardrail 8192-token single-call cap would truncate — this recon's count was derived from the actual document text, not an estimate, and lands almost exactly on that prior estimate.

**This is the target the extraction and its completeness check should be measured against**, not derived here as a design, per non-scope.

### B.5 — Person-features vs. provider-features: surfaced, not decided

The document itself treats these as two **separately numbered, separately headed** lists per standard, not one merged list. This recon does not decide whether the system should represent them as (a) up to 151 distinct obligations (both blocks kept separate — an inspector-facing obligation like "the provider must place human rights at the centre of governance" (1.1 provider) is arguably a distinct auditable duty from "the person's human rights are explained to them" (1.1 person), even where topically paired), (b) 83 (person-experience only), (c) 68 (provider-only), or (d) some merged/paired representation — which, per the counts above, cannot be a clean 1:1 pairing in 9 of 17 standards because the two lists differ in length and/or numbering alignment, so a "merge" option would require editorial judgment about which items correspond, not a mechanical zip. Whichever choice is made, it changes the authoritative target count materially (68-151 depending on interpretation) and is a product decision, not an engineering default — surfaced here, not resolved.

### B.6 — Can document structure be derived programmatically from extracted PDF text, or does it need a curated per-document map?

**Both extraction passes preserve the `X.Y.Z` identifiers as literal, regex-matchable tokens in the flat text — but with source-level and extraction-level irregularities a programmatic derivation would have to tolerate:**

1. **Heading wording is inconsistent within the same document.** The provider-features heading appears as both "Features of **a** service provider meeting this standard are likely to include:" (Principles 1 and 4) and "Features of service provider meeting this standard are likely to include:" (Principles 2 and 3, and Standard 1... no — confirmed: principles 2 and 3) — a literal-string match on one wording alone misses roughly a third of the document's provider blocks (`hiqa_layout.txt:702,770,851,933` vs. `:1140,1233,1344,1453,1523,1703,1836`).
2. **A genuine source-document numbering gap exists** (Standard 2.3's provider block starts at `2.3.2`, not `2.3.1` — §B.4) — confirmed present in the source PDF itself via two independent extraction passes, not an artifact of either extraction method.
3. **A genuine source-document count asymmetry exists** where a standard's provider block has fewer (3.2: 2 vs. 3) or more (4.1: 8 vs. 5; 4.5: 5 vs. 3) items than its person block — a completeness check built only from person-feature counts, or only from provider-feature counts, would be wrong for the other block in 9 of 17 standards.
4. **Identifier-to-body-text association degrades near page breaks under `-layout` extraction specifically**: at the page 18/19 boundary inside Standard 1.1, the `-layout` pass renders `1.1.5` and `1.1.6` as bare identifier lines separated from their body paragraphs by an interleaved page-footer/header block (`hiqa_layout.txt:683-688`) — the identifiers survive as tokens, but naive "identifier line, then next line is the body" parsing would misattribute or drop text at this boundary. The **raw** (non-layout) pass handles the same page boundary more cleanly, merging the two identifiers onto one line (`1.1.5 1.1.6`, `hiqa_raw.txt:194`) but keeping both tokens intact and parseable via regex — the raw pass is a closer proxy to what the codebase's own `PdfPig`-based `page.Text` extraction (`PdfExtractionService.cs:47`, confirmed by the prior recon `docs/regulatory-extraction-rebuild-recon.md:220-224` to do no layout reconstruction, only raw per-page text) would actually produce, but this recon did not run the codebase's own extractor against this file to confirm byte-for-byte.
5. **Numbering style varies**: Standard 4.5 uses trailing-period identifiers (`4.5.1.`, `4.5.2.`, `4.5.3.`) unlike every other standard's bare `X.Y.Z` (`hiqa_raw.txt:907-921`) — a regex anchored to "no trailing punctuation" would silently miss this standard's items.

**Conclusion:** the identifiers are present and machine-extractable via targeted pattern matching (a regex tolerant of the above irregularities could very likely recover the full 151-item inventory programmatically from either extraction pass), but **a general-purpose, no-curation "derive the structure automatically" approach is not safe as-is** — the heading-wording inconsistency (point 1) and the count/numbering asymmetries (points 2-3) mean a purely mechanical parser would need per-document tolerance rules that are themselves a form of curation, and the two known irregularities (2.3's gap, the inconsistent heading wording) would need to be either specifically handled or hand-verified against the actual document rather than assumed away. Whether "regex with tolerance rules, verified once per document" counts as "self-derivable" or "a curated per-document map" is a matter of degree this recon surfaces rather than resolves — the current `HiqaExpectedStandardsByPrinciple` map (`RequirementIngestionJob.cs:393-400`) is itself already a hand-curated, standard-level-only version of exactly this kind of map, and its own doc-comment (`:387-391`) states plainly: "THIS MAP IS HIQA-SPECIFIC... A second regulatory document with a different structure will need its own map... before it can be ingested through this job — do not assume this generalises as-is."

---

## C. Completeness check

### C.7 — What it validates today vs. what faithful extraction requires

**Current check:** `FindMissingStandards` (`RequirementIngestionJob.cs:532-552`), invoked after each principle segment's first attempt (`:451-460`) and again after its retry (`:482-491`).

- For a given principle number, looks up its expected **standard IDs** from `HiqaExpectedStandardsByPrinciple` (`:393-400`) — e.g. Principle 1 → `["1.1", "1.2", "1.3", "1.4"]`.
- For each expected standard ID, checks whether **any** extracted requirement's `Section` field contains that ID as a digit-boundary-guarded substring (`:543-548`, regex `(?<!\d){standardId}(?!\d)`).
- If a standard ID has **zero** matching requirements, it's reported "missing" and triggers a retry (first attempt) or an `Incomplete` segment failure (retry) — which is treated as an all-or-nothing document failure (`:145-152`, no partial persistence).
- **This is presence-only, at standard granularity, requiring exactly one requirement per standard to pass** — a principle segment that returns precisely 4 requirements for Standard 1.1 (matching this task's cited defect example) and 1 requirement each for 1.2/1.3/1.4 would pass this check cleanly, because the check only asks "does at least one requirement mention Standard 1.1," never "does the requirement set for Standard 1.1 account for its known feature count." This directly confirms this task's framing of prior findings ("standard-ID presence only") against the current, post-`4bbceb8` code.

**Gap to faithful-extraction-level checking, precisely stated:**
- Today's check answers one yes/no question per **standard** (17 booleans total across the whole document).
- A faithful check, per the inventory in §B.4, would need to answer up to one yes/no question per **feature**, per **block** (up to 151 booleans, or 83/68 depending on the §B.5 product decision) — roughly a 9x (151/17) increase in the granularity the check would need to operate at, and it would need a feature-level identifier to check against, which (§A.2/A.3) no extracted item currently carries and no field is designed to hold in a distinguishable way from the standard-level `Section`.
- The current check also has no way to detect the *other* half of this task's stated defects — cross-boundary attribution (a requirement whose content belongs to Standard 2.3 but is tagged `Principle: "P3"` / a different `Section`) — because it only checks presence of the standard ID string somewhere in the segment's own requirement set; it does not verify that a given requirement's content actually matches its claimed `Section`/`Principle`, nor would it catch a requirement correctly tagged to the right principle segment (segmentation, per §A.1, now enforces principle-call scoping so cross-*principle* bleeding is structurally reduced) but wrongly labelled to the wrong *standard within* that same principle's own segment — nothing in `FindMissingStandards` cross-checks a requirement's claimed `Section` against its actual textual content.

---

## D. Second document / generalisation

### 8 — Would this generalise beyond HIQA, and what would supplying a per-document structure map entail?

- **`HiqaExpectedStandardsByPrinciple`'s own doc-comment already flags this as debt**, quoted in full above (§B.6) — this is not a new finding, but this recon confirms it is still true on current HEAD and quantifies what "its own map" means concretely: for HIQA, that map would need to grow from 17 standard-level entries to up to 151 feature-level entries (or 83/68, per the §B.5 decision) to support faithful checking, not just remain 17 entries.
- **The generalisation problem is not hypothetical or limited to one future document** — `RegulatoryProfileSeedData.cs:129-135` already seeds **four** regulatory bodies/documents, not one: HIQA ("Draft National Standards for Home Support Services"), HSA ("Safety, Health and Welfare at Work Regulations"), FSAI ("Food Safety Authority of Ireland Regulations"), RSA ("Road Transport Regulations"). Only HIQA has a `HiqaExpectedStandardsByPrinciple`-style map or any seeded `RegulatoryRequirement` rows (`RegulatoryRequirementSeedData.cs:19-61`, chain requires `RegulatoryBody.Code == "HIQA"` specifically). The other three documents' structures (whether they use a comparable standard/feature hierarchy at all, or something else entirely — clause/regulation numbering, schedule/annex structure, etc.) were not read as part of this recon (no PDF for any of the three was located in the repository) and are simply unknown from the codebase today.
- **What supplying a per-document map would entail, based on what HIQA's map required:** (a) reading the actual source document (as this recon did for HIQA, manually, via an external PDF text extractor, since no in-repo tooling exists to assist this — `PdfExtractionService.cs` performs no heading/structure extraction at all, confirmed in §B.6 and independently by the prior recon `docs/regulatory-extraction-rebuild-recon.md:216-225`); (b) identifying that document's own numbering/heading conventions, which per HIQA's own internal inconsistencies (§B.6) cannot be assumed regular even within one document, let alone assumed to match HIQA's `Principle → Standard → dual-feature-block` shape for a differently structured instrument (e.g. numbered "Regulations" or "Articles" with no equivalent of person-experience vs. provider dual framing at all); (c) hand-authoring the equivalent of `HiqaExpectedStandardsByPrinciple` (or its feature-level successor) for that document specifically, with no code currently in place to derive it automatically end-to-end (§B.6) or to route a document to the correct structure map at ingestion time — `RequirementIngestionJob.ExecuteAsync` calls `HiqaExpectedStandardsByPrinciple` unconditionally (`:534`) with no per-document or per-`RegulatoryBody`/`RegulatoryDocument` dispatch of any kind; a second document today would either reuse HIQA's wrong map or (per the `FindMissingStandards` fallback behaviour at `:534-535`, returning no missing standards for any unmapped principle number) silently skip the completeness check entirely rather than fail loudly.
- **Conclusion:** the current design is explicitly and completely HIQA-only, by its own admission in code comments, and generalising it is not a matter of extending one config value — it requires a per-document discovery-and-curation step for every new regulatory document, with no automated tooling in the codebase today to assist that discovery beyond what a person doing what this recon did manually (extract text, read it, hand-count the structure) would produce.

---

## Summary

| Item | Finding |
|---|---|
| 1-2 (current prompt/schema semantics) | Prompt asks for free-judged "requirements" with model-composed title/description, a topical training-relevance filter with no anchor to the document's own feature boundaries, and standard-level-only section referencing. Schema/DTO field set is a 1:1 mirror of this — `Title`/`Description` are authored-summary fields, `Section` is documented and used as a standard-level reference, and no field distinguishes person-experience vs. provider origin or carries a feature-level identifier. `RequirementIngestionJob.cs:633-666` (prompt), `:787-807` (persistence), `RegulatoryRequirement.cs:11-45` (entity) |
| 3 (feature identifier today) | None — never asked for, never stored, never checked. Confirmed by full-repo grep. |
| 4 (authoritative HIQA inventory) | 17 standards; 83 person-experience features + 68 provider features = 151 combined, exact per-standard breakdown in §B.4 table. Independently corroborates the prior ~150 estimate. |
| 5 (person vs. provider decision) | Surfaced, not decided: the two blocks differ in count and/or numbering alignment in 9 of 17 standards, so a clean 1:1 merge is not mechanically possible — this is a product decision with a materially different target count (68/83/151) depending on the choice. |
| 6 (self-derivable structure?) | Identifiers survive as regex-matchable tokens in extracted text, but the source document itself has irregularities (inconsistent heading wording, a genuine numbering gap at 2.3, count asymmetries, one non-standard identifier format at 4.5) that a purely mechanical, no-curation parser would mis-handle — any structural map needs per-document verification, not blind derivation. |
| 7 (completeness check gap) | Current check is 17 standard-presence booleans; faithful checking needs up to 151 feature-presence booleans plus content-to-Section attribution checking, which does not exist in any form today. |
| 8 (generalisation) | `HiqaExpectedStandardsByPrinciple` is explicitly HIQA-only per its own doc-comment, confirmed still true; three other regulatory documents are already seeded with no structure map of any kind and no per-document dispatch mechanism; a second document requires the same manual discovery-and-curation process this recon performed for HIQA. |

## Non-scope confirmation

No fix was designed or written. No prompt rewrite was proposed. The person-vs-provider-features question (§B.5) was not decided. No `RegulatoryRequirement`, `RegulatoryProfile`, `RegulatoryDocument`, or any other row was created, modified, or deleted. Ingestion was not run. The UI redesign and the status-refresh/polling bug (covered by `docs/regulatory-ingestion-flow-recon.md` and `docs/document-sectors-card-recon.md`) are out of scope here.
