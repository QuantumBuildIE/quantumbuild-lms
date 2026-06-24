# Translation Validation Workflow Not Closing — Recon

**Date:** 2026-06-24
**Branch:** transval
**Scope:** Read-only investigation. No application files modified.

---

## 1. One-line summary

The root cause is **not outcome-specific**. `RecordValidationCompleted` is called inside a guard that requires source state `Validating` or `AIGenerated`, but on re-runs (where translations pre-exist from a prior job), state is still `Translating` when that call fires — so it fails silently, no `ValidationCompleted` event is written, and the state machine stays stuck. The correlation with `Fail` outcomes is coincidental: re-runs happen because a previous run produced `Fail`.

**Fix shape:** Call `RecordTranslationCompleted` unconditionally before `StartValidation()` when state is `Translating`, so the state machine advances to `AIGenerated` → `Validating` → `Validated` on every run, regardless of whether translations were generated inline or pre-existed.

**Affected outcomes:** All — `Fail`, `Review`, and `Pass` are equally vulnerable on re-runs. The observed pattern (only `Fail` stuck) is because `Fail` runs are the ones users re-run.

---

## 2. Pipeline trace — TranslationValidationJob from start to finish

File: `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Infrastructure/Jobs/TranslationValidationJob.cs`

### Job entry

`StartTalkTranslationCommandHandler` creates a `TranslationValidationRun` with `IsNewWizard = true` and calls `_workflow.StartTranslation()` which writes the `TranslationStarted` event — state becomes `Translating`. It then enqueues `TranslationValidationJob.ExecuteAsync(validationRunId, tenantId)`.

### Phase A — Section loading (line 138)

```
sections = await LoadSectionsAsync(run, tenantId, cancellationToken)
```

`LoadSectionsAsync` (line 544) checks whether a `ToolboxTalkTranslation` row already exists for the talk+language combination:

- **Translation missing:** calls `GenerateTranslationForSectionsAsync(...)`. That method, if `isNewWizard == true`, calls `_workflowService.RecordTranslationCompleted(...)` at line 1102 → writes `TranslationCompleted` → **state advances to `AIGenerated`**.
- **Translation exists:** returns the cached `TranslatedSections` JSON directly. `GenerateTranslationForSectionsAsync` is **not called**. `RecordTranslationCompleted` is **not called**. **State stays at `Translating`.**

### Phase B — StartValidation (lines 162–180)

```csharp
if (run.ToolboxTalkId.HasValue && run.IsNewWizard)
{
    var startValidationResult = await _workflowService.StartValidation(
        run.ToolboxTalkId.Value, run.LanguageCode,
        explicitTenantId: tenantId, ct: cancellationToken);

    if (!startValidationResult.Success)
        _logger.LogWarning(...); // silent failure — job continues
}
```

`StartValidation` guard (line 280 of `TranslationWorkflowService.cs`):
```csharp
if (state is not (TranslationWorkflowState.AIGenerated or TranslationWorkflowState.Validating))
    return Result.Fail(..., FailureCode.WorkflowInvalidState);
```

- **First run (translation generated inline):** state is `AIGenerated` → guard passes → writes `ValidationStarted` → state = `Validating`. ✓
- **Re-run (translation pre-existed):** state is `Translating` → guard **fails** → returns `Result.Fail` → **logged as warning, not thrown** → state stays `Translating`. ✗

### Phase C — Section validation loop (lines 188–364)

Sections are validated regardless of workflow state. This phase runs correctly in all scenarios.

### Phase D — Aggregate results and closing event (lines 367–431)

```csharp
run.OverallOutcome = DetermineOverallOutcome(run);  // Pass / Review / Fail
run.Status = ValidationRunStatus.Completed;
run.CompletedAt = DateTime.UtcNow;
await _dbContext.SaveChangesAsync(cancellationToken); // line 381

if (run.ToolboxTalkId.HasValue)
{
    var wfResult = await _workflowService.RecordValidationCompleted(
        run.ToolboxTalkId.Value, run.LanguageCode, TriggeredByType.System,
        explicitTenantId: tenantId, ct: cancellationToken);

    if (!wfResult.Success)
        _logger.LogWarning(...); // silent failure — job continues
}
```

`RecordValidationCompleted` guard (line 252 of `TranslationWorkflowService.cs`):
```csharp
if (state is not (TranslationWorkflowState.Validating or TranslationWorkflowState.AIGenerated))
    return Result.Fail(..., FailureCode.WorkflowInvalidState);
```

- **First run:** state is `Validating` (set in Phase B) → guard passes → writes `ValidationCompleted` → state = `Validated`. ✓
- **Re-run:** state is still `Translating` (Phase B failed) → guard **fails** → returns `Result.Fail` → **logged as warning, not thrown** → **no `ValidationCompleted` event written** → **state stays `Translating`**. ✗

The run row is correctly marked `Status = Completed` and `OverallOutcome = Fail|Review|Pass` — that data is fine. Only the workflow event is missing.

### Exception path (line 447) and Cancellation path (line 433)

Both catch blocks call `UpdateRunStatusAsync(...)` to mark the run `Failed` or `Cancelled` on the DB row, but **neither calls `RecordValidationCompleted` or any other workflow service method**. If an exception fires mid-validation, the state machine also sticks. This is a secondary issue — real exceptions cause Hangfire's `[AutomaticRetry(Attempts = 1)]` to re-run the job, which then hits the re-run path described above.

---

## 3. Closing-event audit

### `RecordTranslationCompleted`

Written by:
1. `GenerateContentTranslationsCommandHandler.Handle()` (line 196) — old-wizard sync path
2. `TranslationValidationJob.GenerateTranslationForSectionsAsync()` (line 1102) — called only when translations are absent

Event written: `WorkflowEventTypes.TranslationCompleted` → state = `AIGenerated`

Guard: source state must be `Translating`. This will succeed on a re-run because state IS `Translating` — the problem is it is never called.

### `RecordValidationCompleted`

Written by:
- `TranslationValidationJob.ExecuteAsync()` (line 386) — **only call site in the entire codebase**

Event written: `WorkflowEventTypes.ValidationCompleted` → state = `Validated`

Guard: source state must be `Validating` or `AIGenerated`. Fails silently if state is `Translating`.

**No `TranslationFailed` or `ValidationFailed` event types exist.** Failure is recorded only on the run row, not in the event log. The state machine has no failure terminal state.

---

## 4. Outcome-dependent code paths

`DetermineOverallOutcome(run)` at line 376 determines the outcome from section scores. This runs identically for all outcomes — there is **no outcome-conditional guard** anywhere in the closing workflow event path. The `RecordValidationCompleted` call at line 386 is unconditional with respect to outcome.

The outcome-by-outcome matrix:

| OverallOutcome | Translation pre-existed? | StartValidation succeeds? | RecordValidationCompleted succeeds? | Closing event written | Workflow state |
|---|---|---|---|---|---|
| Any (Pass/Review/Fail) | No — first run | Yes (state=AIGenerated) | Yes (state=Validating) | `ValidationCompleted` ✓ | `Validated` |
| Any (Pass/Review/Fail) | Yes — re-run | No (state=Translating) | No (state=Translating) | None ✗ | `Translating` (stuck) |
| (exception mid-run) | Either | Depends | Not reached | None ✗ | `Translating` or `Validating` |

The "Fail only" pattern observed in production is because re-runs only happen after a `Fail` result (users re-trigger validation when they see a failure). `Review` and `Pass` on re-runs would be equally stuck — there are just no observed re-runs with those outcomes in the sample data.

---

## 5. Workflow state model

File: `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Domain/Enums/TranslationWorkflowState.cs`
State machine mapping: `TranslationWorkflowService.EventTypeToState()` (line 813)

| State | Value | Transient? | Notes |
|---|---|---|---|
| `Initial` | 0 | — | No events recorded yet |
| `AIGenerated` | 1 | — | Translation done, awaiting validation |
| `Validated` | 2 | — | Back-translation completed |
| `ReviewerAccepted` | 3 | — | Internal reviewer accepted/edited |
| `AwaitingThirdParty` | 4 | — | External reviewer invited |
| `ThirdPartyReviewed` | 5 | — | External reviewer submitted |
| `Accepted` | 6 | — | Translation marked final |
| `Stale` | 7 | — | Requires re-translation |
| `Translating` | 8 | Yes | Translation job in progress |
| `Validating` | 9 | Yes | Validation job in progress |

Event-to-state map (only relevant transitions shown):

```
TranslationStarted   → Translating
TranslationCompleted → AIGenerated
ValidationStarted    → Validating
ValidationCompleted  → Validated
```

**There is no `TranslationFailed` event and no `ValidationFailed` event.** The state machine cannot express "validation completed with failure" — the intended design is that `ValidationCompleted` is written for all outcomes (Pass/Review/Fail), and callers read the run row's `OverallOutcome` for the actual result. The state machine expresses workflow *progress*, not *quality*.

**The gap:** on re-runs, the job skips the transition `Translating → AIGenerated` (never calls `RecordTranslationCompleted`), which then blocks `StartValidation` (`AIGenerated → Validating`) and consequently `RecordValidationCompleted` (`Validating → Validated`). The machine is stuck in `Translating` — a transient state that should never persist beyond the job's lifetime.

---

## 6. Fix shape recommendation

### Primary fix — advance state before `StartValidation` (line 162–180)

**Location:** `TranslationValidationJob.ExecuteAsync`, in the `IsNewWizard` block just before the `StartValidation` call.

**Problem to solve:** On re-runs, `RecordTranslationCompleted` is never called because translation generation is skipped. State stays `Translating`, causing both `StartValidation` and `RecordValidationCompleted` to fail silently.

**Fix:** Before calling `StartValidation`, check the current workflow state. If state is `Translating`, call `RecordTranslationCompleted` first to advance to `AIGenerated`.

```csharp
if (run.ToolboxTalkId.HasValue && run.IsNewWizard)
{
    // If state is Translating (translations pre-existed; RecordTranslationCompleted was not
    // called inside GenerateTranslationForSectionsAsync), advance to AIGenerated first so
    // StartValidation's guard passes. RecordTranslationCompleted is idempotent here —
    // if state is already AIGenerated (first run), this call is a no-op via the state guard.
    var currentState = await _workflowService.GetState(
        run.ToolboxTalkId.Value, run.LanguageCode,
        explicitTenantId: tenantId, ct: cancellationToken);

    if (currentState.State == TranslationWorkflowState.Translating)
    {
        var tcResult = await _workflowService.RecordTranslationCompleted(
            run.ToolboxTalkId.Value, run.LanguageCode, TriggeredByType.System,
            explicitTenantId: tenantId, ct: cancellationToken);
        if (!tcResult.Success)
            _logger.LogWarning(
                "RecordTranslationCompleted (pre-StartValidation, re-run) returned failure for " +
                "talk {TalkId}, lang {Lang}: {Error}",
                run.ToolboxTalkId, run.LanguageCode, tcResult.Errors.FirstOrDefault());
    }

    var startValidationResult = await _workflowService.StartValidation(
        run.ToolboxTalkId.Value,
        run.LanguageCode,
        explicitTenantId: tenantId,
        ct: cancellationToken);

    if (!startValidationResult.Success)
        _logger.LogWarning(
            "StartValidation returned failure for talk {TalkId}, lang {Lang}: {Error}",
            run.ToolboxTalkId, run.LanguageCode, startValidationResult.Errors.FirstOrDefault());
}
```

After this fix, re-run state sequence:
1. `TranslationStarted` → `Translating` (command handler)
2. Translation pre-exists → `LoadSectionsAsync` skips generation
3. **New:** state is `Translating` → call `RecordTranslationCompleted` → `AIGenerated`
4. `StartValidation` → state is `AIGenerated` → guard passes → `Validating`
5. Section validation runs
6. `RecordValidationCompleted` → state is `Validating` → guard passes → `Validated` ✓

**Confidence:** High. The `RecordTranslationCompleted` guard requires source state `Translating` — which is exactly the state on re-runs — so it will succeed. No state machine modifications required.

**Effort:** ~10 lines of code change in one method.

### Secondary fix — write closing event in exception/cancellation catch blocks

**Location:** catch blocks at lines 433 and 447.

**Problem:** If an actual exception fires mid-validation, the job fails. Hangfire's `[AutomaticRetry(Attempts = 1)]` re-runs it. The re-run hits the primary bug (translations pre-exist). But if retry is exhausted or disabled, state stays stuck.

**Fix:** Add `RecordValidationCompleted` call in each catch block, using the pre-fetched `run` object:

```csharp
catch (OperationCanceledException)
{
    await UpdateRunStatusAsync(validationRunId, tenantId, ValidationRunStatus.Cancelled);
    await SendCompletionAsync(validationRunId, false, "Validation was cancelled");

    // Advance state so the spinner clears even on cancellation
    if (run?.ToolboxTalkId.HasValue == true)
    {
        await _workflowService.RecordValidationCompleted(
            run.ToolboxTalkId.Value, run.LanguageCode, TriggeredByType.System,
            explicitTenantId: tenantId, ct: default); // use default, original token is cancelled
    }
    throw;
}
```

Similar addition in the `catch (Exception ex)` block at line 447, before `throw`.

**Confidence:** Medium. The `RecordValidationCompleted` guard requires `Validating` or `AIGenerated`. If the exception fires before Phase B (`StartValidation`), state may still be `Translating` — the call would fail silently again. The primary fix is required first; this is belt-and-suspenders for the post-StartValidation exception window.

---

## 7. Risks and edge cases

**Duplicate `TranslationCompleted` events on first runs:** On a first run, `GenerateTranslationForSectionsAsync` writes `RecordTranslationCompleted` at line 1102. The proposed fix would then immediately check state — finding it `AIGenerated` (already advanced) — and skip the additional call. No duplicate events. ✓

**`RecordTranslationCompleted` guard rejects non-`Translating` source state:** If state is somehow neither `Translating` nor what we expect (e.g., `AIGenerated` already advanced by translation generation), the call returns `Result.Fail` which is logged as a warning and execution continues. No regression. ✓

**Old-wizard runs (`IsNewWizard = false`):** The entire `if (run.IsNewWizard)` block is skipped. These runs reach `RecordValidationCompleted` with state potentially `Translating` (if the old-wizard's `GenerateContentTranslationsCommandHandler` wrote `TranslationCompleted` but then validation re-started, overwriting state with `TranslationStarted`). The secondary call at line 386 would still fail silently for old-wizard re-runs. However: old-wizard is being deprecated (Note 29), and validating old-wizard talks via the new job is not the primary flow. Low priority.

**Course-scoped runs (`ToolboxTalkId == null`):** `LoadSectionsAsync` returns `[]` immediately (line 549-554). The job exits early at the zero-sections check (line 140-153). No workflow event path is reached. Not affected.

**Event ordering in the log:** The proposed fix writes `TranslationCompleted` after `TranslationStarted` from the command handler, before `ValidationStarted`. This matches the intended sequence and does not confuse the state machine.

---

## 8. What this recon could NOT determine

1. **Whether any production runs are currently stuck.** The query below identifies affected runs by reading the event log. It must be run by an operator on each environment:

```sql
-- Identify talk+language pairs where the last recorded workflow event
-- is a transient "in-progress" event (Translating or Validating),
-- meaning no closing event was ever written.
SELECT DISTINCT
    e."TargetEntityId"    AS "TalkId",
    e."TargetEntitySubKey" AS "LanguageCode",
    e."TenantId",
    e."EventType"         AS "StuckAtEvent",
    e."OccurredAt"        AS "StuckSince"
FROM workflows."WorkflowEvents" e
WHERE e."EventType" IN ('TranslationStarted', 'ValidationStarted')
  AND NOT EXISTS (
    SELECT 1 FROM workflows."WorkflowEvents" e2
    WHERE e2."TargetEntityId"    = e."TargetEntityId"
      AND e2."TargetEntitySubKey" = e."TargetEntitySubKey"
      AND e2."TenantId"          = e."TenantId"
      AND e2."OccurredAt"        > e."OccurredAt"
      AND e2."EventType" IN (
          'TranslationCompleted',
          'ValidationCompleted',
          'InternalReviewSubmitted',
          'ExternalReviewInitiated',
          'ExternalReviewSubmitted',
          'ExternalReviewConfirmed',
          'ExternalReviewRejected',
          'ExternalReviewCancelled',
          'ExternalReviewDeclined',
          'AcceptedAsFinal',
          'MarkedStale'
      )
  )
ORDER BY e."OccurredAt" DESC;
```

2. **Whether the Polish `Review` run's clean closure was due to correct code or a pre-fix state.** The recon established the code path: Polish's clean closure is consistent with it being a first-time run (translations generated inline → `RecordTranslationCompleted` called → state advanced through the correct sequence). If Polish had been a re-run, it would be equally stuck. The outcome (`Review`) is not the discriminating factor.

3. **Whether `GenerateTranslationForSectionsAsync` can fail silently without rolling back `RecordTranslationCompleted`.** If the translation generation partially succeeds (writes the entity, fires the workflow event, then fails on a subsequent step), the event log would show `TranslationCompleted` but `LoadSectionsAsync` might still return empty on a retry. This would then leave state at `AIGenerated` — and `RecordValidationCompleted` would work correctly (`AIGenerated` is a valid source state). This edge case is not a blocker; it would self-heal.

4. **Exact demo-environment impact.** The query in point 1 identifies stuck runs. The manual `INSERT` done during this session for the Russian talk (`08:57:05 TranslationCompleted` manual backfill) has already unstuck that specific run. Other runs that completed after that manual fix and before the code fix may also be stuck.

### Compensating action for demo

For each stuck talk+language pair found by the query, manually insert the closing `ValidationCompleted` event:

```sql
INSERT INTO workflows."WorkflowEvents"
    ("Id", "TenantId", "WorkflowType", "TargetEntityId", "TargetEntitySubKey",
     "EventType", "PayloadJson", "TriggeredBy", "TriggeredByUserId",
     "OccurredAt", "CreatedAt", "IsDeleted")
VALUES
    (gen_random_uuid(),
     '<TenantId>',
     'Translation',
     '<TalkId>',
     '<LanguageCode>',
     'ValidationCompleted',
     NULL,
     'System',
     NULL,
     NOW(),
     NOW(),
     false);
```

Replace `<TenantId>`, `<TalkId>`, `<LanguageCode>` from the diagnostic query results. Run once per stuck pair. Spinner clears on next frontend poll (no server restart required).
