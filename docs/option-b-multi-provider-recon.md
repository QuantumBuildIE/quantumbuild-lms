# Option B — Complete §5.28 Multi-Provider Migration: Recon

_Date: 2026-06-22 | Branch: transval | Status: Read-only recon — no code modified_

---

## 1. One-line summary

9 services across 10 call sites still read model identifiers from stale settings properties rather than the canonical `AIProviderOptions` registry; one cleanup item (orphaned `ElevenLabs.Model` in `SubtitleProcessingSettingsValidator`) was also left by Option A. All required target keys already exist in `AIProviderOptions`. No schema additions needed. Estimated implementation: 2 chunks, ~3–4 hours.

---

## 2. Reference material

| Item | Location |
|------|----------|
| §5.28 AIProviderOptions class | `src/Core/QuantumBuild.Core.Application/Configuration/AIProviderOptions.cs` |
| §5.28 Validator | `src/Core/QuantumBuild.Core.Application/Configuration/AIProviderOptionsValidator.cs` |
| Option A ElevenLabs fix (pattern) | `ElevenLabsTranscriptionService` + `ElevenLabsTranscriptionServiceTests` |
| `SubtitleProcessingSettings` | `src/Modules/ToolboxTalks/.../Infrastructure/Configuration/SubtitleProcessingSettings.cs` |
| `SubtitleProcessingSettingsValidator` | `src/Modules/ToolboxTalks/.../Infrastructure/Configuration/SubtitleProcessingSettingsValidator.cs` |
| `TranslationValidationSettings` | `src/Modules/ToolboxTalks/.../Infrastructure/Configuration/TranslationValidationSettings.cs` |
| `ClaudeSettings` (shared Core class) | `src/Core/QuantumBuild.Core.Application/Abstractions/AI/ClaudeSettings.cs` |
| appsettings.json | `src/QuantumBuild.API/appsettings.json` |
| appsettings.Development.json | `src/QuantumBuild.API/appsettings.Development.json` |

**The incident shape §5.29 predicted:** §5.28 changed C# defaults to `string.Empty` without completing the read-side migration. The ElevenLabs P0 (2026-06-22, `unsupported_model`) proved that even though appsettings.json still had the correct values in the old keys, the gap created by a partial migration produced a silent failure at runtime. Option A fixed only the transcription site; this recon covers the rest.

---

## 3. Migration sites inventory

### Already migrated — do not touch

| Service | File | What it reads |
|---------|------|---------------|
| `ElevenLabsTranscriptionService` | `Services/Subtitles/ElevenLabsTranscriptionService.cs:95,103` | `_aiProviders.ElevenLabs.Models.Transcription` ✓ |
| `DialectDetectionService` | `Services/Validation/DialectDetectionService.cs:36` | `aiProviders.Anthropic.Models.Haiku` ✓ |
| `RegulatoryScoreService` | `Services/Validation/RegulatoryScoreService.cs:52` | `aiProviders.Anthropic.Models.Sonnet` ✓ |
| `PreFlightScanService` | `Services/Validation/PreFlightScanService.cs:21` | `aiProviders.Anthropic.Models.Haiku` ✓ |

### Obsolete — skip

| Service | File | Reason |
|---------|------|--------|
| `DeepSeekTranslationService` | `Services/Validation/DeepSeekTranslationService.cs:62` | `[Obsolete]` — GDPR removal (Note 2 in CLAUDE.md). Reads `_settings.DeepSeek.Model` but the service is never called. Do NOT migrate. |

---

### Group A — `SubtitleProcessingSettings.Claude.Model` → `AIProviders:Anthropic:Models:Sonnet`

6 services use `_settings.Claude.Model` from `SubtitleProcessingSettings`. All target the same property after migration: `_aiProviders.Anthropic.Models.Sonnet`.

> **Note on `ClaudeSettings`:** `ClaudeSettings` is a shared Core class (`Core.Application.Abstractions.AI.ClaudeSettings`) whose `SectionName = "SubtitleProcessing:Claude"`. It has 4 properties: `ApiKey`, `Model`, `MaxTokens`, `BaseUrl`. Only `Model` is being migrated. The other three remain in use post-migration for the actual HTTP calls (auth header, token limit, base URL). `IOptions<SubtitleProcessingSettings>` stays as a dependency in all Group A services; the constructor gains an additional `IOptions<AIProviderOptions>`.

| # | Service | File | Model read lines | Constructor line | Other SubtitleProcessingSettings fields used |
|---|---------|------|------------------|------------------|----------------------------------------------|
| 1 | `ClaudeTranslationService` | `Services/Subtitles/ClaudeTranslationService.cs` | :56 | :24–33 | `Claude.ApiKey` (:65), `Claude.MaxTokens` (:57), `Claude.BaseUrl` (:64) |
| 2 | `AiQuizGenerationService` | `Services/AiQuizGenerationService.cs` | :83, :222 | inject site | `Claude.ApiKey`, `Claude.MaxTokens`, `Claude.BaseUrl` |
| 3 | `AiSectionGenerationService` | `Services/AiSectionGenerationService.cs` | :81 | inject site | `Claude.ApiKey`, `Claude.MaxTokens`, `Claude.BaseUrl` |
| 4 | `AiSlideshowGenerationService` | `Services/Slideshow/AiSlideshowGenerationService.cs` | :70, :179, :301 | inject site | `Claude.ApiKey`, `Claude.MaxTokens`, `Claude.BaseUrl` |
| 5 | `ContentTranslationService` | `Services/Translations/ContentTranslationService.cs` | :249, :267 | inject site | `Claude.ApiKey`, `Claude.MaxTokens`, `Claude.BaseUrl` |
| 6 | `ContentParserService` | `Services/ContentCreation/ContentParserService.cs` | :84 | inject site | `Claude.ApiKey`, `Claude.MaxTokens`, `Claude.BaseUrl` |

**Note:** Services 2–6 are content generation and translation services, not subtitle services. They happen to use `SubtitleProcessingSettings.Claude` because that settings class holds the general-purpose Anthropic configuration. The naming is misleading but the fix is the same: add `IOptions<AIProviderOptions>`, read model from `_aiProviders.Anthropic.Models.Sonnet`.

**Migration pattern** (identical to Option A's ElevenLabs pattern):

```csharp
// BEFORE
private readonly SubtitleProcessingSettings _settings;

public SomeService(IOptions<SubtitleProcessingSettings> settings, ...)
{
    _settings = settings.Value;
}

// in method:
model = _settings.Claude.Model,

// AFTER
private readonly SubtitleProcessingSettings _settings;
private readonly string _claudeModel;

public SomeService(
    IOptions<SubtitleProcessingSettings> settings,
    IOptions<AIProviderOptions> aiProviders,  // ADDED
    ...)
{
    _settings = settings.Value;
    _claudeModel = aiProviders.Value.Anthropic.Models.Sonnet;  // ADDED
}

// in method:
model = _claudeModel,
```

---

### Group B — `TranslationValidationSettings` model reads → respective `AIProviders` keys

4 services read model identifiers from `TranslationValidationSettings`. Three distinct target properties are involved.

| # | Service | File | Property read | Line | Target after migration |
|---|---------|------|---------------|------|------------------------|
| 7 | `ClaudeHaikuBackTranslationService` | `Services/Validation/ClaudeHaikuBackTranslationService.cs` | `_tvSettings.Round1AModel` | :78 | `_aiProviders.Anthropic.Models.Haiku` |
| 8 | `ClaudeSonnetBackTranslationService` | `Services/Validation/ClaudeSonnetBackTranslationService.cs` | `_tvSettings.Round3DModel` | :79 | `_aiProviders.Anthropic.Models.Sonnet` |
| 9 | `GeminiTranslationService` | `Services/Validation/GeminiTranslationService.cs` | `_settings.Gemini.Model` | :77 | `_aiProviders.Gemini.Models.Flash` |
| 10 | `PipelineVersionService` | `Services/PipelineVersionService.cs` | `_settings.Round1AModel`, `_settings.Gemini.Model`, `_settings.Round3DModel` | :119–122 | All three → respective AIProviders keys |

**ClaudeHaikuBackTranslationService constructor detail (lines 26–37):**
This service injects **both** `IOptions<SubtitleProcessingSettings>` (for `Claude.ApiKey` and `Claude.BaseUrl`, used at line 86) **and** `IOptions<TranslationValidationSettings>` (for `Round1AModel`, used at line 78). After migration: add `IOptions<AIProviderOptions>`, retain both existing `IOptions<>` (still needed for ApiKey/BaseUrl and other threshold settings respectively), change line 78 to read `_aiProviders.Anthropic.Models.Haiku`.

**ClaudeSonnetBackTranslationService** — same pattern as ClaudeHaikuBackTranslationService (both providers). Read `_tvSettings.Round3DModel` at line 79 → `_aiProviders.Anthropic.Models.Sonnet`.

**PipelineVersionService** (`BuildComponentsJson()` at lines 115–134): Builds an audit snapshot JSON that records which model identifiers are active. The hash derived from this JSON is the basis for pipeline version change detection. After migration the values are identical (same model strings, different source), so no hash drift occurs — no DB migration needed. Constructor currently injects only `IOptions<TranslationValidationSettings>`; add `IOptions<AIProviderOptions>`.

---

## 4. Test file inventory

### Unit tests for migrated services

| Service | Unit test file | Current constructor setup | Change needed |
|---------|----------------|--------------------------|---------------|
| `ClaudeTranslationService` | `tests/.../Subtitles/ClaudeTranslationServiceTests.cs` | Lines 32–42: `IOptions<SubtitleProcessingSettings>` with `Claude.Model = "claude-sonnet-4-20250514"` — no `AIProviderOptions` | Add `_aiProviders` field (`IOptions<AIProviderOptions>`), populate in constructor with `Anthropic.Models.Sonnet = "claude-sonnet-4-5"`, update `CreateService()` call, **do not remove `Claude.Model` from settings setup** (other fields from ClaudeSettings still needed) |
| `ConsensusEngineTests` | `tests/.../Validation/ConsensusEngineTests.cs` | All back-translation services mocked as interfaces (lines 18–21). `ConsensusEngine` constructor receives `IOptions<TranslationValidationSettings>` for `MaxRounds` only (line 26). | **No changes needed.** The engine delegates to mocked services; it doesn't hold a model string itself. |
| `AiQuizGenerationService` | _None found_ | N/A | No unit test to update |
| `AiSectionGenerationService` | _None found_ | N/A | No unit test to update |
| `AiSlideshowGenerationService` | _None found_ | N/A | No unit test to update |
| `ContentTranslationService` | _None found_ | N/A | No unit test to update |
| `ContentParserService` | _None found_ | N/A | No unit test to update |
| `ClaudeHaikuBackTranslationService` | _None found_ | N/A | No unit test to update |
| `ClaudeSonnetBackTranslationService` | _None found_ | N/A | No unit test to update |
| `GeminiTranslationService` | _None found_ | N/A | No unit test to update |
| `PipelineVersionService` | _None found_ | N/A | No unit test to update |

**Conclusion:** Only `ClaudeTranslationServiceTests.cs` needs changes. All others either don't have unit tests or test at a higher abstraction level (interface mocks, integration tests via `CustomWebApplicationFactory`).

### ElevenLabs Option A test pattern (reference)

The pattern established in `ElevenLabsTranscriptionServiceTests.cs`:

```csharp
private readonly IOptions<AIProviderOptions> _aiProviders;

// In constructor:
_aiProviders = Options.Create(new AIProviderOptions
{
    ElevenLabs = new ElevenLabsProviderOptions
    {
        Models = new ElevenLabsModels { Transcription = "scribe_v1" }
    }
});

// In CreateService():
return new ElevenLabsTranscriptionService(httpClient, _settings, _aiProviders, logger);
```

For `ClaudeTranslationServiceTests`, apply the same pattern with `Anthropic.Models.Sonnet = "claude-sonnet-4-5"`.

---

## 5. Validator coverage

### `AIProviderOptionsValidator` — current state (complete)

```
AIProviders:Anthropic:Models:Sonnet   → validates non-empty  ✓
AIProviders:Anthropic:Models:Haiku    → validates non-empty  ✓
AIProviders:Gemini:Models:Flash       → validates non-empty  ✓
AIProviders:ElevenLabs:Models:Transcription → validates non-empty  ✓
```

All four properties validated by `AIProviderOptionsValidator` are also the four target properties for Option B. **No additions to `AIProviderOptionsValidator` are needed.** The validator already covers every property that migrated services will read.

### `SubtitleProcessingSettingsValidator` — cleanup required

Current checks:
- `ElevenLabs.ApiKey` — must be non-empty ✓ (keep: ApiKey is still a real requirement)
- `ElevenLabs.Model` — must be non-empty ✗ (remove: property being deleted post-migration; validator would either be redundant or gate a dead property)

After Option B completes, `ElevenLabs.Model` on `SubtitleProcessingSettings` is deleted. The validator's Model check must be removed at the same time. `ApiKey` check stays.

### `TranslationValidationSettings` — no validator exists

`TranslationValidationSettings` is registered with `services.Configure<>()` (no `ValidateOnStart()`). The three model properties (`Round1AModel`, `Round3DModel`, `Gemini.Model`) have always been silent-empty-default failures. After Option B, these properties are deleted from the class entirely, so no validator is needed. The migrated consumers read from `AIProviderOptions`, which already has fail-fast validation.

**No new validator needed for `TranslationValidationSettings`.**

---

## 6. Settings class cleanup

### `ClaudeSettings` (`Core.Application.Abstractions.AI.ClaudeSettings`)

| Property | Status after migration | Reason |
|----------|----------------------|--------|
| `ApiKey` | **Keep** | All Group A services use it for the Anthropic auth header |
| `Model` | **Delete** | Migrated to `AIProviders:Anthropic:Models:Sonnet`; no remaining consumers after Chunk 1 |
| `MaxTokens` | **Keep** | Used by all Group A services |
| `BaseUrl` | **Keep** | Used by all Group A services |
| `SectionName` | **Keep** | Constant used to bind configuration; the `SubtitleProcessing:Claude` section still exists (ApiKey, MaxTokens, BaseUrl remain) |

### `SubtitleProcessingSettings.ElevenLabsSettings` (`ElevenLabsSettings`)

| Property | Status after migration | Reason |
|----------|----------------------|--------|
| `ApiKey` | **Keep** | Required for ElevenLabs API auth |
| `Model` | **Delete** | Migrated to `AIProviders:ElevenLabs:Models:Transcription` in Option A; no consumers remain. Validator check must also be removed at the same time. |
| `BaseUrl` | **Keep** | Required for API calls |

### `TranslationValidationSettings`

| Property | Status after migration | Reason |
|----------|----------------------|--------|
| `Round1AModel` | **Delete** | Migrated to `AIProviders:Anthropic:Models:Haiku`; 2 consumers (`ClaudeHaikuBackTranslationService`, `PipelineVersionService`) both migrated in Chunk 2 |
| `Round3DModel` | **Delete** | Migrated to `AIProviders:Anthropic:Models:Sonnet`; 2 consumers both migrated in Chunk 2 |
| `Gemini.Model` | **Delete** | Migrated to `AIProviders:Gemini:Models:Flash`; 2 consumers both migrated in Chunk 2 |
| `DeepSeek.Model` | **Keep for now** | `DeepSeekTranslationService` is `[Obsolete]` and not called, but removing the whole `DeepSeek` sub-settings block is a separate cleanup that may require its own safety analysis. Out of scope for Option B. |
| All other properties | **Keep** | Threshold values, prompt version, pipeline version, session expiry — none of these are model identifiers and are actively used |

### `GeminiSettings` (nested in `TranslationValidationSettings`)

After `Model` is removed, `GeminiSettings` contains only `ApiKey` and `BaseUrl`. The class itself stays; it's still needed to bind the API key and base URL.

---

## 7. Config file cleanup

### `appsettings.json` — keys to remove post-migration

| Key path | Current value | Remove in | Note |
|----------|---------------|-----------|------|
| `SubtitleProcessing:ElevenLabs:Model` | `"scribe_v1"` | Chunk 1 | Orphaned by Option A; `ElevenLabsTranscriptionService` already reads from `AIProviders:ElevenLabs:Models:Transcription` |
| `SubtitleProcessing:Claude:Model` | `"claude-sonnet-4-5"` | Chunk 1 | Migrated in Chunk 1; `ClaudeSettings.Model` deleted |
| `TranslationValidation:Round1AModel` | `"claude-haiku-4-5-20251001"` | Chunk 2 | Migrated in Chunk 2 |
| `TranslationValidation:Round3DModel` | `"claude-sonnet-4-5"` | Chunk 2 | Migrated in Chunk 2 |
| `TranslationValidation:Gemini:Model` | `"gemini-2.0-flash"` | Chunk 2 | Migrated in Chunk 2 |

### `appsettings.json` — keys to keep

| Key path | Reason |
|----------|--------|
| `SubtitleProcessing:ElevenLabs:ApiKey` | Still needed for ElevenLabs auth |
| `SubtitleProcessing:ElevenLabs:BaseUrl` | Still needed |
| `SubtitleProcessing:Claude:ApiKey` | Still needed for Anthropic auth |
| `SubtitleProcessing:Claude:MaxTokens` | Still needed |
| `SubtitleProcessing:Claude:BaseUrl` | (absent in current appsettings.json — uses C# default `https://api.anthropic.com/v1`) |
| `TranslationValidation:Gemini:ApiKey` | Still needed for Gemini auth |
| `TranslationValidation:Gemini:BaseUrl` | Still needed |
| All `AIProviders:*` keys | These are the migration target — keep and protect |
| All `TranslationValidation:DefaultThreshold`, `SafetyCriticalBump`, etc. | Business logic config, not model identifiers |

### `appsettings.Development.json` — state

`appsettings.Development.json` does **not** contain a `TranslationValidation` section. ASP.NET Core config layering means Development inherits `TranslationValidation` values from `appsettings.json`. This is fine pre- and post-migration: the `AIProviders` section is already present and correct in both files. The Development file only needs updating for the `SubtitleProcessing:ElevenLabs:Model` and `SubtitleProcessing:Claude:Model` keys — both are present in `appsettings.Development.json` (lines 56, 60) and should be removed after their respective migrations.

### Railway env var implications (operational, not engineering)

If any Railway service has env vars set for the stale keys (e.g., `SubtitleProcessing__Claude__Model`, `TranslationValidation__Round1AModel`), those vars become inert after migration — they no longer bind to any property. They will not cause errors (ASP.NET Core silently ignores unbound env vars). Cleanup is desirable for hygiene but not required for the deploy to succeed. Post-migration operational task:

- `SubtitleProcessing__ElevenLabs__Model` (if set)
- `SubtitleProcessing__Claude__Model` (if set)
- `TranslationValidation__Round1AModel` (if set)
- `TranslationValidation__Round3DModel` (if set)
- `TranslationValidation__Gemini__Model` (if set)

---

## 8. Migration sequence

### Recommended: 2 chunks

#### Chunk 1 — Group A: all `SubtitleProcessingSettings.Claude.Model` consumers + ElevenLabs cleanup

**Scope:**
1. Remove `Model` property from `ClaudeSettings` (`Core.Application.Abstractions.AI.ClaudeSettings.cs`)
2. Remove `Model` property from `ElevenLabsSettings` (in `SubtitleProcessingSettings.cs`) — this is the Option A orphan
3. Update `SubtitleProcessingSettingsValidator` — remove the `ElevenLabs.Model` check
4. Add `IOptions<AIProviderOptions>` to all 6 Group A service constructors:
   - `ClaudeTranslationService`
   - `AiQuizGenerationService`
   - `AiSectionGenerationService`
   - `AiSlideshowGenerationService`
   - `ContentTranslationService`
   - `ContentParserService`
5. In each service: replace `_settings.Claude.Model` reads with `_claudeModel` (stored from `aiProviders.Value.Anthropic.Models.Sonnet`)
6. Update `ClaudeTranslationServiceTests.cs` — add `_aiProviders` field and update `CreateService()` call
7. Remove `SubtitleProcessing:ElevenLabs:Model` from `appsettings.json` and `appsettings.Development.json`
8. Remove `SubtitleProcessing:Claude:Model` from `appsettings.json` and `appsettings.Development.json`

**Estimated effort:** 2–2.5 hours (6 service edits, 1 test edit, 2 config edits, 2 settings class edits, 1 validator edit)

**Why Group A first:** Simpler — all 6 sites map to a single target property (`Anthropic.Models.Sonnet`). The `ClaudeSettings.Model` removal in Core is cleaner to do first because Group B services that use `SubtitleProcessingSettings.Claude.ApiKey`/`BaseUrl` are untouched.

---

#### Chunk 2 — Group B: `TranslationValidationSettings` model properties

**Scope:**
1. Remove `Round1AModel` and `Round3DModel` properties from `TranslationValidationSettings`
2. Remove `Model` property from `GeminiSettings` (in `TranslationValidationSettings.cs`)
3. Add `IOptions<AIProviderOptions>` to 4 Group B service constructors:
   - `ClaudeHaikuBackTranslationService` (already has `IOptions<SubtitleProcessingSettings>` + `IOptions<TranslationValidationSettings>` — add a third `IOptions<AIProviderOptions>`)
   - `ClaudeSonnetBackTranslationService` (same pattern as above)
   - `GeminiTranslationService`
   - `PipelineVersionService`
4. In each service: replace old property reads with AIProviders equivalents:
   - `_tvSettings.Round1AModel` → `_aiProviders.Anthropic.Models.Haiku`
   - `_tvSettings.Round3DModel` → `_aiProviders.Anthropic.Models.Sonnet`
   - `_settings.Gemini.Model` → `_aiProviders.Gemini.Models.Flash`
5. Remove `TranslationValidation:Round1AModel`, `Round3DModel`, `Gemini:Model` from `appsettings.json`
6. (No test file changes needed — no unit tests exist for these services)

**Special note for `PipelineVersionService.BuildComponentsJson()`:** The `round1_a`, `round2_c`, `round3_d` fields in the JSON snapshot (lines 119–122) must still emit the same model identifier strings. After migration they'll come from `_aiProviders.Anthropic.Models.Haiku`, `_aiProviders.Gemini.Models.Flash`, `_aiProviders.Anthropic.Models.Sonnet` respectively. Values identical → hash unchanged → no spurious pipeline version change record created.

**Estimated effort:** 1.5–2 hours (4 service edits, 1 settings class edit, 1 config edit)

---

### Why not one chunk?

Group A involves 6 service edits across different sub-directories plus a Core-layer settings class change. Group B involves 4 service edits in `Services/Validation/`. Keeping them separate:
- Makes the PR diff reviewable (Group A is content/subtitle layer; Group B is validation layer)
- Allows a deploy smoke-test of Chunk 1 before touching the validation pipeline
- Failure scope is narrower if something goes wrong in CI

If time pressure demands it, both chunks can be combined — there are no interdependencies.

---

## 9. Risks and edge cases

### R1 — `ClaudeHaikuBackTranslationService` has three `IOptions<>` injections post-migration

After adding `IOptions<AIProviderOptions>`, the constructor will have: `IOptions<SubtitleProcessingSettings>`, `IOptions<TranslationValidationSettings>`, `IOptions<AIProviderOptions>`. Three settings-type injections is unusual. Consider: can `IOptions<TranslationValidationSettings>` be removed entirely (since `Round1AModel` is its only reason for being there)? Check the full file before committing — if no other `_tvSettings.*` property is read, the injection can be dropped in Chunk 2. Same question applies to `ClaudeSonnetBackTranslationService`.

### R2 — Hangfire jobs

All affected services are resolved from the DI container by Hangfire (jobs use `IServiceScopeFactory`, services hold `IOptions<>` — standard pattern). The DI graph in Hangfire execution context is identical to the HTTP request context. `AIProviderOptions` is already registered and validated at startup via `ValidateOnStart()`. No special Hangfire handling needed.

Relevant jobs that call Group A or B services:
- `ContentGenerationJob` → `AiSectionGenerationService`, `AiSlideshowGenerationService`, `ContentParserService`
- `TranslationValidationJob` → `ClaudeHaikuBackTranslationService`, `ClaudeSonnetBackTranslationService`, `GeminiTranslationService`
- `ContentCreationParseJob` → `ContentParserService`

None of these jobs construct services directly — all resolved via DI. Migration is transparent to the job layer.

### R3 — `PipelineVersionService` hash stability

`BuildComponentsJson()` serialises model identifier strings into the pipeline version hash. If model strings change between the current appsettings values and what `AIProviderOptions` contains, the hash changes and a new `PipelineVersion` row is written. Verify before Chunk 2 that the values in `appsettings.json` under both keys are identical:

| Old key | Value | New key | Value |
|---------|-------|---------|-------|
| `TranslationValidation:Round1AModel` | `claude-haiku-4-5-20251001` | `AIProviders:Anthropic:Models:Haiku` | `claude-haiku-4-5-20251001` |
| `TranslationValidation:Round3DModel` | `claude-sonnet-4-5` | `AIProviders:Anthropic:Models:Sonnet` | `claude-sonnet-4-5` |
| `TranslationValidation:Gemini:Model` | `gemini-2.0-flash` | `AIProviders:Gemini:Models:Flash` | `gemini-2.0-flash` |

Current state (verified from `appsettings.json`): **all three pairs match**. Hash stable. No spurious pipeline version record will be created.

On Railway (Production), confirm that `AIProviders__Anthropic__Models__Haiku` and `AIProviders__Anthropic__Models__Sonnet` env vars are set to the same values as the old `TranslationValidation__*` vars before deploying Chunk 2. The §5.28 deployment set these env vars; they should match.

### R4 — Integration test startup

`CustomWebApplicationFactory` does not set `AIProviders__*` config. The integration test process loads `appsettings.json` which already contains the full `AIProviders` section. `AIProviderOptionsValidator.ValidateOnStart()` will fire during test startup — if the validator passes in production it will pass in tests. No changes to `CustomWebApplicationFactory` needed.

However: if any integration test manipulates `SubtitleProcessing:Claude:Model` or `TranslationValidation:Round1AModel` directly (e.g., via `WebApplicationFactory.WithWebHostBuilder` overrides), those overrides become dead code after migration. Audit integration test files for these config keys before committing. Quick search:

```
grep -r "Claude:Model\|Round1AModel\|Round3DModel\|Gemini:Model" tests/
```

If hits are found, they need removal alongside the migration.

### R5 — `CostEstimationService` rate table (flag only, out of scope)

§5.29 follow-up item 2 noted that cost rates for `claude-sonnet-4-5` were inherited from `claude-sonnet-4-0`. The migration surfaces the model identifiers more visibly but does not fix the rate table. Flag for a separate pricing review. Do not touch in Option B.

### R6 — Option A appsettings cleanup was not done

`appsettings.json:83` still contains `SubtitleProcessing:ElevenLabs:Model = "scribe_v1"`. `appsettings.Development.json:56` also contains it. `SubtitleProcessingSettingsValidator` still validates it. These are Option A orphans absorbed into Chunk 1. The validator check at line 18 of `SubtitleProcessingSettingsValidator.cs` must be removed, and the `ElevenLabsSettings.Model` C# property must be deleted, in the same commit that removes the appsettings keys — otherwise the validator will fail startup with "SubtitleProcessing:ElevenLabs:Model must be set" the moment the appsettings key is removed.

**Order within Chunk 1:** delete property → update validator → remove appsettings key. All three must land in the same commit.

### R7 — Content services using `SubtitleProcessingSettings` for non-subtitle purposes

`AiQuizGenerationService`, `AiSectionGenerationService`, `AiSlideshowGenerationService`, `ContentTranslationService`, `ContentParserService` are content generation services that happen to use `SubtitleProcessingSettings.Claude` as their Anthropic config. After Chunk 1, these services still inject `IOptions<SubtitleProcessingSettings>` for `Claude.ApiKey`, `Claude.MaxTokens`, `Claude.BaseUrl`. The section name mismatch (`SubtitleProcessing:Claude` for general-purpose Anthropic config) is a pre-existing design smell, not created by Option B. Do not refactor the config structure within the Option B scope.

---

## 10. Out of scope

- Modifying any application files (recon only)
- API key rotation or audit (`appsettings.json` contains plaintext keys — separate security concern, §5.29 follow-up item 1)
- `CostEstimationService` rate table audit (§5.29 follow-up item 2)
- Railway env var cleanup (operational task, no code change required)
- `DeepSeekSettings` block removal from `TranslationValidationSettings` (separate cleanup; the `[Obsolete]` attribute documents the intent)
- Renaming `SubtitleProcessing:Claude` section to something more generic (design smell, separate refactor)
- Anything in §3.17 (course assignment scoping) — separate work stream
- `appsettings.json` plaintext credential cleanup — noted but not addressed here
- Adding `TranslationValidation` section to `appsettings.Development.json` — not needed; config layering picks up the base file values

---

## Appendix A — `AIProviderOptions` shape (confirmed)

```
AIProviders (SectionName)
├── Anthropic: AnthropicProviderOptions
│   └── Models: AnthropicModels
│       ├── Sonnet: string  (appsettings: "claude-sonnet-4-5")
│       └── Haiku: string   (appsettings: "claude-haiku-4-5-20251001")
├── Gemini: GeminiProviderOptions
│   └── Models: GeminiModels
│       └── Flash: string   (appsettings: "gemini-2.0-flash")
└── ElevenLabs: ElevenLabsProviderOptions
    └── Models: ElevenLabsModels
        └── Transcription: string  (appsettings: "scribe_v1")
```

`AIProviderOptionsValidator` validates all 4 model properties as non-empty. Registered via `builder.Services.AddSingleton<IValidateOptions<AIProviderOptions>, AIProviderOptionsValidator>()` with `ValidateOnStart()`. Registration is in `Program.cs` at lines 116–119.

All target properties exist. No additions to the `AIProviderOptions` shape are required for Option B.

---

## Appendix B — CLAUDE.md note (recommendation)

The §5.28 → ElevenLabs P0 incident produced a portable lesson. Recommend adding a short note to CLAUDE.md under "Notes for Claude Code":

> **Config layer migration rule — complete or leave intact:** When migrating a model identifier from a legacy config key (e.g., `SubtitleProcessing:Claude:Model`) to the canonical `AIProviders:*` registry, the migration must be atomic end-to-end: (1) add `IOptions<AIProviderOptions>` to the service constructor, (2) change the model read to the new property, (3) remove the old C# property from the settings class, (4) remove the old key from `appsettings.json`, (5) update the validator if it checked the old property — all in the same commit. A partial migration (changing C# defaults to empty without updating the service read) leaves the system in a state worse than either old or new architecture: the old key in config is silently ignored, the new key may not be set, and the first actual API call fails at runtime rather than at startup. The §5.28 → ElevenLabs `unsupported_model` P0 (2026-06-22) is the reference incident.

This note belongs as a numbered "Note" in CLAUDE.md (next available number, currently in the 20s range). It should go in the implementation chunk as a separate commit, not in Chunk 1 or Chunk 2.
