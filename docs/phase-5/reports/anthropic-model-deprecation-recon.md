# Anthropic Model Deprecation Recon
## P0 Production Incident — `claude-sonnet-4-20250514` Retired June 15, 2026

---

## Incident Summary

Anthropic retired `claude-sonnet-4-0` (surface identifier: `claude-sonnet-4-20250514`) on June 15, 2026, following a 60-day deprecation notice issued April 14, 2026. API calls using this identifier now return HTTP 400 `{"error":"NotFound"}`. Production symptom confirmed: `POST /api/toolbox-talks/{id}/parse` began returning HTTP 400 on June 16 after working the previous day. The identifier `claude-sonnet-4-20250514` is the **only deprecated model in the codebase** — no `claude-opus-4-0`, no `claude-3-*` models were found. The identifier `claude-haiku-4-5-20251001` (Haiku 4.5) is present in two hardcoded sites and multiple config files but is **current and unaffected**. The scope of breakage is every code path that calls the Anthropic Messages API with `"claude-sonnet-4-20250514"` as the `model` field — see the executable sites catalog below.

---

## Model Identifier Summary

| Identifier | Anthropic alias | Status | Found in codebase |
|---|---|---|---|
| `claude-sonnet-4-20250514` | `claude-sonnet-4-0` | **RETIRED June 15 2026** | Yes — 9 executable sites |
| `claude-haiku-4-5-20251001` | `claude-haiku-4-5` | Current | Yes — 4 executable sites |
| `claude-sonnet-4-6` | Current Sonnet | Current | Not yet — recommended replacement |
| `claude-opus-4-0` / `claude-opus-4-5` | Opus 4 | — | Not found |
| `claude-3-*` any | Deprecated | Deprecated | Not found |

---

## Step 1 — Complete Grep Catalog

All search patterns (`claude-`, `api.anthropic`, `anthropic.com`) were run across `*.cs`, `*.json`, `*.ts`, `*.tsx`. Full results follow.

### C# source files

| File | Line | Identifier | Execution path? |
|---|---|---|---|
| `src/Core/QuantumBuild.Core.Application/Abstractions/AI/ClaudeSettings.cs` | 23 | `claude-sonnet-4-20250514` | **Yes** — C# default value for config property |
| `src/QuantumBuild.API/Controllers/HelpChatController.cs` | 48 | `claude-sonnet-4-20250514` | **Yes** — hardcoded in request body literal |
| `src/Modules/ToolboxTalks/.../Jobs/RequirementMappingJob.cs` | 23 | `claude-sonnet-4-20250514` | **Yes** — `private const string SonnetModel` |
| `src/Modules/ToolboxTalks/.../Jobs/RequirementIngestionJob.cs` | 23 | `claude-sonnet-4-20250514` | **Yes** — `private const string SonnetModel` |
| `src/Modules/ToolboxTalks/.../Services/Validation/RegulatoryScoreService.cs` | 23 | `claude-sonnet-4-20250514` | **Yes** — `private const string SonnetModel` |
| `src/Modules/ToolboxTalks/.../Services/Validation/ClaudeSonnetBackTranslationService.cs` | 79 | `_tvSettings.Round3DModel` | **Yes** — config-driven (reads `TranslationValidationSettings.Round3DModel`) |
| `src/Modules/ToolboxTalks/.../Configuration/TranslationValidationSettings.cs` | 74 | `claude-sonnet-4-20250514` | **Yes** — C# default value for `Round3DModel` property |
| `src/Modules/ToolboxTalks/.../Services/Validation/DialectDetectionService.cs` | 18 | `claude-haiku-4-5-20251001` | Yes — hardcoded const (current model, not deprecated) |
| `src/Modules/ToolboxTalks/.../Services/Validation/PreFlightScanService.cs` | 18 | `claude-haiku-4-5-20251001` | Yes — hardcoded const (current model, not deprecated) |
| `src/Modules/ToolboxTalks/.../Services/Subtitles/ClaudeTranslationService.cs` | 56 | `_settings.Claude.Model` | **Yes** — config-driven (reads `SubtitleProcessingSettings.Claude.Model`) |
| `src/Modules/LessonParser/.../Services/LessonGeneratorService.cs` | 300 | `_claudeSettings.Model` | **Yes** — config-driven (reads `ClaudeSettings.Model`) |
| `src/Modules/ToolboxTalks/.../Services/Validation/ConsensusEngine.cs` | 9, 12 | `claude-haiku-4-5-20251001`, `claude-sonnet-4-20250514` | No — comments only |
| `src/Modules/ToolboxTalks/.../Services/Validation/ClaudeSonnetBackTranslationService.cs` | 13 | `claude-sonnet-4-20250514` | No — XML doc comment only |
| `src/Modules/ToolboxTalks/.../Services/Validation/DeepSeekTranslationService.cs` | 14 | `claude-sonnet-4-20250514` | No — `[Obsolete]` attribute comment only |
| `src/Modules/ToolboxTalks/.../Services/Validation/CostEstimationService.cs` | 13, 17 | `claude-haiku-4-5-20251001`, `claude-sonnet-4-20250514` | No — rate table comments only |
| `src/Modules/ToolboxTalks/.../Application/Abstractions/Validation/IClaudeSonnetBackTranslationService.cs` | 4 | `claude-sonnet-4-20250514` | No — interface XML doc |
| `src/Modules/ToolboxTalks/.../Domain/Entities/AiUsageLog.cs` | 19 | `claude-sonnet-4-20250514` | No — XML doc example string |
| `src/Modules/ToolboxTalks/.../Domain/Entities/AiUsageSummary.cs` | 19 | `claude-sonnet-4-20250514` | No — XML doc example string |
| `src/Modules/ToolboxTalks/.../Domain/Entities/ProviderResultCache.cs` | 19 | `claude-haiku-4-5-20251001` | No — XML doc example string |
| `src/Modules/ToolboxTalks/.../Domain/Entities/PipelineChangeRecord.cs` | 22 | `claude-sonnet-4-20250514` | No — XML doc example string |

### JSON config files

| File | Line | Key | Identifier | Notes |
|---|---|---|---|---|
| `src/QuantumBuild.API/appsettings.json` | 69 | `SubtitleProcessing:Claude:Model` | `claude-sonnet-4-20250514` | **Active config — broken** |
| `src/QuantumBuild.API/appsettings.json` | 127 | `TranslationValidation:Round3Provider` | `claude-sonnet-4-20250514` | **Orphaned key** — no C# property reads `Round3Provider`; actual property is `Round3DModel` |
| `src/QuantumBuild.API/appsettings.json` | 128 | `TranslationValidation:Round1AModel` | `claude-haiku-4-5-20251001` | Current model, not deprecated |
| `src/QuantumBuild.API/appsettings.json` | 129 | `TranslationValidation:Round3DModel` | `claude-sonnet-4-20250514` | **Active config — broken** |
| `src/QuantumBuild.API/appsettings.Development.json` | 29 | `SubtitleProcessing:Claude:Model` | `claude-sonnet-4-20250514` | **Active config — broken (Development)** |
| `src/QuantumBuild.API/appsettings.json` | 117 | `TranslationValidation:DeepSeek:_comment` | `claude-sonnet-4-20250514` | Config comment, not read by code |

### Test files

| File | Line | Identifier | Notes |
|---|---|---|---|
| `tests/QuantumBuild.Tests.Unit/ToolboxTalks/Subtitles/ClaudeTranslationServiceTests.cs` | 37, 392 | `claude-sonnet-4-20250514` | Test fixture uses mock HttpClient — does NOT call real Anthropic API. Not causing test failures but stale. |

### TypeScript / Frontend

No Claude model identifiers found in `web/src/`. The frontend has no direct Anthropic SDK usage — all AI calls go through the .NET backend.

---

## Step 2 — Classification by Feature

### Feature A: Subtitle SRT Translation
**Service:** `ClaudeTranslationService.TranslateSrtBatchAsync`
**Config path:** `SubtitleProcessing:Claude:Model` via `SubtitleProcessingSettings.Claude.Model`
**Sites:**
- `appsettings.json:69` — active config value `"claude-sonnet-4-20250514"` (**broken**)
- `appsettings.Development.json:29` — development config value `"claude-sonnet-4-20250514"` (**broken**)
- `ClaudeSettings.cs:23` — C# default value `"claude-sonnet-4-20250514"` (**would break if config key missing**)
**Trigger:** `SubtitleProcessingOrchestrator` → `ClaudeTranslationService` when subtitle translation is requested.
**Volume:** Per subtitle batch during subtitle processing. Medium volume.

---

### Feature B: Lesson Parse / Bulk Document Generation
**Service:** `LessonGeneratorService.GenerateFromContentAsync`
**Config path:** `SubtitleProcessing:Claude:Model` via `ClaudeSettings.Model` (same `ClaudeSettings` class as Feature A)
**Sites:**
- `appsettings.json:69` — shared with Feature A (**broken**)
- `appsettings.Development.json:29` — shared with Feature A (**broken**)
- `ClaudeSettings.cs:23` — shared with Feature A (**would break if config key missing**)
**Trigger:** `LessonParseJob` → `LessonGeneratorService` when a document parse job runs. This is the **confirmed production symptom** — `POST /api/toolbox-talks/{id}/parse` calls through to this service.
**Volume:** On-demand. One Anthropic call per parse job.

---

### Feature C: TransVal Consensus Engine — Round 3D Back-Translation
**Service:** `ClaudeSonnetBackTranslationService.BackTranslateAsync`
**Config path:** `TranslationValidation:Round3DModel` via `TranslationValidationSettings.Round3DModel`
**Sites:**
- `appsettings.json:129` — active config value `"claude-sonnet-4-20250514"` (**broken**)
- `TranslationValidationSettings.cs:74` — C# default value `"claude-sonnet-4-20250514"` (**would break if config key missing**)
**Trigger:** `ConsensusEngine` escalates to Round 3 (estimated 30% of sections). Only fires when Rounds 1+2 produce inconclusive agreement.
**Volume:** Per-section, ~30% reach Round 3. Quality-critical — this is the final tiebreaker for translation accuracy verdicts.

---

### Feature D: Help Chat Assistant
**Service:** `HelpChatController.Chat`
**Config path:** **None — hardcoded literal** `model = "claude-sonnet-4-20250514"` in anonymous object at line 48.
**Sites:**
- `HelpChatController.cs:48` — **hardcoded in request body** (**broken**)
**Trigger:** Any authenticated user clicking the help chat assistant.
**Volume:** On-demand per chat message.

---

### Feature E: Regulatory Requirement Mapping
**Service:** `RequirementMappingJob.MapRequirementsAsync`
**Config path:** **None — hardcoded const** `private const string SonnetModel = "claude-sonnet-4-20250514"` at line 23.
**Sites:**
- `RequirementMappingJob.cs:23` — **hardcoded const** (**broken**)
**Trigger:** Fired from publish flow when a talk or course is published. Background Hangfire job.
**Volume:** Per-publish. Low frequency but blocks mapping updates for newly published content.

---

### Feature F: Regulatory Requirement Ingestion
**Service:** `RequirementIngestionJob.ExecuteAsync`
**Config path:** **None — hardcoded const** `private const string SonnetModel = "claude-sonnet-4-20250514"` at line 23.
**Sites:**
- `RequirementIngestionJob.cs:23` — **hardcoded const** (**broken**)
**Trigger:** SuperUser triggers ingestion of a regulatory document via `POST /api/regulatory/documents/{id}/ingest`.
**Volume:** On-demand SuperUser action. Low frequency.

---

### Feature G: TransVal Regulatory Scoring
**Service:** `RegulatoryScoreService.ScoreAsync`
**Config path:** **None — hardcoded const** `private const string SonnetModel = "claude-sonnet-4-20250514"` at line 23.
**Sites:**
- `RegulatoryScoreService.cs:23` — **hardcoded const** (**broken**)
**Trigger:** `POST /api/toolbox-talks/validation-runs/{runId}/regulatory-score` triggers scoring against sector-specific regulatory criteria.
**Volume:** On-demand. Low frequency.

---

### Feature H: Dialect Detection (NOT BROKEN)
**Service:** `DialectDetectionService.DetectAsync`
**Config path:** Hardcoded const `private const string HaikuModel = "claude-haiku-4-5-20251001"`
**Sites:**
- `DialectDetectionService.cs:18` — hardcoded but **current model**
**Status:** No action needed. `claude-haiku-4-5-20251001` is the current Haiku 4.5 identifier.

---

### Feature I: Pre-Flight Scan (NOT BROKEN)
**Service:** `PreFlightScanService.ScanAsync`
**Config path:** Hardcoded const `private const string HaikuModel = "claude-haiku-4-5-20251001"`
**Sites:**
- `PreFlightScanService.cs:18` — hardcoded but **current model**
**Status:** No action needed.

---

## Step 3 — Per-Site Replacement Recommendations

### Recommended replacement model

Per CLAUDE.md system prompt (current as of today): `claude-sonnet-4-6` is the current Sonnet 4 model. The Anthropic migration guide says `claude-sonnet-4-5` is the like-for-like replacement for `claude-sonnet-4-0`; `claude-sonnet-4-6` is newer and also suitable.

**Recommendation: replace all instances of `claude-sonnet-4-20250514` with `claude-sonnet-4-6`.**

Rationale: `claude-sonnet-4-6` is the active model as of June 2026. Using it rather than `claude-sonnet-4-5` avoids another forced upgrade if `claude-sonnet-4-5` reaches end-of-life in the near term. All call sites in this codebase are straightforward text-generation workloads that map cleanly to any Sonnet tier.

---

### Site-by-site breakdown

#### Sites 1a–1c: Config-driven `SubtitleProcessing:Claude:Model`
_(ClaudeTranslationService + LessonGeneratorService)_

| File | Current | Recommended |
|---|---|---|
| `appsettings.json:69` | `claude-sonnet-4-20250514` | `claude-sonnet-4-6` |
| `appsettings.Development.json:29` | `claude-sonnet-4-20250514` | `claude-sonnet-4-6` |
| `ClaudeSettings.cs:23` (C# default) | `claude-sonnet-4-20250514` | `claude-sonnet-4-6` |

**Reasoning:** Both consumers (subtitle SRT translation and lesson generation) are standard text-in / structured-JSON-out calls. No extended thinking, no tool use, no agentic chaining. Sonnet 4.6 is suitable and forward-stable. `max_tokens = 4000` (subtitle) and `max_tokens = 16000` (lesson generation) — check these remain within the new model's output token ceiling, which for Sonnet 4.6 is 8192 tokens by default / 64K with extended thinking. The 16000 for lesson generation MAY need adjustment — flag for smoke test.

**Param changes:** None for the model swap itself. The 16000 max_tokens in LessonGeneratorService should be verified against the new model's documented limits.

---

#### Site 2a–2b: Config-driven `TranslationValidation:Round3DModel`
_(ClaudeSonnetBackTranslationService)_

| File | Current | Recommended |
|---|---|---|
| `appsettings.json:129` | `claude-sonnet-4-20250514` | `claude-sonnet-4-6` |
| `TranslationValidationSettings.cs:74` (C# default) | `claude-sonnet-4-20250514` | `claude-sonnet-4-6` |

**Reasoning:** This is the Round 3 tiebreaker in the consensus engine — the highest-stakes AI call in the TransVal pipeline. The call is short text back-translation: input = translated text, output = back-translated text. Quality matters, but it is not a reasoning or agentic task. Sonnet 4.6 is the right tier. `max_tokens = 4096` set in the service is fine.

**Quality flag:** This site warrants a smoke test after patching. Run a sample section through the full validation pipeline and confirm the score distribution is consistent with pre-deprecation baselines. A 5-10 point shift in lexical score means the Round 3 swap changed back-translation style — investigate if observed.

---

#### Site 3: Hardcoded — `HelpChatController.cs:48`

| File | Current | Recommended |
|---|---|---|
| `HelpChatController.cs:48` | `"claude-sonnet-4-20250514"` | `"claude-sonnet-4-6"` |

**Reasoning:** Help chat is the lowest-stakes AI surface in the product. Simple Q&A navigation assistance. Sonnet 4.6 is more than adequate; Haiku 4.5 could work here too (lower cost), but keeping it on Sonnet maintains consistency with other surfaces and avoids a quality downgrade for end users. String-swap only.

**Param changes:** `max_tokens = 1000` is fine.

---

#### Site 4: Hardcoded — `RequirementMappingJob.cs:23`

| File | Current | Recommended |
|---|---|---|
| `RequirementMappingJob.cs:23` | `"claude-sonnet-4-20250514"` | `"claude-sonnet-4-6"` |

**Reasoning:** Analyses published training content and maps it to regulatory requirements. Requires good reasoning over structured content. Sonnet 4.6 is appropriate. `MaxTokens = 8192` is fine — well within limits.

**Param changes:** None.

---

#### Site 5: Hardcoded — `RequirementIngestionJob.cs:23`

| File | Current | Recommended |
|---|---|---|
| `RequirementIngestionJob.cs:23` | `"claude-sonnet-4-20250514"` | `"claude-sonnet-4-6"` |

**Reasoning:** Extracts regulatory requirements from document URLs. Requires structured extraction and categorisation. Sonnet 4.6 is appropriate. `MaxTokens = 8192` fine.

**Param changes:** None.

---

#### Site 6: Hardcoded — `RegulatoryScoreService.cs:23`

| File | Current | Recommended |
|---|---|---|
| `RegulatoryScoreService.cs:23` | `"claude-sonnet-4-20250514"` | `"claude-sonnet-4-6"` |

**Reasoning:** Scores translation validation runs against regulatory criteria — a structured scoring task that requires good analytical reasoning. Sonnet 4.6 is appropriate; Opus would be overkill for what is essentially a rubric-scoring call. `MaxTokens = 4096` fine.

**Param changes:** None.

---

#### Haiku sites — no action required

`DialectDetectionService.cs:18` and `PreFlightScanService.cs:18` both use `claude-haiku-4-5-20251001` which is the current Haiku 4.5 model. No change needed.

---

## Step 4 — Configuration Structure Assessment

### Current state

The codebase uses a **mixed pattern**:

**Pattern A: Config-driven (good)**

Two AI-model config paths exist and work correctly:
1. `SubtitleProcessing:Claude:Model` — bound to `ClaudeSettings.Model`. Used by `ClaudeTranslationService` and `LessonGeneratorService`. Changing the model requires only an env var update in Railway + an appsettings.json change — no code deploy.
2. `TranslationValidation:Round3DModel` — bound to `TranslationValidationSettings.Round3DModel`. Used by `ClaudeSonnetBackTranslationService`. Same env-var-only fix path.

The C# default values in `ClaudeSettings.cs:23` and `TranslationValidationSettings.cs:74` act as in-code fallbacks. Because the appsettings.json files also specify these values, the C# defaults are effectively dead weight in production — but they ARE a hazard: if a config key is ever missing from a deployment, the C# default silently activates the deprecated model. **Both the appsettings.json entries AND the C# defaults must be updated.**

**Pattern B: Hardcoded (fragile)**

Four services have `private const string SonnetModel = "claude-sonnet-4-20250514"` (or an equivalent inline literal in `HelpChatController`). These:
- Cannot be changed without a code deploy
- Do not appear in any config scan or validation tooling
- Were the direct cause of breakage for Features D–G
- Are structurally identical to the failed pattern documented in Anthropic's own deprecation guide ("hardcoded model IDs")

### Fragility assessment

The deprecation-safe config-driven path exists but isn't consistently used. The four hardcoded sites bypassed it, presumably because the authors of those features didn't know about `ClaudeSettings` or felt it was scoped to subtitles. The `ClaudeSettings.SectionName = "SubtitleProcessing:Claude"` naming reinforces that perception — a reader of `RequirementMappingJob.cs` would not intuitively reach for a class called `ClaudeSettings` under `SubtitleProcessing`.

Additionally: `appsettings.json:127` contains `Round3Provider: "claude-sonnet-4-20250514"` — a key with no corresponding C# property. `TranslationValidationSettings` has `Round3DModel` but not `Round3Provider`. This orphaned key does nothing but creates confusion about which config value is actually active.

### Recommendation

**Ship a minimum-viable patch immediately** (production is broken): string-swap all 6 deprecated identifier sites (4 code files + 2 appsettings entries in each of appsettings.json and appsettings.Development.json, plus the C# defaults). This unblocks production with no structural changes.

**Open a follow-up BACKLOG item for the structural patch**: route the 4 hardcoded constants through a shared config key. Rename `ClaudeSettings.SectionName` to something not tied to "SubtitleProcessing" (or add a module-agnostic alias). Remove the orphaned `Round3Provider` key. This prevents the next deprecation from requiring a code deploy for these 4 sites.

---

## Step 5 — Other Potentially Affected Models

Beyond the two primary deprecated identifiers, the grep found the following:

| Identifier | Status | Found at |
|---|---|---|
| `claude-haiku-4-5-20251001` | **Current** | `DialectDetectionService.cs:18`, `PreFlightScanService.cs:18`, `TranslationValidationSettings.cs:67`, `appsettings.json:128` |

No instances of:
- `claude-3-5-sonnet-*` (deprecated, retired earlier 2026)
- `claude-3-haiku-*` (deprecated)
- `claude-opus-4-0` / `claude-opus-4-5`
- Any `claude-2-*` or `claude-1-*` identifiers

**Observation on Haiku 4.5:** `claude-haiku-4-5-20251001` is current as of today and does not require action. However, it is hardcoded in two services (`DialectDetectionService`, `PreFlightScanService`). When Haiku 4.5 is eventually deprecated (Anthropic will announce with 60 days notice), those two sites will require a code deploy, same as the four Sonnet sites in this incident. The structural patch backlog item should cover Haiku too.

---

## Step 6 — Verification Approach for the Patch

_(Reference material for the next chunk — do not execute here.)_

### Minimum post-patch smoke tests

After deploying the patch to Development, verify each broken feature with at least one end-to-end call before promoting to Production:

1. **Feature A — Subtitle SRT translation:** Start subtitle processing on a talk that already has a video. Confirm the SignalR hub sends `SubtitleProcessingProgress` events and the job reaches `Completed` status with SRT files written to R2.

2. **Feature B — Lesson Parse:** `POST /api/toolbox-talks/parse` (or whichever endpoint triggers `LessonParseJob`). Confirm the job runs, creates talks + a course, and no HTTP 400 from Anthropic appears in logs.

3. **Feature C — TransVal Round 3D:** Start a translation validation run on a talk with multiple sections. Let it complete. Confirm at least one Round 3 result appears in the run detail (or check logs for `ClaudeSonnetBackTranslationService` calls). Confirm no `NotFound` Anthropic errors.

4. **Feature D — Help Chat:** Open the in-app help chat and send a question. Confirm a response is returned with HTTP 200 from `POST /api/help/chat`.

5. **Feature E — Requirement Mapping:** Publish a talk (or trigger the mapping job manually via Hangfire dashboard). Confirm no Anthropic 400 in logs and that pending mappings appear.

6. **Feature F — Requirement Ingestion:** Trigger ingestion via `POST /api/regulatory/documents/{id}/ingest`. Confirm draft requirements are created.

7. **Feature G — Regulatory Scoring:** `POST /api/toolbox-talks/validation-runs/{runId}/regulatory-score`. Confirm a score result is returned.

### Quality regression check — TransVal Round 3D (Feature C)

The consensus engine is the most quality-sensitive surface. Recommend:
- Pick one talk that has a stable existing validation run (Pass outcome, score > 80).
- Re-run translation validation with the new model in place on Development.
- Compare the per-section scores from the new run against the prior run.
- If any section drops from Pass to Review/Fail, investigate whether the scoring shift is in lexical similarity (LexicalScoringService) or in the back-translation quality (the new model produces different phrasing).
- A ±5 point shift is within noise. A ±15+ point shift warrants investigating the new model's back-translation style before promoting to Production.

### Log inspection window

After deploying to Development:
- Monitor Railway logs for 30 minutes covering at least one of each job type.
- Grep for `NotFound`, `400`, `"error":"NotFound"` to confirm no Anthropic errors remain.
- Confirm no new `claude-sonnet-4-20250514` references appear in logs (would indicate a missed site).

### Promote to Production only after

- All 7 smoke tests pass on Development
- TransVal quality delta is within ±5 points
- No Anthropic 400 errors in Development logs
- Both `origin/transval` and `company/transval` pushed (per Railway dual-remote requirement in CLAUDE.md)

---

## Adjacent Observations

1. **API keys exposed in appsettings.json:** Both `appsettings.json` and `appsettings.Development.json` contain live API keys (Anthropic, ElevenLabs, Cloudflare R2, MailerSend). These are committed to the repository. This is outside the scope of this recon but is a security observation worth noting.

2. **Orphaned config key `Round3Provider`:** `appsettings.json:127` contains `"Round3Provider": "claude-sonnet-4-20250514"`. No C# property in `TranslationValidationSettings` reads this key — the actual property is `Round3DModel`. This key does nothing. It should be removed to avoid misleading future readers who may think it controls something. Include in the minimum patch for cleanliness (it's a one-line JSON deletion).

3. **LessonGeneratorService uses inline JSON parsing instead of AnthropicResponseParser:** `LessonGeneratorService.ParseClaudeResponse` (lines 346–379) has its own `JsonDocument`-based response parsing instead of calling `AnthropicResponseParser.Parse()` as required by CLAUDE.md conventions. The comment says "inline parsing to avoid cross-module Infrastructure dependency." This is a convention violation noted in CLAUDE.md under AI Usage Logging. Not a blocker for the deprecation patch, but worth a follow-up cleanup.

4. **`TranslationValidationSettings` has no `Round3Provider` property:** The config key in appsettings.json and the C# property name diverged at some point (`Round3Provider` vs `Round3DModel`). The C# class is authoritative; the appsettings.json key is the orphan. See item 2.

5. **No OpenAI / other LLM providers:** The frontend and backend contain no references to OpenAI, LangChain, Gemini SDK (only Gemini via raw HTTP), or other LLM providers beyond what is already documented (DeepL, Gemini, ElevenLabs). This incident is entirely scoped to Anthropic.

---

## Recommended Patch Shape

**Verdict: Hybrid — ship the string-swap immediately, open a structural follow-up BACKLOG entry.**

### Minimum-viable patch (ship now — production is broken)

Files requiring code changes:
- `src/Core/QuantumBuild.Core.Application/Abstractions/AI/ClaudeSettings.cs:23`
- `src/Modules/ToolboxTalks/.../Configuration/TranslationValidationSettings.cs:74`
- `src/QuantumBuild.API/Controllers/HelpChatController.cs:48`
- `src/Modules/ToolboxTalks/.../Jobs/RequirementMappingJob.cs:23`
- `src/Modules/ToolboxTalks/.../Jobs/RequirementIngestionJob.cs:23`
- `src/Modules/ToolboxTalks/.../Services/Validation/RegulatoryScoreService.cs:23`

Files requiring config changes:
- `src/QuantumBuild.API/appsettings.json:69` (`SubtitleProcessing:Claude:Model`)
- `src/QuantumBuild.API/appsettings.json:129` (`TranslationValidation:Round3DModel`)
- `src/QuantumBuild.API/appsettings.Development.json:29` (`SubtitleProcessing:Claude:Model`)

Optional cleanup in same PR:
- Remove `src/QuantumBuild.API/appsettings.json:127` (orphaned `Round3Provider` key)
- Update `tests/.../ClaudeTranslationServiceTests.cs:37` test fixture for accuracy (not blocking)

Railway env vars to update in parallel (unblocks config-driven sites without waiting for code deploy):
- `SubtitleProcessing__Claude__Model=claude-sonnet-4-6`
- `TranslationValidation__Round3DModel=claude-sonnet-4-6`

**Note:** The hardcoded sites (D, E, F, G) still need the code deploy even if env vars are updated, because they don't read from config.

### Structural follow-up (BACKLOG — medium priority)

- Add `Anthropic:SonnetModel` and `Anthropic:HaikuModel` config keys (or a single `Anthropic:DefaultModel`) that all four hardcoded services read from.
- Update `HelpChatController`, `RequirementMappingJob`, `RequirementIngestionJob`, `RegulatoryScoreService` to inject `IConfiguration` or `IOptions<AnthropicSettings>` rather than using consts.
- Rename `ClaudeSettings.SectionName` away from `"SubtitleProcessing:Claude"` to reduce the perception that it is subtitle-specific.
- Remove `Round3Provider` orphaned config key (if not done in the immediate patch).
- Future deprecation then requires only env var updates for all sites.

---

## Coverage Table

| Recon step | Evidence in this report |
|---|---|
| Step 1 — Grep all model identifiers | Complete catalog in "Step 1" section. 20+ .cs files, 2 JSON files, 0 .ts/.tsx files. |
| Step 2 — Classify by feature | 9 features documented (A–I), each with service name, config path, trigger, volume. |
| Step 3 — Per-site replacement assessment | All 6 deprecated executable sites have recommended replacement with reasoning. |
| Step 4 — Configuration structure assessment | Mixed pattern described; structural fragility assessed; hybrid recommendation given. |
| Step 5 — Other affected identifiers | All Claude model identifiers catalogued. `claude-haiku-4-5-20251001` confirmed current. No `claude-3-*` or Opus found. |
| Step 6 — Verification approach | 7 per-feature smoke tests specified; TransVal quality regression check defined; log inspection guidance included. |
| Adjacent observations | 5 observations documented. |
| Recommended patch shape | Minimum-viable + structural follow-up defined with explicit file list. |
