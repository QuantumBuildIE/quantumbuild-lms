# Bulk-import section-parse failure recon

Read-only recon. No code changed. All facts below are `file:line`-cited against
the state of `transval` at commit `23365a1`.

## Summary

Bulk-imported SOPs occasionally fail to parse because Claude's response to the
section-generation prompt contains no `[` at all (a complete miss on the "JSON
array only" instruction), and `ContentParserService.ParseContentAsync` now
reports that honestly instead of silently committing zero sections (recent fix,
commit `1b497a8`). The bulk path and the new learning-wizard's PDF parse path
are **byte-identical** in every respect that could produce this — same handler,
same extraction service, same prompt, same model, same default flags. Nothing
found in this recon distinguishes bulk from wizard as more failure-prone by
construction; if bulk hits it more in practice, the most likely explanation is
volume (SOP batches import many PDFs unattended in one job) plus PDFs whose
raw-extracted text is unusually messy (dense tables, multi-column layouts,
scanned pages), not a difference in the call itself.

**The actual failing Claude response is not captured anywhere** — not logged,
not persisted to `AiUsageLog`. This is the single biggest gap: without seeing
what the model actually returned, "refusal" vs. "off-format" vs. "malformed
JSON" vs. "input was garbage" cannot be distinguished, and the current JSON
extraction is already reasonably fence/preamble-tolerant, so a robustness fix
alone is a guess, not a diagnosis.

---

## A. The input to the parse

### A1. What content is fed to the LLM, and how clean is it

For a PDF-mode talk (both bulk and wizard), the pipeline is:

1. `ParseToolboxTalkContentCommandHandler.HandlePdfAsync` calls
   `_pdfExtractionService.ExtractTextFromUrlAsync(talk.SourceFileUrl, ct)`
   ([ParseToolboxTalkContentCommandHandler.cs:99](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ParseToolboxTalkContent/ParseToolboxTalkContentCommandHandler.cs#L99)).
2. `PdfExtractionService.ExtractTextAsync` opens the PDF with **PdfPig**
   (`UglyToad.PdfPig`) and concatenates `page.Text` for every page verbatim —
   no table detection, no header/footer stripping, no whitespace
   normalisation, no OCR
   ([PdfExtractionService.cs:37-64](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Pdf/PdfExtractionService.cs#L37-L64)).
   It inserts its own structural markers into the extracted text:
   - `[Page {N} - No extractable text]` for pages PdfPig can't get text from
     (line 53) — i.e. scanned/image pages are silently represented as a
     literal bracketed string sitting inside the "raw text", not skipped.
   - `--- End of Page {N} ---` after every page (line 62), regardless of
     content.
   - `PdfPig.page.Text` itself is a naive reading-order concatenation with no
     table/column awareness — a two-column SOP or a table of PPE requirements
     comes out as visually-scrambled interleaved text; page numbers and
     running headers/footers are included as ordinary text since PdfPig has
     no way to distinguish them.
   - The only quality gate is a coarse one: if the *whole document* has no
     letters at all, extraction fails outright (line 69-77). A document that
     is 95% garbled table noise and 5% real sentences still "succeeds".
3. `HandlePdfAsync` caches `extractResult.Text` onto `talk.ExtractedPdfText`
   and passes the **exact same string**, unmodified, straight into
   `_contentParserService.ParseContentAsync(extractResult.Text!, ...)`
   ([ParseToolboxTalkContentCommandHandler.cs:108-113](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ParseToolboxTalkContent/ParseToolboxTalkContentCommandHandler.cs#L108-L113)).
   No cleanup step exists between extraction and prompt construction anywhere
   on this path.
4. `ContentParserService.ParseContentAsync` drops that raw text verbatim into
   `SectionGenerationPrompts.BuildSectionPrompt(content: rawText, ...)` under
   a `CONTENT TO ANALYZE:` heading
   ([ContentParserService.cs:83-89](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentParserService.cs#L83-L89),
   [SectionGenerationPrompts.cs:113-114](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Prompts/SectionGenerationPrompts.cs#L113-L114)).

So: **yes**, the same possibly-messy extracted text (page-number noise,
`[Page N - No extractable text]` literal markers, scrambled table/column
text) is what the model sees, unfiltered. This is a plausible contributor to
off-format output — a model asked to "return only a JSON array" for input
that is itself garbled may respond with an apology/explanation instead of
attempting structure, but this recon cannot confirm that without B3/B4 (see
below).

### A2. Bulk vs. wizard — is the LLM call different?

**No — they are the same call.** `ParseToolboxTalkContentCommand` has exactly
two call sites in the entire codebase:

| Caller | File:line | Path |
|---|---|---|
| `POST /{id}/parse` (new learning-wizard) | [ToolboxTalksController.cs:439](../src/QuantumBuild.API/Controllers/ToolboxTalksController.cs#L439) | Wizard |
| `BulkSopImportJob.ProcessItemAsync` | [BulkSopImportJob.cs:275-276](../src/Core/QuantumBuild.Core.Infrastructure/Jobs/BulkSopImportJob.cs#L275-L276) | Bulk |

Both route into the identical `ParseToolboxTalkContentCommandHandler.HandlePdfAsync`
([ParseToolboxTalkContentCommandHandler.cs:90-131](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/ParseToolboxTalkContent/ParseToolboxTalkContentCommandHandler.cs#L90-L131)),
same `IPdfExtractionService`, same `IContentParserService`, same Claude Sonnet
model (`aiProviders.Value.Anthropic.Models.Sonnet`,
[ContentParserService.cs:39](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentParserService.cs#L39)),
same `max_tokens = 8000` ([ContentParserService.cs:94](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentParserService.cs#L94)).

The only per-call variable is `talk.PreserveSourceWording`, which selects
between two prompt branches in `BuildSectionPrompt` (verbatim-copy vs.
rewrite-and-summarise,
[SectionGenerationPrompts.cs:31-77 vs 79-114](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Prompts/SectionGenerationPrompts.cs#L31-L114)).
Bulk import never sets `PreserveSourceWording` on its
`InitialiseToolboxTalkCommand` call
([BulkSopImportJob.cs:251-259](../src/Core/QuantumBuild.Core.Infrastructure/Jobs/BulkSopImportJob.cs#L251-L259)),
so it resolves through
`request.PreserveSourceWording ?? tenantSettings?.DefaultPreserveSourceWording ?? false`
([InitialiseToolboxTalkCommandHandler.cs:120](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/InitialiseToolboxTalk/InitialiseToolboxTalkCommandHandler.cs#L120))
— `false` unless the tenant has explicitly changed its default. A wizard user
hits the same default unless they flip the Step-1 toggle. So in the common
case both paths use the **same "rewrite and summarise" prompt branch**, not
different prompts.

`minimumSections` is likewise the same single config value
(`ContentGenerationSettings.MinimumSections`, default `2`,
[ContentGenerationSettings.cs:14-20](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Configuration/ContentGenerationSettings.cs#L14-L20))
read directly by `ContentParserService`
([ContentParserService.cs:86](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentParserService.cs#L86))
on both paths — this was the subject of the immediately-preceding commit
`23365a1` ("Make minimumSections a single config value aligned across all
paths"), so as of this recon there's no divergence there either.

**What does differ operationally, not in the call itself:**

- **Volume / unattended batch.** Bulk import processes an entire ZIP of PDFs
  in one Hangfire job with no human watching each result in real time
  ([BulkSopImportJob.cs:114-119](../src/Core/QuantumBuild.Core.Infrastructure/Jobs/BulkSopImportJob.cs#L114-L119)); the wizard parses one PDF per user
  session with a human present. If off-format responses are rare-but-nonzero
  per call, bulk simply rolls the dice more times per session and a human
  isn't there to immediately retry.
- **Document population.** Bulk import is aimed at customers' existing SOP
  libraries — these are more likely to be legacy scanned/converted documents,
  dense regulatory tables, or PDFs originally designed for print (multi-column,
  headers/footers) than the video/PDF a single admin manually curates through
  the wizard. This is a hypothesis about *input shape*, not the code path —
  nothing in the code differentiates "bulk" PDFs from "wizard" PDFs, but the
  population differs by how the feature is used.
- **No mid-flight retry on bulk** (see C6 below) — a wizard user who hits an
  off-format response can just click Retry; a bulk item that hits it fails
  permanently for that run.

---

## B. Capturing the actual failure (the key diagnostic)

### B3/B4. Is the model's actual response ever captured?

**No — not anywhere on the failure path, and not in the AI usage log.**

- On a **zero-sections** result (`sections.Count == 0`, i.e. no `[` found at
  all), `ContentParserService` logs only the input mode, not the response
  text:
  ```
  _logger.LogWarning(
      "[ContentParserService] Claude response for {InputMode} parse contained no sections",
      inputModeHint);
  ```
  ([ContentParserService.cs:149-151](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentParserService.cs#L149-L151)).
  `parsed.ContentText` (the actual model output) is in scope at this point
  but is never written to the log.
- On a **JSON parse exception** (brackets found, but content between them
  isn't valid JSON — e.g. truncated, trailing commentary that broke the
  bracket-matching, or genuinely malformed), the outer `catch (JsonException
  ex)` logs only the exception, not the response body:
  ```
  _logger.LogError(ex, "[ContentParserService] Failed to parse AI response");
  ```
  ([ContentParserService.cs:181-188](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentParserService.cs#L181-L188)).
  `responseBody` (line 113) and `parsed.ContentText` are both out of scope
  by the time this catch block would need them for a useful log line — they
  were declared inside the `try`.
- `BulkSopImportJob.ProcessItemAsync` receives only
  `parseResult.Errors` (the `ContentParseResult.ErrorMessage`, a fixed
  human-readable string, never the raw model text) and logs/records that as
  the item's `FailureReason`
  ([BulkSopImportJob.cs:278-285](../src/Core/QuantumBuild.Core.Infrastructure/Jobs/BulkSopImportJob.cs#L278-L285)).
  Whatever Claude actually said is gone by this point — there's no lower
  layer that kept it.
- **`AiUsageLog` captures only billing metadata**: `ModelId`, `InputTokens`,
  `OutputTokens`, `CalledAt`, `IsSystemCall`, `ReferenceEntityId`
  ([AiUsageLog.cs:9-41](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Entities/AiUsageLog.cs#L9-L41)).
  No content/response column exists. It cannot be mined retroactively to see
  what a failed call returned — token counts alone don't reveal refusal vs.
  malformed-JSON vs. truncation.

**Where a diagnostic log should go:** immediately after `parsed =
AnthropicResponseParser.Parse(responseBody)` at
[ContentParserService.cs:126](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentParserService.cs#L126),
both failure branches (`sections.Count == 0` at line 143, and the
`catch (JsonException ex)` at line 181) need the raw `parsed.ContentText` /
`responseBody` logged (truncated, per the `TruncateForLogging` pattern
`PreFlightScanService` already uses — see C5). Until that exists, there is no
way — live or retroactive — to see an actual failing response; every fix
proposed in section D is a guess against that gap.

---

## C. Handling / robustness

### C5. How robust is the current JSON extraction, and is it recovering wrapped-but-valid JSON already?

`ParseSectionsFromContentText` does a plain substring extraction — no fence
stripping, just bracket search:

```csharp
var jsonStart = textContent.IndexOf('[');
var jsonEnd = textContent.LastIndexOf(']');
if (jsonStart >= 0 && jsonEnd > jsonStart) { ... JsonDocument.Parse(jsonArray) ... }
```
([ContentParserService.cs:246-252](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentParserService.cs#L246-L252)).

This is **already tolerant of markdown fences and preamble/trailing prose**,
because a fence (` ```json ... ``` `) uses backticks, not square brackets — the
first `[` and last `]` in the whole response text are found regardless of
what surrounds them. A response like:

```
Sure, here are the sections:
```json
[ {...}, {...} ]
```
Hope that helps!
```

would extract cleanly. This matters directly for root-causing: **"JSON
present but not extracted" is not the likely defect class here** — the
extraction already recovers wrapped/prefixed valid JSON. The two failure
modes that *do* survive this extraction are:

1. **No `[` anywhere in the response** (true refusal, filtered reply, or a
   reply that's prose-only with no attempt at the array) → `sections.Count ==
   0` → the "no sections" branch (line 143-158). This is what the task
   description names as the observed symptom.
2. **`[` and `]` present, but the JSON between them is invalid** — e.g. the
   model included a second, unrelated `]` later in trailing commentary
   (rare, since prose is very unlikely to contain a literal `]`), or emitted
   genuinely malformed/truncated JSON (missing closing brace, unescaped
   quote inside a `"content"` string copied from messy PDF text, response
   cut off at `max_tokens = 8000` mid-array for an unusually long document)
   → `JsonDocument.Parse` throws → caught by the generic
   `catch (JsonException ex)` at line 181, a **different failure path** from
   the one described in the task (that one still finds brackets, it just
   can't parse what's inside them).

Contrast with `PreFlightScanService` pre-fix
(commit `f6dfb53`, [PreFlightScanService.cs](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Validation/PreFlightScanService.cs)):
before that fix, `ParseResponse` did a **leading-fence-strip only** (stripped
` ``` ` from the very start of the trimmed string, then handed the remainder
straight to `JsonDocument.Parse` with no brace-span search) — so a
preamble/trailing-commentary response threw an unhandled `JsonException`
that wasn't even caught by a dedicated handler (it fell into the generic
`catch (Exception ex)`, logging only target language/sector, no raw response
— identical gap to `ContentParserService` today). The fix added: (a) an
outermost `{...}`-span search after fence-stripping (`ExtractJsonPayload`,
[PreFlightScanService.cs:223-241](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Validation/PreFlightScanService.cs#L223-L241)),
and — the part actually missing from `ContentParserService` — (b) a
**dedicated `catch (JsonException ex)` that logs the truncated raw response**
before falling back
([PreFlightScanService.cs:49-58](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Validation/PreFlightScanService.cs#L49-L58),
`TruncateForLogging` at
[PreFlightScanService.cs:69-74](../src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Validation/PreFlightScanService.cs#L69-L74)).

So `ContentParserService`'s bracket-search is *already* at parity with (in
fact simpler than, but not worse than) `PreFlightScanService`'s post-fix
brace-search for the "wrapped JSON" case — the class of bug
`f6dfb53` fixed (unhandled crash on JSON-parse failure) is a different bug
from what's described here (silent/handled zero-sections on no-bracket-found,
already fixed honestly by `1b497a8`). What `ContentParserService` is missing
is specifically `PreFlightScanService`'s **diagnostic logging half** of that
fix, not its extraction-robustness half.

### C6. Is there any retry on parse failure?

- **`ContentParserService` / handler**: no automatic retry. One Claude call,
  one outcome, no backoff-and-retry loop (contrast with `ResiliencePolicies`
  Polly retries, which only cover transport-level failures — timeouts,
  5xx/429 — not a 200 response with unusable content; a message-parsing
  failure doesn't trigger a Polly retry because the HTTP call itself
  succeeded).
- **Wizard**: the new-wizard parse page wires a manual retry — `onRetry={refetch}`
  ([parse/page.tsx:56](../web/src/app/(authenticated)/admin/toolbox-talks/learnings/%5BtalkId%5D/parse/page.tsx#L56))
  — so a human hitting a failed parse can immediately re-invoke `POST
  /{id}/parse`, which re-runs extraction and re-calls Claude fresh. This
  gives natural variance-based recovery for a one-off bad response, at the
  cost of a manual click.
- **Bulk**: **no retry of any kind for a single failed item.**
  `BulkSopImportJob.ProcessItemAsync` calls `ParseToolboxTalkContentCommand`
  once; on failure it records `Failed(file, reason)` and moves to the next
  PDF in the ZIP
  ([BulkSopImportJob.cs:275-285](../src/Core/QuantumBuild.Core.Infrastructure/Jobs/BulkSopImportJob.cs#L275-L285)).
  The only recovery path is re-running the **entire session** via the
  `IsRerun` mechanism, which the user triggers manually after seeing the
  failure report; there's no per-item automatic retry within a run
  ([BulkSopImportJob.cs:39](../src/Core/QuantumBuild.Core.Infrastructure/Jobs/BulkSopImportJob.cs#L39) —
  `[AutomaticRetry(Attempts = 0)]` is at the *job* level and is deliberately
  disabled per the class doc comment, to avoid re-creating already-succeeded
  learnings; it says nothing about a per-item Claude-call retry, which
  doesn't exist at all).

So bulk is structurally the *least* forgiving of an occasional off-format
response: one bad roll of the dice permanently fails that PDF for the run,
with no human in the loop to notice and retry immediately the way the wizard
allows.

---

## D. Fix options (not implemented — mapped to root cause)

| Option | What it does | Fits which root cause | Evidence for/against, per this recon |
|---|---|---|---|
| **(a) More robust JSON extraction** (fence-strip, brace/bracket-span search) | Recovers valid JSON the model wrapped in fences/prose | "JSON present but not extracted" | **Recon finding: this is likely NOT the bug.** Current bracket search already tolerates fences/preamble (see C5). Adding fence-stripping like `PreFlightScanService`'s would be a no-op for the fenced case already handled, and wouldn't touch the "no `[` at all" symptom the task describes. Worth doing only as defence-in-depth alongside (b)/(d), not as the primary fix. |
| **(b) Retry on failure** (re-call Claude once or twice on empty/invalid result) | Handles transient variance — an occasional bad roll on an otherwise-fine input | Any root cause where failures are intermittent rather than deterministic for a given input | Strong candidate regardless of root cause, since bulk currently has zero retry (C6) while the wizard has manual retry that likely already "fixes itself" on a second attempt for many cases — that's indirect evidence some fraction of failures are non-deterministic. Cheap, low-risk, doesn't require knowing *why* yet. |
| **(c) Input cleanup / better PDF extraction** (strip `--- End of Page N ---` / `[Page N - No extractable text]` markers, detect and clean table/column scrambling, trim to a token-safe length) | Removes noise that may itself trigger off-format replies or push the response past `max_tokens = 8000` mid-array | "Messy input" root cause — plausible given A1 findings (raw PdfPig text, no cleanup, structural markers left in) | Plausible contributor per A1, but unconfirmed — needs B3/B4 (actual failing responses) to know whether failures correlate with page count / table-heavy documents / scanned-page markers before investing here. |
| **(d) Handling refusals / filtered content** (detect a refusal-shaped response, surface it distinctly to the reviewer/admin rather than a generic "no sections" error, possibly reframe the prompt to avoid safety-filter triggers for hazard/chemical/weapons-adjacent SOP content) | Addresses model non-compliance caused by content moderation rather than instruction-following failure | "Refusal / safety filter" root cause | Cannot be confirmed or ruled out at all without B3/B4 — workplace safety SOPs routinely discuss hazards, chemicals, emergency procedures, which is exactly the content class most likely to graze a safety filter, so this is a live hypothesis, not a stretch. |

**All four options are gated on B3/B4.** The zero-sections symptom in the
task description (no `[` at all) rules out (a) as the primary fix — the
extraction already handles the "valid JSON but wrapped" case. Between (b),
(c), and (d), this recon cannot rank them further without the raw failing
response text, which nothing in the current system captures. The
recommended first change (not applied here, per scope) is adding the
diagnostic logging described in B3/B4 — cheap, low-risk, and it is the only
way to turn (b)/(c)/(d) from guesses into a targeted fix.
