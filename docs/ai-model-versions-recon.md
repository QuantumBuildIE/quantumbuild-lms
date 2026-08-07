# AI Model Versions — Recon

**Date:** 2026-08-06
**Type:** Read-only recon (no code changes made)
**Branch:** transval

## Headline

**We are not on a deprecated model.** Every live AI call site in the codebase resolves its model identifier at runtime from one centralised config class, `AIProviderOptions`, bound to the `AIProviders` section of `appsettings.json`. Both `appsettings.json` and `appsettings.Development.json` currently set:

| Provider | Model | Currently configured value |
|---|---|---|
| Anthropic (Sonnet tier) | `AIProviders:Anthropic:Models:Sonnet` | `claude-sonnet-4-5` |
| Anthropic (Haiku tier) | `AIProviders:Anthropic:Models:Haiku` | `claude-haiku-4-5-20251001` |
| Google Gemini | `AIProviders:Gemini:Models:Flash` | `gemini-2.0-flash` |
| ElevenLabs | `AIProviders:ElevenLabs:Models:Transcription` | `scribe_v1` |

There is **no functionality for monitoring or auto-updating model versions** — this is tracked as an explicitly open backlog item (`BACKLOG.md` §33, opened 2026-07-17, P3, still Open). The nearest thing to a monitoring signal is the AI usage logger, which records the model string used on every call (see Part B).

This is the second time this exact question has been investigated in this repo. A prior incident (BACKLOG §5.28, 2026-06-15) and its cleanup (§5.29 "Option B", closed 2026-06-22–2026-07-13) are the reason centralisation now exists — see [History](#history--why-this-is-centralised-now) below.

---

## Part A — What models are we using

### A.1 — Centralisation: single source of truth

All model identifiers used by C# code live in one class:

**`src/Core/QuantumBuild.Core.Application/Configuration/AIProviderOptions.cs`** (63 lines)
- `AIProviderOptions.SectionName = "AIProviders"` (line 10)
- `AnthropicModels.Sonnet` (line 32), `AnthropicModels.Haiku` (line 35)
- `GeminiModels.Flash` (line 48)
- `ElevenLabsModels.Transcription` (line 61)

Bound and validated at startup in **`src/QuantumBuild.API/Program.cs:118-121`**:
```csharp
builder.Services.AddOptions<AIProviderOptions>()
    .BindConfiguration(AIProviderOptions.SectionName)
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<AIProviderOptions>, AIProviderOptionsValidator>();
```

The validator (`src/Core/QuantumBuild.Core.Application/Configuration/AIProviderOptionsValidator.cs:16-23`) fails application startup if any of the four model strings is empty — a missing model config is a boot-time crash, not a silent runtime fallback or a first-API-call surprise.

**Every service and Hangfire job in the codebase that calls Anthropic, Gemini, or ElevenLabs reads its model string from this one class via `IOptions<AIProviderOptions>`.** No service hardcodes a model literal for a live call path. Confirmed by two independent full-repo sweeps: (1) grep for `aiProviders.Value.` / `_aiProviders.` across `src/` — 24 matches, all resolving to `AIProviderOptions` properties; (2) grep for the raw literal `anthropic-version` HTTP header (present only in code that makes a direct Anthropic REST call) — 15 files, all 15 cross-checked against sweep (1).

### A.2 — Inventory by feature

| Feature | Service / Job | File : Line | Model property read | Resolved value |
|---|---|---|---|---|
| **Content generation — sections** | `AiSectionGenerationService` | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/AiSectionGenerationService.cs:35` | `aiProviders.Value.Anthropic.Models.Sonnet` | `claude-sonnet-4-5` |
| **Content generation — quiz** | `AiQuizGenerationService` | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/AiQuizGenerationService.cs:35` | `aiProviders.Value.Anthropic.Models.Sonnet` | `claude-sonnet-4-5` |
| **Content generation — PDF/video parsing** | `ContentParserService` | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/ContentCreation/ContentParserService.cs:36` | `aiProviders.Value.Anthropic.Models.Sonnet` | `claude-sonnet-4-5` |
| **Slideshow generation** | `AiSlideshowGenerationService` | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Slideshow/AiSlideshowGenerationService.cs:44` | `aiProviders.Value.Anthropic.Models.Sonnet` | `claude-sonnet-4-5` |
| **Content translation** (sections, quiz, title, email templates) | `ContentTranslationService` | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Translations/ContentTranslationService.cs:38` | `aiProviders.Value.Anthropic.Models.Sonnet` | `claude-sonnet-4-5` |
| **Subtitle translation** (SRT files) | `ClaudeTranslationService` | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Subtitles/ClaudeTranslationService.cs:35` | `aiProviders.Value.Anthropic.Models.Sonnet` | `claude-sonnet-4-5` |
| **Subtitle transcription** | `ElevenLabsTranscriptionService` | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Subtitles/ElevenLabsTranscriptionService.cs:95,103` | `_aiProviders.ElevenLabs.Models.Transcription` | `scribe_v1` |
| **TransVal — Round 1 back-translation (A)** | `ClaudeHaikuBackTranslationService` | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Validation/ClaudeHaikuBackTranslationService.cs:36` | `aiProviders.Value.Anthropic.Models.Haiku` | `claude-haiku-4-5-20251001` |
| **TransVal — Round 1 back-translation (B)** | `DeepLTranslationService` | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Validation/DeepLTranslationService.cs` | *(none — see A.4)* | n/a — DeepL has no model identifier |
| **TransVal — Round 2 back-translation (C)** | `GeminiTranslationService` | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Validation/GeminiTranslationService.cs:32` | `aiProviders.Value.Gemini.Models.Flash` | `gemini-2.0-flash` |
| **TransVal — Round 3 back-translation (D)** | `ClaudeSonnetBackTranslationService` | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Validation/ClaudeSonnetBackTranslationService.cs:37` | `aiProviders.Value.Anthropic.Models.Sonnet` | `claude-sonnet-4-5` |
| **TransVal — Round 3 back-translation (obsolete)** | `DeepSeekTranslationService` — **`[Obsolete]`, never called** (Note 2, CLAUDE.md — removed for GDPR reasons) | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Validation/DeepSeekTranslationService.cs:62` reads `TranslationValidationSettings.DeepSeek.Model`, default `"deepseek-chat"` at `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Configuration/TranslationValidationSettings.cs:141` | hardcoded, dead path | `deepseek-chat` (inert) |
| **TransVal — safety pre-flight scan** | `PreFlightScanService` | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Validation/PreFlightScanService.cs:21` | `aiProviders.Value.Anthropic.Models.Haiku` | `claude-haiku-4-5-20251001` |
| **TransVal — dialect detection** | `DialectDetectionService` | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Validation/DialectDetectionService.cs:36` | `aiProviders.Value.Anthropic.Models.Haiku` | `claude-haiku-4-5-20251001` |
| **TransVal — pipeline version snapshot** (records, doesn't call) | `PipelineVersionService.BuildComponentsJson()` | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/PipelineVersionService.cs:123` (`round1_a`=Haiku), `:125` (`round2_c`=Gemini Flash), `:126` (`round3_d`=Sonnet) | `_aiProviders.Anthropic.Models.Haiku/.Gemini.Models.Flash/.Anthropic.Models.Sonnet` | as above |
| **TransVal — corpus run cost estimate** (rate table only, doesn't call) | `CostEstimationService` | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Validation/CostEstimationService.cs:13-26` | hardcoded EUR-per-1K-token constants labelled by model name in comments (`claude-haiku-4-5-20251001`, `claude-sonnet-4-5`, `gemini-2.0-flash`, DeepL per-char) | static rate table, April 2026 pricing — **flagged stale, see A.5** |
| **Regulatory — requirement extraction (ingestion)** | `RequirementIngestionJob` | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/RequirementIngestionJob.cs:79` | `aiProviders.Value.Anthropic.Models.Sonnet` | `claude-sonnet-4-5` |
| **Regulatory — feature-level map-coverage verification** | `MapCoverageVerifier` / `RegulatoryStructureMapVerificationService` | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Regulatory/MapCoverageVerifier.cs`, `RegulatoryStructureMapVerificationService.cs` | *(none)* — pure comparison logic against `RequirementIngestionJob`'s already-extracted output; makes no AI call of its own | n/a — rides on the Sonnet call above |
| **Regulatory — requirement→content mapping** | `RequirementMappingJob` | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/RequirementMappingJob.cs:56` | `aiProviders.Value.Anthropic.Models.Sonnet` | `claude-sonnet-4-5` |
| **Regulatory — scoring** (source doc quality / pure translation / regulatory-aware translation) | `RegulatoryScoreService` | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Validation/RegulatoryScoreService.cs:52` | `aiProviders.Value.Anthropic.Models.Sonnet` | `claude-sonnet-4-5` |
| **In-app Help / AI chat assistant** | `HelpChatController` | `src/QuantumBuild.API/Controllers/HelpChatController.cs:53` | `_aiProviders.Anthropic.Models.Sonnet` | `claude-sonnet-4-5` |
| **Lesson Parser — SOP/document → course generation** | `LessonGeneratorService` | `src/Modules/LessonParser/QuantumBuild.Modules.LessonParser.Infrastructure/Services/LessonGeneratorService.cs:51` | `aiProviders.Value.Anthropic.Models.Sonnet` | `claude-sonnet-4-5` |
| **Bulk SOP import** (ZIP → Draft learnings) | `BulkSopImportJob` | `src/Core/QuantumBuild.Core.Infrastructure/Jobs/BulkSopImportJob.cs` | *(none)* — creates Draft `ToolboxTalk` rows with no content; no AI call in this job | n/a |

**Not a real model-selection surface:** `LessonParserInfrastructureExtensions.cs:57,68` and `ServiceCollectionExtensions.cs` chain `ResiliencePolicies.GetClaudePolicy(logger)` onto the relevant `HttpClient` registrations for `LessonParser` and other Claude-calling clients — this is retry/backoff policy wiring (per CLAUDE.md's Polly rules), not a model identifier.

### A.3 — Where model strings live (centralisation confirmed)

- **One C# class, one config section.** `AIProviderOptions` (A.1) is the only place a model identifier is declared as a bindable property. All 21 live call sites above read from it via `IOptions<AIProviderOptions>` — none hardcode a literal model string for an active code path.
- **Config file:** `AIProviders` section in `src/QuantumBuild.API/appsettings.json:24-39` and mirrored in `src/QuantumBuild.API/appsettings.Development.json:21-35` (values identical in both — see A.4). `appsettings.Testing.json` has no `AIProviders` section of its own; ASP.NET Core config layering means it inherits the base `appsettings.json` values.
- **No database-stored model setting.** No entity, `TenantSettings` key, or admin UI field controls which model is called. Model selection is not tenant-configurable.
- **One dead/inert exception:** `TranslationValidationSettings.DeepSeek.Model` (`TranslationValidationSettings.cs:141`, default `"deepseek-chat"`) is a leftover hardcoded default on the `[Obsolete]` `DeepSeekTranslationService`, which is never invoked (GDPR removal, CLAUDE.md Note 2). Not reachable at runtime.
- **Doc-comment-only occurrences of the old, retired model string** (`claude-sonnet-4-20250514`) exist purely as illustrative `<summary>` examples in `AiUsageLog.cs:19`, `AiUsageSummary.cs`, `ProviderResultCache.cs`, `PipelineChangeRecord.cs`, and as a `"_comment"` field in `appsettings.json` — none of these are parsed as functional values.

**Verdict: fully centralised.** Updating any model version is a one-line config change (env var or `appsettings.json` edit) with no code deploy required, per the doc comment at `AIProviderOptions.cs:6`. This is by design — it is the direct outcome of a 2026-06-22 migration project ("Option B") undertaken specifically to eliminate scattered model literals after a production incident (see [History](#history--why-this-is-centralised-now)).

### A.4 — Per-environment differences

No difference found between environments in the repo's config files:

| Environment | `AIProviders:Anthropic:Models:Sonnet` | `:Haiku` | `Gemini:Flash` | `ElevenLabs:Transcription` |
|---|---|---|---|---|
| Base (`appsettings.json:27-38`) | `claude-sonnet-4-5` | `claude-haiku-4-5-20251001` | `gemini-2.0-flash` | `scribe_v1` |
| Development (`appsettings.Development.json:24-35`) | `claude-sonnet-4-5` | `claude-haiku-4-5-20251001` | `gemini-2.0-flash` | `scribe_v1` |
| Testing (`appsettings.Testing.json`) | *(inherits base — no override)* | *(inherits base)* | *(inherits base)* | *(inherits base)* |

Per CLAUDE.md's deployment section, Production and Demo are Railway-hosted and can override any `AIProviders:*` key via env vars (`AIProviders__Anthropic__Models__Sonnet` etc., using ASP.NET Core's `__` env-var separator). **The actual values set in Railway Production/Development/Demo env vars are not visible from this repo** — this recon can only confirm the repo's own config files agree, and that a missing/wrong Railway env var would be caught at startup by `AIProviderOptionsValidator` (A.1) rather than silently falling back to something stale.

### A.5 — Pinned vs. floating, per model string

| Model string | Naming pattern | Classification | Deprecation exposure |
|---|---|---|---|
| `claude-sonnet-4-5` | No date suffix | **Floating alias** — Anthropic's undated aliases point at the current snapshot for that tier and are repointed by Anthropic over time without a code/config change on our side | Lower day-to-day risk (Anthropic manages currency), but means "what we're running" can silently shift version without a corresponding commit here — verify via Anthropic's own alias documentation if exact snapshot matters |
| `claude-haiku-4-5-20251001` | Dated suffix (`20251001`) | **Pinned snapshot** | Higher exposure — pinned snapshots are the ones Anthropic actually retires on a deprecation schedule (as happened to `claude-sonnet-4-20250514` in the §5.28 incident). This is the one string in the inventory to check against Anthropic's retirement calendar first |
| `gemini-2.0-flash` | No dated/numbered suffix (e.g. no `-001`) | **Likely floating alias** | Same reasoning as Sonnet; Google's undated Flash tier names typically point at a current build |
| `scribe_v1` | Versioned name, no date | **Named version, not a live alias** — ElevenLabs versions transcription models by name (`scribe_v1`) rather than snapshot date; typically stable until ElevenLabs ships a new named version | Low near-term exposure; watch for ElevenLabs announcing a `scribe_v2`-style successor |
| `deepseek-chat` | No date | Floating alias | Irrelevant — dead code path, DeepSeek is `[Obsolete]` |

This classification is inferred from each provider's public naming conventions as documented in their model catalogs, not independently re-verified against each provider's live API or status page in this recon (out of scope — read-only code recon). **Action item for whoever owns this:** confirm `claude-haiku-4-5-20251001` (the one pinned/dated string) against Anthropic's current deprecation schedule first, since it's the only identifier in the inventory that carries a hard retirement date by construction.

---

## Part B — Monitoring / update functionality

### B.1 — Does any model-version monitoring/alerting/update functionality exist?

**No.** Confirmed by:
- No settings screen, admin control, or scheduled Hangfire job anywhere in the codebase inspects, surfaces, or updates which model version is configured.
- No code references a provider's deprecation/retirement API, status page, or changelog.
- The only "control" that exists is the fail-fast startup validator (`AIProviderOptionsValidator`, A.1) — it prevents a *missing* value from silently defaulting, but does nothing to warn about a value that is valid-but-approaching-retirement.
- `BACKLOG.md` §33 ("LLM model retirement monitoring", `BACKLOG.md:2601-2626`) explicitly confirms this gap: opened 2026-07-17, Priority P3, **Status: Open**. Its own text: *"we still discover retirements only when config validation fails at startup or an API call errors"* and proposes three unimplemented options (subscribe to provider deprecation notices, periodic automated doc checks, or manual quarterly review) — none have been built.

### B.2 — Does the AI usage logger record model version per call?

**Yes — this is the closest thing to a monitoring foundation that exists.**

- `IAiUsageLogger.LogAsync(...)` takes a `modelId` parameter (`src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/AiUsageLogger.cs:16`) and persists it to the `AiUsageLog.ModelId` column (`src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Entities/AiUsageLog.cs:21`, `string`, per-call).
- Per CLAUDE.md's "AI Usage Logging" section, every successful Claude API response is logged via `AnthropicResponseParser.Parse()` + `IAiUsageLogger.LogAsync()`, tagged with an `AiOperationCategory` (ContentParsing, SectionGeneration, QuizGeneration, SlideshowGeneration, ContentTranslation, BackTranslation, RegulatoryScoring, RequirementIngestion, RequirementMapping, LessonGeneration, DialectDetection).
- Raw rows (`AiUsageLog`) retain for 3 months, then `AggregateAiUsageJob` rolls them into daily `AiUsageSummary` rows — **but `AiUsageSummary` does not carry `ModelId`** (confirmed: no `ModelId` property on `AiUsageSummary.cs`), so per-model granularity is lost once raw rows age out past 3 months and get aggregated/deleted.
- **What this gives you today:** a queryable, per-call audit trail of exactly which model string served every AI operation, for the trailing ~3 months, per tenant. **What it does not give you:** any alerting, any check against a provider's retirement calendar, or any surfacing in an admin UI. It is raw data, not monitoring — building monitoring on top of it (e.g., a scheduled query flagging any `ModelId` not matching current `AIProviderOptions` config, or cross-referencing against a maintained list of known-deprecated strings) is exactly the kind of implementation `BACKLOG.md` §33 leaves open.

### B.3 — In-code comments/TODOs referencing model deprecation

- `BACKLOG.md:1290-1317` — full incident writeup, "§5.28 P0 — Anthropic model deprecation incident (claude-sonnet-4-20250514 retired)", including a still-open follow-up item at `BACKLOG.md:1315`: *"CostEstimationService rate table: Rates for `claude-sonnet-4-5` were inherited from the deprecated `claude-sonnet-4-0` rate table (April 2026 EUR). Verify against current Anthropic pricing and update if needed."* — not a deprecation risk itself, but a pricing-accuracy debt sitting in the same file as the model constants (A.2, `CostEstimationService.cs`).
- `BACKLOG.md:2601-2626` — §33, the open monitoring backlog item described in B.1.
- `AIProviderOptions.cs:6` doc comment — *"Changing a model identifier requires only an env var update — no code deploy"* — documents the centralisation's intent, not a deprecation warning per se, but directly relevant to how fast a retirement can be remediated once known.
- No `TODO`/`FIXME` comments referencing a specific future retirement deadline were found anywhere in `src/`.

---

## History — why this is centralised now

For context on how confident to be in the "fully centralised" verdict: on 2026-06-15, Anthropic retired `claude-sonnet-4-20250514` (aka `claude-sonnet-4-0`) and broke six production code paths simultaneously (help chat, subtitle translation, regulatory ingestion, regulatory mapping, regulatory scoring, TransVal Round 3) because model strings were hardcoded/scattered across multiple settings classes (`BACKLOG.md:1290-1298`). The fix (`docs/phase-5/reports/multi-provider-config-fix.md`) unified everything behind `AIProviderOptions`; a follow-up project ("Option B", `docs/option-b-multi-provider-recon.md`, closed per `docs/anthropic-model-default-verification-recon.md`, 2026-07-13) finished migrating every remaining call site and deleted the old scattered `Model` properties from `ClaudeSettings` and `TranslationValidationSettings` entirely — not merely stopped reading them, but removed them so there is no dead default to accidentally revive. CLAUDE.md Note 32 ("Config layer migration rule") was written directly from this incident as a standing rule for any future model-string migration. This recon's own grep sweep (A.1) independently re-confirms zero remaining scattered literals as of 2026-08-06.

---

## Summary for decision-making

1. **Are we on anything deprecated right now?** No known-deprecated string is configured. `claude-haiku-4-5-20251001` is the one *dated/pinned* identifier in use and is therefore the one to check first against Anthropic's current retirement schedule (this recon did not call out to Anthropic to verify — that's a live-API check, out of scope for a code recon).
2. **How hard would it be to update a model if needed?** Trivial — one env var or one `appsettings.json` line per provider/tier, no code changes, fail-fast validation catches a bad edit at startup rather than at first use (A.1, A.3).
3. **Do we have monitoring so we find out before it breaks?** No (B.1). It's an open, unimplemented, P3 backlog item (`BACKLOG.md` §33). The AI usage log gives you forensic per-call model history for ~3 months (B.2) but nothing proactive.
4. **Any other open item worth bundling with a monitoring build?** `CostEstimationService`'s EUR rate table is pinned to April-2026 pricing and flagged stale (`BACKLOG.md:1315`) — not a deprecation risk, but adjacent, since it lives next to the model identifiers.
