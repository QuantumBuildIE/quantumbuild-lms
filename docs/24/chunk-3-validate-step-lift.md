# §24 Chunk 3 — ValidateStep Lift to Talk Detail Page

_Date: 2026-06-18_
_Branch: transval_
_Author: Claude Code_

---

## 1. Test Results

**Backend (dotnet build):**
```
11 Warning(s)  (all pre-existing)
0 Error(s)
Time Elapsed 00:00:36.80
```
No backend files were touched. Warning count unchanged from prior chunks.

**Backend (dotnet test):**
```
Failed: 1, Passed: 458, Skipped: 0, Total: 459, Duration: 4 m 16 s
```
1 pre-existing failure: `InitialiseToolboxTalkCommandHandlerTests.TextMode_AllFields_Creates201DraftTalk` — asserts `result.IsActive` is False but finds True. Unrelated to this chunk (no backend files changed). Identical failure seen in prior chunks.

**Frontend (TypeScript):**
```
npx tsc --noEmit  →  0 errors (clean, no output)
```

**Frontend (vitest):**
```
Test Files  3 passed (3)
Tests       15 passed (15)
Duration    39.75s
```

**Frontend (Playwright e2e):**
```
1 failed  [chromium] › e2e\login-page.spec.ts:3:5 › login page renders
```
Pre-existing failure: dev server not running at localhost:3000 (same as prior chunks — per Note 30, Playwright requires the Next.js dev server and .NET API to be running). Unrelated to Chunk 3.

---

## 2. Files Changed in Scope

| Path | Change summary |
|------|---------------|
| `web/src/features/toolbox-talks/components/ToolboxTalkDetail.tsx` | Added `Separator`, `ValidateStep` imports; added `useValidationRuns` import and hook call; replaced flat `ValidationHistoryTab` render in Validation tab with three-branch conditional (loading skeleton / empty-state note / ValidateStep + Separator + ValidationHistoryTab). |
| `BACKLOG.md` | Updated §24 Chunk 1+2 shipped note to include Chunk 3 and the Demo cut completion. |

---

## 3. Files Changed Outside Stated Scope

None.

---

## 4. BACKLOG Impact

**§24 status:** Open. Chunks 1, 2, and 3 shipped. Remaining: 4 (settings), 5 (add target language), 6 (Rule-8 stale gate).

**BACKLOG.md line 1784 updated:** Note now reads "Chunks 1 (inline section & quiz edit), 2 (TranslateStep lift), and 3 (ValidateStep lift) shipped 2026-06-17. Demo cut complete."

**Demo cut status:** Complete from an engineering standpoint. The new wizard's edit story is demonstrably end-to-end: create → edit content (Chunk 1) → re-run translations (Chunk 2) → review validation results (Chunk 3). Demo's own operational tasks per §5.7 remain (env vars, DB backup, Railway deploy).

**Toggle-flip cut additionally requires:** Chunk 6 (Rule-8 stale gate).

---

## 5. Build Output

**Frontend:** TypeScript clean — 0 errors. No new warnings.

**Backend:** 0 errors. 11 pre-existing warnings unchanged.

---

## 6. Implementation Report

### What landed

One frontend file modified. Three additions to `ToolboxTalkDetail.tsx`:

1. **Imports** — `Separator` from `@/components/ui/separator`; `ValidateStep` from `./learning-wizard/steps/ValidateStep`; `useValidationRuns` from `@/lib/api/toolbox-talks/use-content-creation`.

2. **Hook call** — `useValidationRuns(talkId)` called at component level (line 58), alongside `useToolboxTalk` and `useDeleteToolboxTalk`. Returns `{ data: validationRuns, isLoading: validationRunsLoading }`. TanStack Query deduplicates — no extra network request when `ValidateStep` (which calls the same hook internally) is rendered.

3. **Validation tab conditional** (lines 355–375) — three branches:
   - `validationRunsLoading`: three skeleton rows (matches `ValidationHistoryTab`'s own loading state shape)
   - `(validationRuns ?? []).length === 0`: dashed-border instructional note "No validation runs yet. Start a translation to begin validation." (replaces `ValidationHistoryTab`'s icon+text empty state; the note does not render `ValidationHistoryTab` — per spec)
   - runs exist: `<ValidateStep talkId={talkId} />` + `<Separator />` + `<ValidationHistoryTab talkId={talkId} basePath={basePath} />` wrapped in `space-y-6` div

### Decisions made

**Loading state** — used three skeleton rows matching the `ValidationHistoryTab` internal loading pattern. Prevents flashing the empty-state note while the `useValidationRuns` query is in flight on first render.

**Visual separator** — used `<Separator />` (shadcn component, confirmed available) between `ValidateStep` and `ValidationHistoryTab`. Provides clear visual delimiter between "current active validation panel" and "run history list". Alternative (gap only via `space-y-6`) was considered but the explicit `Separator` communicates the boundary more clearly for two semantically distinct sections.

**Empty state replaces** `ValidationHistoryTab` — in the zero-runs branch, only the instructional note is rendered. `ValidationHistoryTab` is not also rendered (its own empty state would say redundant things). The instructional note uses a lighter voice ("Start a translation to begin validation") that directs the user to the Translations tab, which is more actionable than the history tab's "Validation runs are created during the content creation workflow."

**Tab visibility unchanged** — `!isPartOfCourse && !previewMode` gate on the Validation tab was not modified.

**Zero changes to `ValidateStep` and `ValidationHistoryTab`** — both components are used as-is. No structural changes required.

### File:line conformance citations

| Claim | File:Line |
|-------|-----------|
| `Separator` import | [ToolboxTalkDetail.tsx:22](web/src/features/toolbox-talks/components/ToolboxTalkDetail.tsx#L22) |
| `ValidateStep` import | [ToolboxTalkDetail.tsx:31](web/src/features/toolbox-talks/components/ToolboxTalkDetail.tsx#L31) |
| `useValidationRuns` import | [ToolboxTalkDetail.tsx:34](web/src/features/toolbox-talks/components/ToolboxTalkDetail.tsx#L34) |
| `useValidationRuns` hook call | [ToolboxTalkDetail.tsx:58](web/src/features/toolbox-talks/components/ToolboxTalkDetail.tsx#L58) |
| Validation tab conditional | [ToolboxTalkDetail.tsx:355–375](web/src/features/toolbox-talks/components/ToolboxTalkDetail.tsx#L355-L375) |
| Empty-state note | [ToolboxTalkDetail.tsx:363–366](web/src/features/toolbox-talks/components/ToolboxTalkDetail.tsx#L363-L366) |
| `ValidateStep` + `Separator` + `ValidationHistoryTab` | [ToolboxTalkDetail.tsx:368–372](web/src/features/toolbox-talks/components/ToolboxTalkDetail.tsx#L368-L372) |
| BACKLOG §24 shipped note | [BACKLOG.md:1784](BACKLOG.md#L1784) |

---

## 7. Demo Cut Completion Note

Chunk 3 closes the engineering prerequisites for Demo (§5.7). The full edit story is now navigable on the talk detail page:

1. **Create** a talk via the new wizard (`/admin/toolbox-talks/learnings/**`)
2. **Edit** sections and quiz inline on the detail page (Chunk 1)
3. **Re-translate** from the Translations tab (Chunk 2 — TranslateStep)
4. **Review validation** from the Validation tab (Chunk 3 — ValidateStep above history)

Demo's own operational tasks per §5.7 remain: 14 missing env vars, DB backup, Railway deploy, and smoke-testing the user creation page.

---

## 8. Manual Smoke Verification (to run in review session)

**Smoke 1 — Published talk with completed validation runs:**
Navigate to a Published talk with completed validation runs. Click Validation tab. Expected: `ValidateStep` renders the per-language tab strip + ValidationSectionCard review cards above the history table. `Separator` visible between them.

**Smoke 2 — Published talk with target languages but no validation runs yet:**
Navigate to a talk with `targetLanguageCodes` set but no validation runs. Click Validation tab. Expected: dashed-border note "No validation runs yet. Start a translation to begin validation." No table rendered.

**Smoke 3 — Trigger re-validation from ValidateStep:**
On the Validation tab of a talk with runs, trigger accept/edit/retry on a section. Expected: SignalR progress updates appear; the run detail updates and the history list below reflects the updated run.

**Smoke 4 — Send for external review:**
On a validated language (state = Validated or ReviewerAccepted), the "Send for external review" button should appear via `ValidateStep`. Expected: `SendExternalReviewDialog` opens and functions normally.

**Smoke 5 — Course-part talk regression:**
Navigate to a talk that is part of a course. Expected: only Overview tab visible — Validation tab absent (same gate as before Chunk 3).

**Smoke 6 — Preview mode regression:**
Open PreviewModal. Expected: only Overview tab visible.
