# ElevenLabs `unsupported_model` Error — Diagnostic Recon

**Date:** 2026-06-22  
**Branch:** `transval`  
**Status:** Read-only recon — no files modified

---

## 1. One-line root cause

`ElevenLabsTranscriptionService` reads the model identifier from `SubtitleProcessingSettings.ElevenLabs.Model` (the old `SubtitleProcessing:ElevenLabs:Model` config key), but the §5.28 fix removed the hardcoded C# default for that property, added a new canonical key (`AIProviders:ElevenLabs:Models:Transcription`), and validated only the *new* key at startup — never migrating the service to read from the new key and never adding a bridge between the two.

---

## 2. Bug classification: **B**

> Service still reads from the old `SubtitleProcessing:ElevenLabs:Model` key, and the C# default for that property was changed to `string.Empty` by §5.28.

### Evidence

#### Call site

**File:** [ElevenLabsTranscriptionService.cs](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Subtitles/ElevenLabsTranscriptionService.cs)

| Line | Code |
|------|------|
| 21 | `IOptions<SubtitleProcessingSettings> settings` injected |
| 25 | `_settings = settings.Value;` |
| 91 | `_logger.LogInformation("... Model: {Model}", _settings.ElevenLabs.Model);` |
| 99 | `{ new StringContent(_settings.ElevenLabs.Model), "model_id" }` ← **empty string sent here** |

#### Property default (post-§5.28)

**File:** [SubtitleProcessingSettings.cs:70](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Configuration/SubtitleProcessingSettings.cs#L70)

```csharp
/// <summary>
/// Transcription model to use.
/// Value sourced from AIProviders:ElevenLabs:Models:Transcription (no hardcoded fallback).
/// </summary>
public string Model { get; set; } = string.Empty;   // ← was "scribe_v1" before §5.28
```

The doc comment documents the *intended* architecture. There is no code implementing it — no `IPostConfigureOptions` bridge exists anywhere in the codebase (confirmed by `grep PostConfigure` returning no matches).

#### DI registration (no bridge)

**File:** [ServiceCollectionExtensions.cs:71–72](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/ServiceCollectionExtensions.cs#L71)

```csharp
services.Configure<SubtitleProcessingSettings>(
    configuration.GetSection(SubtitleProcessingSettings.SectionName));
```

Plain bind, no `PostConfigure<SubtitleProcessingSettings>` step. `ElevenLabsTranscriptionService` is registered at line 88:

```csharp
services.AddHttpClient<ITranscriptionService, ElevenLabsTranscriptionService>(...)
    .AddPolicyHandler(...);
```

The service is **not** in the §5.28 fix report's table of "Service Sites Converted to `IOptions<AIProviderOptions>`" — only 6 Anthropic/Gemini call sites were migrated.

#### What the service actually receives

| Config source | Key | Value |
|---|---|---|
| `appsettings.json` | `SubtitleProcessing:ElevenLabs:Model` | `"scribe_v1"` ✓ |
| `appsettings.Development.json` | `SubtitleProcessing:ElevenLabs:Model` | `"scribe_v1"` ✓ |
| C# property default | *(fallback when key absent from all sources)* | `string.Empty` ✗ |

Both JSON files have the correct value, so the error **cannot reproduce locally**. In the Railway environment where it manifests, the most likely explanation is a Railway env var `SubtitleProcessing__ElevenLabs__Model` that is either:
- Explicitly set to empty string (`""`), which overrides the JSON value; or
- Was deleted after §5.28 on the assumption that `AIProviders__ElevenLabs__Models__Transcription` was now the active path (misled by the doc comment), leaving the app to rely only on the bundled `appsettings.json` — which *should* still work, but reveals the architectural fragility.

---

## 3. Why the §5.28 validator didn't catch it

**File:** [AIProviderOptionsValidator.cs:22–23](src/Core/QuantumBuild.Core.Application/Configuration/AIProviderOptionsValidator.cs#L22)

```csharp
if (string.IsNullOrWhiteSpace(options.ElevenLabs.Models.Transcription))
    errors.Add("AIProviders:ElevenLabs:Models:Transcription must be set");
```

The validator checks `AIProviders:ElevenLabs:Models:Transcription` — the **new** canonical key — which IS set to `"scribe_v1"` in both `appsettings.json` and `appsettings.Development.json`. Validator passes → startup succeeds.

There is **no validator** for `SubtitleProcessingSettings`. Nothing checks whether `SubtitleProcessing:ElevenLabs:Model` (the key the service actually reads) is populated. The gap is:

```
Validated at startup: AIProviders:ElevenLabs:Models:Transcription = "scribe_v1"  ✓ (passes)
Read at runtime:      SubtitleProcessing:ElevenLabs:Model           = ???         ✗ (not validated)
```

The two keys are completely independent — no code links them.

---

## 4. Fix shape

Migrate `ElevenLabsTranscriptionService` to inject `IOptions<AIProviderOptions>` and read from `_aiProviders.ElevenLabs.Models.Transcription` instead of `_settings.ElevenLabs.Model`. This puts the read path in alignment with what the validator already validates, and matches the stated intent of the §5.28 doc comment.

**Alternative (narrower):** Add a `PostConfigure<SubtitleProcessingSettings>` that copies from `AIProviderOptions.ElevenLabs.Models.Transcription` into `SubtitleProcessingSettings.ElevenLabs.Model`. Preserves the existing service code but adds an invisible data-flow dependency between two options classes.

The migration approach is cleaner and consistent with how the 6 other sites were handled in §5.28.

---

## 5. Production deploy implications

The §5.28 fix report states: *"Code shipped to Development and smoke-verified... Production deployment is an operational concern."*

**If §5.28 has NOT been merged to `main`:** Production still has the old code where `SubtitleProcessingSettings.ElevenLabsSettings.Model` defaulted to `"scribe_v1"` in C#. Production is **not affected** by this bug yet.

**If §5.28 HAS been merged to `main`:** Production is exposed. The Railway Production service would rely on:
- `SubtitleProcessing__ElevenLabs__Model` Railway env var (if set), or
- The bundled `appsettings.json` value `"scribe_v1"` as fallback

If Production's Railway env vars include `SubtitleProcessing__ElevenLabs__Model` set to empty or the env var was deleted post-§5.28, Production transcription is broken.

The §5.28 fix report's Railway env var checklist does **not** include `SubtitleProcessing__ElevenLabs__Model` — it lists only `AIProviders__*` vars. Any operator following the fix report would not have set/preserved the old key.

---

## 6. New BACKLOG entries recommended

### A. Migrate `ElevenLabsTranscriptionService` to `AIProviderOptions` (§5.29 completion item)

The §5.28 fix report already lists this as a §5.29 follow-up ("six operational config keys still hold the same identifiers separately, including `SubtitleProcessing:ElevenLabs:Model`"). This bug is the concrete failure that resulted from the incomplete migration. Escalate to **High** priority; it's no longer a housekeeping item.

### B. Add startup validator for `SubtitleProcessingSettings`

As belt-and-suspenders until the service migration is complete, add `IValidateOptions<SubtitleProcessingSettings>` (registered with `.ValidateOnStart()`) that checks:
- `SubtitleProcessing:ElevenLabs:ApiKey` is non-empty
- `SubtitleProcessing:ElevenLabs:Model` is non-empty

This would have caught the current bug at startup with a clear error message, preventing the silent runtime failure. Mirrors the existing `AIProviderOptionsValidator` pattern.

---

## Appendix — Key file locations

| File | Role |
|---|---|
| [ElevenLabsTranscriptionService.cs:99](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Subtitles/ElevenLabsTranscriptionService.cs#L99) | Call site — `model_id` sent to ElevenLabs |
| [SubtitleProcessingSettings.cs:70](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Configuration/SubtitleProcessingSettings.cs#L70) | `Model` property — default now `string.Empty` |
| [AIProviderOptions.cs:61](src/Core/QuantumBuild.Core.Application/Configuration/AIProviderOptions.cs#L61) | New canonical `Transcription` property — not read by service |
| [AIProviderOptionsValidator.cs:22](src/Core/QuantumBuild.Core.Application/Configuration/AIProviderOptionsValidator.cs#L22) | Validator — checks new key, not old key |
| [ServiceCollectionExtensions.cs:71](src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/ServiceCollectionExtensions.cs#L71) | DI binding — no `PostConfigure` bridge |
| [appsettings.json:81–84](src/QuantumBuild.API/appsettings.json#L81) | Has `SubtitleProcessing:ElevenLabs:Model = "scribe_v1"` (correct) |
| [appsettings.Development.json:54–57](src/QuantumBuild.API/appsettings.Development.json#L54) | Has `SubtitleProcessing:ElevenLabs:Model = "scribe_v1"` (correct) |
| [docs/phase-5/reports/multi-provider-config-fix.md](docs/phase-5/reports/multi-provider-config-fix.md) | §5.28 fix report — ElevenLabs transcription service listed in config table but not in "converted sites" table |
