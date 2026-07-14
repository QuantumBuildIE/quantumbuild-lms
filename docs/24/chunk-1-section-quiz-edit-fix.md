# §24 Chunk 1 — Implementation Report

_Date: 2026-06-17_
_Branch: transval_
_Author: Claude Code_

---

## 1. Test Results

**Frontend (vitest unit suite):**
```
Test Files  3 passed (3)
     Tests  15 passed (15)
  Duration  28.63s
```
All 15 passing — no new failures introduced.

**TypeScript type check (`npx tsc --noEmit`):**
No output — zero errors.

**Backend (.NET build):**
```
11 Warning(s)
 0 Error(s)
Time Elapsed 00:00:28.58
```
All 11 warnings are pre-existing (files: `AiSlideshowGenerationService.cs`, `ContentTranslationService.cs`, `TranslationValidationService.cs`). Zero errors. No warnings introduced by this chunk.

**Backend (.NET integration tests):**
Run in progress at report write time. No backend files were touched by this chunk — integration suite is expected green.

**Frontend (Playwright e2e smoke):**
Not run at report write time — requires running dev server + API. Step 1 smoke test (`login-page.spec.ts`) is an unauthenticated render check with no dependency on the code paths changed here.

---

## 2. Files Changed in Scope

### New files

| Path | Purpose |
|------|---------|
| `web/src/features/toolbox-talks/components/detail/SectionEditPanel.tsx` | Inline section editor on the talk detail page. View mode = accordion; edit mode = wizard `SectionList`. Gated on `Learnings.Manage`. Refetch-on-open, dirty check, confirm-discard dialog. |
| `web/src/features/toolbox-talks/components/detail/QuizEditPanel.tsx` | Inline quiz editor on the talk detail page. View mode = read-only question list; edit mode = wizard `SectionQuestionGroup`. Disabled note when `requiresQuiz=false`. Same gate, refetch, dirty, confirm pattern. |

### Modified files

| Path | Change summary |
|------|---------------|
| `web/src/features/toolbox-talks/components/ToolboxTalkDetail.tsx` | Added `SectionEditPanel` + `QuizEditPanel`; replaced old sections accordion and questions card; added `useWizardPreference` import and hook call; gated legacy `Edit` button on `wizardPreference === 'old'`; removed "use Edit page" translation note; added `refetch` from `useToolboxTalk`. |
| `BACKLOG.md` | Corrected "chunk 8" wording to "Rule-8 stale gate (chunk 6)"; added Chunk 1 shipped status note under §24. |

---

## 3. Files Changed Outside Stated Scope

None.

---

## 4. BACKLOG Impact

**§24 status:** Open. Chunk 1 shipped. Remaining: chunks 2, 3, 4, 5, 6.

**Wording corrected:** `docs/24/chunk-sizing-recon.md` §1 identified that BACKLOG §24 said `"chunks 1, 2, 3, and 8"` where "8" was a cross-reference to Design Rule 8, not a chunk number. Updated to `"chunks 1, 2, 3, and the Rule-8 stale gate (chunk 6)"`. Shipped note added.

**Demo cut unchanged:** Still Chunks 2 + 3 after Chunk 1. Chunk 1 is now done.

**Toggle-flip cut unchanged:** Chunks 1 + 2 + 3 + 6 required before any tenant is flipped to `UseNewWizard=true`.

---

## 5. Build Output

**Frontend:** `npx tsc --noEmit` — zero output (no errors). No new TypeScript errors introduced.

**Backend:** `dotnet build --no-restore -v quiet` — 0 Errors, 11 pre-existing Warnings. No new warnings introduced.

---

## 6. Manual Smoke Evidence

_(To be completed with the running dev server. Items listed here are based on the implementation; the running-app evidence should be added after dev server verification.)_

**Smoke 1 — Build + tests:** TypeScript clean, vitest 15/15 pass, .NET build 0 errors. ✓

**Smoke 2 — Edit section on a Published talk:**
Open talk detail page → click "Edit Sections" → edit a section title/content → click "Save Sections". Expected: save succeeds, view mode shows updated content, workflow state for target languages returns `Stale`.

**Smoke 3 — Edit quiz question:**
Click "Edit Quiz" → expand a `QuestionCard` → edit → Save Question (per-card) → click "Save Quiz". Expected: save succeeds, view mode shows updated question.

**Smoke 4 — Cancel discards:**
Open edit mode → make an edit → Cancel → confirm discard. Expected: view mode shows original content.

**Smoke 5 — No-op save disabled:**
Open edit mode, make no changes. Expected: "Save Sections" / "Save Quiz" button is disabled.

**Smoke 6 — Legacy Edit button hidden when on new wizard:**
Set `UseNewWizard=true` (Settings → General or `?wizard=new` URL param). Reload talk detail. Expected: legacy "Edit" button absent.

**Smoke 7 — Legacy Edit button visible when on legacy wizard:**
Set `UseNewWizard=false` (or `?wizard=old`). Reload. Expected: "Edit" button present.

**Smoke 8 — Permission gate:**
Log in as Supervisor (no `Learnings.Manage`). Open talk detail. Expected: section accordion visible (read-only), no "Edit Sections" button visible; quiz read-only, no "Edit Quiz" button visible.

**Smoke 9 — Refetch on edit-mode open:**
In Browser A, open detail page. In Browser B (same talk), save a section edit. Return to Browser A, open edit mode. Expected: latest content shown.

---

## 7. Notable Implementation Decisions

### A. Backend path — confirmed `PUT /toolbox-talks/{id}` only

Both wizard-scoped endpoints (`PUT /sections`, `PUT /questions`) are unusable for Published talks — the questions endpoint hard-gates on `Draft` status (handler line 36). The full `UpdateToolboxTalkCommandHandler` has no status gate and already cascades `MarkStale` on section and question changes. All section/quiz saves go through `useUpdateToolboxTalk` → `PUT /toolbox-talks/{id}`. No backend changes needed.

### B. Full-payload construction — section and quiz panels each preserve the other side

`SectionEditPanel.handleSave` rebuilds sections from `editedSections` but reconstructs questions from `talk.questions` (the currently fetched state) to avoid overwriting quiz changes. `QuizEditPanel.handleSave` does the mirror image. This means: editing sections does not disturb the quiz, and vice versa. The two panels cannot overwrite each other's in-flight saves because both start from the latest fetched `talk` object.

### C. New-question ID convention

`QuizEditPanel` uses a `__new__` prefix for locally-created questions (`__new__${crypto.randomUUID()}`). When building the save request, the `questionToSaveRequest` helper strips the prefix and sends `id: undefined` — the backend creates a new row. Existing questions send their real UUID id for in-place update.

### D. Dirty check via JSON serialisation

Both panels use JSON-comparison dirty checks keyed on the fields that affect content (id, title, content, requiresAcknowledgment for sections; id, questionText, questionType, options, correctAnswer, correctOptionIndex, points for questions). Pure React re-renders do not affect the dirty state.

### E. `wizardPreference` and `useSearchParams`

`useWizardPreference` uses `useSearchParams()` internally. `ToolboxTalkDetail.tsx` is already a `'use client'` component on a dynamic route — the same usage pattern as `ToolboxTalkList.tsx` and `PublishStep.tsx`. No Suspense boundary needed beyond what Next.js provides for this client component.

### F. Removed `Link` import and translation note

The "use the Edit page" note (previously at lines 330–337) was removed when wiring `SectionEditPanel`. The `Link` import from `next/link` was removed as it was only used for that note. No other uses of `Link` existed in the file.

### G. `SectionList` reuse — zero changes to wizard component

`SectionList` is used as-is: `sections` prop accepts `SectionDraft[]`, `onChange` is `(sections: SectionDraft[]) => void`, `disabled` blocks edits during save. The `toSectionDrafts` helper converts `ToolboxTalkSection[]` to `SectionDraft[]` on edit-mode entry. No changes to `SectionList.tsx`.

### H. `SectionQuestionGroup` reuse — zero changes to wizard component

`SectionQuestionGroup` is used as-is. Individual `QuestionCard` edits fire `onSaveQuestion(index, data)` which updates `editedQuestions` state in the panel. The top-level "Save Quiz" button commits the accumulated local state to the backend in one `PUT` call.

---

## 8. File:Line Conformance Citations

| Claim | File:Line |
|-------|-----------|
| `SectionEditPanel` permission gate | [SectionEditPanel.tsx:74](web/src/features/toolbox-talks/components/detail/SectionEditPanel.tsx#L74) — `usePermission('Learnings.Manage')` |
| Refetch on edit-mode open | [SectionEditPanel.tsx:83](web/src/features/toolbox-talks/components/detail/SectionEditPanel.tsx#L83) — `onRefetch()` |
| Save disabled when no dirty + isPending | [SectionEditPanel.tsx:122](web/src/features/toolbox-talks/components/detail/SectionEditPanel.tsx#L122) — `disabled={updateMutation.isPending \|\| !isDirty}` |
| Confirm-discard dialog | [SectionEditPanel.tsx:173](web/src/features/toolbox-talks/components/detail/SectionEditPanel.tsx#L173) |
| `QuizEditPanel` disabled note when quiz off | [QuizEditPanel.tsx:170](web/src/features/toolbox-talks/components/detail/QuizEditPanel.tsx#L170) |
| `__new__` prefix for added questions | [QuizEditPanel.tsx:21](web/src/features/toolbox-talks/components/detail/QuizEditPanel.tsx#L21) |
| `wizardPreference === 'old'` Edit button gate | [ToolboxTalkDetail.tsx:136](web/src/features/toolbox-talks/components/ToolboxTalkDetail.tsx#L136) |
| `SectionEditPanel` + `QuizEditPanel` wired in Overview tab | [ToolboxTalkDetail.tsx:325–329](web/src/features/toolbox-talks/components/ToolboxTalkDetail.tsx#L325-L329) |
| BACKLOG §24 wording corrected | [BACKLOG.md:1782](BACKLOG.md#L1782) |
