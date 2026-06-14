# Strict Review Workflow Port — Implementation Report

**Branch:** `transval`
**Date:** 2026-06-14

---

## Test Added During Commit-Prep Verification

During verification of the strict-review gate (tests 9–11 added in a prior session), a gap was identified: there was no test covering the invariant that the `TranslationValidationJob` auto-accept guard does **not** overwrite an existing user reviewer decision when re-validation changes a section's outcome to Pass.

The guard in `TranslationValidationJob.ExecuteAsync` (lines 196–203):

```csharp
if (result.Outcome == ValidationOutcome.Pass
    && result.ReviewerDecision == Domain.Enums.ReviewerDecision.Pending)
{
    result.ReviewerDecision = Domain.Enums.ReviewerDecision.Accepted;
    result.DecisionAt = DateTime.UtcNow;
    result.DecisionBy = "System";
    await _dbContext.SaveChangesAsync(cancellationToken);
}
```

This guard only fires when `ReviewerDecision == Pending`. But without a test triggering the job with a non-Pending decision, the guard was untested.

---

## Authorized Scope Expansion

The test required calling `TranslationValidationJob.ExecuteAsync` directly, which in turn calls `ITranslationValidationService.ValidateSectionAsync`. The real service calls external translation APIs (Claude Haiku, DeepL, Gemini) which are unavailable in the test environment.

**Authorized additions (test infrastructure only):**

1. **New file:** `tests/QuantumBuild.Tests.Integration/Setup/Fakes/FakeTranslationValidationService.cs`
2. **Modified:** `tests/QuantumBuild.Tests.Integration/Fixtures/CustomWebApplicationFactory.cs` — registered the fake via `services.RemoveAll<ITranslationValidationService>()` + `services.AddScoped<ITranslationValidationService, FakeTranslationValidationService>()`

No production code was changed.

---

## Fake Service Design

`FakeTranslationValidationService` mirrors the real service's upsert pattern:

- Looks up existing entity by `{ValidationRunId, SectionIndex}` using `IgnoreQueryFilters()`
- Only sets `ReviewerDecision = Pending` and `EditedTranslation = null` on **new** entities (`Id == Guid.Empty`) — matching the real service's guard at lines 275–279 of `TranslationValidationService.cs`
- Returns deterministic `Outcome = Pass, FinalScore = 95` for any section

**Explicitly not simulated:**

- Real translation API calls (Claude Haiku, DeepL, Gemini)
- Glossary replacement / correction logic
- Safety-critical threshold bumping
- Multi-round consensus iteration
- Artefact scan / registry violations / word diff

---

## Test: `ReValidation_PreservesUserDecisionWhenOutcomeChangesToPass`

**File:** `tests/QuantumBuild.Tests.Integration/ToolboxTalks/PublishToolboxTalkTests.cs`

**Scenario:**
1. Seed a talk with one section and a French translation
2. Seed a validation run with a `Review`-outcome result that has `ReviewerDecision = Edited`, `DecisionBy = "test-reviewer"`, and `EditedTranslation` set
3. Invoke `TranslationValidationJob.ExecuteAsync` directly for section 0 — fake returns `Pass`
4. Assert all five reviewer decision fields are unchanged:
   - `Outcome == Pass` (fake updated it correctly)
   - `ReviewerDecision == Edited` (not overwritten to `Accepted`)
   - `DecisionAt` within 5 seconds of original
   - `DecisionBy == "test-reviewer"` (not replaced by `"System"`)
   - `EditedTranslation` unchanged

---

## Test Runner Output

```
Passed!  - Failed: 0, Passed: 457, Skipped: 0, Total: 457, Duration: 2 m 54 s
```

Previous count (prior binary): 456. Count after: 457. Increase: +1 (the new test).

All previously-passing tests — including tests 9, 10, 11 (strict-review gate: Review+Pending→409, Review+Accepted→200, Pass+System→200) — remain green.

Notable non-fatal warnings observed in the new test run:
- `PreFlightScanService.CallClaudeAsync` throws (no Claude API key in test environment) — caught by `RunPreFlightScanAsync` try-catch, job continues
- `RecordValidationCompleted` returns failure (workflow state is `Initial`, not `Validating`) — logged as warning, job continues
