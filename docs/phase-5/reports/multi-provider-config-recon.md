# Multi-Provider AI/ML Configuration Recon
## P0 Incident Follow-Up — Unified Configuration Structure for All External AI/ML Providers

**Date:** 2026-06-16
**Trigger:** Anthropic retired `claude-sonnet-4-20250514` on 2026-06-15. An Anthropic-specific patch was identified but the decision was made to design one unified multi-provider configuration structure rather than a siloed Anthropic-only fix, because all providers share the same deprecation exposure shape (model identifiers and API versions embedded in config or code, requiring a code deploy to update).

**Reference document:** `docs/phase-5/reports/anthropic-model-deprecation-recon.md`

---

## 1. Summary

Four distinct external AI/ML providers are actively used in the QuantumBuild LMS codebase: **Anthropic/Claude**, **Google Gemini**, **DeepL**, and **ElevenLabs**. A fifth provider, **DeepSeek**, has been removed from the active pipeline (v6.4, GDPR compliance) but retains residual code and config that must be managed. No OpenAI, Azure Cognitive Services, Bedrock, HuggingFace, Cohere, or Mistral providers were found.

Across all four active providers, **18 executable sites** carry model identifiers, API version strings, or base URLs that would require a code or config deploy when changed. Of these, 9 are hardcoded constants or inline literals (cannot be changed without a code deploy), and 9 are config-driven (can be changed via env vars).

The recommended unified configuration section is `AIProviders` nested inside the existing top-level provider blocks — this avoids restructuring the large `SubtitleProcessing` and `TranslationValidation` sections while adding a canonical single location for model identifiers across all providers. The complete patch covers approximately 12–14 files (up from the Anthropic-only 10-file estimate), primarily because the Haiku hardcoded sites that were acknowledged as "not broken yet" in the Anthropic recon must now be included in the structural fix.

---

## 2. Provider Inventory

### 2.1 Anthropic / Claude

**Status:** Active production provider. P0 broken as of 2026-06-16 for 6 call sites using `claude-sonnet-4-20250514`. `claude-haiku-4-5-20251001` is current but hardcoded in 2 services.

**Features served:**
- Subtitle SRT translation (`ClaudeTranslationService`) — config-driven via `SubtitleProcessing:Claude:Model`
- Lesson parse / bulk document generation (`LessonGeneratorService`) — same config key
- TransVal consensus engine Round 1A back-translation (`ClaudeHaikuBackTranslationService`) — config-driven via `TranslationValidation:Round1AModel`
- TransVal consensus engine Round 3D back-translation (`ClaudeSonnetBackTranslationService`) — config-driven via `TranslationValidation:Round3DModel`
- Help chat assistant (`HelpChatController`) — hardcoded
- Regulatory requirement mapping (`RequirementMappingJob`) — hardcoded const
- Regulatory requirement ingestion (`RequirementIngestionJob`) — hardcoded const
- TransVal regulatory scoring (`RegulatoryScoreService`) — hardcoded const
- Dialect detection (`DialectDetectionService`) — hardcoded const (Haiku, not broken)
- Pre-flight scan (`PreFlightScanService`) — hardcoded const (Haiku, not broken)
- Content translation (`ContentTranslationService`) — reads `ClaudeSettings.Model` via `SubtitleProcessing:Claude:Model`
- AI section generation (`AiSectionGenerationService`) — reads `ClaudeSettings.Model`
- AI quiz generation (`AiQuizGenerationService`) — reads `ClaudeSettings.Model`
- AI slideshow generation (`AiSlideshowGenerationService`) — reads `ClaudeSettings.Model`
- Content parser (`ContentParserService`) — reads `ClaudeSettings.Model`

**Criticality:** CRITICAL. Multiple production features broken. Largest call surface of any provider.

---

### 2.2 Google Gemini

**Status:** Active production provider. Gemini is Round 2C in the consensus engine — fires when Round 1 (Haiku + DeepL) produces inconclusive agreement. Degrades gracefully (skips) when API key is empty.

**Features served:**
- TransVal consensus engine Round 2C back-translation (`GeminiTranslationService`)
- Cost estimation rate table (read-only, no live call — `CostEstimationService`)
- Pipeline version snapshot (`PipelineVersionService` — captures `Gemini.Model` in `ComponentsJson`)

**Criticality:** HIGH for TransVal pipeline quality. Without Gemini, the consensus engine skips Round 2 and falls through directly to Round 3 (Sonnet) for all inconclusive cases. The pipeline degrades but does not fail.

---

### 2.3 DeepL

**Status:** Active production provider. DeepL is Round 1B in the consensus engine — always runs in every validation section alongside Haiku (Round 1A). Degrades gracefully when API key is empty.

**Features served:**
- TransVal consensus engine Round 1B back-translation (`DeepLTranslationService`)
- Cost estimation rate table (read-only, no live call — `CostEstimationService`)
- Pipeline version snapshot (captured as literal string `"deepl"` — not model-versioned)

**Criticality:** HIGH for TransVal pipeline quality. DeepL + Haiku form the first-line consensus gate. Losing DeepL means Round 1 can never reach bilateral agreement and every section escalates to Round 2+.

**Deprecation exposure shape:** Different from model providers. DeepL does not deprecate models — it deprecates API versions in the URL path. Current URL: `{BaseUrl}/translate` where `BaseUrl` defaults to `https://api-free.deepl.com/v2`. The `/v2` in the base URL is the versioned element. No active free-text model identifier.

---

### 2.4 ElevenLabs

**Status:** Active production provider. ElevenLabs provides speech-to-text transcription for video subtitle processing.

**Features served:**
- Video transcription for subtitle processing (`ElevenLabsTranscriptionService`)

**Criticality:** HIGH for subtitle workflow. No subtitle SRT generation is possible without a working ElevenLabs transcription. This is the entry point for the entire subtitle pipeline.

**Deprecation exposure shape:** ElevenLabs identifies transcription models via a `model_id` parameter in the multipart form upload. The current identifier is `scribe_v1`. This is config-driven via `SubtitleProcessing:ElevenLabs:Model` and has a C# default value in `ElevenLabsSettings.Model`. Same exposure shape as Anthropic.

---

### 2.5 DeepSeek (Obsolete)

**Status:** Removed from active pipeline in v6.4 (GDPR compliance — indefinite data retention on China-based servers). `DeepSeekTranslationService` is decorated `[Obsolete]`. `IDeepSeekTranslationService` remains in the Abstractions layer. The `DeepSeekSettings` class is `[Obsolete]` in `TranslationValidationSettings`. Config block retained in `appsettings.json` as a human-readable comment/record of the change.

**Residual references:** `DeepSeekTranslationService.cs`, `IDeepSeekTranslationService.cs`, `DeepSeekSettings` (in `TranslationValidationSettings.cs`), `appsettings.json:116-121` (comment block), `ConsensusEngineTests.cs` (test coverage of DeepSeek path).

**Criticality:** Zero — not called by any active code path. `BackTranslationSelector` does not reference it. `ConsensusEngine` references only `IClaudeSonnetBackTranslationService` as Round 3D.

---

## 3. Per-Provider Site Catalog

### 3.1 Anthropic / Claude — All Sites

*(Lifted from the Anthropic recon with additions for Haiku hardcoded sites.)*

#### Executable sites (hardcoded — require code deploy to change)

| File (abbreviated) | Line | Identifier | Classification | Feature |
|---|---|---|---|---|
| `Controllers/HelpChatController.cs` | 48 | `"claude-sonnet-4-20250514"` | Inline literal in request body | Help Chat |
| `Jobs/RequirementMappingJob.cs` | 23 | `private const string SonnetModel = "claude-sonnet-4-20250514"` | C# const | Regulatory Mapping |
| `Jobs/RequirementIngestionJob.cs` | 23 | `private const string SonnetModel = "claude-sonnet-4-20250514"` | C# const | Regulatory Ingestion |
| `Services/Validation/RegulatoryScoreService.cs` | 23 | `private const string SonnetModel = "claude-sonnet-4-20250514"` | C# const | TransVal Regulatory Scoring |
| `Services/Validation/DialectDetectionService.cs` | 18 | `private const string HaikuModel = "claude-haiku-4-5-20251001"` | C# const | Dialect Detection |
| `Services/Validation/PreFlightScanService.cs` | 18 | `private const string HaikuModel = "claude-haiku-4-5-20251001"` | C# const | Pre-flight Scan |

#### Executable sites (config-driven — require env var update only)

| File (abbreviated) | Line | Identifier | Classification | Feature |
|---|---|---|---|---|
| `appsettings.json` | 69 | `SubtitleProcessing:Claude:Model = "claude-sonnet-4-20250514"` | Config value | Subtitle Translation + Lesson Parse + Content Translation + AI generation suite |
| `appsettings.Development.json` | 29 | `SubtitleProcessing:Claude:Model = "claude-sonnet-4-20250514"` | Config value (dev) | Same |
| `appsettings.json` | 128 | `TranslationValidation:Round1AModel = "claude-haiku-4-5-20251001"` | Config value | TransVal Round 1A |
| `appsettings.json` | 129 | `TranslationValidation:Round3DModel = "claude-sonnet-4-20250514"` | Config value | TransVal Round 3D |
| `Abstractions/AI/ClaudeSettings.cs` | 23 | `public string Model { get; set; } = "claude-sonnet-4-20250514"` | C# default (fallback) | All services reading `ClaudeSettings` |
| `Configuration/TranslationValidationSettings.cs` | 67 | `public string Round1AModel { get; set; } = "claude-haiku-4-5-20251001"` | C# default (fallback) | TransVal Round 1A |
| `Configuration/TranslationValidationSettings.cs` | 74 | `public string Round3DModel { get; set; } = "claude-sonnet-4-20250514"` | C# default (fallback) | TransVal Round 3D |

#### Comment / doc / test fixture sites (not executable)

| File (abbreviated) | Line | Identifier | Classification |
|---|---|---|---|
| `Services/Validation/ConsensusEngine.cs` | 9,12 | `claude-haiku-4-5-20251001`, `claude-sonnet-4-20250514` | Comments in class header |
| `Services/Validation/ClaudeSonnetBackTranslationService.cs` | 13 | `claude-sonnet-4-20250514` | XML doc comment |
| `Services/Validation/DeepSeekTranslationService.cs` | 14 | `claude-sonnet-4-20250514` | `[Obsolete]` attribute text |
| `Services/Validation/CostEstimationService.cs` | 17,13 | `claude-sonnet-4-20250514`, `claude-haiku-4-5-20251001` | Rate table comments |
| `Abstractions/Validation/IClaudeSonnetBackTranslationService.cs` | 4 | `claude-sonnet-4-20250514` | Interface XML doc |
| `Domain/Entities/AiUsageLog.cs` | 19 | `claude-sonnet-4-20250514` | XML doc example |
| `Domain/Entities/AiUsageSummary.cs` | 19 | `claude-sonnet-4-20250514` | XML doc example |
| `Domain/Entities/ProviderResultCache.cs` | 19 | `claude-haiku-4-5-20251001` | XML doc example |
| `Domain/Entities/PipelineChangeRecord.cs` | 22 | `claude-sonnet-4-20250514` | XML doc example |
| `appsettings.json` | 127 | `Round3Provider = "claude-sonnet-4-20250514"` | Orphaned config key — no C# property reads it |
| `appsettings.json` | 117 | DeepSeek `_comment` references `claude-sonnet-4-20250514` | Config comment only |
| `Tests/ClaudeTranslationServiceTests.cs` | 37, 392 | `claude-sonnet-4-20250514` | Test fixture (mocked HTTP — not live) |

---

### 3.2 Google Gemini — All Sites

#### Executable sites

| File (abbreviated) | Line | Identifier | Classification | Feature |
|---|---|---|---|---|
| `appsettings.json` | 113 | `TranslationValidation:Gemini:Model = "gemini-2.0-flash"` | Config value | TransVal Round 2C |
| `appsettings.json` | 114 | `TranslationValidation:Gemini:BaseUrl = "https://generativelanguage.googleapis.com/v1beta"` | Config value | TransVal Round 2C |
| `Configuration/TranslationValidationSettings.cs` | 131 | `public string Model { get; set; } = "gemini-2.0-flash"` | C# default (fallback) | TransVal Round 2C |
| `Configuration/TranslationValidationSettings.cs` | 137 | `public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta"` | C# default (fallback) | TransVal Round 2C |

**URL construction (GeminiTranslationService.cs:77):**
```
$"{_settings.Gemini.BaseUrl}/models/{_settings.Gemini.Model}:generateContent?key={_settings.Gemini.ApiKey}"
```
The model name is embedded in the URL path — `gemini-2.0-flash` appears as a path segment. Both the base URL (`/v1beta`) and model name are fully config-driven. **No hardcoded sites.**

#### Comment / rate table sites (not executable)

| File (abbreviated) | Line | Identifier | Classification |
|---|---|---|---|
| `Services/Validation/CostEstimationService.cs` | 21 | `// gemini-2.0-flash` | Rate table comment |
| `Services/PipelineVersionService.cs` | 121 | `round2_c = _settings.Gemini.Model` | Config-driven audit snapshot — not hardcoded |

---

### 3.3 DeepL — All Sites

#### Executable sites

| File (abbreviated) | Line | Identifier | Classification | Feature |
|---|---|---|---|---|
| `appsettings.json` | 109 | `TranslationValidation:DeepL:BaseUrl = "https://api-free.deepl.com/v2"` | Config value | TransVal Round 1B |
| `Configuration/TranslationValidationSettings.cs` | 114 | `public string BaseUrl { get; set; } = "https://api-free.deepl.com/v2"` | C# default (fallback) | TransVal Round 1B |

**URL construction (DeepLTranslationService.cs:161):**
```
$"{_settings.DeepL.BaseUrl}/translate"
```
The `/translate` path segment is hardcoded — but this is a stable DeepL API endpoint name, not a versioned identifier. The `/v2` version appears in the `BaseUrl` config value. **No model identifier — DeepL uses no model selection parameter.** **No hardcoded sites.**

**Note on DeepL free vs paid tier:** The base URL differs between free and paid tiers: `https://api-free.deepl.com/v2` (free) vs `https://api.deepl.com/v2` (paid). Using the wrong URL returns HTTP 403. This is a config value (fully changeable via env var) but is a known operational footgun documented in the service's error handling code.

---

### 3.4 ElevenLabs — All Sites

#### Executable sites

| File (abbreviated) | Line | Identifier | Classification | Feature |
|---|---|---|---|---|
| `appsettings.json` | 65 | `SubtitleProcessing:ElevenLabs:Model = "scribe_v1"` | Config value | Video Transcription |
| `appsettings.Development.json` | 25 | `SubtitleProcessing:ElevenLabs:Model = "scribe_v1"` | Config value (dev) | Video Transcription |
| `Configuration/SubtitleProcessingSettings.cs` | 70 | `public string Model { get; set; } = "scribe_v1"` | C# default (fallback) | Video Transcription |
| `Configuration/SubtitleProcessingSettings.cs` | 76 | `public string BaseUrl { get; set; } = "https://api.elevenlabs.io/v1"` | C# default (fallback) | Video Transcription |

**Request construction (ElevenLabsTranscriptionService.cs:93–99):**
```csharp
var request = new HttpRequestMessage(HttpMethod.Post, $"{_settings.ElevenLabs.BaseUrl}/speech-to-text");
// ...
{ new StringContent(_settings.ElevenLabs.Model), "model_id" }  // model_id is a form field
```
The model identifier `scribe_v1` is passed as a multipart form field named `model_id`. Both the base URL (`/v1`) and model identifier are fully config-driven. **No hardcoded sites.**

#### Test fixture sites (not executable)

| File (abbreviated) | Line | Identifier | Classification |
|---|---|---|---|
| `Tests/ElevenLabsTranscriptionServiceTests.cs` | 33–34 | `scribe_v1`, `https://api.elevenlabs.io/v1` | Test fixture (mocked HTTP) |

---

### 3.5 DeepSeek — Residual Sites (Obsolete)

| File (abbreviated) | Line | Identifier | Classification | Status |
|---|---|---|---|---|
| `appsettings.json` | 116–121 | DeepSeek block with `ApiKey`, `BaseUrl`, `Model = "deepseek-chat"` | Config block | Retained as human-readable comment record |
| `Configuration/TranslationValidationSettings.cs` | 29–30 | `[Obsolete] public DeepSeekSettings DeepSeek` | C# `[Obsolete]` property | Not read by any active code |
| `Configuration/TranslationValidationSettings.cs` | 152–161 | `DeepSeekSettings` class with `Model = "deepseek-chat"` | C# class | Never instantiated by active pipeline |
| `Services/Validation/DeepSeekTranslationService.cs` | 14 | `[Obsolete]` class | Service class | Not registered in DI for active use |
| `Abstractions/Validation/IDeepSeekTranslationService.cs` | — | Interface | Abstraction | Not injected into any active service |
| `Tests/.../ConsensusEngineTests.cs` | multiple | DeepSeek test paths | Test coverage | Tests an obsolete path — should be reviewed |

---

## 4. Per-Provider Call Pattern Classification

### 4.1 Anthropic / Claude

**Pattern:** Model identifier as primary parameter in request body JSON.

```json
{ "model": "claude-sonnet-4-20250514", "max_tokens": 4096, "messages": [...] }
```

**Deprecation exposure:** HIGH. When Anthropic retires a model identifier, the API returns HTTP 400 `NotFound` for every request using that identifier. P0 incident confirmed. Two identifiers in active use:
- `claude-sonnet-4-20250514` — **retired, all 6 executable Sonnet sites broken**
- `claude-haiku-4-5-20251001` — current, 2 hardcoded sites + 1 config-driven site

**Config-driven vs hardcoded:**
- 3 sites are config-driven (env-var updateable without code deploy)
- 6 sites are hardcoded (require code deploy — 4 Sonnet, 2 Haiku)

---

### 4.2 Google Gemini

**Pattern:** Model identifier in URL path + API version in URL path.

```
POST https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key=...
```

**Deprecation exposure:** HIGH for model name (same pattern as Anthropic — Google has already retired `gemini-pro` and `gemini-1.0-pro`). MODERATE for `v1beta` API version (Google maintains backward compatibility longer than model lifecycle, but `v1beta` routes can be retired).

**Config-driven vs hardcoded:** Both model name and base URL (including `/v1beta`) are fully config-driven. **Zero hardcoded executable sites.** Best-positioned provider in the codebase for deprecation resilience.

---

### 4.3 DeepL

**Pattern:** API version embedded in base URL. No model identifier.

```
POST https://api-free.deepl.com/v2/translate
Authorization: DeepL-Auth-Key {key}
```

**Deprecation exposure:** MODERATE. DeepL does not version models — it versions its API path. The `/v2` suffix in the base URL is the versioned element. When DeepL retires v2, a new `BaseUrl` value is sufficient. No model identifier churn expected.

**Config-driven vs hardcoded:** Base URL fully config-driven. The `/translate` path segment appended by the service is stable (has not changed in DeepL v2 lifecycle). **Zero hardcoded executable sites.**

---

### 4.4 ElevenLabs

**Pattern:** Model identifier as multipart form field `model_id`.

```
POST https://api.elevenlabs.io/v1/speech-to-text
Content-Type: multipart/form-data
model_id=scribe_v1
```

**Deprecation exposure:** HIGH for model identifier. ElevenLabs has deprecated older models (e.g., `scribe_v1` is the current generation; ElevenLabs previously had `whisper-large-v2` and other backend models). MODERATE for `/v1` API version — ElevenLabs has versioned APIs and has introduced v2/v3 for some endpoints.

**Config-driven vs hardcoded:** Model identifier fully config-driven. BaseUrl (`/v1`) is a C# default in `ElevenLabsSettings` — config-overridable. **Zero hardcoded executable sites.**

---

### 4.5 DeepSeek (Obsolete)

**Pattern:** OpenAI-compatible chat completions (`/v1/chat/completions`). Model identifier in request body.

**Deprecation exposure:** N/A — not in active pipeline. `[Obsolete]` throughout.

---

## 5. Unified Configuration Proposal

### 5.1 Proposed JSON shape

```json
"AIProviders": {
  "Anthropic": {
    "Models": {
      "Sonnet": "claude-sonnet-4-6",
      "Haiku": "claude-haiku-4-5-20251001"
    }
  },
  "Gemini": {
    "Models": {
      "Flash": "gemini-2.0-flash"
    },
    "ApiVersion": "v1beta"
  },
  "DeepL": {
    "Tier": "free"
  },
  "ElevenLabs": {
    "Models": {
      "Transcription": "scribe_v1"
    }
  }
}
```

The `AIProviders` section is a **canonical registry of versioned identifiers**. It does not hold API keys (keys remain in their current provider-specific config sections) or operational settings like timeouts, max tokens, or retry counts (those stay in `SubtitleProcessing` and `TranslationValidation`).

---

### 5.2 Naming: `AIProviders` vs `ExternalProviders` vs `AI`

**Recommended: `AIProviders`**

| Option | Pro | Con |
|---|---|---|
| `AIProviders` | Clearly scoped to AI/ML providers. Distinguishes from non-AI external calls (MailerSend, R2, Float). | Slightly longer key. |
| `ExternalProviders` | Broad enough to include all third parties. | Too broad — MailerSend and R2 are also "external providers" but are not AI. Would create ambiguity. |
| `AI` | Shortest. | Too terse for a section containing provider-specific sub-sections. Collides if a broader `AI` feature config is later added. |

`AIProviders` cleanly communicates "these are the AI/ML provider identifiers, separate from API keys and operational settings."

---

### 5.3 Per-provider sub-shape: `Models` dict vs explicit named fields

**Recommendation: Explicit named fields within a `Models` object — not a flat dict.**

```json
"Anthropic": {
  "Models": {
    "Sonnet": "claude-sonnet-4-6",
    "Haiku": "claude-haiku-4-5-20251001"
  }
}
```

A flat dict (`"Models": { "claude-sonnet-4-6": "active" }`) forces callers to iterate. Named fields (`Models.Sonnet`, `Models.Haiku`) bind cleanly to C# `string` properties, are immediately readable, and support Railway env var override via `AIProviders__Anthropic__Models__Sonnet=claude-sonnet-4-6`.

For Gemini, `Models.Flash` follows the same pattern. For DeepL, there is no model identifier — only `Tier` (either `"free"` or `"paid"`) which resolves to a base URL at startup. For ElevenLabs, `Models.Transcription` is the single transcription model identifier.

**Why `Tier` for DeepL instead of an explicit `BaseUrl`?**
Two reasons: (1) The DeepL `BaseUrl` already exists in `TranslationValidation:DeepL:BaseUrl` and is the runtime parameter used by the service. Duplicating a URL is error-prone. (2) `Tier: "free" | "paid"` is a semantic label that resolves to the correct URL in code — easier to reason about for non-developers reviewing the config. The `DeepLSettings` class would map `Tier` to the correct `BaseUrl` at bind time. If an operator sets `Tier = "paid"` they know what they changed.

---

### 5.4 API keys: keep where they are

API keys must **not** move into `AIProviders`. Rationale:
1. Keys are runtime secrets, not versioned identifiers. They do not benefit from the unified registry pattern.
2. Keys are already spread across `SubtitleProcessing:Claude:ApiKey`, `SubtitleProcessing:ElevenLabs:ApiKey`, `TranslationValidation:DeepL:ApiKey`, `TranslationValidation:Gemini:ApiKey`, and `Anthropic:ApiKey`. Centralising them would require touching every service that reads them.
3. The model-identifier problem (retiring `claude-sonnet-4-20250514`) is orthogonal to key rotation.
4. Railway env var naming for keys is already established in production deployments.

---

### 5.5 Backward compatibility: fail-fast startup validation

When `AIProviders` is added to `appsettings.json`, any deployment that has not yet received the new config (e.g., a Railway service that was not updated before the code deploy) will bind empty strings. The hardcoded-const fallback pattern used before the structural patch provided silent fallback to deprecated models — which is the exact failure mode that caused the P0 incident.

**Recommendation: `IValidateOptions<T>` for each provider options class.**

```csharp
public class AIProviderOptions
{
    public AnthropicModelOptions Anthropic { get; set; } = new();
    public GeminiModelOptions Gemini { get; set; } = new();
    public DeepLTierOptions DeepL { get; set; } = new();
    public ElevenLabsModelOptions ElevenLabs { get; set; } = new();
}

public class AIProviderOptionsValidator : IValidateOptions<AIProviderOptions>
{
    public ValidateOptionsResult Validate(string? name, AIProviderOptions options)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(options.Anthropic.Models.Sonnet))
            errors.Add("AIProviders:Anthropic:Models:Sonnet must be set");
        if (string.IsNullOrWhiteSpace(options.Anthropic.Models.Haiku))
            errors.Add("AIProviders:Anthropic:Models:Haiku must be set");
        if (string.IsNullOrWhiteSpace(options.Gemini.Models.Flash))
            errors.Add("AIProviders:Gemini:Models:Flash must be set");
        if (options.DeepL.Tier is not ("free" or "paid"))
            errors.Add("AIProviders:DeepL:Tier must be 'free' or 'paid'");
        if (string.IsNullOrWhiteSpace(options.ElevenLabs.Models.Transcription))
            errors.Add("AIProviders:ElevenLabs:Models:Transcription must be set");
        return errors.Any()
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}
```

Registered in `Program.cs`:
```csharp
services.AddOptions<AIProviderOptions>()
    .BindConfiguration("AIProviders")
    .ValidateOnStart();
services.AddSingleton<IValidateOptions<AIProviderOptions>, AIProviderOptionsValidator>();
```

This causes the application to throw at startup (not at the first API call) with a clear message identifying the missing key. No silent fallback to deprecated models.

---

## 6. Patch Shape

This section describes **what files change and why** — no code written here.

### 6.1 New files (additions)

| File | Purpose |
|---|---|
| `src/Core/QuantumBuild.Core.Application/Configuration/AIProviderOptions.cs` | New options class: `AIProviderOptions`, `AnthropicModelOptions`, `GeminiModelOptions`, `DeepLTierOptions`, `ElevenLabsModelOptions`. All properties are simple strings with no C# defaults (let validation catch missing values). |
| `src/Core/QuantumBuild.Core.Application/Configuration/AIProviderOptionsValidator.cs` | `IValidateOptions<AIProviderOptions>` implementation. Validates all required fields are present and non-empty at startup. |

---

### 6.2 Modified files

#### Config files

| File | Change |
|---|---|
| `src/QuantumBuild.API/appsettings.json` | Add `AIProviders` top-level section. Update `SubtitleProcessing:Claude:Model` to `claude-sonnet-4-6`. Update `TranslationValidation:Round3DModel` to `claude-sonnet-4-6`. Remove orphaned `Round3Provider` key. |
| `src/QuantumBuild.API/appsettings.Development.json` | Add `AIProviders` top-level section. Update `SubtitleProcessing:Claude:Model` to `claude-sonnet-4-6`. |

#### Existing settings / options classes

| File | Change |
|---|---|
| `src/Core/QuantumBuild.Core.Application/Abstractions/AI/ClaudeSettings.cs` | Remove C# default value from `Model` property (was `"claude-sonnet-4-20250514"`). The model is now always sourced from `AIProviders:Anthropic:Models:Sonnet`. Update XML doc to reflect the new source. |
| `src/Modules/ToolboxTalks/.../Configuration/TranslationValidationSettings.cs` | Remove C# default from `Round1AModel` and `Round3DModel`. Remove C# default from `GeminiSettings.Model`. These values are now always sourced from `AIProviders`. |
| `src/Modules/ToolboxTalks/.../Configuration/SubtitleProcessingSettings.cs` | Remove C# default from `ElevenLabsSettings.Model`. |

#### Services that replace hardcoded constants with injected config

| File | Line | Change | Requires new injection? |
|---|---|---|---|
| `src/QuantumBuild.API/Controllers/HelpChatController.cs` | 48 | Replace `"claude-sonnet-4-20250514"` literal with `_aiProviders.Anthropic.Models.Sonnet` | Yes — inject `IOptions<AIProviderOptions>` |
| `src/Modules/ToolboxTalks/.../Jobs/RequirementMappingJob.cs` | 23 | Replace `private const string SonnetModel` with field populated from `IOptions<AIProviderOptions>` | Yes |
| `src/Modules/ToolboxTalks/.../Jobs/RequirementIngestionJob.cs` | 23 | Same as above | Yes |
| `src/Modules/ToolboxTalks/.../Services/Validation/RegulatoryScoreService.cs` | 23 | Same as above | Yes |
| `src/Modules/ToolboxTalks/.../Services/Validation/DialectDetectionService.cs` | 18 | Replace `private const string HaikuModel = "claude-haiku-4-5-20251001"` with field from `AIProviderOptions.Anthropic.Models.Haiku` | Yes |
| `src/Modules/ToolboxTalks/.../Services/Validation/PreFlightScanService.cs` | 18 | Same as above | Yes |

#### Program.cs / DI registration

| File | Change |
|---|---|
| `src/QuantumBuild.API/Program.cs` | Register `AIProviderOptions` binding and validator: `services.AddOptions<AIProviderOptions>().BindConfiguration("AIProviders").ValidateOnStart()`. Register `IValidateOptions<AIProviderOptions>`. |

---

### 6.3 Files NOT changed

These files reference deprecated identifiers in non-executable contexts (comments, XML docs, test fixtures). They are documentation artifacts — updating them is optional cleanup but does not affect production behavior:

- Comment lines in `ConsensusEngine.cs`, `CostEstimationService.cs`, `ClaudeSonnetBackTranslationService.cs`
- XML doc examples in domain entities
- Test fixture strings in `ClaudeTranslationServiceTests.cs` and `ElevenLabsTranscriptionServiceTests.cs`
- The DeepSeek obsolete residuals (`DeepSeekTranslationService.cs`, `IDeepSeekTranslationService.cs`, `DeepSeekSettings` class, `ConsensusEngineTests.cs` DeepSeek paths) — these are a separate cleanup concern

---

### 6.4 Railway env vars

Existing env vars to update when the new section is deployed:

```
# New vars (add)
AIProviders__Anthropic__Models__Sonnet=claude-sonnet-4-6
AIProviders__Anthropic__Models__Haiku=claude-haiku-4-5-20251001
AIProviders__Gemini__Models__Flash=gemini-2.0-flash
AIProviders__Gemini__ApiVersion=v1beta
AIProviders__DeepL__Tier=free
AIProviders__ElevenLabs__Models__Transcription=scribe_v1

# Existing vars — still needed for operational config (max tokens, batch size, etc.)
SubtitleProcessing__Claude__Model=claude-sonnet-4-6   ← update this too (immediate P0 fix)
TranslationValidation__Round3DModel=claude-sonnet-4-6 ← update this too (immediate P0 fix)
# Other existing vars remain unchanged
```

**Transition note:** The existing `SubtitleProcessing__Claude__Model` and `TranslationValidation__Round3DModel` env vars should be updated immediately (P0 patch) before the structural patch lands. After the structural patch, those services will read from `AIProviders` and the old env vars become redundant — but can be left as no-ops. Remove them in a follow-up cleanup rather than risking downtime.

---

### 6.5 Scope estimate

| Category | Count |
|---|---|
| New files to create | 2 |
| Config files to update | 2 |
| Options/settings classes to update | 3 |
| Services to update (inject `AIProviderOptions`) | 6 |
| Program.cs DI registration | 1 |
| **Total files changed** | **~14** |

Compare: Anthropic-only structural patch (identified in the Anthropic recon) was ~10 files. The multi-provider patch is ~14 files, with the additional 4 coming from the Haiku hardcoded services (DialectDetectionService, PreFlightScanService) and the two new files (options class + validator).

---

## 7. Verification Approach

All smoke tests should be run on Development before promoting to Production. Perform in the order listed — early providers (Anthropic) are more critical.

### 7.1 Anthropic / Claude — 7 smoke tests

These are carried forward from the Anthropic recon with the updated model identifier (`claude-sonnet-4-6`):

1. **Subtitle SRT translation:** Start subtitle processing on a talk with an existing video. Confirm SignalR progress events fire and the job reaches `Completed` with SRT files in R2.
2. **Lesson Parse:** Trigger `POST /api/toolbox-talks/{id}/parse`. Confirm job runs and generates talks/course. No HTTP 400 from Anthropic.
3. **TransVal Round 3D:** Run translation validation on a multi-section talk. Confirm at least one Round 3D section fires (`ClaudeSonnetBackTranslationService` logs). Confirm no `NotFound` errors.
4. **Help Chat:** Open help chat, send a message. Confirm HTTP 200 from `POST /api/help/chat` and a coherent response.
5. **Requirement Mapping:** Publish a talk. Confirm mapping job fires (Hangfire dashboard) and pending mappings appear. No Anthropic 400 in logs.
6. **Requirement Ingestion:** Trigger `POST /api/regulatory/documents/{id}/ingest`. Confirm draft requirements created.
7. **Regulatory Scoring:** `POST /api/toolbox-talks/validation-runs/{runId}/regulatory-score`. Confirm score result returned.

**Haiku-specific smoke tests:**

8. **Dialect Detection:** Trigger a content creation session with a non-English source. Confirm `DialectDetectionService` logs appear with the new model name (search Railway logs for `Haiku back-translating` or `Claude Haiku`). No errors.
9. **Pre-flight Scan:** Start a translation validation run. Confirm pre-flight scan logs fire (search for `PreFlightScanService`). No errors.

### 7.2 Gemini — 1 smoke test

10. **TransVal Round 2C:** Run translation validation. Confirm at least one section escalates to Round 2 (check logs for `GeminiTranslationService` or `Round 2 complete`). Confirm no 400/403 from Gemini API.

### 7.3 DeepL — 1 smoke test

11. **TransVal Round 1B:** Run translation validation. Confirm `DeepLTranslationService` back-translation logs appear. Confirm no 403 Forbidden (would indicate free/paid tier mismatch).

### 7.4 ElevenLabs — 1 smoke test

12. **Video Transcription:** Start subtitle processing on a talk with an existing video URL. Confirm `ElevenLabsTranscriptionService` log appears showing model `scribe_v1` was sent. Confirm transcription response received.

### 7.5 Startup validation — 1 smoke test

13. **Fail-fast check:** Temporarily remove `AIProviders:Anthropic:Models:Sonnet` from the Development environment. Restart the API. Confirm the application fails to start with a clear error message identifying the missing key (not a null reference error at call time). Restore the env var and confirm normal startup.

### 7.6 Quality regression check — TransVal (inherited from Anthropic recon)

After the `claude-sonnet-4-20250514` → `claude-sonnet-4-6` swap, re-run one stable validation run and compare per-section scores. A ±5 point shift is within noise. A ±15 point shift warrants investigation before Production promotion.

### 7.7 Log inspection window

After deploying to Development:
- Monitor Railway logs for 30 minutes covering at least one of each job type
- Grep for `NotFound`, `400`, `403`, `"error"` from any AI provider
- Confirm no `claude-sonnet-4-20250514` or deprecated model references appear in logs
- Confirm new model identifiers (`claude-sonnet-4-6`, `gemini-2.0-flash`, `scribe_v1`) appear as expected

---

## 8. Adjacent Observations

### 8.1 API keys exposed in appsettings.json (SECURITY)

Both `appsettings.json` and `appsettings.Development.json` contain live API keys committed to the repository: Anthropic, ElevenLabs, Cloudflare R2 (access key + secret), and MailerSend. These are plaintext secrets in the codebase. This was noted in the Anthropic recon and is re-flagged here. **BACKLOG: Rotate all keys committed to the repository and migrate to Railway-only env vars. Consider a pre-commit hook or secret scanning to prevent future key commits.**

### 8.2 Orphaned config key `Round3Provider`

`appsettings.json:127` contains `"Round3Provider": "claude-sonnet-4-20250514"`. No C# property in `TranslationValidationSettings` reads this key — the actual property is `Round3DModel`. This orphan should be removed in the patch. Documented in Anthropic recon; carried forward here as a reminder.

### 8.3 Gemini `v1beta` exposure — API version in URL

The Gemini base URL `https://generativelanguage.googleapis.com/v1beta` embeds an API version. Google has previously migrated features from `v1beta` to `v1` (stable). If Google stabilises the `generateContent` endpoint to `v1` and simultaneously deprecates `v1beta`, this config would need updating. The `BaseUrl` is already config-driven so no code deploy is needed, but it warrants a BACKLOG entry. **BACKLOG: Monitor Google's v1beta stability announcements. When generateContent is stable in v1, update `AIProviders:Gemini:ApiVersion` and `TranslationValidation:Gemini:BaseUrl`.**

### 8.4 DeepL free vs paid URL distinction

The inline error handling in `DeepLTranslationService.cs:172-174` already documents the 403 footgun (free key with paid URL, or vice versa). The proposed `AIProviders:DeepL:Tier` config value addresses this at a semantic level, but the actual runtime URL lives in `TranslationValidation:DeepL:BaseUrl`. If `Tier` is introduced, the options validator should cross-check that the `BaseUrl` matches the tier expectation. **BACKLOG: When implementing `AIProviders:DeepL:Tier`, have the startup validator confirm `BaseUrl` matches the tier.**

### 8.5 ElevenLabs model identifier lifecycle — `scribe_v1`

ElevenLabs introduced `scribe_v1` as their current-generation Speech-to-Text model. Their older models (which used OpenAI Whisper under the hood) were retired. `scribe_v1` is fully config-driven with no hardcoded sites, so a deprecation would require only env var updates. However, ElevenLabs has not announced `scribe_v2` as of this writing. The `model_id` field is a form parameter — if ElevenLabs changes the parameter name (not just the value) in a future API version, `ElevenLabsTranscriptionService.cs:99` contains the hardcoded string `"model_id"`. **BACKLOG: Monitor ElevenLabs API changelog. If `model_id` parameter is renamed, update `ElevenLabsTranscriptionService.cs:99`.**

### 8.6 Gemini response parsing uses inline JsonDocument (convention violation)

`GeminiTranslationService.ParseGeminiResponse` (lines 161–182) uses inline `JsonDocument` parsing instead of a shared parser equivalent to `AnthropicResponseParser`. There is no `GeminiResponseParser` utility. This is lower-stakes than the Anthropic case (Gemini is a single call site, not a multi-consumer pattern) but violates the same convention documented in CLAUDE.md: "Always use `AnthropicResponseParser.Parse()` instead of manual `JsonDocument` navigation." If a second Gemini call site is added, the parsing will be duplicated. **BACKLOG: When a second Gemini call site is added, extract a `GeminiResponseParser` utility analogous to `AnthropicResponseParser`.**

### 8.7 `CostEstimationService` rate table uses deprecated model label

`CostEstimationService.cs:17-18` contains `// claude-sonnet-4-20250514` as a comment label for the Sonnet rate. After patching to `claude-sonnet-4-6`, the comment will reference the old model. This is a documentation gap — the rate table comment should be updated to `// claude-sonnet-4-6` and the rates re-verified against the new model's pricing. **BACKLOG (minor): Update rate table comments and verify pricing for `claude-sonnet-4-6`.**

### 8.8 DeepSeek residual cleanup — consider removing

The `[Obsolete]` DeepSeek classes, interface, and settings have been retained since v6.4. They add noise to the codebase without providing any runtime value. The `ConsensusEngineTests.cs` tests covering DeepSeek code paths test an inactive pipeline path. **BACKLOG (low priority): Remove `DeepSeekTranslationService`, `IDeepSeekTranslationService`, `DeepSeekSettings`, and DeepSeek test paths in a standalone cleanup PR. Update `ConsensusEngine.cs` comments to remove DeepSeek references.**

### 8.9 `ClaudeSonnetBackTranslationService` XML doc references retired model

The XML doc at line 13 reads: `"Back-translation service using Claude Sonnet (claude-sonnet-4-20250514)"`. After the patch, this is stale. The XML doc should be updated to reference the config-driven identifier rather than a hardcoded model name. **Minor — include in same PR as structural patch.**

### 8.10 TransVal `ClaudeHaikuBackTranslationService` reads `SubtitleProcessing:Claude` for API key

`ClaudeHaikuBackTranslationService` injects `IOptions<SubtitleProcessingSettings>` (not `IOptions<ClaudeSettings>` directly) to get the API key and base URL. This means the Haiku back-translation service is coupled to the subtitle processing config section naming. After the structural patch introduces `AIProviders`, the API key reading pattern is unchanged, but the coupling to `SubtitleProcessing:Claude:ApiKey` for non-subtitle use should be noted. **This is not a new problem — it predates this recon. The BACKLOG item for `ClaudeSettings` rename from the Anthropic recon covers this.**

---

## 9. Coverage Table

| Recon step | Evidence in this report |
|---|---|
| Step 0 — Pre-flight reads | Anthropic recon, `appsettings.json`, `appsettings.Development.json`, `ClaudeSettings.cs`, `TranslationValidationSettings.cs` all read. |
| Step 1 — Provider catalog | Section 2: Five providers catalogued (Anthropic, Gemini, DeepL, ElevenLabs, DeepSeek[Obsolete]). No OpenAI, Azure Cognitive, Bedrock, HuggingFace, Cohere, Mistral found. |
| Step 2 — Per-provider site catalog | Section 3: Comprehensive tables per provider covering all executable, config, and non-executable (comment/test) sites with file paths, line numbers, identifiers, classification, and feature. |
| Step 3 — Call pattern classification | Section 4: Each provider classified by call pattern (model-in-body, model-in-URL-path, API-version-in-URL, form-field). Config-driven vs hardcoded assessed per provider. |
| Step 4 — Unified configuration proposal | Section 5: `AIProviders` JSON shape proposed. Naming rationale given. `Models` dict vs named fields decision explained. API key handling addressed. `IValidateOptions<T>` fail-fast startup validation specified. |
| Step 5 — Patch shape | Section 6: New files, modified files, Railway env vars all listed. Scope estimated at ~14 files. |
| Step 6 — Verification approach | Section 7: 13 per-provider smoke tests specified (9 Anthropic including Haiku, 1 Gemini, 1 DeepL, 1 ElevenLabs, 1 startup validation). Quality regression guidance and log inspection window included. |
| Step 7 — Adjacent observations | Section 8: 10 observations documented covering security, orphaned config, API version exposure, DeepL tier footgun, ElevenLabs lifecycle, Gemini parsing conventions, rate table staleness, DeepSeek cleanup, stale XML docs, and API key coupling. |
