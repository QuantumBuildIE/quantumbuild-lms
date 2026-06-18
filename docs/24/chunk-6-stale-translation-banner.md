# §24 Chunk 6 — Stale-Translation Banner

_Date: 2026-06-18_
_Branch: transval_
_Author: Claude Code_

---

## 1. Test Results

**Backend (dotnet build):**
```
11 Warning(s)  (all pre-existing)
0 Error(s)
Time Elapsed 00:00:44.93
```
No backend files were touched.

**Backend (dotnet test):**
```
Passed!  - Failed: 0, Passed: 230, Skipped: 0, Total: 230  (unit)
Passed!  - Failed: 0, Passed: 459, Skipped: 0, Total: 459  (integration)
Duration: ~4 m 18 s
```
459 of 459 passing. The pre-existing `TextMode_AllFields_Creates201DraftTalk` failure resolved in the post-Chunk-3 IsActive follow-up commit (`e6c2d38`). Clean baseline confirmed.

**Frontend (TypeScript):**
```
npx tsc --noEmit  →  0 errors (clean, no output)
```

**Frontend (vitest):**
```
Test Files  3 passed (3)
Tests       15 passed (15)
Duration    2.47s
```

**Frontend (Playwright e2e):**
```
1 passed (1.4s)
[chromium] › e2e\login-page.spec.ts:3:5 › login page renders
```
1/1. Clean. Playwright webServer config auto-spawns the dev server.

---

## 2. Files Changed in Scope

| Path | Change summary |
|------|---------------|
| `web/src/features/toolbox-talks/components/ToolboxTalkDetail.tsx` | Added `Alert`/`AlertTitle`/`AlertDescription` import; added `useWorkflowSubscription` import; added hook call + `hasStaleTranslation` derived value; added amber banner in Overview tab between Talk Details grid and SectionEditPanel. |
| `BACKLOG.md` | Updated §24 Chunk status note (line 1784) to include Chunk 6 and toggle-flip cut completion. Updated §5.27 adjacent-dependencies list (line 1089) to mark §24 gate cleared. Updated Phase 5 status note (line 37). |

---

## 3. Files Changed Outside Stated Scope

None.

---

## 4. BACKLOG Impact

**§24 status:** Open. Chunks 1, 2, 3, and 6 shipped. Remaining: 4 (settings inline edit), 5 (add target language post-publish). Neither is required for Demo or toggle-flip.

**Toggle-flip cut status:** Engineering-complete as of this chunk. §5.27's prerequisite gate on §24 is cleared. Flipping any tenant to `UseNewWizard = "true"` is now an operational decision.

**BACKLOG.md line 1784:** Updated to read "Chunks 1, 2, 3, and 6 shipped 2026-06-18. Demo cut and toggle-flip cut both engineering-complete."

**BACKLOG.md §5.27 adjacent-dependencies (line 1089):** §24 bullet updated from "← remaining gate" to "✅ engineering gate cleared 2026-06-18".

**BACKLOG.md Phase 5 status (line 37):** Updated to note "§24 engineering gate cleared 2026-06-18; toggle-flip is now an operational decision."

---

## 5. Build Output

**Frontend:** TypeScript clean — 0 errors. No new warnings.

**Backend:** 0 errors. 11 pre-existing warnings unchanged.

---

## 6. Implementation Report

### What landed

One frontend component modified (`ToolboxTalkDetail.tsx`). Three distinct additions:

**1. Imports (lines 19, 35, 37)**
- `Alert`, `AlertTitle`, `AlertDescription` from `@/components/ui/alert`
- `useWorkflowSubscription` from `./learning-wizard/hooks/useWorkflowSubscription`

**2. Hook call + derived value (lines 61–63)**
```typescript
const { data: workflowStates, isLoading: workflowStatesLoading } = useWorkflowSubscription(talkId);
...
const hasStaleTranslation = !workflowStatesLoading
  && (workflowStates ?? []).some(s => s.state === 'Stale');
```
`useWorkflowSubscription` uses query key `[...TOOLBOX_TALKS_KEY, talkId, 'workflow-state']` — same key as `TranslateStep` and `ValidateStep`. TanStack Query deduplicates: no additional network request when all three consumers are rendered simultaneously.

**3. Amber banner in Overview tab (inside `TabsContent value="overview"`, between Talk Details grid and `SectionEditPanel`)**
```tsx
{hasStaleTranslation && (
  <Alert className="border-amber-300 bg-amber-50 dark:border-amber-700 dark:bg-amber-950/30">
    <AlertTriangleIcon className="h-4 w-4 text-amber-600" />
    <AlertTitle className="text-amber-800 dark:text-amber-300">
      One or more translations are outdated
    </AlertTitle>
    <AlertDescription className="text-amber-700 dark:text-amber-400">
      Content changes have been made since the last translation run.
      Re-run translations before scheduling new assignments.
    </AlertDescription>
  </Alert>
)}
```

### Decisions made

**Alert variant:** shadcn `Alert` has only `default` and `destructive` variants — no `warning`. Used the project's established amber convention: `className="border-amber-300 bg-amber-50 dark:border-amber-700 dark:bg-amber-950/30"` with amber text on title and description. This pattern is used identically in `ExternalReviewWarningBanner` (PublishStep.tsx:423), `ScheduleDialog.tsx:263`, `AssignCourseDialog.tsx:261`, and `InputConfigStep.tsx:792`.

**Icon name:** `AlertTriangleIcon` (lucide-react). Already imported in `ToolboxTalkDetail.tsx` for the Overdue stat card (line 13). Used the same import rather than adding `AlertTriangle` (which `PublishStep.tsx` uses — both resolve to the same lucide component; the `Icon` suffix is the project's established convention in this file).

**Banner placement:** Inside `TabsContent value="overview"`, between the Talk Details + Recent Completions grid and the `SectionEditPanel`. This puts the banner at the first point below the stats summary where the edit consequence is visible — on the same surface as the editable content that caused the stale state.

**Banner not on Translations or Validation tabs:** Correct per spec. Those tabs already surface per-language stale state via `WizardTranslationPanel` badges and `ValidateStep`'s tab strip. The banner aggregates: "one or more" — precision lives on the respective tabs.

**Loading guard:** `hasStaleTranslation = !workflowStatesLoading && ...` — banner is suppressed while the query is in flight. No flashing false-negative on first render.

### File:line conformance citations

| Claim | File:Line |
|-------|-----------|
| `Alert`/`AlertTitle`/`AlertDescription` import | [ToolboxTalkDetail.tsx:19](web/src/features/toolbox-talks/components/ToolboxTalkDetail.tsx#L19) |
| `useWorkflowSubscription` import | [ToolboxTalkDetail.tsx:37](web/src/features/toolbox-talks/components/ToolboxTalkDetail.tsx#L37) |
| Hook call | [ToolboxTalkDetail.tsx:61](web/src/features/toolbox-talks/components/ToolboxTalkDetail.tsx#L61) |
| `hasStaleTranslation` derived value | [ToolboxTalkDetail.tsx:63–64](web/src/features/toolbox-talks/components/ToolboxTalkDetail.tsx#L63-L64) |
| Amber banner (Overview tab) | [ToolboxTalkDetail.tsx:350–362](web/src/features/toolbox-talks/components/ToolboxTalkDetail.tsx#L350-L362) |
| BACKLOG §24 shipped note | [BACKLOG.md:1784](BACKLOG.md#L1784) |
| BACKLOG §5.27 gate cleared | [BACKLOG.md:1089](BACKLOG.md#L1089) |
| BACKLOG Phase 5 status | [BACKLOG.md:37](BACKLOG.md#L37) |

---

## 7. Toggle-Flip Cut Completion Note

Chunk 6 closes the engineering prerequisites for flipping any production tenant to the new wizard. The full edit story is now navigable on the talk detail page:

1. **Create** via new wizard (`/admin/toolbox-talks/learnings/**`)
2. **Edit** sections and quiz inline on detail page (Chunk 1)
3. **Re-translate** from Translations tab (Chunk 2 — TranslateStep)
4. **Review validation** from Validation tab (Chunk 3 — ValidateStep)
5. **Stale warning** visible on Overview tab when content changes make translations outdated (Chunk 6 — Rule 8 closed)

`UseNewWizard = "true"` for any tenant is now an operational decision. No engineering work is required first.

---

## 8. Manual Smoke Verification (to run in review session)

**Smoke 1 — Published talk with all translations current:**
Navigate to a Published talk with completed, non-stale translations. Expected: no amber banner on Overview tab.

**Smoke 2 — Published talk with stale translation:**
Edit a section on a Published talk (Chunk 1 panel). Save. Refresh Overview tab. Expected: amber banner "One or more translations are outdated — Content changes have been made since the last translation run. Re-run translations before scheduling new assignments."

**Smoke 3 — Banner does not appear on Translations or Validation tabs:**
On the same stale talk, navigate to Translations tab. Expected: no amber banner. Per-language Stale badge visible in WizardTranslationPanel instead. Same for Validation tab — no banner.

**Smoke 4 — Banner clears after re-translation:**
From Translations tab, re-run translation for the stale language. After completion, return to Overview tab. Expected: amber banner gone.

**Smoke 5 — Talk with no target languages:**
Navigate to a Published talk with no `targetLanguageCodes`. Expected: no banner shown (no workflow states exist to be Stale).

**Smoke 6 — Loading state does not flash banner:**
Hard-refresh the Overview tab. Expected: banner does not flash during initial load (guarded by `!workflowStatesLoading`).
