# Step 7 Reachability — Structural Robustness Fix

**Date:** 2026-06-14  
**Branch:** transval  
**Diff scope:** 3 files — `stepOrder.ts`, `useStepNavigation.ts`, `BACKLOG.md`

---

## What Was Fixed and Why

The previous wiring fix (commit `9e834d6`) passed `validationRuns` from all
seven page wrappers into `useStepNavigation`, resolving the immediate bug where
Continue was stuck disabled on multi-language talks. That fix mitigated the
symptom but left the underlying brittleness intact:

- The step 7 rule used `if (!validationRuns) return false`, which treated `null`
  and `undefined` the same but was one line away from the `codes.length === 0`
  short-circuit — a reorder could silently break English-only.
- The four behavioural states were conflated via short-circuit ordering rather
  than named explicitly, making the rule hard to audit.
- `validationRuns` was optional in `UseStepNavigationOptions` — callers could
  omit it and get silent `undefined`, passing the type check without the compiler
  raising a flag.

This chunk closes the bug class at three structural layers.

---

## Layer 1 — Defensive Default

**File:** `web/src/features/toolbox-talks/components/learning-wizard/lib/stepOrder.ts:83`

**Before:**
```ts
if (!validationRuns) return false;
return validationRuns.some((r) => r.status === 'Completed');
```

**After:**
```ts
const runs = validationRuns ?? [];
if (runs.length === 0) return false; // none exist (not fetched yet, or no runs created)
return runs.some((r) => r.status === 'Completed');
```

`?? []` collapses `null`, `undefined`, and empty array into the same code path,
eliminating the `!validationRuns` falsy check that masked the asymmetry with
the English-only branch.

---

## Layer 2 — Explicit States

**File:** `web/src/features/toolbox-talks/components/learning-wizard/lib/stepOrder.ts:71–86`

The case 7 block now names each behavioral state with a comment:

| State | Condition | Return |
|---|---|---|
| Zero sections | `talk.sections.length === 0` | `false` |
| Already published | `talk.status === 'Published'` | `false` |
| No target languages (English-only) | `codes.length === 0` | `true` |
| Target languages declared, no runs | `runs.length === 0` | `false` |
| Target languages declared, completed run exists | `runs.some(…)` | `true` |

Previously states 4 and 5 were conflated by the `!validationRuns` short-circuit.

---

## Layer 3 — Required Parameter

**File:** `web/src/features/toolbox-talks/components/learning-wizard/hooks/useStepNavigation.ts:17`

**Before:**
```ts
validationRuns?: ValidationRunSummary[] | null;
```

**After:**
```ts
validationRuns: ValidationRunSummary[] | null | undefined;
```

The `?` is removed. The value type is unchanged — callers may still pass
`undefined` during loading — but the property itself is required, so TypeScript
raises an error at any callsite that omits it entirely.

---

## TypeScript Results

| State | Errors |
|---|---|
| Baseline (before changes) | 0 |
| After changes | 0 |

No new errors introduced. All seven page wrappers were already compliant (the
prior wiring fix ensured this), so making the property required produced no
callsite failures.

---

## BACKLOG Entry Added

**§22 — Unit tests for Step 7 reachability rule (depends on §17)**

Logged in `BACKLOG.md` at line 1345. Captures the five test cases (zero
sections, already published, English-only, target languages + no runs, target
languages + completed run) and the `undefined ?? []` equivalence assertion.
Blocked on BACKLOG §17 (no frontend test framework installed).

---

## Prompt Requirements Coverage

| Requirement | Status |
|---|---|
| Layer 1 — `?? []` defensive default | Done — `stepOrder.ts:83` |
| Layer 2 — explicit named states with comments | Done — `stepOrder.ts:71–86` |
| Layer 3 — required property in `UseStepNavigationOptions` | Done — `useStepNavigation.ts:17` |
| BACKLOG §22 entry referencing §17 | Done — `BACKLOG.md:1345` |
| Baseline TS check captured | Done — 0 errors |
| Post-change TS check captured | Done — 0 errors |
| Diff limited to 3 files | Done — `stepOrder.ts`, `useStepNavigation.ts`, `BACKLOG.md` |
| Persisted report | This file |
| Unit tests | Deferred — blocked on §17 (BACKLOG §22) |
| Smoke test | N/A — structural-only changes; prior wiring-fix smoke sufficient |

---

## Files Changed

| File | Purpose |
|---|---|
| `web/src/features/toolbox-talks/components/learning-wizard/lib/stepOrder.ts` | Layer 1 + Layer 2 |
| `web/src/features/toolbox-talks/components/learning-wizard/hooks/useStepNavigation.ts` | Layer 3 |
| `BACKLOG.md` | §22 deferred test entry |
| `docs/phase-5/reports/step-navigation-robustness-fix.md` | This report |

## Files Changed Outside Stated Scope

None.
