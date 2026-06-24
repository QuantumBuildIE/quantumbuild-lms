# `GenerateContentTranslations` Data Contract Recon

**Date:** 2026-06-24  
**Status:** Read-only investigation  
**Scope:** Confirm root causes of the two UI bugs reported for the old-wizard `TranslationWorkflowPanel` and Detail-page `TranslateStep` after translating a new language via the Edit page.

---

## 1. One-Line Summary

`GenerateContentTranslationsCommandHandler` writes a `ToolboxTalkTranslation` row and workflow events, but never appends the new language code to `talk.TargetLanguageCodes`; the `workflow-state` endpoint reads ONLY `TargetLanguageCodes` to decide which states to return, so any language translated outside that list is invisible to both UI surfaces.

---

## 2. Handler Behaviour Audit

**File:** `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/GenerateContentTranslations/GenerateContentTranslationsCommandHandler.cs`

**Invocation path:** `POST /api/toolbox-talks/{id}/translations/generate` → controller → `_mediator.Send(command)` **directly** (synchronous HTTP, not Hangfire). `TriggeredBy` defaults to `TriggeredByType.User`.

### `Handle` method — step by step

| Step | Reads | Writes / Side-effects | Save? |
|---|---|---|---|
| 1. Load talk | `ToolboxTalk` + `Sections`, `Questions`, `Translations`, `Slides`, `SlideshowTranslations` via `IgnoreQueryFilters()` | — | — |
| 2. Resolve source language name | `ILanguageCodeService` | — | — |
| **Per-language loop** | | | |
| 3. Resolve target code | `ILanguageCodeService.GetLanguageCodeAsync(language)` | — | — |
| 4. Skip if target == source | — | — | — |
| 5. `_workflowService.StartTranslation(talkId, targetCode, confirmOverwrite, TriggeredBy.User)` | `WorkflowEvents` table (via `GetState`) | Writes `WorkflowEvent { EventType = TranslationStarted }` with `TenantId = currentUser.TenantId` | Yes — internal `SaveChangesAsync` |
| 6. `TranslateForLanguageAsync(...)` | `ToolboxTalkTranslation` (existing or null), `IContentTranslationService` | Creates/updates `ToolboxTalkTranslation` with: `TranslatedTitle`, `TranslatedDescription`, `TranslatedSections` (JSON), `TranslatedQuestions` (JSON), `TranslatedAt`, `TranslationProvider = "Claude"`, `EmailSubject`, `EmailBody`; adds `ToolboxTalkSlideTranslation` and `ToolboxTalkSlideshowTranslation` entities to DbSet | **No** — deferred |
| **End of loop** | | | |
| 7. `_context.SaveChangesAsync` | — | Persists all translation entities accumulated in the loop | **Yes** |
| 8. `_workflowService.RecordTranslationCompleted(talkId, languageCode, TriggeredBy.User)` (no `explicitTenantId`) | `WorkflowEvents` (via `GetState`) | Writes `WorkflowEvent { EventType = TranslationCompleted }` with `TenantId = currentUser.TenantId` (correct — HTTP context) | Yes — internal `SaveChangesAsync` |
| 9. `_notificationService.NotifyTranslationCompleteAsync` | — | Sends completion email | — |
| 10. Return `SuccessResult(results)` | — | — | — |

### Critical omissions

- **`talk.TargetLanguageCodes` is never read, never written.** The handler does not append the new language code to this field.
- **No `TranslationValidationRun` is created.** Back-translation validation is not triggered.
- **No `TranslationValidationJob` is enqueued.** State never advances past `AIGenerated`.

---

## 3. `TranslationWorkflowPanel` Data Contract

**File:** `web/src/features/toolbox-talks/components/TranslationWorkflowPanel.tsx`

### Data sources

| Data | Source hook / prop | Query key |
|---|---|---|
| Language rows — existing translations | `existingTranslations` prop (= `talk.translations` from parent) | `['toolbox-talks', talkId]` |
| Language rows — employee languages not yet translated | `useAvailableLanguages()` | separate key |
| Per-language state, timestamps, validation outcome | `useWorkflowStates(toolboxTalkId)` | `['toolbox-talks', talkId, 'workflow-state']` |

### Row construction (two loops)

```
// Loop 1 — translations that exist
for t in existingTranslations:
    row.state = stateByCode.get(t.languageCode)?.state ?? 'Initial'

// Loop 2 — employee languages with NO translation row yet
for lang in employeeLanguages where lang.code not in seen:
    row.state = 'Initial'   ← hardcoded, never reads workflow state
```

### Per-field rendering

| UI element | Derived from | Affected field(s) |
|---|---|---|
| Language code badge | `row.languageCode` from `existingTranslations` or `employeeLanguages` | `ToolboxTalkTranslation.LanguageCode` |
| `WorkflowStateBadge` | `row.state` = `stateByCode.get(row.languageCode)?.state ?? 'Initial'` | `WorkflowEvent.EventType` (via workflow-state endpoint) |
| Validation outcome pill (Pass/Review/Fail) | `dto?.lastValidationOutcome` | `TranslationValidationRun.OverallOutcome` (only when `Status == Completed`) |
| Timestamp | `dto?.lastEventAt` | `WorkflowEvent.OccurredAt` (latest event) |
| Flagged word count | `dto?.flaggedWordCount` | Computed from `TranslationValidationResult.Flags` |
| "Translate" button enabled | `isTranslateButtonEnabled(row.state)` → `state === 'Initial' \| 'Stale' \| 'Accepted'` | `row.state` |
| "Validate" button enabled | `canValidate(row.state)` → `state === 'AIGenerated'` | `row.state` |
| "Review" button enabled | `canReview(row.state)` → `state === 'Validated' \| 'ReviewerAccepted' \| 'ThirdPartyReviewed'` | `row.state` |
| "Send for review" button visible | `canSendForExternalReview(row.state)` → `state === 'Validated' \| 'ReviewerAccepted'` | `row.state` |

### The Polish "Validated / Fail / 23 Jun / Send for review" example confirmed

Polish was run through the new wizard (`StartTalkTranslation` → `TranslationValidationJob`). The job:
- Calls `RecordTranslationCompleted` → state `AIGenerated`
- Runs back-translation → calls `RecordValidationCompleted` → state `Validated`
- The `Fail` outcome pill comes from `TranslationValidationRun.OverallOutcome = Fail` (status `Completed`)
- The timestamp is the `ValidationCompleted` WorkflowEvent's `OccurredAt`
- "Send for review" is visible because state = `Validated`

---

## 4. `TranslateStep` (Detail Page) Data Contract

**Files:**
- `web/src/features/toolbox-talks/components/learning-wizard/steps/TranslateStep.tsx`
- `web/src/features/toolbox-talks/components/learning-wizard/hooks/useTalk.ts`

### Data sources

| Data | Source hook | Query key |
|---|---|---|
| Talk object (incl. `targetLanguageCodes`) | `useTalk(talkId)` | `['learnings', talkId]` ← different root from `TOOLBOX_TALKS_KEY` |
| Per-language state, active run IDs | `useWorkflowSubscription(talkId)` → internally `useWorkflowStates` | `['toolbox-talks', talkId, 'workflow-state']` |

### Language list derivation

```tsx
const languages = parseLanguageCodes(talk?.targetLanguageCodes ?? null);
// Returns string[] of ISO codes from the JSON field
```

If a language code is absent from `talk.targetLanguageCodes`, the row is simply not rendered — there is no fallback to `employeeLanguages`.

### Button enablement

```tsx
function canStart(state): boolean {
  return state === 'AIGenerated' || state === 'Initial' || state === 'Stale';
}
```

"Start" (= start translation+validation job) is enabled from those three states. Note: `Validated` is NOT startable — the Detail page assumes the new-wizard `TranslationValidationJob` flow.

---

## 5. Field-by-Field Gap Analysis

Fields that drive UI state in either surface, checked against what each handler writes:

| Field | Read by `TranslationWorkflowPanel` | Read by `TranslateStep` | Written by `GenerateContentTranslations` | Written by `StartTalkTranslation` / `TranslationValidationJob` |
|---|---|---|---|---|
| `ToolboxTalk.TargetLanguageCodes` | Indirectly — `workflow-state` endpoint reads it to decide which codes to return states for | Directly — `parseLanguageCodes(talk?.targetLanguageCodes)` drives the language list | **NOT written** ← PRIMARY BUG | Written by `addTargetLanguage` API (separate step before `StartTalkTranslation`) |
| `ToolboxTalkTranslation.LanguageCode` | Yes — determines which loop a row goes into | No (not loaded here) | Written ✓ | Written by `TranslationValidationJob` ✓ |
| `ToolboxTalkTranslation.TranslatedTitle` | Shown in `WorkflowStateBadge` tooltip via `dto.translatedTitle` | No | Written ✓ | Written by `TranslationValidationJob` ✓ |
| `ToolboxTalkTranslation.TranslatedAt` | Exposed as `dto.translatedAt` | No | Written ✓ | Written by `TranslationValidationJob` ✓ |
| `ToolboxTalkTranslation.NeedsRevalidation` | `dto.needsRevalidation` (in `WorkflowStateDto`) | No | Not written (defaults to `false`) | Not written by job; set by edit-propagation path |
| `WorkflowEvent (TranslationStarted)` | Yes — drives state = `Translating` | Yes | Written ✓ (via `StartTranslation`) | Written ✓ |
| `WorkflowEvent (TranslationCompleted)` | Yes — drives state = `AIGenerated` | Yes | Written ✓ (via `RecordTranslationCompleted`) | Written ✓ by job with `explicitTenantId` |
| `WorkflowEvent (ValidationStarted/Completed)` | Yes — drives state = `Validating` / `Validated` | Yes | **NOT written** | Written ✓ by `TranslationValidationJob` |
| `TranslationValidationRun` (any) | `dto.lastValidationOutcome`, `dto.lastValidationRunId` | `activeRunIds` (for SignalR subscriptions) | **NOT created** | Created by `StartTalkTranslationCommandHandler` ✓ |
| `TranslationValidationResult` rows | `dto.flaggedWordCount` (aggregated) | No | **NOT created** | Created by `TranslationValidationJob` ✓ |

### The gap that causes both symptoms

`TargetLanguageCodes` is never written by `GenerateContentTranslations`. This single gap produces both reported symptoms:

1. **`TranslationWorkflowPanel` shows `Initial`:** The `workflow-state` endpoint iterates ONLY over codes in `TargetLanguageCodes`. Russian is absent from `TargetLanguageCodes` → endpoint doesn't return a state for Russian → `stateByCode.get('ru')` is `undefined` → falls back to `'Initial'` → "Translate" button enabled.

2. **`TranslateStep` doesn't show the language at all:** `parseLanguageCodes(talk?.targetLanguageCodes)` returns only the codes already in `TargetLanguageCodes`. Russian absent → no row rendered.

---

## 6. State Transitions and Downstream Effects

### Old-wizard path (`GenerateContentTranslations`)

```
(initial)
    → StartTranslation()       → WorkflowEvent: TranslationStarted  → state: Translating
    → TranslateForLanguageAsync → ToolboxTalkTranslation row created/updated
    → SaveChangesAsync
    → RecordTranslationCompleted → WorkflowEvent: TranslationCompleted → state: AIGenerated
    → NotifyTranslationCompleteAsync → email sent
```

Final state: **AIGenerated**  
"Validate" button enabled; "Review"/"Send for review" NOT enabled.  
BUT: because `TargetLanguageCodes` is not updated, the `workflow-state` endpoint never returns this state → panel shows `Initial` instead.

### New-wizard path (`StartTalkTranslation` + `TranslationValidationJob`)

```
(precondition: language code already in TargetLanguageCodes via addTargetLanguage)
    → StartTranslation()       → WorkflowEvent: TranslationStarted  → state: Translating
    → SaveChangesAsync
    → TranslationValidationRun created (Status: Pending)
    → TranslationValidationJob enqueued
    
    [Job executes — with explicitTenantId throughout]
    → Translate all content     → ToolboxTalkTranslation row created/updated
    → RecordTranslationCompleted (explicitTenantId) → state: AIGenerated
    → Back-translate per section (multi-provider consensus)
    → RecordValidationCompleted (explicitTenantId) → state: Validated
    → TranslationValidationRun.Status = Completed, OverallOutcome set
    → TranslationValidationResult rows created
```

Final state: **Validated**  
"Review" and "Send for review" buttons enabled.

### `MissingTranslationsJob` path (background)

This job also calls `GenerateContentTranslationsCommand` (with `TriggeredBy = System`) from a Hangfire context. In that context, `currentUser.TenantId = Guid.Empty`. The workflow events written by `StartTranslation` and `RecordTranslationCompleted` (both without `explicitTenantId`) get `TenantId = Guid.Empty`. These events are invisible to the `workflow-state` endpoint (which queries with the real `TenantId`). The `TargetLanguageCodes` gap exists here too.

---

## 7. Cache Invalidation Audit

### `useGenerateContentTranslations.onSuccess` invalidates:

```ts
queryClient.invalidateQueries({ queryKey: ['toolbox-talks', toolboxTalkId] });
queryClient.invalidateQueries({ queryKey: ['toolbox-talks', toolboxTalkId, 'translations'] });
queryClient.invalidateQueries({ queryKey: ['toolbox-talks', toolboxTalkId, 'workflow-state'] });
```

### Coverage analysis

| Query key | Affected surface | Invalidated? |
|---|---|---|
| `['toolbox-talks', talkId]` — talk object incl. translations | `TranslationWorkflowPanel` receives `talk.translations` from this | Yes ✓ |
| `['toolbox-talks', talkId, 'translations']` — separate translations endpoint | `useContentTranslations` hook (if used) | Yes ✓ (defensive) |
| `['toolbox-talks', talkId, 'workflow-state']` | `TranslationWorkflowPanel` via `useWorkflowStates` | Yes ✓ |
| `['learnings', talkId]` — `useTalk` in Detail page | `TranslateStep` language list | **NOT invalidated** ← secondary gap |

### Net result of gaps

Even if the backend bug (missing `TargetLanguageCodes` write) is fixed:
- The `TranslationWorkflowPanel` would show the correct state after mutation `onSuccess` (workflow-state refetch would return the new code).
- The `TranslateStep` would NOT update until the user reloads the page, because `['learnings', talkId]` is never invalidated by this mutation.

---

## 8. New-Wizard Path Comparison

What `StartTalkTranslationCommandHandler` does that `GenerateContentTranslations` doesn't:

| Behaviour | `StartTalkTranslation` | `GenerateContentTranslations` |
|---|---|---|
| Guard: language must be in `TargetLanguageCodes` | Yes — returns `WorkflowInvalidState` if not | No guard; proceeds regardless |
| Update `TargetLanguageCodes` | Not needed — caller (`addTargetLanguage`) does it as a separate API call before this command | **Never done** |
| Create `TranslationValidationRun` | Yes | No |
| Enqueue `TranslationValidationJob` | Yes — job does translate + validate | No |
| `RecordTranslationCompleted` | Done by job, with `explicitTenantId` | Done inline, without `explicitTenantId` (correct in HTTP context, wrong in Hangfire context) |
| `RecordValidationCompleted` | Done by job, with `explicitTenantId` | Never called |
| Final workflow state | `Validated` | `AIGenerated` (theoretically; `Initial` in practice due to `TargetLanguageCodes` gap) |
| Translation content written | Yes (by job) | Yes (by handler synchronously) |

The key architectural difference: `StartTalkTranslation` is intentionally a thin dispatcher that assumes the language is already registered in `TargetLanguageCodes`. The old-wizard path (`GenerateContentTranslations`) is a self-contained translate-and-done handler that was never updated to maintain `TargetLanguageCodes` as a side effect.

The `TranslationValidationJob` being the one that reads `TargetLanguageCodes` would be correct IF `GenerateContentTranslations` also kept that field up to date — it doesn't.

---

## 9. Fix Shape Recommendation

### Shape: Small backend + small frontend

**Confidence: High.** The root cause is a single missing write; everything else flows correctly once that field is maintained.

#### Fix A — Backend (required, primary): Append to `TargetLanguageCodes` on success

In `GenerateContentTranslationsCommandHandler.Handle`, after the per-language loop and BEFORE the single `SaveChangesAsync`, accumulate all successfully translated codes and append any that are not already in `toolboxTalk.TargetLanguageCodes`:

```csharp
// Before SaveChangesAsync:
var existingCodes = string.IsNullOrWhiteSpace(toolboxTalk.TargetLanguageCodes)
    ? new List<string>()
    : JsonSerializer.Deserialize<List<string>>(toolboxTalk.TargetLanguageCodes) ?? new List<string>();

var successfulCodes = results.Where(r => r.Success).Select(r => r.LanguageCode);
var changed = false;
foreach (var code in successfulCodes)
{
    if (!existingCodes.Contains(code, StringComparer.OrdinalIgnoreCase))
    {
        existingCodes.Add(code);
        changed = true;
    }
}
if (changed)
    toolboxTalk.TargetLanguageCodes = JsonSerializer.Serialize(existingCodes);

await _context.SaveChangesAsync(cancellationToken);
```

The `toolboxTalk` entity is already in the change tracker (loaded at the top of `Handle`), so no extra `SaveChanges` call is needed.

**Effect:** After the fix, `workflow-state` returns the correct state (`AIGenerated`) for the new language, and `TranslateStep` includes the language in its list.

#### Fix B — Frontend (required, secondary): Invalidate `['learnings', talkId]`

In `use-toolbox-talks.ts`, `useGenerateContentTranslations.onSuccess`:

```ts
onSuccess: (_, { toolboxTalkId }) => {
  queryClient.invalidateQueries({ queryKey: [...TOOLBOX_TALKS_KEY, toolboxTalkId] });
  queryClient.invalidateQueries({ queryKey: [...TOOLBOX_TALKS_KEY, toolboxTalkId, 'translations'] });
  queryClient.invalidateQueries({ queryKey: [...TOOLBOX_TALKS_KEY, toolboxTalkId, 'workflow-state'] });
  queryClient.invalidateQueries({ queryKey: ['learnings', toolboxTalkId] }); // ← add this
},
```

**Effect:** The Detail page's `TranslateStep` sees the updated `targetLanguageCodes` immediately after the mutation, without requiring a page reload.

#### Fix C — Backend (recommended): Fix `MissingTranslationsJob` workflow event TenantId

When `MissingTranslationsJob` calls `GenerateContentTranslationsCommand`, it doesn't pass `explicitTenantId` through to the workflow service calls. Both `StartTranslation` and `RecordTranslationCompleted` will write workflow events with `TenantId = Guid.Empty` (because `ICurrentUserService.TenantId` is empty in Hangfire context). Those events are invisible to the tenant-scoped `workflow-state` query.

The fix requires adding `ExplicitTenantId` to `GenerateContentTranslationsCommand` and threading it through to the `_workflowService.StartTranslation(...)` and `_workflowService.RecordTranslationCompleted(...)` calls. The `MissingTranslationsJob` would pass the job-parameter `tenantId`.

This is separate from the user-reported bug (which is the HTTP path), but should be fixed in the same pass to prevent the same symptom from recurring on background-triggered translations.

---

## 10. Risks and Edge Cases

### Partial success

`GenerateContentTranslations` translates multiple languages in one request. If Russian succeeds but German fails, only Russian should be appended to `TargetLanguageCodes`. Fix A correctly filters by `results.Where(r => r.Success)`.

### Language already in `TargetLanguageCodes`

The existing translations are re-translations (overwrite). The fix must not duplicate the code. The `Contains` check in Fix A handles this.

### `MissingTranslationsJob` and Fix A

`MissingTranslationsJob` also calls `GenerateContentTranslationsCommand`. Fix A would cause the job to also append the code to `TargetLanguageCodes`. This is correct behaviour — if the job detects a missing translation for an employee language and creates it, that language should become a registered target language. This is actually the desired state.

### `StartTalkTranslation` guard compatibility

`StartTalkTranslationCommandHandler` guards on `IsLanguageInTargets(talk.TargetLanguageCodes, request.LanguageCode)`. After Fix A, any language translated via the old-wizard path is added to `TargetLanguageCodes`, making it eligible for the new-wizard path's `StartTalkTranslation` flow. This is correct.

### State after fix: `AIGenerated` not `Validated`

After Fix A, the Edit panel will show state `AIGenerated` (Validate button available), not `Validated` (Review/Send-for-review). This is correct for the old-wizard path, which does not include back-translation validation. The user must click "Validate" to run the validation step. This matches the intended workflow for the old wizard.

### Concurrency

If two concurrent HTTP requests translate different languages for the same talk simultaneously, both will read `TargetLanguageCodes` at the start of `Handle` (the entity is loaded once and cached in the DbContext for the request lifetime). Each request writes its own codes. Since each request has its own scoped DbContext, there is a last-writer-wins race for `TargetLanguageCodes`. This is the same race that already exists for any multi-language batch; it's low-risk in practice since single-language requests from the panel are the common path.

### Backward compatibility

Talks that were translated via the old wizard before this fix will have `ToolboxTalkTranslation` rows but missing codes in `TargetLanguageCodes`. Those talks remain broken on the Detail page until their `TargetLanguageCodes` is corrected. A one-time data repair migration (or background job) would be needed to backfill `TargetLanguageCodes` from existing `ToolboxTalkTranslation` rows for affected tenants. This is out of scope for the fix itself but should be noted for production rollout.
