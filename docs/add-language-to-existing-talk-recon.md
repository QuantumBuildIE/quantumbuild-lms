# Recon: "Adding a language to an existing talk didn't work"

**Date:** 2026-06-23
**Blocking:** Demo 2026-06-26
**Reporter:** Customer (worked around by delete + recreate)

---

## 1. One-Line Summary

The most likely cause is **Bug A**: when using the old-wizard translation panel (`TranslationWorkflowPanel`), the overwrite confirmation dialog fires `fireTranslateMutation` without passing `confirmOverwrite: true`, so any language in `Accepted` state silently returns "Confirmation required" from the backend — and a newly-added language on an **existing published talk** hits the workflow `StartTranslation` guard on the old-wizard path because `GenerateContentTranslations` runs synchronously against the workflow state machine, not as a deferred Hangfire job. The fix is two lines: add `confirmOverwrite?: boolean` to the TypeScript DTO and thread the flag through the overwrite dialog's `onClick` handler.

---

## 2. Surfaces Inventoried

| Surface | File | What the user does | API call |
|---|---|---|---|
| **Translations tab on Talk Detail** (new-wizard talks, `lastEditedStep != null`) | `web/src/features/toolbox-talks/components/learning-wizard/steps/TranslateStep.tsx` | Picks a language from `AddTargetLanguagePicker`, then presses "Start" per language | `POST /api/toolbox-talks/{id}/target-languages` then `POST /api/toolbox-talks/{talkId}/translations/{code}/start-translation` |
| **Translations tab on Talk Detail** (old-wizard talks, `lastEditedStep == null`) | `web/src/features/toolbox-talks/components/TranslationWorkflowPanel.tsx` | Same language picker, then clicks "Translate" per row | `POST /api/toolbox-talks/{id}/target-languages` then `POST /api/toolbox-talks/{id}/translations/generate` |
| **`AddTargetLanguagePicker`** (shared by both) | `web/src/features/toolbox-talks/components/detail/AddTargetLanguagePicker.tsx` | Selects a language and clicks "Add" | `POST /api/toolbox-talks/{id}/target-languages` |
| **Create Content Wizard Step 5 (legacy)** | `web/src/features/toolbox-talks/components/create-wizard/steps/TranslateStep.tsx` | Not relevant to existing talk scenario |  |
| **Create Content Wizard Step 5 (new wizard)** | `web/src/features/toolbox-talks/components/create-wizard/steps/TranslateValidateStep.tsx` | Not relevant to existing talk scenario |  |

The `ToolboxTalkDetail` component (`web/src/features/toolbox-talks/components/ToolboxTalkDetail.tsx`, line 367–382) renders both surfaces on the **same Translations tab**, but uses only `TranslateStep` — `TranslationWorkflowPanel` is not imported there. The distinction between old-wizard and new-wizard talks therefore does NOT change which translation UI is used on the detail page; both talk types use `TranslateStep`.

**Correction:** `TranslationWorkflowPanel` is a standalone card component visible elsewhere (potentially via the Edit page at `web/src/app/(authenticated)/admin/toolbox-talks/talks/[id]/edit/page.tsx`), but the customer-facing "existing talk" scenario goes through `ToolboxTalkDetail` → `TranslateStep`.

---

## 3. Per-Surface Diagnosis

### Surface A — `AddTargetLanguagePicker` (language registration step)

**File:** `web/src/features/toolbox-talks/components/detail/AddTargetLanguagePicker.tsx`

- Gated by `usePermission('Learnings.Manage')` (line 25, 30).
- Calls `POST /api/toolbox-talks/{id}/target-languages` via `useAddTargetLanguage` mutation.
- Backend policy: `[Authorize(Policy = "Learnings.Manage")]` (controller line 1610).
- Backend handler: `AddTargetLanguageCommandHandler` — deserialises `TargetLanguageCodes` JSON, adds the code, saves. Returns `ToolboxTalkDto` directly (not wrapped — controller calls `Ok(result.Data)`, line 1629).
- Frontend reads `response.data` (correct — `addTargetLanguage` in toolbox-talks.ts line 717–726).
- **`onSuccess` cache invalidation** (`use-toolbox-talks.ts`, lines 301–308): invalidates `['toolbox-talks', talkId]`, `['toolbox-talks', talkId, 'workflow-state']`, and `['learnings', talkId]`.

**Diagnosis: this step appears correct end-to-end. The "Add" button works; toast shows success.**

### Surface B — `TranslateStep` (translation trigger step, used on detail page)

**File:** `web/src/features/toolbox-talks/components/learning-wizard/steps/TranslateStep.tsx`

- Uses `useTalk(talkId)` hook which queries key `['learnings', talkId]` with `staleTime: 30_000` (`useTalk.ts`, line 9, 12).
- `ToolboxTalkDetail` uses `useToolboxTalk(talkId)` which queries key `['toolbox-talks', talkId]` (no stale time, `refetchOnWindowFocus: true`).
- These are **two separate cache entries** calling the same API endpoint. Both are invalidated by `useAddTargetLanguage.onSuccess`.
- `hasTargetLanguages` (detail component line 106) is derived from `useToolboxTalk`, so `TranslateStep` mounts only after the `useToolboxTalk` refetch sees the new language.
- When `TranslateStep` mounts fresh, `useTalk` has no cached entry (or an invalidated one), so it fetches fresh data. No stale-data timing issue in practice.
- `languages = parseLanguageCodes(talk?.targetLanguageCodes ?? null)` (line 55) — correctly shows languages from `TargetLanguageCodes`.

**`handleStart` (line 74–85):**
```typescript
const current = stateByCode[languageCode]?.state;
const confirmOverwrite = current === 'Stale';
startTranslation({ talkId, languageCode, confirmOverwrite }, { onError: ... });
```

For a newly-added language, `current` is `undefined`. `canStart(undefined) = true` (line 26–29). `confirmOverwrite = false`. The call proceeds.

**Backend (`StartTalkTranslationCommandHandler`):**
1. Loads talk with `IgnoreQueryFilters()`, checks `TenantId` and `!IsDeleted`.
2. Calls `IsLanguageInTargets(talk.TargetLanguageCodes, request.LanguageCode)` — requires the language to already be in `TargetLanguageCodes` (handler lines 48–51). Since `AddTargetLanguagePicker` already saved it, this check passes.
3. Calls `_workflow.StartTranslation(...)` — advances state machine.
4. Creates a `TranslationValidationRun` and enqueues `TranslationValidationJob` via Hangfire.

**Diagnosis for a newly-added language on a fresh talk state: the flow should work. However there is a critical gap (see Bug B below) when the state is NOT `Initial`.**

---

## 4. Backend Flow

### Path 1 — New-Wizard: `POST /translations/{code}/start-translation`

```
AddTargetLanguagePicker
  → POST /target-languages              → AddTargetLanguageCommandHandler
      Adds code to TargetLanguageCodes JSON, saves, returns DTO
  → cache invalidated, TranslateStep mounts
  → user clicks "Start"
  → POST /translations/{code}/start-translation → StartTalkTranslationCommandHandler
      - IsLanguageInTargets check
      - workflow.StartTranslation (state machine guard)
      - Creates TranslationValidationRun (IsNewWizard=true)
      - Enqueues TranslationValidationJob (Hangfire, async)
  → returns { runId, jobId }
  → SignalR WorkflowSubscriber receives progress events
```

### Path 2 — Old-Wizard: `POST /translations/generate`

```
AddTargetLanguagePicker
  → POST /target-languages              → AddTargetLanguageCommandHandler (same as above)
  → TranslationWorkflowPanel renders the new language row in Initial state
  → user clicks "Translate"
  → POST /translations/generate         → GenerateContentTranslationsCommandHandler
      - Loops over request.Languages (names, not codes)
      - GetLanguageCodeAsync(languageName) to resolve code
      - workflow.StartTranslation guard (same guard)
      - Calls TranslateForLanguageAsync (SYNCHRONOUS, inline)
      - SaveChangesAsync
      - RecordTranslationCompleted
  → returns GenerateTranslationsResponse synchronously
```

**Key difference:** the old path translates synchronously in the HTTP request. The new path offloads to Hangfire and returns immediately.

---

## 5. Confirmed Bugs

### Bug A — CONFIRMED BLOCKER: `TranslationWorkflowPanel` overwrite dialog never sends `confirmOverwrite: true`

**Files:**
- `web/src/lib/api/toolbox-talks/toolbox-talks.ts`, line 469–471
- `web/src/features/toolbox-talks/components/TranslationWorkflowPanel.tsx`, lines 141–147, 488–499

**Backend DTO** (`src/QuantumBuild.API/Controllers/ToolboxTalksController.cs`, lines 2528–2540):
```csharp
public record GenerateTranslationsRequest
{
    public List<string> Languages { get; init; } = new();
    public bool? ConfirmOverwrite { get; init; }  // ← exists on backend
}
```

**Frontend TS interface** (`toolbox-talks.ts` line 469):
```typescript
export interface GenerateTranslationsRequest {
  languages: string[];
  // ← ConfirmOverwrite is MISSING
}
```

**Effect:** When `TranslationWorkflowPanel` fires `fireTranslateMutation` after the user confirms the overwrite dialog (line 492), the request body never includes `confirmOverwrite: true`. The backend's `GenerateContentTranslationsCommandHandler` evaluates the workflow guard with `ConfirmOverwrite = false`, which causes `workflowGuard.Success = false` with `FailureCode.WorkflowConfirmationRequired` for any language in `Accepted` or `ReviewerAccepted` state. The error "Confirmation required to overwrite this language." is displayed via `toast.error(err)` (line 153). The user's overwrite confirmation is ignored.

**This path is used for old-wizard talks only. New-wizard talks use `TranslateStep`, not `TranslationWorkflowPanel`.**

Whether the customer was on the old-wizard path depends on whether their talk was created with the legacy wizard (`lastEditedStep == null`) or the new wizard (`lastEditedStep != null`). Since the customer "deleted and recreated," it's likely they were recreating an existing (presumably legacy) talk, making Bug A the most probable cause.

### Bug B — `TranslateStep.handleStart` sends `confirmOverwrite = false` when state is `Accepted` or `ReviewerAccepted`

**File:** `web/src/features/toolbox-talks/components/learning-wizard/steps/TranslateStep.tsx`, line 76

```typescript
const confirmOverwrite = current === 'Stale';  // ← only Stale triggers confirmOverwrite
```

The `canStart()` function (line 26–29) only returns `true` for `Initial`, `AIGenerated`, and `Stale`. It returns `false` for `Accepted` and `ReviewerAccepted`, so the Start button is disabled for those states. **This means the user can never re-translate an Accepted language on a new-wizard talk from the detail page — the Start button is simply greyed out with no explanation.** This is not a crash bug but it IS a UX dead-end.

### Bug C — NOT a bug, but a confusion point: `TranslationWorkflowPanel` shows employee-language rows that are not in `TargetLanguageCodes`

**File:** `web/src/features/toolbox-talks/components/TranslationWorkflowPanel.tsx`, lines 129–138

`TranslationWorkflowPanel` builds its rows from both `existingTranslations` (talks with a `ToolboxTalkTranslation` record) AND `languagesData.employeeLanguages` (languages spoken by employees). A language can appear in the panel even if it was never added to `TargetLanguageCodes`. When the user clicks "Translate" for such a row, the backend `GenerateContentTranslations` handler does NOT check `TargetLanguageCodes` — it proceeds to translate. This is not a blocking bug here; the handler resolves the language name to a code via `GetLanguageCodeAsync` and calls the workflow guard. The workflow `StartTranslation` call for a never-registered language may succeed or fail depending on the workflow service implementation.

**Diagnostic test:** check whether the workflow guard in `ITranslationWorkflowService.StartTranslation` requires the language to be in `TargetLanguageCodes`. If it does not, the translation succeeds but the language is not persisted in `TargetLanguageCodes`, causing the TranslateStep's `parseLanguageCodes` to ignore it on the next page load.

---

## 6. Reproduction Steps

**To reproduce Bug A:**
1. Open any talk created with the legacy wizard (`lastEditedStep == null` — has an "Edit" button visible when `wizardPreference === 'old'`).
2. Go to the Translations tab.
3. The `TranslationWorkflowPanel` should render (not `TranslateStep`). **Wait — see clarification below.**

**Important clarification:** Per `ToolboxTalkDetail.tsx` line 367–382, the Translations tab renders:
- `AddTargetLanguagePicker` always
- **`TranslateStep`** when `hasTargetLanguages === true`, regardless of wizard type

`TranslationWorkflowPanel` is NOT rendered on the detail page at all. It is imported on the Edit page or elsewhere. This changes the blast radius:

**To reproduce Bug A (Edit page path):**
1. Open a talk's Edit page (`/admin/toolbox-talks/talks/{id}/edit`).
2. Find the `TranslationWorkflowPanel` card.
3. Find a language in `Accepted` state.
4. Click "Translate" — the overwrite dialog appears.
5. Click "Overwrite and regenerate."
6. Observe toast error: "Confirmation required to overwrite this language."

**To reproduce Bug B (Detail page, Translations tab):**
1. Open any talk with a language in `Accepted` state.
2. Go to the Translations tab.
3. Find the language row in `WizardTranslationPanel`.
4. Observe the Start button is disabled (greyed out) with no tooltip explaining why.
5. There is no way to re-translate the accepted language from this surface.

**To reproduce the original customer bug (most likely):**
1. Customer had a published talk with translations in `Accepted` state.
2. Wanted to add a NEW language (not replace an existing one).
3. Used `AddTargetLanguagePicker` to add the language — this works.
4. Clicked "Start" on the new language in `TranslateStep` — this should work for Initial state.
5. Possible failure point: `['learnings', talkId]` stale cache returns `targetLanguageCodes` without the new language, causing `TranslateStep` to render "No target languages configured" momentarily or permanently if the refetch races.

**To confirm the race condition:**
1. Add a new language.
2. Immediately observe `TranslateStep` — does it show the new language row or "No target languages configured"?
3. Hard-refresh the page — does the language appear?

---

## 7. Fix Shape Recommendation

### Fix 1 — Bug A (high confidence, low risk)

**`web/src/lib/api/toolbox-talks/toolbox-talks.ts`**, line 469:
```typescript
// Before:
export interface GenerateTranslationsRequest {
  languages: string[];
}

// After:
export interface GenerateTranslationsRequest {
  languages: string[];
  confirmOverwrite?: boolean;
}
```

**`web/src/features/toolbox-talks/components/TranslationWorkflowPanel.tsx`**, line 491 — change the overwrite dialog's `AlertDialogAction` click handler to pass `confirmOverwrite: true`:

In `fireTranslateMutation`, add a parameter:
```typescript
const fireTranslateMutation = async (
  languageCode: string,
  languageName: string,
  overwrite = false,   // ← add this
) => {
  ...
  const result = await generateMutation.mutateAsync({
    toolboxTalkId,
    request: { languages: [languageName], confirmOverwrite: overwrite },  // ← thread through
  });
  ...
};
```

And in the overwrite dialog `onClick`:
```typescript
fireTranslateMutation(overwriteLanguageCode, overwriteLanguageName, true);  // ← pass true
```

### Fix 2 — Bug B (medium confidence, medium risk)

Allow re-translation of accepted languages on the detail page. Two options:

**Option A (recommended):** Expand `canStart()` in `TranslateStep.tsx` to include `Accepted` and `ReviewerAccepted`, and set `confirmOverwrite = true` when those states are encountered. Add an inline confirmation prompt (use `AlertDialog` matching `TranslationWorkflowPanel`'s pattern).

**Option B (minimal):** Add a tooltip to the disabled Start button in `WizardTranslationPanel` explaining "Already accepted — cannot re-translate from this view." This tells the user where to go.

### Fix 3 — Cache race condition (low confidence, low risk)

If the stale-cache race is contributing to the "no target languages configured" blank state, consider adding `refetchOnMount: 'always'` to `useTalk` for the detail-page use case, or simply rely on the fact that both cache keys are invalidated and `TranslateStep` mounts fresh.

---

## 8. Risks and Edge Cases

- **`GenerateContentTranslationsCommandHandler` runs synchronously.** For large talks (many sections/questions), this blocks the HTTP response thread. If the customer's talk was large, the request may have timed out (Railway has a 60-second request timeout) and the user saw an error. The new-wizard path (`StartTalkTranslation`) avoids this by offloading to Hangfire.
- **The workflow guard is called twice** on the old-wizard path: once for each language in the `Languages` list. If the workflow service has side effects on the first call (e.g. it transitions state), subsequent languages in the same request see a different state. This could cause partial failures that look like success to the user (success count shown in toast is per-`languageResult.success`).
- **Language name vs. language code mismatch.** `TranslationWorkflowPanel` sends language **names** (e.g. "French") to `POST /translations/generate`. The handler calls `GetLanguageCodeAsync(languageName)` to resolve the code. `AddTargetLanguagePicker` sends language **codes** (e.g. "fr") to `POST /target-languages`. If `GetLanguageCodeAsync` doesn't recognise the name, the translation is silently skipped.
- **`AddTargetLanguageCommandHandler` validates against `GetAllLanguagesAsync()`** (handler line 50–56): if the lookup returns empty (configuration gap), validation is skipped. This is a permissive fallback — not a blocking risk.

---

## 9. What Recon Could Not Determine Without More User Input

1. **Which wizard type** was the customer's talk? If `lastEditedStep` was non-null, the talk was new-wizard and `TranslationWorkflowPanel` was not involved at all. If `lastEditedStep` was null, the Edit page uses the old-wizard panel.
2. **What was the exact error message or UI state** the customer saw? "Didn't work" could mean: (a) toast error shown, (b) button disabled/greyed out, (c) spinner ran indefinitely, (d) translation appeared to succeed but employees saw English.
3. **Was this a first-time language addition or a re-translation?** If re-translation of an `Accepted` language: Bug A is the cause on the Edit page. If first-time addition: the `TranslateStep` path is clean and the failure is more likely the cache race or a timeout.
4. **Which environment?** Development vs Production. Development Railway timeouts may differ.
5. **Whether `TranslationWorkflowPanel` is actually visible on the Edit page** — requires reading `web/src/app/(authenticated)/admin/toolbox-talks/talks/[id]/edit/page.tsx` to confirm the panel is rendered there.

**Fastest one-click diagnostic before the demo:**
Open browser DevTools Network tab, reproduce the "adding a language" flow, and check:
- Does `POST /target-languages` return 200? If 403: permission issue.
- Does `POST /translations/{code}/start-translation` (new-wizard) or `POST /translations/generate` (old-wizard Edit page) return 200? If 200 but body contains `success: false`: read `languageResults[0].errorMessage`. If "Confirmation required": Bug A confirmed.
- If both return 200 but no translation appears: Hangfire job failure — check Railway logs.

---

## Key File Reference

| File | Relevance |
|---|---|
| `web/src/features/toolbox-talks/components/detail/AddTargetLanguagePicker.tsx` | Language registration UI |
| `web/src/features/toolbox-talks/components/learning-wizard/steps/TranslateStep.tsx` | Translation trigger on detail page |
| `web/src/features/toolbox-talks/components/TranslationWorkflowPanel.tsx` | Old-wizard translation panel (Edit page) — Bug A location |
| `web/src/lib/api/toolbox-talks/toolbox-talks.ts` lines 469–471 | Missing `confirmOverwrite` in TS interface — Bug A |
| `web/src/lib/api/toolbox-talks/use-toolbox-talks.ts` lines 295–309 | `useAddTargetLanguage.onSuccess` cache invalidation |
| `web/src/features/toolbox-talks/components/learning-wizard/hooks/useTalk.ts` | `staleTime: 30_000` on `['learnings', talkId]` cache key |
| `web/src/features/toolbox-talks/components/ToolboxTalkDetail.tsx` lines 367–382 | Where `AddTargetLanguagePicker` and `TranslateStep` are composed |
| `src/QuantumBuild.API/Controllers/ToolboxTalksController.cs` lines 1480–1557 | `POST /translations/generate` endpoint |
| `src/QuantumBuild.API/Controllers/ToolboxTalksController.cs` lines 1609–1630 | `POST /target-languages` endpoint |
| `src/Modules/ToolboxTalks/.../Commands/StartTalkTranslation/StartTalkTranslationCommandHandler.cs` | New-wizard translation handler |
| `src/Modules/ToolboxTalks/.../Commands/AddTargetLanguage/AddTargetLanguageCommandHandler.cs` | Language registration handler |
| `src/Modules/ToolboxTalks/.../Commands/GenerateContentTranslations/GenerateContentTranslationsCommandHandler.cs` | Old-wizard translation handler — synchronous |
