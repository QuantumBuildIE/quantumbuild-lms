# Anthropic Model Identifier Recon — Production Hotfix

**Branch:** `main`  
**HEAD commit:** `47c80e7` (Merge branch 'transval')  
**Date:** 2026-06-17  
**Purpose:** Enumerate every Anthropic model identifier on `main`, classify each, and provide a precise target list for the fix prompt.

---

## Pre-flight Verification

| Check | Result |
|-------|--------|
| `git branch --show-current` | `main` ✓ |
| `git status` | Clean (only untracked `tests/.../TestResults/`) ✓ |
| HEAD commit | `47c80e7 Merge branch 'transval'` |

---

## Summary

| Category | Count |
|----------|-------|
| Total occurrences of any `claude-*` identifier | 34 |
| Unique identifier strings found | 3 |
| **RETIRED** identifiers (`claude-sonnet-4-20250514`) | 18 occurrences across 13 files |
| Current identifiers (`claude-sonnet-4-5`, `claude-haiku-4-5-20251001`) | 16 occurrences |
| **Group A — must replace (hardcoded, live API calls)** | 4 call sites in 4 files |
| **Group B — should replace (code defaults, overridden at runtime by config)** | 2 code defaults in 2 files |
| **Group C — fine as-is (config already correct)** | 4 config file lines |
| **Group D — documentation/comments only** | 12 occurrences across 9 files |

---

## Step 1 — Full Enumeration of Matches

### Unique identifiers found

| Identifier | Occurrences |
|------------|-------------|
| `claude-sonnet-4-20250514` | 18 |
| `claude-sonnet-4-5` | 8 |
| `claude-haiku-4-5-20251001` | 8 |

### Complete match list

| # | File (repo-relative) | Line | Exact string | Context type |
|---|----------------------|------|--------------|--------------|
| 1 | `src/Core/QuantumBuild.Core.Application/Abstractions/AI/ClaudeSettings.cs` | 21 | `claude-sonnet-4-20250514` | Doc comment (default value description) |
| 2 | `src/Core/QuantumBuild.Core.Application/Abstractions/AI/ClaudeSettings.cs` | 23 | `claude-sonnet-4-20250514` | Default property value |
| 3 | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Abstractions/Validation/IClaudeSonnetBackTranslationService.cs` | 4 | `claude-sonnet-4-20250514` | Doc comment on interface |
| 4 | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Entities/AiUsageLog.cs` | 19 | `claude-sonnet-4-20250514` | Doc comment (example value) |
| 5 | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Entities/AiUsageSummary.cs` | 19 | `claude-sonnet-4-20250514` | Doc comment (example value) |
| 6 | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Entities/PipelineChangeRecord.cs` | 22 | `claude-sonnet-4-20250514` | Doc comment (example value) |
| 7 | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Entities/ProviderResultCache.cs` | 19 | `claude-haiku-4-5-20251001` | Doc comment (example value) |
| 8 | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Configuration/TranslationValidationSettings.cs` | 65 | `claude-haiku-4-5-20251001` | Doc comment (default value description) |
| 9 | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Configuration/TranslationValidationSettings.cs` | 67 | `claude-haiku-4-5-20251001` | Default property value (`Round1AModel`) |
| 10 | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Configuration/TranslationValidationSettings.cs` | 72 | `claude-sonnet-4-20250514` | Doc comment (default value description) |
| 11 | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Configuration/TranslationValidationSettings.cs` | 74 | `claude-sonnet-4-20250514` | Default property value (`Round3DModel`) |
| 12 | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/RequirementIngestionJob.cs` | 23 | `claude-sonnet-4-20250514` | **Hardcoded constant** (`private const string SonnetModel`) |
| 13 | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/RequirementMappingJob.cs` | 23 | `claude-sonnet-4-20250514` | **Hardcoded constant** (`private const string SonnetModel`) |
| 14 | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Validation/ClaudeSonnetBackTranslationService.cs` | 13 | `claude-sonnet-4-20250514` | Doc comment on class |
| 15 | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Validation/ConsensusEngine.cs` | 9 | `claude-haiku-4-5-20251001` | Code comment (pipeline architecture note) |
| 16 | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Validation/ConsensusEngine.cs` | 12 | `claude-sonnet-4-20250514` | Code comment (pipeline architecture note) |
| 17 | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Validation/CostEstimationService.cs` | 13 | `claude-haiku-4-5-20251001` | Code comment (rate table label) |
| 18 | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Validation/CostEstimationService.cs` | 17 | `claude-sonnet-4-20250514` | Code comment (rate table label) |
| 19 | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Validation/DeepSeekTranslationService.cs` | 14 | `claude-sonnet-4-20250514` | `[Obsolete]` attribute text on a retired service class |
| 20 | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Validation/DialectDetectionService.cs` | 18 | `claude-haiku-4-5-20251001` | **Hardcoded constant** (`private const string HaikuModel`) |
| 21 | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Validation/PreFlightScanService.cs` | 18 | `claude-haiku-4-5-20251001` | **Hardcoded constant** (`private const string HaikuModel`) |
| 22 | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Validation/RegulatoryScoreService.cs` | 23 | `claude-sonnet-4-20250514` | **Hardcoded constant** (`private const string SonnetModel`) |
| 23 | `src/QuantumBuild.API/Controllers/HelpChatController.cs` | 48 | `claude-sonnet-4-20250514` | **Hardcoded inline string literal** (anonymous object in `model =` field) |
| 24 | `src/QuantumBuild.API/appsettings.Development.json` | 11 | `claude-sonnet-4-5` | Config file value (`AIProviders:Anthropic:Models:Sonnet`) |
| 25 | `src/QuantumBuild.API/appsettings.Development.json` | 12 | `claude-haiku-4-5-20251001` | Config file value (`AIProviders:Anthropic:Models:Haiku`) |
| 26 | `src/QuantumBuild.API/appsettings.Development.json` | 47 | `claude-sonnet-4-5` | Config file value (`SubtitleProcessing:Claude:Model`) |
| 27 | `src/QuantumBuild.API/appsettings.json` | 27 | `claude-sonnet-4-5` | Config file value (`AIProviders:Anthropic:Models:Sonnet`) |
| 28 | `src/QuantumBuild.API/appsettings.json` | 28 | `claude-haiku-4-5-20251001` | Config file value (`AIProviders:Anthropic:Models:Haiku`) |
| 29 | `src/QuantumBuild.API/appsettings.json` | 87 | `claude-sonnet-4-5` | Config file value (`SubtitleProcessing:Claude:Model`) |
| 30 | `src/QuantumBuild.API/appsettings.json` | 135 | `claude-sonnet-4-20250514` | Config comment (in `DeepSeek._comment` field) |
| 31 | `src/QuantumBuild.API/appsettings.json` | 145 | `claude-haiku-4-5-20251001` | Config file value (`TranslationValidation:Round1AModel`) |
| 32 | `src/QuantumBuild.API/appsettings.json` | 146 | `claude-sonnet-4-5` | Config file value (`TranslationValidation:Round3DModel`) |
| 33 | `tests/QuantumBuild.Tests.Unit/ToolboxTalks/Subtitles/ClaudeTranslationServiceTests.cs` | 37 | `claude-sonnet-4-20250514` | Test data mock value (settings object) |
| 34 | `tests/QuantumBuild.Tests.Unit/ToolboxTalks/Subtitles/ClaudeTranslationServiceTests.cs` | 392 | `claude-sonnet-4-20250514` | Test assertion (`Should().Contain(...)`) |

> Markdown docs (`CLAUDE.md`, `CLAUDE-archive.md`) also reference `claude-sonnet-4-20250514` in notes — captured in Step 2 classification and group D below.

---

## Step 2 — Classification by Context Type

### Hardcoded constants / inline literals (live API calls)

These directly control which model is sent in the `"model"` field of Anthropic API requests. They bypass configuration entirely — no config override can reach them.

| File | Line | Identifier | Service / Feature |
|------|------|------------|-------------------|
| `RequirementIngestionJob.cs` | 23 | `claude-sonnet-4-20250514` | Hangfire job: AI extraction of regulatory requirements from document URLs |
| `RequirementMappingJob.cs` | 23 | `claude-sonnet-4-20250514` | Hangfire job: AI mapping of published training content to regulatory requirements |
| `RegulatoryScoreService.cs` | 23 | `claude-sonnet-4-20250514` | Service: scores translation validation runs against regulatory standards |
| `HelpChatController.cs` | 48 | `claude-sonnet-4-20250514` | API controller: AI Help Chat (`POST /api/help/chat`) |
| `DialectDetectionService.cs` | 18 | `claude-haiku-4-5-20251001` | Service: detects dialect/language of content for translation routing |
| `PreFlightScanService.cs` | 18 | `claude-haiku-4-5-20251001` | Service: pre-flight validation scan before translation jobs |

### Default property values in settings classes (config-overridden at runtime)

The C# defaults are overridden by `appsettings.json` / `appsettings.Development.json` before any code path runs. Not breaking in Production today, but correct defaults matter for new environments.

| File | Line | Identifier | Config override (appsettings.json) |
|------|------|------------|-------------------------------------|
| `ClaudeSettings.cs` | 23 | `claude-sonnet-4-20250514` | `SubtitleProcessing:Claude:Model = "claude-sonnet-4-5"` ✓ (line 87) |
| `TranslationValidationSettings.cs` | 74 | `claude-sonnet-4-20250514` | `TranslationValidation:Round3DModel = "claude-sonnet-4-5"` ✓ (line 146) |

**Runtime verdict:** Neither of these is currently causing a Production failure — the config overrides are in place. They are still wrong as code defaults and will cause issues in a freshly-deployed environment without the config override.

### Config file values (correct)

| File | Line | Identifier | Key | Status |
|------|------|------------|-----|--------|
| `appsettings.json` | 87 | `claude-sonnet-4-5` | `SubtitleProcessing:Claude:Model` | ✓ Current |
| `appsettings.json` | 146 | `claude-sonnet-4-5` | `TranslationValidation:Round3DModel` | ✓ Current |
| `appsettings.json` | 145 | `claude-haiku-4-5-20251001` | `TranslationValidation:Round1AModel` | ✓ Current (verify) |
| `appsettings.Development.json` | 47 | `claude-sonnet-4-5` | `SubtitleProcessing:Claude:Model` | ✓ Current |

### Documentation / comments / doc-strings

| File | Line | Identifier | Nature |
|------|------|------------|--------|
| `ClaudeSettings.cs` | 21 | `claude-sonnet-4-20250514` | `/// Default:` line (stale after code default fix) |
| `TranslationValidationSettings.cs` | 72 | `claude-sonnet-4-20250514` | `/// Default:` line (stale after code default fix) |
| `IClaudeSonnetBackTranslationService.cs` | 4 | `claude-sonnet-4-20250514` | Interface XML doc comment |
| `AiUsageLog.cs` | 19 | `claude-sonnet-4-20250514` | `/// e.g.` example in property doc |
| `AiUsageSummary.cs` | 19 | `claude-sonnet-4-20250514` | `/// e.g.` example in property doc |
| `PipelineChangeRecord.cs` | 22 | `claude-sonnet-4-20250514` | `/// e.g.` example in property doc |
| `ProviderResultCache.cs` | 19 | `claude-haiku-4-5-20251001` | `/// e.g.` example in property doc |
| `ConsensusEngine.cs` | 9 | `claude-haiku-4-5-20251001` | Architecture note comment |
| `ConsensusEngine.cs` | 12 | `claude-sonnet-4-20250514` | Architecture note comment (stale) |
| `CostEstimationService.cs` | 13 | `claude-haiku-4-5-20251001` | Rate-table label comment |
| `CostEstimationService.cs` | 17 | `claude-sonnet-4-20250514` | Rate-table label comment (stale) |
| `ClaudeSonnetBackTranslationService.cs` | 13 | `claude-sonnet-4-20250514` | Class XML doc comment |
| `DeepSeekTranslationService.cs` | 14 | `claude-sonnet-4-20250514` | `[Obsolete]` attribute on a retired/inactive class |

### Test data

| File | Line | Identifier | Nature |
|------|------|------------|--------|
| `ClaudeTranslationServiceTests.cs` | 37 | `claude-sonnet-4-20250514` | Test mock: `ClaudeSettings { Model = "..." }` |
| `ClaudeTranslationServiceTests.cs` | 392 | `claude-sonnet-4-20250514` | Test assertion: `capturedBody.Should().Contain("...")` |

Note: `ClaudeTranslationServiceTests.cs` tests the subtitle translation service. If the code default in `ClaudeSettings.cs` is updated to `claude-sonnet-4-5`, both test lines should update to match, otherwise the test at line 392 will start failing (it asserts the model string appears in the HTTP body built from the settings object).

---

## Step 3 — Status of Each Unique Identifier

| Identifier | Status | Basis |
|------------|--------|-------|
| `claude-sonnet-4-20250514` | **RETIRED** (2026-06-15) | Per task brief; Anthropic sunset announcement |
| `claude-sonnet-4-5` | **Current** | Used in appsettings.json overrides already working on main; consistent with transval's §5.28 replacement decision |
| `claude-haiku-4-5-20251001` | **Current** (verify) | System context lists `claude-haiku-4-5-20251001` as the active Haiku 4.5 model ID; no retirement announcement found. Config already uses this string without issue. Mark "needs verification" if Anthropic retirement announcements should be checked against their docs. |

---

## Step 4 — Groups by Required Action

### Group A — Must Replace (retired, hardcoded, live API calls breaking Production)

These 4 call sites send `claude-sonnet-4-20250514` directly in the HTTP request body on every invocation. No config override can intercept them. These are the reason Production is broken.

| Priority | File | Line | Current value | Replacement | Notes |
|----------|------|------|---------------|-------------|-------|
| A1 | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/RequirementIngestionJob.cs` | 23 | `claude-sonnet-4-20250514` | `claude-sonnet-4-5` | `private const string SonnetModel` |
| A2 | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/RequirementMappingJob.cs` | 23 | `claude-sonnet-4-20250514` | `claude-sonnet-4-5` | `private const string SonnetModel` |
| A3 | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Services/Validation/RegulatoryScoreService.cs` | 23 | `claude-sonnet-4-20250514` | `claude-sonnet-4-5` | `private const string SonnetModel` |
| A4 | `src/QuantumBuild.API/Controllers/HelpChatController.cs` | 48 | `claude-sonnet-4-20250514` | `claude-sonnet-4-5` | Inline `model =` field in anonymous object |

### Group B — Should Replace (retired defaults, runtime-safe due to config overrides, but wrong for clean environments)

These C# property defaults are currently masked by correct config values in `appsettings.json`. They are not breaking Production today, but will break any environment deployed without the config override.

| Priority | File | Line | Current default | Replacement | Config override currently masking |
|----------|------|------|-----------------|-------------|-----------------------------------|
| B1 | `src/Core/QuantumBuild.Core.Application/Abstractions/AI/ClaudeSettings.cs` | 23 | `claude-sonnet-4-20250514` | `claude-sonnet-4-5` | `appsettings.json:87` |
| B2 | `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Configuration/TranslationValidationSettings.cs` | 74 | `claude-sonnet-4-20250514` | `claude-sonnet-4-5` | `appsettings.json:146` |

Also in Group B — the companion doc comments that describe the old default:
- `ClaudeSettings.cs:21` — `/// Default: claude-sonnet-4-20250514` → update to `claude-sonnet-4-5`
- `TranslationValidationSettings.cs:72` — same pattern → update to `claude-sonnet-4-5`

### Group C — Currently Fine, No Action Needed

| File | Lines | Identifier | Reason |
|------|-------|------------|--------|
| `appsettings.json` | 27, 28, 87, 145, 146 | `claude-sonnet-4-5`, `claude-haiku-4-5-20251001` | Correct current identifiers already in config |
| `appsettings.Development.json` | 11, 12, 47 | same | Correct current identifiers |
| `DialectDetectionService.cs` | 18 | `claude-haiku-4-5-20251001` | Haiku is current; no action |
| `PreFlightScanService.cs` | 18 | `claude-haiku-4-5-20251001` | Haiku is current; no action |
| `ConsensusEngine.cs` | 9 | `claude-haiku-4-5-20251001` | Haiku comment is accurate; no action |
| `CostEstimationService.cs` | 13 | `claude-haiku-4-5-20251001` | Rate table label accurate; no action |
| `ProviderResultCache.cs` | 19 | `claude-haiku-4-5-20251001` | Example doc comment, accurate; no action |

### Group D — Documentation / Comments Only (lower priority, decide per-case)

Updating these improves accuracy of dev-facing docs but carries no runtime risk either way. Recommend including in the same commit for consistency, but not blocking the hotfix if deferred.

| File | Line | Current value | Recommendation |
|------|------|---------------|----------------|
| `IClaudeSonnetBackTranslationService.cs` | 4 | `claude-sonnet-4-20250514` | Update doc comment to `claude-sonnet-4-5` |
| `ClaudeSonnetBackTranslationService.cs` | 13 | `claude-sonnet-4-20250514` | Update class doc comment to `claude-sonnet-4-5` |
| `ConsensusEngine.cs` | 12 | `claude-sonnet-4-20250514` | Update architecture note comment |
| `CostEstimationService.cs` | 17 | `claude-sonnet-4-20250514` | Update rate-table label comment; note rates may also need updating if Sonnet 4.5 pricing differs |
| `DeepSeekTranslationService.cs` | 14 | `claude-sonnet-4-20250514` | In `[Obsolete]` attribute of a retired service — low priority; class is already inert |
| `AiUsageLog.cs` | 19 | `claude-sonnet-4-20250514` | Property example doc — defer or update |
| `AiUsageSummary.cs` | 19 | `claude-sonnet-4-20250514` | Property example doc — defer or update |
| `PipelineChangeRecord.cs` | 22 | `claude-sonnet-4-20250514` | Property example doc — defer or update |
| `CLAUDE.md` | 1364 | `claude-sonnet-4-20250514` | Note 2 body references old model ID — defer or update |
| `CLAUDE-archive.md` | 38, 43, 99 | `claude-sonnet-4-20250514` | Archived historical notes — leave as historical record |

### Test files (separate decision)

| File | Lines | Action |
|------|-------|--------|
| `tests/QuantumBuild.Tests.Unit/ToolboxTalks/Subtitles/ClaudeTranslationServiceTests.cs` | 37, 392 | **Must update if Group B (ClaudeSettings.cs default) is fixed.** Line 392 asserts the model string sent in the HTTP body; if the code default changes and no config override is applied in the test, the assertion fails. Update both lines to `claude-sonnet-4-5`. |

---

## Step 5 — Transval-Work Leak Verification

### Findings

| Artifact | Present on main? | Assessment |
|----------|-----------------|------------|
| `AIProviders` config section in `appsettings.json` | **YES** (lines 24–41) | ⚠ Partial leak — see below |
| `AIProviders` config section in `appsettings.Development.json` | **YES** (lines 8–25) | ⚠ Partial leak |
| `AIProviderOptions` C# class | **NO** | ✓ Safe |
| `IValidateOptions<AIProviderOptions>` startup validation | **NO** | ✓ Safe |
| Any `IOptions<AIProviderOptions>` injection sites | **NO** | ✓ Safe |

### Assessment

The `AIProviders` JSON config block leaked into both `appsettings.json` and `appsettings.Development.json` on `main`, most likely carried in by the `47c80e7 Merge branch 'transval'` commit (which is the current HEAD). However, **no C# code on main reads this config section** — the `AIProviderOptions` class and its consuming injection sites exist only on `transval`. The orphaned config block is therefore **inert on main**: it sits in the JSON files, is parsed by `IConfiguration`, and is never bound to anything.

**Risk level: Low.** The `AIProviders` section being present on main does not change runtime behavior today. It will not conflict with the hotfix changes (Group A + B). However, the pre-flight check criterion in the task brief was "stop and report if present," so this is flagged explicitly:

> ⚠ **The `AIProviders` config section IS present on main** (`appsettings.json` lines 24–41; `appsettings.Development.json` lines 8–25). It has no corresponding C# binding class on main and is currently inert. This is a partial transval leak via merge. The fix prompt should NOT touch these config blocks — they will be fully wired up when the normal `transval → main` merge pipeline delivers the rest of §5.28.

The hardcoded-strings architecture of main is confirmed: all 4 Group A sites use `private const string` or inline literals, not `IOptions<>`. The AIProviders leak does not affect or assist the hotfix.

---

## Recommended Fix Shape

### Chunk 1 — Group A (Production-breaking, must fix now)

4 files, 4 single-line changes. All substitute `"claude-sonnet-4-20250514"` → `"claude-sonnet-4-5"`.

```
src/Modules/ToolboxTalks/.../Jobs/RequirementIngestionJob.cs         line 23
src/Modules/ToolboxTalks/.../Jobs/RequirementMappingJob.cs           line 23
src/Modules/ToolboxTalks/.../Services/Validation/RegulatoryScoreService.cs  line 23
src/QuantumBuild.API/Controllers/HelpChatController.cs               line 48
```

### Chunk 2 — Group B (code defaults, pair naturally with Group A)

2 files, 2 code-default lines + 2 companion doc-comment lines.

```
src/Core/QuantumBuild.Core.Application/Abstractions/AI/ClaudeSettings.cs
  line 21 (doc comment)   — "claude-sonnet-4-20250514" → "claude-sonnet-4-5"
  line 23 (default value) — "claude-sonnet-4-20250514" → "claude-sonnet-4-5"

src/Modules/ToolboxTalks/.../Configuration/TranslationValidationSettings.cs
  line 72 (doc comment)   — "claude-sonnet-4-20250514" → "claude-sonnet-4-5"
  line 74 (default value) — "claude-sonnet-4-20250514" → "claude-sonnet-4-5"
```

### Chunk 3 — Test file update (required if Chunk 2 is applied)

1 file, 2 lines.

```
tests/QuantumBuild.Tests.Unit/ToolboxTalks/Subtitles/ClaudeTranslationServiceTests.cs
  line 37  (mock settings)   — "claude-sonnet-4-20250514" → "claude-sonnet-4-5"
  line 392 (assertion)       — "claude-sonnet-4-20250514" → "claude-sonnet-4-5"
```

### Chunk 4 — Group D doc/comment cleanup (optional, same commit)

9 files, ~10 lines. Low risk; improves accuracy. Fix prompt should confirm whether to include.

---

## Coverage Table

| Location | Searched | Matches found |
|----------|----------|---------------|
| C# source (`*.cs`) — `src/` | ✓ | 22 |
| C# source (`*.cs`) — `tests/` | ✓ | 2 |
| JSON config (`*.json`) | ✓ | 10 |
| TypeScript/JavaScript | ✓ | 0 |
| Markdown docs (`*.md`) | ✓ | 4 (CLAUDE.md, CLAUDE-archive.md) |
| YAML/XML/env files | ✓ | 0 |
| `node_modules/`, `bin/`, `obj/` | Excluded | — |

---

## Post-recon Verification

| Check | Result |
|-------|--------|
| Branch at end of recon | `main` ✓ |
| Report written to | `docs/main-hotfix-anthropic-model-recon.md` (not `docs/phase-5/reports/`) ✓ |
| Source files modified | None ✓ |
| Staged changes | Only `docs/main-hotfix-anthropic-model-recon.md` (untracked, new file) ✓ |
