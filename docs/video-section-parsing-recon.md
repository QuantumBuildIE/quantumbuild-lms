# Video Section-Parsing Recon: Old Wizard (3 sections) vs New Wizard (1 section)

**Date:** 2026-08-07
**Type:** Read-only recon. No code changed.
**Question:** Same video, same ElevenLabs transcription — why does the legacy
create-wizard produce three sections and the new wizard produce one (or fewer)?

---

## Headline

Two independent, compounding differences in the **parse step**, both real
code-level differences, not configuration drift:

1. **The "minimum sections" instruction sent to Claude is different** — legacy
   asks for "at least 7", new wizard hardcodes "at least 2". (§1, §2, §3)
2. **The transcript text itself is structurally different** — legacy sends a
   timestamped, line-broken transcript (one line per ~N-word subtitle chunk);
   new wizard sends one unbroken single-line blob of words with no timestamps,
   no line breaks, no structural cues at all. (§6)

The model is identical in both paths (§4) — not the cause. Neither path
detects or guards against under-production; both silently accept whatever
Claude returns, with zero enforcement of the stated minimum (§5). The new
wizard is **not** the default entry point for most tenants today, but is
reachable per-tenant or per-navigation (§7).

---

## 1. Section-parsing step — NEW create workflow (video)

**Entry point:** `ParseStep.tsx` (new wizard, `/admin/toolbox-talks/learnings/**`)
→ "Parse Content" button → `POST /toolbox-talks/{id}/parse` (synchronous) →
`ParseToolboxTalkContentCommandHandler.Handle` →
`ParseToolboxTalkContentCommandHandler.cs:54` dispatches on `talk.InputMode`.

For `InputMode.Video`:

- `ParseToolboxTalkContentCommandHandler.cs:172-190` (`HandleVideoAsync`) — sets
  `talk.Status = Processing` and calls
  `_parseJobScheduler.EnqueueVideoTranscriptionJob(talk.Id, talk.TenantId)`
  (line 187), then returns immediately (frontend polls).
- `ParseJobScheduler.cs:13` (`EnqueueVideoTranscriptionJob`) enqueues Hangfire
  job `VideoTranscriptionJobForTalk`.
- `VideoTranscriptionJobForTalk.cs:80` calls
  `transcriptionService.TranscribeAsync(videoUrl, ct)` (ElevenLabs, via
  `ITranscriptionService`/`ElevenLabsTranscriptionService`).
- `VideoTranscriptionJobForTalk.cs:92-94` builds the transcript text:
  ```csharp
  var transcriptText = string.Join(" ", result.Words
      .Where(w => w.Type == "word")
      .Select(w => w.Text));
  ```
  — every word from ElevenLabs joined with a single space. **No timestamps, no
  line breaks, no segment boundaries of any kind.**
- `VideoTranscriptionJobForTalk.cs:106` stores this as
  `talk.ExtractedVideoTranscript = transcriptText;`, then
  `VideoTranscriptionJobForTalk.cs:115-116` enqueues
  `ContentCreationParseJobForTalk`.
- `ContentCreationParseJobForTalk.cs:70-76` calls
  ```csharp
  contentParserService.ParseContentAsync(
      rawText: talk.ExtractedVideoTranscript,
      inputModeHint: InputMode.Video,
      tenantId: tenantId,
      userId: null,
      preserveSourceWording: talk.PreserveSourceWording,
      cancellationToken: cancellationToken);
  ```
  i.e. `IContentParserService.ParseContentAsync` — implementation:
  `ContentParserService.cs`.

### `ContentParserService.ParseContentAsync` (`ContentParserService.cs:41-175`)

- **Prompt builder call** — `ContentParserService.cs:79-85`:
  ```csharp
  var prompt = SectionGenerationPrompts.BuildSectionPrompt(
      content: rawText,
      sourceDescription: sourceDescription,
      minimumSections: 2,                              // <-- hardcoded
      hasVideo: inputModeHint == InputMode.Video,
      hasPdf: inputModeHint == InputMode.Pdf,
      preserveSourceWording: preserveSourceWording);
  ```
  `minimumSections` is a **hardcoded literal `2`** — not configurable, not
  passed from the frontend, not read from any request/option object. Grep of
  `web/**/*.ts*` for `minimumSections`/`MinimumSections` confirms
  `web/src/lib/api/toolbox-talks/content-creation.ts` never sends this field
  to the new-wizard endpoints — there is no UI control for it in the new
  wizard.
- **Model** — `ContentParserService.cs:36`:
  `_claudeModel = aiProviders.Value.Anthropic.Models.Sonnet;` (resolved at
  construction from `IOptions<AIProviderOptions>`).
- **Request body** — `ContentParserService.cs:87-95`:
  ```csharp
  var requestBody = new {
      model = _claudeModel,
      max_tokens = 8000,
      messages = new[] { new { role = "user", content = prompt } }
  };
  ```
  No `temperature` override — Anthropic API default applies, same as legacy
  (legacy also omits `temperature`, see §2).
- **No minimum enforcement after the call.** `ContentParserService.cs:122-146`
  parses the response and returns `Success: true` with whatever
  `ParseSectionsFromContentText` extracted — there is no check comparing
  `sections.Count` against the `minimumSections: 2` that was sent in the
  prompt, and no warning is logged even at 0 or 1 sections (§5).

---

## 2. Section-parsing step — OLD/legacy create workflow (video)

**Entry point:** legacy create-wizard / `ContentCreationSessionService` flow,
or the `POST /toolbox-talks/{id}/generate` and `POST
/toolbox-talks/{id}/smart-generate` endpoints
(`ToolboxTalksController.cs:1240-1334`, `1406-1500`) — both enqueue Hangfire
job `ContentGenerationJob`.

- `ToolboxTalksController.cs:1304-1313` (`/generate`) and
  `ToolboxTalksController.cs:1474-1483` (`/smart-generate`) build
  `ContentGenerationOptions` with:
  ```csharp
  MinimumSections: request.MinimumSections ?? 7,
  ```
  — defaults to **7** if the caller doesn't override it (`MinimumSections`
  default is also declared at `IContentGenerationService.cs:44`:
  `int MinimumSections = 7,` and `SmartGenerateContentCommand.cs:17:
  public int MinimumSections { get; init; } = 7;`).
- `ContentGenerationJob.cs:131-136` calls
  `_generationService.GenerateContentAsync(toolboxTalkId, options, tenantId, progress, ct)`
  → `ContentGenerationService.cs` (`IContentGenerationService`).
- `ContentGenerationService.cs:110-111` calls
  `_extractionService.ExtractContentAsync(...)` (`IContentExtractionService` /
  `ContentExtractionService.cs`) to get `extractionResult.CombinedContent` —
  see §6 for exactly what this transcript text looks like.
- `ContentGenerationService.cs:179-187` calls the section generator:
  ```csharp
  var sectionResult = await _sectionService.GenerateSectionsAsync(
      toolboxTalkId,
      extractionResult.CombinedContent!,
      extractionResult.VideoContent != null,
      extractionResult.PdfContent != null,
      tenantId: tenantId,
      userId: _currentUser.UserIdGuid,
      minimumSections: options.MinimumSections,   // 7 by default
      cancellationToken: cancellationToken);
  ```
  `_sectionService` is `IAiSectionGenerationService` →
  `AiSectionGenerationService.cs`.

### `AiSectionGenerationService.GenerateSectionsAsync` (`AiSectionGenerationService.cs:41-179`)

- **Prompt builder call** — `AiSectionGenerationService.cs:81`:
  ```csharp
  var prompt = SectionGenerationPrompts.BuildSectionPrompt(
      combinedContent, sourceDescription, minimumSections, hasVideoContent, hasPdfContent);
  ```
  Same shared prompt builder as the new wizard
  (`SectionGenerationPrompts.cs`), but `minimumSections` here is whatever
  `ContentGenerationOptions.MinimumSections` resolved to — **7** by default,
  vs the new wizard's hardcoded **2**.
- **Model** — `AiSectionGenerationService.cs:35`:
  `_claudeModel = aiProviders.Value.Anthropic.Models.Sonnet;` — identical
  config key to the new wizard (§4).
- **Request body** — `AiSectionGenerationService.cs:83-91`:
  ```csharp
  var requestBody = new {
      model = _claudeModel,
      max_tokens = 8000, // Larger for section generation
      messages = new[] { new { role = "user", content = prompt } }
  };
  ```
  Identical `max_tokens = 8000` to the new wizard; no `temperature` set here
  either.
- **Minimum check exists but is non-blocking** —
  `AiSectionGenerationService.cs:135-140`:
  ```csharp
  if (sections.Count < minimumSections)
  {
      _logger.LogWarning(
          "AI generated only {Count} sections for toolbox talk {Id}, minimum was {Minimum}",
          sections.Count, toolboxTalkId, minimumSections);
  }
  ```
  This is a **log line only** — it is not surfaced in `SectionGenerationResult`
  (no `Warnings` field on that record), not returned to the frontend, and does
  not block `Success: true` at line 146. Functionally the same "silently
  accept whatever came back" behaviour as the new wizard (§5) — the only
  difference is legacy logs a warning server-side and new wizard doesn't even
  do that.

---

## 3. The diff, explicit

| | Legacy (`AiSectionGenerationService`) | New wizard (`ContentParserService`) |
|---|---|---|
| Prompt builder | `SectionGenerationPrompts.BuildSectionPrompt` (shared) | Same shared function |
| `minimumSections` passed to prompt | `options.MinimumSections`, defaults to **7** (`ToolboxTalksController.cs:1307/1448/1477`, `IContentGenerationService.cs:44`) | **Hardcoded `2`** (`ContentParserService.cs:82`) — not configurable, no request field |
| Prompt text produced (non-`preserveSourceWording` branch, `SectionGenerationPrompts.cs:80`) | `"Create at least 7 sections (more if the content warrants it)"` | `"Create at least 2 sections (more if the content warrants it)"` |
| Model | `aiProviders.Value.Anthropic.Models.Sonnet` (`AiSectionGenerationService.cs:35`) | Same key: `aiProviders.Value.Anthropic.Models.Sonnet` (`ContentParserService.cs:36`) — **identical**, see §4 |
| `max_tokens` | `8000` (`AiSectionGenerationService.cs:86`) | `8000` (`ContentParserService.cs:90`) — **identical** |
| `temperature` | not set (API default) | not set (API default) — **identical** |
| HTTP client timeout/retry policy | 3 min timeout, `GetClaudePolicy` (`ServiceCollectionExtensions.cs:186-192`) | 3 min timeout, `GetClaudePolicy` (`ServiceCollectionExtensions.cs:337-343`) — **identical** |
| Minimum enforcement after response | Logs a warning if under minimum, does not fail (`AiSectionGenerationService.cs:135-140`) | No check at all — no log, no warning (`ContentParserService.cs:122-146`) |
| Transcript text shape fed into the prompt | Timestamped, line-broken (SRT-derived) — see §6 | Single unbroken line of words, no timestamps — see §6 |

**Two real differences drive the section-count gap, not one:** the prompt's
stated minimum (7 vs 2) and — more consequentially, per §6 — the structural
shape of the transcript text itself. The model, token limit, temperature, and
timeout/retry config are all identical between the two paths, so none of
those explain the gap.

---

## 4. Model version

Both services resolve the model from the exact same configuration key:
`aiProviders.Value.Anthropic.Models.Sonnet` (`AIProviderOptions.cs:32`:
`public string Sonnet { get; set; } = string.Empty;`).

Current value, found in both `src/QuantumBuild.API/appsettings.json:27` and
`appsettings.Development.json:24`:
```json
"Sonnet": "claude-sonnet-4-5"
```

This **is a floating alias**, not a dated/pinned snapshot (a pinned value
would look like `claude-sonnet-4-5-20250929`). Anthropic can move what
`claude-sonnet-4-5` resolves to without any code or config change in this
repo, and both workflows would shift together since they read the identical
key.

**Cannot determine when this value was last changed.** Both
`appsettings.json` and `appsettings.Development.json` are gitignored
(`.gitignore:12`, `.gitignore:13`) — confirmed via `git check-ignore`, and
`git log` on either path returns nothing because the files are untracked.
`appsettings.Testing.json` is the only tracked variant and does not set a
`Sonnet` value. Any production value is set via Railway environment variables
(`AIProviders__Anthropic__Models__Sonnet`), which this session cannot inspect.

**Conclusion on model version:** since both the legacy and new-wizard paths
read the exact same config key, a model-alias drift would affect **both
workflows identically** and cannot by itself explain why the two workflows
differ from each other on the same video. It remains a live risk for
absolute behaviour drift over time (either workflow could quietly produce
different section counts than it did last month), but it is not the cause of
the *old-vs-new* discrepancy.

---

## 5. Truncation / silent-failure check

- **Neither service inspects the Anthropic API's `stop_reason` field.**
  `AnthropicResponseParser.cs` (shared by both services) only extracts
  `content[0].text`, `usage.input_tokens`, `usage.output_tokens`, and `model`
  (`AnthropicResponseParser.cs:20-46`) — `stop_reason` is never read. If a
  response were cut off by hitting `max_tokens` mid-JSON-array, neither path
  would detect it as a truncation specifically; both would fall through to
  their generic JSON-parse-failure handling (both wrapped in `catch
  (JsonException ex)` — `AiSectionGenerationService.cs:161-169`,
  `ContentParserService.cs:157-165`) and report a generic
  `"Failed to parse response"` error, not "response was truncated."
- **`max_tokens = 8000` is identical in both paths** (§3) — not a plausible
  truncation differentiator between old and new, since a short transcript
  (implied by the low section counts on both sides) is nowhere near an 8000
  output-token ceiling for 1-3 short sections.
- **Below-minimum output is silently accepted in both paths**, with different
  degrees of silence:
  - Legacy: logs a warning (`AiSectionGenerationService.cs:135-140`) but
    still returns `Success: true`; nothing propagates to
    `ContentGenerationResult.Warnings` from this check specifically (that
    list is populated from `extractionResult.Warnings` only, see
    `ContentGenerationService.cs:167`, not from section-count shortfall).
  - New wizard: no check, no log, no warning at any section count including
    zero. `ContentParserService.cs:138-140` only logs the count achieved as
    an informational line.
  - Neither path has a hard floor — an AI response returning a single JSON
    object (interpreted as 1 section) or an empty array (`[]` → 0 sections)
    both flow through as a "successful" parse with `Success: true`. This
    matches the "silent truncation" pattern noted elsewhere in this codebase
    (CLAUDE.md).
- **This is a pre-existing shared gap in both workflows, not new-wizard-only**
  — worth fixing regardless of the section-count investigation's outcome, but
  it does not itself explain why new produces fewer sections than old on the
  *same* input; it only explains why nothing alerts anyone when either path
  under-produces.

---

## 6. Transcript input — same source API, different shape reaching the prompt

Scope explicitly asked to confirm both workflows receive the same transcript
text. **They start from the same ElevenLabs call, but the text string that
actually reaches the Claude section-parsing prompt is structurally
different**, which is directly relevant to "why fewer sections."

### New wizard

`VideoTranscriptionJobForTalk.cs:80,92-94`:
```csharp
var result = await transcriptionService.TranscribeAsync(videoUrl, cancellationToken);
...
var transcriptText = string.Join(" ", result.Words
    .Where(w => w.Type == "word")
    .Select(w => w.Text));
```
Every word from the ElevenLabs response is joined with a single space into
**one continuous line** — no timestamps, no punctuation-driven line breaks
beyond what's naturally in the words themselves, no paragraph or segment
markers of any kind. This exact string becomes `talk.ExtractedVideoTranscript`
(`VideoTranscriptionJobForTalk.cs:106`) and is passed verbatim as `rawText` to
`ContentParserService.ParseContentAsync` (`ContentCreationParseJobForTalk.cs:71`).

### Legacy wizard

`ContentExtractionService.cs:112-193` (`ExtractContentAsync`, video branch):

- **If a transcript already exists** (SRT already generated, e.g. from a
  prior subtitle-processing run): `ContentExtractionService.cs:130-148` calls
  `_transcriptService.GetTranscriptAsync(...)` →
  `TranscriptService.cs:26-56`, which fetches the stored English SRT
  (`_orchestrator.GetSrtContentAsync(toolboxTalkId, "en", ...)`) and parses it
  via `ParseSrtContent` (`TranscriptService.cs:59-150`). Critically,
  `TranscriptService.cs:108`:
  ```csharp
  fullTextBuilder.AppendLine($"[{FormatTimestamp(startTime.Value)}] {text}");
  ```
  — the resulting `FullTranscript` string has **one line per SRT subtitle
  cue, each prefixed with a `[mm:ss]` timestamp**.
- **If no transcript exists yet** (first-time transcription, the common case
  for a brand-new video): `ContentExtractionService.cs:169-181` calls
  `AutoTranscribeVideoAsync` (`ContentExtractionService.cs:389-621`), which:
  1. Calls `_transcriptionService.TranscribeAsync(...)` — **the same
     ElevenLabs call** as the new wizard (`ContentExtractionService.cs:432-434`).
  2. Immediately chunks the raw words into subtitle blocks:
     `_srtGeneratorService.GenerateSrt(transcriptionResult.Words,
     _settings.WordsPerSubtitle)` (`ContentExtractionService.cs:450-452`) —
     groups words into `WordsPerSubtitle`-sized cues, each with its own
     timestamp, producing real SRT content.
  3. Parses that generated SRT back via `_transcriptService.ParseSrtContent`
     (`ContentExtractionService.cs:592`), producing the same
     timestamped/line-broken `FullTranscript` shape described above
     (`ContentExtractionService.cs:604-610`).
  4. This is what becomes `extractionResult.CombinedContent` / `VideoContent`
     handed to `AiSectionGenerationService.GenerateSectionsAsync` in §2.

**Net effect:** the legacy path never hands Claude a raw undifferentiated
word blob for video content — it always routes video transcripts through SRT
generation/parsing first, which imposes visual/structural chunking (one line
per subtitle cue, each with a timestamp) before the section-generation prompt
ever sees it. The new wizard skips that step entirely for video and hands
Claude one unbroken line of words. This is a genuine, code-level structural
difference in the *input*, separate from and additive to the
`minimumSections` prompt-instruction difference in §3 — both push in the same
direction (new wizard → fewer sections), and both would apply even with
identical `minimumSections` values.

(Text/PDF/Docx modes in the new wizard funnel through the *same*
`ContentParserService.ParseContentAsync` used here, per
`ParseToolboxTalkContentCommandHandler.cs:60-170` — so this transcript-shape
gap is specific to the **Video** input mode; PDF/Text/Docx content in both
workflows is closer in shape since neither involves SRT chunking.)

---

## 7. Which workflow is the live default today

- **In-code default:** `TenantSettingsService.cs:56`:
  ```csharp
  [TenantSettingKeys.UseNewWizard] = "false",
  ```
  Legacy wizard is the default for any tenant with no explicit
  `TenantSettings` row for the `UseNewWizard` key. A dedicated recon
  (`docs/wizard-toggle-retirement-recon.md`, 2026-07-09) recommended flipping
  this default to `"true"` and hiding the toggle UI, but as of this recon
  (2026-08-07) **that flip has not been applied** — confirmed by re-reading
  `TenantSettingsService.cs:56` directly (still `"false"`) and
  `web/src/app/(authenticated)/admin/toolbox-talks/settings/page.tsx:69`
  (`<WizardToggleSection />` still rendered, not removed).
- **How a tenant/user ends up on the new wizard despite the default:**
  - An admin explicitly flips `TenantSettings.UseNewWizard = "true"` for
    their tenant via Settings → General (`WizardToggleSection`,
    `wizard-toggle-section.tsx`).
  - A user appends `?wizard=new` to any page with the "Create New" button —
    one-shot override, resolved in `useWizardPreference.ts` before the
    tenant-setting fallback, not persisted (CLAUDE.md Note 29).
  - Direct navigation to `/admin/toolbox-talks/learnings/new` regardless of
    the toggle state (the toggle only controls where the "Create New" button
    *routes to*, not whether the new-wizard URLs themselves are reachable).
- **Course creation is always legacy** regardless of the toggle — no
  new-wizard course-creation flow exists (`docs/wizard-toggle-retirement-recon.md`
  Part 4, "Courses are permanently excluded from the toggle").
- **This recon cannot determine, from code alone, whether the specific
  tenant/video in question was on the new wizard by tenant-level toggle, by
  `?wizard=new` override, or by direct URL navigation** — only that the
  mechanism exists for a user to reach the new wizard's talk-based parse path
  (§1) even though it is not the global default.

---

## Summary for the fix decision

To make the new wizard produce comparable section counts to the legacy
wizard for video input, both of the following would need addressing (not
in scope to fix here, per instructions — facts only):

1. `ContentParserService.cs:82` hardcodes `minimumSections: 2` where the
   legacy path defaults to `7` — no request-level override exists for the
   new wizard's talk-based video/PDF/text/docx parse endpoint.
2. `VideoTranscriptionJobForTalk.cs:92-94` builds the video transcript as one
   unbroken line of joined words, where the legacy path
   (`ContentExtractionService.cs`) always routes video transcripts through
   SRT generation/parsing first, giving Claude a timestamped, line-broken
   transcript with built-in structural chunking cues.

Neither the model (`claude-sonnet-4-5`, identical config key in both paths)
nor `max_tokens` (`8000`, identical in both paths) differs between the two
workflows, and neither service inspects `stop_reason`, so truncation is not a
distinguishing factor between old and new — it is a shared, pre-existing blind
spot in both.
