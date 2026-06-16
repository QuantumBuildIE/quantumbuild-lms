# Multi-Provider Config Unification — P0 Anthropic Deprecation Fix

**Date:** 2026-06-16  
**Priority:** P0  
**Branch:** `transval`  
**BACKLOG:** §5.28 (Done), §5.29 (Follow-up items, Open)

---

## Incident Summary

Anthropic retired `claude-sonnet-4-20250514` (claude-sonnet-4-0) effective 2026-06-15 after a 60-day deprecation notice. Six production code paths broke at runtime:

| Code path | File | Symptom |
|---|---|---|
| Help chat | `HelpChatController.cs` | 502 from Anthropic API |
| Subtitle translation | `appsettings.json` — `SubtitleProcessing:Claude:Model` | 502 from Anthropic API |
| Requirement ingestion | `RequirementIngestionJob.cs` | Hangfire job failures |
| Requirement mapping | `RequirementMappingJob.cs` | Hangfire job failures |
| Regulatory scoring | `RegulatoryScoreService.cs` | Scoring endpoint errors |
| Round 3 back-translation | `appsettings.json` — `TranslationValidation:Round3DModel` | TransVal Round 3 failures |

---

## Design Decision

Rather than a narrow string-swap, the team chose to implement a unified `AIProviders` config section covering all active AI model identifiers. This prevents the same class of failure recurring silently when any provider retires a model.

**Constraint:** API keys were explicitly kept in their existing provider-specific config locations. Key migration to environment variables only is tracked in §5.29 item 1.

---

## Files Changed

### New Files

| File | Purpose |
|---|---|
| `src/Core/QuantumBuild.Core.Application/Configuration/AIProviderOptions.cs` | Options class hierarchy for unified provider registry |
| `src/Core/QuantumBuild.Core.Application/Configuration/AIProviderOptionsValidator.cs` | `IValidateOptions<AIProviderOptions>` — fail-fast startup validation |

### Modified Files

#### Config

| File | Change |
|---|---|
| `src/QuantumBuild.API/appsettings.json` | Added `AIProviders` section; updated `SubtitleProcessing:Claude:Model` → `claude-sonnet-4-5`; updated `TranslationValidation:Round3DModel` → `claude-sonnet-4-5`; removed orphaned `Round3Provider` key |
| `src/QuantumBuild.API/appsettings.Development.json` | Added `AIProviders` section; updated `SubtitleProcessing:Claude:Model` → `claude-sonnet-4-5` |

#### DI Registration

| File | Change |
|---|---|
| `src/QuantumBuild.API/Program.cs` | Added `AddOptions<AIProviderOptions>().BindConfiguration().ValidateOnStart()` + singleton validator registration |

#### Settings Classes (hardcoded defaults removed)

| File | Property | Before | After |
|---|---|---|---|
| `Core.Application/Abstractions/AI/ClaudeSettings.cs` | `Model` | `"claude-sonnet-4-20250514"` | `string.Empty` |
| `ToolboxTalks.Infrastructure/Configuration/TranslationValidationSettings.cs` | `Round1AModel` | `"claude-haiku-4-5-20251001"` | `string.Empty` |
| `ToolboxTalks.Infrastructure/Configuration/TranslationValidationSettings.cs` | `Round3DModel` | `"claude-sonnet-4-20250514"` | `string.Empty` |
| `ToolboxTalks.Infrastructure/Configuration/TranslationValidationSettings.cs` | `GeminiSettings.Model` | `"gemini-2.0-flash"` | `string.Empty` |
| `ToolboxTalks.Infrastructure/Configuration/SubtitleProcessingSettings.cs` | `ElevenLabsSettings.Model` | `"scribe_v1"` | `string.Empty` |

#### Service Sites Converted to IOptions\<AIProviderOptions\>

| Site | File | Field | Model |
|---|---|---|---|
| 1 | `API/Controllers/HelpChatController.cs` | `_aiProviders.Anthropic.Models.Sonnet` | Sonnet |
| 2 | `Jobs/RequirementMappingJob.cs` | `_sonnetModel` | Sonnet |
| 3 | `Jobs/RequirementIngestionJob.cs` | `_sonnetModel` | Sonnet |
| 4 | `Services/Validation/RegulatoryScoreService.cs` | `_sonnetModel` | Sonnet |
| 5 | `Services/Validation/DialectDetectionService.cs` | `_haikuModel` | Haiku |
| 6 | `Services/Validation/PreFlightScanService.cs` | `_haikuModel` (primary ctor) | Haiku |

#### Adjacent Items

| File | Change |
|---|---|
| `Services/Validation/CostEstimationService.cs` | Updated comment `// claude-sonnet-4-20250514` → `// claude-sonnet-4-5` |
| `Services/Validation/ClaudeSonnetBackTranslationService.cs` | Updated XML doc to reference config-driven model identifier |

---

## AIProviders Config Section Shape

```json
"AIProviders": {
  "Anthropic": {
    "Models": {
      "Sonnet": "claude-sonnet-4-5",
      "Haiku": "claude-haiku-4-5-20251001"
    }
  },
  "Gemini": {
    "Models": {
      "Flash": "gemini-2.0-flash"
    }
  },
  "ElevenLabs": {
    "Models": {
      "Transcription": "scribe_v1"
    }
  }
}
```

**Note:** No API keys in this section — keys remain in their existing provider-specific config locations.

## Railway Environment Variables Required Before Deploy

```
AIProviders__Anthropic__Models__Sonnet=claude-sonnet-4-5
AIProviders__Anthropic__Models__Haiku=claude-haiku-4-5-20251001
AIProviders__Gemini__Models__Flash=gemini-2.0-flash
AIProviders__ElevenLabs__Models__Transcription=scribe_v1
```

The fail-fast validator will cause startup to fail with a clear error if any of these are missing — set them in both Development and Production Railway services before deploying.

---

## Fail-Fast Validation

`AIProviderOptionsValidator` implements `IValidateOptions<AIProviderOptions>` and validates all four required model identifiers at startup via `.ValidateOnStart()`. Any missing value causes the app to fail immediately with a descriptive error rather than a silent null-reference at the first API call.

---

## Follow-up Items

See BACKLOG §5.29 for the non-blocking follow-up items:
1. API key migration to Railway env vars only
2. Verify `claude-sonnet-4-5` pricing rates in `CostEstimationService`
3. Add DI registration rules to CLAUDE.md conventions
4. Confirm Railway env vars set before deploy
