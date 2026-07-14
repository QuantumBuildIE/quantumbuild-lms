# Anthropic Model Default — Verification Recon

**Date:** 2026-07-13
**Type:** Read-only verification recon (no code changes made)

## Headline

**Claim inaccurate — defaults are current models, no fix needed.**

The retired model `claude-sonnet-4-20250514` did once ship as the hardcoded
default in exactly the two locations cited (`ClaudeSettings.Model` and
`TranslationValidationSettings.Round3DModel`), and it did break production
on 2026-06-15 when Anthropic retired it. But that incident was fixed on
2026-06-16 (BACKLOG §5.28) and the properties themselves were **deleted**
on 2026-06-22 as part of a follow-up cleanup (BACKLOG §5.29, "Option B").
The outside Claude query is citing the pre-fix state as if it were current.
The specific file `docs/main-hotfix-anthropic-model-*.md` cited as context
does not exist anywhere in the repo — real recon/fix docs covering this
exact incident exist, but under different filenames (see §4 below).

## 1. ClaudeSettings.cs — exists, does not match claim

Path: `src/Core/QuantumBuild.Core.Application/Abstractions/AI/ClaudeSettings.cs`
(30 lines total)

```csharp
public class ClaudeSettings
{
    public const string SectionName = "SubtitleProcessing:Claude";
    public string ApiKey { get; set; } = string.Empty;
    public int MaxTokens { get; set; } = 4000;              // line 23
    public string BaseUrl { get; set; } = "https://api.anthropic.com/v1";
}
```

**There is no `Model` property in this class at all.** Line 23 is
`MaxTokens`, not a model default. The `Model` property the claim describes
was removed in the §5.28/Option-B cleanup (confirmed by BACKLOG §5.28's own
change log: "Removed C# default model values from `ClaudeSettings`...").

## 2. TranslationValidationSettings.cs — exists, does not match claim

Path: `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Configuration/TranslationValidationSettings.cs`
(143 lines total)

Line 74 is inside the XML doc comment for `PromptVersion` (`/// Default: v3`).
**There is no `Round3DModel` property anywhere in this file.** It existed
historically (per `docs/phase-5/reports/multi-provider-config-recon.md:238`
and `multi-provider-config-fix.md:63`, both of which document
`Round3DModel = "claude-sonnet-4-20250514"` as a pre-fix default) but was
deleted in the same cleanup that removed `ClaudeSettings.Model`.

`Round1AModel` (paired with Round3DModel in the historical docs) is also
absent from the current file.

The only `Model` property left in this file belongs to the unrelated,
already-`[Obsolete]` `DeepSeekSettings` class (line 141: `"deepseek-chat"`)
— DeepSeek was removed from the pipeline entirely for GDPR reasons (Note 2
in CLAUDE.md) and is irrelevant to the Anthropic claim.

## 3. Actual current model defaults (enumerated)

Only one place in the live C# code hardcodes a model default relevant to
Anthropic, and it isn't the retired one:

| File:Line | Default | Status |
|---|---|---|
| `TranslationValidationSettings.cs:141` | `DeepSeekSettings.Model = "deepseek-chat"` | Obsolete/unused class, non-Anthropic |

Everywhere else, Anthropic/Gemini model identifiers are sourced at runtime
from `IOptions<AIProviderOptions>` (`aiProviders.Value.Anthropic.Models.Sonnet`
/ `.Haiku`), confirmed by grep across 13 service/job call sites (
`ContentTranslationService`, `AiSlideshowGenerationService`,
`RegulatoryScoreService`, `ClaudeTranslationService`, `PreFlightScanService`,
`ClaudeHaikuBackTranslationService`, `ClaudeSonnetBackTranslationService`,
`DialectDetectionService`, `ContentParserService`, `AiSectionGenerationService`,
`AiQuizGenerationService`, `RequirementMappingJob`, `RequirementIngestionJob`,
`LessonGeneratorService`). None of these read a `ClaudeSettings.Model` or
`TranslationValidationSettings.Round3DModel`/`Round1AModel` property, because
those properties no longer exist.

**Configuration values** (`appsettings.json`, both root and Development):

```json
"AIProviders": {
  "Anthropic": { "Models": { "Sonnet": "claude-sonnet-4-5", "Haiku": "claude-haiku-4-5-20251001" } },
  "Gemini": { "Models": { "Flash": "gemini-2.0-flash" } },
  "ElevenLabs": { "Models": { "Transcription": "scribe_v1" } }
}
```

`claude-sonnet-4-5` is the live model everywhere. The old model string
survives only as a **harmless comment string** at `appsettings.json:132`
(a `"_comment"` field on the obsolete `DeepSeek` block, purely descriptive,
not parsed as a value) and in a few doc-comment examples in unrelated
entities (`AiUsageLog.cs`, `AiUsageSummary.cs`, `ProviderResultCache.cs`,
`PipelineChangeRecord.cs`) that use it only as an illustrative `<summary>`
example, not a functional default.

## 4. docs/main-hotfix-anthropic-model-*.md — does not exist

No file matching that glob exists anywhere in the repo. The real,
pre-existing documentation trail for this exact incident lives at:

- `docs/phase-5/reports/anthropic-model-deprecation-recon.md` — original recon that first identified the two defaults now cited (dated to the 2026-06-15/16 incident)
- `docs/phase-5/reports/multi-provider-config-recon.md` — the Option B follow-up recon that flagged the *duplicate source of truth* problem (operational keys vs. `AIProviderOptions`) and proposed removing the C# defaults
- `docs/phase-5/reports/multi-provider-config-fix.md` — the fix report confirming both properties were deleted and replaced with `string.Empty`-then-removed

The outside Claude query appears to have synthesized a plausible-looking
filename (`main-hotfix-anthropic-model-*.md`) rather than citing one of
these three real files. This is the same failure pattern flagged in the
prompt (cf. the earlier hallucinated backslash-route bug) — a fabricated
citation wrapped around a claim that has a kernel of real (but stale)
history behind it.

## 5. Cross-check against AIProviderOptions / prior migration recon

Confirmed consistent with the provider-client-architecture recon's finding
that migration to `AIProviderOptions` is complete: `ClaudeSettings` and
`TranslationValidationSettings` no longer carry `Model`/`Round1AModel`/
`Round3DModel` properties at all — not merely unread defaults. This is the
"properties removed" shape of completion, not "properties kept but unused."
BACKLOG §5.28 explicitly states: *"Removed C# default model values from
`ClaudeSettings`, `TranslationValidationSettings`, `SubtitleProcessingSettings`
— no silent fallback to retired models possible."*

## 6. Dead-code-path question

Not applicable — the properties don't exist to be dead. There is no
lingering default to fall back to, silently or otherwise.

## 7. Was `claude-sonnet-4-20250514` really retired?

Yes, per BACKLOG §5.28: retired by Anthropic effective 2026-06-15 after a
60-day deprecation notice (also known as `claude-sonnet-4-0`). This part of
the outside Claude's framing is accurate as *history* — it just isn't
current-state accurate. This recon did not independently re-verify retirement
status against Anthropic's live API (out of scope for a read-only code
recon), but the incident is corroborated by BACKLOG's dated, detailed
incident write-up plus three internal recon/fix documents, not just a single
unverified claim.

## 8. Environment override masking?

Not relevant — there is no default left to mask. `appsettings.json` and
`appsettings.Development.json` both set `AIProviders:Anthropic:Models:Sonnet`
explicitly to `claude-sonnet-4-5`, and `IValidateOptions<AIProviderOptions>`
with `.ValidateOnStart()` (added in §5.28) makes a missing value a startup
failure rather than a silent fallback.

## Recommended action

**Nothing to fix.** The claim describes a real incident from four weeks
ago that was fixed same-day and then hardened further a week later. No
code changes, test changes, or environment verification are needed on this
front.

One pre-existing, already-tracked, unrelated item worth flagging since it
touches the same area: BACKLOG §5.29 follow-up #2 notes that
`CostEstimationService`'s EUR rate table for `claude-sonnet-4-5` was
inherited from the deprecated model's April-2026 rates and should be
verified against current Anthropic pricing. This is already open in
BACKLOG and out of scope for this recon.

## Size estimate

N/A — no fix recommended.
