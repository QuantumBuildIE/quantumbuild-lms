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

---

## Post-deploy smoke evidence (2026-06-15)

**Environment:** Railway transval (production deploy)
**Browser:** Chrome (smoke session)

### Scenario 1 — Auto-accept Pass end-to-end

**What this verifies:** When validation produces all-Pass outcomes, sections auto-accept and Continue enables without any user action.

**Steps and observations:**
1. Created draft talk with source + one target language (Russian), simple content.
2. Translation and validation completed.
3. Step 6 Validate: both sections showed Pass outcome with "Accepted" badges automatically — no user action taken.
4. Summary bar: "2 passed · 0 for review · 0 failed · Ready to publish".
5. Continue enabled; clicked through to Publish; publish succeeded.

**Key observation:** Auto-accept Pass logic at `TranslationValidationJob.cs:196-203` working as designed. Pass-outcome sections receive `ReviewerDecision = Accepted` with `DecisionBy = "System"` on validation completion.

**Verdict:** ✅ Pass

### Scenario 2 — Strict gate enforces with user resolution

**What this verifies:** When validation produces Review-state sections, Continue is blocked until each section is decided. Accept actions work.

**Setup:** Single target language, content engineered to produce at least one Review outcome.

**Steps and observations:**
1. Created draft talk with source + one target language (Russian).
2. Used Safety Protocol content (confined spaces + chemical spill response).
3. Validation produced 1 Pass, 1 Review outcome.
4. At Step 6: Review section had no decision badge; Pass section had Accepted badge; Continue was disabled. ✅ Gate enforcing correctly.
5. Clicked Accept on Review section. Badge changed to "Accepted".
6. **Bug surfaced:** Continue remained disabled despite all sections now Accepted. Summary bar showed correct counts ("1 passed · 1 for review · 0 failed") but gate failed.
7. Root cause traced via DevTools Network tab: section decision mutations refetched the run detail query but did not invalidate the `validationRuns` list query that feeds the navigation gate via the `hasPendingDecisions` derived field.
8. Fix applied (see follow-up commit): `useSectionDecision` and `useSessionSectionDecision` now invalidate `contentCreationKeys.validationRuns(talkId)` on success.
9. Re-tested after fix deploy: Accept enabled Continue immediately, no page refresh required.

**Key observation:** Strict gate correctly blocks until all non-Pass sections have decisions. Fix to mutation cache invalidation was load-bearing for the gate to function in practice.

**Verdict:** ✅ Pass (after follow-up fix)

### Scenario 3 — Tab strip multi-language

**What this verifies:** Switching languages via tabs changes which sections render; amber dots on tabs correctly indicate which languages have pending decisions.

**Steps and observations:**
1. Created draft talk with source + three target languages (RU, AF, French).
2. Used Equipment Maintenance content (3 sections).
3. After all validation runs completed, Step 6 rendered tab strip with three language buttons.
4. RU tab showed amber dot (1 Pass, 2 Review for that language). AF and French tabs showed no dots (all Pass auto-accepted for those languages).
5. Switching tabs correctly updated the section list shown below.
6. Accepted both Review-state sections on RU tab. Amber dot disappeared once all RU sections decided.
7. Summary bar reflected aggregate state across all languages correctly.
8. Continue enabled once all languages clean across all tabs; Publish succeeded.

**Key observation:** Tab strip with per-language pending indicators works. The cache invalidation fix from Scenario 2 also propagates to the tab strip's amber dot computation, since both read from the same `validationRuns` list query.

**Verdict:** ✅ Pass

### Summary

| Scenario | Verifies | Verdict |
|---|---|---|
| 1 | Auto-accept Pass end-to-end | ✅ |
| 2 | Strict gate enforces with user resolution | ✅ (after cache invalidation fix) |
| 3 | Tab strip with amber dots clearing on resolution | ✅ |

**Bug found and fixed during smoke:** Section decision mutations did not invalidate the `validationRuns` list query. Caused Continue button and amber dots to stay stale until manual page reload. One-file fix (six lines) applied and verified.

**BACKLOG §23 closed by passing smoke.**
