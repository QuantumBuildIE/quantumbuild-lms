# Requirement-mapping parse failure recon

Read-only recon. No code changed. Subject: Production Sentry error
'Failed to parse mapping suggestions from Claude response after retry'
(`RequirementMappingJob`, talk a66c69aa, short hand-washing video, 15 requirements).

All references are against `transval` at HEAD (986887f).
File: `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/RequirementMappingJob.cs` (abbrev. `Job.cs`).

## Headline

**OBSERVED:** `TryParseSuggestions` returns `null` for a valid empty array `[]`
(`Job.cs:333`: `return suggestions?.Count > 0 ? suggestions : null;`). The caller
treats `null` as 'invalid JSON', so a perfectly correct 'nothing applies' answer
triggers the retry, and if Claude answers `[]` again, the `LogError` at `Job.cs:222`
fires (the Sentry event).

**INFERRED:** this is the most likely cause for a short hand-washing video against
15 requirements: Claude legitimately returns `[]` (or a few low-relevance items are
not emitted) on both attempts. Not confirmed, because the raw response is not captured (item 3).

---

## 1. Prompts

### Mapping prompt (OBSERVED, `Job.cs:239-267`)

```
You are a regulatory compliance analyst. You will be given a piece of training content and a list of regulatory requirements.

Your task is to identify which requirements this training content addresses, either fully or partially.

TRAINING CONTENT:
{contentString}

REGULATORY REQUIREMENTS:
{requirementsList}      // per requirement: ID, Title, Description, Section — SectionLabel

For each requirement that this content addresses, provide:
- requirementId: the exact ID from the list
- confidenceScore: 0-100 (how well the content addresses this requirement)
- reasoning: one sentence explaining why this content addresses the requirement

Only include requirements that are genuinely addressed by the content. Do not include requirements where there is no meaningful connection.

Respond ONLY with a valid JSON array. No preamble, no markdown, no explanation outside the JSON.

Example format:
[ { "requirementId": "guid-here", "confidenceScore": 85, "reasoning": "..." } ]

If no requirements are addressed, respond with an empty array: []
```

### Stricter retry prompt (OBSERVED, `Job.cs:216`)

The original prompt plus this suffix:

```
IMPORTANT: Your previous response was not valid JSON. You MUST respond with ONLY a JSON array. No text before or after. No markdown code fences. Just the raw JSON array starting with [ and ending with ].
```

### Does either allow an empty result?

**OBSERVED:** The mapping prompt explicitly allows it (last line: 'If no requirements are addressed, respond with an empty array: []'). The retry prompt does not repeat that, but it says 'ONLY a JSON array', which `[]` satisfies.

**OBSERVED, the conflict:** the prompt tells Claude `[]` is a valid answer; the parser rejects `[]`. The prompt contract and the parser contract disagree.

Other prompt facts (OBSERVED): single user message, no system prompt, no prefill, `max_tokens = 8192` (`Job.cs:25`), model `AIProviders.Anthropic.Models.Sonnet`. The retry is a fresh independent call (not a conversation continuation), so it carries the same inputs and same likelihood of the same answer.

## 2. Parse logic (`Job.cs:315-341`)

**Expected shape (OBSERVED):** `List<MappingSuggestion>` where `MappingSuggestion` = `{ string RequirementId, int ConfidenceScore, string Reasoning }`, camelCase, case-insensitive.

**JSON extraction (OBSERVED):**
1. `null`/whitespace response returns `null`.
2. `Trim()`. If it starts with ```` ``` ````, drop through the first newline, drop a trailing ```` ``` ````, trim.
3. `JsonSerializer.Deserialize<List<MappingSuggestion>>` on the whole remainder.

There is **no** bracket scanning (`[` to `]`). A preamble ('Here are the mappings:'), trailing commentary, a fence with text after the closing fence, or a refusal sentence all fail deserialization.

**What counts as 'invalid' (OBSERVED), all collapse to `null`:**
- Empty/whitespace response.
- Any `JsonException` (preamble, trailing text, truncated output, object instead of array, wrong types such as `confidenceScore` as a float like `85.5` or a string; `int` deserialization fails on `85.5`).
- A valid `[]` or JSON `null` (`Count > 0` check).

Not treated as invalid: items with a bad GUID are skipped later in persist (`Job.cs:361-365`), with a Warning.

**INFERRED:** `max_tokens` truncation is unlikely at 15 requirements (8192 tokens is ample), so truncated JSON is a low-probability cause.

## 3. Is the raw response captured on failure?

**OBSERVED: partially, and not at the point that matters.**
- `TryParseSuggestions` logs a **Warning** with the first 200 chars on `JsonException` only (`Job.cs:337-338`). It logs **nothing** on the empty-array path and nothing on the blank-response path.
- The final `LogError` (`Job.cs:222`) carries **no response text, no talk/course id, no tenant id, no attempt info**. The Sentry event as reported therefore cannot say what Claude returned or which entity failed (the talk id a66c69aa came from elsewhere).
- Warning-level log lines are probably not Sentry events (the bulk-parse commit f633087 had to *raise* a Warning to Error specifically so it would reach Sentry). **INFERRED:** the 200-char preview is likely visible only in Railway logs, if retained.
- `AiUsageLog` stores tokens/model only, not content (`CallClaudeAsync`, `Job.cs:301-310`). Note it does log both calls (usage logged before parse), so billing is recorded, but content is not.

Compared with the bulk-parse diagnostic (f633087, **OBSERVED** from its commit message): that change logs the raw response (truncated), an input sample, the talk id and a source hint, at Error level, on both the zero-sections and JsonException branches. The mapping job has none of this: no talk/course id, no full-ish response, no distinction between the failure modes (empty array vs malformed vs blank).

## 4. State after final failure

- **Talk/course state: untouched.** **OBSERVED:** the job never writes to the talk/course. It writes only `RegulatoryRequirementMappings` rows, and none are written on this path (`MapViaClaudeAsync` returns `null`, then `MapRequirementsAsync` hits `suggestions == null || Count == 0`, logs Information 'Claude returned no mapping suggestions' and returns, `Job.cs:95-99`). Publish status is unaffected.
- **Failure vs empty is indistinguishable downstream (OBSERVED):** a parse failure and a legitimate `[]` both end at the same early return. No marker, flag, or status records 'mapping attempted, result X'.
- **Hangfire retry: effectively none. OBSERVED:** the method carries `[AutomaticRetry(Attempts = 1)]` (`Job.cs:59`), but the whole body is in `try/catch` that swallows exceptions ('Don't rethrow', `Job.cs:111-117`), and the parse failure returns `null` without throwing anyway. Hangfire sees success, so no job-level retry occurs. The only retry is the single in-job re-prompt. (A thrown Claude HTTP error would also be swallowed, though the Polly policy on the HttpClient, `ServiceCollectionExtensions.cs:395-401`, retries transient HTTP errors first.)
- **User told anything: no. OBSERVED:** fire-and-forget; no notification, no UI error state, no SignalR. Only server logs/Sentry.
- **Compliance coverage: treated as unmapped, indistinguishable from 'no match'. OBSERVED/INFERRED:** `GetComplianceChecklistAsync` builds coverage from mapping rows joined to live talks/courses (`RequirementMappingService.cs:234-240`). With no rows, every requirement shows no coverage for this talk (shown as a gap, per `GapCount`, though I did not trace every branch of the gap derivation). Nothing flags that the content was never successfully analysed. For a talk that is genuinely about hand washing, some requirements (infection control) probably *should* map, so a silent miss here produces a false compliance gap.

## 5. Triggers

**OBSERVED:** `BackgroundJob.Enqueue<RequirementMappingJob>` (concrete class, per Note 21) has four call sites, all fire-and-forget and all enqueued once at publish time:

| Site | Context |
|---|---|
| `ToolboxTalksController.cs:691` | `POST /{talkId}/publish` (new wizard), after successful status flip |
| `ContentCreationSessionService.cs:1679` | Legacy wizard `PublishAsync`, publishing the draft talk |
| `ContentCreationSessionService.cs:1777` | Legacy wizard, talk creation path |
| `ContentCreationSessionService.cs:2178` | Legacy wizard, course publish (`courseId` set) |

No trigger on edit, on content change, on a schedule, or on newly added requirements/sectors/frameworks (none found by grep across `src`; the only other references to the job are DI registration and the commit-less doc comment).

**Can the same talk be re-mapped later?**
- **OBSERVED:** No automatic path and no 'Re-run mapping' endpoint or button. Re-enqueue only happens if the talk goes through a publish call again. `PublishToolboxTalk` has a precondition 'not already published' (per the controller doc comment, `ToolboxTalksController.cs:665`), so a published talk cannot be re-published to re-trigger it. **INFERRED** (did not exercise): an admin can only recover by creating **manual** mappings (`POST /manual`, 'Add mapping' dialog in the web app, `AddMappingDialog.tsx`) or by ops enqueueing the job by hand.
- Re-running would be safe: `PersistMappingsAsync` is idempotent (skips existing active mappings, restores soft-deleted ones, `Job.cs:350-414`).

---

## Notable risks

1. **Valid `[]` is reported as a failure (OBSERVED).** Sentry noise, plus a wasted second Sonnet call per legitimately-empty talk (cost logged to `AiUsageLog`, ~2x tokens including the full requirement list).
2. **Diagnosis is currently blind (OBSERVED).** The error carries no talk id, tenant id or response. The talk id for this incident cannot be recovered from the Sentry message itself.
3. **Silent false gaps (OBSERVED + INFERRED).** A real parse failure (preamble, wrong int type) leaves the content unmapped with no flag, no retry, no UI signal, and no re-map path. Compliance and the Training Evidence Pack would show a gap.
4. **Brittle extraction (OBSERVED).** No bracket-scan fallback; `confidenceScore` must be an `int` (a float score fails the entire array).
5. **`[AutomaticRetry(Attempts = 1)]` is dead configuration (OBSERVED)** given the swallow-all catch; it gives a false impression of resilience.
6. **Retry prompt is not conversation-aware (OBSERVED).** Its 'previous response was not valid JSON' claim is false when the previous response was a valid `[]`, and the call is independent so it carries no information from the first response.

Unverified: what Claude actually returned for a66c69aa. Confirming requires either Railway logs for the Warning preview (only emitted on `JsonException`, so its *absence* would itself support the empty-array theory) or the diagnostic capture proposed above.
