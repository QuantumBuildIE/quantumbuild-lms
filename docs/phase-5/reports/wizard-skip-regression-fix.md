# Wizard Skip Regression Fix — §22 & §23

**Date:** 2026-06-14  
**Branch:** transval  
**Chunk:** Joint §22 + §23  
**Closes:** BACKLOG 5.13 (English-only blocked) and BACKLOG 5.14 (quiz skip not honored)  
**Recon:** `docs/phase-5/reports/wizard-skip-regression-recon.md`

---

## 1. Test Results

### Backend — unit tests

| Run | Failed | Passed | Skipped | Total |
|---|---|---|---|---|
| Baseline (HEAD, no changes) | 0 | 192 | 0 | 192 |
| With changes applied | 0 | 192 | 0 | 192 |

Runner: `dotnet test tests/QuantumBuild.Tests.Unit --no-build`  
Result: **192 of 192 passing, 0 failures, no change from baseline.**

### Frontend — TypeScript check

| Run | Result |
|---|---|
| Baseline | 0 errors (exit 0) |
| With changes | 0 errors (exit 0) |

Command: `npx tsc --noEmit` from `web/`  
No test framework installed (BACKLOG #17).

### Integration tests

Not run (require live database). Unit tests cover all backend validator logic exercised by this chunk.

---

## 2. Files Changed in Scope

### Navigation layer

**`web/src/features/toolbox-talks/components/learning-wizard/hooks/useStepNavigation.ts`**
- Line 7: Added `findNextReachableStep` to import from `../lib/stepOrder`
- Lines 50–58: Replaced `goNext` body — was `currentStep + 1` integer increment; now calls `findNextReachableStep(currentStep, talk, validationRuns)` and navigates to the first reachable step. Dependency array updated accordingly.

**`web/src/features/toolbox-talks/components/learning-wizard/lib/stepOrder.ts`**
- Lines 65–70: Cases 5 & 6 `isStepReachable` now return `talk.sections.length > 0 && parseTargetCodes(talk.targetLanguageCodes ?? null).length > 0` (was `talk.sections.length > 0` only).
- Lines 92–99: `isStepSkipped` gains cases for steps 5 and 6 — returns `true` when `sections.length > 0 && targetLanguageCodes.length === 0`, matching the existing step 3 pattern.
- Lines 101–113: New exported `findNextReachableStep(currentStep, talk, validationRuns)` helper — walks `currentStep + 1` upward, returns first step where `isStepReachable` is true, or `null` if none.

**`web/src/app/(authenticated)/admin/toolbox-talks/learnings/[talkId]/settings/page.tsx`**
- Import: Removed `getStepUrl` (no longer needed).
- Line 17: Added `goNext` to `useStepNavigation` destructure.
- Line 42: `SettingsStep.onContinue` changed from `() => router.push(getStepUrl(talkId, 5))` to `goNext`. This closes the Settings-page bypass that the navigation-layer fix alone couldn't reach.

### Data-acceptance layer

**`web/src/features/toolbox-talks/components/learning-wizard/schemas/inputConfigSchema.ts`**
- Line 28: `targetLanguageCodes` changed from `.min(1, 'At least one target language is required')` to `.min(0)`. Frontend now accepts empty arrays.

**`src/Modules/ToolboxTalks/QuantumBuild.Modules.ToolboxTalks.Application/Commands/InitialiseToolboxTalk/InitialiseToolboxTalkCommandValidator.cs`**
- Lines 33–35 removed: `RuleFor(x => x.TargetLanguageCodes).NotEmpty()`. Empty target languages now accepted by the backend. Pre-flight check confirmed the two downstream consumers (`PublishToolboxTalkCommandHandler.cs:43` and `ContentCreationSessionService.cs:762`) both handle empty collections safely.

---

## 3. Files Changed Outside Stated Scope

**`tests/QuantumBuild.Tests.Integration/ToolboxTalks/InitialiseToolboxTalkCommandHandlerTests.cs`**  
**Rationale:** Test 7 `EmptyTargetLanguageCodes_Returns400` directly asserted the behavior being removed. With the backend validator rule gone, the endpoint returns 201 for empty target languages. The test was updated to `EmptyTargetLanguageCodes_Returns201` asserting `HttpStatusCode.Created`. Not updating it would leave a failing integration test as a consequence of an in-scope change. This is a contract-update housekeeping change, not a new feature.

---

## 4. Build Output

**Backend:** `dotnet test tests/QuantumBuild.Tests.Unit --no-build` → `Passed! Failed: 0, Passed: 192, Skipped: 0, Total: 192`  
**Frontend:** `npx tsc --noEmit` → exit 0, no output  
No new warnings introduced by this chunk.

---

## 5. Prompt Requirements Coverage

| Requirement | Status |
|---|---|
| **File 1** `useStepNavigation.ts` — `goNext()` uses `findNextReachableStep` not `currentStep + 1` | ✅ Done |
| **File 2** `stepOrder.ts` — case 5 reachability checks `targetLanguageCodes.length > 0` | ✅ Done |
| **File 2** `stepOrder.ts` — case 6 reachability checks `targetLanguageCodes.length > 0` | ✅ Done |
| **File 2** `stepOrder.ts` — `isStepSkipped` handles steps 5 and 6 (mirrors step 3 pattern) | ✅ Done |
| **File 2** `stepOrder.ts` — `findNextReachableStep` helper added | ✅ Done |
| **File 3** `inputConfigSchema.ts` — `.min(1)` changed to `.min(0)` on `targetLanguageCodes` | ✅ Done |
| **File 4** `InitialiseToolboxTalkCommandValidator.cs` — `.NotEmpty()` rule on TargetLanguageCodes removed | ✅ Done |
| **File 5** `settings/page.tsx` — `router.push(getStepUrl(talkId, 5))` replaced with `goNext` | ✅ Done |
| Pre-flight check: downstream consumers of empty `TargetLanguageCodes` safe | ✅ Verified (recon confirmed; no new check required) |
| PowerShell spot-checks (all 5) passing | ✅ Done |
| Baseline unit test run captured | ✅ 192/192 |
| Post-change unit test run captured | ✅ 192/192 |
| Frontend TypeScript baseline captured | ✅ 0 errors |
| Frontend TypeScript post-change captured | ✅ 0 errors |
| Diff scope — six files (+1 test update, disclosed) | ✅ Done |
| BACKLOG 5.13 closed with Done status and fix summary | ✅ Done |
| BACKLOG 5.14 closed with Done status and fix summary | ✅ Done |
| Report persisted at `docs/phase-5/reports/wizard-skip-regression-fix.md` | ✅ Done |
| **Scenario A** — English-only end-to-end | ⏸ Pending deploy (smoke deferred to post-push) |
| **Scenario B** — Quiz-disabled end-to-end | ⏸ Pending deploy (smoke deferred to post-push) |
| **Scenario C** — Multi-language + quiz regression guard | ⏸ Pending deploy (smoke deferred to post-push) |
| Unit tests for navigation logic | 🚫 Out of scope — no frontend test framework (BACKLOG #17) |
| Auditing other wizard steps for similar `router.push(getStepUrl(...))` bypasses | 🚫 Out of scope — per prompt |
| Refactoring `useStepNavigation` beyond `goNext` fix | 🚫 Out of scope — per prompt |

---

## 6. Smoke Evidence

Pending post-deploy run against Railway `transval` environment. Scenarios are:

- **Scenario A** — English-only end-to-end: Step 1 accepts empty targets, Continue from Settings jumps to Publish (7), steps 5 & 6 render "— Skipped", Publish succeeds.
- **Scenario B** — Quiz-disabled end-to-end: Continue from Parse (2) jumps to Settings (4), step 3 shows "— Skipped", full flow through to Publish.
- **Scenario C** — Multi-language + quiz regression guard: All 7 steps visited in order, no step skipped, Publish succeeds.

Scenario A from this chunk is equivalent to 5.5b smoke Scenario 1 — 5.5b smoke may reference this report's evidence rather than re-running.

---

## 7. BACKLOG Notes

- **5.13** closed — see section 3 of BACKLOG.md
- **5.14** closed — see section 3 of BACKLOG.md
- **Integration test** `EmptyTargetLanguageCodes_Returns201` updated — out-of-scope disclosure in §3 above
- No new BACKLOG items introduced by this chunk

---

## Post-deploy smoke evidence (2026-06-14)

**Environment:** Railway transval (production deploy)
**Browser:** Chrome (smoke session)

### Scenario A — English-only end-to-end

**What this verifies:** Creating an English-only learning, walking through Settings, and verifying the wizard skips Translate and Validate to land on Publish.

**Steps and observations:**
1. Created draft talk with source language, no target languages. Step 1 form accepted the submission; no "at least one target language is required" error.
2. Parsed content (2 sections), continued through Quiz and Settings.
3. Clicked Continue on Settings → wizard navigated directly to Step 7 Publish.
4. Step indicator showed Steps 5 (Translate) and 6 (Validate) as "— Skipped".
5. Publish → success, navigated to talk detail page.

**Key observation:** Settings → Publish skip works end-to-end. Steps 5 and 6 are not visited.

**Verdict:** ✅ Pass

### Scenario B — Quiz-disabled end-to-end

**What this verifies:** When "include quiz" is deselected, the wizard skips Quiz (Step 3) on Continue from Parse.

**Steps and observations:**
1. Created draft talk with source + at least one target language, "include quiz" deselected on Step 1.
2. Parsed content; step indicator showed Step 3 as "Quiz — Skipped".
3. Clicked Continue on Parse → wizard navigated to Step 4 Settings (not Step 3 Quiz).
4. Continued through remaining steps; reached Publish without ever stopping on Quiz.

**Key observation:** Parse → Settings skip works; Quiz step is not visited.

**Verdict:** ✅ Pass

### Scenario C — Multi-language + quiz regression guard

**What this verifies:** The wizard does not over-skip on the happy path; every step in a multi-language + quiz flow remains reachable. Also verifies the validate-page wiring fix (Continue button enabled at Step 6 once at least one validation run completes).

**Steps and observations:**
1. Created draft talk with source + three target languages (RU, AF, French) + quiz enabled.
2. Walked through every step in order: Parse → Quiz → Settings → Translate → Validate.
3. Translations completed across all three languages.
4. Validation runs completed; outcomes varied (Pass, Review).
5. On Step 6 Validate, Continue button was enabled.
6. Continue on Step 6 → navigated to Step 7 Publish.
7. Publish succeeded.

**Key observation:** Continue enabled at Step 6 despite Review-state outcomes (the previously-broken case). Validate-page wiring fix verified working. No step is over-skipped.

**Verdict:** ✅ Pass

**Observations (not blockers):** Validate step's detail pages render full read-side information (scores, back-translations, consensus calculations, regulatory scoring panel) but expose no reviewer-action UI for per-section accept/reject or initiate-external-review. Logged as consolidated BACKLOG entry §23 (see BACKLOG.md).

### Summary

| Scenario | Verifies | Verdict |
|---|---|---|
| A | English-only skip path (Settings → Publish, skipping 5 and 6) | ✅ |
| B | Quiz-disabled skip path (Parse → Settings, skipping 3) | ✅ |
| C | Multi-language + quiz regression guard; validate-page wiring | ✅ |

**Overall:** All three scenarios pass. Validate-page wiring bug verified closed via Scenario C.

**BACKLOG entries closed by passing smoke:** §5.13 (English-only blocked), §5.14 (quiz skip not honored).
**BACKLOG entries surfaced during smoke:** Consolidated reviewer-action UI entry §23 (see BACKLOG.md).
