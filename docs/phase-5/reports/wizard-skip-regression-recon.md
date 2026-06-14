# Wizard Skip Regression Recon — §22 & §23

**Date:** 2026-06-14  
**Scope:** Read-only investigation. No code changed.  
**Regressions:** §22 (English-only blocked) and §23 (quiz-skip not honored on Continue)  
**Surfaced:** 5.5b post-deploy smoke, 2026-06-14  
**Conclusion:** Both regressions share a root cause. Single chunk recommended.

---

## Part 1 — §22 Backend Validator

**File:** `src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/InitialiseToolboxTalk/InitialiseToolboxTalkCommandValidator.cs`

Target language rule at lines 33–35:

```csharp
RuleFor(x => x.TargetLanguageCodes)
    .NotEmpty()
    .WithMessage("At least one target language is required.");
```

This is the **only** target-language rule in the file. All other rules cover `TenantId`, `Title`, `Code`, `Description`, `SourceText`, `SourceFileUrl`, `VideoUrl`, `AudienceRole`, and audit metadata. Fix: remove lines 33–35 only.

---

## Part 2 — §22 Frontend Validator

**File:** `web/src/features/toolbox-talks/components/learning-wizard/schemas/inputConfigSchema.ts`  
**Lines 26–28:**

```typescript
targetLanguageCodes: z
  .array(z.string())
  .min(1, 'At least one target language is required'),
```

Frontend also enforces `.min(1)`, rejecting empty arrays. No other files matched the target-language search. **Fix must address both layers.**

---

## Part 3 — §22 Steps 5 & 6 Reachability + Skipped Display

**File:** `web/src/features/toolbox-talks/components/learning-wizard/lib/stepOrder.ts`

Step 5 (Translate), lines 65–67:

```typescript
case 5:
  // Translate: reachable once sections exist
  return talk.sections.length > 0;
```

Step 6 (Validate), lines 68–70:

```typescript
case 6:
  // Validate: reachable once sections exist (translation may still be running)
  return talk.sections.length > 0;
```

**Critical gap:** Neither step checks `targetLanguageCodes`. An English-only talk with sections will show both steps as reachable. Fix: add `&& talk.targetLanguageCodes.length > 0` to both cases.

### "Skipped" display logic

**File:** `web/src/features/toolbox-talks/components/learning-wizard/components/StepIndicator.tsx`

Steps are passed both `reachable` and `skipped` booleans. Lines 20–28:

```typescript
type StepState = 'current' | 'complete' | 'reachable' | 'skipped' | 'unreachable';

function getStepState(step: StepItem, currentStep: number): StepState {
  if (step.number === currentStep) return 'current';
  if (step.skipped) return 'skipped';
  if (step.number < currentStep) return 'complete';
  if (step.reachable) return 'reachable';
  return 'unreachable';
}
```

Line 103:

```typescript
{state === 'skipped' ? `${step.label} — Skipped` : step.label}
```

The "Skipped" label requires the step's `skipped` property to be `true`. This is populated by `isStepSkipped()` (see Part 6 below). Steps 5 and 6 currently have no `isStepSkipped` logic — they would display as "unreachable" (not "— Skipped") even after fixing reachability. **Fix also needs `isStepSkipped(5/6, talk)` logic added to `stepOrder.ts` to produce the "— Skipped" label for English-only talks.**

---

## Part 4 — Continue-Button Next-Step Navigation (Root Cause of Both)

**File:** `web/src/features/toolbox-talks/components/learning-wizard/hooks/useStepNavigation.ts`

`goNext()` function, lines 50–58 — **the root cause:**

```typescript
const goNext = useCallback(async () => {
  const nextStep = currentStep + 1;
  if (nextStep > TOTAL_STEPS) return;
  // Persist progress before navigating forward
  if (talkId) {
    await updateStep.mutateAsync(nextStep);
  }
  router.push(getStepUrl(talkId, nextStep));
}, [currentStep, talkId, updateStep, router]);
```

`goNext` advances by **integer increment** — no reachability check at all.

`goToStep()` function, lines 60–66 — **the correct pattern:**

```typescript
const goToStep = useCallback(async (step: number) => {
  if (!isStepReachable(step, talk, validationRuns)) return;
  if (step > currentStep && talkId) {
    await updateStep.mutateAsync(step);
  }
  router.push(getStepUrl(talkId, step));
}, [talk, validationRuns, currentStep, talkId, updateStep, router]);
```

`goToStep` validates reachability before navigating. Step indicator clicks use `goToStep`; Continue buttons call `goNext`. **This architecture mismatch is the shared root cause of both §22 and §23.**

Fix: replace `goNext`'s body with a `findNextReachableStep(currentStep, talk, validationRuns)` helper that walks forward from `currentStep + 1` and returns the first step where `isStepReachable` is true. Then navigate via `goToStep(found)`.

---

## Part 5 — §23 Step 3 Reachability Rule

**File:** `web/src/features/toolbox-talks/components/learning-wizard/lib/stepOrder.ts`  
**Lines 58–61:**

```typescript
case 3:
  // Quiz: reachable once talk has sections AND requiresQuiz is true
  // When requiresQuiz is false, the step is skipped (not unreachable — it was intentionally disabled)
  return talk.sections.length > 0 && talk.requiresQuiz;
```

Field: `talk.requiresQuiz`. Step 3 is already correctly unreachable when `requiresQuiz` is false — the reachability rule for step 3 is not the bug.

---

## Part 6 — §23 Step 3 Skipped Display (Two-Signal Pattern)

**File:** `web/src/features/toolbox-talks/components/learning-wizard/lib/stepOrder.ts`

`isStepSkipped()`, lines 92–96:

```typescript
export function isStepSkipped(step: number, talk: ToolboxTalk | null): boolean {
  if (!talk) return false;
  if (step === 3) return talk.sections.length > 0 && !talk.requiresQuiz;
  return false;
}
```

**How these two signals wire together** (`useStepNavigation.ts` lines 68–72):

```typescript
const reachableSteps = WIZARD_STEPS.map((s) => ({
  ...s,
  reachable: isStepReachable(s.number, talk, validationRuns),
  skipped: isStepSkipped(s.number, talk),
}));
```

Each step object carries both `reachable` and `skipped`. When `requiresQuiz === false`:
- `isStepReachable(3, talk)` → `false`
- `isStepSkipped(3, talk)` → `true`
- Step indicator displays "3 Quiz — Skipped" ✓

**These functions are correct.** The indicator for §23 works. The failure is downstream — `goNext()` never consults either function and walks to step 3 regardless.

---

## Part 7 — §23 Step 2 Continue Behavior

**File:** `web/src/app/(authenticated)/admin/toolbox-talks/learnings/[talkId]/parse/page.tsx`  
**Line 43:**

```typescript
<ParseStep talkId={talkId} onContinue={goNext} />
```

ParseStep's Continue calls `goNext` from `useStepNavigation`, which does `currentStep + 1 = 3`. Step 3 is unreachable (`requiresQuiz === false`), but the router pushes to it anyway. The user is delivered to Step 3 which renders even though the quiz is disabled, blocking navigation to Step 4.

---

## Part 8 — Joint Diagnosis

### Do §22 and §23 share a root cause?

**Yes.** Both are caused by `goNext()` in `useStepNavigation.ts` using `currentStep + 1` without consulting `isStepReachable`. The step indicator is correct; the Continue navigation is not.

§22 additionally requires:
1. Relaxing the backend and frontend validators (the `goNext` fix alone does not unblock Step 1 submission)
2. Adding `targetLanguageCodes.length > 0` checks to step 5 and 6 reachability rules
3. Adding `isStepSkipped` logic for steps 5 and 6 (so they display "— Skipped" not silently "unreachable")

§23 requires only the `goNext` fix — no reachability rule changes needed.

### File surface — single chunk (both together)

| File | Change | Reason |
|---|---|---|
| `useStepNavigation.ts` | Replace `currentStep + 1` in `goNext()` with `findNextReachableStep()` | Shared root cause |
| `stepOrder.ts` | Add `&& talk.targetLanguageCodes.length > 0` to case 5 and case 6 | §22 reachability |
| `stepOrder.ts` | Add `isStepSkipped` logic for steps 5 and 6 | §22 "— Skipped" display |
| `inputConfigSchema.ts` | Change `.min(1)` to `.min(0)` on `targetLanguageCodes` | §22 frontend validator |
| `InitialiseToolboxTalkCommandValidator.cs` | Remove lines 33–35 (target language `.NotEmpty()` rule) | §22 backend validator |
| `settings/page.tsx` | Fix hardcoded `router.push(getStepUrl(talkId, 5))` → use `goNext` | §22 Settings→Translate skip (see adjacent) |

### File surface — if split

**§23 alone (1 file):**
- `useStepNavigation.ts` — fix `goNext()` only

**§22 alone (4–5 files):**
- `useStepNavigation.ts` — fix `goNext()` (also fixes §23, so splitting is wasteful)
- `stepOrder.ts` — step 5/6 reachability + isStepSkipped
- `inputConfigSchema.ts` — `.min(0)`
- `InitialiseToolboxTalkCommandValidator.cs` — remove rule
- `settings/page.tsx` — hardcoded step 5 navigation

### Recommendation: single chunk

File surface overlaps entirely (§23's only file is a subset of §22's). Splitting produces two PRs where the first (`goNext` fix) half-closes §22 but the validator still blocks Step 1, leaving a partially-broken state. Single chunk is cleaner and shorter.

### Existing test coverage

**File:** `tests/QuantumBuild.Tests.E2E/tests/toolbox-talks/content-creation.spec.ts`

Coverage:
- Lines 109–127: Basic wizard navigation (happy path, always with languages selected)
- Lines 129–143: Step 1 with PDF + language selection (always includes a language)
- Lines 152–186: Parse step completion
- Lines 188–197: Quiz generation
- Lines 199+: Steps 4–7

**Gaps — no tests for:**
- `includeQuiz: false` path (§23 scenario)
- Empty `targetLanguageCodes` / English-only path (§22 scenario)
- Continue-button navigation respecting reachability

**Risk note:** This fix touches step routing exercised by every user flow. Manual smoke after the fix must cover: (a) English-only talk creation end-to-end, (b) talk with quiz disabled end-to-end, (c) full multi-language + quiz enabled (regression guard on the happy path).

---

## Adjacent Observations (do not expand scope)

1. **`settings/page.tsx` hardcodes step 5:** `onContinue={() => router.push(getStepUrl(talkId, 5))}` bypasses `goNext()` entirely. For an English-only talk, even after fixing `goNext`, Settings → Continue still navigates to Translate (step 5) regardless of `targetLanguageCodes`. This file **must** be included in the fix chunk — it is a prerequisite for §22 working end-to-end, not merely adjacent.

2. **Step 7 reachability already checks `targetLanguageCodes`:** `stepOrder.ts` lines 71–81 show step 7's logic correctly branches on `targetLanguageCodes.length`. This confirms the intent — steps 5 and 6 were simply missed when target-language branching was added to step 7.

3. **No `isStepSkipped` entries for steps 5 and 6:** `isStepSkipped()` only handles step 3 currently. Steps 5 and 6 fall through to `return false`, so without explicit cases they'll display as "unreachable" (greyed out, no label suffix) rather than "— Skipped". The fix should add cases for 5 and 6 matching step 3's pattern.

4. **`goToStep` is the safe wrapper:** It already validates reachability before navigating. The fix for `goNext` can delegate to `goToStep(findNextReachable(...))` rather than duplicating the guard — keeps the fix minimal.
