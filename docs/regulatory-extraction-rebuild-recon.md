# Regulatory Extraction Segmentation Rebuild — Recon

**Date:** 2026-07-30
**Status:** Read-only recon. Facts only, no design, no code changes, no fix proposed.

**Purpose:** Map everything a segmented-extraction rebuild (splitting the single
whole-document Claude call into N calls along the document's own structure —
principles/standards — so each call stays well within the output-token
ceiling) would have to touch. This recon runs against the code **as it stands
after** the truncation-guardrail commit (`d257361`, "Make silent regulatory
extraction failures loud") and the profile-creation commit (`6ed10c1`, "Add
sector-to-document attachment via RegulatoryProfile creation") — both already
shipped on `transval` at the time of this recon. No design decision on
per-principle vs per-standard granularity, no prompt rewrite, and no
partial-failure policy is made here.

**Relationship to prior recon docs:** `docs/regulatory-extraction-size-recon.md`
(same day, pre-guardrail) established that truncation on a real ~150-requirement
document is near-certain against the 8192-token cap and that a truncated run
used to report `Success` with zero requirements persisted. That silent-success
gap is now closed by the guardrail this recon reads (§1 below) — a truncated
run now reports `Failed`/`extraction_truncated` loudly instead. This recon
does not re-verify the truncation-likelihood arithmetic; it starts from "yes,
truncation is expected and now fails loudly" and maps what a segmented
rebuild would have to change. `docs/multi-document-regulation-recon.md` (same
day) separately investigated multi-*document* (multi-PDF, one profile pair
each) support — a different axis from segmenting *within* one document's
extraction call, though §4 below reuses some of its dedup findings.

---

## 1. Current extraction flow, end to end (post-guardrail)

**File:** `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/RequirementIngestionJob.cs`

`ExecuteAsync` (`RequirementIngestionJob.cs:61-195`) runs as a Hangfire job
(`[AutomaticRetry(Attempts = 1)]`, queue `content-generation`,
`RequirementIngestionJob.cs:59-60`), keyed by `regulatoryDocumentId`:

1. **Load** — `RegulatoryDocument` with `.Include(d => d.Profiles).ThenInclude(p => p.Sector)` (`:72-75`). 404s (logs error, returns) if not found (`:77-81`).
2. **Mark `Ingesting`** — sets `LastIngestionStatus = Ingesting`, clears prior error fields, saves (`:83-86`).
3. **Re-validate SourceUrl** — `SourceUrlValidator.IsValid`; on failure calls `MarkFailedAsync(document, "invalid_uri", ...)` and returns (`:91-95`).
4. **Fetch + extract text** — `FetchDocumentTextAsync` (`:262-333`); dispatches to `IPdfExtractionService.ExtractTextFromUrlAsync` for `.pdf` URLs or an HTML-strip path otherwise. On failure, `MarkFailedAsync` with a mapped error code and return (`:98-107`).
5. **Claude extraction** — `ExtractRequirementsViaClaudeAsync(extractedText, ...)` (`:366-410`), described in detail below. Returns a `ClaudeExtractionOutcome` record (`:356-364`) carrying either a `Requirements` list or an `ExtractionFailureReason` (`None` / `Truncated` / `InvalidJson`, `:349-354`).
6. **Guardrail branch on the outcome** (`:116-134`) — the four terminal outcomes:
   - `Failure == Truncated` → `MarkFailedAsync(document, "extraction_truncated", ...)`, return (`:116-124`).
   - `Failure == InvalidJson` → `MarkFailedAsync(document, "extraction_invalid_json", ...)`, return (`:126-134`).
   - Otherwise, `extractedRequirements = extraction.Requirements ?? new List<>()` (`:136`) — a well-formed, non-truncated response, possibly an empty array.
7. **Active-profile check** (`:141-153`) — `document.Profiles.Where(p => p.IsActive)`; if empty, `MarkSkippedAsync(document, "no_active_profiles", ...)`, return. This is the third terminal outcome and is reached *after* a successful extraction — the extracted candidates are discarded with a message stating how many were found (`:150`).
8. **Zero-requirements check** (`:155-164`) — if extraction succeeded (parseable, non-truncated) but the array is empty, `MarkFailedAsync(document, "extraction_zero_requirements", ...)`, return. Fourth terminal outcome.
9. **Persist** — loop over `profiles`, calling `PersistDraftRequirementsAsync(profile, extractedRequirements, ...)` once per profile (`:166-172`), see §3.
10. **Mark `Success`** — `MarkSucceededAsync(document, ...)` (`:175`), stamping `LastIngestedAt`, clearing error fields.

An outer `try/catch(Exception ex)` around the whole method (`:69, 181-194`)
catches anything unhandled, logs it, and calls `MarkFailedAsync(document,
"unknown", ex.Message, ...)` rather than rethrowing — the comment at
`:187-189` states this is deliberate: "Hangfire job should not fail noisily.
But it must not swallow the failure silently either."

**The four terminal outcomes** (`RegulatoryIngestionStatus` values, confirmed
by test names in `tests/QuantumBuild.Tests.Integration/ToolboxTalks/RegulatoryIngestionTests.cs`):

| Outcome | Status set | Error code | Trigger | Line |
|---|---|---|---|---|
| Truncated | `Failed` | `extraction_truncated` | Both initial + retry Claude calls returned `stop_reason == "max_tokens"` | `:116-124` |
| Invalid JSON | `Failed` | `extraction_invalid_json` | Both calls returned non-parseable, non-truncated text | `:126-134` |
| Zero requirements | `Failed` | `extraction_zero_requirements` | A parseable, non-truncated response, but the array was empty | `:155-164` |
| No active profiles | `Skipped` | `no_active_profiles` | Extraction produced N candidates but the document has 0 active `RegulatoryProfile` rows to attach them to | `:141-153` |
| (Success) | `Success` | — | Extraction produced ≥1 requirement and ≥1 active profile exists | `:175` |

Test coverage confirming all four failure/skip paths exists at
`RegulatoryIngestionTests.cs:285-390` (asserting `LastIngestionErrorCode`
values `no_active_profiles` at `:306`, `extraction_zero_requirements` at
`:331`, `extraction_truncated` at `:359`, `extraction_invalid_json` at `:387`).

### The Claude call mechanics — `ExtractRequirementsViaClaudeAsync` (`:366-410`)

- Builds the prompt once via `BuildExtractionPrompt(documentText)` (`:369`, prompt itself at `:412-443`, §2 below).
- **First attempt:** `CallClaudeAsync(prompt, ...)` (`:372`) returns `(responseText, stopReason)`. `TryParseRequirements(responseText)` (`:373`) attempts JSON parse. `truncated = stopReason == "max_tokens"` (`:374`).
- If parsed successfully **and not truncated** → returns `Success` immediately (`:376-377`). Note this is an `&&`: a response that both parses as valid JSON *and* carries `stop_reason: max_tokens` is still treated as truncated, not accepted — `TryParseRequirements` returning non-null does not short-circuit the truncation check.
- Otherwise, logs which condition failed (`:379-388`) and retries with an appended stricter-JSON-only instruction (`:391`, the exact string is quoted in full in the file — not reproduced here as prose, see `:391` directly).
- **Retry attempt:** identical call shape, same full `documentText` re-sent (`:393`). Same success check (`:397-398`).
- If the retry is truncated → returns `ExtractionFailureReason.Truncated` (`:400-406`).
- Else if still unparseable → returns `ExtractionFailureReason.InvalidJson` (`:408-409`).
- **Total Claude calls per ingestion run today: 1 or 2**, both over the entire `documentText` unmodified — confirmed no chunking, no page-range or section-range slicing anywhere in this method or in `BuildExtractionPrompt`.

### `stop_reason` plumbing — now present, unlike the pre-guardrail state

`CallClaudeAsync` (`:445-490`) now extracts `stopReason` via a private
`ExtractStopReason(responseBody)` helper (`:494-500`) that re-parses the raw
response body for the `stop_reason` property, because `AnthropicResponseParser`
does not carry that field (`AnthropicResponseParser.cs:13-49` — reads only
`content`, `usage`, `model`). The helper's leading comment (`:492-493`)
explicitly states it "Mirrors `AiSlideshowGenerationService.ExtractStopReason`"
— see §9 for that reference implementation. `MaxTokens = 8192` remains a
`const int` (`:25`), unchanged by the guardrail work, and is passed as
`max_tokens` in the request body (`:451`). No `temperature` field is set in
the request body (`:448-456`) — the Anthropic API default applies, same as
pre-guardrail.

---

## 2. The prompt — output shape and whole-document assumptions

**File:** `RequirementIngestionJob.cs:412-443` (`BuildExtractionPrompt`)

Structure (quoting field-spec labels and instruction headers verbatim since
they are this codebase's own prompt text, not external material; not
reproducing the full instructional prose):

- Opening role sentence: "You are a regulatory compliance expert. Analyse the following regulatory document and extract **all** requirements that relate to staff training, competency, or compliance obligations." (`:414`, emphasis on "all" is in the source text itself, not added here — this is the clearest whole-document assumption in the prompt).
- Per-field extraction spec for 8 output fields, each introduced by `- fieldName: ...` (`:416-424`): `title`, `description`, `section`, `sectionLabel`, `principle`, `principleLabel`, `priority`, `displayOrder`. Four fields (`section`, `sectionLabel`, `principle`, `principleLabel`) are explicitly marked "This field is MANDATORY — never return null or omit it" (`:419-421`).
- `displayOrder` instruction: **"Sequential numbering starting from 1"** (`:424`) — this is a whole-document-scoped instruction; the model is told to number every extracted item starting at 1 across the entire response, not per-section or per-principle.
- "CANONICAL PRINCIPLE LABELS" block (`:426-431`) — hard-codes exactly three canonical `principle`/`principleLabel` pairs (P2/P3/P4) with instructions to map document wording onto them or fall back to the document's own text if no match. This block is static, sector-agnostic prompt content — not derived from or scoped to any particular part of the document.
- "IMPORTANT RULES" block (`:434-439`) — scope restriction (training/competency/compliance only, exclude general policy/organisational-structure content), repeats the four-mandatory-fields rule, and: "Respond ONLY with a valid JSON array — no preamble, no markdown, no explanation."
- Then: `"DOCUMENT TEXT:\n{documentText}"` (`:441-442`) — the entire extracted PDF text, interpolated in full, unmodified, exactly once, as the final block of the prompt.

**Whole-document assumptions identified — every one of these would need to
change or be re-validated for segment-level operation:**

1. `"extract **all** requirements"` (`:414`) — phrased for a complete document, not a fragment. A segment-scoped call would need different phrasing or the model may under-extract, assuming it's seeing a curated excerpt rather than the full source.
2. `displayOrder: "Sequential numbering starting from 1"` (`:424`) — if sent unchanged to N segment calls, every segment's first extracted item would independently claim `displayOrder = 1`, colliding across segments. This is a direct assembly-ordering problem, not just a prompt-wording nuance — see §5.
3. The CANONICAL PRINCIPLE LABELS block (`:426-431`) is static and would carry over to a segment prompt unchanged with no adaptation needed — it does not depend on seeing the whole document, only on knowing the sector's known principle taxonomy. **This one instruction block is segment-safe as-is.**
4. The mandatory-fields and scope-restriction rules (`:416-424, :434-439`) are per-requirement instructions, not whole-document instructions — they operate identically whether the model sees one page or seventy. **Segment-safe as-is.**
5. Nothing in the prompt tells the model what to do if a requirement's full context (e.g., a cross-reference to a definition in an earlier, un-included section) falls outside the segment it's given — this is not addressed anywhere in the current prompt because the question has never arisen under whole-document extraction.

**Net: two prompt instructions (`"all requirements"`, sequential `displayOrder`
from 1) are explicitly whole-document-scoped and would need to change or be
reconciled at the assembly layer; the rest of the prompt's field spec and
rules are structurally segment-agnostic already.**

---

## 3. Persistence structure — `PersistDraftRequirementsAsync` and its caller loop

**File:** `RequirementIngestionJob.cs`

- **Caller loop** (`:166-172`, inside `ExecuteAsync`): `foreach (var profile in profiles) { var created = await PersistDraftRequirementsAsync(profile, extractedRequirements, cancellationToken); totalCreated += created; }`. `extractedRequirements` here is the **single, complete list** returned by the one (or two, on retry) Claude call(s) over the whole document — the same full list is passed to `PersistDraftRequirementsAsync` once per active profile on the document.
- **`PersistDraftRequirementsAsync`** (`:535-599`):
  1. Loads all existing titles for the target profile (`IgnoreQueryFilters()`, includes soft-deleted) into a `HashSet<string>` (`:541-547`).
  2. Iterates the full `extractedRequirements` list once (`:550-589`), skipping empty titles (`:552-556`) and title-duplicates against the set (`:558-562`), otherwise constructing a new `RegulatoryRequirement` and calling `_dbContext.RegulatoryRequirements.Add(requirement)` (`:564-587`) — no `SaveChangesAsync` per item.
  3. **One `SaveChangesAsync` call for the whole profile's batch**, gated on `created > 0` (`:591-596`) — i.e., **the unit of persistence today is "one profile's worth of the complete extraction result," not "one requirement" and not "one document."**
- **No explicit transaction (`BeginTransaction`/`IDbContextTransaction`) anywhere in this file** — confirmed by absence of any `Transaction` string literal in `RequirementIngestionJob.cs`. Each `SaveChangesAsync` call is EF Core's implicit per-call transaction only. This means: if a document has 3 active profiles, the 3 `PersistDraftRequirementsAsync` calls in the `foreach` loop (`:167-172`) are **3 independent save operations**, each its own implicit transaction — a failure persisting profile 2's batch does not roll back profile 1's already-committed batch. This is true today, independent of any segmentation question.

**The key question — could results be persisted incrementally, or does the
current structure assume one complete requirement set per profile in a single
write?**

The current structure does not *require* a single write in the sense of an
atomic all-or-nothing transaction — each profile's persist call is already
its own independent `SaveChangesAsync`, and calling `PersistDraftRequirementsAsync`
multiple times for the *same* profile across multiple invocations would work
mechanically: the method reloads `existingTitles` fresh every call (`:541-545`),
so a second call for the same profile would see requirements persisted by a
first call and correctly dedup against them. **Nothing in the method signature,
its title-loading query, or its `Add`/`SaveChangesAsync` pattern assumes it is
the only call for that profile in the run.** What *does* assume single-shot,
whole-document input is entirely upstream of this method: the caller
(`ExecuteAsync`) only ever produces one `extractedRequirements` list from one
extraction outcome (§1) and only ever enters the persist loop after passing
the truncation/invalid-JSON/zero-requirements gates as a single pass/fail
unit. **What partial persistence would collide with is not
`PersistDraftRequirementsAsync` itself, but `ExecuteAsync`'s single-pass
control flow around it**: the four terminal-outcome branches (§1, `:116-164`)
are evaluated once per run against one `extraction` result, and the document's
own status fields (`LastIngestionStatus`, `LastIngestionErrorCode`,
`LastIngestionErrorMessage`) are single-valued per document (see
`RegulatoryDocument.cs:29-51` — no per-segment or per-call breakdown field
exists on the entity), so there is currently no field to record "segments
1-2 succeeded and persisted, segment 3 failed" — only a whole-run verdict.

---

## 4. Per-profile dedup check — cross-segment behaviour

**File:** `RequirementIngestionJob.cs:535-599`, confirmed also in
`docs/multi-document-regulation-recon.md:41` (independent same-day recon,
consistent finding).

- Dedup key: **exact match (case-insensitive, via `.ToLower()`) on `Title`, scoped to `RegulatoryProfileId`** (`:558`, `existingTitleSet` built at `:541-547` filtered `Where(r => r.RegulatoryProfileId == profile.Id)`).
- The set is seeded once per `PersistDraftRequirementsAsync` call (fresh DB query, `:541-545`) and then **updated in-memory as new items are added within that same call** (`existingTitleSet.Add(extracted.Title.ToLower())`, `:587`) — so within a single call, item 5 in the list is correctly deduped against item 2 if they share a title, without a second DB round-trip.
- **Would this naturally cover cross-segment duplicates if the same profile received requirements from N separate segment calls instead of one list?** Yes, *if* `PersistDraftRequirementsAsync` were invoked once per segment for the same profile (rather than once for one combined list, as it is today) — each invocation reloads `existingTitles` fresh from the DB (`:541-545`), which would include whatever the previous segment's call had already persisted. **The dedup mechanism itself needs no structural change to work across segments** — it already re-queries per call rather than trusting an in-memory snapshot carried from a prior call.
- **What it does not do, today or under a hypothetical segment-call pattern:** anything beyond exact (case-insensitive) title matching. Two segments describing the same underlying obligation with even slightly different title phrasing (plausible if the same topic is referenced in both a "Principle 2" summary section and a later detailed "Standard 2.3" section of the same document) would **not** be caught — both would persist as separate rows. This is the same limitation `multi-document-regulation-recon.md:42` already identified for the cross-*document* case; it applies identically within a single document split into segments, since the mechanism is the same string-exact check regardless of what produced the two candidate lists.
- **Conclusion: the existing per-profile dedup would extend to a segmented-call pattern automatically and correctly for exact-title repeats, but provides zero protection against near-duplicate titles arising from the same obligation being visible in more than one structural segment of the document** (e.g., an overview principle section and its detailed standard section both mentioning the same training requirement).

---

## 5. `DisplayOrder` / `Section` / `Principle` — population and segment-ordering coherence

**Entity fields:** `RegulatoryRequirement.cs:19-29` (`Section`, `SectionLabel`, `Principle`, `PrincipleLabel`, `DisplayOrder`); max lengths enforced at `RegulatoryRequirementConfiguration.cs:26-43` — `Section` 20 chars, `SectionLabel` 200, `Principle` 20, `PrincipleLabel` 200, `DisplayOrder` required `int`, no max/range constraint.

**How they're populated today** (`PersistDraftRequirementsAsync`, `:564-587`):

```
Section = extracted.Section?.Length > 20 ? extracted.Section[..20] : extracted.Section,
SectionLabel = extracted.SectionLabel?.Length > 200 ? extracted.SectionLabel[..200] : extracted.SectionLabel,
Principle = extracted.Principle?.Length > 20 ? extracted.Principle[..20] : extracted.Principle,
PrincipleLabel = extracted.PrincipleLabel?.Length > 200 ? extracted.PrincipleLabel[..200] : extracted.PrincipleLabel,
Priority = ValidatePriority(extracted.Priority),
DisplayOrder = extracted.DisplayOrder > 0 ? extracted.DisplayOrder : created + 1,
```

(`:573-578`, silent-truncation-to-max-length behaviour on `Section`/`SectionLabel`/`Principle`/`PrincipleLabel` unchanged from what `regulatory-extraction-size-recon.md:37` already documented — not re-litigated here, noted only because it's directly relevant to what "populated from the extraction" means).

- **All four of `Section`/`SectionLabel`/`Principle`/`PrincipleLabel` are taken verbatim (length-clamped) from the model's own JSON output** — there is no server-side derivation, lookup, or cross-check against a canonical section/principle list beyond what the prompt's "CANONICAL PRINCIPLE LABELS" instructs the model to self-apply (§2). The server trusts the model's self-reported classification entirely.
- **`DisplayOrder` fallback logic:** `extracted.DisplayOrder > 0 ? extracted.DisplayOrder : created + 1` (`:578`) — if the model supplies a positive `displayOrder`, it's used as-is; only if the model omits it or returns `0`/negative does the server assign a fallback of `created + 1`, where `created` is **the running per-`PersistDraftRequirementsAsync`-call counter of items actually persisted so far in that call** (`created++` at `:588`, only incremented on successful `Add`, not on skips). This fallback counter is scoped to a single call/profile — it does not read the profile's existing max `DisplayOrder` from the DB before assigning fallback values, so a second call against a profile that already has requirements (e.g., a prior partial-segment persist) would restart its fallback numbering at 1 for any item the model didn't supply a `displayOrder` for, **colliding with `DisplayOrder` values already persisted by an earlier call for the same profile.**

**Assembly-ordering implications of segmenting by principle/standard:**

- If the model is told (per §2 finding 2) to number sequentially "starting from 1" independently within each segment prompt, and no assembly-layer renumbering exists, **every segment's output would restart at `displayOrder = 1`** — direct collisions once persisted to the same profile (two, three, or N requirements all claiming `DisplayOrder = 1` etc.), since nothing currently deduplicates or offsets on `DisplayOrder` (dedup, per §4, is title-only).
- Conversely, **segmenting extraction to align with the document's own Principle → Standard structure could make `Principle`/`PrincipleLabel`/`Section`/`SectionLabel` *more* internally consistent per requirement** (today the model infers these fields from context across the whole document in one pass; if each call is scoped to exactly one principle's text, the model has less room to misclassify a requirement's principle) — but this is a plausible quality improvement, not something this recon can confirm without a live run; it is not a currently-observable fact.
- **No code path currently reads or reconciles `DisplayOrder` across calls or across profiles for the same document** — the only consumer-side ordering behaviour identifiable from this file is the fallback counter described above; how `DisplayOrder` is actually used for *display* (sorting in admin UI, browse page, etc.) is out of scope for this recon (not investigated — no controller/DTO/frontend files were read for this section).

---

## 6. Structural signal available in extracted PDF text to segment on

**File:** `PdfExtractionService.cs:26-116` (`ExtractTextAsync`)

- **Per-page text is extracted via PdfPig's `page.Text`** (`:47`) — this returns whatever raw reading-order text PdfPig recovers from the page's content stream. **No heading detection, font-size analysis, or layout structure extraction of any kind exists in this file** — confirmed by the method body containing no font/style/bounding-box inspection, only `page.Text` string access and length checks.
- **The only structural markers this service inserts itself** are page-boundary separators: `--- End of Page N ---` (`:62`, wrapped in blank lines at `:60-63`), appended after every page's text (or after a `[Page {N} - No extractable text]` placeholder for pages with no extractable text, `:49-54`). This means: **the one machine-reliable segmentation boundary available in the extracted text today is the page boundary** — nothing else is instrumented.
- **Whether "Principle N" / "Standard N.N" markers survive into the extracted text at all depends entirely on whether the source PDF's own page content contains that text as visible, extractable characters** — e.g., if a real HIQA PDF has "Standard 2.3" printed as a heading on the page, `page.Text` would very likely include that string somewhere in its output (PdfPig extracts visible text regardless of font size/weight), but **it would appear as plain text indistinguishable from body text** — no bold/heading semantic tag, no separate "this is a heading" marker of any kind. This service does not parse or preserve visual heading emphasis.
- **Conclusion: structural segmentation directly on the extracted-text output (post-`PdfExtractionService`) is feasible only via regex/pattern matching against whatever heading-style text strings happen to appear literally in the flat text** (e.g., searching for lines matching `"Standard \d+\.\d+"` or `"Principle \d+"` patterns) **— there is no pre-parsed structural index (no page-to-heading map, no outline/bookmark extraction, no font-size-based heading detection) to segment on today.** PdfPig itself is capable of more (bounding boxes, font info are available on `Letter`/`Word` objects it exposes), but **none of that capability is currently used** — `ExtractTextAsync` only ever calls `page.Text`, the simplest whole-page string accessor (`PdfExtractionService.cs:47`). Whether a real document's own heading text is regular/parseable enough for regex-based segmentation to work reliably was not testable within this recon's read-only, no-run scope — this recon establishes what's structurally available, not whether it would work well against the actual document.
- **No document outline/bookmark/table-of-contents extraction exists anywhere in this file or elsewhere in the codebase** — confirmed by this file being the sole `PdfExtractionService` implementation (per the earlier grep for `class PdfExtractionService`) and containing no calls to any PdfPig bookmark/outline API.

---

## 7. Retry and cost shape

**File:** `RequirementIngestionJob.cs`

- **Current retry logic:** exactly one retry, only triggered by a failed/truncated first attempt, using the same full `documentText` with a stricter appended instruction (`:391`, described in §1). **Worst case today: 2 Claude calls per ingestion run.**
- **Segmenting into N calls would multiply this to up to 2N calls per run** if the existing per-attempt retry pattern were preserved unchanged per segment (each segment independently eligible for its own 1-retry). No code currently caps or coordinates retries across multiple logical "attempts" belonging to different segments — because no segment concept exists yet.
- **Cost tracking on the current single call:** `CallClaudeAsync` calls `_aiUsageLogger.LogAsync(...)` once per HTTP call it makes (`:478-487`), passing `AiOperationCategory.RequirementIngestion`, the model/token counts parsed from that specific response, `isSystemCall: true`, and `referenceEntityId: documentId` (`:486`) — **note this is logged once per `CallClaudeAsync` invocation, not once per document run.** Since `ExtractRequirementsViaClaudeAsync` already calls `CallClaudeAsync` up to twice today (initial + retry, `:372` and `:393`), **the logging mechanism already accommodates multiple calls per document without modification** — it would naturally log N (or up to 2N) rows for a segmented run, all sharing the same `referenceEntityId = documentId`, with no per-segment identifier on the log row itself (`IAiUsageLogger.LogAsync`'s parameter list per the call site has no segment/chunk index parameter — confirmed by the call at `:478-487` passing only the parameters listed above).
- **No pre-flight cost estimate or approval gate exists for regulatory ingestion**, unlike the Corpus-run feature's `CostEstimationService` (estimate → confirm >€3 → SuperUser approval >€10, per CLAUDE.md Note 8, independently confirmed at `docs/multi-document-regulation-recon.md:43,84`). This recon did not re-verify `CostEstimationService`'s internals (out of scope — that service belongs to the Corpus feature, referenced only as a comparison point per investigation item 9, §9 below) but confirms via the same grep search performed for §9 that no equivalent gate exists in `RequirementIngestionJob.cs`, `RequirementIngestionService.cs`, or `RegulatoryIngestionController.cs` (no `CostEstimation`, `estimate`, or `SuperUser` approval string literals found in any of those three files).

---

## 8. Idempotency / re-run behaviour

**Files:** `RequirementIngestionJob.cs`, `RegulatoryDocument.cs`

- **Hangfire's own automatic retry is effectively inert for this job.** `[AutomaticRetry(Attempts = 1)]` (`:59`) only fires on an unhandled exception propagating out of `ExecuteAsync`. But the outer `try/catch(Exception ex)` (`:69` opens the try, `:181-194` is the catch) catches essentially everything and calls `MarkFailedAsync` rather than rethrowing (`:190-193`) — the comment at `:187-189` states this is intentional ("Don't rethrow — Hangfire job should not fail noisily"). **This means Hangfire's `AutomaticRetry` attribute has no unhandled exception to act on in the vast majority of real failure cases** — a truncated extraction, an invalid-JSON response, a fetch failure, etc. are all caught internally and turned into a `Failed`/`Skipped` status write, not an exception. Re-running ingestion after a `Failed` status is therefore a **manual action** — re-calling `POST /api/regulatory/documents/{id}/ingest` (per `multi-document-regulation-recon.md:55`), not something Hangfire retries automatically.
- **`RegulatoryDocument`'s ingestion-tracking fields are single-valued, whole-run fields, not per-segment:** `LastIngestedAt` (`RegulatoryDocument.cs:29`), `LastIngestionStatus` (`:37`), `LastIngestionErrorMessage` (`:43`), `LastIngestionErrorCode` (`:51`) — each one holds exactly one value representing the outcome of the *most recent full run*. **There is no field anywhere on this entity, or on any related entity found in this recon, that records "which segments of a multi-segment extraction succeeded."** A hypothetical segmented rebuild that failed partway through (e.g., segment 3 of 4) and wrote its per-segment progress nowhere durable would have no way to answer "what already happened" on the next attempt except by re-deriving it from what's already in `RegulatoryRequirements` for that profile.
- **What a manual re-run does today, given only whole-document dedup exists:** re-triggering ingestion calls Claude fresh over the same full document text again (no caching of prior extraction results anywhere in this file — confirmed by no cache/memoization structure in `ExtractRequirementsViaClaudeAsync` or its callers). The resulting new `extractedRequirements` list is passed through `PersistDraftRequirementsAsync` again per profile, which **would correctly skip re-persisting any titles that exactly match what's already in the DB for that profile** (§4) — but since the request has no `temperature` override (§1, defaults to the API's `1.0`), **there is no guarantee the model reproduces byte-identical titles on a second full run**, so a naive re-run today can already produce partial duplicate sets if wording drifts between calls. This is a pre-existing idempotency gap independent of segmentation.
- **Extrapolated to a hypothetical segmented design (stated as inference, not an observed fact, since segmentation doesn't exist yet):** given (a) dedup already re-queries fresh per persist call and would naturally skip byte-identical repeats (§4), (b) `DisplayOrder` fallback numbering restarts per call with no cross-call awareness (§5), and (c) no per-segment progress is recorded anywhere on `RegulatoryDocument`, **a partial-failure retry today would have no record of "segments 1-2 already ran" to consult — a re-run's only signal of prior partial progress would be indirectly inferable by querying what `RegulatoryRequirement` rows already exist for the profile, which the dedup step already does, but nothing currently maps a persisted requirement back to "which segment produced it"** (no segment-index field exists on `RegulatoryRequirement`, confirmed by the full field list at `RegulatoryRequirement.cs:11-45`, and no metadata/JSON column of any kind on the entity to carry one). Whether re-extracting the already-successful segments 1-2 on retry (rather than skipping them) is wasteful-but-safe (thanks to dedup) or risky (thanks to non-deterministic title drift under `temperature: 1.0`) is exactly the class of fact this recon surfaces for a future partial-failure policy decision — not something resolved here.

---

## 9. Existing segmented/chunked AI-extraction patterns elsewhere in the codebase

Two reference implementations were found — both do "many focused Claude calls,
assembled/persisted incrementally," structurally the closest existing analogue
to what a segmented extraction rebuild would need, though neither operates on
*document segments* specifically (one is per-section on already-segmented
content; the other is per-corpus-entry).

### a) `AiSlideshowGenerationService` — truncation detection (already the reference for the `stop_reason` mechanism used in §1)

**File:** `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Slideshow/AiSlideshowGenerationService.cs`

- Has its own `ExtractStopReason(responseBody)` helper (`:441-445`) re-parsing the raw response body for `stop_reason`, exactly mirrored by `RequirementIngestionJob`'s own copy (`RequirementIngestionJob.cs:492-500`, whose comment explicitly says so).
- Sets `wasTruncated = stopReason == "max_tokens"` (`:430`) and, on truncation, retries once with "efficiency instructions" appended (three separate call sites do this: `:109-121`, `:212-224`, `:334-346` — for slideshow-from-PDF, slideshow-from-transcript, and slideshow-from-sections respectively). **This service does not chunk its *input* or split work across multiple independent content segments** — it generates one HTML document per call, same as `RequirementIngestionJob` generates one requirements array per call. Its `max_tokens` is `32000` (`:75, 184, 306`), not `8192`. **This is a same-shape-of-problem reference (truncation detection + single retry) but not a segmentation reference** — it does not split a large input into N independent generation calls and assemble the results.

### b) `TranslationValidationJob` — genuine per-unit call + incremental persist pattern (closer structural analogue)

**File:** `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/TranslationValidationJob.cs`

- Iterates `sections` in a `for` loop (`:214-...`), calling `_validationService.ValidateSectionAsync(...)` **once per section** (`:227-239`) — each call is an independent Claude-backed operation (multi-round back-translation consensus per CLAUDE.md's TransVal documentation) scoped to exactly one section's text, not the whole talk.
- **Persists incrementally, per section, inside the loop** — e.g. `await _dbContext.SaveChangesAsync(cancellationToken)` at `:252` immediately after processing one section's result, before moving to the next iteration of the `for` loop. This is a genuine "persist unit N before starting unit N+1" pattern, unlike `RequirementIngestionJob`'s "accumulate the whole list, then persist per profile" pattern (§3).
- **`ValidateSectionAsync` itself is documented (CLAUDE.md Phase 13 notes, "Upsert pattern for TranslationValidationResult rows") as querying for an existing `{ValidationRunId, SectionIndex}` row and updating in place rather than delete-then-insert** — i.e. each section's persistence is independently idempotent and re-runnable by key, which is exactly the property §8 above identifies as missing for `RegulatoryRequirement` (no segment-index key exists to upsert against).
- **This is the closest available reference implementation in the codebase for "split extraction into N independently-callable, independently-persistable units, keyed so a retry of one unit doesn't require re-doing the others."** It operates on already-segmented input (talk sections are a pre-existing DB structure, not something the job itself has to discover/split from raw text), which is a meaningfully different starting point than segmenting a flat PDF-extracted string (§6) — but the call-per-unit / persist-per-unit / idempotent-upsert-per-unit shape is directly transferable as a pattern.

### c) `CorpusRunJob` — per-entry loop with per-entry persistence and result caching

**File:** `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/CorpusRunJob.cs`

- Loops over corpus entries with per-entry `SaveChangesAsync` calls (e.g. `:199`, `:245`) rather than one batch save at the end.
- Uses a `ProviderResultCache` keyed by `(CorpusEntryId, Provider, ProviderVersion)` (per CLAUDE.md Note 8) to avoid re-calling providers for entries that haven't changed — `LoadProviderCacheAsync` (`:471-...`) and the cache-population logic (`:484-517`) are a second reference for "avoid redoing already-completed units on a re-run," conceptually the caching analogue to what §8 identifies as absent for segmented regulatory extraction (no cache/skip mechanism keyed by segment exists today).
- This job's `persist = false` dry-run mode (per CLAUDE.md Note 8, used for cost-estimation smoke tests before a real run) and its two-step estimate→confirm→approval cost gate (§7 above) are the reference implementation `multi-document-regulation-recon.md:43,84,148` already points to for the *cost-gating* half of a batch-AI-calls feature — reconfirmed here as still the only such gate in the codebase (no equivalent found in the regulatory ingestion path, per §7).

**No code anywhere in the repository currently splits a single source document's
extracted text into structural segments (by heading, section marker, or page
range) before an AI call, and reassembles/orders results afterward** — this
was confirmed by the same searches performed for `regulatory-extraction-size-recon.md:71`
(no alternate ingestion path found) plus additional greps run for this recon
(`for.*chunk|segment|SplitBy` and `multiple.*claude.*call|aggregat|assembl.*result`
across `Infrastructure/Services` and `src`) — every hit was one of the three
services covered above, `ContentCreationSessionService`/`RegulatoryScoreService`/
`ContentExtractionService`/`TranscriptService`/`InspectionReportService`/
`ArtefactScanService`/`ContentGenerationService`/`WordDiffService`/
`ToolboxTalkExportService` (all matched on unrelated uses of the word "segment"
or "chunk" in comments/variable names, not chunked-call architecture — not
individually inspected further since none appeared in the multi-call-pattern
grep and this recon's scope is the closest reference implementations, not an
exhaustive audit of every partial keyword match), and the TransVal/Corpus/
Slideshow trio already covered above in (a)-(c).

---

## Summary table

| # | Question | Answer | Key evidence |
|---|---|---|---|
| 1 | Current flow post-guardrail | Load → validate URL → fetch/extract text → 1-2 Claude calls (whole doc) → 4 terminal outcomes (Failed×3, Skipped×1, else Success) → persist per active profile | `RequirementIngestionJob.cs:61-195`, terminal branches `:116-164` |
| 2 | Prompt whole-document assumptions | Two explicit: "extract all requirements", sequential displayOrder "starting from 1"; field-spec + rules are otherwise segment-agnostic | `RequirementIngestionJob.cs:414, 424` |
| 3 | Persistence unit | One `SaveChangesAsync` per profile per run today; method itself (fresh dedup query per call) would tolerate multiple calls per profile; the caller's single-pass control flow and the document's single-valued status fields are what assume one complete set | `RequirementIngestionJob.cs:166-172, 535-599`; `RegulatoryDocument.cs:29-51` |
| 4 | Cross-segment dedup | Exact-title dedup naturally extends across multiple calls to the same profile (fresh query per call); zero protection against near-duplicate titles from different segments describing the same obligation | `RequirementIngestionJob.cs:541-547, 558` |
| 5 | DisplayOrder/Section/Principle across segments | All four populated verbatim from model output, length-clamped; DisplayOrder fallback counter restarts at 1 per call with no DB-max lookback — would collide across segments if each segment's prompt independently numbers from 1 | `RequirementIngestionJob.cs:573-578` |
| 6 | Structural signal in extracted text | Only page-boundary markers (`--- End of Page N ---`); no heading/font/outline extraction; any "Principle N"/"Standard N.N" text survives only as plain flat text if present in the source PDF at all | `PdfExtractionService.cs:47, 60-63` |
| 7 | Retry/cost multiplication | Current worst case 2 calls/run; segmenting to N would multiply to up to 2N if the existing per-attempt retry pattern is preserved per segment; usage logging already handles multiple calls per document (logs per `CallClaudeAsync` call); no cost-estimate/approval gate exists | `RequirementIngestionJob.cs:372, 393, 478-487` |
| 8 | Re-run/idempotency safety | Hangfire's AutomaticRetry rarely fires (exceptions are caught internally); manual re-run is the real retry path; no per-segment progress field exists anywhere; dedup provides partial, non-deterministic-title-drift-vulnerable protection against duplicate re-persistence | `RequirementIngestionJob.cs:59, 181-194`; `RegulatoryDocument.cs:29-51` |
| 9 | Existing segmented/multi-call AI patterns | `TranslationValidationJob` (per-section call + incremental idempotent-upsert persist) is the closest structural reference; `CorpusRunJob` (per-entry persist + result caching + cost gate) is the closest cost/caching reference; `AiSlideshowGenerationService` is the direct source of the `stop_reason`/truncation-retry mechanism already mirrored into this job; no existing code splits one document's text into structural segments before calling Claude | `TranslationValidationJob.cs:214-252`; `CorpusRunJob.cs:199,245,471-517`; `AiSlideshowGenerationService.cs:430,441-445` |
