# Anthropic Model Identifier Hotfix — Fix Report

**Branch:** `main`  
**Pre-fix HEAD:** `47c80e7` (Merge branch 'transval')  
**Date:** 2026-06-17  
**Scope:** Group A only — 4 hardcoded call sites. Group B and Group D deliberately excluded.

---

## What Was Fixed

Anthropic retired `claude-sonnet-4-20250514` on 2026-06-15. Four call sites on `main` hardcoded this identifier directly in the HTTP request body, bypassing configuration entirely. No config override could reach them.

**Files changed — 4 files, 4 lines:**

| File | Line | Change |
|------|------|--------|
| `src/Modules/ToolboxTalks/.../Jobs/RequirementIngestionJob.cs` | 23 | `private const string SonnetModel` |
| `src/Modules/ToolboxTalks/.../Jobs/RequirementMappingJob.cs` | 23 | `private const string SonnetModel` |
| `src/Modules/ToolboxTalks/.../Services/Validation/RegulatoryScoreService.cs` | 23 | `private const string SonnetModel` |
| `src/QuantumBuild.API/Controllers/HelpChatController.cs` | 48 | Inline `model =` field |

All four: `"claude-sonnet-4-20250514"` → `"claude-sonnet-4-5"`

---

## Verification

- **git diff:** Exactly 4 single-line substitutions. No other changes.
- **git status:** Exactly 4 modified files (plus untracked recon doc and test results — both pre-existing untracked files, untouched).
- **dotnet build:** 0 errors, 9 warnings. All 9 warnings are pre-existing; none are in the 4 changed files. Warning count consistent with pre-fix baseline.
- **Branch at commit time:** `main`.

---

## What Was Deliberately NOT Changed

### Group B — C# default property values

| File | Line | Current default (still wrong) | Config override currently masking it |
|------|------|-------------------------------|---------------------------------------|
| `ClaudeSettings.cs` | 23 | `claude-sonnet-4-20250514` | `appsettings.json:87` (`claude-sonnet-4-5`) |
| `TranslationValidationSettings.cs` | 74 | `claude-sonnet-4-20250514` | `appsettings.json:146` (`claude-sonnet-4-5`) |

These are wrong but not breaking Production today — the config overrides are already in place. The next normal `transval → main` merge will supersede them entirely via the AIProviders config rework (Phase 5 closeout work on `transval`).

### Group D — Documentation / comments

~10 occurrences of `claude-sonnet-4-20250514` in doc comments, XML doc strings, and architecture notes across 9 files. No runtime impact. Left for the next `transval → main` merge.

### Test files

`ClaudeTranslationServiceTests.cs` lines 37 and 392 reference `claude-sonnet-4-20250514`. Per the recon, these test `ClaudeSettings.cs` defaults — not any of the 4 Group A sites. Since Group B (`ClaudeSettings.cs` default value) was deliberately not changed, the test assertions remain valid and were not touched.

---

## AIProviders Config Block

The `AIProviders` JSON section leaked into both `appsettings.json` (lines 24–41) and `appsettings.Development.json` (lines 8–25) via the `47c80e7 Merge branch 'transval'` commit. **Left alone deliberately.** No C# binding class exists on `main` — the section is inert. It will be fully wired up when the Phase 5 AIProviders work on `transval` reaches `main` through the normal promotion pipeline.

---

## Next Steps (not part of this hotfix)

- **Full fix on `transval`:** Phase 5 closeout — AIProviders configuration section, `IValidateOptions<AIProviderOptions>` startup validation, six sites converted to inject `IOptions<AIProviderOptions>` — will supersede both Group B defaults and Group D doc comments when it merges to `main`.
- **Push to Production:** Awaiting explicit push instructions. Commands will be provided separately by the user, with verification between `origin` push and `company` push.

---

## Recon Document

Full enumeration of all 34 `claude-*` occurrences on `main`, classification by group, and the target list that drove this fix:  
`docs/main-hotfix-anthropic-model-recon.md`
