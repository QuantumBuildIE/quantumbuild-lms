# Translation/Validation Sentry Errors — Recon: Gemini 404 + Pre-Flight JSON Parse Failure

**Date:** 2026-08-25
**Type:** Read-only recon. No code changed.

---

## Headline

1. **Gemini model fix is complete.** The only code site that resolves a Gemini model
   identifier (`GeminiTranslationService.cs:32`) reads it from
   `AIProviderOptions.Gemini.Models.Flash`, which is fail-fast validated at startup
   (`AIProviderOptionsValidator.cs:20-21`). Both tracked local config files
   (`appsettings.json:36`, `appsettings.Development.json:30`) are set to
   `"gemini-2.0-flash"`. A repo-wide grep found **zero** remaining references to
   `gemini-1.5-flash` (or any `1.5-*` Gemini model) anywhere in source, config, or
   docs. There is exactly one consumer of this config key — no other hardcoded/stray
   Gemini model references exist. **The only thing this recon cannot confirm is
   whether the Railway Production/Development environment variable
   `AIProviders__Gemini__Models__Flash` is actually set to `gemini-2.0-flash`** —
   that lives outside the repo and this session has no Railway access. If it's unset
   or still holds the old value, the app would either fail to start (validator) or
   still 404 (stale value) — the fix being *complete in code* doesn't guarantee it's
   *deployed correctly*.

2. **The JSON parse failure is a symptom, not the root cause.** The pre-flight scan's
   input text is not a separate "transcript" variable — it is `ToolboxTalkSection.Content`,
   the **actual stored, employee-facing learning content**. The `[0:00]`, `[0:02]`
   timestamps in the failing payload are direct proof that a video's raw
   `[mm:ss]`-prefixed transcript text leaked into that real, published section content
   during AI section generation, and nothing anywhere in the pipeline strips it back
   out before the content is used for scanning, translation, or display.

3. **Blast radius is wider than the scan.** The same polluted `ToolboxTalkSection.Content`
   that breaks the pre-flight scan is: (a) what employees read as their actual training
   content, (b) what gets sent to AI translation (so translated content and back-translation
   scoring both ingest it too), and (c) what the pre-flight scan chokes on. This is not a
   scan-only bug.

4. **Root cause location:** `SectionGenerationPrompts.cs` — the AI section-generation
   prompt (used to turn a video transcript into `ToolboxTalkSection.Content`) is given a
   transcript that has `[mm:ss]`-prefixed lines (produced by
   `TranscriptService.cs:108`, `FormatTimestamp`), with **no instruction anywhere telling
   the model to exclude/ignore the timestamp markers**, and no post-processing step that
   strips them from the model's output before it's persisted as `Content`.

---

## A. Gemini model — confirm the 404 fix is complete

### A1. Single source of truth for the Gemini model string

- `GeminiTranslationService.cs:32` — `_geminiModel = aiProviders.Value.Gemini.Models.Flash;`
  (constructor-injected from `IOptions<AIProviderOptions>`)
- `GeminiTranslationService.cs:81` — the model is used only to build the request URL:
  `$"{_settings.Gemini.BaseUrl}/models/{_geminiModel}:generateContent?key={_settings.Gemini.ApiKey}"`
- `AIProviderOptions.cs:39-49` — `GeminiProviderOptions.Models.Flash` (string, no C# default —
  `= string.Empty`), binds to config section `AIProviders:Gemini:Models:Flash`.
- `AIProviderOptionsValidator.cs:20-21` — `ValidateOnStart()` fails app startup if
  `options.Gemini.Models.Flash` is blank: `errors.Add("AIProviders:Gemini:Models:Flash must be set")`.
  This means a misconfigured/empty value cannot silently reach runtime — it either has a
  real value or the API refuses to start.

### A2. Confirmed current values (tracked local config)

- `src/QuantumBuild.API/appsettings.json:34-38`:
  ```json
  "Gemini": { "Models": { "Flash": "gemini-2.0-flash" } }
  ```
- `src/QuantumBuild.API/appsettings.Development.json:28-32`: identical, `"gemini-2.0-flash"`.
- `GeminiTranslationService` itself has **no hardcoded model string anywhere** — it is
  100% config-driven.

### A3. Repo-wide sweep for other/stale Gemini model references

- `grep -i "gemini-1\.5"` across the entire repo (`src/`, `web/`, `docs/`, `tests/`, `.md`
  files): **zero matches**. No deprecated `gemini-1.5-flash`/`gemini-1.5-pro` string
  exists anywhere, tracked or not.
- `grep -i "gemini"` across `src/`: exactly **18 files** touch the word "Gemini" at all.
  Of those, only `GeminiTranslationService.cs` makes an actual API call; the rest are:
  the options/validator classes, DI registration
  (`ServiceCollectionExtensions.cs`), resilience/bulkhead policy wiring
  (`ProviderConcurrencyOptions.cs`, `ProviderBulkheadPolicies.cs`, `ResiliencePolicies.cs`),
  `ConsensusEngine.cs`/`BackTranslationSelector.cs` (call the service, don't hold a model
  string), `PipelineVersionService.cs:125` (records `_aiProviders.Gemini.Models.Flash` into
  an audit-snapshot JSON — reads the same canonical config, not a separate literal), and
  `CostEstimationService.cs:21` (a **comment only**, `// gemini-2.0-flash`, labelling a
  hardcoded EUR-per-1K-token rate-table constant — not a model identifier used in any API
  call; already up to date but worth noting this file has a separate, unrelated staleness
  risk called out in prior recon `docs/ai-model-versions-recon.md:67`).
- **Conclusion: the fix is complete and singular.** There is exactly one call site that
  resolves a Gemini model name for an actual API request, it reads from the canonical
  `AIProviders` registry, and that registry currently holds `gemini-2.0-flash` in both
  tracked config files. No other deprecated-model reference exists to miss.

### A4. What this recon cannot verify

- Railway env vars (`AIProviders__Gemini__Models__Flash` for Development/Production) are
  not visible to this session. `appsettings.json`/`appsettings.Development.json` are the
  **base** values only — Railway env vars override them at runtime (standard ASP.NET Core
  config precedence). If the Sentry error is recent/ongoing, the most likely explanation
  given the code-level fix is complete is that the Railway env var was never updated (or
  was updated on one environment but not the other) — this needs to be checked directly
  in Railway, not in this repo.

---

## B. The timestamp data-flow problem (the real issue)

### B1. Where the pre-flight scan's input text actually comes from

`PreFlightScanService.ScanAsync` (`PreFlightScanService.cs:28-53`) takes
`IReadOnlyList<string> sectionTexts` as a plain parameter — it has no idea where the text
came from. The caller is `TranslationValidationJob.RunPreFlightScanAsync`
(`TranslationValidationJob.cs:599-633`):

```csharp
var sectionTexts = sections.Select(s => s.OriginalText).ToList();   // :608
var result = await _preFlightScanService.ScanAsync(
    sectionTexts, languageName, run.SectorKey, cancellationToken);  // :612-613
```

`sections` here is `List<SectionPair>` built by `LoadSectionsAsync`
(`TranslationValidationJob.cs:638-756`). The critical line:

```csharp
var originalText = editEntry?.EditedSource
    ?? StripHtml(orig.Content);          // :737-738
```

`orig.Content` is `OriginalSectionInfo.Content`, populated directly from the DB at
`TranslationValidationJob.cs:655`:

```csharp
.Select(s => new OriginalSectionInfo(s.Id, s.SectionNumber, s.Title, s.Content))
```

— `s.Content` is `ToolboxTalkSection.Content` from the `ToolboxTalkSections` table.
**This is the actual, real, persisted section content** — the same field the employee
sees when they read the training material — not a separate transcript buffer, not
anything scan-specific. `StripHtml` (`TranslationValidationJob.cs:915-932`) only removes
HTML tags and decodes entities (`<[^>]+>` regex, entity replace, whitespace collapse) —
**it has no timestamp-awareness at all**.

**Confirmed: the pre-flight scan input is the real learning content**, after HTML
stripping only.

### B2. Is there any timestamp-stripping step, anywhere, on this path?

**No.** Searched every file touching `StripHtml` (3 independent implementations exist —
`TranslationValidationJob.cs:915`, `RequirementMappingJob.cs:416`,
`RequirementIngestionJob.cs:1071` — all HTML-only, none timestamp-aware) and grepped the
whole repo for any timestamp-removal regex/utility (`\[\d{1,2}:\d{2}\]` pattern, "strip
timestamp", etc.) — no matches. There is no timestamp-stripping logic anywhere in the
codebase, on any code path.

### B3. Where do the `[mm:ss]` timestamps actually get created, and how do they end up in `Content`?

Traced upstream from `ToolboxTalkSection.Content` to the AI section-generation step that
writes it. Two content-creation code paths exist for video input; only one of them
produces timestamped transcript text:

**Path used by `POST /toolbox-talks/{id}/generate` and `/smart-generate`** (the
Hangfire job `ContentGenerationJob` → `ContentGenerationService` →
`AiSectionGenerationService`) — **this is the path that matches the Sentry evidence**:

1. `ContentExtractionService.ExtractContentAsync` (`ContentExtractionService.cs:70-350`),
   video branch. Two sub-paths, both end up timestamped:
   - **Existing transcript already stored:** `ContentExtractionService.cs:130-161` calls
     `_transcriptService.GetTranscriptAsync(...)` → `TranscriptService.cs:26-56` →
     `ParseSrtContent` → `FullText` built at `TranscriptService.cs:108`:
     ```csharp
     fullTextBuilder.AppendLine($"[{FormatTimestamp(startTime.Value)}] {text}");
     ```
   - **No existing transcript (fresh transcription):**
     `AutoTranscribeVideoAsync` (`ContentExtractionService.cs:389-621`) calls ElevenLabs
     (`:428-443`), chunks the raw words into SRT cues via
     `ISrtGeneratorService.GenerateSrt` (`:450-458`), then **re-parses that generated SRT
     through the exact same `ParseSrtContent` → `TranscriptService.cs:108`** (`:592`),
     producing an identical `[mm:ss] <cue text>` line-per-line `FullText`.
   - `FormatTimestamp` (`TranscriptService.cs:253-258`), for any video under 1 hour:
     `$"{ts.Minutes}:{ts.Seconds:D2}"` — e.g. start time `0s` → `"0:00"`, start time
     `2s` → `"0:02"`. **This exactly matches the Sentry payload's `[0:00]`, `[0:02]`
     markers**, both format (single-digit minute, no leading zero, `mm:ss`) and value
     (both are timestamps from the first few seconds of a video, exactly what the first
     one or two SRT cues would produce).
2. Both sub-paths' result becomes `VideoContentInfo.FullTranscript`
   (`ContentExtractionService.cs:604-610`), which `BuildCombinedContent`
   (`ContentExtractionService.cs:357-383`) concatenates verbatim (no stripping, no
   transformation — `builder.AppendLine(video.FullTranscript)` at `:365`) into
   `CombinedContent`.
3. `ContentGenerationService.cs:179-187` passes `CombinedContent` straight into
   `AiSectionGenerationService.GenerateSectionsAsync`, which builds the Claude prompt via
   `SectionGenerationPrompts.BuildSectionPrompt` (`AiSectionGenerationService.cs:81`).
4. **The prompt gives the model zero instruction to exclude timestamp markers.**
   `AiSectionGenerationService` never passes a `preserveSourceWording` argument (confirmed
   — zero references to that parameter anywhere in the file), so the prompt always uses
   the summarize-mode branch (`SectionGenerationPrompts.cs:77-110`):
   > "Analyze the following {sourceDescription} and create clear, concise sections that
   > summarize the key points... Each section content should be 4-5 lines..."

   Nothing in this instruction says "ignore/omit the `[mm:ss]` timestamp markers in the
   source." The model is simply asked to summarize text that has bracketed timecodes
   sprinkled through it every ~8 words (one per SRT cue, `WordsPerSubtitle` default `8`),
   and nothing tells it those tokens are metadata rather than content. Whether the model
   copies a `[0:00]`-style token into its output is then down to per-call LLM behaviour —
   not something the code guards against either way.
5. `AiSectionGenerationService`'s response is parsed into section title/content pairs and
   persisted as `ToolboxTalkSection.Content` with **no post-processing filter of any
   kind** applied to the model's returned `content` field (only the response is decoded
   from JSON — no regex/sanitization pass runs on the text values).

**A second, independent path exists that would make this worse but is a distinct trigger:**
`SectionGenerationPrompts.cs:31-75` has a `preserveSourceWording = true` branch used by
the two other content-creation flows (`ContentCreationSessionService` "Create Content
Wizard" and the newer `learning-wizard`/`ContentParserService` talk-based flow), whose UI
toggle defaults to **on** tenant-wide (`ToolboxTalkSettings.cs:151`,
`DefaultPreserveSourceWording = true`; also hardcoded `true` as the wizard form's own
default, `InputConfigStep.tsx:175`). That branch explicitly instructs: *"copy the source
text VERBATIM into 'content'... do not rephrase, condense, or expand... exactly as
written."* **However**, both of those two flows' video-transcript source text is built by
`VideoTranscriptionJobForTalk.cs:92-94` / `VideoTranscriptionJob.cs:76-79` — a plain
`string.Join(" ", words)` with **no timestamps at all** (confirmed by reading both files
directly). So today, the "verbatim copy" instruction and the "has timestamps" transcript
never actually combine in the same request — they're on different flows. This is worth
flagging as a **latent risk**, not the active cause: if either of those two flows' video
transcript generation is ever changed to route through SRT structuring (which a separate,
prior recon — `docs/video-transcript-structuring-recon.md` — recommends doing, to fix an
unrelated section-count-quality gap), it would combine with the always-on
`preserveSourceWording` default and guarantee timestamp leakage on every video talk, not
just occasionally.

### B4. How far does the timestamped text propagate — scan only, or real content too?

**Confirmed: it is real content, not scan-only pollution.** The propagation chain:

1. **`ToolboxTalkSection.Content`** — the actual stored section, is polluted at the source
   (§B3). This is what an employee reads when viewing the talk (no stripping happens
   anywhere in the employee-facing read path either — out of this recon's traced scope,
   but no stripping mechanism exists anywhere in the codebase per §B2, so there's no place
   it could be cleaned before display).
2. **Pre-flight scan input** — `TranslationValidationJob.cs:608,737-738` — `StripHtml(orig.Content)`,
   confirmed §B1. Timestamps pass through untouched.
3. **Translation generation input** — `LoadSectionsAsync` (`TranslationValidationJob.cs:638-756`)
   feeds the same `originalSections` (built from `s.Content`, same query as §B1) into
   `GenerateTranslationForSectionsAsync` (`TranslationValidationJob.cs:679-681`) when no
   translation exists yet — i.e., the AI translation prompt receives the polluted content
   and produces a `TranslatedSections` JSON that will carry the same `[mm:ss]`-style
   fragments through into the target language (an LLM translating a string containing
   `[0:00]` will typically pass the bracketed numeric token through unchanged, since
   there's no natural-language content to translate in it — meaning the pollution is very
   likely preserved verbatim in every target language too).
4. **Back-translation / consensus scoring** — `ValidateSectionAsync` (called at
   `TranslationValidationJob.cs:227-239`) uses `section.OriginalText`/`section.TranslatedText`
   — the same polluted pair — for lexical scoring and back-translation, meaning validation
   scores themselves are computed against content containing structural noise not part of
   the intended training material.
5. **Pre-flight scan JSON parse failure** — the symptom that surfaced in Sentry, §B5.

**Blast radius: this is not a scan-only defect.** It pollutes the actual employee-facing
learning content, the actual stored translations in every target language, and the
back-translation validation scores — the pre-flight scan crash is simply the most visible
symptom because it's the step whose failure mode (unhandled `JsonReaderException`) reaches
Sentry, while the content/translation pollution has no error path to surface through at
all — it just silently ships as slightly-corrupted content.

### B5. Connecting to the JSON parse failure — plausible mechanism

`PreFlightScanService.BuildPrompt` (`PreFlightScanService.cs:55-89`) joins all section
texts (`string.Join("\n\n---\n\n", sectionTexts)`, `TranslationValidationJob.cs:36`
equivalent inline) into one `SOURCE TEXT:` block appended to the prompt, and asks Claude
to return **only** a JSON object matching a fixed shape (`highRiskTerms`, `properNouns`,
`roleConstructs`, `slashConstructs`).

`ParseResponse` (`PreFlightScanService.cs:141-207`) does:
1. Trim, strip a leading/trailing ` ```...``` ` markdown fence if present (`:146-152`).
2. `JsonDocument.Parse(json)` — **strict, no other fallback** (`:154`).
3. Any exception (fence-stripping failure, malformed JSON, unescaped control chars) is
   caught only by the outer `ScanAsync` try/catch (`PreFlightScanService.cs:48-52`), which
   logs and returns an empty result — **this is where the Sentry
   `JsonReaderException`/"Pre-flight scan failed" log line (`:50`) comes from.**

**A concrete, plausible mechanism connecting the timestamped input to a JSON parse
failure:** the section content built by `TranscriptService.cs:108`
(`fullTextBuilder.AppendLine(...)`) is **one line per SRT cue** — for an
8-word-per-subtitle default (`SubtitleProcessingSettings.WordsPerSubtitle` = 8), a video
transcript that leaked into section content would carry many embedded literal newlines
close together, each immediately preceded by a `[mm:ss]` bracket token. If the model, when
building a JSON string value (e.g. quoting or paraphrasing a chunk of the source text into
a `"risk"` or `"suggestedTranslation"` field), echoes a multi-line span of this
structurally-choppy source text without properly escaping the embedded newlines as `\n`,
the result is a JSON string value containing a **raw, unescaped control character** —
one of the most common LLM JSON-generation failure modes, and something
`JsonDocument.Parse` will reject outright with exactly a `JsonReaderException`. The
timestamp brackets themselves (`[`/`]`) are not inherently JSON-breaking when properly
inside a quoted string — the newline-dense, choppy-line structure they're embedded in is
the more likely proximate cause of malformed escaping, not the brackets per se.

**This is a plausible, mechanistically-grounded hypothesis, not a confirmed one** — this
recon has no access to the actual failing request/response payload (Sentry PII
scrubbing removes request bodies per `CLAUDE.md` note on Sentry configuration, and no
raw payload was provided). It cannot be stated as certain from static code alone.

**Does fixing the input (stripping timestamps) resolve the parse error, or is defensive
parsing also needed?** Both matter, for different reasons:
- Stripping timestamps at the source (before section content is persisted, or at minimum
  before it's used as an LLM prompt input) removes the specific newline-dense/bracketed
  structure most likely to trigger this failure mode, **and** fixes the actual content
  pollution (§B4) — the higher-value fix, since it addresses real content quality, not
  just this one crash site.
- `ParseResponse`'s strict `JsonDocument.Parse` with no defensive fallback (§D) means
  **any** future LLM JSON hiccup — truncation, a stray comment, an extra trailing comma,
  unescaped control characters from a completely different source of messy input — will
  hit this exact same unhandled-exception path. Fixing only the timestamp input would
  close this one specific trigger but leaves the parser fragile to every other JSON
  malformation mode.

---

## C. Relation to the subtitle/video work

**Confirmed link:** yes, this is the same subtitle/transcript generation machinery
documented in `CLAUDE.md`'s "Subtitle Processing" section and prior recon
`docs/video-transcript-structuring-recon.md`. `TranscriptService.ParseSrtContent`
(`TranscriptService.cs:59-150`) is the **same parser** used both to (a) structure the
transcript text for AI section-generation prompts (`ContentExtractionService.cs:592`,
`:130-148`) and (b) is conceptually the SRT-cue-boundary logic that underlies the actual
subtitle files served to employees via `SubtitleProcessingOrchestrator`/`SrtStorage` (the
SRT *generation* side, `ISrtGeneratorService.GenerateSrt`, produces the subtitle file
itself; `ParseSrtContent` is what turns that SRT back into the AI-prompt-shaped text —
same round-trip mechanism documented in `docs/video-transcript-structuring-recon.md §A1`
step 3/9).

**The timestamps are a legitimate, intentional subtitle artifact for the *prompt input*
purpose** — `[mm:ss]`-prefixed lines give Claude structural chunking cues so it can
identify natural section boundaries in a long transcript (this is explicitly the reason
the legacy path routes video through SRT generation/parsing at all — see
`docs/video-section-parsing-recon.md §6`, which found this structuring is *why* the legacy
path produces better section counts than the newer word-blob flows). **The defect is not
that timestamps exist in the prompt input — it's that nothing tells the model to leave
them out of its output, and nothing strips them back out of the model's output before
that output becomes permanent learning content.** The subtitle/transcript machinery itself
is working as designed for its own purpose (captions); the gap is specifically at the
prompt-instruction and post-generation-sanitization boundary in the section-generation
step.

---

## D. JSON parsing robustness (secondary)

`PreFlightScanService.ParseResponse` (`PreFlightScanService.cs:141-207`):

- Strips a **single** leading/trailing ` ``` ` fence if the trimmed response literally
  starts with `` ``` `` (`:146-152`) — handles the common "wrapped in a markdown code
  block" case, including a lone `` ```json `` opening line (it discards everything up to
  and including the first newline, then trims a trailing `` ``` ``).
- Otherwise goes straight to `JsonDocument.Parse(json)` (`:154`) — **no other fallback**:
  - No handling for the model prepending prose before the JSON (e.g. "Here is the
    analysis:\n\n{...}") — the fence-strip only fires if the string *starts* with
    `` ``` ``; leading prose without fences would go straight into `Parse` and fail.
  - No extraction of the largest `{...}` or `[...]` block from a longer response.
  - No retry/second-pass on parse failure — a single failure aborts the whole scan
    (caught by the outer `try/catch` in `ScanAsync`, `:48-52`, which silently returns an
    empty result).
  - No handling of trailing commas, comments, or other near-miss JSON dialects.
- **Even after fixing the timestamp input (§B5), LLM JSON output remains inherently
  variable** — defensive parsing (extract the JSON block robustly, tolerate
  leading/trailing prose, one retry-with-stricter-prompt on failure) would be a reasonable
  robustness addition independent of the root-cause fix, but per this recon's scope,
  **the primary, higher-value fix is the timestamp data-flow problem (§B)**, not the
  parser — the parser only decides what happens when something upstream already went
  wrong; the timestamp leak is what's actually going wrong.

---

## E. Impact / severity

### E1. Is this subsystem actively used in production?

Yes. `TranslationValidationJob` is the core Hangfire job for the TransVal
(Translation Validation) feature documented extensively in `CLAUDE.md` ("Translation
Validation (TransVal)" section) — it runs on every validation run triggered from the
6-step creation wizard's "Translate & Validate" step and from `POST
/toolbox-talks/{talkId}/validation/validate`. This is a live, actively-used production
feature, not dormant code.

### E2. What actually breaks for a user

- **(a) Gemini 404 (Round 2 back-translation):** **Does not block or fail the validation
  run.** `ConsensusEngine.RunConsensusAsync` (`ConsensusEngine.cs:106-136`) calls
  `_gemini.BackTranslateAsync`, and `GeminiTranslationService.BackTranslateAsync` returns
  a `BackTranslationResult.FailureResult(...)` (not a thrown exception, not `null`) on any
  non-success HTTP status (`GeminiTranslationService.cs:94-101`). `ApplyBackTranslation`
  (`ConsensusEngine.cs:175+`) checks `btResult.Success` before scoring — a failed Gemini
  call is treated the same as "provider returned nothing usable," and the engine falls
  through to Round 3 (Claude Sonnet tiebreaker, `ConsensusEngine.cs:138-159`) or finalizes
  on whatever rounds succeeded (`:161-169`). **Net effect: Round 2's cross-check is
  silently skipped; the run still completes and still gets a Pass/Review/Fail
  outcome** — just with one fewer independent back-translation vote contributing to
  consensus, which could plausibly shift a borderline section's score, but does not
  produce a visible failure to any user. The error only surfaces as a Sentry `LogError`
  (`GeminiTranslationService.cs:96-98`) — no user-facing error, no blocked workflow.
- **(b) Pre-flight scan JSON parse failure:** **Does not block validation either.**
  `RunPreFlightScanAsync` is explicitly documented as "non-blocking, never fails the job"
  (`TranslationValidationJob.cs:596-597` comment) and wraps its entire body in a
  try/catch that logs and sends a `"Pre-flight scan skipped (error)"` SignalR progress
  message (`TranslationValidationJob.cs:628-632`), then the job proceeds to full section
  validation regardless. **User-visible effect: the reviewer simply never sees any
  pre-flight suggestions (high-risk terms, proper nouns, role constructs, slash
  constructs) for that run** — a silently degraded advisory feature, not a broken
  validation run. `run.PreFlightScanJson` stays unset/stale for that run.
- **Neither failure blocks translation, blocks publishing, or corrupts the validation
  run's Pass/Review/Fail outcome directly.** The real, higher-severity issue (§B4) is
  independent of both errors: the section content itself already contains the timestamp
  pollution *before* either of these two services ever touch it — that's a content-quality
  defect that ships to real employees and real translations regardless of whether the
  pre-flight scan crashes or the Gemini call 404s.

### E3. Severity ranking

1. **Highest — content/translation pollution (§B4):** silent, no error surfaces anywhere,
   directly affects what employees read and what gets certified as validated/translated
   content. This is a correctness defect in shipped training material, not just a
   background-job error.
2. **Medium — pre-flight scan crash:** visible in Sentry, degrades an advisory feature,
   does not block the pipeline.
3. **Low — Gemini 404:** visible in Sentry, degrades one of three back-translation votes,
   does not block the pipeline, and (per §A) the code-level fix is already complete —
   only a possible deployment/env-var gap remains to verify outside this repo.

---

## F. Test coverage

- **`PreFlightScanService`** — **zero test files reference it** (`grep` across
  `tests/QuantumBuild.Tests.Unit` and `tests/QuantumBuild.Tests.Integration` for
  `PreFlightScanService` found no matches). No unit test exists for `ParseResponse`'s
  fence-stripping, JSON parsing, or its `ScanAsync` error-handling path.
- **`GeminiTranslationService`** — no dedicated test file exists; it's referenced only
  indirectly via `ConsensusEngineTests.cs` (which mocks `IGeminiTranslationService`, per
  `CLAUDE.md`'s "Unit Tests" table listing `ConsensusEngineTests` coverage — "Round 1-3
  escalation, threshold pass/review/fail, agreement tolerance"). No test directly exercises
  the HTTP-error path or JSON-response parsing inside `GeminiTranslationService` itself.
- **Timestamp handling** — no test anywhere in the repo references timestamp
  stripping/handling in section content (grep for timestamp-stripping patterns and
  utility names returned nothing), consistent with §B2's finding that no such logic
  exists to test.

---

## Summary — what a complete root-cause fix would need to address

Facts only, no fix proposed, per scope:

1. **Gemini model config is already correct and singular** (§A) — the only open item is
   confirming the Railway env var actually matches, which is outside this repo's
   visibility.
2. **The real defect is at AI section-generation time**, not at the pre-flight scan:
   `SectionGenerationPrompts.cs`'s summarize-mode prompt (used by
   `AiSectionGenerationService`, the path matching the Sentry evidence) receives a
   `[mm:ss]`-timestamped video transcript (`TranscriptService.cs:108`) with no instruction
   to exclude timestamp markers, and no output sanitization exists anywhere afterward to
   strip any that leak through into the persisted `ToolboxTalkSection.Content`.
3. **This pollution is not scan-only** — it propagates into real employee-facing content,
   real AI-generated translations in every target language, and back-translation
   validation scoring (§B4), independent of whether the pre-flight scan itself crashes.
4. **The JSON parse failure is a plausible downstream symptom** of feeding
   newline-dense, bracket-heavy timestamped text into a strict-JSON-output LLM prompt
   (§B5) — mechanistically reasonable given `ParseResponse`'s all-or-nothing
   `JsonDocument.Parse`, but not confirmable from the actual failing payload in this
   recon.
5. **Neither error currently blocks the TransVal pipeline** (§E2) — both degrade quality
   silently rather than failing loudly, which is itself worth noting: the Gemini 404 and
   scan-parse failure are the *visible* symptoms; the actual timestamp-polluted content
   shipping to employees has no error path at all today.
6. **No test coverage exists** for either `PreFlightScanService` or
   `GeminiTranslationService`'s failure paths, and none exists for timestamp handling in
   section content anywhere in the pipeline.
