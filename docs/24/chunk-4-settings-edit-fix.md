# §24 Chunk 4 — Settings Inline Edit: Implementation Report

_Date: 2026-06-18_
_Branch: transval_
_Author: Claude Code_

---

## 1. Test Results

**TypeScript (`npx tsc --noEmit`):**
No output — zero errors.

**Frontend (vitest):**
```
Test Files  3 passed (3)
     Tests  15 passed (15)
  Duration  1.43s
```

**Backend integration tests:**
```
Passed!  - Failed: 0, Passed: 460, Skipped: 0, Total: 460, Duration: 3 m 45 s
```

**Backend unit tests:**
```
Passed!  - Failed: 0, Passed: 230, Skipped: 0, Total: 230, Duration: 323 ms
```

**Backend build:**
```
0 Error(s)
Time Elapsed 00:00:26.37
```
11 pre-existing warnings — none introduced by this chunk.

**Playwright e2e:**
```
1 passed (1.2s)
```

All four runners clean. No new failures introduced. Baseline maintained at 460/460 integration + 230/230 unit.

---

## 2. §A Placement Decision

**Decision: Keep the existing "Talk Details" card; add `SettingsEditPanel` below it (adjacent, not replacing).**

**Rationale:** The "Talk Details" card (ToolboxTalkDetail.tsx lines 244–323) displays a mix of non-settings fields — Frequency, Sections count, Video source/URL, Minimum Watch %, Attachment URL — alongside `requiresQuiz` and `passingScore`. The Chunk 4 scope covers ten specific settings fields, not the media/identity fields in that card. Replacing the card would remove information (video URL, frequency display) that belongs in a read-only summary. Adding adjacent is the cleaner call: the new panel is a separate collapsible edit surface, and the minor visual duplication of `requiresQuiz`/`passingScore` in both the card and the panel view mode is acceptable (the card is read-only display; the panel adds edit capability).

---

## 3. Files Changed in Scope

### New files

| Path | Purpose |
|------|---------|
| `web/src/features/toolbox-talks/components/detail/SettingsEditPanel.tsx` | Inline settings editor on the talk detail page. View mode = read-only grouped display; edit mode = React Hook Form + zod form. Gated on `Learnings.Manage`. Refetch-on-open, dirty-check via `form.formState.isDirty`, confirm-discard dialog. |

### Modified files

| Path | Change summary |
|------|---------------|
| `web/src/features/toolbox-talks/components/ToolboxTalkDetail.tsx` | Added `SettingsEditPanel` import; added `<SettingsEditPanel talk={talk} onRefetch={refetch} />` in the Overview tab between the stale banner and SectionEditPanel. |
| `BACKLOG.md` | Updated §24 shipped-chunks note to include Chunk 4; updated remaining-chunks note to Chunk 5 only. |

---

## 4. Files Changed Outside Stated Scope

None.

---

## 5. BACKLOG Impact

**§24 status:** Open. Chunks 1, 2, 3, 4, and 6 shipped 2026-06-18. Remaining: Chunk 5 (add target language post-publish — only chunk needing new backend endpoints). Not required for Demo or toggle-flip.

---

## 6. Build Output

**Frontend:** `npx tsc --noEmit` — zero output (no errors). vitest 15/15 pass.

**Backend:** `dotnet build` — 0 Errors, 11 pre-existing warnings (none introduced by this chunk).

---

## 7. Notable Implementation Decisions

### A. React Hook Form + zod for validation

Unlike SectionEditPanel and QuizEditPanel (which use manual state + JSON dirty-check), SettingsEditPanel uses `react-hook-form` + `zod` because it has numeric fields requiring range validation (`passingScore`, `refresherIntervalMonths`, `autoAssignDueDays`). This aligns with the wizard's QuizSettingsPanel and SettingsStep patterns.

Dirty check uses `form.formState.isDirty` (react-hook-form built-in, comparing against the `reset()` baseline). Equivalent to the JSON comparison in Chunk 1 panels but cleaner for a form library.

### B. Validation ranges

- `passingScore`: `z.number().int().min(0).max(100)` — per prompt specification
- `refresherIntervalMonths`: `z.number().int().min(1).max(60)` — up to 5 years; no legacy form reference so a reasonable cap was applied
- `autoAssignDueDays`: `z.number().int().min(1).max(365)` — matching legacy form's `.min(1).max(365)`

### C. Refresher conditional — SettingsEditPanel correctly trips the backend handler

The backend `UpdateToolboxTalkCommandHandler` conditional is:
```
if (request.RequiresRefresher || request.RefresherIntervalMonths != 12)
```

SettingsEditPanel sends BOTH `requiresRefresher` and `refresherIntervalMonths` explicitly in the save payload (lines 170–171 of SettingsEditPanel.tsx). This means:
- User changes `requiresRefresher` to true: conditional fires → uses (true, intervalMonths) directly ✓
- User changes `refresherIntervalMonths` to 3: conditional fires (3 != 12) → uses (requiresRefresher, 3) directly ✓
- User leaves both at defaults (false, 12): conditional does NOT fire → mapper runs with `frequency: talk.frequency` → (false, 12) ✓

This is the correct behavior documented in `backend-fix-allowretry-refresher.md`.

### D. Parent-toggle conditional rendering in edit mode

- When `requiresQuiz` is false: `passingScore`, `shuffleQuestions`, `shuffleOptions`, `useQuestionPool`, `allowRetry` are hidden (conditional render via `form.watch('requiresQuiz')`)
- When `requiresRefresher` is false: `refresherIntervalMonths` is hidden
- In view mode: same conditional display — child fields only shown when parent toggle is enabled

### E. Full payload construction

SettingsEditPanel follows the same full-payload pattern as Chunk 1 panels: spreads all current talk fields, overrides the 10 settings fields with form values. Sections and questions are preserved unchanged. `allowRetry` is explicitly set from `values.allowRetry` (not from `talk.allowRetry`) — consistent with the backend-fix commit requirement.

### F. Panel ordering in Overview tab

Order after this chunk:
1. Grid (Talk Details card + Recent Completions card)
2. Stale-translation banner (Chunk 6)
3. **SettingsEditPanel** (Chunk 4 — NEW)
4. SectionEditPanel (Chunk 1)
5. QuizEditPanel (Chunk 1)

Settings come before sections/quiz because they configure behaviour (quiz passing, refresher policy, certificate) rather than content.

---

## 8. File:Line Conformance Citations

| Claim | File:Line |
|-------|-----------|
| Permission gate | [SettingsEditPanel.tsx:116](web/src/features/toolbox-talks/components/detail/SettingsEditPanel.tsx#L116) — `usePermission('Learnings.Manage')` |
| Refetch on open | [SettingsEditPanel.tsx:122](web/src/features/toolbox-talks/components/detail/SettingsEditPanel.tsx#L122) — `onRefetch()` |
| Form reset on open | [SettingsEditPanel.tsx:123](web/src/features/toolbox-talks/components/detail/SettingsEditPanel.tsx#L123) — `form.reset(talkToFormData(talk))` |
| Save disabled when not dirty | [SettingsEditPanel.tsx:193](web/src/features/toolbox-talks/components/detail/SettingsEditPanel.tsx#L193) — `disabled={updateMutation.isPending \|\| !form.formState.isDirty}` |
| requiresRefresher + refresherIntervalMonths explicit in payload | [SettingsEditPanel.tsx:170-171](web/src/features/toolbox-talks/components/detail/SettingsEditPanel.tsx#L170-L171) |
| allowRetry explicit in payload | [SettingsEditPanel.tsx:169](web/src/features/toolbox-talks/components/detail/SettingsEditPanel.tsx#L169) |
| Parent-toggle conditional (quiz) | [SettingsEditPanel.tsx:216](web/src/features/toolbox-talks/components/detail/SettingsEditPanel.tsx#L216) — `{requiresQuiz && (` |
| Parent-toggle conditional (refresher) | [SettingsEditPanel.tsx:279](web/src/features/toolbox-talks/components/detail/SettingsEditPanel.tsx#L279) — `{requiresRefresher && (` |
| SettingsEditPanel wired in ToolboxTalkDetail | [ToolboxTalkDetail.tsx:356](web/src/features/toolbox-talks/components/ToolboxTalkDetail.tsx#L356) |

---

## 9. Manual Smoke Verification (deferred to post-commit review session)

- Navigate to a Published talk; open SettingsEditPanel edit mode; edit `passingScore` from current to a new valid value; save; verify view mode reflects the new value
- Edit `refresherIntervalMonths` from 12 to 3 via the panel; save; verify the talk now has `refresherIntervalMonths = 3` (NOT 1 — confirming the backend conditional honors the explicit value, not the frequency mapper)
- Edit `allowRetry` from true to false; save; verify the talk's AllowRetry is persisted as false
- Verify parent-toggle hide: set `requiresQuiz` to false → confirm `passingScore` and other quiz sub-fields disappear from edit form
- Verify parent-toggle hide: set `requiresRefresher` to false → confirm `refresherIntervalMonths` disappears
- Verify validation: try to save `passingScore = 150` → should show validation error and prevent save
- Verify cancel-with-confirm: make an edit, click Cancel, confirm discard; verify changes discarded
- Verify permission gate: log in as Supervisor; verify Settings panel's view mode is visible but no "Edit Settings" button
- Verify settings changes do NOT show stale banner (settings scalar changes do not cascade MarkStale per `UpdateToolboxTalkCommandHandler` logic — quiz-settings changes are not in the staleness detection path)
