# Video Transcript Structuring Recon: What Legacy Does That the New Wizard Omits

**Date:** 2026-08-07
**Type:** Read-only recon. No code changed.
**Builds on:** `docs/video-section-parsing-recon.md` (2026-08-07), which established that
the new wizard's video transcript is an unstructured word blob while legacy's is
timestamped/line-broken (SRT-derived). This doc goes one level deeper: everything else
`AutoTranscribeVideoAsync` does, which of it the new flow omits, whether SRT/subtitles
are a second missing purpose, and the exact reusable structuring unit.

---

## Headline

1. `ContentExtractionService.AutoTranscribeVideoAsync` (`ContentExtractionService.cs:389-621`)
   does **nine sequential steps**, not just "generate SRT for the prompt." Six of those
   nine (steps 4-8) exist purely to produce and store subtitles for the video player —
   they have nothing to do with section-parsing quality. (§A1)
2. The new wizard's `VideoTranscriptionJobForTalk` does **none** of those six steps. The
   result: new-wizard video talks have **zero subtitles available to employees**, not
   just a worse transcript shape for AI parsing. The employee-facing subtitle
   status/download endpoints depend entirely on a `SubtitleProcessingJob` row existing,
   and nothing in the new flow's automatic pipeline ever creates one. (§A3)
3. The reusable **structuring-only** unit is two independent, side-effect-free calls —
   `ISrtGeneratorService.GenerateSrt(words, wordsPerSubtitle)` then
   `ITranscriptService.ParseSrtContent(srt, null)` — already proven to run safely inside
   Hangfire job context elsewhere in the codebase, requiring no new infrastructure to
   inject. (§B4-B6)
4. A **third, separate flow** (`VideoTranscriptionJob` + `ContentCreationSessionService`,
   the session-based "Create Content Wizard") has the identical unstructured-word-join
   defect for section-parsing, but — unlike the new talk-based flow — it DOES
   automatically create a real `SubtitleProcessingJob` later in its pipeline, reusing the
   cached ElevenLabs words. This is useful evidence for what a complete fix should look
   like. (§A4)

---

## A. Full behaviour of legacy `AutoTranscribeVideoAsync`

### A1. Step-by-step walkthrough

Entry point: `ContentExtractionService.ExtractContentAsync` (`ContentExtractionService.cs:70-350`)
is called by `ContentGenerationService.GenerateContentAsync` (`ContentGenerationService.cs:110-111`),
itself invoked from the Hangfire job `ContentGenerationJob` (`ContentGenerationJob.cs:131-136`),
triggered by `POST /toolbox-talks/{id}/generate` or `/smart-generate`.

Inside `ExtractContentAsync`, for `includeVideo = true`:

- `ContentExtractionService.cs:130-133` — first tries `_transcriptService.GetTranscriptAsync(toolboxTalkId, ...)`,
  which looks for an **already-existing** English SRT via the subtitle orchestrator
  (`TranscriptService.cs:34`, `_orchestrator.GetSrtContentAsync(toolboxTalkId, "en", ...)`).
  If found, that path is used and `AutoTranscribeVideoAsync` is skipped entirely
  (`ContentExtractionService.cs:135-161`).
- Only if no existing transcript is found (`ContentExtractionService.cs:169-170`) does it call
  `AutoTranscribeVideoAsync(toolboxTalk, tenantId, cancellationToken)`.

`AutoTranscribeVideoAsync` (`ContentExtractionService.cs:389-621`) then does, in order:

| # | Lines | What it does | Side effect |
|---|---|---|---|
| 0 | `395-402` | Checks `_settings.ElevenLabs.ApiKey` is configured; fails fast with a user-facing message if not | none (early exit) |
| 1 | `407-425` | `DetermineVideoSourceType(talk.VideoUrl)` then `_videoSourceProvider.GetDirectUrlAsync(talk.VideoUrl, sourceType, ct)` — resolves Google Drive share links / Azure Blob URLs to a directly-downloadable URL | none (read-only HTTP resolution) |
| 2 | `428-443` | `_transcriptionService.TranscribeAsync(videoUrlResult.DirectUrl, ct)` — the ElevenLabs call, using the **resolved direct URL** from step 1, not the raw `talk.VideoUrl` | none (external API call) |
| 3 | `450-458` | `_srtGeneratorService.GenerateSrt(transcriptionResult.Words, _settings.WordsPerSubtitle)` — chunks raw words into subtitle-cue-sized blocks with timestamps; `CountSubtitleBlocks(srtContent)` for logging | none (pure in-memory transform) |
| 4 | `461-496` | Uploads the generated English SRT to R2 via `_srtStorageProvider.UploadSrtAsync(srtContent, "{slug}_en.srt", tenantId, ct)` | **writes SRT file to Cloudflare R2** |
| 5 | `499-527` | Creates a `SubtitleProcessingJob` entity (`Status = Completed`, `EnglishSrtContent`, `EnglishSrtUrl`, `TotalSubtitles`) plus one `SubtitleTranslation` row for English (`Status = Completed`, carries the SRT content/URL); adds both to `_dbContext` (not yet saved) | staged DB insert |
| 6 | `530-562` | `GetTargetLanguagesFromEmployeesAsync(tenantId, ct)` — queries all employees' `PreferredLanguage` (tenant-scoped, `IgnoreQueryFilters()` used deliberately here per the method's own comment about background-job tenant-filter risk, `ContentExtractionService.cs:664-666`); for each non-English language, adds a **Pending** `SubtitleTranslation` row | staged DB insert (N more rows) |
| 7 | `565-568` | If any non-English languages were queued, sets `subtitleJob.Status = Translating` | staged mutation |
| 8 | `571-585` | Sets `toolboxTalk.VideoTranscriptExtractedAt` / `toolboxTalk.ExtractedVideoTranscript = srtContent` (raw SRT text, at this point — see note below), calls `_dbContext.SaveChangesAsync(ct)` (commits steps 4-8's staged writes), then if translations are pending, **enqueues** `_backgroundJobClient.Enqueue<SubtitleProcessingOrchestrator>(o => o.ProcessRetryAsync(subtitleJob.Id, CancellationToken.None))` | **DB commit** + **enqueues a second Hangfire job** for per-language SRT translation |
| 9 | `592-610` | `_transcriptService.ParseSrtContent(srtContent, null)` — parses the **same in-memory `srtContent`** string from step 3 back into a `TranscriptResult` (segments + `FullText` with `[mm:ss]` timestamp-prefixed lines, one per subtitle cue); builds the `VideoContentInfo` returned to the caller, using `parsedTranscript.FullText` (or `FormatForAi(parsedTranscript)` as a fallback if parsing failed) | none (pure in-memory transform) |

Back in `ExtractContentAsync` (`ContentExtractionService.cs:172-181`), the `VideoContentInfo`
from step 9 becomes `videoContent`, which is folded into `CombinedContent`
(`BuildCombinedContent`, `ContentExtractionService.cs:357-383`) — this is what
`AiSectionGenerationService.GenerateSectionsAsync` receives as the prompt's transcript text.

**Note on `toolboxTalk.ExtractedVideoTranscript`:** step 8 caches the **raw SRT text**
(with numeric indices and `-->` timestamp lines) on the entity, not the parsed
`[mm:ss]`-prefixed plain text from step 9. The parsed/structured version is only ever
held in a local variable and returned to the caller for that single request — it is
**not** persisted anywhere. So the entity-level cache (`ExtractedVideoTranscript`) is raw
SRT, while the AI section-generation prompt gets the parsed, human/timestamp-formatted
text. This matters for the "existing transcript" branch (§ above, `ContentExtractionService.cs:130-161`):
when a transcript already exists, it is retrieved as SRT and re-parsed on every call —
`ExtractedVideoTranscript` caching does not save the parse step, only the ElevenLabs call
and SRT generation.

### A2. What the new flow (`VideoTranscriptionJobForTalk`) omits, and whether it matters

`VideoTranscriptionJobForTalk.ExecuteAsync` (`VideoTranscriptionJobForTalk.cs:29-133`) does:
resolve `videoUrl` from `talk.SourceFileUrl`/`talk.VideoUrl` (lines 64-66) →
`transcriptionService.TranscribeAsync(videoUrl, ct)` (line 80) → join words with spaces
(lines 92-94) → save `talk.ExtractedVideoTranscript` + `talk.TranscriptWordsJson` (lines
106-107) → enqueue `ContentCreationParseJobForTalk`.

Mapped against the nine legacy steps:

| Legacy step | New flow has it? | Matters for section-parsing quality? | Matters for subtitles/SRT elsewhere? | Matters for state/error handling? |
|---|---|---|---|---|
| 0 — API key check | No (relies on `TranscribeAsync` failing naturally if unconfigured) | No | No | Minor — legacy gives a specific "not configured" message; new flow would surface a generic transcription failure instead |
| 1 — Resolve direct video URL via `IVideoSourceProvider` | **No** — raw `videoUrl` passed straight to `TranscribeAsync` | **Yes, conditionally.** `ElevenLabsTranscriptionService.TranscribeAsync` (`ElevenLabsTranscriptionService.cs:47`) does a plain `_httpClient.GetAsync(videoUrl, ...)` — it does not itself resolve Google Drive share links or similar. If `talk.VideoUrl` is a Google Drive URL (`InitialiseToolboxTalkCommandHandler.cs:101` sets `VideoUrl` verbatim from the request), the download would fetch the share-page HTML, not the video, and transcription would fail or return garbage. For R2-uploaded videos (`SourceFileUrl`, the more common new-wizard path — already a direct downloadable URL), this omission is inert. | No | Yes — if this causes transcription to fail for Drive-sourced videos, it's a silent-ish failure surfaced only as a generic ElevenLabs/HTTP error, not a clear "unsupported video source" message |
| 2 — ElevenLabs transcription | Yes (line 80) | — (present) | — | — |
| 3 — `GenerateSrt` (chunk into timestamped cues) | **No** | **Yes — the core finding from the prior recon** (unstructured single-line blob vs. timestamped/line-broken text reaching the Claude prompt) | Yes — this is also the input to step 4's storage | — |
| 4 — Upload English SRT to R2 | **No** | No | **Yes** | No |
| 5 — Create `SubtitleProcessingJob` + English `SubtitleTranslation` | **No** | No | **Yes — this is the record employee-facing subtitle endpoints query for** (see §A3) | No |
| 6 — Queue per-employee-language `SubtitleTranslation` rows | **No** | No | **Yes** — no non-English subtitles get queued either | No |
| 7 — Set job status to Translating | **No** (no job exists) | No | Yes (follows from 6) | No |
| 8 — Save + enqueue `SubtitleProcessingOrchestrator.ProcessRetryAsync` | **No** | No | **Yes** — no second job ever runs to produce translated subtitle files | No |
| 9 — `ParseSrtContent` (structure SRT back into segments/timestamped text) | **No** (new flow never has SRT to parse — it joins words directly) | **Yes** (same finding as step 3 — together these two steps are the reusable unit, see §B) | — | — |

**Net: the new flow omits structuring (steps 3+9, the AI-parsing-quality gap already
identified) AND the entire subtitle-generation-and-storage side effect (steps 4-8), which
is a second, independent gap not covered by the prior recon.**

### A3. Does legacy's SRT generation serve two purposes, and does the new flow miss both?

**Yes, confirmed.** `AutoTranscribeVideoAsync`'s SRT generation is genuinely dual-purpose:
the same `srtContent` variable (from step 3) is (a) parsed via step 9 into the
AI-prompt transcript text, and (b) uploaded/persisted via steps 4-8 as the talk's actual
subtitle file. The new flow only ever needed purpose (a) but currently gets neither
purpose correctly — it skips (a)'s structuring and doesn't attempt (b) at all.

Employee-facing subtitle delivery depends entirely on a `SubtitleProcessingJob` row
existing for the talk:

- `SubtitleProcessingOrchestrator.GetSrtContentAsync` (`SubtitleProcessingOrchestrator.cs:705-734`)
  queries `_dbContext.SubtitleProcessingJobs.Include(j => j.Translations).Where(j => j.ToolboxTalkId == toolboxTalkId && !j.IsDeleted)...FirstOrDefaultAsync(...)` —
  returns `null` if no job row exists, or if the matching-language `SubtitleTranslation`
  isn't `Completed`.
- Employee subtitle download (`MyToolboxTalksController.cs:696`, `GET
  /my/toolbox-talks/{id}/subtitles/{languageCode}`) and subtitle status
  (`MyToolboxTalksController.cs:642`, `GET /my/toolbox-talks/{id}/subtitles/status`) both
  route through this same query. With no `SubtitleProcessingJob` row, status returns 404
  ("No subtitles available for this learning") and the video player has no captions in
  any language, including English.
- `QrScanController.cs:545` and `SubtitleProcessingController.cs:215` (admin-facing status)
  use the identical `GetSrtContentAsync` path — same dependency.

**Conclusion: a new-wizard video talk, left on its automatic pipeline, has no subtitles
at all** — not degraded subtitles, none. This is a gap independent of, and additive to,
the section-parsing quality gap from the prior recon.

**Existing (non-automatic) workaround, and its limits:** `POST
/toolbox-talks/{id}/subtitles/process` (`SubtitleProcessingController.cs:42-97`) is a
generic, talk-origin-agnostic endpoint that calls
`_orchestrator.StartProcessingAsync(toolboxTalkId, request.VideoUrl, request.VideoSourceType,
request.TargetLanguages, cachedTranscriptWordsJson: null, ct)`
(`SubtitleProcessingController.cs:73-79`) — this is the manual "Generate Subtitles"
action available for any talk regardless of which wizard created it, since it doesn't
depend on `ExtractedVideoTranscript`/`TranscriptWordsJson` at all, only on the request
body's own `VideoUrl`/`TargetLanguages`. So an admin **can** manually trigger subtitle
generation after the fact for a new-wizard talk. Two things it does **not** do:

1. It is **not automatic** — nothing in the new wizard's pipeline calls it. Whether the
   admin UI actually surfaces this action for new-wizard-created talks was not verified
   (out of backend-only scope for this recon).
2. It **always passes `cachedTranscriptWordsJson: null`** (`SubtitleProcessingController.cs:78`,
   hardcoded), so it always re-calls ElevenLabs from scratch even though
   `talk.TranscriptWordsJson` (populated by `VideoTranscriptionJobForTalk.cs:107`) already
   holds the exact same words from the automatic transcription that just ran. This wastes
   an ElevenLabs call/cost, though it produces a correct result — the orchestrator's
   `ProcessAsync` (`SubtitleProcessingOrchestrator.cs:174-190`) only skips re-transcription
   when `job.CachedTranscriptWordsJson` is non-empty, and it is never set from this call
   site.

Contrast: `SubtitleProcessingOrchestrator.StartProcessingAsync` accepts
`cachedTranscriptWordsJson` specifically to avoid this double-call, and the session-based
wizard **does** wire this correctly (see §A4) — so the plumbing to skip a redundant
ElevenLabs call already exists and is exercised elsewhere; it's just not connected from
either the new talk-based flow's automatic pipeline or its manual-trigger controller.

### A4. A third flow with the same defect, for comparison: the session-based wizard

`VideoTranscriptionJob` (no "ForTalk" suffix, `VideoTranscriptionJob.cs`) is the
transcription job for the separate, session-based "Create Content Wizard" (CLAUDE.md
Phase 13 — the 6-step Input→Parse→Quiz→Settings→Translate&Validate→Publish flow, driven
by `ContentCreationSessionService`). Its own doc comment
(`VideoTranscriptionJobForTalk.cs:14`) calls it "analogous... but targets the talk row
directly," confirming the new talk-based job was modeled on this one.

- `VideoTranscriptionJob.cs:76-79` — **identical unstructured word-join**, with a comment
  literally saying `"Join words into plain text — same logic as the synchronous path"`.
  This flow has the same section-parsing structuring gap as the new talk-based flow — it
  is not a place where the fix already exists.
- However, `session.TranscriptWordsJson` (set at `VideoTranscriptionJob.cs:92`, same
  pattern as the new flow's `talk.TranscriptWordsJson`) **is** later reused:
  `ContentCreationSessionService.cs:733-739` calls
  `_subtitleOrchestrator.StartProcessingAsync(talkId, session.SourceFileUrl,
  SubtitleVideoSourceType.DirectUrl, targetLanguageNames, session.TranscriptWordsJson, ct)`
  — passing the cached words as `cachedTranscriptWordsJson`, which lets
  `SubtitleProcessingOrchestrator.ProcessAsync` skip re-transcription
  (`SubtitleProcessingOrchestrator.cs:177-190`). This call happens automatically, when the
  session reaches its "Translate & Validate" step (`ContentCreationSessionService.cs:716`,
  gated on `session.InputMode == InputMode.Video && !string.IsNullOrEmpty(session.SourceFileUrl)`).

So the session-based wizard **does** end up with a real, automatically-created
`SubtitleProcessingJob` (reusing cached words, no wasted ElevenLabs call) — it just still
has the transcript-structuring defect for section-parsing. This is useful precedent: the
codebase already has a proven, working pattern for "cache transcript words on
transcription, reuse them later when kicking off subtitle processing" — it's just not
wired into the new talk-based flow at all (§A3).

---

## B. The reusable SRT-generate-and-parse unit

### B4. Exact services, signatures, inputs/outputs

**`ISrtGeneratorService.GenerateSrt`** (`ISrtGeneratorService.cs:8-33`, implementation
`SrtGeneratorService.cs:9-113`, registered `AddScoped` in
`Application/DependencyInjection.cs:30`):

```csharp
string GenerateSrt(List<TranscriptWord> words, int wordsPerSubtitle = 8);
```

- Input: `List<TranscriptWord>` — exactly the shape `ITranscriptionService.TranscribeAsync`
  returns as `TranscriptionResult.Words` (`ITranscriptionService.cs:35`). **Takes the
  ElevenLabs words result directly, no transformation needed.** Each `TranscriptWord` has
  `Text`, `Type` ("word"/"spacing"/"punctuation"/"audio_event"), `Start`, `End`
  (`ITranscriptionService.cs:67-88`).
- Skips `spacing`/`audio_event` types and empty text internally
  (`SrtGeneratorService.cs:27-33`); groups into subtitle blocks of `wordsPerSubtitle`
  words or on sentence-ending punctuation, whichever comes first
  (`SrtGeneratorService.cs:43-45`).
- Output: a plain SRT-formatted string (numbered blocks, `HH:MM:SS,mmm --> HH:MM:SS,mmm`
  timestamp lines, text lines, blank-line separators).
- `wordsPerSubtitle` source: `SubtitleProcessingSettings.WordsPerSubtitle`
  (`SubtitleProcessingSettings.cs:27`, default `8`). No `"WordsPerSubtitle"` key exists in
  any tracked or local config file (confirmed via grep across the repo) — the effective
  value everywhere it's used today (`ContentExtractionService.cs:452`,
  `SubtitleProcessingOrchestrator.cs:224`) is the code default of `8`.
- Pure function — no I/O, no async, no DB, no HTTP.

**`ITranscriptService.ParseSrtContent`** (`ITranscriptService.cs:28`, implementation
`TranscriptService.cs:59-150`, registered `AddScoped` in
`ServiceCollectionExtensions.cs:179`):

```csharp
TranscriptResult ParseSrtContent(string srtContent, TimeSpan? totalDuration = null);
```

- Input: raw SRT string (exactly what `GenerateSrt` produces, or a stored/retrieved SRT
  file — same parser handles both).
- Output: `TranscriptResult` record (`ITranscriptService.cs:65-96`) —
  `Success`, `FullText` (the structured, timestamp-prefixed text: one
  `[mm:ss] <cue text>` line per subtitle block, built by `TranscriptService.cs:108`),
  `Segments` (list of `TranscriptSegment` with `Index`/`StartTime`/`EndTime`/`Text`/`PercentageIntoVideo`),
  `TotalDuration`, `ErrorMessage`.
- `FullText` is exactly the shape legacy's video content ends up as in the AI prompt
  (§A1, step 9) — this is the string that would replace the new flow's `string.Join(" ",
  words)` blob.
- Also pure/synchronous once called — no I/O inside the method itself (only the sibling
  method `GetTranscriptAsync` does I/O, via `ISubtitleProcessingOrchestrator`;
  `ParseSrtContent` does not call it).

**Minimal reuse chain**, confirmed independent of any of the storage/DB steps in §A1:

```csharp
var srt = srtGeneratorService.GenerateSrt(result.Words, settings.WordsPerSubtitle);
var parsed = transcriptService.ParseSrtContent(srt, null);
var structuredText = parsed.Success && !string.IsNullOrEmpty(parsed.FullText)
    ? parsed.FullText
    : transcriptService.FormatForAi(parsed); // legacy's own fallback, ContentExtractionService.cs:605-607
```

This is a direct copy of what `ContentExtractionService.cs:450-452` and `:592-607`
already do — the two calls, with the same fallback logic legacy uses when parsing the
freshly-generated SRT fails.

### B5. Dependencies, context, Hangfire-safety

Both services are constructor-injectable with **no HttpContext/request-scoped
dependency in the paths that matter here**:

- `SrtGeneratorService` — zero constructor dependencies (`SrtGeneratorService.cs:9`, no
  constructor at all). Cannot have a context problem.
- `TranscriptService` — constructor takes `ISubtitleProcessingOrchestrator orchestrator,
  ILogger<TranscriptService> logger` (`TranscriptService.cs:17-19`). `ParseSrtContent`
  itself never touches `_orchestrator` (only `GetTranscriptAsync` does) — DI will still
  construct the full `ISubtitleProcessingOrchestrator` graph when resolving
  `ITranscriptService`, but that graph (`SubtitleProcessingOrchestrator.cs:47-58`) is
  itself `AddScoped` with only DbContext/settings/logger/other-scoped-service
  dependencies — no `IHttpContextAccessor` anywhere in it.
- **Both are already proven to execute correctly inside Hangfire job context today**,
  independent of any tenant-filter question (§B6 below): `SubtitleProcessingOrchestrator.ProcessAsync`
  — itself a Hangfire job entry point, enqueued at `SubtitleProcessingOrchestrator.cs:146-147`
  and `ContentExtractionService.cs:579-580` — calls `_srtGeneratorService.GenerateSrt(...)`
  directly at `SubtitleProcessingOrchestrator.cs:224`. And `ContentExtractionService.AutoTranscribeVideoAsync`,
  which calls both `GenerateSrt` and `ParseSrtContent`, executes inside the Hangfire job
  `ContentGenerationJob`. No request-context read occurs in either call.
- DI registration confirms scoping: `ISrtGeneratorService` → `Application/DependencyInjection.cs:30`
  (`AddScoped`); `ITranscriptService` → `ServiceCollectionExtensions.cs:179` (`AddScoped`).
  Both are ordinary scoped services resolved the same way any other constructor
  dependency on a Hangfire job class is resolved (per-job DI scope) — the same pattern
  `VideoTranscriptionJobForTalk` already uses for its existing `ITranscriptionService`
  and `IToolboxTalksDbContext` dependencies.

**One flagged, unresolved fact — out of this recon's scope to settle:** the *entity
lookup* surrounding this unit in legacy code has a latent question. `ContentExtractionService.ExtractContentAsync`
(`ContentExtractionService.cs:88-89`) and `ContentGenerationService.GenerateContentAsync`
(`ContentGenerationService.cs:67-70`) both query `_dbContext.ToolboxTalks` with an
explicit `.Where(t => t.TenantId == tenantId)` predicate but **no `.IgnoreQueryFilters()`**.
`ToolboxTalk` carries a global EF query filter
`!e.IsDeleted && (BypassTenantFilter || e.TenantId == TenantId)`
(`ApplicationDbContext.cs:355`), where `TenantId`/`BypassTenantFilter` on the DbContext
resolve from `ICurrentUserService` (`ApplicationDbContext.cs:36,41`), which in turn reads
`IHttpContextAccessor.HttpContext` — `null` inside a Hangfire job — falling back to
`Guid.Empty`/`false` unless a job has explicitly populated the scoped
`IJobTenantContextAccessor` (`CurrentUserService.cs:124-133`; confirmed via grep that only
`BulkSopImportJob.cs:206` currently does this anywhere in the codebase). Neither
`ContentGenerationJob` nor `ContentGenerationService` nor `ContentExtractionService` sets
it. Whether this is an already-live gap in the legacy `/generate` path, or whether some
other mechanism not found in this recon's search compensates, was **not resolved** — it
is orthogonal to the transcript-structuring question (the reusable unit itself,
`GenerateSrt`/`ParseSrtContent`, needs no DB access at all, so it is unaffected either
way) but is directly relevant if a fix chooses to reuse `IContentExtractionService.ExtractContentAsync`
wholesale rather than the two granular calls (§B6). `VideoTranscriptionJobForTalk`'s own
existing entity lookup already uses `.IgnoreQueryFilters()` plus an explicit
tenant/deleted check (`VideoTranscriptionJobForTalk.cs:35-37`), i.e. the newer job code
already defends against exactly this trap where the older service code may not.

### B6. Reuse boundary: standalone services vs. buried logic

`GenerateSrt` and `ParseSrtContent` are **standalone, already-public interface methods on
services with zero other logic entangled** — they are not private helpers buried inside
`ContentExtractionService`. `ContentExtractionService` merely *calls* them
(`ContentExtractionService.cs:450-452` and `:592`) as one of several injected
dependencies (`ContentExtractionService.cs:30,28` — `_srtGeneratorService`,
`_transcriptService` fields). Nothing about them needs to be extracted or refactored to
be reused elsewhere; they can be injected into `VideoTranscriptionJobForTalk`'s
constructor exactly as `ContentExtractionService`'s constructor already does
(`ContentExtractionService.cs:39-67`).

Two structurally different reuse options exist, as a matter of fact (not recommendation):

1. **Granular** — inject `ISrtGeneratorService` + `ITranscriptService` +
   `IOptions<SubtitleProcessingSettings>` directly into `VideoTranscriptionJobForTalk`,
   call the two-line chain from §B4 in place of the current `string.Join(" ", ...)`. Adds
   3 constructor parameters. Does not touch subtitle storage (§A1 steps 4-8) at all —
   addresses only the section-parsing structuring gap, not the missing-subtitles gap.
2. **Coarse** — inject `IContentExtractionService` (already registered,
   `ServiceCollectionExtensions.cs:183`) and call `ExtractContentAsync(talkId, includeVideo:
   true, includePdf: false, tenantId, ct)` in place of the current transcription+join
   logic. This single call would reproduce **all nine** legacy steps, including the
   subtitle storage/translation-queueing side effects (§A1 steps 4-8) that option 1 does
   not touch — but inherits whatever answer resolves the open tenant-filter question in
   §B5, and returns `CombinedContent` pre-wrapped with the
   `"=== VIDEO TRANSCRIPT ==="` / `"=== VIDEO FINAL PORTION (80-100%) ==="` section
   markers (`ContentExtractionService.cs:357-383`) that `ContentParserService.ParseContentAsync`
   (the new flow's Claude-calling parser) does not currently expect or need to strip.

### B7. Is storing the generated SRT part of the reusable unit, or separate?

**Separate.** The store step (steps 4-8 in §A1 — R2 upload, `SubtitleProcessingJob` +
`SubtitleTranslation` row creation, per-employee-language queueing, the second Hangfire
job enqueue for translation) is interleaved *between* the two structuring calls in
legacy's source order (`GenerateSrt` at step 3, storage at steps 4-8, `ParseSrtContent`
at step 9) but has **no data dependency** on anything storage produces — `ParseSrtContent`
at step 9 parses the same in-memory `srtContent` local variable computed by `GenerateSrt`
at step 3, not anything re-read from the database or R2 (`ContentExtractionService.cs:592`
references the local `srtContent`, not `subtitleJob.EnglishSrtContent` or a storage
round-trip). So the two structuring calls can be lifted out and called back-to-back with
nothing in between, or the storage steps can be added independently — they are two
separable concerns that legacy happens to run in the same method, not one fused unit.

The storage/queueing steps use `ISrtStorageProvider.UploadSrtAsync`
(`ISrtStorageProvider.cs:17-21`, registered `AddScoped`, `ServiceCollectionExtensions.cs:159-170`),
direct `SubtitleProcessingJob`/`SubtitleTranslation` entity construction against
`IToolboxTalksDbContext`, `ILanguageCodeService`, and
`_backgroundJobClient.Enqueue<SubtitleProcessingOrchestrator>(...)` — none of which
`VideoTranscriptionJobForTalk` currently has injected. Alternatively (§A4), the exact
same outcome (a correctly populated `SubtitleProcessingJob`, without re-calling
ElevenLabs) is already achieved via `ISubtitleProcessingOrchestrator.StartProcessingAsync(...,
cachedTranscriptWordsJson: talk.TranscriptWordsJson, ...)` — a single already-registered
(`ServiceCollectionExtensions.cs:176`, `AddScoped`) service call, mirroring exactly what
`ContentCreationSessionService.cs:733-739` does for the session-based wizard, and reusing
the `talk.TranscriptWordsJson` the new flow already caches at `VideoTranscriptionJobForTalk.cs:107`.

---

## Summary table — everything a complete fix would need to address

| Gap | Where | Legacy behaviour | New-flow behaviour | Independent of the other gaps? |
|---|---|---|---|---|
| Transcript structure fed to Claude for sectioning | `VideoTranscriptionJobForTalk.cs:92-94` vs `ContentExtractionService.cs:450-452,592` | Timestamped, line-broken (SRT-derived) | Single unbroken line, no timestamps | Prior recon's finding; reusable unit is §B4 |
| `minimumSections` prompt instruction | `ContentParserService.cs:82` vs `AiSectionGenerationService.cs` call chain | Defaults to 7 | Hardcoded 2 | Prior recon's finding; not this doc's concern |
| Video source URL resolution before transcription | `VideoTranscriptionJobForTalk.cs:80` vs `ContentExtractionService.cs:413-425` | Resolves Drive/Blob share links via `IVideoSourceProvider` first | Passes raw `talk.VideoUrl`/`SourceFileUrl` straight to ElevenLabs | Yes — separate from structuring; only manifests for external-URL (non-uploaded) videos |
| Subtitle file generation/storage for the video player | `VideoTranscriptionJobForTalk.cs` (absent) vs `ContentExtractionService.cs:461-585` | Creates `SubtitleProcessingJob` + per-language `SubtitleTranslation` rows, uploads SRT to R2, queues translation job | Nothing — no `SubtitleProcessingJob` ever created automatically | Yes — completely independent side effect; new-wizard talks currently have zero subtitles until/unless an admin manually triggers `/subtitles/process` |
| Reuse of already-fetched ElevenLabs words if subtitles are generated later | `SubtitleProcessingController.cs:78` (hardcoded `null`) vs `ContentCreationSessionService.cs:733-739` (passes cached words) | Session-based wizard passes `session.TranscriptWordsJson` as `cachedTranscriptWordsJson`, skipping a redundant ElevenLabs call | New flow's manual-trigger controller ignores `talk.TranscriptWordsJson` entirely, always re-transcribes | Yes — a cost/efficiency gap, not correctness |

No fix is proposed or implemented in this document, per scope.
